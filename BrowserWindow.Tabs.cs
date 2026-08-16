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
                _environment = null;
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
            BookmarksOverflowPanel.Children.Clear();
            if (_currentSettings.Bookmarks == null) return;

            double availableWidth = BookmarksBorder.ActualWidth;
            if (availableWidth == 0) availableWidth = this.ActualWidth; // fallback during initialization
            double currentWidth = 0;
            double overflowButtonWidth = 30; // 24 + margins

            bool isOverflowing = false;

            for (int i = 0; i < _currentSettings.Bookmarks.Count; i++)
            {
                var bookmark = _currentSettings.Bookmarks[i];
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
                    Style = (Style)FindResource("SecondaryButtonStyle"),
                    Tag = bookmark
                };

                btn.Click += (s, e) => { GetCurrentBrowser()?.CoreWebView2.Navigate(bookmark.Url); };

                var cm = new System.Windows.Controls.ContextMenu();
                var deleteMi = new System.Windows.Controls.MenuItem { Header = "Delete Bookmark" };
                deleteMi.Click += (s, e) => { _currentSettings.Bookmarks.Remove(bookmark); _currentSettings.Save(); RefreshBookmarksUI(); };
                cm.Items.Add(deleteMi);
                btn.ContextMenu = cm;

                // Drag and Drop
                btn.PreviewMouseLeftButtonDown += Bookmark_PreviewMouseLeftButtonDown;
                btn.PreviewMouseMove += Bookmark_PreviewMouseMove;
                btn.AllowDrop = true;
                btn.Drop += Bookmark_Drop;
                btn.DragEnter += Bookmark_DragEnter;

                btn.Measure(new System.Windows.Size(double.PositiveInfinity, 26));
                double btnWidth = btn.DesiredSize.Width + 4; // Including margins

                if (!isOverflowing && currentWidth + btnWidth > availableWidth - overflowButtonWidth)
                {
                    isOverflowing = true;
                }

                if (isOverflowing)
                {
                    btn.Margin = new Thickness(2); // Adjust margin for vertical layout
                    BookmarksOverflowPanel.Children.Add(btn);
                }
                else
                {
                    currentWidth += btnWidth;
                    BookmarksPanel.Children.Add(btn);
                }
            }

            BtnBookmarksOverflow.Visibility = isOverflowing ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BookmarksBorder_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RefreshBookmarksUI();
        }

        private void BtnBookmarksOverflow_Click(object sender, RoutedEventArgs e)
        {
            BookmarksOverflowPopup.IsOpen = true;
        }

        private System.Windows.Point _bookmarkDragStartPoint;
        private bool _isBookmarkDragging = false;

        private void Bookmark_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _bookmarkDragStartPoint = e.GetPosition(null);
            _isBookmarkDragging = false;
        }

        private void Bookmark_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && !_isBookmarkDragging)
            {
                System.Windows.Point position = e.GetPosition(null);
                if (Math.Abs(position.X - _bookmarkDragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(position.Y - _bookmarkDragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    if (sender is System.Windows.Controls.Button btn && btn.Tag is BookmarkItem bookmark)
                    {
                        _isBookmarkDragging = true;
                        DragDrop.DoDragDrop(btn, bookmark, System.Windows.DragDropEffects.Move);
                        _isBookmarkDragging = false;
                    }
                }
            }
        }

        private void Bookmark_DragEnter(object sender, System.Windows.DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(BookmarkItem)))
            {
                e.Effects = System.Windows.DragDropEffects.None;
            }
        }

        private void Bookmark_Drop(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(BookmarkItem)))
            {
                var sourceBookmark = e.Data.GetData(typeof(BookmarkItem)) as BookmarkItem;
                if (sender is System.Windows.Controls.Button targetBtn && targetBtn.Tag is BookmarkItem targetBookmark)
                {
                    if (sourceBookmark != null && sourceBookmark != targetBookmark)
                    {
                        int sourceIndex = _currentSettings.Bookmarks.IndexOf(sourceBookmark);
                        int targetIndex = _currentSettings.Bookmarks.IndexOf(targetBookmark);

                        if (sourceIndex >= 0 && targetIndex >= 0)
                        {
                            _currentSettings.Bookmarks.RemoveAt(sourceIndex);
                            _currentSettings.Bookmarks.Insert(targetIndex, sourceBookmark);
                            _currentSettings.Save();
                            RefreshBookmarksUI();
                        }
                    }
                }
            }
        }

        private async Task<TabItem?> AddNewTab(string? url = null, bool selectTab = true, bool isPinned = false)
        {
            try
            {
                if (!Directory.Exists(_userDataFolder)) Directory.CreateDirectory(_userDataFolder);
                if (!Directory.Exists(_permanentCacheFolder)) Directory.CreateDirectory(_permanentCacheFolder);

                var webView = new WebView2
                {
                    DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 18, 19, 22)
                };
                var tabItem = new TabItem
                {
                    Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(18, 19, 22))
                };

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
                    if (_environment == null)
                    {
                        var options = new CoreWebView2EnvironmentOptions();
                        var browserArguments = new List<string>
                        {
                            $"--disk-cache-dir=\"{_permanentCacheFolder}\"",
                            $"--disk-cache-size={ChromiumDiskCacheBytes}",
                            "--aggressive-cache-discard=false",
                            "--disable-features=BackForwardCacheMemoryControls"
                        };

                        // Always route through our local proxy bridge to support dynamic runtime configuration
                        browserArguments.Add($"--proxy-server=\"http://127.0.0.1:{ProxyBridge.Port}\"");
                        options.AdditionalBrowserArguments = string.Join(" ", browserArguments);
                        _environment = await CoreWebView2Environment.CreateAsync(null, _userDataFolder, options);
                    }
                }
                finally { _envLock.Release(); }

                // Ensure CoreWebView2 is initialized before using it
                await webView.EnsureCoreWebView2Async(_environment);

                if (webView.CoreWebView2 == null) throw new Exception("CoreWebView2 initialization failed");
                _coreWebViewTabMap[webView.CoreWebView2] = tabItem;

                webView.DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 18, 19, 22);
                try { webView.CoreWebView2.Profile.PreferredColorScheme = CoreWebView2PreferredColorScheme.Dark; } catch { }

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
                InjectSnippetHelperScript(webView);

                webView.NavigationStarting += (s, e) =>
                {
                    if (BrowserSettingsPageHelper.IsSettingsUrl(e.Uri))
                    {
                        e.Cancel = true;
                        _tabStates[tabItem].IsSettingsTab = true;
                        _tabStates[tabItem].IsCombinerTab = false;
                        _internalNewTabs.Remove(tabItem);
                        UpdateTabHeader(tabItem, "⚙", "تنظیمات مرورگر");
                        if (BrowserTabs.SelectedItem == tabItem && TxtUrl != null) TxtUrl.Text = BrowserSettingsPageHelper.SettingsUrl;
                        SetTabLoadingState(tabItem, false);
                        UpdateStopButtonState();
                        UpdateTabStatusOverlay(tabItem);
                        webView.CoreWebView2.NavigateToString(BrowserSettingsPageHelper.GetSettingsHtml());
                        return;
                    }

                    if (BrowserCombinerPageHelper.IsCombinerUrl(e.Uri))
                    {
                        e.Cancel = true;
                        _tabStates[tabItem].IsCombinerTab = true;
                        _tabStates[tabItem].IsSettingsTab = false;
                        _internalNewTabs.Remove(tabItem);
                        UpdateTabHeader(tabItem, "🧩", "مدیریت کمباینر");
                        if (BrowserTabs.SelectedItem == tabItem && TxtUrl != null) TxtUrl.Text = BrowserCombinerPageHelper.CombinerUrl;
                        SetTabLoadingState(tabItem, false);
                        UpdateStopButtonState();
                        UpdateTabStatusOverlay(tabItem);
                        webView.CoreWebView2.NavigateToString(BrowserCombinerPageHelper.GetCombinerHtml());
                        return;
                    }

                    if (_internalNewTabs.Contains(tabItem) && !string.IsNullOrWhiteSpace(e.Uri) && e.Uri != "about:blank")
                        _internalNewTabs.Remove(tabItem);

                    if (BrowserTabs.SelectedItem == tabItem && GetCurrentBrowser() == webView && TxtUrl != null)
                    {
                        if (!string.IsNullOrWhiteSpace(e.Uri) && e.Uri != "about:blank" && !e.Uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                        {
                            TxtUrl.Text = e.Uri;
                        }
                    }

                    UpdateTabHeader(tabItem, GetIconForUrl(e.Uri), "Loading...");
                    SetTabLoadingState(tabItem, true);
                    ResetTabNetworkStats(tabItem);
                };

                webView.NavigationCompleted += (s, e) =>
                {
                    if (BrowserTabs.SelectedItem == tabItem && GetCurrentBrowser() == webView && TxtUrl != null)
                    {
                        string? currentUrl = webView.CoreWebView2?.Source ?? webView.Source?.ToString();
                        bool isInternalNewTab = _internalNewTabs.Contains(tabItem) && (string.IsNullOrEmpty(currentUrl) || currentUrl == "about:blank" || currentUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase));
                        TxtUrl.Text = isInternalNewTab ? "" : currentUrl ?? "";
                    }

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
                else if (BrowserSettingsPageHelper.IsSettingsUrl(url))
                {
                    _tabStates[tabItem].IsSettingsTab = true;
                    _tabStates[tabItem].IsCombinerTab = false;
                    UpdateTabHeader(tabItem, "⚙", "تنظیمات مرورگر");
                    if (BrowserTabs.SelectedItem == tabItem && TxtUrl != null) TxtUrl.Text = BrowserSettingsPageHelper.SettingsUrl;
                    webView.CoreWebView2.NavigateToString(BrowserSettingsPageHelper.GetSettingsHtml());
                }
                else if (BrowserCombinerPageHelper.IsCombinerUrl(url))
                {
                    _tabStates[tabItem].IsCombinerTab = true;
                    _tabStates[tabItem].IsSettingsTab = false;
                    UpdateTabHeader(tabItem, "🧩", "مدیریت کمباینر");
                    if (BrowserTabs.SelectedItem == tabItem && TxtUrl != null) TxtUrl.Text = BrowserCombinerPageHelper.CombinerUrl;
                    webView.CoreWebView2.NavigateToString(BrowserCombinerPageHelper.GetCombinerHtml());
                }
                else
                {
                    if (BrowserTabs.SelectedItem == tabItem && TxtUrl != null) TxtUrl.Text = url;
                    webView.CoreWebView2.Navigate(url);
                }

                void SyncUrlToBar()
                {
                    if (_tabStates.TryGetValue(tabItem, out var tabState))
                    {
                        if (tabState.IsSettingsTab)
                        {
                            if (BrowserTabs.SelectedItem == tabItem && TxtUrl != null) TxtUrl.Text = BrowserSettingsPageHelper.SettingsUrl;
                            UpdateTabHeader(tabItem, "⚙", "تنظیمات مرورگر");
                            return;
                        }
                        if (tabState.IsCombinerTab)
                        {
                            if (BrowserTabs.SelectedItem == tabItem && TxtUrl != null) TxtUrl.Text = BrowserCombinerPageHelper.CombinerUrl;
                            UpdateTabHeader(tabItem, "🧩", "مدیریت کمباینر");
                            return;
                        }
                    }

                    string? currentUrl = webView.CoreWebView2?.Source ?? webView.Source?.ToString();
                    bool isInternalNewTab = _internalNewTabs.Contains(tabItem) && (string.IsNullOrEmpty(currentUrl) || currentUrl == "about:blank" || currentUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase));
                    if (BrowserTabs.SelectedItem == tabItem && (GetCurrentBrowser() == webView || BrowserTabs.Items.Count <= 1))
                    {
                        if (TxtUrl != null) TxtUrl.Text = isInternalNewTab ? "" : currentUrl ?? "";
                    }
                    if (!string.IsNullOrEmpty(currentUrl) && currentUrl != "about:blank" && !currentUrl.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                    {
                        _internalNewTabs.Remove(tabItem);
                        _currentSettings.LastUrl = currentUrl;
                        SaveSession();
                        string icon = GetIconForUrl(currentUrl);
                        string title = webView.CoreWebView2?.DocumentTitle ?? "Loading...";
                        UpdateTabHeader(tabItem, icon, title);
                    }
                }

                webView.SourceChanged += (s, e) => SyncUrlToBar();
                webView.CoreWebView2.SourceChanged += (s, e) => SyncUrlToBar();
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

                    var splitItem = new MenuItem
                    {
                        Header = state.IsSplitView ? "Close Split View" : "Split View / Dual Screen"
                    };
                    splitItem.Click += (_, _) => ToggleSplitView(tabItem);
                    menu.Items.Add(splitItem);

                    if (state.IsSplitView)
                    {
                        var unsplitItem = new MenuItem { Header = "Move Right Side to New Tab" };
                        unsplitItem.Click += (_, _) => UnsplitSecondaryToNewTab(tabItem, moveLeft: false);
                        menu.Items.Add(unsplitItem);
                    }

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
            public bool IsSettingsTab { get; set; }
            public bool IsCombinerTab { get; set; }

            // Split View Properties
            public bool IsSplitView { get; set; }
            public WebView2? SecondaryWebView { get; set; }
            public Grid? SplitContainer { get; set; }
            public Border? LeftPaneBorder { get; set; }
            public Border? RightPaneBorder { get; set; }
            public System.Windows.Controls.Primitives.Popup? LeftHeaderPopup { get; set; }
            public System.Windows.Controls.Primitives.Popup? RightHeaderPopup { get; set; }
            public int ActivePaneIndex { get; set; } = 0; // 0 = Left (Primary), 1 = Right (Secondary)
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
            _currentSettings.Save(CurrentProfile);
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
            if (browser != null && TxtUrl != null)
            {
                if (BrowserTabs.SelectedItem is TabItem selTab && _tabStates.TryGetValue(selTab, out var state) && state.IsSettingsTab)
                {
                    TxtUrl.Text = BrowserSettingsPageHelper.SettingsUrl;
                }
                else
                {
                    bool isInternalNewTab = BrowserTabs.SelectedItem is TabItem tab && _internalNewTabs.Contains(tab);
                    string? url = browser.CoreWebView2?.Source ?? browser.Source?.ToString();
                    TxtUrl.Text = (isInternalNewTab || url == "about:blank" || (url != null && url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))) ? "" : url ?? "";
                }
            }
            UpdateStopButtonState();
            UpdateTabStatusOverlay(BrowserTabs.SelectedItem as TabItem);

            foreach (var s in _tabStates.Values)
            {
                if (s.IsSplitView)
                {
                    bool isThisTabSelected = (s.Tab == BrowserTabs.SelectedItem);
                    bool shouldBeOpen = isThisTabSelected && this.IsActive && this.WindowState != WindowState.Minimized;
                    if (s.LeftHeaderPopup != null) s.LeftHeaderPopup.IsOpen = shouldBeOpen;
                    if (s.RightHeaderPopup != null) s.RightHeaderPopup.IsOpen = shouldBeOpen;
                }
            }
        }

        public void UpdateSplitViewPopupsVisibility(bool isVisible)
        {
            foreach (var s in _tabStates.Values)
            {
                if (s.IsSplitView)
                {
                    bool isThisTabSelected = (s.Tab == BrowserTabs.SelectedItem);
                    bool shouldBeOpen = isVisible && isThisTabSelected && this.IsActive && this.WindowState != WindowState.Minimized;
                    if (s.LeftHeaderPopup != null) s.LeftHeaderPopup.IsOpen = shouldBeOpen;
                    if (s.RightHeaderPopup != null) s.RightHeaderPopup.IsOpen = shouldBeOpen;
                }
            }
        }

        public void RefreshSplitViewPopupsPosition()
        {
            if (!this.IsActive || this.WindowState == WindowState.Minimized)
            {
                UpdateSplitViewPopupsVisibility(false);
                return;
            }

            foreach (var s in _tabStates.Values)
            {
                if (s.IsSplitView && s.Tab == BrowserTabs.SelectedItem)
                {
                    if (s.LeftHeaderPopup != null && s.LeftHeaderPopup.IsOpen)
                    {
                        s.LeftHeaderPopup.HorizontalOffset += 0.0001;
                        s.LeftHeaderPopup.HorizontalOffset -= 0.0001;
                    }
                    if (s.RightHeaderPopup != null && s.RightHeaderPopup.IsOpen)
                    {
                        s.RightHeaderPopup.HorizontalOffset += 0.0001;
                        s.RightHeaderPopup.HorizontalOffset -= 0.0001;
                    }
                }
            }
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
                if (state.IsSplitView && state.SecondaryWebView != null)
                {
                    if (state.SecondaryWebView.CoreWebView2 != null)
                    {
                        _coreWebViewTabMap.Remove(state.SecondaryWebView.CoreWebView2);
                        state.SecondaryWebView.CoreWebView2.ContextMenuRequested -= CoreWebView2_ContextMenuRequested;
                    }
                    state.SecondaryWebView.Dispose();
                }

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
        private void BtnReload_Click(object? sender, RoutedEventArgs e)
        {
            bool isShiftOrCtrlPressed = (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) != 0;
            if (isShiftOrCtrlPressed)
            {
                HardReloadCurrentTab();
            }
            else
            {
                GetCurrentBrowser()?.Reload();
            }
        }

        public async void HardReloadCurrentTab()
        {
            var browser = GetCurrentBrowser();
            if (browser == null || browser.CoreWebView2 == null) return;

            try
            {
                // 1) Clear local storage, session storage, service workers & cache storage via script
                string script = @"
                    (function() {
                        try { localStorage.clear(); } catch(e){}
                        try { sessionStorage.clear(); } catch(e){}
                        try {
                            if (navigator.serviceWorker) {
                                navigator.serviceWorker.getRegistrations().then(regs => {
                                    for(let reg of regs) reg.unregister();
                                });
                            }
                        } catch(e){}
                        try {
                            if (window.caches) {
                                caches.keys().then(names => {
                                    for(let name of names) caches.delete(name);
                                });
                            }
                        } catch(e){}
                    })();
                ";
                await browser.CoreWebView2.ExecuteScriptAsync(script);

                // 2) Clear Disk Cache and DOM Storage via CoreWebView2 Profile API
                var dataKinds = CoreWebView2BrowsingDataKinds.DiskCache | 
                                CoreWebView2BrowsingDataKinds.LocalStorage | 
                                CoreWebView2BrowsingDataKinds.CacheStorage | 
                                CoreWebView2BrowsingDataKinds.IndexedDb |
                                CoreWebView2BrowsingDataKinds.WebSql;

                await browser.CoreWebView2.Profile.ClearBrowsingDataAsync(dataKinds);

                // 3) Reload ignoring cache
                browser.CoreWebView2.Reload();
                UpdateStatus("⚡ Hard Reload Executed: Cache & LocalStorage Cleared!", "Browser");
            }
            catch
            {
                browser.Reload();
                UpdateStatus("⚡ Hard Reload Completed", "Browser");
            }
        }
        private void BtnGo_Click(object? sender, RoutedEventArgs e) => Navigate();
        private void TxtUrl_KeyDown(object? sender, System.Windows.Input.KeyEventArgs e) { if (e.Key == Key.Enter) Navigate(); }

        public async void ToggleSplitView(TabItem? targetTab = null, string? secondaryUrl = null)
        {
            var tabItem = targetTab ?? (BrowserTabs.SelectedItem as TabItem);
            if (tabItem == null || !TryGetTabState(tabItem, out var state)) return;

            if (state.IsSplitView)
            {
                UnsplitSecondaryToNewTab(tabItem, moveLeft: false);
                return;
            }

            if (state.PrimaryWebView == null) return;

            WebView2? existingSecondaryWebView = null;

            // If user has another open tab and didn't specify secondaryUrl, merge the other tab's live WebView2 into split view
            if (string.IsNullOrWhiteSpace(secondaryUrl) && BrowserTabs.Items.Count > 1)
            {
                int currentIndex = BrowserTabs.Items.IndexOf(tabItem);
                int otherIndex = (currentIndex == 0) ? 1 : currentIndex - 1;
                if (otherIndex >= 0 && otherIndex < BrowserTabs.Items.Count)
                {
                    var otherTab = BrowserTabs.Items[otherIndex] as TabItem;
                    if (otherTab != null && TryGetTabState(otherTab, out var otherState) && otherState.PrimaryWebView != null)
                    {
                        existingSecondaryWebView = otherState.PrimaryWebView;
                        DetachFromParent(existingSecondaryWebView);
                        otherTab.Content = null;

                        _tabHeaderMap.Remove(otherTab);
                        _tabNetworkStats.Remove(otherTab);
                        _tabStates.Remove(otherTab);
                        _internalNewTabs.Remove(otherTab);
                        BrowserTabs.Items.Remove(otherTab);
                    }
                }
            }

            state.IsSplitView = true;

            WebView2 secondaryWebView = existingSecondaryWebView ?? new WebView2
            {
                DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 18, 19, 22)
            };
            state.SecondaryWebView = secondaryWebView;

            // Build SplitContainer Grid FIRST so secondaryWebView is attached to the Visual Tree
            var splitGrid = new Grid();
            splitGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            splitGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6, GridUnitType.Pixel) });
            splitGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Create Left Pane
            var leftPaneGrid = new Grid();

            DetachFromParent(state.PrimaryWebView);
            tabItem.Content = null; // Clear logical child
            leftPaneGrid.Children.Add(state.PrimaryWebView);

            var leftBorder = new Border
            {
                BorderBrush = (System.Windows.Media.Brush)FindResource("BorderBrush"),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(2),
                Child = leftPaneGrid
            };
            Grid.SetColumn(leftBorder, 0);
            splitGrid.Children.Add(leftBorder);
            state.LeftPaneBorder = leftBorder;

            var leftPopup = CreatePaneBottomPopup(leftBorder,
                onSwap: () => SetActivePane(tabItem, 0),
                onClose: () => CloseSplitView(tabItem, keepLeft: false));
            state.LeftHeaderPopup = leftPopup;

            // Divider Splitter
            var splitter = new GridSplitter
            {
                Width = 6,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Stretch,
                Background = (System.Windows.Media.Brush)FindResource("BorderBrush"),
                Cursor = System.Windows.Input.Cursors.SizeWE
            };
            Grid.SetColumn(splitter, 1);
            splitGrid.Children.Add(splitter);

            // Create Right Pane
            var rightPaneGrid = new Grid();

            DetachFromParent(secondaryWebView);
            rightPaneGrid.Children.Add(secondaryWebView);

            var rightBorder = new Border
            {
                BorderBrush = (System.Windows.Media.Brush)FindResource("BorderBrush"),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(2),
                Child = rightPaneGrid
            };
            Grid.SetColumn(rightBorder, 2);
            splitGrid.Children.Add(rightBorder);
            state.RightPaneBorder = rightBorder;

            var rightPopup = CreatePaneBottomPopup(rightBorder,
                onSwap: () => SetActivePane(tabItem, 1),
                onClose: () => CloseSplitView(tabItem, keepLeft: true));
            state.RightHeaderPopup = rightPopup;

            // Mouse down and focus listeners for active pane switching
            leftBorder.PreviewMouseDown += (s, e) => SetActivePane(tabItem, 0);
            leftPaneGrid.PreviewMouseDown += (s, e) => SetActivePane(tabItem, 0);
            if (state.PrimaryWebView != null)
            {
                state.PrimaryWebView.GotFocus += (s, e) => SetActivePane(tabItem, 0);
                state.PrimaryWebView.GotKeyboardFocus += (s, e) => SetActivePane(tabItem, 0);
                state.PrimaryWebView.PreviewMouseDown += (s, e) => SetActivePane(tabItem, 0);
            }

            rightBorder.PreviewMouseDown += (s, e) => SetActivePane(tabItem, 1);
            rightPaneGrid.PreviewMouseDown += (s, e) => SetActivePane(tabItem, 1);
            if (state.SecondaryWebView != null)
            {
                state.SecondaryWebView.GotFocus += (s, e) => SetActivePane(tabItem, 1);
                state.SecondaryWebView.GotKeyboardFocus += (s, e) => SetActivePane(tabItem, 1);
                state.SecondaryWebView.PreviewMouseDown += (s, e) => SetActivePane(tabItem, 1);
            }

            state.SplitContainer = splitGrid;
            tabItem.Content = splitGrid;

            // Initialize secondary WebView if it was newly created
            if (existingSecondaryWebView == null)
            {
                await _envLock.WaitAsync();
                try
                {
                    if (_environment == null)
                    {
                        var options = new CoreWebView2EnvironmentOptions();
                        var browserArguments = new List<string>
                        {
                            $"--disk-cache-dir=\"{_permanentCacheFolder}\"",
                            $"--disk-cache-size={ChromiumDiskCacheBytes}",
                            "--aggressive-cache-discard=false",
                            "--disable-features=BackForwardCacheMemoryControls",
                            $"--proxy-server=\"http://127.0.0.1:{ProxyBridge.Port}\""
                        };
                        options.AdditionalBrowserArguments = string.Join(" ", browserArguments);
                        _environment = await CoreWebView2Environment.CreateAsync(null, _userDataFolder, options);
                    }
                }
                finally { _envLock.Release(); }

                try
                {
                    await secondaryWebView.EnsureCoreWebView2Async(_environment);
                    if (secondaryWebView.CoreWebView2 != null)
                    {
                        _coreWebViewTabMap[secondaryWebView.CoreWebView2] = tabItem;
                        secondaryWebView.DefaultBackgroundColor = System.Drawing.Color.FromArgb(255, 18, 19, 22);
                        try { secondaryWebView.CoreWebView2.Profile.PreferredColorScheme = CoreWebView2PreferredColorScheme.Dark; } catch { }
                        secondaryWebView.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;
                        secondaryWebView.CoreWebView2.Settings.IsGeneralAutofillEnabled = false;
                        secondaryWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
                        secondaryWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                        secondaryWebView.CoreWebView2.Settings.IsScriptEnabled = true;

                        secondaryWebView.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
                        secondaryWebView.CoreWebView2.WebResourceRequested += CoreWebView2_WebResourceRequested;
                        secondaryWebView.CoreWebView2.WebResourceResponseReceived += CoreWebView2_WebResourceResponseReceived;
                        secondaryWebView.CoreWebView2.DownloadStarting += CoreWebView2_DownloadStarting;
                        secondaryWebView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
                        secondaryWebView.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;
                        secondaryWebView.CoreWebView2.ContextMenuRequested += CoreWebView2_ContextMenuRequested;
                        InjectSnippetHelperScript(secondaryWebView);

                        secondaryWebView.NavigationStarting += (s, e) =>
                        {
                            if (BrowserSettingsPageHelper.IsSettingsUrl(e.Uri))
                            {
                                e.Cancel = true;
                                if (state.ActiveWebView == secondaryWebView)
                                {
                                    SetTabLoadingState(tabItem, false);
                                    UpdateStopButtonState();
                                    UpdateTabStatusOverlay(tabItem);
                                    if (BrowserTabs.SelectedItem == tabItem && TxtUrl != null) TxtUrl.Text = BrowserSettingsPageHelper.SettingsUrl;
                                }
                                secondaryWebView.CoreWebView2.NavigateToString(BrowserSettingsPageHelper.GetSettingsHtml());
                                return;
                            }

                            if (state.ActiveWebView == secondaryWebView)
                            {
                                SetTabLoadingState(tabItem, true);
                            }
                        };

                        secondaryWebView.NavigationCompleted += (s, e) =>
                        {
                            if (state.ActiveWebView == secondaryWebView)
                            {
                                SetTabLoadingState(tabItem, false);
                                UpdateStopButtonState();
                                UpdateTabStatusOverlay(tabItem);
                            }
                        };

                        secondaryWebView.SourceChanged += (s, e) =>
                        {
                            if (state.ActiveWebView == secondaryWebView && BrowserTabs.SelectedItem == tabItem)
                            {
                                if (TxtUrl != null) TxtUrl.Text = secondaryWebView.Source?.ToString() ?? "";
                            }
                        };

                        ApplyBrowserSettingsTo(secondaryWebView);

                        if (string.IsNullOrWhiteSpace(secondaryUrl))
                        {
                            secondaryWebView.CoreWebView2.NavigateToString(GetNewTabPageHtml());
                        }
                        else
                        {
                            secondaryWebView.CoreWebView2.Navigate(secondaryUrl);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to initialize secondary WebView2: {ex.Message}");
                }
            }
            else
            {
                if (secondaryWebView.CoreWebView2 != null)
                {
                    _coreWebViewTabMap[secondaryWebView.CoreWebView2] = tabItem;
                }
            }

            SetActivePane(tabItem, 0);
        }

        private System.Windows.Controls.Primitives.Popup CreatePaneBottomPopup(FrameworkElement placementTarget, Action onSwap, Action onClose)
        {
            var toolsPanel = new StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 2, 4, 2)
            };

            var btnIcon = new System.Windows.Controls.Button
            {
                ToolTip = "تغییر / جابجایی صفحه (Switch / Swap)",
                Width = 22, Height = 22,
                Margin = new Thickness(0, 0, 3, 0),
                Padding = new Thickness(0),
                Style = (Style)FindResource("ProfileAvatarBtnStyle"),
                Content = new System.Windows.Shapes.Path
                {
                    Data = (Geometry)FindResource("IconChrome"),
                    Fill = (System.Windows.Media.Brush)FindResource("ForegroundMutedBrush"),
                    Width = 14, Height = 14,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            btnIcon.Click += (s, e) => onSwap();
            toolsPanel.Children.Add(btnIcon);

            var btnClose = new System.Windows.Controls.Button
            {
                ToolTip = "بستن این صفحه اسپلیت (Close)",
                Width = 20, Height = 20,
                Margin = new Thickness(0),
                Padding = new Thickness(0),
                Style = (Style)FindResource("TabCloseBtnStyle"),
                Content = new System.Windows.Shapes.Path
                {
                    Data = (Geometry)FindResource("IconClose"),
                    Fill = (System.Windows.Media.Brush)FindResource("ForegroundMutedBrush"),
                    Width = 9, Height = 9,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            btnClose.Click += (s, e) => onClose();
            toolsPanel.Children.Add(btnClose);

            var badgeBorder = new Border
            {
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(235, 0x1E, 0x1F, 0x22)),
                BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(180, 0x3E, 0x42, 0x48)),
                BorderThickness = new Thickness(1, 1, 0, 0),
                CornerRadius = new CornerRadius(6, 0, 0, 0),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                Child = toolsPanel
            };

            var popup = new System.Windows.Controls.Primitives.Popup
            {
                PlacementTarget = placementTarget,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Custom,
                AllowsTransparency = true,
                IsOpen = true,
                StaysOpen = true,
                Child = badgeBorder
            };

            popup.CustomPopupPlacementCallback = (popupSize, targetSize, offset) =>
            {
                // Align to bottom-right corner inside placement target
                double x = Math.Max(0, targetSize.Width - popupSize.Width - 1);
                double y = Math.Max(0, targetSize.Height - popupSize.Height - 1);
                return new[] { new System.Windows.Controls.Primitives.CustomPopupPlacement(new System.Windows.Point(x, y), System.Windows.Controls.Primitives.PopupPrimaryAxis.Horizontal) };
            };

            // Keep popup position 100% updated in real-time on resize / split drag
            placementTarget.SizeChanged += (s, e) =>
            {
                if (popup.IsOpen)
                {
                    popup.HorizontalOffset += 0.0001;
                    popup.HorizontalOffset -= 0.0001;
                }
            };

            return popup;
        }

        private void SetActivePane(TabItem tabItem, int paneIndex)
        {
            if (!TryGetTabState(tabItem, out var state) || !state.IsSplitView) return;

            state.ActivePaneIndex = paneIndex;
            var activeBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0x7A, 0xCC)); // Modern Blue Accent
            var normalBrush = (System.Windows.Media.Brush)FindResource("BorderBrush");

            if (paneIndex == 0)
            {
                state.ActiveWebView = state.PrimaryWebView;
                if (state.LeftPaneBorder != null) state.LeftPaneBorder.BorderBrush = activeBrush;
                if (state.RightPaneBorder != null) state.RightPaneBorder.BorderBrush = normalBrush;
            }
            else
            {
                state.ActiveWebView = state.SecondaryWebView;
                if (state.LeftPaneBorder != null) state.LeftPaneBorder.BorderBrush = normalBrush;
                if (state.RightPaneBorder != null) state.RightPaneBorder.BorderBrush = activeBrush;
            }

            if (BrowserTabs.SelectedItem == tabItem)
            {
                var activeBrowser = GetCurrentBrowser();
                if (activeBrowser != null && TxtUrl != null)
                {
                    TxtUrl.Text = activeBrowser.Source?.ToString() ?? "";
                }
                UpdateStopButtonState();
                UpdateTabStatusOverlay(tabItem);
            }
        }

        private void CloseSplitView(TabItem tabItem, bool keepLeft = true)
        {
            if (!TryGetTabState(tabItem, out var state) || !state.IsSplitView) return;

            if (!keepLeft && state.SecondaryWebView != null)
            {
                var oldPrimary = state.PrimaryWebView;
                state.PrimaryWebView = state.SecondaryWebView;
                state.SecondaryWebView = oldPrimary;
            }

            if (state.SplitContainer != null)
            {
                state.SplitContainer.Children.Clear();
                state.SplitContainer = null;
            }

            if (state.SecondaryWebView != null)
            {
                if (state.SecondaryWebView.CoreWebView2 != null)
                {
                    _coreWebViewTabMap.Remove(state.SecondaryWebView.CoreWebView2);
                    state.SecondaryWebView.CoreWebView2.ContextMenuRequested -= CoreWebView2_ContextMenuRequested;
                }
                state.SecondaryWebView.Dispose();
                state.SecondaryWebView = null;
            }

            if (state.LeftHeaderPopup != null)
            {
                state.LeftHeaderPopup.IsOpen = false;
                state.LeftHeaderPopup = null;
            }
            if (state.RightHeaderPopup != null)
            {
                state.RightHeaderPopup.IsOpen = false;
                state.RightHeaderPopup = null;
            }

            state.LeftPaneBorder = null;
            state.RightPaneBorder = null;
            state.IsSplitView = false;
            state.ActivePaneIndex = 0;
            state.ActiveWebView = state.PrimaryWebView;

            if (state.PrimaryWebView != null)
            {
                DetachFromParent(state.PrimaryWebView);
                tabItem.Content = state.PrimaryWebView;
            }

            if (BrowserTabs.SelectedItem == tabItem && TxtUrl != null && state.PrimaryWebView?.Source != null)
            {
                TxtUrl.Text = state.PrimaryWebView.Source.ToString();
            }
            UpdateStopButtonState();
            UpdateTabStatusOverlay(tabItem);
        }

        private void SwapSplitPanes(TabItem tabItem)
        {
            if (!TryGetTabState(tabItem, out var state) || !state.IsSplitView) return;

            var tempWeb = state.PrimaryWebView;
            state.PrimaryWebView = state.SecondaryWebView;
            state.SecondaryWebView = tempWeb;

            if (state.LeftPaneBorder?.Child is Grid leftGrid && state.RightPaneBorder?.Child is Grid rightGrid)
            {
                if (state.SecondaryWebView != null) DetachFromParent(state.SecondaryWebView);
                if (state.PrimaryWebView != null) DetachFromParent(state.PrimaryWebView);

                if (state.PrimaryWebView != null)
                {
                    leftGrid.Children.Insert(0, state.PrimaryWebView);
                }

                if (state.SecondaryWebView != null)
                {
                    rightGrid.Children.Insert(0, state.SecondaryWebView);
                }
            }

            SetActivePane(tabItem, state.ActivePaneIndex == 0 ? 1 : 0);
        }

        private void UnsplitSecondaryToNewTab(TabItem tabItem, bool moveLeft)
        {
            if (!TryGetTabState(tabItem, out var state) || !state.IsSplitView) return;

            var targetWebView = moveLeft ? state.PrimaryWebView : state.SecondaryWebView;
            var remainingWebView = moveLeft ? state.SecondaryWebView : state.PrimaryWebView;

            if (targetWebView == null || remainingWebView == null) return;

            // Remove WebView controls from SplitContainer without disposing targetWebView
            if (state.LeftPaneBorder?.Child is Grid leftGrid) leftGrid.Children.Clear();
            if (state.RightPaneBorder?.Child is Grid rightGrid) rightGrid.Children.Clear();

            if (state.SplitContainer != null)
            {
                state.SplitContainer.Children.Clear();
                state.SplitContainer = null;
            }

            DetachFromParent(targetWebView);
            DetachFromParent(remainingWebView);

            if (state.LeftHeaderPopup != null)
            {
                state.LeftHeaderPopup.IsOpen = false;
                state.LeftHeaderPopup = null;
            }
            if (state.RightHeaderPopup != null)
            {
                state.RightHeaderPopup.IsOpen = false;
                state.RightHeaderPopup = null;
            }

            // Left tab keeps remainingWebView as its primary content
            state.PrimaryWebView = remainingWebView;
            state.SecondaryWebView = null;
            state.LeftPaneBorder = null;
            state.RightPaneBorder = null;
            state.IsSplitView = false;
            state.ActivePaneIndex = 0;
            state.ActiveWebView = remainingWebView;
            tabItem.Content = remainingWebView;

            if (remainingWebView.CoreWebView2 != null)
            {
                string icon = GetIconForUrl(remainingWebView.Source?.ToString());
                string title = remainingWebView.CoreWebView2.DocumentTitle ?? "Tab";
                UpdateTabHeader(tabItem, icon, title);
            }

            // Move targetWebView into a brand NEW TabItem live!
            var newTabItem = new TabItem();
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

            newTabItem.Header = headerPanel;
            newTabItem.ContextMenu = CreateTabContextMenu(newTabItem);
            newTabItem.Content = targetWebView;

            _tabHeaderMap[newTabItem] = (headerText, loadingBadge);
            _tabStates[newTabItem] = new BrowserTabState
            {
                Tab = newTabItem,
                PrimaryWebView = targetWebView,
                ActiveWebView = targetWebView
            };

            if (targetWebView.CoreWebView2 != null)
            {
                _coreWebViewTabMap[targetWebView.CoreWebView2] = newTabItem;
                string icon = GetIconForUrl(targetWebView.Source?.ToString());
                string title = targetWebView.CoreWebView2.DocumentTitle ?? "Tab";
                UpdateTabHeader(newTabItem, icon, title);
            }

            int insertIndex = BrowserTabs.Items.IndexOf(tabItem) + 1;
            if (insertIndex >= 0 && insertIndex <= BrowserTabs.Items.Count)
                BrowserTabs.Items.Insert(insertIndex, newTabItem);
            else
                BrowserTabs.Items.Add(newTabItem);

            SaveSession();
            UpdateStopButtonState();
            UpdateTabStatusOverlay(tabItem);
        }

        public async void OpenSettingsTab()
        {
            foreach (TabItem item in BrowserTabs.Items)
            {
                if (_tabStates.TryGetValue(item, out var state) && state.IsSettingsTab)
                {
                    BrowserTabs.SelectedItem = item;
                    return;
                }
            }

            var newTab = await AddNewTab(BrowserSettingsPageHelper.SettingsUrl, selectTab: true);
            if (newTab != null && _tabStates.TryGetValue(newTab, out var s))
            {
                s.IsSettingsTab = true;
            }
        }

        public async void OpenCombinerTab()
        {
            foreach (TabItem item in BrowserTabs.Items)
            {
                if (_tabStates.TryGetValue(item, out var state) && state.IsCombinerTab)
                {
                    BrowserTabs.SelectedItem = item;
                    return;
                }
            }

            var newTab = await AddNewTab(BrowserCombinerPageHelper.CombinerUrl, selectTab: true);
            if (newTab != null && _tabStates.TryGetValue(newTab, out var s))
            {
                s.IsCombinerTab = true;
            }
        }

        private void Navigate()
        {
            string url = TxtUrl.Text.Trim();
            if (string.IsNullOrEmpty(url)) return;

            if (BrowserSettingsPageHelper.IsSettingsUrl(url))
            {
                OpenSettingsTab();
                return;
            }

            if (BrowserCombinerPageHelper.IsCombinerUrl(url))
            {
                OpenCombinerTab();
                return;
            }

            if (!url.Contains(".") && !url.StartsWith("http")) url = "https://www.google.com/search?q=" + Uri.EscapeDataString(url);
            else if (!url.StartsWith("http")) url = "https://" + url;
            try
            {
                if (BrowserTabs.SelectedItem is TabItem tab)
                {
                    _internalNewTabs.Remove(tab);
                    if (_tabStates.TryGetValue(tab, out var state))
                    {
                        state.IsSettingsTab = false;
                        state.IsCombinerTab = false;
                    }
                }
                GetCurrentBrowser()?.CoreWebView2.Navigate(url);
            }
            catch { }
        }
    }
}
