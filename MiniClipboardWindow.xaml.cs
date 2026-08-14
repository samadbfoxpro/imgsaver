using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Collections.ObjectModel;
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
    public partial class MiniClipboardWindow : Window, IMiniClipHost
    {
        private class CapturedImageInfo
        {
            public BitmapSource Bitmap { get; set; }
            public string OriginalPath { get; set; }
        }

        private InputPlayer _playerRec = new InputPlayer();
        private HwndSource _hwndSource;
        private GlobalHook _globalHook;
        private FileSystemWatcher _autoImportWatcher;

        private bool _hasImage = false;
        private bool _hasPositivePrompt = false;
        private bool _hasNegativePrompt = false;
        private bool _ignoreNextClipboardChange = true;
        private bool _ignoreNextSpiSyncClipboardText = false;
        private DateTime _lastClipboardTime = DateTime.MinValue;
        private string _lastClipboardText = "";
        private string _lastSavedMainTitle = "";
        private bool _isSaving = false;
        private int _nextMiniSlot = 1;
        private const string MiniExtraPlaceholderTag = "[extra]";
        private string _miniExtraTemplate = "";

        // Auto-Save Settings
        private bool _autoSaveEnabled = false;
        private int _autoSaveThreshold = 1;
        private bool _autoCaptureExtraTemplate = true;
        private bool _autoCopyExtraTemplateOutput = true;
        private bool _autoCopyTagReplacerOutput = true;
        private bool _replacePositivePromptOnClipboardText = true;
        private bool _useTagReplacerForMiniClip = false;
        private string _tagReplacerPrefix = "PH_";
        private bool _autoSaveDelayEnabled = false;
        private int _autoSaveDelaySeconds = 10;
        private DispatcherTimer? _autoSaveCountdownTimer;
        private int _autoSaveRemainingSeconds = 0;

        public bool IsAutoSaveEnabled
        {
            get => _autoSaveEnabled;
            set { _autoSaveEnabled = value; SaveConfigSettings(); }
        }
        public int AutoSaveThreshold
        {
            get => _autoSaveThreshold;
            set { _autoSaveThreshold = value; SaveConfigSettings(); }
        }
        public bool IsAutoSaveDelayEnabled
        {
            get => _autoSaveDelayEnabled;
            set { _autoSaveDelayEnabled = value; SaveConfigSettings(); }
        }
        public int AutoSaveDelaySeconds
        {
            get => _autoSaveDelaySeconds;
            set { _autoSaveDelaySeconds = value; SaveConfigSettings(); }
        }

        public void SaveConfigSettings()
        {
            try
            {
                string configPath = DataPathManager.GetSettingsFilePath("config.txt");
                if (!File.Exists(configPath)) return;
                string[] lines = File.ReadAllLines(configPath);
                if (lines.Length > 4) lines[4] = _autoSaveEnabled.ToString().ToLower();
                if (lines.Length > 5) lines[5] = _autoSaveThreshold.ToString();
                if (lines.Length > 14) lines[14] = _autoSaveDelayEnabled.ToString().ToLower();
                if (lines.Length > 15) lines[15] = _autoSaveDelaySeconds.ToString();
                File.WriteAllLines(configPath, lines);
            }
            catch { }
        }

        private DispatcherTimer _netTimer;
        private long _lastBytesReceived = 0;
        private long _lastBytesSent = 0;
        private bool _isBrowserQuickPasteEnabled = false;
        private bool _isBrowserQuickPasteRunning = false;
        private bool _isManualExtraCopyRunning = false;

        public static readonly DependencyProperty IsDisabledProperty =
            DependencyProperty.Register("IsDisabled", typeof(bool), typeof(MiniClipboardWindow), new PropertyMetadata(false, OnIsDisabledChanged));

        private static void OnIsDisabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MiniClipboardWindow window && (bool)e.NewValue == false)
            {
                window._ignoreNextClipboardChange = false;
                window._lastClipboardText = "";
                window.OnClipboardChanged();
            }
        }

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
            DependencyProperty.Register("IsNegativeLocked", typeof(bool), typeof(MiniClipboardWindow), 
                new PropertyMetadata(false, OnIsNegativeLockedChanged));

        private static void OnIsNegativeLockedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MiniClipboardWindow window)
            {
                window.SaveNegativePromptState();
            }
        }

        public bool IsNegativeLocked
        {
            get { return (bool)GetValue(IsNegativeLockedProperty); }
            set { SetValue(IsNegativeLockedProperty, value); }
        }

        public static readonly DependencyProperty IsDescriptionVisibleProperty =
            DependencyProperty.Register("IsDescriptionVisible", typeof(bool), typeof(MiniClipboardWindow), new PropertyMetadata(false));

        public bool IsDescriptionVisible
        {
            get { return (bool)GetValue(IsDescriptionVisibleProperty); }
            set { SetValue(IsDescriptionVisibleProperty, value); }
        }

        public static readonly DependencyProperty IsAdditionalTitleVisibleProperty =
            DependencyProperty.Register("IsAdditionalTitleVisible", typeof(bool), typeof(MiniClipboardWindow), new PropertyMetadata(false));

        public bool IsAdditionalTitleVisible
        {
            get { return (bool)GetValue(IsAdditionalTitleVisibleProperty); }
            set { SetValue(IsAdditionalTitleVisibleProperty, value); }
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

        public static readonly DependencyProperty IsDescriptionEnabledProperty =
            DependencyProperty.Register("IsDescriptionEnabled", typeof(bool), typeof(MiniClipboardWindow), new PropertyMetadata(false, OnIsDescriptionEnabledChanged));

        private static void OnIsDescriptionEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MiniClipboardWindow window && (bool)e.NewValue == false)
            {
                window.DescriptionText = "";
            }
        }

        private void AutoDetectDirection_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                string text = textBox.Text;
                if (string.IsNullOrWhiteSpace(text))
                {
                    textBox.FlowDirection = System.Windows.FlowDirection.RightToLeft;
                    textBox.TextAlignment = TextAlignment.Right;
                    return;
                }

                // Check for Persian / Arabic character presence in the text
                bool isRtl = false;
                foreach (char c in text)
                {
                    if ((c >= 0x0600 && c <= 0x06FF) || (c >= 0x0750 && c <= 0x077F) || (c >= 0xFB50 && c <= 0xFDFF) || (c >= 0xFE70 && c <= 0xFEFF))
                    {
                        isRtl = true;
                        break;
                    }
                }

                if (isRtl)
                {
                    textBox.FlowDirection = System.Windows.FlowDirection.RightToLeft;
                    textBox.TextAlignment = TextAlignment.Right;
                }
                else
                {
                    textBox.FlowDirection = System.Windows.FlowDirection.LeftToRight;
                    textBox.TextAlignment = TextAlignment.Left;
                }
            }
        }

        public bool IsDescriptionEnabled
        {
            get { return (bool)GetValue(IsDescriptionEnabledProperty); }
            set { SetValue(IsDescriptionEnabledProperty, value); }
        }

        public static readonly DependencyProperty DescriptionTextProperty =
            DependencyProperty.Register("DescriptionText", typeof(string), typeof(MiniClipboardWindow), new PropertyMetadata(""));

        public string DescriptionText
        {
            get { return (string)GetValue(DescriptionTextProperty); }
            set { SetValue(DescriptionTextProperty, value); }
        }

        public static readonly DependencyProperty IsDescriptionLockedProperty =
            DependencyProperty.Register("IsDescriptionLocked", typeof(bool), typeof(MiniClipboardWindow), new PropertyMetadata(false));

        public bool IsDescriptionLocked
        {
            get { return (bool)GetValue(IsDescriptionLockedProperty); }
            set { SetValue(IsDescriptionLockedProperty, value); }
        }

        private List<CapturedImageInfo> _capturedImages = new List<CapturedImageInfo>();
        private void ResetAllFields()
        {
            _capturedImages.Clear();
            _positivePrompt = "";
            _basePositivePrompt = "";
            NegativePrompt = ""; // Also clears _hasNegativePrompt
            _hasPositivePrompt = false;
            TxtTitle.Text = "";
            TxtTitle.IsEnabled = false;
            IsDescriptionEnabled = false;
            IsAdditionalTitleEnabled = false;
            AdditionalTitle = "";
            TxtPositiveCheck.Text = "○";
            TxtPositiveCheck.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#969696"));
            TxtNegativeCheck.Text = "○";
            TxtNegativeCheck.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#969696"));
            IsNegativeLocked = false;
            UpdateImagePreviews();
            UpdatePositivePromptToolTip();
            UpdateState();
            
            try
            {
                var combinerData = PromptCombinerStore.Load();
                bool combinerChanged = false;
                if (combinerData != null)
                {
                    if (combinerData.ActiveItemIds != null && combinerData.ActiveItemIds.Count > 0)
                    {
                        combinerData.ActiveItemIds.Clear();
                        combinerChanged = true;
                    }
                    if (combinerData.Folders != null)
                    {
                        foreach (var f in combinerData.Folders)
                        {
                            if (f.IsCustomInputActive)
                            {
                                f.IsCustomInputActive = false;
                                combinerChanged = true;
                            }
                        }
                    }
                    if (combinerChanged)
                    {
                        PromptCombinerStore.Save(combinerData);
                        foreach (System.Windows.Window win in System.Windows.Application.Current.Windows)
                        {
                            if (win is BrowserWindow bw)
                            {
                                bw.InitializeCombiner();
                            }
                        }
                    }
                }
            }
            catch { }
        }

        private string _positivePrompt = "";
        private string _basePositivePrompt = "";
        private string _negativePrompt = "";
        public string NegativePrompt
        {
            get => _negativePrompt;
            set
            {
                _negativePrompt = value;
                _hasNegativePrompt = !string.IsNullOrEmpty(_negativePrompt);
                if (BorderNegative != null)
                {
                    BorderNegative.ToolTip = GetTruncatedTooltipText(_negativePrompt, isPositive: false);
                }

                if (TxtNegativeCheck != null)
                {
                    if (_hasNegativePrompt)
                    {
                        TxtNegativeCheck.Text = "✓";
                        TxtNegativeCheck.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#89D185"));
                    }
                    else
                    {
                        TxtNegativeCheck.Text = "○";
                        TxtNegativeCheck.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#969696"));
                    }
                }

                SaveNegativePromptState();
                _miniNegativePanel?.UpdateActiveText();
                UpdateState();
            }
        }

        private string GetTruncatedTooltipText(string text, bool isPositive = false)
        {
            if (string.IsNullOrEmpty(text)) return isPositive ? "Positive Prompt (Click to clear)" : "Negative Prompt (Click to clear)";
            string cleanText = Regex.Replace(text.Replace('\r', ' ').Replace('\n', ' '), @"\s+", " ").Trim();
            var words = cleanText.Split(' ');
            if (words.Length <= 5) return cleanText;
            return string.Join(" ", words.Take(5)) + "...";
        }

        private void UpdatePositivePromptToolTip()
        {
            if (BorderPositive != null)
            {
                BorderPositive.ToolTip = GetTruncatedTooltipText(_positivePrompt, isPositive: true);
            }
        }

        private string NegativePromptStatePath => DataPathManager.GetSettingsFilePath("negative_prompt_state.json");

        private void SaveNegativePromptState()
        {
            try
            {
                var data = new NegativePromptState
                {
                    NegativePrompt = _negativePrompt,
                    IsNegativeLocked = IsNegativeLocked
                };
                string path = NegativePromptStatePath;
                string dir = Path.GetDirectoryName(path) ?? "";
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch { }
        }

        private void LoadNegativePromptState()
        {
            try
            {
                string path = NegativePromptStatePath;
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var data = JsonSerializer.Deserialize<NegativePromptState>(json);
                    if (data != null)
                    {
                        _negativePrompt = data.NegativePrompt ?? "";
                        if (BorderNegative != null)
                        {
                            BorderNegative.ToolTip = GetTruncatedTooltipText(_negativePrompt, isPositive: false);
                        }
                        IsNegativeLocked = data.IsNegativeLocked;
                        if (!string.IsNullOrEmpty(_negativePrompt))
                        {
                            _hasNegativePrompt = true;
                            TxtNegativeCheck.Text = "✓";
                            TxtNegativeCheck.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#89D185"));
                        }
                        UpdateState();
                    }
                }
                UpdatePositivePromptToolTip();
            }
            catch { }
        }

        private class NegativePromptState
        {
            public string? NegativePrompt { get; set; }
            public bool IsNegativeLocked { get; set; }
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        private const int WM_CLIPBOARDUPDATE = 0x031D;

        public MiniClipboardWindow()
        {
            InitializeComponent();
            LanguageManager.ApplyWindowLanguage(this);
            
            TouchRightClickHelper.Register(TxtTitle);
            TouchRightClickHelper.Register(TxtAdditionalTitle);
            TouchRightClickHelper.Register(TxtDescription);

            Loaded  += MiniClipboardWindow_Loaded;
            Closed  += MiniClipboardWindow_Closed;
            LocationChanged += (s, e) => { RepositionExtraPanel(); RepositionNegativePanel(); RepositionAutoSavePanel(); RepositionBaseCombinerPanel(); };
            SizeChanged     += (s, e) => { RepositionExtraPanel(); RepositionNegativePanel(); RepositionAutoSavePanel(); RepositionBaseCombinerPanel(); };

            if (!string.IsNullOrEmpty(ExtraFloatBridge.LastConfirmedTitle))
            {
                IsAdditionalTitleEnabled = true;
                AdditionalTitle = ExtraFloatBridge.LastConfirmedTitle;
            }
            ExtraFloatBridge.ExtraTitleConfirmed += OnExtraTitleConfirmed;
        }

        private void OnExtraTitleConfirmed(string title)
        {
            Dispatcher.Invoke(() =>
            {
                IsAdditionalTitleEnabled = true;
                AdditionalTitle = title;
            });
        }

        private void MiniClipboardWindow_Loaded(object sender, RoutedEventArgs e)
        {
            SetIconsAtRuntime();
            var helper = new WindowInteropHelper(this);
            IntPtr handle = helper.EnsureHandle();
            _hwndSource = HwndSource.FromHwnd(handle);
            _hwndSource?.AddHook(WndProc);
            _ignoreNextClipboardChange = true;
            AddClipboardFormatListener(handle);

            try
            {
                _globalHook = new GlobalHook();
                _globalHook.OnKeyPressed += GlobalHook_OnKeyPressed;
            }
            catch { }

            StartNetMonitoring();
            RefreshAutoImport();
            LoadNegativePromptState();
            UpdateCombinerToggleVisual();
        }

        private void UpdateCombinerToggleVisual()
        {
            if (BtnCombinerToggle == null) return;
            var data = PromptCombinerStore.Load();
            bool isEnabled = data != null && data.IsEnabled;

            var txt = BtnCombinerToggle.Template?.FindName("txt", BtnCombinerToggle) as TextBlock;
            var bd = BtnCombinerToggle.Template?.FindName("bd", BtnCombinerToggle) as Border;

            if (txt != null)
            {
                txt.Foreground = isEnabled ? new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2ECC71")) : new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#888888"));
            }
            if (bd != null)
            {
                bd.BorderBrush = isEnabled ? new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2ECC71")) : new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#444444"));
            }

            BtnCombinerToggle.ToolTip = isEnabled ? "Smart Prompt Combiner: ON (Click to Disable)" : "Smart Prompt Combiner: OFF (Click to Enable)";
        }

        private void BtnCombinerToggle_Click(object sender, RoutedEventArgs e)
        {
            var data = PromptCombinerStore.Load();
            if (data == null) return;
            data.IsEnabled = !data.IsEnabled;
            PromptCombinerStore.Save(data);
            UpdateCombinerToggleVisual();

            // Sync with BrowserWindow if open
            try
            {
                foreach (System.Windows.Window win in System.Windows.Application.Current.Windows)
                {
                    if (win is BrowserWindow bw)
                    {
                        bw.InitializeCombiner();
                    }
                }
            }
            catch { }
        }

        public void FlashTagReplacedSuccess()
        {
            Dispatcher.InvokeAsync(() =>
            {
                CursorCombinerBadge.ShowTagReplaced("🧩 Tag Replaced!");
            });
        }

        public void FlashExtraReplacedSuccess()
        {
            Dispatcher.InvokeAsync(() =>
            {
                CursorCombinerBadge.ShowExtraReplaced("✨ Extra Applied!");
            });
        }

        private void TriggerExtraOrTagNotification()
        {
            if (_useTagReplacerForMiniClip)
            {
                FlashTagReplacedSuccess();
            }
            else
            {
                FlashExtraReplacedSuccess();
            }
        }

        public void FlashCombinerSuccess()
        {
            Dispatcher.InvokeAsync(() =>
            {
                CursorCombinerBadge.Show("⚡ Combined!");
                UpdateCombinerToggleVisual();
                if (BtnCombinerToggle == null) return;
                try
                {
                    var txt = BtnCombinerToggle.Template?.FindName("txt", BtnCombinerToggle) as TextBlock;
                    var bd = BtnCombinerToggle.Template?.FindName("bd", BtnCombinerToggle) as Border;

                    var vividOrange = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF6A00");
                    var normalGreen = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2ECC71");

                    var borderBrush = new System.Windows.Media.SolidColorBrush(normalGreen);
                    var textBrush = new System.Windows.Media.SolidColorBrush(normalGreen);

                    if (bd != null) bd.BorderBrush = borderBrush;
                    if (txt != null) txt.Foreground = textBrush;

                    var animation = new System.Windows.Media.Animation.ColorAnimation
                    {
                        From = normalGreen,
                        To = vividOrange,
                        Duration = new Duration(TimeSpan.FromMilliseconds(250)),
                        AutoReverse = true,
                        RepeatBehavior = new System.Windows.Media.Animation.RepeatBehavior(3)
                    };

                    borderBrush.BeginAnimation(System.Windows.Media.SolidColorBrush.ColorProperty, animation);
                    textBrush.BeginAnimation(System.Windows.Media.SolidColorBrush.ColorProperty, animation);
                }
                catch { }
            });
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

                string configPath = DataPathManager.GetSettingsFilePath("config.txt");
                if (!File.Exists(configPath)) return;

                string[] lines = File.ReadAllLines(configPath);

                // Load Auto-Save Settings
                if (lines.Length > 4) _autoSaveEnabled = lines[4].Trim().ToLower() == "true";
                if (lines.Length > 5 && int.TryParse(lines[5].Trim(), out int threshold)) _autoSaveThreshold = threshold;
                _autoCaptureExtraTemplate = lines.Length <= 6 || lines[6].Trim().ToLower() == "true";
                _autoCopyExtraTemplateOutput = lines.Length <= 7 || lines[7].Trim().ToLower() == "true";
                _replacePositivePromptOnClipboardText = lines.Length <= 8 || lines[8].Trim().ToLower() == "true";
                _useTagReplacerForMiniClip = lines.Length > 10 && lines[10].Trim().ToLower() == "true";
                _tagReplacerPrefix = lines.Length > 11 ? lines[11].Trim() : "PH_";
                _autoCopyTagReplacerOutput = lines.Length <= 13 || lines[13].Trim().ToLower() == "true";
                _autoSaveDelayEnabled = lines.Length > 14 && lines[14].Trim().ToLower() == "true";
                if (lines.Length > 15 && int.TryParse(lines[15].Trim(), out int delaySec)) _autoSaveDelaySeconds = delaySec;

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

        public void ImportBrowserImage(string filePath, int minWidth, int minHeight, bool replaceExisting = false)
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

                if (replaceExisting)
                    _capturedImages.Clear();

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
            _netTimer.Interval = TimeSpan.FromSeconds(2); // Reduced frequency from 1s to 2s
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
            // Run heavy network scan on background thread to avoid UI blocking
            Task.Run(() =>
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
                        // Update UI on UI thread
                        Dispatcher.InvokeAsync(() =>
                        {
                            TxtDownloadSpeed.Text = FormatSpeed(diffReceived);
                            TxtUploadSpeed.Text = FormatSpeed(diffSent);
                        });
                    }

                    _lastBytesReceived = currentReceived;
                    _lastBytesSent = currentSent;
                }
                catch { }
            });
        }

        private void GlobalHook_OnKeyPressed(System.Windows.Forms.Keys key)
        {
            if (key == System.Windows.Forms.Keys.ShiftKey || key == System.Windows.Forms.Keys.LShiftKey || key == System.Windows.Forms.Keys.RShiftKey)
            {
                Dispatcher.InvokeAsync(() =>
                {
                    if (!IsDisabled && !IsTitleLocked && !string.IsNullOrWhiteSpace(_lastSavedMainTitle))
                    {
                        TxtTitle.Text = _lastSavedMainTitle;
                        TxtTitle.IsEnabled = true;
                    }
                });
                return;
            }

            if (key == System.Windows.Forms.Keys.ControlKey || key == System.Windows.Forms.Keys.LControlKey || key == System.Windows.Forms.Keys.RControlKey)
            {
                if (_isBrowserQuickPasteEnabled && !_isBrowserQuickPasteRunning)
                {
                    Dispatcher.InvokeAsync(() => TriggerBrowserQuickPasteIfReady());
                }
                return;
            }

            bool ctrlPressed = (System.Windows.Forms.Control.ModifierKeys & System.Windows.Forms.Keys.Control) == System.Windows.Forms.Keys.Control;
            if (ctrlPressed)
            {
                if (key == System.Windows.Forms.Keys.E)
                {
                    Dispatcher.InvokeAsync(() => { if (!IsDisabled && BtnPlayRec.IsEnabled) BtnPlayRec_Click(null, null); });
                }
                else if (key == System.Windows.Forms.Keys.R)
                {
                    Dispatcher.InvokeAsync(() => { if (!IsDisabled) BtnSaveFromPersonaInjector_Click(null, null); });
                }
                else if (key == System.Windows.Forms.Keys.S)
                {
                    Dispatcher.InvokeAsync(() => { if (!IsDisabled && BtnSEO.IsEnabled) SaveDirectly(); });
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
            ExtraFloatBridge.ExtraTitleConfirmed -= OnExtraTitleConfirmed;
            _miniExtraPanel?.Close();
            _miniNegativePanel?.Close();
            _miniAutoSavePanel?.Close();
            _miniBaseCombinerPanel?.Close();
            _netTimer?.Stop();
            _globalHook?.Dispose();
            if (_autoImportWatcher != null) { _autoImportWatcher.EnableRaisingEvents = false; _autoImportWatcher.Dispose(); }
            if (_hwndSource != null)
            {
                var helper = new WindowInteropHelper(this);
                RemoveClipboardFormatListener(helper.Handle);
                _hwndSource.RemoveHook(WndProc);
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_CLIPBOARDUPDATE)
            {
                OnClipboardChanged();
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void OnClipboardChanged()
        {
            try
            {
                if (IsDisabled) return;
                if (_ignoreNextClipboardChange) { _ignoreNextClipboardChange = false; return; }

                bool hasText = SafeClipboardContainsText();
                string rawText = "";

                if (hasText)
                {
                    rawText = SafeClipboardGetText();
                    if (rawText.EndsWith("\u200B"))
                    {
                        rawText = rawText.TrimEnd('\u200B');
                    }
                    if (rawText == _lastClipboardText) return;
                    _lastClipboardText = rawText;
                }
                else
                {
                    _lastClipboardText = "";
                    if ((DateTime.Now - _lastClipboardTime).TotalMilliseconds < 100) return;
                    _lastClipboardTime = DateTime.Now;
                }

                if (IsAutoFillEnabled && ClipboardMetadata.IsValid())
                {
                    bool isCharValid = !string.IsNullOrWhiteSpace(ClipboardMetadata.CharacterName) &&
                                       !ClipboardMetadata.CharacterName.Equals("Unknown", StringComparison.OrdinalIgnoreCase);

                    bool isBaseValid = !string.IsNullOrWhiteSpace(ClipboardMetadata.BasePromptName) &&
                                       !ClipboardMetadata.BasePromptName.Equals("Unknown", StringComparison.OrdinalIgnoreCase);

                    if (ClipboardMetadata.PreserveMiniClipTitle)
                    {
                        if (isCharValid && !IsAdditionalTitleLocked)
                        {
                            IsAdditionalTitleEnabled = true;
                            AdditionalTitle = ClipboardMetadata.CharacterName;
                        }
                    }
                    else
                    {
                        if (isCharValid && !IsTitleLocked)
                        {
                            TxtTitle.Text = ClipboardMetadata.CharacterName;
                        }

                        if (isBaseValid && !IsAdditionalTitleLocked)
                        {
                            IsAdditionalTitleEnabled = true;
                            AdditionalTitle = ClipboardMetadata.BasePromptName;
                        }
                    }
                    ClipboardMetadata.Clear();
                }

                if (SafeClipboardContainsImage())
                {
                    var image = SafeClipboardGetImage();
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
                else if (hasText)
                {
                    if (_ignoreNextSpiSyncClipboardText)
                    {
                        _ignoreNextSpiSyncClipboardText = false;
                        return;
                    }

                    string tagPrefix = _tagReplacerPrefix;
                    if (string.IsNullOrEmpty(tagPrefix)) tagPrefix = "PH_";
                    var tagPattern = $@"\[{Regex.Escape(tagPrefix)}\d+\]";
                    bool isPhTemplate = Regex.IsMatch(rawText, tagPattern, RegexOptions.IgnoreCase);
                    bool isExtraTemplate = rawText.Contains(MiniExtraPlaceholderTag, StringComparison.OrdinalIgnoreCase);

                    bool shouldCapture = false;
                    bool shouldAutoCopy = false;

                    if (isPhTemplate)
                    {
                        if (_autoCopyTagReplacerOutput)
                        {
                            shouldCapture = true;
                            shouldAutoCopy = true;
                            _useTagReplacerForMiniClip = true;
                        }
                    }
                    else if (isExtraTemplate)
                    {
                        if (_autoCaptureExtraTemplate)
                        {
                            shouldCapture = true;
                            _useTagReplacerForMiniClip = false;
                            shouldAutoCopy = _autoCopyExtraTemplateOutput;
                        }
                    }

                    if (shouldCapture)
                    {
                        _miniExtraTemplate = rawText;
                        _basePositivePrompt = rawText;
                        SetMiniExtraButtonState(true);

                        if (shouldAutoCopy)
                            _ = AutoCopyExtraTemplateOutputAsync();
                        return;
                    }

                    int englishLetterCountTemp = Regex.Matches(rawText, "[A-Za-z]").Count;
                    if (Regex.IsMatch(rawText, @"[\u0600-\u06FF]") && englishLetterCountTemp < 5)
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
                        if (Regex.IsMatch(text.Trim(), @"^\d{4,}")) return;

                        bool wasCombined = false;
                        bool isCombinerEnabled = false;

                        // Apply Smart Prompt Combiner if enabled
                        try
                        {
                            var combinerData = PromptCombinerStore.Load();
                            if (combinerData != null && combinerData.IsEnabled)
                            {
                                isCombinerEnabled = true;
                                var activeItems = combinerData.Items
                                    .Where(i => combinerData.ActiveItemIds != null && combinerData.ActiveItemIds.Contains(i.Id))
                                    .Select(i => i.Text)
                                    .Where(t => !string.IsNullOrWhiteSpace(t))
                                    .ToList();

                                var customTexts = combinerData.Folders
                                    .Where(f => f.IsCustomInput && f.IsCustomInputActive && !string.IsNullOrWhiteSpace(f.CustomInputText))
                                    .Select(f => f.CustomInputText.Trim())
                                    .ToList();

                                if (activeItems.Count > 0 || customTexts.Count > 0)
                                {
                                    string combined;
                                    if (combinerData.PlacementMode == CombinerPlacementMode.PerFolder)
                                    {
                                        combined = PromptCombinerEngine.CombinePerFolder(text, combinerData);
                                    }
                                    else
                                    {
                                        var allSnippetTexts = new List<string>(activeItems);
                                        allSnippetTexts.AddRange(customTexts);
                                        combined = PromptCombinerEngine.Combine(text, allSnippetTexts, combinerData.PlacementMode, combinerData.CommaIndex, combinerData.Separator);
                                    }

                                    if (combined != text)
                                    {
                                        wasCombined = true;
                                        if (combinerData.AutoCaptureBasePrompt)
                                        {
                                            try
                                            {
                                                string configPath = DataPathManager.GetSettingsFilePath("base_combiner_config.json");
                                                string dir = System.IO.Path.GetDirectoryName(configPath);
                                                if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                                                    System.IO.Directory.CreateDirectory(dir);
                                                System.IO.File.WriteAllText(configPath, text);
                                                _miniBaseCombinerPanel?.UpdateBasePromptText(text);
                                            }
                                            catch { }
                                        }

                                        text = combined;
                                        SetClipboardTextIgnoringNextChange(combined + "\u200B");
                                        try
                                        {
                                            foreach (System.Windows.Window win in System.Windows.Application.Current.Windows)
                                            {
                                                if (win is BrowserWindow bw)
                                                {
                                                    bw.FlashCombinerSuccess();
                                                }
                                            }
                                        }
                                        catch { }
                                    }

                                    if (!IsAdditionalTitleLocked)
                                    {
                                        var activeTitles = new List<string>();
                                        foreach (var item in combinerData.Items.Where(i => combinerData.ActiveItemIds != null && combinerData.ActiveItemIds.Contains(i.Id)))
                                        {
                                            string title = !string.IsNullOrWhiteSpace(item.Title) ? item.Title.Trim() : item.Text?.Trim();
                                            if (!string.IsNullOrWhiteSpace(title) && !activeTitles.Contains(title)) activeTitles.Add(title);
                                        }
                                        foreach (var folder in combinerData.Folders.Where(f => f.IsCustomInput && f.IsCustomInputActive && !string.IsNullOrWhiteSpace(f.CustomInputText)))
                                        {
                                            string title = !string.IsNullOrWhiteSpace(folder.CustomTitle) ? folder.CustomTitle.Trim() : "متن سفارشی";
                                            if (!activeTitles.Contains(title)) activeTitles.Add(title);
                                        }
                                        if (activeTitles.Count > 0)
                                        {
                                            IsAdditionalTitleVisible = true;
                                            IsAdditionalTitleEnabled = true;
                                            AdditionalTitle = string.Join(" - ", activeTitles);
                                        }
                                    }
                                }
                            }
                        }
                        catch { }

                        bool isReplacing = _hasPositivePrompt;

                        if (wasCombined || isCombinerEnabled)
                        {
                            CursorBadgeNotification.ShowCombiner("⚡ Combined!");
                        }
                        else if (isReplacing)
                        {
                            CursorBadgeNotification.ShowReplaced("🔄 Replaced!");
                        }
                        else
                        {
                            CursorBadgeNotification.ShowCopied("📋 Copied!");
                        }

                        if (text == _positivePrompt || text == _negativePrompt) return;
                        if (!_replacePositivePromptOnClipboardText && _hasPositivePrompt) return;

                        if (!_hasPositivePrompt)
                        {
                            _basePositivePrompt = text; _positivePrompt = text; _hasPositivePrompt = true; UpdatePositivePromptToolTip();
                            TxtPositiveCheck.Text = "✓"; TxtPositiveCheck.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#89D185"));
                        }
                        else if (!_hasNegativePrompt && !IsNegativeLocked)
                        {
                            NegativePrompt = text; _hasNegativePrompt = true;
                            TxtNegativeCheck.Text = "✓"; TxtNegativeCheck.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#89D185"));
                            IsNegativeLocked = true; // Auto-lock after receiving
                        }
                        else if (!IsNegativeLocked)
                        {
                            _basePositivePrompt = _negativePrompt; _positivePrompt = _negativePrompt; NegativePrompt = text; UpdatePositivePromptToolTip();
                            TxtPositiveCheck.Text = "✓"; TxtNegativeCheck.Text = "✓";
                            IsNegativeLocked = true; // Auto-lock after receiving
                        }
                        else if (IsNegativeLocked)
                        {
                            _basePositivePrompt = text; _positivePrompt = text; _hasPositivePrompt = true; UpdatePositivePromptToolTip();
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
            UpdateTitleWatermarkHint();
            BtnSEO.IsEnabled = _capturedImages.Count > 0 && _hasPositivePrompt && _hasNegativePrompt;
            if (_hasPositivePrompt && _hasNegativePrompt && !IsCompactMode)
            {
                try
                {
                    var settings = BrowserSettings.Load();
                    if (settings.AutoFocusMiniClip)
                    {
                        this.Activate();
                        TxtTitle.Focus();
                    }
                }
                catch
                {
                    this.Activate();
                    TxtTitle.Focus();
                }
            }
        }

        private void CheckAutoSaveTrigger()
        {
            if (_autoSaveEnabled &&
                _capturedImages.Count >= _autoSaveThreshold &&
                _hasPositivePrompt &&
                _hasNegativePrompt &&
                !string.IsNullOrWhiteSpace(TxtTitle.Text))
            {
                if (_autoSaveDelayEnabled && _autoSaveDelaySeconds > 0)
                {
                    StartAutoSaveCountdown();
                }
                else
                {
                    SaveDirectly();
                }
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

        private void UpdateTitleWatermarkHint()
        {
            if (TxtTitle == null) return;
            if (!string.IsNullOrEmpty(MiniClipHistory.LastSavedTitle))
            {
                TxtTitle.Tag = $"Last: {MiniClipHistory.LastSavedTitle}";
            }
            else
            {
                TxtTitle.Tag = "Enter Title...";
            }
        }

        private void TxtTitle_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (IsAdditionalTitleEnabled && string.IsNullOrWhiteSpace(TxtAdditionalTitle.Text)) { TxtAdditionalTitle.Focus(); return; }
                if (BtnSEO.IsEnabled && !string.IsNullOrWhiteSpace(TxtTitle.Text)) SaveDirectly();
            }
            else if (e.Key == Key.Up || ((e.Key == Key.LeftShift || e.Key == Key.RightShift) && string.IsNullOrEmpty(TxtTitle.Text)))
            {
                if (!string.IsNullOrEmpty(MiniClipHistory.LastSavedTitle))
                {
                    TxtTitle.Text = MiniClipHistory.LastSavedTitle;
                    TxtTitle.SelectionStart = TxtTitle.Text.Length;
                    UpdateState();
                    CheckAutoSaveTrigger();
                    e.Handled = true;
                }
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
            _hasPositivePrompt = false; _positivePrompt = ""; _basePositivePrompt = ""; TxtPositiveCheck.Text = "○";
            TxtPositiveCheck.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#969696"));
            _lastClipboardText = "";
            UpdatePositivePromptToolTip();
            UpdateState();
        }

        private void NegativePrompt_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (IsNegativeLocked) return;
            _hasNegativePrompt = false; NegativePrompt = ""; TxtNegativeCheck.Text = "○";
            TxtNegativeCheck.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#969696"));
            _lastClipboardText = "";
            UpdateState();
        }

        private bool _isCountdownPaused = false;

        private void StartAutoSaveCountdown()
        {
            if (_autoSaveCountdownTimer == null)
            {
                _autoSaveCountdownTimer = new DispatcherTimer();
                _autoSaveCountdownTimer.Interval = TimeSpan.FromSeconds(1);
                _autoSaveCountdownTimer.Tick += AutoSaveCountdownTimer_Tick;
            }

            _autoSaveRemainingSeconds = _autoSaveDelaySeconds;

            if (BtnPauseCountdown != null)
            {
                BtnPauseCountdown.IsEnabled = true;
            }

            if (_isCountdownPaused)
            {
                _autoSaveCountdownTimer.Stop();
                if (BtnPauseCountdown != null)
                {
                    BtnPauseCountdown.Content = "▶";
                }
            }
            else
            {
                if (BtnPauseCountdown != null)
                {
                    BtnPauseCountdown.Content = "⏸";
                }
                _autoSaveCountdownTimer.Start();
            }

            UpdateButtonCountdownText();
        }

        private void AutoSaveCountdownTimer_Tick(object? sender, EventArgs e)
        {
            _autoSaveRemainingSeconds--;
            if (_autoSaveRemainingSeconds <= 0)
            {
                ResetCountdownButtonText();
                SaveDirectly();
            }
            else
            {
                UpdateButtonCountdownText();
            }
        }

        private void UpdateButtonCountdownText()
        {
            BtnSEO.Content = $"Save ({_autoSaveRemainingSeconds}s)";
        }

        private void ResetCountdownButtonText()
        {
            if (_autoSaveCountdownTimer != null)
            {
                _autoSaveCountdownTimer.Stop();
                _autoSaveCountdownTimer = null;
            }
            BtnSEO.Content = "Save";
            _isCountdownPaused = false;
            if (BtnPauseCountdown != null)
            {
                BtnPauseCountdown.Content = "⏸";
                BtnPauseCountdown.IsEnabled = false;
            }
        }

        private void BtnPauseCountdown_Click(object sender, RoutedEventArgs e)
        {
            if (_autoSaveCountdownTimer == null) return;

            if (_isCountdownPaused)
            {
                _autoSaveCountdownTimer.Start();
                _isCountdownPaused = false;
                BtnPauseCountdown.Content = "⏸";
            }
            else
            {
                _autoSaveCountdownTimer.Stop();
                _isCountdownPaused = true;
                BtnPauseCountdown.Content = "▶";
            }
        }

        private void SaveDirectly()
        {
            ResetCountdownButtonText();
            if (_isSaving) return;
            string savePath = "";
            foreach (Window w in System.Windows.Application.Current.Windows) if (w is MainWindow mw) savePath = mw.SavePath;
            if (string.IsNullOrEmpty(savePath) || !Directory.Exists(savePath)) { System.Windows.MessageBox.Show("Invalid Save Path"); return; }

            string mainTitle = TxtTitle.Text.Trim();
            if (!string.IsNullOrEmpty(mainTitle))
            {
                _lastSavedMainTitle = mainTitle;
            }
            string title = mainTitle;
            if (IsAdditionalTitleEnabled && !string.IsNullOrWhiteSpace(AdditionalTitle)) title += " " + AdditionalTitle.Trim();
            if (string.IsNullOrEmpty(title)) { if (!IsCompactMode) TxtTitle.Focus(); return; }

            string safeTitle = SanitizeFileName(title);
            if (string.IsNullOrWhiteSpace(safeTitle))
            {
                System.Windows.MessageBox.Show("Title contains only invalid filename characters.");
                return;
            }

            _isSaving = true;
            try
            {
                for (int i = 0; i < _capturedImages.Count; i++)
                {
                    var item = _capturedImages[i];
                    string ext = item.OriginalPath != null ? Path.GetExtension(item.OriginalPath) : ".png";
                    string currentTitle = _capturedImages.Count > 1 ? $"{safeTitle} ({i + 1})" : safeTitle;
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
                    string basePromptCandidate = !string.IsNullOrWhiteSpace(_basePositivePrompt) ? _basePositivePrompt.Trim() : (!string.IsNullOrWhiteSpace(_miniExtraTemplate) ? _miniExtraTemplate.Trim() : "");
                    string txtContent = $"Positive Prompt:\n{_positivePrompt}";
                    if (!string.IsNullOrWhiteSpace(basePromptCandidate) && basePromptCandidate != _positivePrompt.Trim())
                    {
                        txtContent += $"\n\nBase Prompt:\n{basePromptCandidate}";
                    }
                    txtContent += $"\n\nNegative Prompt:\n{_negativePrompt}";
                    if (IsDescriptionEnabled && !string.IsNullOrWhiteSpace(DescriptionText))
                    {
                        txtContent += $"\n\nDescription:\n{DescriptionText.Trim()}";
                    }
                    File.WriteAllText(txtPath, txtContent);
                }
                FlashSuccess();
                if (!string.IsNullOrWhiteSpace(title))
                {
                    MiniClipHistory.LastSavedTitle = title;
                }
                if (IsSaveBasePromptEnabled && !string.IsNullOrWhiteSpace(_positivePrompt))
                {
                    BasePromptManager.Add(new BasePrompt { Name = !string.IsNullOrWhiteSpace(AdditionalTitle) ? AdditionalTitle.Trim() : "BP", PromptText = _positivePrompt.Trim() });
                }
                ResetState();
                UpdateTitleWatermarkHint();
            }
            catch (Exception ex) { System.Windows.MessageBox.Show(ex.Message); }
            finally { _isSaving = false; }
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";

            string invalid = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
            var cleaned = new string(value.Where(ch => !invalid.Contains(ch)).ToArray());
            cleaned = cleaned.Trim().TrimEnd('.');
            cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
            return cleaned;
        }

        private void FlashSuccess()
        {
            var anim = new DoubleAnimation(1.0, 0.4, TimeSpan.FromMilliseconds(200)) { AutoReverse = true };
            MainBorder.BeginAnimation(UIElement.OpacityProperty, anim);
        }

        private void ResetState()
        {
            ResetCountdownButtonText();
            _hasImage = false; _hasPositivePrompt = false; _capturedImages.Clear(); _positivePrompt = ""; _basePositivePrompt = "";
            UpdatePositivePromptToolTip();
            if (!IsNegativeLocked) { _hasNegativePrompt = false; NegativePrompt = ""; TxtNegativeCheck.Text = "○"; TxtNegativeCheck.Foreground = System.Windows.Media.Brushes.Gray; }
            UpdateImagePreviews();
            TxtPositiveCheck.Text = "○"; TxtPositiveCheck.Foreground = System.Windows.Media.Brushes.Gray;
            if (!IsTitleLocked) TxtTitle.Text = "";
            if (!IsAdditionalTitleLocked) { AdditionalTitle = ""; IsAdditionalTitleEnabled = false; }
            if (!IsDescriptionLocked) DescriptionText = "";
            BtnSEO.IsEnabled = false;
            _lastClipboardText = "";
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) { if (e.ClickCount == 1) DragMove(); }
        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
        private void BtnLockNegative_Click(object sender, RoutedEventArgs e)
        {
            IsNegativeLocked = !IsNegativeLocked;
            SaveNegativePromptState();
        }
        private void BtnLockAdditionalTitle_Click(object sender, RoutedEventArgs e) => IsAdditionalTitleLocked = !IsAdditionalTitleLocked;
        private void BtnLockTitle_Click(object sender, RoutedEventArgs e) => IsTitleLocked = !IsTitleLocked;
        private void BtnToggleMenu_Click(object sender, RoutedEventArgs e) => IsExtraMenuOpen = !IsExtraMenuOpen;
        private void BtnLockDescription_Click(object sender, RoutedEventArgs e) => IsDescriptionLocked = !IsDescriptionLocked;
        private void BtnExtraMenuPageOne_Click(object sender, RoutedEventArgs e) => ExtraMenuPage = 0;
        private void BtnExtraMenuPageTwo_Click(object sender, RoutedEventArgs e) => ExtraMenuPage = 1;
        private void BtnExtraMenuPageThree_Click(object sender, RoutedEventArgs e) => ExtraMenuPage = 2;
        private void BtnExtraMenuPageFour_Click(object sender, RoutedEventArgs e) => ExtraMenuPage = 3;
        private bool TrySetMiniExtraTemplate(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;

            string prefix = _tagReplacerPrefix;
            if (string.IsNullOrEmpty(prefix)) prefix = "PH_";
            var tagPattern = $@"\[{Regex.Escape(prefix)}\d+\]";
            bool hasPhTags = Regex.IsMatch(text, tagPattern, RegexOptions.IgnoreCase);
            bool hasExtraTag = text.Contains(MiniExtraPlaceholderTag, StringComparison.OrdinalIgnoreCase);

            if (!hasPhTags && !hasExtraTag) return false;

            if (hasPhTags)
            {
                _useTagReplacerForMiniClip = true;
            }
            else
            {
                _useTagReplacerForMiniClip = false;
            }

            _miniExtraTemplate = text;
            if (string.IsNullOrWhiteSpace(_basePositivePrompt) || _basePositivePrompt == _positivePrompt)
            {
                _basePositivePrompt = text;
            }
            SetMiniExtraButtonState(true);
            return true;
        }

        private void BtnCopyExtraTemplateOutput_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!TryBuildExtraTemplateOutput(out var output, out var errorMessage))
                {
                    CustomMessageBox.Show(errorMessage, "Extra Template", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                SetClipboardTextIgnoringNextChange(output);
                SetMiniExtraButtonState(true);
                TriggerExtraOrTagNotification();
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(ex.Message, "Extra Template", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnManualExtraCopy_Click(object sender, RoutedEventArgs e)
        {
            if (_isManualExtraCopyRunning) return;

            _isManualExtraCopyRunning = true;
            BtnManualExtraCopy.IsEnabled = false;
            SetManualExtraCopyStatus(true);

            try
            {
                await Task.Delay(80);
                if (System.Windows.Clipboard.ContainsText())
                    TrySetMiniExtraTemplate(System.Windows.Clipboard.GetText());

                if (!TryBuildExtraTemplateOutput(out var output, out var errorMessage))
                {
                    CustomMessageBox.Show(errorMessage, "Manual EX", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                SetClipboardTextIgnoringNextChange(output);
                TryApplyAutoExtraOutputToPositivePrompt(output);
                SetMiniExtraButtonState(true);
                TriggerExtraOrTagNotification();

                if (LastExtraSelectionStore.TryGetSelection(out var lastSel, out _) && lastSel != null && !string.IsNullOrWhiteSpace(lastSel.ShortName))
                {
                    TxtTitle.Text = lastSel.ShortName;
                    TxtTitle.IsEnabled = true;
                    TxtTitle.SelectionStart = TxtTitle.Text.Length;
                    UpdateState();
                    CheckAutoSaveTrigger();
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show(ex.Message, "Manual EX", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                await Task.Delay(180);
                SetManualExtraCopyStatus(false);
                BtnManualExtraCopy.IsEnabled = true;
                _isManualExtraCopyRunning = false;
            }
        }

        private bool TryBuildExtraTemplateOutput(out string output, out string errorMessage)
        {
            output = "";
            errorMessage = "";

            if (string.IsNullOrWhiteSpace(_miniExtraTemplate))
            {
                if (_useTagReplacerForMiniClip)
                    errorMessage = $"First capture a template that contains placeholder tags (e.g. [{_tagReplacerPrefix}1]).";
                else
                    errorMessage = "First capture a clipboard template that contains [extra].";
                return false;
            }

            string extraText = "";
            if (_useTagReplacerForMiniClip)
            {
                if (PromptTaggerStore.UseManualValuesMode)
                {
                    extraText = PromptTaggerStore.ManualValues;
                }
                else
                {
                    TryGetLatestExtraText(out extraText, out errorMessage);
                }

                // Fallback to general Tagger store values if empty
                if (string.IsNullOrWhiteSpace(extraText))
                {
                    extraText = PromptTaggerStore.Values;
                }

                if (string.IsNullOrWhiteSpace(extraText))
                {
                    errorMessage = "Prompt Tagger values list is empty. Please enter manual values or select items from the list first.";
                    return false;
                }
            }
            else
            {
                if (!TryGetLatestExtraText(out extraText, out errorMessage))
                    return false;
            }

            if (_useTagReplacerForMiniClip)
            {
                string prefix = _tagReplacerPrefix;
                if (string.IsNullOrEmpty(prefix)) prefix = "PH_";

                var values = extraText.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                      .Select(v => v.Trim())
                                      .Where(v => !string.IsNullOrEmpty(v))
                                      .ToList();

                var tagPattern = $@"\[{Regex.Escape(prefix)}\d+\]";
                var regex = new Regex(tagPattern, RegexOptions.IgnoreCase);

                int valIndex = 0;
                output = regex.Replace(_miniExtraTemplate, m =>
                {
                    if (valIndex < values.Count)
                    {
                        string replacement = values[valIndex];
                        valIndex++;
                        return replacement;
                    }
                    return m.Value; // leave unreplaced if no values left
                });
            }
            else
            {
                output = _miniExtraTemplate.Replace(MiniExtraPlaceholderTag, extraText, StringComparison.OrdinalIgnoreCase);
            }
            return true;
        }

        private async Task AutoCopyExtraTemplateOutputAsync()
        {
            SetAutoExtraCopyStatus(true);
            try
            {
                await Task.Delay(80);
                if (TryBuildExtraTemplateOutput(out var output, out _))
                {
                    SetClipboardTextIgnoringNextChange(output);
                    TryApplyAutoExtraOutputToPositivePrompt(output);
                    SetMiniExtraButtonState(true);
                    TriggerExtraOrTagNotification();
                }
            }
            finally
            {
                await Task.Delay(180);
                SetAutoExtraCopyStatus(false);
            }
        }

        private void SetClipboardTextIgnoringNextChange(string text)
        {
            _ignoreNextClipboardChange = true;
            _lastClipboardText = text;
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    System.Windows.Clipboard.SetText(text);
                    return;
                }
                catch
                {
                    System.Threading.Thread.Sleep(40);
                }
            }
        }

        private bool SafeClipboardContainsText()
        {
            for (int i = 0; i < 8; i++)
            {
                try { return System.Windows.Clipboard.ContainsText(); }
                catch { System.Threading.Thread.Sleep(30); }
            }
            return false;
        }

        private string SafeClipboardGetText()
        {
            for (int i = 0; i < 8; i++)
            {
                try { return System.Windows.Clipboard.GetText(); }
                catch { System.Threading.Thread.Sleep(30); }
            }
            return string.Empty;
        }

        private bool SafeClipboardContainsImage()
        {
            for (int i = 0; i < 8; i++)
            {
                try { return System.Windows.Clipboard.ContainsImage(); }
                catch { System.Threading.Thread.Sleep(30); }
            }
            return false;
        }

        private System.Windows.Media.Imaging.BitmapSource? SafeClipboardGetImage()
        {
            for (int i = 0; i < 8; i++)
            {
                try { return System.Windows.Clipboard.GetImage(); }
                catch { System.Threading.Thread.Sleep(30); }
            }
            return null;
        }

        private bool TryApplyAutoExtraOutputToPositivePrompt(string rawText)
        {
            string text = FilterEnglishOnly(rawText);
            if (string.IsNullOrWhiteSpace(text)) return false;

            int englishLetterCount = Regex.Matches(rawText, "[A-Za-z]").Count;
            if (englishLetterCount > 0 && englishLetterCount < 5) return false;
            if (text == _positivePrompt || text == _negativePrompt) return false;
            if (!_replacePositivePromptOnClipboardText && _hasPositivePrompt) return false;

            if (string.IsNullOrWhiteSpace(_basePositivePrompt))
            {
                if (!string.IsNullOrWhiteSpace(_miniExtraTemplate))
                    _basePositivePrompt = _miniExtraTemplate;
                else if (!string.IsNullOrWhiteSpace(_positivePrompt))
                    _basePositivePrompt = _positivePrompt;
                else
                    _basePositivePrompt = text;
            }

            _positivePrompt = text;
            _hasPositivePrompt = true;
            UpdatePositivePromptToolTip();
            TxtPositiveCheck.Text = "\u2713";
            TxtPositiveCheck.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#89D185"));
            UpdateState();
            CheckAutoSaveTrigger();
            return true;
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
            if (BtnAutoExtraCopyStatus?.Template.FindName("txt", BtnAutoExtraCopyStatus) is TextBlock captureText)
                captureText.Foreground = hasTemplate ? (System.Windows.Media.Brush)FindResource("SuccessBrush") : (System.Windows.Media.Brush)FindResource("ForegroundMutedBrush");

            if (BtnCopyExtraTemplateOutput?.Template.FindName("txt", BtnCopyExtraTemplateOutput) is TextBlock copyText)
                copyText.Foreground = hasTemplate ? (System.Windows.Media.Brush)FindResource("SuccessBrush") : (System.Windows.Media.Brush)FindResource("ForegroundMutedBrush");

            if (BtnManualExtraCopy?.Template.FindName("txt", BtnManualExtraCopy) is TextBlock manualText)
                manualText.Foreground = hasTemplate ? (System.Windows.Media.Brush)FindResource("SuccessBrush") : (System.Windows.Media.Brush)FindResource("ForegroundMutedBrush");

            if (BtnAutoExtraCopyStatus?.Template.FindName("bd", BtnAutoExtraCopyStatus) is Border captureBorder)
                captureBorder.BorderBrush = hasTemplate ? (System.Windows.Media.Brush)FindResource("SuccessBrush") : (System.Windows.Media.Brush)FindResource("BorderBrush");

            if (BtnCopyExtraTemplateOutput?.Template.FindName("bd", BtnCopyExtraTemplateOutput) is Border copyBorder)
                copyBorder.BorderBrush = hasTemplate ? (System.Windows.Media.Brush)FindResource("SuccessBrush") : (System.Windows.Media.Brush)FindResource("BorderBrush");

            if (BtnManualExtraCopy?.Template.FindName("bd", BtnManualExtraCopy) is Border manualBorder)
                manualBorder.BorderBrush = hasTemplate ? (System.Windows.Media.Brush)FindResource("SuccessBrush") : (System.Windows.Media.Brush)FindResource("BorderBrush");
        }

        private void SetAutoExtraCopyStatus(bool isRunning)
        {
            if (BtnAutoExtraCopyStatus?.Template.FindName("bd", BtnAutoExtraCopyStatus) is Border border)
                border.BorderBrush = isRunning
                    ? new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFD700"))
                    : (System.Windows.Media.Brush)FindResource("BorderBrush");

            if (BtnAutoExtraCopyStatus?.Template.FindName("txt", BtnAutoExtraCopyStatus) is TextBlock text)
                text.Foreground = isRunning
                    ? new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFD700"))
                    : (System.Windows.Media.Brush)FindResource("ForegroundMutedBrush");
        }

        private void SetManualExtraCopyStatus(bool isRunning)
        {
            if (BtnManualExtraCopy?.Template.FindName("bd", BtnManualExtraCopy) is Border border)
                border.BorderBrush = isRunning
                    ? new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFD700"))
                    : (System.Windows.Media.Brush)FindResource("BorderBrush");

            if (BtnManualExtraCopy?.Template.FindName("txt", BtnManualExtraCopy) is TextBlock text)
                text.Foreground = isRunning
                    ? new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFD700"))
                    : (System.Windows.Media.Brush)FindResource("ForegroundMutedBrush");
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e) => ResetState();
        private void BtnSaveBasePrompt_Click(object sender, RoutedEventArgs e) => IsSaveBasePromptEnabled = !IsSaveBasePromptEnabled;
        private void BtnAutoFill_Click(object sender, RoutedEventArgs e) => IsAutoFillEnabled = !IsAutoFillEnabled;
        private void BtnBrowserQuickPaste_Click(object sender, RoutedEventArgs e) => SetBrowserQuickPasteEnabled(!_isBrowserQuickPasteEnabled);

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
            await PlayMiniRecordingAsync();
        }

        public async Task<bool> PlayMiniRecordingAsync()
        {
            if (IsDisabled || !BtnPlayRec.IsEnabled || _playerRec.IsPlaying)
                return false;

            RecordingManager.LoadState();
            int slotToPlay = RecordingManager.SequentialMode ? _nextMiniSlot : RecordingManager.SelectedSlot;
            if (!RecordingManager.HasEvents(slotToPlay))
            {
                int other = (slotToPlay == 1) ? 2 : 1;
                if (RecordingManager.HasEvents(other)) slotToPlay = other;
                else return false;
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
            return true;
        }

        private void BtnSaveFromPersonaInjector_Click(object sender, RoutedEventArgs e)
        {
            PersonaInjectorWindow injector = null;
            foreach (Window w in System.Windows.Application.Current.Windows) if (w is PersonaInjectorWindow piw) injector = piw;
            if (injector == null) return;
            bool preserveMiniClipTitle = IsSpiSyncPreserveBasePromptEnabled();
            if (preserveMiniClipTitle) _ignoreNextSpiSyncClipboardText = true;
            injector.PerformRandomForCurrentTab();
            System.Threading.Tasks.Task.Delay(150).ContinueWith(_ => Dispatcher.Invoke(() => {
                if (ClipboardMetadata.IsValid())
                {
                    bool isCharValid = !string.IsNullOrWhiteSpace(ClipboardMetadata.CharacterName) &&
                                       !ClipboardMetadata.CharacterName.Equals("Unknown", StringComparison.OrdinalIgnoreCase);

                    bool isBaseValid = !string.IsNullOrWhiteSpace(ClipboardMetadata.BasePromptName) &&
                                       !ClipboardMetadata.BasePromptName.Equals("Unknown", StringComparison.OrdinalIgnoreCase);

                    if (ClipboardMetadata.PreserveMiniClipTitle)
                    {
                        if (isCharValid && !IsAdditionalTitleLocked)
                        {
                            IsAdditionalTitleEnabled = true;
                            AdditionalTitle = ClipboardMetadata.CharacterName;
                        }
                    }
                    else
                    {
                        if (isCharValid && !IsTitleLocked)
                        {
                            TxtTitle.Text = ClipboardMetadata.CharacterName;
                        }

                        if (isBaseValid && !IsAdditionalTitleLocked)
                        {
                            IsAdditionalTitleEnabled = true;
                            AdditionalTitle = ClipboardMetadata.BasePromptName;
                        }
                    }
                    ClipboardMetadata.Clear();
                    try
                    {
                        var settings = BrowserSettings.Load();
                        if (settings.AutoFocusMiniClip)
                        {
                            this.Activate();
                            if (!IsCompactMode) TxtTitle.Focus();
                        }
                    }
                    catch
                    {
                        this.Activate();
                        if (!IsCompactMode) TxtTitle.Focus();
                    }
                    CheckAutoSaveTrigger();
                }
            }));
        }

        private bool IsSpiSyncPreserveBasePromptEnabled()
        {
            try
            {
                string configPath = DataPathManager.GetSettingsFilePath("config.txt");
                if (!File.Exists(configPath)) return false;
                string[] lines = File.ReadAllLines(configPath);
                return lines.Length > 9 && lines[9].Trim().ToLower() == "true";
            }
            catch { }
            return false;
        }

        private string FilterEnglishOnly(string input) => string.IsNullOrEmpty(input) ? "" : Regex.Replace(input, @"[^\u0000-\u007F]+", "");
        private void AdditionalTitle_KeyDown(object sender, System.Windows.Input.KeyEventArgs e) { if (e.Key == Key.Enter && BtnSEO.IsEnabled) SaveDirectly(); }

        // ─── Mini Extra Panel (attached compact panel) ───────────────────

        private MiniExtraPanel? _miniExtraPanel;

        private void BtnToggleSidePanel_Click(object sender, RoutedEventArgs e)
        {
            if (_miniExtraPanel != null && _miniExtraPanel.IsVisible)
            {
                _miniExtraPanel.Hide();
                return;
            }

            _miniNegativePanel?.Hide();
            _miniAutoSavePanel?.Hide();
            _miniBaseCombinerPanel?.Hide();

            if (_miniExtraPanel == null || !_miniExtraPanel.IsLoaded)
            {
                _miniExtraPanel = new MiniExtraPanel();
                _miniExtraPanel.Closed += (s, ev) => _miniExtraPanel = null;
            }

            RepositionExtraPanel();
            _miniExtraPanel.Show();
            _miniExtraPanel.Activate();
        }

        private void RepositionExtraPanel() => RepositionAttachedPanel(_miniExtraPanel);

        // ─── Mini Negative Presets Panel (attached to left side) ─────────

        private MiniNegativePanel? _miniNegativePanel;

        private void BtnToggleNegativePanel_Click(object sender, RoutedEventArgs e)
        {
            if (_miniNegativePanel != null && _miniNegativePanel.IsVisible)
            {
                _miniNegativePanel.Hide();
                return;
            }

            _miniExtraPanel?.Hide();
            _miniAutoSavePanel?.Hide();
            _miniBaseCombinerPanel?.Hide();

            if (_miniNegativePanel == null || !_miniNegativePanel.IsLoaded)
            {
                _miniNegativePanel = new MiniNegativePanel(this);
                _miniNegativePanel.Closed += (s, ev) => _miniNegativePanel = null;
            }

            RepositionNegativePanel();
            _miniNegativePanel.Show();
            _miniNegativePanel.Activate();
        }

        private void BtnToggleDescriptionVisibility_Click(object sender, RoutedEventArgs e)
        {
            IsDescriptionVisible = !IsDescriptionVisible;
        }

        private void BtnToggleAdditionalTitleVisibility_Click(object sender, RoutedEventArgs e)
        {
            IsAdditionalTitleVisible = !IsAdditionalTitleVisible;
        }

        private MiniAutoSavePanel? _miniAutoSavePanel;

        private void BtnToggleAutoSavePanel_Click(object sender, RoutedEventArgs e)
        {
            if (_miniAutoSavePanel != null && _miniAutoSavePanel.IsVisible)
            {
                _miniAutoSavePanel.Hide();
                return;
            }

            _miniExtraPanel?.Hide();
            _miniNegativePanel?.Hide();
            _miniBaseCombinerPanel?.Hide();

            if (_miniAutoSavePanel == null || !_miniAutoSavePanel.IsLoaded)
            {
                _miniAutoSavePanel = new MiniAutoSavePanel(this);
                _miniAutoSavePanel.Closed += (s, ev) => _miniAutoSavePanel = null;
            }

            RepositionAutoSavePanel();
            _miniAutoSavePanel.Show();
            _miniAutoSavePanel.Activate();
        }

        private void RepositionAutoSavePanel() => RepositionAttachedPanel(_miniAutoSavePanel);
        private void RepositionNegativePanel() => RepositionAttachedPanel(_miniNegativePanel);
        private void RepositionBaseCombinerPanel() => RepositionAttachedPanel(_miniBaseCombinerPanel);

        private MiniBaseCombinerPanel? _miniBaseCombinerPanel;

        private void BtnToggleBaseCombinerPanel_Click(object sender, RoutedEventArgs e)
        {
            if (_miniBaseCombinerPanel != null && _miniBaseCombinerPanel.IsVisible)
            {
                _miniBaseCombinerPanel.Hide();
                return;
            }

            _miniExtraPanel?.Hide();
            _miniNegativePanel?.Hide();
            _miniAutoSavePanel?.Hide();

            if (_miniBaseCombinerPanel == null || !_miniBaseCombinerPanel.IsLoaded)
            {
                _miniBaseCombinerPanel = new MiniBaseCombinerPanel(this);
                _miniBaseCombinerPanel.Closed += (s, ev) => _miniBaseCombinerPanel = null;
            }

            _miniBaseCombinerPanel.UpdateCombinerSummary();
            RepositionBaseCombinerPanel();
            _miniBaseCombinerPanel.Show();
            _miniBaseCombinerPanel.Activate();
        }

        private void BtnFastRunBaseCombiner_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string configPath = DataPathManager.GetSettingsFilePath("base_combiner_config.json");
                string baseText = "";
                if (System.IO.File.Exists(configPath))
                {
                    baseText = System.IO.File.ReadAllText(configPath);
                }

                if (string.IsNullOrWhiteSpace(baseText))
                {
                    BtnToggleBaseCombinerPanel_Click(sender, e);
                    CursorBadgeNotification.ShowCombiner("⚠️ Set Base Prompt first!");
                    return;
                }

                var combinerData = PromptCombinerStore.Load();
                string combinedText = PromptCombinerEngine.CombinePerFolder(baseText, combinerData);

                string clipboardPayload = combinedText + "\u200B";
                System.Windows.Clipboard.SetText(clipboardPayload);

                CursorBadgeNotification.ShowCombiner("⚡ Combined Base Prompt!");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Error combining base prompt:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RepositionAttachedPanel(Window? panel)
        {
            if (panel == null) return;

            try
            {
                double panelWidth = !double.IsNaN(panel.Width) && panel.Width > 0 ? panel.Width : 230;
                double panelHeight = !double.IsNaN(panel.Height) && panel.Height > 0 ? panel.Height : 250;

                if (panel.SizeToContent != SizeToContent.Manual || double.IsNaN(panel.Height) || panel.Height <= 0)
                {
                    panel.Measure(new System.Windows.Size(double.PositiveInfinity, double.PositiveInfinity));
                    panelWidth = panel.DesiredSize.Width > 0 ? panel.DesiredSize.Width : panelWidth;
                    panelHeight = panel.DesiredSize.Height > 0 ? panel.DesiredSize.Height : panelHeight;
                }

                // Determine if there is enough space on the right side of MiniClipboardWindow
                double targetLeft;
                if (this.Left + this.ActualWidth + panelWidth + 10 <= SystemParameters.WorkArea.Right)
                {
                    // Place on the right
                    targetLeft = this.Left + this.ActualWidth + 4;
                }
                else
                {
                    // Place on the left
                    targetLeft = this.Left - panelWidth - 4;
                }

                // Adjust vertical position to stay within screen boundaries
                double targetTop = this.Top;

                // MiniNegativePanel has a 15px transparent margin for its drop shadow, adjust to align visually
                if (panel is MiniNegativePanel)
                {
                    targetLeft += (targetLeft > this.Left) ? -12 : 12; // Bring it closer horizontally
                    targetTop -= 15; // Align top visually
                }

                if (targetTop + panelHeight > SystemParameters.WorkArea.Bottom)
                {
                    targetTop = SystemParameters.WorkArea.Bottom - panelHeight - 10;
                }
                if (targetTop < SystemParameters.WorkArea.Top)
                {
                    targetTop = SystemParameters.WorkArea.Top + 10;
                }

                panel.Left = targetLeft;
                panel.Top = targetTop;
            }
            catch
            {
                panel.Left = this.Left + this.ActualWidth + 4;
                panel.Top = this.Top;
            }
        }
    }
}
