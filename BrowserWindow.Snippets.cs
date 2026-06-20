using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace imgsaver
{
    public partial class BrowserWindow
    {
        private void InjectSnippetHelperScript(WebView2 webView)
        {
            bool showMiniClipImageImportButtons = _currentSettings?.ShowMiniClipImageImportButtons == true;
            string script = @"
                (function() {
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
                            if (window.chrome && window.chrome.webview) {
                                if (e.key.length === 1 || e.key === 'Backspace' || e.key === 'Enter' || e.key === 'Tab' || e.key === 'Escape' || e.key === ' ') {
                                    window.chrome.webview.postMessage({ type: 'keyup', key: e.key });
                                }
                            }
                        }, true);
                        window.imgsaver_hooked = true;
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
                        const BUTTON_WIDTH = 92;
                        const BUTTON_HEIGHT = 38;
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

                        const importIconSvg = '<svg width=""15"" height=""15"" viewBox=""0 0 24 24"" fill=""none"" xmlns=""http://www.w3.org/2000/svg"" style=""flex:none""><path d=""M12 3v10.5"" stroke=""currentColor"" stroke-width=""2"" stroke-linecap=""round""/><path d=""M7.5 10.5 12 15l4.5-4.5"" stroke=""currentColor"" stroke-width=""2"" stroke-linecap=""round"" stroke-linejoin=""round""/><path d=""M4.5 17.5v1.8c0 .94.76 1.7 1.7 1.7h11.6c.94 0 1.7-.76 1.7-1.7v-1.8"" stroke=""currentColor"" stroke-width=""2"" stroke-linecap=""round"" stroke-linejoin=""round""/></svg>';

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
                            btn.innerHTML = importIconSvg + '<span style=""font:600 11.5px/1 -apple-system,Segoe UI,system-ui,sans-serif;letter-spacing:.2px"">Mini Clip</span>';
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
                                'gap:6px',
                                'box-shadow:0 6px 18px rgba(0,0,0,.35), 0 0 0 1px rgba(0,0,0,.2) inset',
                                'backdrop-filter:blur(7px)',
                                'opacity:' + (coarse ? '.92' : '0'),
                                'cursor:pointer',
                                'touch-action:manipulation',
                                'padding:0 12px 0 10px',
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
                    }
                })();";
            script = script.Replace("__SHOW_MINI_CLIP_IMAGE_IMPORT_BUTTONS__", showMiniClipImageImportButtons ? "true" : "false");
            webView.CoreWebView2.ExecuteScriptAsync(script);
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
            if (_downloadService == null || _currentSettings == null) return;
            _downloadService.UpdateProxySettings(
                _currentSettings.ProxyEnabled,
                _currentSettings.ProxyType ?? "http",
                _currentSettings.ProxyAddress ?? "",
                _currentSettings.ProxyPort ?? "");
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
            }
            catch { }
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