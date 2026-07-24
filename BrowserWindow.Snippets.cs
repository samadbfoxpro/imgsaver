using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Runtime.InteropServices;

namespace imgsaver
{
    public partial class BrowserWindow
    {
        private void InjectSnippetHelperScript(WebView2 webView)
        {
            bool showMiniClipImageImportButtons = _currentSettings?.ShowMiniClipImageImportButtons == true;
            bool showQuickPasteButton = _currentSettings?.ShowQuickPasteButton == true;
            string script = BuildSnippetHelperScript(showMiniClipImageImportButtons, showQuickPasteButton);
            webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(script);
            webView.CoreWebView2.ExecuteScriptAsync(script);

            // BELT-AND-SUSPENDERS: AddScriptToExecuteOnDocumentCreatedAsync is documented
            // to run in all frames including iframes, but in practice we've observed it
            // NOT reaching seaart.ai's same-origin iframe (id="myIframe", src=".../comfyui/")
            // that hosts its LiteGraph/ComfyUI canvas editor -- no [IFRAME] hook log ever
            // appears for that site even after minutes of interaction, while it works fine
            // on every other site tested (Google, Facebook, etc). Rather than depend on that
            // behavior, we ALSO explicitly inject into every child frame the moment it's
            // created, using the frame-scoped CoreWebView2Frame.ExecuteScriptAsync API. This
            // doesn't rely on any "applies to all frames" assumption at all.
            if (!_frameCreatedHooked.Contains(webView.CoreWebView2))
            {
                _frameCreatedHooked.Add(webView.CoreWebView2);
                webView.CoreWebView2.FrameCreated += CoreWebView2_FrameCreated;
            }
        }

        private readonly System.Collections.Generic.HashSet<CoreWebView2> _frameCreatedHooked = new System.Collections.Generic.HashSet<CoreWebView2>();
        private long _lastQuickPasteTime = 0;

        private void CoreWebView2_FrameCreated(object? sender, CoreWebView2FrameCreatedEventArgs e)
        {
            DebugLog($"[C#] FrameCreated: Name='{e.Frame.Name}'");

            e.Frame.NavigationStarting += (s3, e3) =>
            {
                DebugLog($"[C#] Frame NavigationStarting: Uri='{e3.Uri}'");
            };

            // 1) Inject immediately in case DOM is already loaded
            _ = InjectIntoFrameAsync(e.Frame);

            // 2) Inject on DOMContentLoaded for subsequent page loads
            e.Frame.DOMContentLoaded += async (s2, e2) =>
            {
                await InjectIntoFrameAsync(e.Frame);
            };
        }

        private async System.Threading.Tasks.Task InjectIntoFrameAsync(CoreWebView2Frame frame)
        {
            try
            {
                bool showMiniClip = _currentSettings?.ShowMiniClipImageImportButtons == true;
                bool showQuickPaste = _currentSettings?.ShowQuickPasteButton == true;
                string script = BuildSnippetHelperScript(showMiniClip, showQuickPaste);
                string result = await frame.ExecuteScriptAsync(script);
                DebugLog($"[C#] Injected Quick Paste script into frame '{frame.Name}', ExecuteScriptAsync result='{result}'");
            }
            catch (Exception ex)
            {
                DebugLog($"[C#] EXCEPTION injecting into frame '{frame.Name}': " + ex.Message);
            }
        }

        private string BuildSnippetHelperScript(bool showMiniClipImageImportButtons, bool showQuickPasteButton)
        {
            string script = @"
                (function() {
                  try {
                    function dbg(msg) {}

                    // DIAGNOSTIC CROSS-FRAME MESSAGE FORWARDER WITH RECURSIVE BUBBLING
                    window.addEventListener('message', (e) => {
                        if (!e.data) return;
                        if (window === window.top) {
                            if (e.data.type === 'imgsaver_dbg') {
                                dbg('[Frame Log] ' + e.data.msg);
                            }
                            if (e.data.type === 'imgsaver_keyup') {
                                if (window.chrome && window.chrome.webview) {
                                    window.chrome.webview.postMessage({ type: 'keyup', key: e.data.key });
                                }
                            }
                            if (e.data.type === 'imgsaver_quick_paste_click') {
                                dbg('[Frame Log] Forwarding quick_paste_click from frame');
                                if (window.chrome && window.chrome.webview) {
                                    window.chrome.webview.postMessage({
                                        type: 'quick_paste_click',
                                        x: e.data.x,
                                        y: e.data.y
                                    });
                                }
                            }
                        } else {
                            if (['imgsaver_dbg', 'imgsaver_keyup', 'imgsaver_quick_paste_click'].includes(e.data.type)) {
                                try {
                                    window.parent.postMessage(e.data, '*');
                                } catch (err) {}
                            }
                        }
                    });

                    // Diagnostic marker disabled

                    const imgsaverShowMiniClipImageImportButtons = __SHOW_MINI_CLIP_IMAGE_IMPORT_BUTTONS__;
                    window.imgsaver_insertSnippet = function(text, keyLength) {
                        const getActive = (el = document.activeElement) => 
                            el && el.shadowRoot && el.shadowRoot.activeElement ? getActive(el.shadowRoot.activeElement) : el;
                        const target = getActive();
                        if (!target) return;
                        if (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA') {
                            const start = target.selectionStart;
                            target.setSelectionRange(start - keyLength, start);
                            let ok = false;
                            try { ok = document.execCommand('insertText', false, text); } catch(e) {}
                            if (!ok) {
                                const val = target.value;
                                const newVal = val.slice(0, start - keyLength) + text + val.slice(start);
                                const prototype = target.tagName === 'TEXTAREA' ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype;
                                const desc = Object.getOwnPropertyDescriptor(prototype, 'value');
                                if (desc && desc.set) { desc.set.call(target, newVal); } else { target.value = newVal; }
                                const newPos = start - keyLength + text.length;
                                target.setSelectionRange(newPos, newPos);
                            }
                            ['input', 'change'].forEach(ev => target.dispatchEvent(new Event(ev, { bubbles: true })));
                        } else if (target.isContentEditable) {
                            for(let i=0; i<keyLength; i++) { document.execCommand('delete', false, null); }
                            document.execCommand('insertText', false, text);
                        }
                    };
                    if (!window.imgsaver_hooked) {
                        window.addEventListener('keyup', e => {
                            if (e.key.length === 1 || e.key === 'Backspace' || e.key === 'Enter' || e.key === 'Tab' || e.key === 'Escape' || e.key === ' ') {
                                if (window === window.top) {
                                    if (window.chrome && window.chrome.webview) {
                                        window.chrome.webview.postMessage({ type: 'keyup', key: e.key });
                                    }
                                } else {
                                    try {
                                        window.parent.postMessage({ type: 'imgsaver_keyup', key: e.key }, '*');
                                    } catch (err) {}
                                }
                            }
                        }, true);
                        window.imgsaver_hooked = true;
                    }
                    // --- Quick Paste Helper Button Feature ---
                    // NOTE: moved above the Mini Clip early-return below, so this feature keeps
                    // working even when ShowMiniClipImageImportButtons is disabled.
                    if (!window.imgsaver_quickPasteHooked) {
                        (function() {
                            if (!__SHOW_QUICK_PASTE_BUTTON__) return;
                            dbg('Quick Paste script hooked on (' + location.href + (window !== window.top ? ' [IFRAME]' : ' [TOP]') + ')');
                            let activeInput = null;
                            let button = null;
                            let lastPasteTime = 0;

                            function createButton() {
                                if (button) return;
                                button = document.createElement('div');
                                button.innerText = '📋 BR Paste';
                                button.style.position = 'absolute';
                                button.style.zIndex = '999999999';
                                button.style.background = 'linear-gradient(135deg, #FF9800, #F57C00)';
                                button.style.color = 'white';
                                button.style.fontWeight = 'bold';
                                button.style.fontSize = '14px';
                                button.style.padding = '10px 18px';
                                button.style.borderRadius = '24px';
                                button.style.boxShadow = '0 5px 15px rgba(0,0,0,0.4)';
                                button.style.cursor = 'pointer';
                                button.style.opacity = '0.85';
                                button.style.transition = 'opacity 0.2s, transform 0.1s';
                                button.style.userSelect = 'none';
                                button.style.pointerEvents = 'auto';

                                button.addEventListener('mouseenter', () => button.style.opacity = '1.0');
                                button.addEventListener('mouseleave', () => button.style.opacity = '0.85');

                                document.body.appendChild(button);
                            }

                            // Walks up through same-origin iframes to turn a rect that's
                            // local to THIS document into one relative to the top-level
                            // WebView2 document. seaart.ai's canvas editor lives inside a
                            // same-origin iframe (myIframe -> /comfyui/), so window.frameElement
                            // is accessible here and lets us add up the offsets.
                            function getAbsoluteRect(el) {
                                let rect = el.getBoundingClientRect();
                                let x = rect.left, y = rect.top, w = rect.width, h = rect.height;
                                let win = window;
                                while (win !== win.top) {
                                    let frameEl;
                                    try { frameEl = win.frameElement; } catch (err) { break; }
                                    if (!frameEl) break;
                                    const fr = frameEl.getBoundingClientRect();
                                    x += fr.left;
                                    y += fr.top;
                                    win = win.parent;
                                }
                                return { x, y, w, h };
                            }

                            window.imgsaver_blockContextMenu = false;

                            const preventBlur = (e) => {
                                if (!button || (e.target !== button && !button.contains(e.target))) return;
                                e.preventDefault();
                                e.stopImmediatePropagation();
                            };

                            const triggerPaste = (e) => {
                                if (!button || (e.target !== button && !button.contains(e.target))) return;
                                e.preventDefault();
                                e.stopImmediatePropagation();

                                if (!activeInput) return;

                                const now = Date.now();
                                if (now - lastPasteTime < 500) {
                                    return; // Avoid duplicate paste from pointerup + mouseup + touchend
                                }
                                lastPasteTime = now;

                                // Temporarily block all context menus globally for 600ms
                                window.imgsaver_blockContextMenu = true;
                                setTimeout(() => {
                                    window.imgsaver_blockContextMenu = false;
                                }, 600);

                                const r = getAbsoluteRect(activeInput);
                                dbg('BR Paste pressed. Sending physical click at rect=' + JSON.stringify(r));
                                if (window === window.top) {
                                    if (window.chrome && window.chrome.webview) {
                                        window.chrome.webview.postMessage({
                                            type: 'quick_paste_click',
                                            x: r.x + r.w / 2,
                                            y: r.y + r.h / 2
                                        });
                                    }
                                } else {
                                    try {
                                        window.parent.postMessage({
                                            type: 'imgsaver_quick_paste_click',
                                            x: r.x + r.w / 2,
                                            y: r.y + r.h / 2
                                        }, '*');
                                    } catch (err) {
                                        dbg('Error sending message to parent window: ' + err.message);
                                    }
                                }
                            };

                            ['mousedown', 'pointerdown', 'touchstart'].forEach(evName => {
                                document.addEventListener(evName, preventBlur, true);
                            });

                            ['mouseup', 'pointerup', 'touchend'].forEach(evName => {
                                document.addEventListener(evName, triggerPaste, true);
                            });

                            document.addEventListener('contextmenu', (e) => {
                                if (window.imgsaver_blockContextMenu) {
                                    e.preventDefault();
                                    e.stopImmediatePropagation();
                                    return;
                                }
                                if (button && (e.target === button || button.contains(e.target))) {
                                    e.preventDefault();
                                    e.stopImmediatePropagation();
                                }
                            }, true);

                            function positionButton(el) {
                                if (!button) createButton();
                                button.style.display = 'block';

                                const rect = el.getBoundingClientRect();
                                const scrollTop = window.pageYOffset || document.documentElement.scrollTop;
                                const scrollLeft = window.pageXOffset || document.documentElement.scrollLeft;

                                const btnHeight = 38;
                                const top = rect.top + scrollTop - btnHeight - 12;
                                const left = rect.left + scrollLeft + (rect.width - 120) / 2;

                                button.style.top = `${top >= 0 ? top : rect.bottom + scrollTop + 12}px`;
                                button.style.left = `${left >= 0 ? left : 8}px`;
                            }

                            function hideButton() {
                                if (button) {
                                    button.style.display = 'none';
                                }
                            }

                            document.addEventListener('focusin', (e) => {
                                let el = e.target;
                                if (el && (el.tagName === 'INPUT' || el.tagName === 'TEXTAREA' || el.isContentEditable)) {
                                    if (el.tagName === 'INPUT' && ['checkbox', 'radio', 'file', 'submit', 'button', 'image', 'hidden'].includes(el.type)) {
                                        return;
                                    }
                                    dbg('focusin on ' + el.tagName + '.' + el.className);
                                    activeInput = el;
                                    setTimeout(() => {
                                        if (activeInput === el) positionButton(el);
                                    }, 80);
                                }
                            }, true); // capture: run before any capturing listener the page itself
                                      // might use to stop propagation before it reaches us in bubble phase

                            document.addEventListener('focusout', (e) => {
                                setTimeout(() => {
                                    if (document.activeElement !== activeInput) {
                                        hideButton();
                                        activeInput = null;
                                    }
                                }, 250);
                            }, true);

                            // DIAGNOSTIC: log the first couple of clicks in this document no
                            // matter what they hit, so we can tell whether ANY of our listeners
                            // are even reachable in this frame at all (independent of focusin).
                            let __imgsaverClickDbgCount = 0;
                            document.addEventListener('click', (e) => {
                                if (__imgsaverClickDbgCount++ < 5) {
                                    dbg('click seen on ' + (e.target && e.target.tagName) + '.' + (e.target && e.target.className));
                                }
                            }, true);

                            window.addEventListener('resize', () => {
                                if (activeInput) positionButton(activeInput);
                            });
                            window.addEventListener('scroll', () => {
                                if (activeInput) positionButton(activeInput);
                            }, true);

                            window.imgsaver_insertQuickPasteText = function(text) {
                                let el = document.activeElement || activeInput;
                                if (el) {
                                    if (el.tagName === 'INPUT' || el.tagName === 'TEXTAREA') {
                                        const prototype = el.tagName === 'TEXTAREA' ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype;
                                        const desc = Object.getOwnPropertyDescriptor(prototype, 'value');
                                        if (desc && desc.set) { desc.set.call(el, text); } else { el.value = text; }
                                        el.setSelectionRange(text.length, text.length);
                                        ['input', 'change'].forEach(ev => el.dispatchEvent(new Event(ev, { bubbles: true })));
                                    } else if (el.isContentEditable) {
                                        el.innerText = text;
                                        el.dispatchEvent(new Event('input', { bubbles: true }));
                                    }
                                }
                            };
                        })();
                        window.imgsaver_quickPasteHooked = true;
                    }

                    window.imgsaver_miniClipEnabled = imgsaverShowMiniClipImageImportButtons;
                    if (!imgsaverShowMiniClipImageImportButtons) {
                        document.querySelectorAll('[data-imgsaver-mini-clip-import-button=""true""]').forEach(btn => btn.remove());
                        if (window.imgsaver_miniClipButtonsMap) window.imgsaver_miniClipButtonsMap.clear();
                        return;
                    }
                    if (!window.imgsaver_miniClipImageButtonsHooked) {
                        const MIN_IMAGE_SIZE = 360;
                        const MIN_VISIBLE_SIZE = 80;
                        const BUTTON_WIDTH = 32;
                        const BUTTON_HEIGHT = 32;
                        // img -> { btn, onEnter, onLeave }
                        const buttons = window.imgsaver_miniClipButtonsMap = new Map();
                        let hideTimer = null;

                        // Touch screens (and hybrid touch/mouse Windows devices) have no hover state,
                        // so the button can't rely on mouseenter/mouseleave to become visible/tappable.
                        const isCoarsePointer = () => {
                            try {
                                return (window.matchMedia && window.matchMedia('(any-pointer: coarse)').matches) ||
                                    navigator.maxTouchPoints > 0 ||
                                    'ontouchstart' in window;
                            } catch (e) { return false; }
                        };

                        const importIconSvg = '<svg width=""16"" height=""16"" viewBox=""0 0 24 24"" fill=""none"" xmlns=""http://www.w3.org/2000/svg"" style=""flex:none""><path d=""M12 3v10.5"" stroke=""currentColor"" stroke-width=""2.2"" stroke-linecap=""round""/><path d=""M7.5 10.5 12 15l4.5-4.5"" stroke=""currentColor"" stroke-width=""2.2"" stroke-linecap=""round"" stroke-linejoin=""round""/><path d=""M4.5 17.5v1.8c0 .94.76 1.7 1.7 1.7h11.6c.94 0 1.7-.76 1.7-1.7v-1.8"" stroke=""currentColor"" stroke-width=""2.2"" stroke-linecap=""round"" stroke-linejoin=""round""/></svg>';

                        const isElementSane = el => {
                            const style = getComputedStyle(el);
                            return style.visibility !== 'hidden' && style.display !== 'none' && parseFloat(style.opacity || '1') > 0.05;
                        };

                        const canImport = img => {
                            if (!window.imgsaver_miniClipEnabled) return false;
                            if (!img || !img.isConnected || !img.src) return false;
                            if (img.closest('[data-imgsaver-mini-clip-import-button]')) return false;
                            if (img.getAttribute('aria-hidden') === 'true' || img.getAttribute('role') === 'presentation') return false;
                            const src = img.currentSrc || img.src || '';
                            if (!/^(https?:|blob:|data:image\/)/i.test(src)) return false;
                            const naturalWidth = img.naturalWidth || 0;
                            const naturalHeight = img.naturalHeight || 0;
                            if (naturalWidth && naturalHeight && (naturalWidth < MIN_IMAGE_SIZE || naturalHeight < MIN_IMAGE_SIZE)) return false;
                            const rect = img.getBoundingClientRect();
                            if (rect.width < 90 || rect.height < 90) return false;
                            if (!isElementSane(img)) return false;
                            let parent = img.parentElement;
                            let depth = 0;
                            while (parent && depth < 6) {
                                if (!isElementSane(parent)) return false;
                                parent = parent.parentElement;
                                depth++;
                            }
                            return rect.bottom > 0 && rect.right > 0 && rect.top < innerHeight && rect.left < innerWidth;
                        };

                        // Intersect the image's box with every scroll-clipping ancestor so the
                        // button is only ever placed over the part of the image that is actually visible.
                        const getVisibleRect = img => {
                            const r = img.getBoundingClientRect();
                            let left = r.left, top = r.top, right = r.right, bottom = r.bottom;
                            let parent = img.parentElement;
                            let depth = 0;
                            while (parent && parent !== document.documentElement && depth < 12) {
                                const cs = getComputedStyle(parent);
                                if (/(hidden|auto|scroll|clip)/.test(cs.overflowX + cs.overflowY)) {
                                    const pr = parent.getBoundingClientRect();
                                    left = Math.max(left, pr.left);
                                    top = Math.max(top, pr.top);
                                    right = Math.min(right, pr.right);
                                    bottom = Math.min(bottom, pr.bottom);
                                }
                                parent = parent.parentElement;
                                depth++;
                            }
                            left = Math.max(left, 0);
                            top = Math.max(top, 0);
                            right = Math.min(right, innerWidth);
                            bottom = Math.min(bottom, innerHeight);
                            return { left, top, right, bottom, width: right - left, height: bottom - top };
                        };

                        const postImport = img => {
                            const uri = img.currentSrc || img.src;
                            if (uri && window.chrome && window.chrome.webview) {
                                window.chrome.webview.postMessage({ type: 'miniClipImageImport', uri });
                            }
                        };

                        const reveal = btn => {
                            clearTimeout(btn._hideT);
                            btn.style.opacity = '1';
                            btn.style.transform = 'scale(1) translateY(0)';
                        };
                        const conceal = btn => {
                            // No hover state exists on touch, so hiding the button would make it
                            // impossible to tap. Keep it resting in its visible state instead.
                            if (isCoarsePointer()) { reveal(btn); return; }
                            clearTimeout(btn._hideT);
                            btn._hideT = setTimeout(() => {
                                btn.style.opacity = '0';
                                btn.style.transform = 'scale(.92) translateY(2px)';
                            }, 160);
                        };

                        const createButton = img => {
                            const btn = document.createElement('button');
                            btn.type = 'button';
                            btn.dataset.imgsaverMiniClipImportButton = 'true';
                            btn.title = 'Import to Mini Clip';
                            btn.setAttribute('aria-label', 'Import to Mini Clip');
                            btn.innerHTML = importIconSvg;
                            const coarse = isCoarsePointer();
                            btn.style.cssText = [
                                'position:fixed',
                                'z-index:2147483647',
                                'width:' + BUTTON_WIDTH + 'px',
                                'height:' + BUTTON_HEIGHT + 'px',
                                'border-radius:999px',
                                'border:1px solid rgba(255,205,30,.55)',
                                'background:linear-gradient(150deg, rgba(32,30,20,.92), rgba(10,10,10,.86))',
                                'color:#ffd21f',
                                'display:flex',
                                'align-items:center',
                                'justify-content:center',
                                'gap:0',
                                'box-shadow:0 6px 18px rgba(0,0,0,.35), 0 0 0 1px rgba(0,0,0,.2) inset',
                                'backdrop-filter:blur(7px)',
                                'opacity:' + (coarse ? '.92' : '0'),
                                'cursor:pointer',
                                'touch-action:manipulation',
                                'padding:0',
                                'user-select:none',
                                'transform:' + (coarse ? 'scale(1) translateY(0)' : 'scale(.92) translateY(2px)'),
                                'transform-origin:center',
                                'transition:opacity .16s ease,transform .16s ease,box-shadow .16s ease,border-color .16s ease',
                                'pointer-events:auto'
                            ].join(';');
                            btn.addEventListener('mouseenter', () => {
                                reveal(btn);
                                btn.style.borderColor = 'rgba(255,210,31,.95)';
                                btn.style.boxShadow = '0 8px 22px rgba(0,0,0,.4), 0 0 0 1px rgba(255,210,31,.15) inset';
                            }, true);
                            btn.addEventListener('mouseleave', () => {
                                btn.style.borderColor = 'rgba(255,205,30,.55)';
                                btn.style.boxShadow = '0 6px 18px rgba(0,0,0,.35), 0 0 0 1px rgba(0,0,0,.2) inset';
                                conceal(btn);
                            }, true);
                            btn.addEventListener('pointerdown', e => {
                                e.preventDefault();
                                e.stopPropagation();
                                btn.style.transform = 'scale(.93) translateY(0)';
                            }, true);
                            btn.addEventListener('pointerup', e => {
                                e.preventDefault();
                                e.stopPropagation();
                                btn.style.transform = 'scale(1) translateY(0)';
                                postImport(img);
                            }, true);
                            btn.addEventListener('click', e => {
                                e.preventDefault();
                                e.stopPropagation();
                            }, true);
                            document.documentElement.appendChild(btn);

                            const onEnter = () => reveal(btn);
                            const onLeave = () => conceal(btn);
                            img.addEventListener('mouseenter', onEnter, true);
                            img.addEventListener('mouseleave', onLeave, true);

                            buttons.set(img, { btn, onEnter, onLeave });
                            return btn;
                        };

                        const removeEntry = (img, entry) => {
                            img.removeEventListener('mouseenter', entry.onEnter, true);
                            img.removeEventListener('mouseleave', entry.onLeave, true);
                            const btn = entry.btn;
                            clearTimeout(btn._hideT);
                            btn.style.transition = 'opacity .14s ease, transform .14s ease';
                            btn.style.opacity = '0';
                            btn.style.transform = 'scale(.8) translateY(4px)';
                            btn.style.pointerEvents = 'none';
                            setTimeout(() => btn.remove(), 160);
                            buttons.delete(img);
                        };

                        // Verifies the image is actually the top-most thing at the button's anchor
                        // point, so the button never floats over an unrelated overlay/header.
                        const isAnchorPointClear = (img, btn, x, y) => {
                            const prevDisplay = btn.style.display;
                            btn.style.display = 'none';
                            const topEl = document.elementFromPoint(x, y);
                            btn.style.display = prevDisplay;
                            if (!topEl) return false;
                            return topEl === img || img.contains(topEl) || topEl.contains(img);
                        };

                        const positionButtons = () => {
                            // Clean up buttons whose image was removed/replaced from the DOM.
                            buttons.forEach((entry, img) => {
                                if (!img.isConnected) removeEntry(img, entry);
                            });

                            document.querySelectorAll('img').forEach(img => {
                                const entry = buttons.get(img);
                                if (!canImport(img)) {
                                    if (entry) entry.btn.style.display = 'none';
                                    return;
                                }

                                const btn = entry ? entry.btn : createButton(img);
                                const visible = getVisibleRect(img);
                                if (visible.width < MIN_VISIBLE_SIZE || visible.height < MIN_VISIBLE_SIZE) {
                                    btn.style.display = 'none';
                                    return;
                                }

                                let left = visible.left + visible.width / 2 - BUTTON_WIDTH / 2;
                                let top = visible.top + visible.height / 2 - BUTTON_HEIGHT / 2;
                                left = Math.max(visible.left + 4, Math.min(left, visible.right - BUTTON_WIDTH - 4));
                                top = Math.max(visible.top + 4, Math.min(top, visible.bottom - BUTTON_HEIGHT - 4));

                                if (!isAnchorPointClear(img, btn, left + BUTTON_WIDTH / 2, top + BUTTON_HEIGHT / 2)) {
                                    btn.style.display = 'none';
                                    return;
                                }

                                btn.style.display = 'flex';
                                btn.style.left = left + 'px';
                                btn.style.top = top + 'px';
                            });
                        };

                        let raf = 0;
                        const schedule = () => {
                            if (raf) return;
                            raf = requestAnimationFrame(() => {
                                raf = 0;
                                positionButtons();
                            });
                        };

                        window.addEventListener('scroll', schedule, true);
                        window.addEventListener('resize', schedule, true);
                        document.addEventListener('load', e => {
                            if (e.target && e.target.tagName === 'IMG') schedule();
                        }, true);

                        const observer = new MutationObserver(schedule);
                        observer.observe(document.documentElement, { childList: true, subtree: true, attributes: true, attributeFilter: ['src', 'srcset', 'style', 'class'] });
                        setInterval(schedule, 1200);
                        schedule();
                        window.imgsaver_miniClipImageButtonsHooked = true;
                    }
                    // Fix for Google Colab gapi loading issues
                    if (window.location.hostname.includes('colab')) {
                        window.addEventListener('error', (e) => {
                            if (e.message && e.message.includes('gapi')) {
                                console.log('Gapi loading issue detected, attempting to reload...');
                                setTimeout(() => location.reload(), 500);
                            }
                        }, true);
                    }window.addEventListener('mousedown', function() {
    try { if (window.chrome && window.chrome.webview) window.chrome.webview.postMessage({ type: 'focus_pane' }); } catch(e){}
}, true);
window.addEventListener('focus', function() {
    try { if (window.chrome && window.chrome.webview) window.chrome.webview.postMessage({ type: 'focus_pane' }); } catch(e){}
}, true);

                  } catch (imgsaverTopLevelErr) {
                      try {
                          if (window.chrome && window.chrome.webview) {
                              window.chrome.webview.postMessage({
                                  type: 'debug_log',
                                  message: '(' + location.href + (window !== window.top ? ' [IFRAME]' : ' [TOP]') + ') TOP-LEVEL SCRIPT ERROR: ' + (imgsaverTopLevelErr && imgsaverTopLevelErr.stack ? imgsaverTopLevelErr.stack : imgsaverTopLevelErr)
                              });
                          }
                      } catch (reportErr) {}
                  }
                })();";
             return script
                 .Replace("__SHOW_MINI_CLIP_IMAGE_IMPORT_BUTTONS__", showMiniClipImageImportButtons ? "true" : "false")
                 .Replace("__SHOW_QUICK_PASTE_BUTTON__", showQuickPasteButton ? "true" : "false");
        }

        private async void CoreWebView2_NewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            if (ShouldAllowExternalAuthPopup(e.Uri))
            {
                e.Handled = false;
                return;
            }

            e.Handled = true;
            await AddNewTab(e.Uri);
        }

        private bool ShouldAllowExternalAuthPopup(string? uri)
        {
            if (string.IsNullOrWhiteSpace(uri)) return false;

            try
            {
                var parsedUri = new Uri(uri);
                var host = parsedUri.Host;
                string[] authPopupHosts =
                {
                    "accounts.google.com",
                    "oauth.google.com",
                    "signin.google.com"
                };

                return authPopupHosts.Any(authHost =>
                    host.Equals(authHost, StringComparison.OrdinalIgnoreCase) ||
                    host.EndsWith("." + authHost, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }

        private void SyncDownloadProxySettings()
        {
        }

        private async void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var data = JObject.Parse(e.WebMessageAsJson);
                if (data == null) return;

                string? type = data["type"]?.ToString();
                if (type == "keyup")
                {
                    HandleKeyUp(data["key"]?.ToString());
                    return;
                }

                if (type == "miniClipImageImport")
                {
                    string? uri = data["uri"]?.ToString();
                    if (!string.IsNullOrWhiteSpace(uri))
                        await ManualImportImageToMiniClipAsync(sender as CoreWebView2, uri);
                }

                if (type == "debug_log")
                {
                    DebugLog("[JS] " + (data["message"]?.ToString() ?? ""));
                }

                if (type == "quick_paste_click")
                {
                    long nowTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    if (nowTime - _lastQuickPasteTime < 800)
                    {
                        return; // Ignore rapid duplicate messages
                    }
                    _lastQuickPasteTime = nowTime;

                    // STRATEGY: perform the ENTIRE interaction at the highest possible
                    // layer -- real OS-level mouse click + real OS-level Ctrl+V, both
                    // delivered through SendInput into the actual Windows input queue.
                    // No DOM API (.focus(), dispatchEvent, execCommand) is used at all,
                    // so there is nothing for a strict site (e.g. seaart.ai's LiteGraph
                    // canvas inside its iframe) to detect or block -- from the browser
                    // engine's perspective this is 100% identical to the user physically
                    // clicking the field and pressing Ctrl+V themselves.
                    DebugLog("[C#] quick_paste_click received");
                    try
                    {
                        double cssX = data["x"]?.ToObject<double>() ?? 0;
                        double cssY = data["y"]?.ToObject<double>() ?? 0;
                        var browser = GetCurrentBrowser();
                        DebugLog($"[C#] GetCurrentBrowser() == null ? {browser == null}, css=({cssX},{cssY})");

                        System.Windows.Point screenPoint = default;
                        bool gotPoint = false;

                        Dispatcher.Invoke(() =>
                        {
                            if (browser == null) return;

                            // Make sure our window is the real OS foreground window first,
                            // otherwise the click could land on top of another application.
                            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                            bool fgOk = hwnd != IntPtr.Zero && SetForegroundWindow(hwnd);
                            this.Activate();

                            // WebView2 CSS pixels map 1:1 to WPF device-independent units
                            // as long as the page isn't manually zoomed (default ZoomFactor
                            // is 1.0), so we can convert straight from the coordinate the
                            // page reported into the control's local point, then let WPF's
                            // PointToScreen do the DPI-aware conversion to real screen pixels.
                            screenPoint = browser.PointToScreen(new System.Windows.Point(cssX, cssY));
                            gotPoint = true;
                            DebugLog($"[C#] hwnd={hwnd} SetForegroundWindow ok={fgOk} screenPoint={screenPoint}");
                        });

                        if (!gotPoint)
                        {
                            DebugLog("[C#] aborting: could not resolve screen point (browser null?)");
                            return;
                        }

                        // 1) Wait a moment to ensure touch/tablet release transitions are fully completed
                        // by the OS before we simulate a mouse click (prevents contextmenu/selection issues).
                        await System.Threading.Tasks.Task.Delay(150);

                        // 2) Real, physical mouse click at the exact pixel of the field.
                        // This is what actually (re)creates focus on the field from the
                        // browser engine's point of view -- no .focus() call involved.
                        Dispatcher.Invoke(() => SendRealMouseClick((int)screenPoint.X, (int)screenPoint.Y));
                        DebugLog($"[C#] Physical click sent at ({screenPoint.X},{screenPoint.Y})");

                        // 3) Give the site's own click/focus handling a moment to run
                        // before we paste, same as a real human would naturally do.
                        await System.Threading.Tasks.Task.Delay(120);

                        // 3) Real, physical Ctrl+A followed by Ctrl+V to select all and replace.
                        Dispatcher.Invoke(() =>
                        {
                            InputSimulator.SimulateSelectAll();
                        });
                        await System.Threading.Tasks.Task.Delay(50);
                        Dispatcher.Invoke(() =>
                        {
                            SendNativeCtrlV();
                            DebugLog("[C#] Ctrl+A and Ctrl+V dispatched to replace input text.");
                        });
                    }
                    catch (Exception ex)
                    {
                        DebugLog("[C#] EXCEPTION in quick_paste_click: " + ex);
                    }
                }
            }
            catch { }
        }

        private static readonly string _debugLogPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "imgsaver_paste_debug.log");

        /// <summary>
        /// Temporary diagnostic logging for the BR Paste feature. Writes timestamped
        /// lines to a log file on the Desktop so we can see exactly which step fails
        /// on tricky sites (like seaart.ai's iframe-hosted, canvas-based node editor)
        /// without needing a debugger attached. Safe to remove once the feature is
        /// confirmed working everywhere.
        /// </summary>
        private void DebugLog(string message)
        {
            // Logging disabled
        }

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, BRINPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        private const int BR_INPUT_MOUSE = 0;
        private const uint BR_MOUSEEVENTF_MOVE = 0x0001;
        private const uint BR_MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint BR_MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint BR_MOUSEEVENTF_ABSOLUTE = 0x8000;
        private const uint BR_MOUSEEVENTF_VIRTUALDESK = 0x4000;
        private const int BR_SM_XVIRTUALSCREEN = 76;
        private const int BR_SM_YVIRTUALSCREEN = 77;
        private const int BR_SM_CXVIRTUALSCREEN = 78;
        private const int BR_SM_CYVIRTUALSCREEN = 79;

        [StructLayout(LayoutKind.Sequential)]
        private struct BRMOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct BRINPUTUNION
        {
            [FieldOffset(0)] public BRMOUSEINPUT mi;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BRINPUT
        {
            public uint type;
            public BRINPUTUNION U;
        }

        /// <summary>
        /// Sends a genuine OS-level left mouse click at the given absolute screen
        /// coordinates via SendInput. This goes through the real Windows input queue,
        /// the exact same path a physical mouse click takes, so the browser engine
        /// (and any JS running on the page, including inside iframes) cannot tell it
        /// apart from the user actually clicking there. Unlike a JS-dispatched
        /// MouseEvent or a WPF-level .focus() call, there is no synthetic-event flag
        /// or software focus API involved for a strict site to detect or block.
        /// </summary>
        private static void SendRealMouseClick(int x, int y)
        {
            int screenLeft = GetSystemMetrics(BR_SM_XVIRTUALSCREEN);
            int screenTop = GetSystemMetrics(BR_SM_YVIRTUALSCREEN);
            int screenWidth = Math.Max(1, GetSystemMetrics(BR_SM_CXVIRTUALSCREEN));
            int screenHeight = Math.Max(1, GetSystemMetrics(BR_SM_CYVIRTUALSCREEN));

            int absX = ((x - screenLeft) * 65536) / screenWidth;
            int absY = ((y - screenTop) * 65536) / screenHeight;

            uint moveFlags = BR_MOUSEEVENTF_MOVE | BR_MOUSEEVENTF_ABSOLUTE | BR_MOUSEEVENTF_VIRTUALDESK;
            uint downFlags = BR_MOUSEEVENTF_LEFTDOWN | BR_MOUSEEVENTF_ABSOLUTE | BR_MOUSEEVENTF_VIRTUALDESK;
            uint upFlags = BR_MOUSEEVENTF_LEFTUP | BR_MOUSEEVENTF_ABSOLUTE | BR_MOUSEEVENTF_VIRTUALDESK;

            BRINPUT MakeMouse(uint flags) => new BRINPUT
            {
                type = BR_INPUT_MOUSE,
                U = new BRINPUTUNION { mi = new BRMOUSEINPUT { dx = absX, dy = absY, dwFlags = flags } }
            };

            var move = MakeMouse(moveFlags);
            SendInput(1, new BRINPUT[] { move }, Marshal.SizeOf(typeof(BRINPUT)));

            var down = MakeMouse(downFlags);
            var up = MakeMouse(upFlags);
            SendInput(1, new BRINPUT[] { down }, Marshal.SizeOf(typeof(BRINPUT)));
            SendInput(1, new BRINPUT[] { up }, Marshal.SizeOf(typeof(BRINPUT)));
        }

        /// <summary>
        /// Simulates a real, OS-level Ctrl+V keystroke via the project's existing
        /// InputSimulator helper (SendInput-based). This is delivered through the
        /// normal Windows input queue, so it's indistinguishable from the user
        /// actually pressing Ctrl+V — which is what makes it work with editors
        /// (e.g. seaart.ai's workflow canvas) that ignore synthetic JS/DOM events and
        /// only react to trusted native input.
        /// </summary>
        private void SendNativeCtrlV()
        {
            InputSimulator.SimulatePaste();
        }

        private void HandleKeyUp(string? key)
        {
            if (string.IsNullOrEmpty(key)) return;
            var browser = GetCurrentBrowser();
            if (browser == null) return;
            if (key == " " || key == "Enter" || key == "Tab")
            {
                var match = SnippetManager.FindMatch(_typeBuffer);
                if (match != null)
                {
                    string safeVal = match.Value.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "\\n").Replace("\r", "\\r");
                    string script = $"if(window.imgsaver_insertSnippet) window.imgsaver_insertSnippet('{safeVal}', {match.Key.Length + 1});";
                    browser.CoreWebView2.ExecuteScriptAsync(script);
                    _typeBuffer = "";
                    SpawnParticles();
                }
                else { _typeBuffer = ""; }
                return;
            }
            if (key == "Escape") { _typeBuffer = ""; return; }
            if (key == "Backspace") { if (_typeBuffer.Length > 0) _typeBuffer = _typeBuffer.Substring(0, _typeBuffer.Length - 1); return; }
            if (key.Length == 1)
            {
                _typeBuffer += char.ToLower(key[0]);
                if (_typeBuffer.Length > 30) _typeBuffer = _typeBuffer.Substring(_typeBuffer.Length - 30);
                var match = SnippetManager.FindMatch(_typeBuffer);
                if (match != null)
                {
                    string safeVal = match.Value.Replace("\\", "\\\\").Replace("'", "\\'").Replace("\n", "\\n").Replace("\r", "\\r");
                    string script = $"if(window.imgsaver_insertSnippet) window.imgsaver_insertSnippet('{safeVal}', {match.Key.Length});";
                    browser.CoreWebView2.ExecuteScriptAsync(script);
                    _typeBuffer = "";
                    SpawnParticles();
                }
            }
        }

        private void SpawnParticles()
        {
            try
            {
                Random rnd = new Random();
                string[] particles = { "*", "+", "x", "." };
                int count = rnd.Next(5, 10);
                double startX = this.ActualWidth / 2;
                double startY = this.ActualHeight / 2;
                for (int i = 0; i < count; i++)
                {
                    TextBlock p = new TextBlock { Text = particles[rnd.Next(particles.Length)], FontSize = rnd.Next(14, 24), RenderTransformOrigin = new System.Windows.Point(0.5, 0.5), IsHitTestVisible = false, FontFamily = new System.Windows.Media.FontFamily("Segoe UI Emoji") };
                    Canvas.SetLeft(p, startX); Canvas.SetTop(p, startY);
                    TransformGroup group = new TransformGroup();
                    TranslateTransform trans = new TranslateTransform();
                    RotateTransform rot = new RotateTransform();
                    ScaleTransform scale = new ScaleTransform { ScaleX = 0, ScaleY = 0 };
                    group.Children.Add(scale); group.Children.Add(rot); group.Children.Add(trans);
                    p.RenderTransform = group; ParticleCanvas.Children.Add(p);
                    AnimateParticle(p, trans, rot, scale, rnd);
                }
            }
            catch { }
        }

        private void AnimateParticle(TextBlock particle, TranslateTransform trans, RotateTransform rot, ScaleTransform scale, Random rnd)
        {
            double durationSec = rnd.NextDouble() * 0.5 + 0.3;
            Duration duration = new Duration(TimeSpan.FromSeconds(durationSec));
            double angle = rnd.NextDouble() * 2 * Math.PI;
            double speed = rnd.Next(100, 300);
            DoubleAnimation animX = new DoubleAnimation(0, Math.Cos(angle) * speed, duration);
            DoubleAnimation animY = new DoubleAnimation(0, Math.Sin(angle) * speed, duration);
            animX.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            animY.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            trans.BeginAnimation(TranslateTransform.XProperty, animX);
            trans.BeginAnimation(TranslateTransform.YProperty, animY);
            rot.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, rnd.Next(-360, 360), duration));
            DoubleAnimation animScale = new DoubleAnimation(0, 1.5, new Duration(TimeSpan.FromSeconds(0.15)));
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, animScale);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, animScale);
            DoubleAnimation animFade = new DoubleAnimation(1, 0, duration);
            animFade.Completed += (s, e) => { ParticleCanvas.Children.Remove(particle); };
            particle.BeginAnimation(UIElement.OpacityProperty, animFade);
        }
    }
}