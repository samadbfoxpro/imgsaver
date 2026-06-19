using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.Windows.Threading;
using System.Threading.Tasks;

namespace imgsaver
{
    public partial class MiniClipboardWindow : Window
    {
        private class CapturedImageInfo
        {
            public BitmapSource Bitmap { get; set; }
            public string OriginalPath { get; set; }
        }

        private InputPlayer _playerRec = new InputPlayer();
        private HwndSource _hwndSource;
        private IntPtr _nextClipboardViewer;
        private GlobalHook _globalHook;
        private FileSystemWatcher _autoImportWatcher;

        private bool _hasImage = false;
        private bool _hasPositivePrompt = false;
        private bool _hasNegativePrompt = false;
        private bool _ignoreNextClipboardChange = true;
        private DateTime _lastClipboardTime = DateTime.MinValue;
        private bool _isSaving = false;
        private int _nextMiniSlot = 1;
        private const string MiniExtraPlaceholderTag = "[extra]";
        private string _miniExtraTemplate = "";

        // Auto-Save Settings
        private bool _autoSaveEnabled = false;
        private int _autoSaveThreshold = 1;
        private bool _autoCaptureExtraTemplate = false;

        private DispatcherTimer _netTimer;
        private long _lastBytesReceived = 0;
        private long _lastBytesSent = 0;
        private bool _isBrowserQuickPasteEnabled = false;
        private bool _isBrowserQuickPasteRunning = false;

        public static readonly DependencyProperty IsDisabledProperty =
            DependencyProperty.Register("IsDisabled", typeof(bool), typeof(MiniClipboardWindow), new PropertyMetadata(false));

        public bool IsDisabled
        {
            get { return (bool)GetValue(IsDisabledProperty); }
            set { SetValue(IsDisabledProperty, value); }
        }

        public static readonly DependencyProperty IsCompactModeProperty =
            DependencyProperty.Register("IsCompactMode", typeof(bool), typeof(MiniClipboardWindow), new PropertyMetadata(false));

        public bool IsCompactMode
        {
            get { return (bool)GetValue(IsCompactModeProperty); }
            set { SetValue(IsCompactModeProperty, value); }
        }

        public static readonly DependencyProperty IsNegativeLockedProperty =
            DependencyProperty.Register("IsNegativeLocked", typeof(bool), typeof(MiniClipboardWindow), new PropertyMetadata(false));

        public bool IsNegativeLocked
        {
            get { return (bool)GetValue(IsNegativeLockedProperty); }
            set { SetValue(IsNegativeLockedProperty, value); }
        }

        public static readonly DependencyProperty IsAdditionalTitleLockedProperty =
            DependencyProperty.Register("IsAdditionalTitleLocked", typeof(bool), typeof(MiniClipboardWindow), new PropertyMetadata(false));

        public bool IsAdditionalTitleLocked
        {
            get { return (bool)GetValue(IsAdditionalTitleLockedProperty); }
            set { SetValue(IsAdditionalTitleLockedProperty, value); }
        }

        public static readonly DependencyProperty IsTitleLockedProperty =
            DependencyProperty.Register("IsTitleLocked", typeof(bool), typeof(MiniClipboardWindow), new PropertyMetadata(false));

        public bool IsTitleLocked
        {
            get { return (bool)GetValue(IsTitleLockedProperty); }
            set { SetValue(IsTitleLockedProperty, value); }
        }

        public static readonly DependencyProperty IsExtraMenuOpenProperty =
            DependencyProperty.Register("IsExtraMenuOpen", typeof(bool), typeof(MiniClipboardWindow), new PropertyMetadata(false));

        public bool IsExtraMenuOpen
        {
            get { return (bool)GetValue(IsExtraMenuOpenProperty); }
            set { SetValue(IsExtraMenuOpenProperty, value); }
        }

        public static readonly DependencyProperty ExtraMenuPageProperty =
            DependencyProperty.Register("ExtraMenuPage", typeof(int), typeof(MiniClipboardWindow), new PropertyMetadata(0));

        public int ExtraMenuPage
        {
            get { return (int)GetValue(ExtraMenuPageProperty); }
            set { SetValue(ExtraMenuPageProperty, value); }
        }

        public static readonly DependencyProperty IsAdditionalTitleEnabledProperty =
            DependencyProperty.Register("IsAdditionalTitleEnabled", typeof(bool), typeof(MiniClipboardWindow), new PropertyMetadata(false, OnIsAdditionalTitleEnabledChanged));

        private static void OnIsAdditionalTitleEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MiniClipboardWindow window && (bool)e.NewValue == false)
            {
                window.AdditionalTitle = "";
            }
        }

        public bool IsAdditionalTitleEnabled
        {
            get { return (bool)GetValue(IsAdditionalTitleEnabledProperty); }
            set { SetValue(IsAdditionalTitleEnabledProperty, value); }
        }

        public static readonly DependencyProperty AdditionalTitleProperty =
            DependencyProperty.Register("AdditionalTitle", typeof(string), typeof(MiniClipboardWindow), new PropertyMetadata(""));

        public string AdditionalTitle
        {
            get { return (string)GetValue(AdditionalTitleProperty); }
            set { SetValue(AdditionalTitleProperty, value); }
        }

        public static readonly DependencyProperty IsAutoFillEnabledProperty =
            DependencyProperty.Register("IsAutoFillEnabled", typeof(bool), typeof(MiniClipboardWindow), new PropertyMetadata(false));

        public bool IsAutoFillEnabled
        {
            get { return (bool)GetValue(IsAutoFillEnabledProperty); }
            set { SetValue(IsAutoFillEnabledProperty, value); }
        }

        public static readonly DependencyProperty IsSaveBasePromptEnabledProperty =
            DependencyProperty.Register("IsSaveBasePromptEnabled", typeof(bool), typeof(MiniClipboardWindow), new PropertyMetadata(false));

        public bool IsSaveBasePromptEnabled
        {
            get { return (bool)GetValue(IsSaveBasePromptEnabledProperty); }
            set { SetValue(IsSaveBasePromptEnabledProperty, value); }
        }

        private List<CapturedImageInfo> _capturedImages = new List<CapturedImageInfo>();
        private string _positivePrompt = "";
        private string _negativePrompt = "";

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetClipboardViewer(IntPtr hWndNewViewer);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ChangeClipboardChain(IntPtr hWndRemove, IntPtr hWndNewNext);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private const int WM_DRAWCLIPBOARD = 0x0308;
        private const int WM_CHANGECBCHAIN = 0x030D;

        public MiniClipboardWindow()
        {
            InitializeComponent();
            Loaded += MiniClipboardWindow_Loaded;
            Closed += MiniClipboardWindow_Closed;
        }

        private void MiniClipboardWindow_Loaded(object sender, RoutedEventArgs e)
        {
            SetIconsAtRuntime();
            var helper = new WindowInteropHelper(this);
            _hwndSource = HwndSource.FromHwnd(helper.Handle);
            _hwndSource?.AddHook(WndProc);
            _ignoreNextClipboardChange = true;
            _nextClipboardViewer = SetClipboardViewer(helper.Handle);

            try
            {
                _globalHook = new GlobalHook();
                _globalHook.OnKeyPressed += GlobalHook_OnKeyPressed;
            }
            catch { }

            StartNetMonitoring();
            RefreshAutoImport();
        }

        public void RefreshAutoImport()
        {
            try
            {
                if (_autoImportWatcher != null)
                {
                    _autoImportWatcher.EnableRaisingEvents = false;
                    _autoImportWatcher.Dispose();
                    _autoImportWatcher = null;
                }

                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data\\config.txt");
                if (!File.Exists(configPath)) return;

                string[] lines = File.ReadAllLines(configPath);

                // Load Auto-Save Settings
                if (lines.Length > 4) _autoSaveEnabled = lines[4].Trim().ToLower() == "true";
                if (lines.Length > 5 && int.TryParse(lines[5].Trim(), out int threshold)) _autoSaveThreshold = threshold;
                _autoCaptureExtraTemplate = lines.Length > 6 && lines[6].Trim().ToLower() == "true";

                if (lines.Length < 4) return;

                bool enabled = lines[2].Trim().ToLower() == "true";
                string watchPath = lines[3].Trim();

                if (enabled && Directory.Exists(watchPath))
                {
                    _autoImportWatcher = new FileSystemWatcher(watchPath);
                    _autoImportWatcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime;
                    _autoImportWatcher.Created += (s, e) => ProcessImportedFile(e.FullPath);
                    _autoImportWatcher.Renamed += (s, e) => ProcessImportedFile(e.FullPath);
                    _autoImportWatcher.EnableRaisingEvents = true;
                }
            }
            catch { }
        }

        private async void ProcessImportedFile(string filePath)
        {
            try
            {
                bool isDisabled = await Dispatcher.InvokeAsync(() => IsDisabled);
                if (isDisabled) return;

                string ext = Path.GetExtension(filePath).ToLower();
                string[] allowed = { ".jpg", ".jpeg", ".png", ".bmp", ".webp" };
                if (!Array.Exists(allowed, x => x == ext)) return;

                for (int i = 0; i < 10; i++)
                {
                    try
                    {
                        await Task.Delay(500);
                        if (!File.Exists(filePath)) return;

                        BitmapImage bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(filePath);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        bitmap.Freeze();

                        await Dispatcher.InvokeAsync(() => {
                            _capturedImages.Add(new CapturedImageInfo { Bitmap = bitmap, OriginalPath = filePath });
                            _hasImage = true;
                            UpdateImagePreviews();
                            UpdateState();
                            CheckAutoSaveTrigger();
                        });
                        return;
                    }
                    catch
                    {
                        await Task.Delay(500);
                    }
                }
            }
            catch { }
        }

        public void ImportBrowserImage(string filePath, int minWidth, int minHeight)
        {
            try
            {
                if (IsDisabled || string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;

                string ext = Path.GetExtension(filePath).ToLowerInvariant();
                string[] allowed = { ".jpg", ".jpeg", ".png", ".bmp", ".webp", ".gif", ".tiff", ".avif" };
                if (!Array.Exists(allowed, x => x == ext)) return;

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(filePath);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();

                if (bitmap.PixelWidth < minWidth || bitmap.PixelHeight < minHeight) return;

                _capturedImages.Add(new CapturedImageInfo { Bitmap = bitmap, OriginalPath = filePath });
                _hasImage = true;
                UpdateImagePreviews();
                UpdateState();
                CheckAutoSaveTrigger();
            }
            catch { }
        }

        private void StartNetMonitoring()
        {
            _netTimer = new DispatcherTimer();
            _netTimer.Interval = TimeSpan.FromSeconds(1);
            _netTimer.Tick += NetTimer_Tick;
            _netTimer.Start();
        }

        private string FormatSpeed(long bytes)
        {
            double kb = bytes / 1024.0;
            if (kb < 1024) return $"{kb:F1} KB";
            else return $"{(kb / 1024.0):F1} MB";
        }

        private void NetTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                long currentReceived = 0;
                long currentSent = 0;
                var interfaces = NetworkInterface.GetAllNetworkInterfaces();
                foreach (var ni in interfaces)
                {
                    if (ni.OperationalStatus == OperationalStatus.Up &&
                        (ni.NetworkInterfaceType == NetworkInterfaceType.Ethernet || ni.NetworkInterfaceType == NetworkInterfaceType.Wireless80211))
                    {
                        var stats = ni.GetIPStatistics();
                        currentReceived += stats.BytesReceived;
                        currentSent += stats.BytesSent;
                    }
                }

                if (_lastBytesReceived > 0)
                {
                    long diffReceived = currentReceived - _lastBytesReceived;
                    long diffSent = currentSent - _lastBytesSent;
                    TxtDownloadSpeed.Text = FormatSpeed(diffReceived);
                    TxtUploadSpeed.Text = FormatSpeed(diffSent);
                }

                _lastBytesReceived = currentReceived;
                _lastBytesSent = currentSent;
            }
            catch { }
        }

        private void GlobalHook_OnKeyPressed(System.Windows.Forms.Keys key)
        {
            if (key == System.Windows.Forms.Keys.ControlKey || key == System.Windows.Forms.Keys.LControlKey || key == System.Windows.Forms.Keys.RControlKey)
            {
                Dispatcher.Invoke(() => TriggerBrowserQuickPasteIfReady());
                return;
            }

            bool ctrlPressed = (System.Windows.Forms.Control.ModifierKeys & System.Windows.Forms.Keys.Control) == System.Windows.Forms.Keys.Control;
            if (ctrlPressed)
            {
                if (key == System.Windows.Forms.Keys.E)
                {
                    Dispatcher.Invoke(() => { if (!IsDisabled && BtnPlayRec.IsEnabled) BtnPlayRec_Click(null, null); });
                }
                else if (key == System.Windows.Forms.Keys.R)
                {
                    Dispatcher.Invoke(() => { if (!IsDisabled) BtnSaveFromPersonaInjector_Click(null, null); });
                }
                else if (key == System.Windows.Forms.Keys.S)
                {
                    Dispatcher.Invoke(() => { if (!IsDisabled && BtnSEO.IsEnabled) SaveDirectly(); });
                }
            }
        }

        private void SetIconsAtRuntime()
        {
            try
            {
                titleText.Text = "\U0001F4CB Mini Clip";
                TxtNoImage.Text = "\U0001F4F7";
            }
            catch { }
        }

        private void BtnDisable_Click(object sender, RoutedEventArgs e) => IsDisabled = !IsDisabled;
        private void BtnCompact_Click(object sender, RoutedEventArgs e) => IsCompactMode = !IsCompactMode;

        private void MiniClipboardWindow_Closed(object sender, EventArgs e)
        {
            _netTimer?.Stop();
            _globalHook?.Dispose();
            if (_autoImportWatcher != null) { _autoImportWatcher.EnableRaisingEvents = false; _autoImportWatcher.Dispose(); }
            if (_hwndSource != null)
            {
                var helper = new WindowInteropHelper(this);
                ChangeClipboardChain(helper.Handle, _nextClipboardViewer);
                _hwndSource.RemoveHook(WndProc);
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case WM_DRAWCLIPBOARD:
                    OnClipboardChanged();
                    SendMessage(_nextClipboardViewer, msg, wParam, lParam);
                    break;
                case WM_CHANGECBCHAIN:
                    if (wParam == _nextClipboardViewer) _nextClipboardViewer = lParam;
                    else if (_nextClipboardViewer != IntPtr.Zero) SendMessage(_nextClipboardViewer, msg, wParam, lParam);
                    break;
            }
            return IntPtr.Zero;
        }

        private void OnClipboardChanged()
        {
            try
            {
                if (IsDisabled) return;
                if (_ignoreNextClipboardChange) { _ignoreNextClipboardChange = false; return; }
                if ((DateTime.Now - _lastClipboardTime).TotalMilliseconds < 500) return;
                _lastClipboardTime = DateTime.Now;

                if (IsAutoFillEnabled && ClipboardMetadata.IsValid())
                {
                    if (!string.IsNullOrEmpty(ClipboardMetadata.CharacterName)) TxtTitle.Text = ClipboardMetadata.CharacterName;
                    if (!string.IsNullOrEmpty(ClipboardMetadata.BasePromptName)) { IsAdditionalTitleEnabled = true; AdditionalTitle = ClipboardMetadata.BasePromptName; }
                    ClipboardMetadata.Clear();
                }

                if (System.Windows.Clipboard.ContainsImage())
                {
                    var image = System.Windows.Clipboard.GetImage();
                    if (image != null)
                    {
                        var convertedBitmap = new FormatConvertedBitmap();
                        convertedBitmap.BeginInit();
                        convertedBitmap.Source = image;
                        convertedBitmap.DestinationFormat = PixelFormats.Bgra32;
                        convertedBitmap.EndInit();
                        var finalPreview = new WriteableBitmap(convertedBitmap);
                        finalPreview.Freeze();
                        _capturedImages.Add(new CapturedImageInfo { Bitmap = finalPreview, OriginalPath = null });
                        _hasImage = true;
                        UpdateImagePreviews();
                        UpdateState();
                        CheckAutoSaveTrigger();
                    }
                }
                else if (System.Windows.Clipboard.ContainsText())
                {
                    string rawText = System.Windows.Clipboard.GetText();
                    if (_autoCaptureExtraTemplate && TrySetMiniExtraTemplate(rawText))
                    {
                        return;
                    }

                    if (Regex.IsMatch(rawText, @"[\u0600-\u06FF]"))
                    {
                        if (!IsTitleLocked)
                        {
                            TxtTitle.Text = rawText.Trim();
                            TxtTitle.IsEnabled = true;
                            this.Activate();
                            TxtTitle.Focus();
                            UpdateState();
                            CheckAutoSaveTrigger();
                            return;
                        }
                    }

                    string text = FilterEnglishOnly(rawText);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        int englishLetterCount = Regex.Matches(rawText, "[A-Za-z]").Count;
                        if (englishLetterCount > 0 && englishLetterCount < 5) return;
                        if (text == _positivePrompt || text == _negativePrompt) return;

                        if (!_hasPositivePrompt)
                        {
                            _positivePrompt = text; _hasPositivePrompt = true;
                            TxtPositiveCheck.Text = "✓"; TxtPositiveCheck.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#89D185"));
                        }
                        else if (!_hasNegativePrompt && !IsNegativeLocked)
                        {
                            _negativePrompt = text; _hasNegativePrompt = true;
                            TxtNegativeCheck.Text = "✓"; TxtNegativeCheck.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#89D185"));
                            IsNegativeLocked = true; // Auto-lock after receiving
                        }
                        else if (!IsNegativeLocked)
                        {
                            _positivePrompt = _negativePrompt; _negativePrompt = text;
                            TxtPositiveCheck.Text = "✓"; TxtNegativeCheck.Text = "✓";
                            IsNegativeLocked = true; // Auto-lock after receiving
                        }
                        else if (IsNegativeLocked)
                        {
                            _positivePrompt = text; _hasPositivePrompt = true;
                            TxtPositiveCheck.Text = "✓";
                        }
                        UpdateState();
                        CheckAutoSaveTrigger();
                    }
                }
            }
            catch { }
        }

        private void UpdateState()
        {
            TxtTitle.IsEnabled = true;
            BtnSEO.IsEnabled = _capturedImages.Count > 0 && _hasPositivePrompt && _hasNegativePrompt;
            if (_hasPositivePrompt && _hasNegativePrompt && !IsCompactMode) { this.Activate(); TxtTitle.Focus(); }
        }

        private void CheckAutoSaveTrigger()
        {
            if (_autoSaveEnabled &&
                _capturedImages.Count >= _autoSaveThreshold &&
                _hasPositivePrompt &&
                _hasNegativePrompt &&
                !string.IsNullOrWhiteSpace(TxtTitle.Text))
            {
                SaveDirectly();
            }
        }

        private void UpdateImagePreviews()
        {
            ImageGrid.Children.Clear();
            for (int i = 0; i < _capturedImages.Count; i++)
            {
                var border = new Border
                {
                    Width = 70,
                    Height = 70,
                    CornerRadius = new CornerRadius(4),
                    Margin = new Thickness(2),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Tag = i.ToString(),
                    Background = System.Windows.Media.Brushes.Black,
                    BorderBrush = (System.Windows.Media.Brush)FindResource("BorderBrush"),
                    BorderThickness = new Thickness(1)
                };
                border.MouseLeftButtonDown += ImagePreview_MouseLeftButtonDown;
                border.Child = new System.Windows.Controls.Image { Source = _capturedImages[i].Bitmap, Stretch = Stretch.Uniform };
                ImageGrid.Children.Add(border);
            }
            TxtNoImage.Visibility = _capturedImages.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        private void TxtTitle_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (IsAdditionalTitleEnabled && string.IsNullOrWhiteSpace(TxtAdditionalTitle.Text)) { TxtAdditionalTitle.Focus(); return; }
                if (BtnSEO.IsEnabled && !string.IsNullOrWhiteSpace(TxtTitle.Text)) SaveDirectly();
            }
        }

        private void BtnSEO_Click(object sender, RoutedEventArgs e) => SaveDirectly();

        private void ImagePreview_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement el && int.TryParse(el.Tag.ToString(), out int idx))
            {
                _capturedImages.RemoveAt(idx); UpdateImagePreviews(); _hasImage = _capturedImages.Count > 0; UpdateState();
            }
        }

        private void PositivePrompt_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _hasPositivePrompt = false; _positivePrompt = ""; TxtPositiveCheck.Text = "○";
            TxtPositiveCheck.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#969696"));
            UpdateState();
        }

        private void NegativePrompt_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (IsNegativeLocked) return;
            _hasNegativePrompt = false; _negativePrompt = ""; TxtNegativeCheck.Text = "○";
            TxtNegativeCheck.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#969696"));
            UpdateState();
        }

        private void SaveDirectly()
        {
            if (_isSaving) return;
            string savePath = "";
            foreach (Window w in System.Windows.Application.Current.Windows) if (w is MainWindow mw) savePath = mw.SavePath;
            if (string.IsNullOrEmpty(savePath) || !Directory.Exists(savePath)) { System.Windows.MessageBox.Show("Invalid Save Path"); return; }

            string title = TxtTitle.Text.Trim();
            if (IsAdditionalTitleEnabled && !string.IsNullOrWhiteSpace(AdditionalTitle)) title += " " + AdditionalTitle.Trim();
            if (string.IsNullOrEmpty(title)) { if (!IsCompactMode) TxtTitle.Focus(); return; }

            _isSaving = true;
            try
            {
                for (int i = 0; i < _capturedImages.Count; i++)
                {
                    var item = _capturedImages[i];
                    string ext = item.OriginalPath != null ? Path.GetExtension(item.OriginalPath) : ".png";
                    string currentTitle = _capturedImages.Count > 1 ? $"{title} ({i + 1})" : title;
                    string imgPath = Path.Combine(savePath, currentTitle + ext);
                    string txtPath = Path.Combine(savePath, currentTitle + ".txt");
                    int counter = 1;
                    while (File.Exists(imgPath)) { imgPath = Path.Combine(savePath, $"{currentTitle} ({counter}){ext}"); txtPath = Path.Combine(savePath, $"{currentTitle} ({counter}).txt"); counter++; }

                    if (item.OriginalPath != null && File.Exists(item.OriginalPath))
                    {
                        File.Copy(item.OriginalPath, imgPath, true);
                    }
                    else
                    {
                        using (var fs = new FileStream(imgPath, FileMode.Create))
                        {
                            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(item.Bitmap)); encoder.Save(fs);
                        }
                    }
                    File.WriteAllText(txtPath, $"Positive Prompt:\n{_positivePrompt}\n\nNegative Prompt:\n{_negativePrompt}");
                }
                FlashSuccess();
                if (IsSaveBasePromptEnabled && !string.IsNullOrWhiteSpace(_positivePrompt))
                {
                    BasePromptManager.Add(new BasePrompt { Name = !string.IsNullOrWhiteSpace(AdditionalTitle) ? AdditionalTitle.Trim() : "BP", PromptText = _positivePrompt.Trim() });
                }
                ResetState();
            }
            catch (Exception ex) { System.Windows.MessageBox.Show(ex.Message); }
            finally { _isSaving = false; }
        }

        private void FlashSuccess()
        {
            var anim = new DoubleAnimation(1.0, 0.4, TimeSpan.FromMilliseconds(200)) { AutoReverse = true };
            MainBorder.BeginAnimation(UIElement.OpacityProperty, anim);
        }

        private void ResetState()
        {
            _hasImage = false; _hasPositivePrompt = false; _capturedImages.Clear(); _positivePrompt = "";
            if (!IsNegativeLocked) { _hasNegativePrompt = false; _negativePrompt = ""; TxtNegativeCheck.Text = "○"; TxtNegativeCheck.Foreground = System.Windows.Media.Brushes.Gray; }
            UpdateImagePreviews();
            TxtPositiveCheck.Text = "○"; TxtPositiveCheck.Foreground = System.Windows.Media.Brushes.Gray;
            if (!IsTitleLocked) TxtTitle.Text = "";
            if (!IsAdditionalTitleLocked) AdditionalTitle = "";
            BtnSEO.IsEnabled = false;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.ClickCount == 1) DragMove(); }
        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
        private void BtnLockNegative_Click(object sender, RoutedEventArgs e) => IsNegativeLocked = !IsNegativeLocked;
        private void BtnLockAdditionalTitle_Click(object sender, RoutedEventArgs e) => IsAdditionalTitleLocked = !IsAdditionalTitleLocked;
        private void BtnLockTitle_Click(object sender, RoutedEventArgs e) => IsTitleLocked = !IsTitleLocked;
        private void BtnToggleMenu_Click(object sender, RoutedEventArgs e) => IsExtraMenuOpen = !IsExtraMenuOpen;
        private void BtnExtraMenuPageOne_Click(object sender, RoutedEventArgs e) => ExtraMenuPage = 0;
        private void BtnExtraMenuPageTwo_Click(object sender, RoutedEventArgs e) => ExtraMenuPage = 1;
        private void BtnExtraMenuPageThree_Click(object sender, RoutedEventArgs e) => ExtraMenuPage = 2;
        private void BtnCaptureExtraTemplate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!System.Windows.Clipboard.ContainsText())
                {
                    CustomMessageBox.Show("Clipboard does not contain text.", "Extra Template", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var text = System.Windows.Clipboard.GetText();
                if (TrySetMiniExtraTemplate(text)) return;

                _miniExtraTemplate = "";
                SetMiniExtraButtonState(false);
                string message = string.IsNullOrWhiteSpace(text)
                    ? "Clipboard text is empty."
                    : "Template must contain the [extra] tag.";
                CustomMessageBox.Show(message, "Extra Template", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(ex.Message, "Extra Template", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool TrySetMiniExtraTemplate(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            if (!text.Contains(MiniExtraPlaceholderTag, StringComparison.OrdinalIgnoreCase)) return false;

            _miniExtraTemplate = text;
            SetMiniExtraButtonState(true);
            return true;
        }

        private void BtnCopyExtraTemplateOutput_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(_miniExtraTemplate))
                {
                    CustomMessageBox.Show("First capture a clipboard template that contains [extra].", "Extra Template", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (!TryGetLatestExtraText(out var extraText, out var errorMessage))
                {
                    CustomMessageBox.Show(errorMessage, "Extra Template", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var output = _miniExtraTemplate.Replace(MiniExtraPlaceholderTag, extraText, StringComparison.OrdinalIgnoreCase);
                System.Windows.Clipboard.SetText(output);
                SetMiniExtraButtonState(true);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(ex.Message, "Extra Template", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool TryGetLatestExtraText(out string extraText, out string errorMessage)
        {
            extraText = "";
            errorMessage = "";

            var injector = GetOpenPersonaInjector();
            if (injector != null && injector.TryGetCurrentExtraText(out extraText, out errorMessage))
            {
                return true;
            }

            return LastExtraSelectionStore.TryGetText(out extraText, out errorMessage);
        }

        private PersonaInjectorWindow GetOpenPersonaInjector()
        {
            foreach (Window w in System.Windows.Application.Current.Windows)
            {
                if (w is PersonaInjectorWindow injector) return injector;
            }

            return null;
        }

        private void SetMiniExtraButtonState(bool hasTemplate)
        {
            if (BtnCaptureExtraTemplate?.Template.FindName("txt", BtnCaptureExtraTemplate) is TextBlock captureText)
                captureText.Foreground = hasTemplate ? (System.Windows.Media.Brush)FindResource("SuccessBrush") : (System.Windows.Media.Brush)FindResource("ForegroundMutedBrush");

            if (BtnCopyExtraTemplateOutput?.Template.FindName("txt", BtnCopyExtraTemplateOutput) is TextBlock copyText)
                copyText.Foreground = hasTemplate ? (System.Windows.Media.Brush)FindResource("SuccessBrush") : (System.Windows.Media.Brush)FindResource("ForegroundMutedBrush");

            if (BtnCaptureExtraTemplate?.Template.FindName("bd", BtnCaptureExtraTemplate) is Border captureBorder)
                captureBorder.BorderBrush = hasTemplate ? (System.Windows.Media.Brush)FindResource("SuccessBrush") : (System.Windows.Media.Brush)FindResource("BorderBrush");

            if (BtnCopyExtraTemplateOutput?.Template.FindName("bd", BtnCopyExtraTemplateOutput) is Border copyBorder)
                copyBorder.BorderBrush = hasTemplate ? (System.Windows.Media.Brush)FindResource("SuccessBrush") : (System.Windows.Media.Brush)FindResource("BorderBrush");
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e) => ResetState();
        private void BtnSaveBasePrompt_Click(object sender, RoutedEventArgs e) => IsSaveBasePromptEnabled = !IsSaveBasePromptEnabled;
        private void BtnAutoFill_Click(object sender, RoutedEventArgs e) => IsAutoFillEnabled = !IsAutoFillEnabled;
        private void BtnBrowserQuickPaste_Click(object sender, RoutedEventArgs e) => SetBrowserQuickPasteEnabled(!_isBrowserQuickPasteEnabled);

        private async void BtnPlayBrowserRec_Click(object sender, RoutedEventArgs e)
        {
            BrowserWindow? browserTarget = null;
            foreach (Window w in System.Windows.Application.Current.Windows)
            {
                if (w is BrowserWindow browser && browser.IsLoaded)
                {
                    browserTarget = browser;
                    break;
                }
            }

            if (browserTarget == null) return;

            BtnPlayBrowserRec.IsEnabled = false;
            try
            {
                await browserTarget.PlayBrowserRecordingAsync();
            }
            finally
            {
                BtnPlayBrowserRec.IsEnabled = true;
            }
        }

        private async void TriggerBrowserQuickPasteIfReady()
        {
            if (!_isBrowserQuickPasteEnabled || _isBrowserQuickPasteRunning) return;
            if (!IsBrowserWebViewTargetActive()) return;

            _isBrowserQuickPasteRunning = true;
            try
            {
                await Task.Delay(50);
                if (!IsBrowserWebViewTargetActive()) return;
                InputSimulator.SimulateSelectAll();
                await Task.Delay(500);
                if (!IsBrowserWebViewTargetActive()) return;
                InputSimulator.SimulatePaste();
            }
            finally
            {
                _isBrowserQuickPasteRunning = false;
            }
        }

        private bool IsBrowserWebViewTargetActive()
        {
            foreach (Window w in System.Windows.Application.Current.Windows)
            {
                if (w is BrowserWindow browser && browser.IsCurrentWebViewTarget()) return true;
            }

            return false;
        }

        private void SetBrowserQuickPasteEnabled(bool enabled)
        {
            _isBrowserQuickPasteEnabled = enabled;
            SetToggleButtonState(BtnBrowserQuickPaste, _isBrowserQuickPasteEnabled, "#89D185");
        }

        private void SetToggleButtonState(System.Windows.Controls.Button button, bool isEnabled, string activeColor)
        {
            var activeBrush = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(activeColor));

            if (button?.Template.FindName("txt", button) is TextBlock text)
                text.Foreground = isEnabled ? activeBrush : (System.Windows.Media.Brush)FindResource("ForegroundMutedBrush");

            if (button?.Template.FindName("bd", button) is Border border)
                border.BorderBrush = isEnabled ? activeBrush : (System.Windows.Media.Brush)FindResource("BorderBrush");
        }

        private async void BtnPlayRec_Click(object sender, RoutedEventArgs e)
        {
            RecordingManager.LoadState();
            int slotToPlay = RecordingManager.SequentialMode ? _nextMiniSlot : RecordingManager.SelectedSlot;
            if (!RecordingManager.HasEvents(slotToPlay))
            {
                int other = (slotToPlay == 1) ? 2 : 1;
                if (RecordingManager.HasEvents(other)) slotToPlay = other;
                else return;
            }
            BtnPlayRec.IsEnabled = false;
            if (BtnPlayRec.Template.FindName("txtPlay", BtnPlayRec) is TextBlock txt)
            {
                txt.Foreground = new SolidColorBrush(slotToPlay == 1 ? (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F1C40F") : (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E67E22"));
                txt.Text = $"▶ S{slotToPlay}";
            }
            _playerRec.SetEvents(RecordingManager.GetEvents(slotToPlay));
            _playerRec.SetSpeed(RecordingManager.PlaybackSpeed);
            await _playerRec.PlayAsync(false);
            if (RecordingManager.SequentialMode) _nextMiniSlot = (slotToPlay == 1) ? 2 : 1;
            BtnPlayRec.IsEnabled = true;
        }

        private void BtnSaveFromPersonaInjector_Click(object sender, RoutedEventArgs e)
        {
            PersonaInjectorWindow injector = null;
            foreach (Window w in System.Windows.Application.Current.Windows) if (w is PersonaInjectorWindow piw) injector = piw;
            if (injector == null) return;
            injector.PerformRandomForCurrentTab();
            System.Threading.Tasks.Task.Delay(150).ContinueWith(_ => Dispatcher.Invoke(() => {
                if (ClipboardMetadata.IsValid())
                {
                    if (!string.IsNullOrEmpty(ClipboardMetadata.CharacterName)) TxtTitle.Text = ClipboardMetadata.CharacterName;
                    if (!string.IsNullOrEmpty(ClipboardMetadata.BasePromptName)) { IsAdditionalTitleEnabled = true; AdditionalTitle = ClipboardMetadata.BasePromptName; }
                    ClipboardMetadata.Clear(); this.Activate(); if (!IsCompactMode) TxtTitle.Focus();
                    CheckAutoSaveTrigger();
                }
            }));
        }

        private string FilterEnglishOnly(string input) => string.IsNullOrEmpty(input) ? "" : Regex.Replace(input, @"[^\u0000-\u007F]+", "");
        private void AdditionalTitle_KeyDown(object sender, System.Windows.Input.KeyEventArgs e) { if (e.Key == Key.Enter && BtnSEO.IsEnabled) SaveDirectly(); }
    }
}
