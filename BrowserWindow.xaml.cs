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
using WpfPanel = System.Windows.Controls.Panel;
using WpfPoint = System.Windows.Point;
using WpfTextBox = System.Windows.Controls.TextBox;
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfButton = System.Windows.Controls.Button;
using WpfColor = System.Windows.Media.Color;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;

namespace imgsaver
{
    public partial class BrowserWindow : Window
    {
        public BrowserProfile CurrentProfile { get; private set; }
        private readonly string _userDataFolder;
        private readonly string _permanentCacheFolder;
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
        private readonly InputRecorder _browserInputRecorder = new InputRecorder();
        private string _lastRequestUrl = "";

        // Environment for this profile window
        private CoreWebView2Environment? _environment;
        private readonly SemaphoreSlim _envLock = new SemaphoreSlim(1, 1);

        // Track previous proxy settings to detect changes
        private string _previousProxyAddress = "";
        private string _previousProxyPort = "";
        private string _previousProxyMode = "system";
        private string _previousProxyType = "http";

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        public static LocalProxyBridge ProxyBridge { get; } = new LocalProxyBridge();

        public BrowserWindow(BrowserProfile? profile = null)
        {
            CurrentProfile = profile ?? ProfileManager.GetActiveProfile();

            ProfileManager.SetActiveProfile(CurrentProfile);
            _userDataFolder = ProfileManager.GetUserDataFolder(CurrentProfile);
            _permanentCacheFolder = ProfileManager.GetCacheFolder(CurrentProfile);

            ProxyBridge.Start();
            InitializeComponent();

            InitializeStatusTimer();
            RefreshSettings();
            SaveCurrentProxySettings(); // Initialize proxy tracking
            RefreshBookmarksUI();
            UpdateProfileUIBadge();

            this.StateChanged += (s, e) =>
            {
                BrowserWindow_StateChanged(s, e);
                OnBrowserWindowStateChanged();
            };
            this.Activated += (s, e) =>
            {
                UpdateSplitViewPopupsVisibility(true);
                OnBrowserWindowVisibilityChanged(true);
            };
            this.Deactivated += (s, e) =>
            {
                UpdateSplitViewPopupsVisibility(false);
                OnBrowserWindowVisibilityChanged(false);
            };
            this.LocationChanged += (s, e) =>
            {
                RefreshSplitViewPopupsPosition();
                RepositionBaseCombinerPopup();
            };
            this.SizeChanged += (s, e) =>
            {
                RefreshSplitViewPopupsPosition();
                RepositionBaseCombinerPopup();
            };
            this.Closing += (s, e) =>
            {
                if (PopInlineBaseCombiner != null) PopInlineBaseCombiner.IsOpen = false;
            };
            _browserInputRecorder.OnStopRequested += StopBrowserRecordingAndSave;

            InitializeTabs();
            this.PreviewKeyDown += BrowserWindow_PreviewKeyDown;
            this.Loaded += (s, e) =>
            {
                WindowResizingHelper.HookWindow(this);
                DwmHelper.UseImmersiveDarkMode(this);
                UpdateProfileUIBadge();
                InitializeCombiner();
                var handle = new WindowInteropHelper(this).Handle;
                var hwndSource = HwndSource.FromHwnd(handle);
                hwndSource?.AddHook(WndProc);
            };
        }

        private void UpdateProfileUIBadge()
        {
            try
            {
                if (CurrentProfile != null && BtnAccountProfile != null)
                {
                    if (UserProfileAvatarPath != null)
                    {
                        UserProfileAvatarPath.Data = ProfileVectorHelper.GetGeometry(CurrentProfile.Icon);
                    }
                    if (TxtProfileName != null)
                    {
                        TxtProfileName.Text = string.IsNullOrWhiteSpace(CurrentProfile.Name) ? "Account" : CurrentProfile.Name;
                    }

                    if (UserProfileAvatarBorder != null && !string.IsNullOrEmpty(CurrentProfile.ColorHex))
                    {
                        try
                        {
                            var color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(CurrentProfile.ColorHex);
                            UserProfileAvatarBorder.Background = new System.Windows.Media.SolidColorBrush(color);
                        }
                        catch { }
                    }

                    BtnAccountProfile.ToolTip = $"Active Account Profile: {CurrentProfile.Name}\nClick to switch or launch another account profile.";
                }
            }
            catch { }
        }

        private void BtnAccountProfile_Click(object sender, RoutedEventArgs e)
        {
            var selector = new ProfileSelectionWindow();
            if (selector.ShowDialog() == true && selector.SelectedProfile != null)
            {
                if (selector.SelectedProfile.Id == CurrentProfile.Id)
                {
                    // Update in-place if the current profile was edited
                    CurrentProfile = selector.SelectedProfile;
                    UpdateProfileUIBadge();
                    return;
                }

                var newBrowser = new BrowserWindow(selector.SelectedProfile);
                newBrowser.Show();
            }
        }

        private void BrowserWindow_StateChanged(object? sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Minimized)
            {
                UpdateSplitViewPopupsVisibility(false);
            }
            else
            {
                if (this.IsActive) UpdateSplitViewPopupsVisibility(true);
                RefreshSplitViewPopupsPosition();
            }

            if (this.WindowState == WindowState.Maximized) 
            {
                this.MaxHeight = double.PositiveInfinity;
                this.MaxWidth = double.PositiveInfinity;
                var resizeThickness = SystemParameters.WindowResizeBorderThickness;
                MainBorder.Margin = new Thickness(resizeThickness.Left, resizeThickness.Top, resizeThickness.Right, resizeThickness.Bottom);
                MainBorder.CornerRadius = new CornerRadius(0);
                MainBorder.BorderThickness = new Thickness(0);
                if (TitleBarBorder != null) TitleBarBorder.CornerRadius = new CornerRadius(0);
                if (MaximizeIconPath != null) MaximizeIconPath.Data = (Geometry)FindResource("IconRestore");
                if (BtnMaximize != null) BtnMaximize.ToolTip = "بازگردانی";
            }
            else 
            {
                this.MaxHeight = double.PositiveInfinity;
                this.MaxWidth = double.PositiveInfinity;
                MainBorder.Margin = new Thickness(0);
                MainBorder.CornerRadius = new CornerRadius(8);
                MainBorder.BorderThickness = new Thickness(1);
                if (TitleBarBorder != null) TitleBarBorder.CornerRadius = new CornerRadius(8, 8, 0, 0);
                if (MaximizeIconPath != null) MaximizeIconPath.Data = (Geometry)FindResource("IconMaximize");
                if (BtnMaximize != null) BtnMaximize.ToolTip = "بزرگ کردن";
            }
        }

        private void InitializeStatusTimer()
        {
            _statusFadeTimer = new DispatcherTimer();
            _statusFadeTimer.Interval = TimeSpan.FromSeconds(2);
            _statusFadeTimer.Tick += (s, e) => HideStatus();
        }

        private void RefreshSettings()
        {
            _currentSettings = BrowserSettings.Load(CurrentProfile);
            SyncDownloadProxySettings();
            if (CombinerBar != null)
            {
                bool showCombiner = _currentSettings.EnableCombinerBar;
                CombinerBar.Visibility = showCombiner ? Visibility.Visible : Visibility.Collapsed;
                if (showCombiner)
                {
                    InitializeCombiner();
                }
            }
            if (_currentSettings.AutoHideStatus)
            {
                StatusOverlay.Visibility = Visibility.Collapsed;
                StatusOverlay.Opacity = 0;
                _statusFadeTimer?.Stop();
            }
            else
            {
                StatusOverlay.Visibility = Visibility.Visible;
                StatusOverlay.Opacity = 1;
                _statusFadeTimer?.Stop();
            }

            // Re-apply settings and reinject helper scripts to all active webviews
            try
            {
                if (BrowserTabs != null)
                {
                    foreach (TabItem tab in BrowserTabs.Items)
                    {
                        if (TryGetTabState(tab, out var state) && state.PrimaryWebView != null)
                        {
                            ApplyBrowserSettingsTo(state.PrimaryWebView);
                        }
                    }
                }
            }
            catch { }
        }

        private bool ProxySettingsChanged()
        {
            return (_currentSettings.ProxyMode ?? "system") != _previousProxyMode ||
                   _currentSettings.ProxyAddress != _previousProxyAddress ||
                   _currentSettings.ProxyPort != _previousProxyPort ||
                   _currentSettings.ProxyType != _previousProxyType;
        }

        private void SaveCurrentProxySettings()
        {
            _previousProxyMode = _currentSettings.ProxyMode ?? "system";
            _previousProxyAddress = _currentSettings.ProxyAddress;
            _previousProxyPort = _currentSettings.ProxyPort;
            _previousProxyType = _currentSettings.ProxyType ?? "http";
        }

        private void TitleBar_MouseLeftButtonDown(object? sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) BtnMaximize_Click(sender, e);
            else if (e.LeftButton == MouseButtonState.Pressed) this.DragMove();
        }

        private void BtnOpenMainMiniClip_Click(object? sender, RoutedEventArgs e)
        {
            try
            {
                MiniClipboardWindow? miniClip = null;
                foreach (Window w in System.Windows.Application.Current.Windows)
                {
                    if (w is MiniClipboardWindow mc && mc.IsLoaded)
                    {
                        miniClip = mc;
                        break;
                    }
                }

                if (miniClip == null)
                {
                    miniClip = new MiniClipboardWindow();
                    miniClip.Show();
                }
                else
                {
                    if (miniClip.WindowState == WindowState.Minimized)
                    {
                        miniClip.WindowState = WindowState.Normal;
                    }
                    miniClip.Show();
                    miniClip.Activate();
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show("Error opening Mini Clip: " + ex.Message, "Error");
            }
        }

        private void BtnClose_Click(object? sender, RoutedEventArgs e) => this.Close();
        protected override void OnClosed(EventArgs e)
        {
            _browserInputRecorder.OnStopRequested -= StopBrowserRecordingAndSave;
            _browserInputRecorder.Dispose();
            _browserRecordingPlayer.Stop();
            base.OnClosed(e);
        }

        private void BtnMinimize_Click(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void BtnMaximize_Click(object? sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

        private void BtnSplitView_Click(object? sender, RoutedEventArgs e)
        {
            ToggleSplitView();
        }

        private async void BrowserWindow_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Ctrl+L for Quick App Security Lock
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control && e.Key == Key.L)
            {
                if (SecurityManager.IsPasswordConfigured())
                {
                    e.Handled = true;
                    AppLockManager.LockApp();
                    return;
                }
            }

            // Ctrl+F5, Shift+F5, Ctrl+Shift+R or Ctrl+R for Hard Reload (Force Refresh & Clear Storage Cache)
            if ((e.Key == Key.F5 && (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) != 0) ||
                (e.Key == Key.R && (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) == (ModifierKeys.Control | ModifierKeys.Shift)) ||
                (e.Key == Key.R && (Keyboard.Modifiers & ModifierKeys.Control) != 0 && (Keyboard.Modifiers & ModifierKeys.Alt) != 0))
            {
                e.Handled = true;
                HardReloadCurrentTab();
            }
            else if (e.Key == Key.F5)
            {
                e.Handled = true;
                GetCurrentBrowser()?.Reload();
            }
            else if (e.Key == System.Windows.Input.Key.S && 
                (System.Windows.Input.Keyboard.Modifiers & (System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Shift)) == (System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Shift))
            {
                e.Handled = true;
                ToggleSplitView();
            }
            else if (e.Key == System.Windows.Input.Key.D && 
                (System.Windows.Input.Keyboard.Modifiers & (System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Alt)) == (System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Alt))
            {
                e.Handled = true;
                await DumpCurrentPageSourceAsync();
            }
        }

        private async Task DumpCurrentPageSourceAsync()
        {
            try
            {
                var browser = GetCurrentBrowser();
                if (browser == null || browser.CoreWebView2 == null) return;

                string url = browser.Source?.ToString() ?? "unknown";
                string html = await browser.CoreWebView2.ExecuteScriptAsync("document.documentElement.outerHTML");

                try
                {
                    html = Newtonsoft.Json.JsonConvert.DeserializeObject<string>(html) ?? html;
                }
                catch { }

                var uriObj = new Uri(url);

                // 1) Download and inline Stylesheets and Scripts
                using (var client = new System.Net.Http.HttpClient())
                {
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                    
                    var cssMatches = Regex.Matches(html, @"<link[^>]+href=[""']([^""']+)[""']", RegexOptions.IgnoreCase);
                    foreach (Match m in cssMatches)
                    {
                        string href = m.Groups[1].Value;
                        if (href.Contains(".css") || m.Value.Contains("stylesheet"))
                        {
                            string absUrl = href.StartsWith("http") ? href : new Uri(uriObj, href).AbsoluteUri;
                            try
                            {
                                string cssContent = await client.GetStringAsync(absUrl);
                                string styleTag = $"<style id=\"inlined_{Guid.NewGuid().ToString("N")}\">/* Inlined from {absUrl} */\n{cssContent}\n</style>";
                                html = html.Replace(m.Value, styleTag);
                            }
                            catch { }
                        }
                    }

                    // 2) Download and inline Scripts
                    var jsMatches = Regex.Matches(html, @"<script[^>]+src=[""']([^""']+)[""']", RegexOptions.IgnoreCase);
                    foreach (Match m in jsMatches)
                    {
                        string src = m.Groups[1].Value;
                        string absUrl = src.StartsWith("http") ? src : new Uri(uriObj, src).AbsoluteUri;
                        try
                        {
                            string jsContent = await client.GetStringAsync(absUrl);
                            if (jsContent.Length < 10 * 1024 * 1024)
                            {
                                string scriptTag = $"<script id=\"inlined_{Guid.NewGuid().ToString("N")}\">/* Inlined from {absUrl} */\n{jsContent}\n</script>";
                                html = html.Replace(m.Value, scriptTag);
                            }
                        }
                        catch { }
                    }
                }

                string dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "dom_dumps");
                Directory.CreateDirectory(dataDir);

                string host = uriObj.Host;
                string filename = $"{host}_FULL_{DateTime.Now:yyyyMMdd_HHmmss}.html";
                string filepath = Path.Combine(dataDir, filename);

                File.WriteAllText(filepath, html, Encoding.UTF8);

                UpdateStatus($"Full page code dumped to: {filename}", "Success");
                System.Windows.MessageBox.Show($"Current page DOM and all linked JS/CSS scripts have been fully inlined and saved successfully to:\n\n{filepath}", "Full DOM Dump Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                UpdateStatus($"Full dump failed: {ex.Message}", "Error");
                System.Windows.MessageBox.Show($"Failed to dump full page DOM: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private const int WM_GETMINMAXINFO = 0x0024;
        private const uint MONITOR_DEFAULTTONEAREST = 0x00000002;

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr handle, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_GETMINMAXINFO)
            {
                WmGetMinMaxInfo(hwnd, lParam);
                handled = true;
            }
            return IntPtr.Zero;
        }

        private static void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
        {
            try
            {
                MINMAXINFO mmi = (MINMAXINFO)Marshal.PtrToStructure(lParam, typeof(MINMAXINFO))!;
                IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
                if (monitor != IntPtr.Zero)
                {
                    MONITORINFO monitorInfo = new MONITORINFO();
                    monitorInfo.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
                    if (GetMonitorInfo(monitor, ref monitorInfo))
                    {
                        RECT rcWorkArea = monitorInfo.rcWork;
                        RECT rcMonitorArea = monitorInfo.rcMonitor;
                        mmi.ptMaxPosition.x = Math.Abs(rcWorkArea.left - rcMonitorArea.left);
                        mmi.ptMaxPosition.y = Math.Abs(rcWorkArea.top - rcMonitorArea.top);
                        mmi.ptMaxSize.x = Math.Abs(rcWorkArea.right - rcWorkArea.left);
                        mmi.ptMaxSize.y = Math.Abs(rcWorkArea.bottom - rcWorkArea.top);
                    }
                }
                Marshal.StructureToPtr(mmi, lParam, true);
            }
            catch { }
        }

        private void BtnAppLock_Click(object sender, RoutedEventArgs e)
        {
            if (SecurityManager.IsPasswordConfigured())
            {
                AppLockManager.LockApp();
            }
            else
            {
                CustomMessageBox.Show("رمز عبور اصلی هنوز تعریف نشده است. لطفاً از بخش تنظیمات برنامه ابتدا رمز عبور تعیین کنید.", "امنیت برنامه", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
