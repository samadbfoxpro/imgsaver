using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Windows.Threading;
using System.Threading;

namespace imgsaver
{
    public partial class BrowserWindow : Window
    {
        private readonly string _userDataFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "browser_profile");
        private readonly string _permanentCacheFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "web_cache");
        private BrowserSettings _currentSettings = null!;
        private string _typeBuffer = "";
        private DispatcherTimer _statusFadeTimer = null!;

        // Shared environment to ensure all tabs use the same profile/settings
        private static CoreWebView2Environment? _sharedEnvironment;
        private static readonly SemaphoreSlim _envLock = new SemaphoreSlim(1, 1);

        public BrowserWindow()
        {
            InitializeComponent();

            this.MaxHeight = SystemParameters.WorkArea.Height + 16;
            this.MaxWidth = SystemParameters.WorkArea.Width + 16;

            InitializeStatusTimer();
            RefreshSettings();
            RefreshBookmarksUI();

            this.StateChanged += BrowserWindow_StateChanged;

            InitializeTabs();
        }

        private async void InitializeTabs()
        {
            if (_currentSettings.OpenTabs != null && _currentSettings.OpenTabs.Count > 0)
            {
                foreach (var url in _currentSettings.OpenTabs)
                {
                    await AddNewTab(url);
                }
            }
            else
            {
                await AddNewTab(string.IsNullOrEmpty(_currentSettings.LastUrl) ? "https://www.google.com" : _currentSettings.LastUrl);
            }
        }

        private void BrowserWindow_StateChanged(object? sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Maximized) { MainBorder.Margin = new Thickness(8); }
            else { MainBorder.Margin = new Thickness(0); }
        }

        private void InitializeStatusTimer()
        {
            _statusFadeTimer = new DispatcherTimer();
            _statusFadeTimer.Interval = TimeSpan.FromSeconds(2);
            _statusFadeTimer.Tick += (s, e) => HideStatus();
        }

        private void RefreshSettings()
        {
            _currentSettings = BrowserSettings.Load();
            if (!_currentSettings.AutoHideStatus)
            {
                StatusOverlay.Visibility = Visibility.Visible;
                StatusOverlay.Opacity = 1;
                _statusFadeTimer?.Stop();
            }
        }

        private string GetIconForUrl(string? url)
        {
            if (string.IsNullOrEmpty(url)) return "🌐";
            string lower = url.ToLower();
            if (lower.Contains("google")) return "🔍";
            if (lower.Contains("github")) return "🐙";
            if (lower.Contains("youtube")) return "📺";
            if (lower.Contains("facebook")) return "👥";
            if (lower.Contains("twitter") || lower.Contains("x.com")) return "🐦";
            if (lower.Contains("instagram")) return "📸";
            if (lower.Contains("reddit")) return "🤖";
            if (lower.Contains("amazon")) return "🛒";
            if (lower.Contains("netflix")) return "🎬";
            if (lower.Contains("spotify")) return "🎵";
            if (lower.Contains("seaart")) return "🎨";
            if (lower.Contains("civitai")) return "🏗️";
            if (lower.Contains("pinterest")) return "📌";
            if (lower.Contains("discord")) return "💬";
            return "🌐";
        }

        private void RefreshBookmarksUI()
        {
            BookmarksPanel.Children.Clear();
            if (_currentSettings.Bookmarks == null) return;

            foreach (var bookmark in _currentSettings.Bookmarks)
            {
                var stack = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal };
                stack.Children.Add(new TextBlock { Text = GetIconForUrl(bookmark.Url), Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center, FontSize = 12 });
                stack.Children.Add(new TextBlock { Text = bookmark.Name, VerticalAlignment = VerticalAlignment.Center, FontSize = 11 });

                var btn = new System.Windows.Controls.Button
                {
                    Content = stack,
                    ToolTip = bookmark.Url,
                    Margin = new Thickness(2, 0, 2, 0),
                    Padding = new Thickness(8, 2, 8, 2),
                    Height = 26,
                    Style = (Style)FindResource("SecondaryButtonStyle")
                };

                btn.Click += (s, e) => { GetCurrentBrowser()?.CoreWebView2.Navigate(bookmark.Url); };

                var cm = new System.Windows.Controls.ContextMenu();
                var deleteMi = new System.Windows.Controls.MenuItem { Header = "Delete Bookmark" };
                deleteMi.Click += (s, e) => { _currentSettings.Bookmarks.Remove(bookmark); _currentSettings.Save(); RefreshBookmarksUI(); };
                cm.Items.Add(deleteMi);
                btn.ContextMenu = cm;

                BookmarksPanel.Children.Add(btn);
            }
        }

        private async Task AddNewTab(string? url = null)
        {
            try
            {
                if (!Directory.Exists(_userDataFolder)) Directory.CreateDirectory(_userDataFolder);
                if (!Directory.Exists(_permanentCacheFolder)) Directory.CreateDirectory(_permanentCacheFolder);

                var webView = new WebView2();
                var tabItem = new TabItem { Header = "🌐 New Tab", Content = webView };
                BrowserTabs.Items.Add(tabItem);
                BrowserTabs.SelectedItem = tabItem;

                await _envLock.WaitAsync();
                try
                {
                    if (_sharedEnvironment == null)
                    {
                        var options = new CoreWebView2EnvironmentOptions();
                        if (_currentSettings.ProxyEnabled && !string.IsNullOrEmpty(_currentSettings.ProxyAddress))
                        {
                            string proxyAddr = _currentSettings.ProxyAddress;
                            if (proxyAddr.Contains("://")) proxyAddr = proxyAddr.Split(new[] { "://" }, StringSplitOptions.None)[1];

                            string scheme = (_currentSettings.ProxyType?.ToLower() == "socks5") ? "socks5://" : "http://";
                            string proxyServer = $"{scheme}{proxyAddr}";
                            if (!string.IsNullOrEmpty(_currentSettings.ProxyPort)) proxyServer += ":" + _currentSettings.ProxyPort;

                            options.AdditionalBrowserArguments = $"--proxy-server=\"{proxyServer}\"";
                        }
                        _sharedEnvironment = await CoreWebView2Environment.CreateAsync(null, _userDataFolder, options);
                    }
                }
                finally { _envLock.Release(); }

                await webView.EnsureCoreWebView2Async(_sharedEnvironment);

                webView.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;
                webView.CoreWebView2.Settings.IsGeneralAutofillEnabled = false;

                webView.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
                webView.CoreWebView2.WebResourceRequested += CoreWebView2_WebResourceRequested;
                webView.CoreWebView2.WebResourceResponseReceived += CoreWebView2_WebResourceResponseReceived;
                webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

                webView.NavigationCompleted += (s, e) =>
                {
                    InjectSnippetHelperScript(webView);
                    string icon = GetIconForUrl(webView.Source?.ToString());
                    string title = webView.CoreWebView2.DocumentTitle ?? "New Tab";
                    tabItem.Header = $"{icon} {title}";
                };

                ApplyBrowserSettingsTo(webView);

                string startUrl = url ?? "https://www.google.com";
                webView.CoreWebView2.Navigate(startUrl);

                webView.SourceChanged += (s, e) =>
                {
                    string? currentUrl = webView.Source?.ToString();
                    if (BrowserTabs.SelectedItem == tabItem) { if (TxtUrl != null) TxtUrl.Text = currentUrl ?? ""; }
                    if (!string.IsNullOrEmpty(currentUrl) && currentUrl != "about:blank")
                    {
                        _currentSettings.LastUrl = currentUrl;
                        SaveSession();
                        string icon = GetIconForUrl(currentUrl);
                        string title = webView.CoreWebView2?.DocumentTitle ?? "Loading...";
                        tabItem.Header = $"{icon} {title}";
                    }
                };
            }
            catch (Exception ex) { CustomMessageBox.Show($"Failed to create tab: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void SaveSession()
        {
            if (_currentSettings == null) return;
            var urls = new List<string>();
            foreach (TabItem item in BrowserTabs.Items)
            {
                if (item.Content is WebView2 wv && wv.Source != null)
                {
                    string u = wv.Source.ToString();
                    if (!string.IsNullOrEmpty(u) && u != "about:blank") urls.Add(u);
                }
            }
            _currentSettings.OpenTabs = urls;
            _currentSettings.Save();
        }

        private WebView2? GetCurrentBrowser()
        {
            if (BrowserTabs.SelectedItem is TabItem tab && tab.Content is WebView2 webView) return webView;
            return null;
        }

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
                })();";
            webView.CoreWebView2.ExecuteScriptAsync(script);
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
                string[] particles = { "⚡", "✨", "🔥", "🚀" };
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

        private void ApplyBrowserSettingsTo(WebView2 webView)
        {
            if (webView.CoreWebView2 == null) return;
            var settings = _currentSettings ?? BrowserSettings.Load();
            webView.CoreWebView2.Settings.IsScriptEnabled = settings.EnableJavaScript;
            try { webView.CoreWebView2.IsMuted = settings.MuteAudio; } catch { }
        }

        private void CoreWebView2_WebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
        {
            string uri = e.Request.Uri.ToLower();
            UpdateStatus(uri, "Requesting...");
            if (_currentSettings == null) return;
            var ctx = e.ResourceContext;
            if (IsTrackerOrAd(uri)) { if (sender is CoreWebView2 wv) e.Response = wv.Environment.CreateWebResourceResponse(null, 403, "Forbidden", ""); return; }
            if (!_currentSettings.LoadImages && IsImageContext(ctx, uri)) { if (sender is CoreWebView2 wv) e.Response = wv.Environment.CreateWebResourceResponse(null, 403, "Forbidden", ""); return; }
            if (!_currentSettings.LoadMedia && IsMediaContext(ctx, uri)) { if (sender is CoreWebView2 wv) e.Response = wv.Environment.CreateWebResourceResponse(null, 403, "Forbidden", ""); return; }
            if (IsCacheableContext(ctx, uri))
            {
                string? cachePath = GetCacheFilePath(uri);
                if (!string.IsNullOrEmpty(cachePath) && File.Exists(cachePath))
                {
                    try
                    {
                        var stream = File.OpenRead(cachePath);
                        string mime = GetMimeType(ctx, uri);
                        string headers = $"Content-Type: {mime}\nCache-Control: public, max-age=31536000, immutable\nAccess-Control-Allow-Origin: *";
                        if (sender is CoreWebView2 wv) e.Response = wv.Environment.CreateWebResourceResponse(stream, 200, "OK", headers);
                        UpdateStatus(uri, "Cached");
                        return;
                    }
                    catch { }
                }
            }
        }

        private async void CoreWebView2_WebResourceResponseReceived(object? sender, CoreWebView2WebResourceResponseReceivedEventArgs e)
        {
            string uri = e.Request.Uri.ToLower();
            long size = 0;
            if (e.Response.Headers.Contains("Content-Length")) { long.TryParse(e.Response.Headers.GetHeader("Content-Length"), out size); }
            UpdateStatus(uri, FormatBytes(size));
            if (e.Response.StatusCode != 200 || e.Request.Method != "GET") return;
            bool isSeaArtImage = uri.Contains("seaart.me") && (uri.Contains(".webp") || uri.Contains(".png") || uri.Contains(".jpg"));
            if (IsCacheableExtension(uri) || isSeaArtImage)
            {
                string? cachePath = GetCacheFilePath(uri);
                if (string.IsNullOrEmpty(cachePath)) return;
                bool shouldUpdate = !File.Exists(cachePath);
                if (!shouldUpdate && size > 0) { long localSize = new FileInfo(cachePath).Length; if (localSize != size) shouldUpdate = true; }
                if (shouldUpdate)
                {
                    try
                    {
                        string? dir = Path.GetDirectoryName(cachePath);
                        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                        using (var stream = await e.Response.GetContentAsync())
                        {
                            if (stream != null)
                            {
                                using (var fileStream = File.Create(cachePath)) { await stream.CopyToAsync(fileStream); }
                                if (isSeaArtImage) { Dispatcher.Invoke(() => ClipboardMetadata.NotifyImageCaptured(cachePath)); }
                            }
                        }
                    }
                    catch { }
                }
            }
        }

        private void UpdateStatus(string url, string sizeInfo)
        {
            Dispatcher.Invoke(() => {
                TxtStatusUrl.Text = url;
                TxtStatusSize.Text = sizeInfo;
                if (StatusOverlay.Visibility != Visibility.Visible)
                {
                    StatusOverlay.Visibility = Visibility.Visible;
                    DoubleAnimation fadeIn = new DoubleAnimation(1, TimeSpan.FromSeconds(0.2));
                    StatusOverlay.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                }
                if (_currentSettings.AutoHideStatus) { _statusFadeTimer?.Stop(); _statusFadeTimer?.Start(); }
                else { _statusFadeTimer?.Stop(); StatusOverlay.Opacity = 1; }
            });
        }

        private void HideStatus()
        {
            if (!_currentSettings.AutoHideStatus) return;
            DoubleAnimation fadeOut = new DoubleAnimation(0, TimeSpan.FromSeconds(0.5));
            fadeOut.Completed += (s, e) => { StatusOverlay.Visibility = Visibility.Collapsed; };
            StatusOverlay.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        private string FormatBytes(long bytes)
        {
            string[] Suffix = { "B", "KB", "MB", "GB" };
            int i; double dblSByte = bytes;
            for (i = 0; i < Suffix.Length && bytes >= 1024; i++, bytes /= 1024) { dblSByte = bytes / 1024.0; }
            return string.Format("{0:0.##} {1}", dblSByte, Suffix[i]);
        }

        private bool IsTrackerOrAd(string uri) => uri.Contains("google-analytics.com") || uri.Contains("doubleclick.net") || uri.Contains("googletagmanager.com") || uri.Contains("facebook.net") || uri.Contains("adservice.google") || uri.Contains("analytics.") || uri.Contains("/ads/") || uri.Contains("pixel.");
        private bool IsCacheableContext(CoreWebView2WebResourceContext ctx, string uri) => ctx == CoreWebView2WebResourceContext.Script || ctx == CoreWebView2WebResourceContext.Stylesheet || ctx == CoreWebView2WebResourceContext.Font || ctx == CoreWebView2WebResourceContext.Fetch || ctx == CoreWebView2WebResourceContext.XmlHttpRequest || IsCacheableExtension(uri);
        private bool IsCacheableExtension(string uri) => uri.Contains(".js") || uri.Contains(".css") || uri.Contains(".woff") || uri.Contains(".woff2") || uri.Contains(".ttf") || uri.Contains(".otf") || uri.Contains(".wasm") || uri.Contains(".json") || uri.Contains(".svg");
        private bool IsImageContext(CoreWebView2WebResourceContext ctx, string uri) => ctx == CoreWebView2WebResourceContext.Image || uri.EndsWith(".jpg") || uri.EndsWith(".png") || uri.EndsWith(".webp") || uri.EndsWith(".gif");
        private bool IsMediaContext(CoreWebView2WebResourceContext ctx, string uri) => ctx == CoreWebView2WebResourceContext.Media || uri.EndsWith(".mp4") || uri.EndsWith(".webm") || uri.EndsWith(".mp3");

        private string? GetCacheFilePath(string uri)
        {
            try
            {
                Uri parsedUri = new Uri(uri);
                string host = parsedUri.Host;
                foreach (char c in Path.GetInvalidFileNameChars()) host = host.Replace(c, '_');
                string siteFolder = Path.Combine(_permanentCacheFolder, host);
                using (MD5 md5 = MD5.Create())
                {
                    byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(uri));
                    string filename = BitConverter.ToString(hash).Replace("-", "").ToLower();
                    if (uri.Contains(".webp")) filename += ".webp";
                    else if (uri.Contains(".png")) filename += ".png";
                    else if (uri.Contains(".jpg")) filename += ".jpg";
                    else if (uri.Contains(".wasm")) filename += ".wasm";
                    else if (uri.Contains(".js")) filename += ".js";
                    return Path.Combine(siteFolder, filename);
                }
            }
            catch { return null; }
        }

        private string GetMimeType(CoreWebView2WebResourceContext ctx, string uri)
        {
            if (uri.Contains(".js")) return "application/javascript";
            if (uri.Contains(".css")) return "text/css";
            if (uri.Contains(".wasm")) return "application/wasm";
            if (uri.Contains(".json")) return "application/json";
            if (uri.Contains(".woff2")) return "font/woff2";
            if (uri.Contains(".woff")) return "font/woff";
            if (uri.Contains(".svg")) return "image/svg+xml";
            return "application/octet-stream";
        }

        private void BrowserTabs_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            var browser = GetCurrentBrowser();
            if (browser != null && TxtUrl != null) TxtUrl.Text = browser.Source?.ToString() ?? "";
        }

        private async void BtnNewTab_Click(object? sender, RoutedEventArgs e) => await AddNewTab();

        private void BtnCloseTab_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is TabItem tab)
            {
                if (tab.Content is WebView2 webView) webView.Dispose();
                BrowserTabs.Items.Remove(tab);
                if (BrowserTabs.Items.Count == 0) _ = AddNewTab();
                SaveSession();
            }
        }

        private async void BtnBrowserSettings_Click(object? sender, RoutedEventArgs e)
        {
            var settingsWin = new BrowserSettingsWindow();
            settingsWin.Owner = this;
            if (settingsWin.ShowDialog() == true)
            {
                if (settingsWin.RequestClearData)
                {
                    var browser = GetCurrentBrowser();
                    if (browser?.CoreWebView2 != null)
                    {
                        await browser.CoreWebView2.Profile.ClearBrowsingDataAsync();
                        CustomMessageBox.Show("All browsing data has been cleared.", "Success");
                        browser.Reload();
                    }
                }
                RefreshSettings();
                foreach (TabItem tab in BrowserTabs.Items)
                {
                    if (tab.Content is WebView2 webView) ApplyBrowserSettingsTo(webView);
                }
                CustomMessageBox.Show("Some settings (like Proxy) require restarting the browser window to take effect.", "Settings Updated");
            }
        }

        private async void BtnClearSiteData_Click(object? sender, RoutedEventArgs e)
        {
            var browser = GetCurrentBrowser();
            if (browser == null || browser.Source == null) return;
            string host = browser.Source.Host;
            if (string.IsNullOrEmpty(host)) return;
            if (CustomMessageBox.Show($"Clear all cached data and cookies for {host}?", "Clear Site Data", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                try
                {
                    string sanitizedHost = host;
                    foreach (char c in Path.GetInvalidFileNameChars()) sanitizedHost = sanitizedHost.Replace(c, '_');
                    string targetDir = Path.Combine(_permanentCacheFolder, sanitizedHost);
                    if (Directory.Exists(targetDir)) { Directory.Delete(targetDir, true); }
                }
                catch { }
                try
                {
                    var cookieManager = browser.CoreWebView2.CookieManager;
                    var cookies = await cookieManager.GetCookiesAsync(browser.Source.ToString());
                    foreach (var cookie in cookies) { cookieManager.DeleteCookie(cookie); }
                }
                catch { }
                CustomMessageBox.Show($"Data for {host} has been cleared.", "Success");
                browser.Reload();
            }
        }

        private void BtnAddBookmark_Click(object? sender, RoutedEventArgs e)
        {
            var browser = GetCurrentBrowser();
            if (browser == null || browser.Source == null) return;
            string url = browser.Source.ToString();
            string title = browser.CoreWebView2.DocumentTitle ?? url;
            if (_currentSettings.Bookmarks == null) _currentSettings.Bookmarks = new List<BookmarkItem>();
            if (_currentSettings.Bookmarks.Any(b => b.Url == url)) return;
            _currentSettings.Bookmarks.Add(new BookmarkItem { Name = title, Url = url });
            _currentSettings.Save();
            RefreshBookmarksUI();
        }

        private void TitleBar_MouseLeftButtonDown(object? sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) BtnMaximize_Click(sender, e);
            else if (e.LeftButton == MouseButtonState.Pressed) this.DragMove();
        }

        private void BtnClose_Click(object? sender, RoutedEventArgs e) => this.Close();
        private void BtnMinimize_Click(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void BtnMaximize_Click(object? sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        private void BtnBack_Click(object? sender, RoutedEventArgs e) { var b = GetCurrentBrowser(); if (b != null && b.CanGoBack) b.GoBack(); }
        private void BtnForward_Click(object? sender, RoutedEventArgs e) { var b = GetCurrentBrowser(); if (b != null && b.CanGoForward) b.GoForward(); }
        private void BtnReload_Click(object? sender, RoutedEventArgs e) => GetCurrentBrowser()?.Reload();
        private void BtnGo_Click(object? sender, RoutedEventArgs e) => Navigate();
        private void TxtUrl_KeyDown(object? sender, System.Windows.Input.KeyEventArgs e) { if (e.Key == Key.Enter) Navigate(); }

        private void Navigate()
        {
            string url = TxtUrl.Text.Trim();
            if (string.IsNullOrEmpty(url)) return;
            if (!url.Contains(".") && !url.StartsWith("http")) url = "https://www.google.com/search?q=" + Uri.EscapeDataString(url);
            else if (!url.StartsWith("http")) url = "https://" + url;
            try { GetCurrentBrowser()?.CoreWebView2.Navigate(url); } catch { }
        }
    }

    public class BookmarkItem { public string Name { get; set; } = ""; public string Url { get; set; } = ""; }

    public class BrowserSettings
    {
        public bool LoadImages { get; set; } = true;
        public bool LoadMedia { get; set; } = true;
        public bool EnableJavaScript { get; set; } = true;
        public bool MuteAudio { get; set; } = false;
        public bool AutoHideStatus { get; set; } = true;
        public string LastUrl { get; set; } = "";
        public List<string> OpenTabs { get; set; } = new List<string>();
        public List<BookmarkItem> Bookmarks { get; set; } = new List<BookmarkItem>();

        public bool ProxyEnabled { get; set; } = false;
        public string ProxyType { get; set; } = "http";
        public string ProxyAddress { get; set; } = "";
        public string ProxyPort { get; set; } = "";

        private static string FilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "browser_settings.json");

        public static BrowserSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    string json = File.ReadAllText(FilePath);
                    return System.Text.Json.JsonSerializer.Deserialize<BrowserSettings>(json) ?? new BrowserSettings();
                }
            }
            catch { }
            return new BrowserSettings();
        }

        public void Save()
        {
            try
            {
                string? dir = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string json = System.Text.Json.JsonSerializer.Serialize(this);
                File.WriteAllText(FilePath, json);
            }
            catch { }
        }
    }
}
