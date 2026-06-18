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
        private readonly InputRecorder _browserInputRecorder = new InputRecorder();

        // Download Manager
        private DownloadManagerService _downloadService = null!;
        private DownloadManagerWindow? _downloadManagerWindow;

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
            RefreshSettings();
            SaveCurrentProxySettings(); // Initialize proxy tracking
            RefreshBookmarksUI();

            this.StateChanged += BrowserWindow_StateChanged;
            _browserInputRecorder.OnStopRequested += StopBrowserRecordingAndSave;

            InitializeTabs();
        }

        private void InitializeDownloadService()
        {
            _downloadService = new DownloadManagerService();
            SyncDownloadProxySettings();
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

        private void TitleBar_MouseLeftButtonDown(object? sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) BtnMaximize_Click(sender, e);
            else if (e.LeftButton == MouseButtonState.Pressed) this.DragMove();
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

    }
}
