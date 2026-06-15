using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.Linq;
using System.Windows.Threading;
using System.Threading;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using WpfDragEventArgs = System.Windows.DragEventArgs;
using WpfDragDropEffects = System.Windows.DragDropEffects;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfPanel = System.Windows.Controls.Panel;
using WpfPoint = System.Windows.Point;
using WpfTextBox = System.Windows.Controls.TextBox;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfButton = System.Windows.Controls.Button;
using WpfColor = System.Windows.Media.Color;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfTabControl = System.Windows.Controls.TabControl;
using WpfTabItem = System.Windows.Controls.TabItem;

namespace imgsaver
{
    public partial class BrowserWindow : Window
    {
        private readonly string _userDataFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "browser_profile");
        private readonly string _permanentCacheFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "web_cache");
        private const long MaxDiskCacheItemBytes = 512L * 1024L * 1024L;
        private const long ChromiumDiskCacheBytes = 4L * 1024L * 1024L * 1024L;
        private BrowserSettings _currentSettings = null!;
        private string _typeBuffer = "";
        private DispatcherTimer _statusFadeTimer = null!;
        private readonly Dictionary<TabItem, (TextBlock HeaderText, Border LoadingBadge)> _tabHeaderMap = new();
        private readonly Dictionary<TabItem, TabNetworkInfo> _tabNetworkStats = new();
        private readonly Dictionary<CoreWebView2, TabItem> _coreWebViewTabMap = new();
        private readonly Dictionary<TabItem, BrowserTabState> _tabStates = new();
        private readonly HashSet<TabItem> _internalNewTabs = new();
        private readonly HashSet<string> _handledDownloadUris = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _miniClipImportedImageUris = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _miniClipImportedImageSignatures = new(StringComparer.OrdinalIgnoreCase);
        private readonly string _miniClipImportFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "browser_mini_clip_imports");
        private readonly InputPlayer _browserRecordingPlayer = new InputPlayer();

        // Download Manager
        private DownloadManagerService _downloadService = null!;
        private DownloadManagerWindow? _downloadManagerWindow;

        // Split View Management
        private SplitViewManager _splitViewManager = null!;
        private SplitViewUIManager _splitViewUIManager = null!;
        private string _currentActivePanelId = "";
        private Dictionary<string, WpfTabControl> _panelTabControls = new();
        private bool _isSplitViewEnabled = false;

        // Shared environment to ensure all tabs use the same profile/settings
        private static CoreWebView2Environment? _sharedEnvironment;
        private static readonly SemaphoreSlim _envLock = new SemaphoreSlim(1, 1);

        // Track previous proxy settings to detect changes
        private string _previousProxyAddress = "";
        private string _previousProxyPort = "";
        private bool _previousProxyEnabled = false;
        private string _previousProxyType = "http";

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        public BrowserWindow()
        {
            InitializeComponent();

            this.MaxHeight = SystemParameters.WorkArea.Height + 16;
            this.MaxWidth = SystemParameters.WorkArea.Width + 16;

            InitializeStatusTimer();
            InitializeDownloadService();
            InitializeSplitView();
            RefreshSettings();
            SaveCurrentProxySettings(); // Initialize proxy tracking
            RefreshBookmarksUI();

            this.StateChanged += BrowserWindow_StateChanged;

            InitializeTabs();
        }

        protected override void OnClosed(EventArgs e)
        {
            _browserRecordingPlayer.Stop();
            base.OnClosed(e);
        }

        private void InitializeDownloadService()
        {
            _downloadService = new DownloadManagerService();
            SyncDownloadProxySettings();
        }

        private void InitializeSplitView()
        {
            _splitViewManager = new SplitViewManager();
            _currentActivePanelId = _splitViewManager.GetRootState().GroupId;
            _isSplitViewEnabled = false;
            
            // Initialize UI Manager
            if (SplitViewContainer is SplitViewContainer container)
            {
                _splitViewUIManager = new SplitViewUIManager(container);
                _splitViewUIManager.InitializeRootPanel(_currentActivePanelId);
                
                // Wire up event handlers
                container.OnPanelClosed += SplitViewContainer_OnPanelClosed;
                container.OnPanelActivated += SplitViewContainer_OnPanelActivated;
            }
        }

        private async void InitializeTabs()
        {
            // Load split view state if it was enabled before
            LoadSplitViewState();

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
            SyncDownloadProxySettings();
            if (!_currentSettings.AutoHideStatus)
            {
                StatusOverlay.Visibility = Visibility.Visible;
                StatusOverlay.Opacity = 1;
                _statusFadeTimer?.Stop();
            }
        }

        private bool ProxySettingsChanged()
        {
            return _currentSettings.ProxyEnabled != _previousProxyEnabled ||
                   _currentSettings.ProxyAddress != _previousProxyAddress ||
                   _currentSettings.ProxyPort != _previousProxyPort ||
                   _currentSettings.ProxyType != _previousProxyType;
        }

        private void SaveCurrentProxySettings()
        {
            _previousProxyEnabled = _currentSettings.ProxyEnabled;
            _previousProxyAddress = _currentSettings.ProxyAddress;
            _previousProxyPort = _currentSettings.ProxyPort;
            _previousProxyType = _currentSettings.ProxyType ?? "http";
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
                    if (state.PrimaryWebView.CoreWebView2 != null) _coreWebViewTabMap.Remove(state.PrimaryWebView.CoreWebView2);
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

        private string GetNewTabPageHtml() => """
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>New Tab</title>
<style>
:root{color-scheme:dark;--bg:#0e1116;--panel:#161b22;--panel2:#10151d;--border:#283241;--text:#edf2f7;--muted:#8b98a8;--accent:#3ea6ff;--green:#31c46b;--orange:#f7b955}
*{box-sizing:border-box}html,body{height:100%;margin:0}body{font-family:Segoe UI,Inter,Arial,sans-serif;background:radial-gradient(circle at 28% 18%,#1a3148 0,#111820 34%,var(--bg) 76%);color:var(--text);display:flex;align-items:center;justify-content:center;padding:32px}
.shell{width:min(980px,100%);display:grid;gap:22px}.top{display:flex;align-items:end;justify-content:space-between;gap:18px}.brand{display:flex;align-items:center;gap:14px}.mark{width:46px;height:46px;border-radius:12px;background:linear-gradient(135deg,var(--accent),var(--green));display:grid;place-items:center;font-weight:800;color:#061018;box-shadow:0 14px 40px #0008}.title{font-size:30px;font-weight:700;letter-spacing:0}.sub{color:var(--muted);font-size:13px;margin-top:3px}.clock{text-align:right}.time{font-size:28px;font-weight:650}.date{font-size:12px;color:var(--muted)}
.google-entry{background:color-mix(in srgb,var(--panel) 88%,transparent);border:1px solid var(--border);border-radius:10px;display:grid;grid-template-columns:1fr auto;align-items:center;gap:18px;padding:18px 20px;box-shadow:0 18px 48px #0007}.google-entry h1{font-size:18px;margin:0 0 5px}.google-entry p{margin:0;color:var(--muted);font-size:13px}.google-button{display:inline-flex;align-items:center;gap:10px;text-decoration:none;border:1px solid #2f78b7;background:linear-gradient(180deg,#16629b,#11476f);color:#f2fbff;border-radius:8px;padding:13px 20px;font-weight:750;box-shadow:0 12px 30px #0005}.google-button:hover{background:linear-gradient(180deg,#1b75b8,#14537f);border-color:#4da3e8}.gmark{width:24px;height:24px;border-radius:50%;background:#fff;color:#111;display:grid;place-items:center;font-weight:800}
.grid{display:grid;grid-template-columns:1.15fr .85fr;gap:18px}.panel{background:linear-gradient(180deg,color-mix(in srgb,var(--panel) 92%,transparent),color-mix(in srgb,var(--panel2) 94%,transparent));border:1px solid var(--border);border-radius:10px;padding:18px}.panel h2{font-size:13px;text-transform:uppercase;letter-spacing:.08em;color:var(--muted);margin:0 0 14px}.quick{display:grid;grid-template-columns:repeat(3,1fr);gap:10px}.tile{min-height:74px;border:1px solid #263140;background:#111923;border-radius:8px;padding:12px;text-decoration:none;color:var(--text);display:flex;flex-direction:column;justify-content:space-between}.tile:hover{border-color:#3c7fae;background:#132130}.tile b{font-size:14px}.tile small{color:var(--muted);font-size:11px}.stats{display:grid;gap:10px}.stat{display:flex;align-items:center;justify-content:space-between;border-bottom:1px solid #222b38;padding:0 0 10px}.stat:last-child{border-bottom:0;padding-bottom:0}.stat label{color:var(--muted);font-size:12px}.stat strong{font-size:15px}.hint{font-size:12px;color:var(--muted);line-height:1.65;margin-top:14px}.pill{display:inline-flex;align-items:center;gap:7px;border:1px solid #2b3a4c;background:#111923;border-radius:999px;padding:6px 10px;color:#b9c5d3;font-size:12px}
@media(max-width:760px){body{padding:18px}.top{align-items:flex-start;flex-direction:column}.clock{text-align:left}.google-entry{grid-template-columns:1fr}.google-button{justify-content:center}.grid{grid-template-columns:1fr}.quick{grid-template-columns:1fr 1fr}.title{font-size:25px}}
</style>
</head>
<body>
<main class="shell">
  <section class="top">
    <div class="brand"><div class="mark">IS</div><div><div class="title">imgsaver Browser</div><div class="sub">Clean start page for search, downloads, and focused browsing</div></div></div>
    <div class="clock"><div class="time" id="time">--:--</div><div class="date" id="date"></div></div>
  </section>
  <section class="google-entry">
    <div><h1>Start with Google</h1><p>Open Google first, then search normally from the Google page.</p></div>
    <a class="google-button" href="https://www.google.com"><span class="gmark">G</span> Open Google</a>
  </section>
  <section class="grid">
    <div class="panel"><h2>Quick Links</h2><div class="quick">
      <a class="tile" href="https://www.google.com"><b>Google</b><small>Open homepage</small></a>
      <a class="tile" href="https://chat.openai.com"><b>ChatGPT</b><small>Open assistant</small></a>
      <a class="tile" href="https://www.youtube.com"><b>YouTube</b><small>Watch videos</small></a>
      <a class="tile" href="https://github.com"><b>GitHub</b><small>Code workspace</small></a>
      <a class="tile" href="https://mail.google.com"><b>Gmail</b><small>Mail inbox</small></a>
      <a class="tile" href="https://drive.google.com"><b>Drive</b><small>Cloud files</small></a>
    </div></div>
    <aside class="panel"><h2>Session</h2><div class="stats">
      <div class="stat"><label>Status</label><strong>Ready</strong></div>
      <div class="stat"><label>New tab</label><strong>Internal</strong></div>
      <div class="stat"><label>Privacy</label><strong>No file URL</strong></div>
    </div><p class="hint">Type a phrase to search, or enter a domain like <span class="pill">example.com</span>. This page is generated by the app and does not require an external HTML file.</p></aside>
  </section>
</main>
<script>
function tick(){const now=new Date();time.textContent=now.toLocaleTimeString([], {hour:'2-digit', minute:'2-digit'});date.textContent=now.toLocaleDateString([], {weekday:'long', month:'short', day:'numeric'});}tick();setInterval(tick,1000);
</script>
</body>
</html>
""";

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

        private async Task<TabItem?> AddNewTab(string? url = null, bool selectTab = true, bool isPinned = false, string? targetPanelId = null)
        {
            try
            {
                if (!Directory.Exists(_userDataFolder)) Directory.CreateDirectory(_userDataFolder);
                if (!Directory.Exists(_permanentCacheFolder)) Directory.CreateDirectory(_permanentCacheFolder);

                var webView = new WebView2();
                var tabItem = new TabItem();

                var headerText = new TextBlock
                {
                    Text = "🌐 در حال بارگیری...",
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
                        Text = "در حال بارگیری",
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
                
                // Add tab to appropriate container (BrowserTabs or split panel)
                if (_isSplitViewEnabled && !string.IsNullOrEmpty(targetPanelId) && _panelTabControls.TryGetValue(targetPanelId, out var panelControl))
                {
                    panelControl.Items.Add(tabItem);
                    if (selectTab) panelControl.SelectedItem = tabItem;
                }
                else
                {
                    BrowserTabs.Items.Add(tabItem);
                    if (selectTab) BrowserTabs.SelectedItem = tabItem;
                }
                
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

                webView.NavigationStarting += (s, e) =>
                {
                    if (_internalNewTabs.Contains(tabItem) && !string.IsNullOrWhiteSpace(e.Uri) && e.Uri != "about:blank")
                        _internalNewTabs.Remove(tabItem);
                    UpdateTabHeader(tabItem, GetIconForUrl(e.Uri), "در حال بارگیری...");
                    SetTabLoadingState(tabItem, true);
                    ResetTabNetworkStats(tabItem);
                };

                webView.NavigationCompleted += (s, e) =>
                {
                    if (!e.IsSuccess)
                    {
                        UpdateTabHeader(tabItem, "❌", "Failed to load");
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
                    UpdateTabHeader(tabItem, "＋", "New Tab");
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

        /// <summary>
        /// Save split view state to settings
        /// </summary>
        private void SaveSplitViewState()
        {
            if (_splitViewManager != null)
            {
                // Save complete split view state for restoration
                _currentSettings.SplitViewState = _splitViewManager.SerializeState();
                _currentSettings.EnableSplitView = _isSplitViewEnabled;
                _currentSettings.Save();
            }
        }

        /// <summary>
        /// Load and restore split view state from settings
        /// </summary>
        private void LoadSplitViewState()
        {
            if (string.IsNullOrEmpty(_currentSettings.SplitViewState) || !_currentSettings.EnableSplitView)
                return;

            try
            {
                // Deserialize the saved state
                if (_splitViewManager.DeserializeState(_currentSettings.SplitViewState))
                {
                    // State was restored, now enable split view
                    if (_splitViewManager.IsInSplitMode)
                    {
                        _isSplitViewEnabled = true;
                        
                        // Build UI from restored state
                        var rootState = _splitViewManager.GetRootState();
                        if (SplitViewContainer is SplitViewContainer container)
                        {
                            var rootGrid = BuildPanelGridUI(rootState);
                            if (rootGrid != null)
                            {
                                container.Children.Add(rootGrid);
                            }
                        }
                        
                        // Show split view UI
                        BrowserTabs.Visibility = Visibility.Collapsed;
                        SplitViewContainer.Visibility = Visibility.Visible;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading split view state: {ex.Message}");
            }
        }

        /// <summary>
        /// Get target panel for new tab (with user prompt if in split mode)
        /// </summary>
        private async Task<string> GetTargetPanelForNewTab()
        {
            if (!_isSplitViewEnabled)
                return _currentActivePanelId;

            // In split view mode, ask user which panel to open in
            var panels = _splitViewManager.GetAllLeafPanels();
            if (panels.Count <= 1)
                return _currentActivePanelId;

            var result = CustomMessageBox.Show(
                $"Open new tab in:\n• Current Panel (Enter)\n• Opposite Panel (O)",
                "Choose Panel",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (result == MessageBoxResult.Yes)
                return _currentActivePanelId;

            // Find other panel
            foreach (var panel in panels)
            {
                if (panel.GroupId != _currentActivePanelId)
                    return panel.GroupId;
            }

            return _currentActivePanelId;
        }

        /// <summary>
        /// Set active panel for keyboard shortcuts
        /// </summary>
        private void SetActivePanel(string panelId)
        {
            _currentActivePanelId = panelId;
            SaveSplitViewState();
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
            
            // Save split view state along with session
            SaveSplitViewState();
        }

        private class TabNetworkInfo
        {
            public long CachedBytes { get; set; }
            public long DownloadedBytes { get; set; }
            public long TotalBytes => CachedBytes + DownloadedBytes;
        }

        private WebView2? GetCurrentBrowser()
        {
            if (BrowserTabs.SelectedItem is TabItem tab && TryGetTabState(tab, out var state))
            {
                return state.ActiveWebView ?? state.PrimaryWebView;
            }
            return null;
        }

        public bool IsCurrentWebViewTarget()
        {
            if (!IsVisible || WindowState == WindowState.Minimized) return false;

            var helper = new WindowInteropHelper(this);
            if (helper.Handle == IntPtr.Zero || GetForegroundWindow() != helper.Handle) return false;

            var browser = GetCurrentBrowser();
            return browser != null && TxtUrl?.IsKeyboardFocusWithin != true;
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

        private TabItem? GetTabItemForCoreWebView2(CoreWebView2? core)
        {
            if (core == null) return null;
            return _coreWebViewTabMap.TryGetValue(core, out var tab) ? tab : null;
        }

        private void InitializeTabNetworkStats(TabItem tabItem)
        {
            _tabNetworkStats[tabItem] = new TabNetworkInfo();
        }

        private void ResetTabNetworkStats(TabItem tabItem)
        {
            if (_tabNetworkStats.TryGetValue(tabItem, out var stats))
            {
                stats.CachedBytes = 0;
                stats.DownloadedBytes = 0;
            }
            else
            {
                _tabNetworkStats[tabItem] = new TabNetworkInfo();
            }
            if (BrowserTabs.SelectedItem == tabItem) UpdateTabStatusOverlay(tabItem);
        }

        private void AddTabCachedBytes(TabItem tabItem, long bytes)
        {
            if (!_tabNetworkStats.TryGetValue(tabItem, out var stats))
            {
                stats = new TabNetworkInfo();
                _tabNetworkStats[tabItem] = stats;
            }
            stats.CachedBytes += bytes;
            if (BrowserTabs.SelectedItem == tabItem) UpdateTabStatusOverlay(tabItem);
        }

        private void AddTabDownloadedBytes(TabItem tabItem, long bytes)
        {
            if (bytes <= 0) return;
            if (!_tabNetworkStats.TryGetValue(tabItem, out var stats))
            {
                stats = new TabNetworkInfo();
                _tabNetworkStats[tabItem] = stats;
            }
            stats.DownloadedBytes += bytes;
            if (BrowserTabs.SelectedItem == tabItem) UpdateTabStatusOverlay(tabItem);
        }

        private void UpdateTabStatusOverlay(TabItem? tabItem = null, string? currentUrl = null)
        {
            tabItem ??= BrowserTabs.SelectedItem as TabItem;
            if (tabItem == null)
            {
                StatusOverlay.Visibility = Visibility.Collapsed;
                return;
            }

            if (!_tabNetworkStats.TryGetValue(tabItem, out var stats))
            {
                stats = new TabNetworkInfo();
                _tabNetworkStats[tabItem] = stats;
            }

            TxtStatusUrl.Text = currentUrl ?? "تب جاری";
            TxtCacheUsage.Text = $"کش: {FormatBytes(stats.CachedBytes)}";
            TxtDownloadUsage.Text = $"دانلود: {FormatBytes(stats.DownloadedBytes)}";
            TxtTotalUsage.Text = $"مجموع: {FormatBytes(stats.TotalBytes)}";

            if (StatusOverlay.Visibility != Visibility.Visible)
            {
                StatusOverlay.Visibility = Visibility.Visible;
                DoubleAnimation fadeIn = new DoubleAnimation(1, TimeSpan.FromSeconds(0.2));
                StatusOverlay.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            }
            if (_currentSettings.AutoHideStatus)
            {
                _statusFadeTimer?.Stop();
                _statusFadeTimer?.Start();
            }
            else
            {
                _statusFadeTimer?.Stop();
                StatusOverlay.Opacity = 1;
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

        private void CoreWebView2_WebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
        {
            var tabItem = GetTabItemForCoreWebView2(sender as CoreWebView2) ?? BrowserTabs.SelectedItem as TabItem;
            if (tabItem == null) return;

            string uri = e.Request.Uri;
            string lowerUri = uri.ToLowerInvariant();
            UpdateStatus($"در حال درخواست: {uri}", "در صف");

            // Allow Google APIs and essential scripts for Colab
            if (lowerUri.Contains("gstatic.com") || lowerUri.Contains("googleapis.com") || lowerUri.Contains("google.com/accounts"))
            {
                return; // Allow the request by not setting e.Response
            }

            if (_currentSettings == null) return;
            var ctx = e.ResourceContext;
            if (IsTrackerOrAd(lowerUri)) { if (sender is CoreWebView2 wv) e.Response = wv.Environment.CreateWebResourceResponse(null, 403, "Forbidden", ""); return; }
            if (!_currentSettings.LoadImages && IsImageContext(ctx, lowerUri)) { if (sender is CoreWebView2 wv) e.Response = wv.Environment.CreateWebResourceResponse(null, 403, "Forbidden", ""); return; }
            if (!_currentSettings.LoadMedia && IsMediaContext(ctx, lowerUri)) { if (sender is CoreWebView2 wv) e.Response = wv.Environment.CreateWebResourceResponse(null, 403, "Forbidden", ""); return; }
            if (IsHostNoCached(uri) || e.Request.Method != "GET") return;
            if (IsCacheableContext(ctx, lowerUri))
            {
                string? cachePath = GetCacheFilePath(uri);
                if (!string.IsNullOrEmpty(cachePath) && File.Exists(cachePath))
                {
                    try
                    {
                        var stream = File.OpenRead(cachePath);
                        string mime = GetMimeType(ctx, lowerUri);
                        string headers = $"Content-Type: {mime}\nCache-Control: public, max-age=31536000, immutable\nAccess-Control-Allow-Origin: *\nX-ImgSaver-Cache: HIT";
                        if (sender is CoreWebView2 wv) e.Response = wv.Environment.CreateWebResourceResponse(stream, 200, "OK", headers);
                        AddTabCachedBytes(tabItem, stream.Length);
                        UpdateStatus(uri, "Disk cache");
                        return;
                    }
                    catch { }
                }
            }
        }

        private async void CoreWebView2_WebResourceResponseReceived(object? sender, CoreWebView2WebResourceResponseReceivedEventArgs e)
        {
            var tabItem = GetTabItemForCoreWebView2(sender as CoreWebView2) ?? BrowserTabs.SelectedItem as TabItem;
            if (tabItem == null) return;

            string uri = e.Request.Uri;
            string lowerUri = uri.ToLowerInvariant();
            long size = 0;
            if (e.Response.Headers.Contains("Content-Length")) { long.TryParse(e.Response.Headers.GetHeader("Content-Length"), out size); }
            if (size > 0) AddTabDownloadedBytes(tabItem, size);
            UpdateStatus(uri, FormatBytes(size));
            if (e.Response.StatusCode != 200 || e.Request.Method != "GET") return;

            if (HasAttachmentDisposition(e.Response))
            {
                return; // Don't cache downloads
            }

            // Skip caching if host is in no-cache list
            if (IsHostNoCached(uri)) return;

            if (IsCacheableExtension(lowerUri) || IsImageResponse(e.Response))
                await SaveCacheResponseAsync(sender as CoreWebView2, uri, e.Response, size);
        }

        private bool HasAttachmentDisposition(CoreWebView2WebResourceResponseView response)
        {
            try
            {
                return response.Headers.Contains("Content-Disposition") &&
                    response.Headers.GetHeader("Content-Disposition")
                        .Contains("attachment", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private async Task SaveCacheResponseAsync(CoreWebView2? coreWebView2, string uri, CoreWebView2WebResourceResponseView response, long contentLength)
        {
            try
            {
                bool isImage = IsImageResponse(response);
                bool shouldImportImage = isImage && !IsComfyUiTransientImageUri(uri);
                if (ShouldSkipDiskCache(response, contentLength))
                {
                    if (shouldImportImage)
                        await SaveTemporaryImageImportAsync(coreWebView2, uri, response);
                    return;
                }

                string? cachePath = GetCacheFilePath(uri, response);
                if (string.IsNullOrWhiteSpace(cachePath)) return;
                if (File.Exists(cachePath))
                {
                    if (shouldImportImage)
                        await ImportImageToMiniClipAsync(coreWebView2, uri, cachePath);
                    return;
                }

                string? dir = Path.GetDirectoryName(cachePath);
                if (string.IsNullOrWhiteSpace(dir)) return;
                Directory.CreateDirectory(dir);

                string tempPath = cachePath + ".tmp";
                using (var content = await response.GetContentAsync())
                {
                    if (content == null) return;
                    using var output = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
                    await content.CopyToAsync(output);
                    if (output.Length == 0 || output.Length > MaxDiskCacheItemBytes)
                    {
                        output.Close();
                        try { File.Delete(tempPath); } catch { }
                        return;
                    }
                }

                if (!File.Exists(cachePath))
                    File.Move(tempPath, cachePath);
                else
                    File.Delete(tempPath);

                if (shouldImportImage)
                    await ImportImageToMiniClipAsync(coreWebView2, uri, cachePath);
            }
            catch { }
        }

        private bool IsComfyUiTransientImageUri(string uri)
        {
            try
            {
                var parsedUri = new Uri(uri);
                string path = parsedUri.AbsolutePath.TrimEnd('/').ToLowerInvariant();
                if (!path.EndsWith("/view") && !path.EndsWith("/api/view")) return false;

                var query = ParseQueryString(parsedUri.Query);
                query.TryGetValue("type", out string? type);
                query.TryGetValue("filename", out string? filename);
                type ??= "";
                filename ??= "";

                if (type.Equals("output", StringComparison.OrdinalIgnoreCase)) return false;
                if (type.Equals("temp", StringComparison.OrdinalIgnoreCase) ||
                    type.Equals("preview", StringComparison.OrdinalIgnoreCase))
                    return true;

                return filename.Contains("temp", StringComparison.OrdinalIgnoreCase) ||
                       filename.Contains("preview", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static Dictionary<string, string> ParseQueryString(string query)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(query)) return values;

            foreach (string part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] pair = part.Split('=', 2);
                string key = Uri.UnescapeDataString(pair[0].Replace("+", " "));
                if (string.IsNullOrWhiteSpace(key)) continue;

                string value = pair.Length > 1
                    ? Uri.UnescapeDataString(pair[1].Replace("+", " "))
                    : "";
                values[key] = value;
            }
            return values;
        }

        private async Task SaveTemporaryImageImportAsync(CoreWebView2? coreWebView2, string uri, CoreWebView2WebResourceResponseView response)
        {
            try
            {
                if (_miniClipImportedImageUris.Contains(uri)) return;

                Directory.CreateDirectory(_miniClipImportFolder);
                string tempPath = Path.Combine(_miniClipImportFolder, $"{CreateStableHash(uri)}{GetExtensionFromResponse(response)}");

                using (var content = await response.GetContentAsync())
                {
                    if (content == null) return;
                    using var output = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
                    await content.CopyToAsync(output);
                    if (output.Length == 0 || output.Length > MaxDiskCacheItemBytes)
                    {
                        output.Close();
                        try { File.Delete(tempPath); } catch { }
                        return;
                    }
                }

                await ImportImageToMiniClipAsync(coreWebView2, uri, tempPath);
            }
            catch { }
        }

        private static string CreateStableHash(string value)
        {
            using MD5 md5 = MD5.Create();
            byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(value));
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        private async Task ImportImageToMiniClipAsync(CoreWebView2? coreWebView2, string uri, string cachePath)
        {
            if (IsAiWorkflowImageContext(coreWebView2, uri))
                await TryImportDomConfirmedImageToMiniClipAsync(coreWebView2, uri, cachePath);
            else
                await TryImportCachedImageToMiniClipAsync(uri, cachePath);
        }

        private async Task TryImportDomConfirmedImageToMiniClipAsync(CoreWebView2? coreWebView2, string uri, string cachePath)
        {
            try
            {
                await Task.Delay(1000);
                if (!await IsImageLoadedInCurrentPageAsync(coreWebView2, uri)) return;
                await TryImportCachedImageToMiniClipAsync(uri, cachePath);
            }
            catch { }
        }

        private bool IsAiWorkflowImageContext(CoreWebView2? coreWebView2, string uri)
        {
            try
            {
                string pageUrl = coreWebView2?.Source ?? "";
                string title = coreWebView2?.DocumentTitle ?? "";
                string combined = $"{pageUrl}\n{title}\n{uri}".ToLowerInvariant();

                return combined.Contains("comfyui") ||
                       combined.Contains("comfy-ui") ||
                       combined.Contains("/workflow") && combined.Contains("seaart.") ||
                       combined.Contains("/api/view") ||
                       combined.Contains("/view?");
            }
            catch { return false; }
        }

        private async Task<bool> IsImageLoadedInCurrentPageAsync(CoreWebView2? coreWebView2, string uri)
        {
            if (coreWebView2 == null) return true;
            try
            {
                string target = System.Text.Json.JsonSerializer.Serialize(uri);
                string script = $@"
(() => {{
  const target = {target};
  const minRendered = 96;
  const normalize = (value) => {{
    try {{ return new URL(value || '', document.baseURI).href; }}
    catch {{ return value || ''; }}
  }};
  return Array.from(document.images).some(img => {{
    if (!img.complete || img.naturalWidth <= 0 || img.naturalHeight <= 0) return false;
    if (normalize(img.currentSrc || img.src) !== target) return false;

    const rect = img.getBoundingClientRect();
    const style = getComputedStyle(img);
    return rect.width >= minRendered &&
      rect.height >= minRendered &&
      rect.bottom > 0 &&
      rect.right > 0 &&
      rect.top < innerHeight &&
      rect.left < innerWidth &&
      style.visibility !== 'hidden' &&
      style.display !== 'none';
  }});
}})()";
                string result = await coreWebView2.ExecuteScriptAsync(script);
                return result.Equals("true", StringComparison.OrdinalIgnoreCase);
            }
            catch { return true; }
        }

        private async Task TryImportCachedImageToMiniClipAsync(string uri, string cachePath)
        {
            try
            {
                if (_miniClipImportedImageUris.Contains(uri)) return;
                if (!File.Exists(cachePath)) return;

                var settings = _currentSettings ?? BrowserSettings.Load();
                string? imageSignature = GetImageImportSignature(cachePath, settings.MinImageWidth, settings.MinImageHeight);
                if (string.IsNullOrEmpty(imageSignature)) return;
                if (_miniClipImportedImageSignatures.Contains(imageSignature)) return;

                _miniClipImportedImageUris.Add(uri);
                _miniClipImportedImageSignatures.Add(imageSignature);

                await Dispatcher.InvokeAsync(() =>
                {
                    var miniClip = GetOpenMiniClipboardWindow();
                    if (miniClip == null) return;

                    miniClip.ImportBrowserImage(cachePath, settings.MinImageWidth, settings.MinImageHeight);
                });
            }
            catch { }
        }

        private string? GetImageImportSignature(string path, int minWidth, int minHeight)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                var frame = decoder.Frames.FirstOrDefault();
                if (frame == null || frame.PixelWidth < minWidth || frame.PixelHeight < minHeight) return null;

                BitmapSource source = frame;
                if (source.Format != PixelFormats.Bgra32)
                {
                    var converted = new FormatConvertedBitmap();
                    converted.BeginInit();
                    converted.Source = source;
                    converted.DestinationFormat = PixelFormats.Bgra32;
                    converted.EndInit();
                    source = converted;
                }

                int stride = source.PixelWidth * 4;
                byte[] pixels = new byte[stride * source.PixelHeight];
                source.CopyPixels(pixels, stride, 0);

                using MD5 md5 = MD5.Create();
                byte[] hash = md5.ComputeHash(pixels);
                return $"{source.PixelWidth}x{source.PixelHeight}:{BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant()}";
            }
            catch { return null; }
        }

        private MiniClipboardWindow? GetOpenMiniClipboardWindow()
        {
            try
            {
                foreach (Window window in System.Windows.Application.Current.Windows)
                {
                    if (window is MiniClipboardWindow miniClip && miniClip.IsLoaded)
                        return miniClip;
                }
            }
            catch { }
            return null;
        }

        private bool IsImageResponse(CoreWebView2WebResourceResponseView response)
        {
            try
            {
                if (!response.Headers.Contains("Content-Type")) return false;
                return response.Headers.GetHeader("Content-Type")
                    .StartsWith("image/", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private bool ShouldSkipDiskCache(CoreWebView2WebResourceResponseView response, long contentLength)
        {
            try
            {
                if (contentLength > MaxDiskCacheItemBytes) return true;
                if (response.Headers.Contains("Cache-Control"))
                {
                    string cacheControl = response.Headers.GetHeader("Cache-Control").ToLowerInvariant();
                    if (cacheControl.Contains("no-store"))
                        return true;
                }
                if (response.Headers.Contains("Pragma") &&
                    response.Headers.GetHeader("Pragma").Contains("no-cache", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            catch { }
            return false;
        }

        private async Task AddToDownloadManagerAsync(string uri, string? fileName = null, Dictionary<string, string>? requestHeaders = null)
        {
            try
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    _downloadService.AddDownload(uri, fileName, requestHeaders);
                });
            }
            catch { }
        }

        private async Task<Dictionary<string, string>?> GetDownloadHeadersAsync(CoreWebView2? coreWebView2, string uri)
        {
            if (coreWebView2 == null) return null;
            try
            {
                var headers = new Dictionary<string, string>();
                var tabItem = GetTabItemForCoreWebView2(coreWebView2);
                if (tabItem != null && TryGetTabState(tabItem, out var state) && state.PrimaryWebView?.Source != null)
                {
                    headers["Referer"] = GetAsciiUriHeader(state.PrimaryWebView.Source);
                }

                var cookies = await coreWebView2.CookieManager.GetCookiesAsync(uri);
                if (cookies != null && cookies.Count > 0)
                {
                    string cookieHeader = string.Join("; ", cookies
                        .Where(c => IsAsciiHeaderName(c.Name))
                        .Select(c => $"{c.Name}={EscapeCookieValue(c.Value)}"));
                    if (!string.IsNullOrEmpty(cookieHeader))
                        headers["Cookie"] = cookieHeader;
                }

                return headers.Count > 0 ? headers : null;
            }
            catch { return null; }
        }

        private static string GetAsciiUriHeader(Uri uri)
        {
            try
            {
                return uri.GetComponents(UriComponents.AbsoluteUri, UriFormat.UriEscaped);
            }
            catch
            {
                return RemoveNonAscii(uri.AbsoluteUri);
            }
        }

        private static string EscapeCookieValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return IsAsciiHeaderValue(value)
                ? value
                : Uri.EscapeDataString(value);
        }

        private static bool IsAsciiHeaderName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            return value.All(c => c > 32 && c < 127 && "()<>@,;:\\\"/[]?={} \t".IndexOf(c) < 0);
        }

        private static bool IsAsciiHeaderValue(string value)
        {
            return value.All(c => c == '\t' || c == '\r' || c == '\n' || (c >= 32 && c < 127));
        }

        private static string RemoveNonAscii(string value)
        {
            return new string(value.Where(c => c == '\t' || (c >= 32 && c < 127)).ToArray());
        }

        private string GetDownloadFileName(string uri, CoreWebView2WebResourceResponseView? response = null)
        {
            try
            {
                if (response != null && response.Headers.Contains("Content-Disposition"))
                {
                    string header = response.Headers.GetHeader("Content-Disposition");
                    var match = Regex.Match(header, "filename\\*?=(?:UTF-8''?)?\\\"?([^\\\";]+)\\\"?", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        string filename = Uri.UnescapeDataString(match.Groups[1].Value.Trim('"'));
                        if (!string.IsNullOrWhiteSpace(filename)) return filename;
                    }
                }

                var parsedUri = new Uri(uri);
                string fileName = Path.GetFileName(parsedUri.LocalPath);
                if (!string.IsNullOrWhiteSpace(fileName)) return fileName;
            }
            catch { }
            return $"download_{Guid.NewGuid():N}.bin";
        }

        private async void CoreWebView2_DownloadStarting(object? sender, CoreWebView2DownloadStartingEventArgs e)
        {
            try
            {
                e.Handled = true;
                string uri = e.DownloadOperation.Uri;
                if (_handledDownloadUris.Contains(uri) || _downloadService.HasDownload(uri))
                    return;

                _handledDownloadUris.Add(uri);
                string fileName = !string.IsNullOrWhiteSpace(e.ResultFilePath)
                    ? Path.GetFileName(e.ResultFilePath)
                    : GetDownloadFileName(uri);
                var headers = await GetDownloadHeadersAsync(sender as CoreWebView2, uri);
                await AddToDownloadManagerAsync(uri, fileName, headers);
            }
            catch { }
        }

        private bool IsHostNoCached(string uri)
        {
            try
            {
                Uri parsedUri = new Uri(uri);
                string host = parsedUri.Host;
                if (_currentSettings?.NoCacheHosts != null && _currentSettings.NoCacheHosts.Count > 0)
                {
                    return _currentSettings.NoCacheHosts.Any(h =>
                        host.Equals(h, StringComparison.OrdinalIgnoreCase) ||
                        host.EndsWith("." + h, StringComparison.OrdinalIgnoreCase)
                    );
                }
                return false;
            }
            catch { return false; }
        }

        private void UpdateStatus(string url, string sizeInfo)
        {
            Dispatcher.Invoke(() => {
                TxtStatusUrl.Text = $"{url} — {sizeInfo}";
                UpdateTabStatusOverlay(BrowserTabs.SelectedItem as TabItem, TxtStatusUrl.Text);
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
        private bool IsCacheableContext(CoreWebView2WebResourceContext ctx, string uri) =>
            ctx == CoreWebView2WebResourceContext.Script ||
            ctx == CoreWebView2WebResourceContext.Stylesheet ||
            ctx == CoreWebView2WebResourceContext.Font ||
            ctx == CoreWebView2WebResourceContext.Image ||
            ctx == CoreWebView2WebResourceContext.Media ||
            ctx == CoreWebView2WebResourceContext.Fetch ||
            ctx == CoreWebView2WebResourceContext.XmlHttpRequest ||
            IsCacheableExtension(uri);

        private bool IsCacheableExtension(string uri) =>
            uri.Contains(".js") || uri.Contains(".css") ||
            uri.Contains(".woff") || uri.Contains(".woff2") ||
            uri.Contains(".ttf") || uri.Contains(".otf") ||
            uri.Contains(".wasm") || uri.Contains(".json") ||
            uri.Contains(".svg") || uri.Contains(".webp") ||
            uri.Contains(".png") || uri.Contains(".jpg") ||
            uri.Contains(".jpeg") || uri.Contains(".gif") ||
            uri.Contains(".avif") || uri.Contains(".ico") ||
            uri.Contains(".mp4") || uri.Contains(".webm") ||
            uri.Contains(".mp3") || uri.Contains(".m4a");
        private bool IsImageContext(CoreWebView2WebResourceContext ctx, string uri) => ctx == CoreWebView2WebResourceContext.Image || uri.EndsWith(".jpg") || uri.EndsWith(".png") || uri.EndsWith(".webp") || uri.EndsWith(".gif");
        private bool IsMediaContext(CoreWebView2WebResourceContext ctx, string uri) => ctx == CoreWebView2WebResourceContext.Media || uri.EndsWith(".mp4") || uri.EndsWith(".webm") || uri.EndsWith(".mp3");

        private string? GetCacheFilePath(string uri, CoreWebView2WebResourceResponseView? response = null)
        {
            try
            {
                Uri parsedUri = new Uri(uri);
                string host = SanitizeHostForCache(parsedUri.Host);
                string siteFolder = Path.Combine(_permanentCacheFolder, host);
                using (MD5 md5 = MD5.Create())
                {
                    byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(uri));
                    string filename = BitConverter.ToString(hash).Replace("-", "").ToLower();
                    if (uri.Contains(".webp")) filename += ".webp";
                    else if (uri.Contains(".png")) filename += ".png";
                    else if (uri.Contains(".jpg")) filename += ".jpg";
                    else if (uri.Contains(".jpeg")) filename += ".jpg";
                    else if (uri.Contains(".gif")) filename += ".gif";
                    else if (uri.Contains(".avif")) filename += ".avif";
                    else if (uri.Contains(".ico")) filename += ".ico";
                    else if (uri.Contains(".wasm")) filename += ".wasm";
                    else if (uri.Contains(".js")) filename += ".js";
                    else if (uri.Contains(".css")) filename += ".css";
                    else if (uri.Contains(".svg")) filename += ".svg";
                    else if (uri.Contains(".json")) filename += ".json";
                    else if (uri.Contains(".woff2")) filename += ".woff2";
                    else if (uri.Contains(".woff")) filename += ".woff";
                    else if (uri.Contains(".mp4")) filename += ".mp4";
                    else if (uri.Contains(".webm")) filename += ".webm";
                    else if (uri.Contains(".mp3")) filename += ".mp3";
                    else filename += GetExtensionFromResponse(response);
                    return Path.Combine(siteFolder, filename);
                }
            }
            catch { return null; }
        }

        private string GetExtensionFromResponse(CoreWebView2WebResourceResponseView? response)
        {
            try
            {
                if (response == null || !response.Headers.Contains("Content-Type")) return "";
                string contentType = response.Headers.GetHeader("Content-Type").Split(';')[0].Trim().ToLowerInvariant();
                return contentType switch
                {
                    "image/jpeg" => ".jpg",
                    "image/png" => ".png",
                    "image/webp" => ".webp",
                    "image/gif" => ".gif",
                    "image/bmp" => ".bmp",
                    "image/tiff" => ".tiff",
                    "image/avif" => ".avif",
                    "image/svg+xml" => ".svg",
                    _ => ""
                };
            }
            catch { return ""; }
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
            if (uri.Contains(".webp")) return "image/webp";
            if (uri.Contains(".png")) return "image/png";
            if (uri.Contains(".jpg") || uri.Contains(".jpeg")) return "image/jpeg";
            if (uri.Contains(".gif")) return "image/gif";
            if (uri.Contains(".avif")) return "image/avif";
            if (uri.Contains(".ico")) return "image/x-icon";
            if (uri.Contains(".mp4")) return "video/mp4";
            if (uri.Contains(".webm")) return "video/webm";
            if (uri.Contains(".mp3")) return "audio/mpeg";
            return "application/octet-stream";
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

        /// <summary>
        /// Split the selected tab vertically
        /// </summary>
        private void MenuItem_SplitVertical(object? sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.MenuItem item && item.Tag is TabItem sourceTab)
            {
                SplitTabWithNewTab(sourceTab, SplitOrientation.Vertical);
            }
        }

        /// <summary>
        /// Split the selected tab horizontally
        /// </summary>
        private void MenuItem_SplitHorizontal(object? sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.MenuItem item && item.Tag is TabItem sourceTab)
            {
                SplitTabWithNewTab(sourceTab, SplitOrientation.Horizontal);
            }
        }

        /// <summary>
        /// Create split view with the source tab in one panel and a new tab in the other
        /// </summary>
        private async void SplitTabWithNewTab(TabItem sourceTab, SplitOrientation orientation)
        {
            try
            {
                if (!_isSplitViewEnabled)
                {
                    // Create split WITHOUT transferring all tabs yet
                    _isSplitViewEnabled = true;
                    var newPanel = _splitViewManager.CreateSplit(_currentActivePanelId, orientation);
                    if (newPanel == null)
                    {
                        _isSplitViewEnabled = false;
                        return;
                    }

                    // Register new panel
                    _splitViewUIManager?.RegisterNewPanel(newPanel.GroupId);
                    
                    // Build UI for split
                    var rootState = _splitViewManager.GetRootState();
                    if (SplitViewContainer is SplitViewContainer container)
                    {
                        container.Children.Clear();
                        var rootGrid = BuildPanelGridUI(rootState);
                        if (rootGrid != null)
                        {
                            container.Children.Add(rootGrid);
                        }
                    }
                    
                    // Hide normal tabs, show split view
                    BrowserTabs.Visibility = Visibility.Collapsed;
                    SplitViewContainer.Visibility = Visibility.Visible;
                    
                    await System.Threading.Tasks.Task.Delay(100);
                }

                // Get the two leaf panels
                var leafPanels = _splitViewManager.GetAllLeafPanels();
                if (leafPanels.Count < 2)
                    return;

                var leftPanel = leafPanels[0];
                var rightPanel = leafPanels[1];

                if (!_panelTabControls.TryGetValue(leftPanel.GroupId, out var leftTabControl))
                    return;
                if (!_panelTabControls.TryGetValue(rightPanel.GroupId, out var rightTabControl))
                    return;

                // Transfer ONLY the source tab to the left panel
                if (sourceTab.Parent == BrowserTabs)
                {
                    BrowserTabs.Items.Remove(sourceTab);
                    leftTabControl.Items.Add(sourceTab);
                    leftTabControl.SelectedItem = sourceTab;
                }

                // Create new tab in right panel
                _currentActivePanelId = rightPanel.GroupId;
                await AddNewTab(selectTab: true, targetPanelId: rightPanel.GroupId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in SplitTabWithNewTab: {ex.Message}");
                CustomMessageBox.Show($"Error creating split: {ex.Message}", "Error");
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
                if (state.PrimaryWebView?.CoreWebView2 != null) _coreWebViewTabMap.Remove(state.PrimaryWebView.CoreWebView2);
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

        private void BtnDownloadManager_Click(object? sender, RoutedEventArgs e)
        {
            if (_downloadManagerWindow == null || !_downloadManagerWindow.IsLoaded)
            {
                _downloadManagerWindow = new DownloadManagerWindow(_downloadService);
                _downloadManagerWindow.Owner = this;
                _downloadManagerWindow.Show();
            }
            else
            {
                _downloadManagerWindow.Focus();
                _downloadManagerWindow.WindowState = WindowState.Normal;
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
                        DeleteDirectoryContents(_permanentCacheFolder);
                        CustomMessageBox.Show("All browsing data has been cleared.", "Success");
                        browser.Reload();
                    }
                }

                // Check if proxy settings changed BEFORE refreshing
                bool proxyChanged = false;
                var oldSettings = _currentSettings;
                var newSettings = BrowserSettings.Load();
                proxyChanged = oldSettings.ProxyEnabled != newSettings.ProxyEnabled ||
                              oldSettings.ProxyAddress != newSettings.ProxyAddress ||
                              oldSettings.ProxyPort != newSettings.ProxyPort ||
                              oldSettings.ProxyType != newSettings.ProxyType;

                RefreshSettings();

                if (proxyChanged)
                {
                    await ResetEnvironmentAndReloadTabs();
                    SyncDownloadProxySettings();
                    CustomMessageBox.Show("Proxy settings updated. Tabs and new downloads will use the new proxy configuration.", "Proxy Updated");
                }
                else
                {
                    // For other settings, just apply them to existing tabs
                    foreach (TabItem tab in BrowserTabs.Items)
                    {
                        if (TryGetTabState(tab, out var state) && state.PrimaryWebView != null) ApplyBrowserSettingsTo(state.PrimaryWebView);
                    }
                    CustomMessageBox.Show("Settings updated.", "Success");
                }
            }
        }

        private void BtnSplitView_Click(object? sender, RoutedEventArgs e)
        {
            // Show a context menu to select split orientation
            var menu = new ContextMenu();
            var verticalItem = new MenuItem { Header = "Split Vertical (V)" };
            verticalItem.Click += (s, a) => CreateSplitView(SplitOrientation.Vertical);
            var horizontalItem = new MenuItem { Header = "Split Horizontal (H)" };
            horizontalItem.Click += (s, a) => CreateSplitView(SplitOrientation.Horizontal);
            
            menu.Items.Add(verticalItem);
            menu.Items.Add(horizontalItem);
            
            if ((sender as WpfButton) is WpfButton btn)
            {
                menu.PlacementTarget = btn;
                menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                menu.IsOpen = true;
            }
        }

        private void CreateSplitView(SplitOrientation orientation)
        {
            if (_isSplitViewEnabled)
                return;

            try
            {
                _isSplitViewEnabled = true;
                
                // Create the split in the state manager
                var newPanel = _splitViewManager.CreateSplit(_currentActivePanelId, orientation);
                if (newPanel == null)
                {
                    _isSplitViewEnabled = false;
                    CustomMessageBox.Show("Cannot create split view at this time.", "Error");
                    return;
                }

                // Register the new panel with UI manager
                _splitViewUIManager?.RegisterNewPanel(newPanel.GroupId);
                
                // Get the current state
                var rootState = _splitViewManager.GetRootState();
                
                // Render the split view container with the updated state
                if (SplitViewContainer is SplitViewContainer container)
                {
                    // Rebuild the container UI based on the updated state
                    container.Children.Clear();
                    var rootGrid = BuildPanelGridUI(rootState);
                    if (rootGrid != null)
                    {
                        container.Children.Add(rootGrid);
                    }
                    
                    // Transfer tabs from current panel to left/top panel
                    if (rootState.LeftTopPanel != null)
                    {
                        TransferTabsToPanel(rootState.LeftTopPanel.GroupId);
                    }
                    
                    // Set the new panel as active
                    _currentActivePanelId = newPanel.GroupId;
                    _splitViewUIManager?.SetActivePanel(_currentActivePanelId);
                }
                
                // Hide normal tabs, show split view
                BrowserTabs.Visibility = Visibility.Collapsed;
                SplitViewContainer.Visibility = Visibility.Visible;
                
                CustomMessageBox.Show($"Split view enabled ({orientation.ToString()}). Tabs have been transferred to the left/top panel.", "Split View Created");
            }
            catch (Exception ex)
            {
                _isSplitViewEnabled = false;
                CustomMessageBox.Show($"Error creating split view: {ex.Message}", "Error");
            }
        }

        /// <summary>
        /// Build the UI grid structure for a split view state recursively
        /// </summary>
        private Grid? BuildPanelGridUI(SplitViewState state)
        {
            if (state == null)
                return null;

            var grid = new Grid
            {
                Background = (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["BackgroundBrush"]
            };

            if (state.IsLeafPanel)
            {
                // Leaf panel - create TabControl with tab items
                var tabControl = new WpfTabControl
                {
                    Background = System.Windows.Media.Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Name = $"Tab_{state.GroupId}"
                };
                
                // Apply tab styling
                ApplyTabControlStyle(tabControl);
                
                // Store reference for later access
                _panelTabControls[state.GroupId] = tabControl;
                _splitViewUIManager?.RegisterNewPanel(state.GroupId);
                
                grid.Children.Add(tabControl);
            }
            else
            {
                // Split panel - create child grids with splitter
                if (state.Orientation == SplitOrientation.Vertical)
                {
                    // Vertical split: Left and Right
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(state.SplitRatio, GridUnitType.Star) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1 - state.SplitRatio, GridUnitType.Star) });

                    var leftGrid = BuildPanelGridUI(state.LeftTopPanel);
                    if (leftGrid != null)
                    {
                        Grid.SetColumn(leftGrid, 0);
                        grid.Children.Add(leftGrid);
                    }

                    // Splitter
                    var splitter = new GridSplitter
                    {
                        Width = 4,
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                        VerticalAlignment = System.Windows.VerticalAlignment.Stretch,
                        Background = (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["BorderBrush"],
                        Cursor = System.Windows.Input.Cursors.SizeWE
                    };
                    Grid.SetColumn(splitter, 1);
                    grid.Children.Add(splitter);

                    var rightGrid = BuildPanelGridUI(state.RightBottomPanel);
                    if (rightGrid != null)
                    {
                        Grid.SetColumn(rightGrid, 2);
                        grid.Children.Add(rightGrid);
                    }
                }
                else // Horizontal
                {
                    // Horizontal split: Top and Bottom
                    grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(state.SplitRatio, GridUnitType.Star) });
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1 - state.SplitRatio, GridUnitType.Star) });

                    var topGrid = BuildPanelGridUI(state.LeftTopPanel);
                    if (topGrid != null)
                    {
                        Grid.SetRow(topGrid, 0);
                        grid.Children.Add(topGrid);
                    }

                    // Splitter
                    var splitter = new GridSplitter
                    {
                        Height = 4,
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                        VerticalAlignment = System.Windows.VerticalAlignment.Stretch,
                        Background = (System.Windows.Media.Brush)System.Windows.Application.Current.Resources["BorderBrush"],
                        Cursor = System.Windows.Input.Cursors.SizeNS
                    };
                    Grid.SetRow(splitter, 1);
                    grid.Children.Add(splitter);

                    var bottomGrid = BuildPanelGridUI(state.RightBottomPanel);
                    if (bottomGrid != null)
                    {
                        Grid.SetRow(bottomGrid, 2);
                        grid.Children.Add(bottomGrid);
                    }
                }
            }

            return grid;
        }

        /// <summary>
        /// Apply tab control styling to match the main browser tabs style
        /// </summary>
        private void ApplyTabControlStyle(WpfTabControl tabControl)
        {
            var resources = this.Resources;
            if (resources.Contains("TabItemStyle"))
            {
                tabControl.ItemContainerStyle = (Style)resources["TabItemStyle"];
            }
        }

        /// <summary>
        /// Transfer tabs from the current main tab control to a panel
        /// </summary>
        private void TransferTabsToPanel(string targetPanelId)
        {
            var targetTabControl = _panelTabControls.TryGetValue(targetPanelId, out var tc) ? tc : null;
            if (targetTabControl == null)
                return;

            // Copy all tabs from BrowserTabs to the target panel
            var itemsToTransfer = BrowserTabs.Items.Cast<WpfTabItem>().ToList();
            foreach (var tabItem in itemsToTransfer)
            {
                if (tabItem.DataContext is BrowserTabState tabState)
                {
                    var newTabItem = new WpfTabItem
                    {
                        Header = tabItem.Header,
                        Content = tabItem.Content,
                        DataContext = tabState,
                        AllowDrop = true
                    };
                    targetTabControl.Items.Add(newTabItem);
                }
            }

            // Select the first tab in the new panel
            if (targetTabControl.Items.Count > 0)
            {
                targetTabControl.SelectedIndex = 0;
            }
        }

        /// <summary>
        /// Handle panel closed event
        /// </summary>
        private void SplitViewContainer_OnPanelClosed(object? sender, string panelId)
        {
            if (_splitViewManager.ClosePanel(panelId))
            {
                _splitViewUIManager?.UnregisterPanel(panelId);
                _panelTabControls.Remove(panelId);

                // If only one panel left, exit split view mode
                if (_splitViewManager.GetPanelCount() <= 1)
                {
                    ExitSplitView();
                }
                else
                {
                    // Rebuild UI with updated state
                    var rootState = _splitViewManager.GetRootState();
                    if (SplitViewContainer is SplitViewContainer container)
                    {
                        container.Children.Clear();
                        var rootGrid = BuildPanelGridUI(rootState);
                        if (rootGrid != null)
                        {
                            container.Children.Add(rootGrid);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Handle panel activated event
        /// </summary>
        private void SplitViewContainer_OnPanelActivated(object? sender, string panelId)
        {
            _currentActivePanelId = panelId;
            _splitViewUIManager?.SetActivePanel(panelId);
        }

        /// <summary>
        /// Exit split view mode and return to normal tab mode
        /// </summary>
        private void ExitSplitView()
        {
            if (!_isSplitViewEnabled)
                return;

            try
            {
                _isSplitViewEnabled = false;
                
                // Reset split view manager to single panel
                _splitViewManager.ResetToSinglePanel();
                _splitViewUIManager?.ClearAll();
                _panelTabControls.Clear();
                _currentActivePanelId = _splitViewManager.GetRootState().GroupId;
                
                // Clear and hide split view container
                if (SplitViewContainer is SplitViewContainer container)
                {
                    container.Children.Clear();
                    container.Visibility = Visibility.Collapsed;
                }
                
                // Show normal tabs
                BrowserTabs.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Error exiting split view: {ex.Message}", "Error");
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
                    DeleteDiskCacheForHost(host);
                }
                catch { }
                try
                {
                    var cookieManager = browser.CoreWebView2.CookieManager;
                    var cookies = await cookieManager.GetCookiesAsync(browser.Source.ToString());
                    foreach (var cookie in cookies) { cookieManager.DeleteCookie(cookie); }
                }
                catch { }
                try
                {
                    await ClearCurrentSiteClientStorageAsync(browser);
                }
                catch { }
                CustomMessageBox.Show($"Data for {host} has been cleared.", "Success");
                browser.Reload();
            }
        }

        private void DeleteDiskCacheForHost(string host)
        {
            string sanitizedHost = SanitizeHostForCache(host);
            string targetDir = Path.Combine(_permanentCacheFolder, sanitizedHost);
            if (Directory.Exists(targetDir))
                Directory.Delete(targetDir, true);
        }

        private string SanitizeHostForCache(string host)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                host = host.Replace(c, '_');
            return host;
        }

        private void DeleteDirectoryContents(string folder)
        {
            try
            {
                if (!Directory.Exists(folder)) return;
                foreach (var file in Directory.GetFiles(folder))
                    File.Delete(file);
                foreach (var dir in Directory.GetDirectories(folder))
                    Directory.Delete(dir, true);
            }
            catch { }
        }

        private async Task ClearCurrentSiteClientStorageAsync(WebView2 browser)
        {
            if (browser.CoreWebView2 == null) return;
            string script = """
(async () => {
  try { localStorage.clear(); } catch {}
  try { sessionStorage.clear(); } catch {}
  try {
    if (window.caches && caches.keys) {
      const keys = await caches.keys();
      await Promise.all(keys.map(k => caches.delete(k)));
    }
  } catch {}
  try {
    if (indexedDB && indexedDB.databases) {
      const dbs = await indexedDB.databases();
      await Promise.all(dbs.filter(db => db && db.name).map(db => new Promise(resolve => {
        const req = indexedDB.deleteDatabase(db.name);
        req.onsuccess = req.onerror = req.onblocked = () => resolve();
      })));
    }
  } catch {}
})();
""";
            await browser.CoreWebView2.ExecuteScriptAsync(script);
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

        /// <summary>
        /// Handle window-level keyboard shortcuts
        /// </summary>
        private void Window_PreviewKeyDown(object? sender, System.Windows.Input.KeyEventArgs e)
        {
            // Only process shortcuts when not in edit mode
            if (Keyboard.FocusedElement is System.Windows.Controls.TextBox)
                return;

            // Check for Ctrl+Shift+V for Vertical Split
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control &&
                (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
            {
                if (e.Key == Key.V && !_isSplitViewEnabled)
                {
                    CreateSplitView(SplitOrientation.Vertical);
                    e.Handled = true;
                    return;
                }
                else if (e.Key == Key.H && !_isSplitViewEnabled)
                {
                    CreateSplitView(SplitOrientation.Horizontal);
                    e.Handled = true;
                    return;
                }
            }

            // Check for Escape to exit split view
            if (e.Key == Key.Escape && _isSplitViewEnabled)
            {
                ExitSplitView();
                e.Handled = true;
                return;
            }
        }

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

        public async Task PlayBrowserRecordingAsync()
        {
            RecordingManager.LoadState();
            int slotToPlay = RecordingManager.SelectedSlot == 2 ? 2 : 1;
            if (!RecordingManager.HasEvents(slotToPlay))
            {
                int other = slotToPlay == 1 ? 2 : 1;
                if (RecordingManager.HasEvents(other)) slotToPlay = other;
                else return;
            }

            _browserRecordingPlayer.Stop();
            _browserRecordingPlayer.SetEvents(RecordingManager.GetEvents(slotToPlay));
            _browserRecordingPlayer.SetSpeed(RecordingManager.PlaybackSpeed);
            _browserRecordingPlayer.SetRelativeTargetProvider(() =>
            {
                var helper = new WindowInteropHelper(this);
                return helper.Handle;
            });
            await _browserRecordingPlayer.PlayAsync(loop: false);
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
        public List<BrowserTabSession> TabSessions { get; set; } = new List<BrowserTabSession>();
        public int SelectedTabIndex { get; set; } = 0;
        public List<BookmarkItem> Bookmarks { get; set; } = new List<BookmarkItem>();

        public bool ProxyEnabled { get; set; } = false;
        public string ProxyType { get; set; } = "http";
        public string ProxyAddress { get; set; } = "";
        public string ProxyPort { get; set; } = "";

        // Minimum image dimensions for import to Mini Clipboard
        public int MinImageWidth { get; set; } = 50;
        public int MinImageHeight { get; set; } = 50;

        // List of hosts that should not use page cache (only cookies/login cache)
        public List<string> NoCacheHosts { get; set; } = new List<string>();

        // Split View Configuration
        public string SplitViewState { get; set; } = "";
        public bool EnableSplitView { get; set; } = true;

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

    public class BrowserTabSession
    {
        public string Url { get; set; } = "";
        public bool IsPinned { get; set; }
        public string SplitGroupId { get; set; } = "";
        public double SplitRatio { get; set; } = 0.5;
        public string SplitOrientation { get; set; } = "Vertical";
    }
}
