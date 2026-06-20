using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WpfPanel = System.Windows.Controls.Panel;

namespace imgsaver
{
    public partial class BrowserWindow
    {
        private async void InitializeTabs()
        {
            if (_currentSettings.TabSessions != null && _currentSettings.TabSessions.Count > 0)
            {
                foreach (var tabSession in _currentSettings.TabSessions)
                {
                    await AddNewTab(IsLegacyNewTabUrl(tabSession.Url) ? null : tabSession.Url, selectTab: false);
                }
                BrowserTabs.SelectedIndex = Math.Clamp(_currentSettings.SelectedTabIndex, 0, Math.Max(0, BrowserTabs.Items.Count - 1));
            }
            else if (_currentSettings.OpenTabs != null && _currentSettings.OpenTabs.Count > 0)
            {
                foreach (var url in _currentSettings.OpenTabs)
                {
                    await AddNewTab(IsLegacyNewTabUrl(url) ? null : url);
                }
            }
            else
            {
                await AddNewTab(string.IsNullOrEmpty(_currentSettings.LastUrl) || IsLegacyNewTabUrl(_currentSettings.LastUrl) ? null : _currentSettings.LastUrl);
            }
        }

        private bool IsLegacyNewTabUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            return url.Contains("NewTabPage.html", StringComparison.OrdinalIgnoreCase);
        }

        private async Task ResetEnvironmentAndReloadTabs()
        {
            // Clear the shared environment so it will be recreated with new proxy settings
            await _envLock.WaitAsync();
            try
            {
                _sharedEnvironment = null;
            }
            finally { _envLock.Release(); }

            // Collect URLs to reload before removing tabs
            var urlsToReload = new List<string>();
            var tabsToRemove = new List<TabItem>();

            foreach (TabItem tab in BrowserTabs.Items)
            {
                if (TryGetTabState(tab, out var state) && state.PrimaryWebView?.Source != null)
                {
                    urlsToReload.Add(state.PrimaryWebView.Source.ToString());
                    tabsToRemove.Add(tab);
                }
            }

            // Remove old tabs and dispose WebView2 controls
            foreach (var tab in tabsToRemove)
            {
                if (TryGetTabState(tab, out var state) && state.PrimaryWebView != null)
                {
                    if (state.PrimaryWebView.CoreWebView2 != null)
                    {
                        _coreWebViewTabMap.Remove(state.PrimaryWebView.CoreWebView2);
                        state.PrimaryWebView.CoreWebView2.ContextMenuRequested -= CoreWebView2_ContextMenuRequested;
                    }
                    state.PrimaryWebView.Dispose();
                }
                _tabHeaderMap.Remove(tab);
                _tabNetworkStats.Remove(tab);
                _tabStates.Remove(tab);
                _internalNewTabs.Remove(tab);
                BrowserTabs.Items.Remove(tab);
            }

            // Wait a bit to ensure environment cleanup
            await Task.Delay(200);

            // Recreate tabs with the new environment
            foreach (var url in urlsToReload)
            {
                await AddNewTab(url);
            }

            SaveCurrentProxySettings();
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

        private async Task<TabItem?> AddNewTab(string? url = null, bool selectTab = true, bool isPinned = false)
        {
            try
            {
                if (!Directory.Exists(_userDataFolder)) Directory.CreateDirectory(_userDataFolder);
                if (!Directory.Exists(_permanentCacheFolder)) Directory.CreateDirectory(_permanentCacheFolder);

                var webView = new WebView2();
                var tabItem = new TabItem();

                var headerText = new TextBlock
                {
                    Text = "* Loading...",
                    MaxWidth = 140,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Foreground = (System.Windows.Media.Brush)FindResource("ForegroundBrush"),
                    VerticalAlignment = VerticalAlignment.Center
                };
                var loadingBadge = new Border
                {
                    Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 0xFF, 0xC4, 0x00)),
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(6, 1, 6, 1),
                    Margin = new Thickness(6, 0, 0, 0),
                    Visibility = Visibility.Collapsed,
                    Child = new TextBlock
                    {
                        Text = "Loading",
                        FontSize = 10,
                        Foreground = System.Windows.Media.Brushes.Black,
                        VerticalAlignment = VerticalAlignment.Center
                    }
                };
                var headerPanel = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                headerPanel.Children.Add(headerText);
                headerPanel.Children.Add(loadingBadge);

                tabItem.Header = headerPanel;
                tabItem.ContextMenu = CreateTabContextMenu(tabItem);
                tabItem.Content = webView;
                
                BrowserTabs.Items.Add(tabItem);
                if (selectTab) BrowserTabs.SelectedItem = tabItem;
                
                _tabHeaderMap[tabItem] = (headerText, loadingBadge);
                _tabNetworkStats[tabItem] = new TabNetworkInfo();
                _tabStates[tabItem] = new BrowserTabState { Tab = tabItem, PrimaryWebView = webView, ActiveWebView = webView, IsPinned = isPinned };

                await _envLock.WaitAsync();
                try
                {
                    if (_sharedEnvironment == null)
                    {
                        var options = new CoreWebView2EnvironmentOptions();
                        var browserArguments = new List<string>
                        {
                            $"--disk-cache-dir=\"{_permanentCacheFolder}\"",
                            $"--disk-cache-size={ChromiumDiskCacheBytes}",
                            "--aggressive-cache-discard=false",
                            "--disable-features=BackForwardCacheMemoryControls"
                        };

                        if (_currentSettings.ProxyEnabled && !string.IsNullOrEmpty(_currentSettings.ProxyAddress))
                        {
                            string proxyAddr = _currentSettings.ProxyAddress;
                            if (proxyAddr.Contains("://")) proxyAddr = proxyAddr.Split(new[] { "://" }, StringSplitOptions.None)[1];

                            string scheme = (_currentSettings.ProxyType?.ToLower() == "socks5") ? "socks5://" : "http://";
                            string proxyServer = $"{scheme}{proxyAddr}";
                            if (!string.IsNullOrEmpty(_currentSettings.ProxyPort)) proxyServer += ":" + _currentSettings.ProxyPort;

                            browserArguments.Add($"--proxy-server=\"{proxyServer}\"");
                        }
                        options.AdditionalBrowserArguments = string.Join(" ", browserArguments);
                        _sharedEnvironment = await CoreWebView2Environment.CreateAsync(null, _userDataFolder, options);
                    }
                }
                finally { _envLock.Release(); }

                // Ensure CoreWebView2 is initialized before using it
                await webView.EnsureCoreWebView2Async(_sharedEnvironment);

                if (webView.CoreWebView2 == null) throw new Exception("CoreWebView2 initialization failed");
                _coreWebViewTabMap[webView.CoreWebView2] = tabItem;

                webView.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;
                webView.CoreWebView2.Settings.IsGeneralAutofillEnabled = false;
                webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                webView.CoreWebView2.Settings.IsScriptEnabled = true;

                webView.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
                webView.CoreWebView2.WebResourceRequested += CoreWebView2_WebResourceRequested;
                webView.CoreWebView2.WebResourceResponseReceived += CoreWebView2_WebResourceResponseReceived;
                webView.CoreWebView2.DownloadStarting += CoreWebView2_DownloadStarting;
                webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
                webView.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;
                webView.CoreWebView2.ContextMenuRequested += CoreWebView2_ContextMenuRequested;

                webView.NavigationStarting += (s, e) =>
                {
                    if (_internalNewTabs.Contains(tabItem) && !string.IsNullOrWhiteSpace(e.Uri) && e.Uri != "about:blank")
                        _internalNewTabs.Remove(tabItem);
                    UpdateTabHeader(tabItem, GetIconForUrl(e.Uri), "Loading...");
                    SetTabLoadingState(tabItem, true);
                    ResetTabNetworkStats(tabItem);
                };

                webView.NavigationCompleted += (s, e) =>
                {
                    if (!e.IsSuccess)
                    {
                        UpdateTabHeader(tabItem, "!", "Failed to load");
                        SetTabLoadingState(tabItem, false);
                        UpdateStopButtonState();
                        UpdateTabStatusOverlay(tabItem);
                        return;
                    }
                    InjectSnippetHelperScript(webView);
                    string icon = GetIconForUrl(webView.Source?.ToString());
                    string title = webView.CoreWebView2.DocumentTitle ?? "New Tab";
                    UpdateTabHeader(tabItem, icon, title);
                    SetTabLoadingState(tabItem, false);
                    UpdateStopButtonState();
                    UpdateTabStatusOverlay(tabItem);
                };

                ApplyBrowserSettingsTo(webView);

                if (string.IsNullOrWhiteSpace(url))
                {
                    _internalNewTabs.Add(tabItem);
                    webView.CoreWebView2.NavigateToString(GetNewTabPageHtml());
                    UpdateTabHeader(tabItem, "+", "New Tab");
                    if (BrowserTabs.SelectedItem == tabItem && TxtUrl != null) TxtUrl.Text = "";
                }
                else
                {
                    webView.CoreWebView2.Navigate(url);
                }

                webView.SourceChanged += (s, e) =>
                {
                    string? currentUrl = webView.Source?.ToString();
                    bool isInternalNewTab = _internalNewTabs.Contains(tabItem) && (string.IsNullOrEmpty(currentUrl) || currentUrl == "about:blank");
                    if (BrowserTabs.SelectedItem == tabItem && GetCurrentBrowser() == webView)
                    {
                        if (TxtUrl != null) TxtUrl.Text = isInternalNewTab ? "" : currentUrl ?? "";
                    }
                    if (!string.IsNullOrEmpty(currentUrl) && currentUrl != "about:blank")
                    {
                        _internalNewTabs.Remove(tabItem);
                        _currentSettings.LastUrl = currentUrl;
                        SaveSession();
                        string icon = GetIconForUrl(currentUrl);
                        string title = webView.CoreWebView2?.DocumentTitle ?? "Loading...";
                        UpdateTabHeader(tabItem, icon, title);
                    }
                };
                return tabItem;
            }
            catch (Exception ex) { CustomMessageBox.Show($"Failed to create tab: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); System.Diagnostics.Debug.WriteLine(ex); return null; }
        }

        private ContextMenu CreateTabContextMenu(TabItem tabItem)
        {
            var menu = new ContextMenu();
            menu.Opened += (s, e) =>
            {
                menu.Items.Clear();
                if (TryGetTabState(tabItem, out var state))
                {
                    var pin = new MenuItem { Header = state.IsPinned ? "Unpin Tab" : "Pin Tab" };
                    pin.Click += (_, _) => TogglePinnedTab(tabItem);
                    menu.Items.Add(pin);

                    var moveLeft = new MenuItem { Header = "Move Tab Left", IsEnabled = BrowserTabs.Items.IndexOf(tabItem) > 0 };
                    moveLeft.Click += (_, _) => MoveTab(tabItem, -1);
                    var moveRight = new MenuItem { Header = "Move Tab Right", IsEnabled = BrowserTabs.Items.IndexOf(tabItem) < BrowserTabs.Items.Count - 1 };
                    moveRight.Click += (_, _) => MoveTab(tabItem, 1);
                    menu.Items.Add(moveLeft);
                    menu.Items.Add(moveRight);
                }
            };
            return menu;
        }

        private bool TryGetTabState(TabItem tabItem, out BrowserTabState state) => _tabStates.TryGetValue(tabItem, out state!);

        private bool IsInternalNewTab(TabItem tabItem, WebView2 webView)
        {
            string? currentUrl = webView.Source?.ToString();
            return _internalNewTabs.Contains(tabItem) && (string.IsNullOrEmpty(currentUrl) || currentUrl == "about:blank");
        }

        private void NavigateWebView(WebView2 webView, string rawUrl)
        {
            string url = rawUrl.Trim();
            if (string.IsNullOrEmpty(url) || webView.CoreWebView2 == null) return;
            if (!url.Contains(".") && !url.StartsWith("http")) url = "https://www.google.com/search?q=" + Uri.EscapeDataString(url);
            else if (!url.StartsWith("http")) url = "https://" + url;
            webView.CoreWebView2.Navigate(url);
        }

        private static void DetachFromParent(UIElement element)
        {
            switch (VisualTreeHelper.GetParent(element))
            {
                case WpfPanel panel:
                    panel.Children.Remove(element);
                    break;
                case ContentControl contentControl when ReferenceEquals(contentControl.Content, element):
                    contentControl.Content = null;
                    break;
                case Decorator decorator when ReferenceEquals(decorator.Child, element):
                    decorator.Child = null;
                    break;
            }
        }

        private void TogglePinnedTab(TabItem tabItem)
        {
            if (!TryGetTabState(tabItem, out var state)) return;
            state.IsPinned = !state.IsPinned;
            BrowserTabs.Items.Remove(tabItem);
            BrowserTabs.Items.Insert(state.IsPinned ? 0 : BrowserTabs.Items.Count, tabItem);
            BrowserTabs.SelectedItem = tabItem;
            SaveSession();
        }

        private void MoveTab(TabItem tabItem, int direction)
        {
            var index = BrowserTabs.Items.IndexOf(tabItem);
            var targetIndex = Math.Clamp(index + direction, 0, BrowserTabs.Items.Count - 1);
            if (index < 0 || index == targetIndex) return;
            BrowserTabs.Items.Remove(tabItem);
            BrowserTabs.Items.Insert(targetIndex, tabItem);
            BrowserTabs.SelectedItem = tabItem;
            SaveSession();
        }

        private sealed class BrowserTabState
        {
            public TabItem Tab { get; set; } = null!;
            public WebView2? PrimaryWebView { get; set; }
            public WebView2? ActiveWebView { get; set; }
            public bool IsPinned { get; set; }
        }

        private void SaveSession()
        {
            if (_currentSettings == null) return;
            var urls = new List<string>();
            var sessions = new List<BrowserTabSession>();
            foreach (TabItem item in BrowserTabs.Items)
            {
                if (!TryGetTabState(item, out var state)) continue;
                var webView = state.PrimaryWebView;
                if (webView?.Source != null && !_internalNewTabs.Contains(item))
                {
                    string u = webView.Source.ToString();
                    if (!string.IsNullOrEmpty(u) && u != "about:blank")
                    {
                        urls.Add(u);
                        sessions.Add(new BrowserTabSession
                        {
                            Url = u,
                            IsPinned = state.IsPinned
                        });
                    }
                }
            }
            _currentSettings.OpenTabs = urls;
            _currentSettings.TabSessions = sessions;
            _currentSettings.SelectedTabIndex = BrowserTabs.SelectedIndex;
            _currentSettings.Save();
        }

        private WebView2? GetCurrentBrowser()
        {
            if (BrowserTabs.SelectedItem is TabItem tab && TryGetTabState(tab, out var state))
            {
                return state.ActiveWebView ?? state.PrimaryWebView;
            }
            return null;
        }

        private void ApplyBrowserSettingsTo(WebView2 webView)
        {
            if (webView.CoreWebView2 == null) return;
            var settings = _currentSettings ?? BrowserSettings.Load();
            webView.CoreWebView2.Settings.IsScriptEnabled = settings.EnableJavaScript;
            try { webView.CoreWebView2.IsMuted = settings.MuteAudio; } catch { }
            if (settings.EnableJavaScript)
                InjectSnippetHelperScript(webView);
        }

        private void UpdateTabHeader(TabItem tabItem, string icon, string title)
        {
            if (_tabHeaderMap.TryGetValue(tabItem, out var header))
            {
                header.HeaderText.Text = $"{icon} {title}";
            }
            else
            {
                tabItem.Header = $"{icon} {title}";
            }
        }

        private void SetTabLoadingState(TabItem tabItem, bool isLoading)
        {
            if (_tabHeaderMap.TryGetValue(tabItem, out var header))
            {
                header.LoadingBadge.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void UpdateStopButtonState()
        {
            if (BtnStop != null)
            {
                BtnStop.IsEnabled = GetCurrentBrowser() != null;
            }
        }

        private void BrowserTabs_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            var browser = GetCurrentBrowser();
            if (browser != null && TxtUrl != null && browser.CoreWebView2 != null)
            {
                bool isInternalNewTab = BrowserTabs.SelectedItem is TabItem tab && _internalNewTabs.Contains(tab);
                TxtUrl.Text = isInternalNewTab ? "" : browser.Source?.ToString() ?? "";
            }
            UpdateStopButtonState();
            UpdateTabStatusOverlay(BrowserTabs.SelectedItem as TabItem);
        }

        private async void BtnNewTab_Click(object? sender, RoutedEventArgs e) => await AddNewTab();

        private void BtnStop_Click(object? sender, RoutedEventArgs e) => GetCurrentBrowser()?.Stop();

        private void BtnCloseTab_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is TabItem tab)
            {
                CloseTab(tab);
            }
            else if (sender is System.Windows.Controls.MenuItem mi && mi.Tag is TabItem miTab)
            {
                CloseTab(miTab);
            }
        }

        private void TabItem_MouseRightButtonUp(object? sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // Context menu is handled automatically by WPF
            e.Handled = false;
        }

        private void CloseTab(TabItem tab)
        {
            if (TryGetTabState(tab, out var state))
            {
                if (state.PrimaryWebView?.CoreWebView2 != null)
                {
                    _coreWebViewTabMap.Remove(state.PrimaryWebView.CoreWebView2);
                    state.PrimaryWebView.CoreWebView2.ContextMenuRequested -= CoreWebView2_ContextMenuRequested;
                }
                state.PrimaryWebView?.Dispose();
                _tabStates.Remove(tab);
            }
            _internalNewTabs.Remove(tab);
            _tabHeaderMap.Remove(tab);
            _tabNetworkStats.Remove(tab);
            BrowserTabs.Items.Remove(tab);
            if (BrowserTabs.Items.Count == 0) _ = AddNewTab();
            SaveSession();
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
            try
            {
                if (BrowserTabs.SelectedItem is TabItem tab) _internalNewTabs.Remove(tab);
                GetCurrentBrowser()?.CoreWebView2.Navigate(url);
            }
            catch { }
        }
    }
}
