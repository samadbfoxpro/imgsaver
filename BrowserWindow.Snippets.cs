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
            string script = @"
                (function() {
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

        private void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var data = JObject.Parse(e.WebMessageAsJson);
                if (data != null && data["type"]?.ToString() == "keyup") { HandleKeyUp(data["key"]?.ToString()); }
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
