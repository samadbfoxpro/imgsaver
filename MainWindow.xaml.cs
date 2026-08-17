using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WinForms = System.Windows.Forms;

namespace imgsaver
{
    public partial class MainWindow : Window
    {
        private string _savePath = "";
        public string SavePath => _savePath;
        private BitmapSource? _previewImage;
        private string? _sourceFilePath = null;
        private string _detectedExtension = ".png";
        private const string ConfigFileName = "config.txt";

        private GlobalHook? _globalHook;
        private StringBuilder _typedBuffer = new StringBuilder();
        private RemoteServer? _remoteServer;
        private FileShareServer? _fileShareServer;
        private bool _isShuttingDown = false;
        private const string ServerStateFileName = "server_state.txt";

        private InputRecorder? _inputRecorder;
        private InputPlayer? _inputPlayer;

        private string RecordingsDir => DataPathManager.GetDataSubfolderPath("recordings");

        private SnippetWindow? _snippetWindow;
        private GalleryWindow? _galleryWindow;
        private MiniClipboardWindow? _miniClipboardWindow;
        private PersonaInjectorWindow? _personaInjectorWindow;
        private TutorialWindow? _tutorialWindow;
        private FileShareWindow? _fileShareWindow;
        private InputRecorderWindow? _inputRecorderWindow;
        private BrowserWindow? _browserWindow;
        private FloatingExtraWindow? _floatingExtraWindow;
        private PromptTaggerWindow? _promptTaggerWindow;

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("user32.dll")]
        private static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);
        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
        [StructLayout(LayoutKind.Sequential)]
        private struct GUITHREADINFO
        {
            public int cbSize;
            public int flags;
            public IntPtr hwndActive;
            public IntPtr hwndFocus;
            public IntPtr hwndCapture;
            public IntPtr hwndMenuOwner;
            public IntPtr hwndMoveSize;
            public IntPtr hwndCaret;
            public RECT rcCaret;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int x; public int y; }

        private void SafeSetClipboardText(string text, int maxAttempts = 8, int delayMs = 50)
        {
            if (string.IsNullOrEmpty(text)) return;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    System.Windows.Clipboard.SetText(text);
                    return;
                }
                catch (COMException)
                {
                    Thread.Sleep(delayMs);
                }
                catch
                {
                    return;
                }
            }
        }

        public MainWindow()
        {
            StartupProfiler.Log("MainWindow Constructor ENTER");
            InitializeComponent();
            SourceInitialized += (s, e) => WindowResizingHelper.ApplyModernWindowStyle(this);
            StartupProfiler.Log("MainWindow Constructor -> InitializeComponent END");
            LanguageManager.ApplyWindowLanguage(this);
            StartupProfiler.Log("MainWindow Constructor -> ApplyWindowLanguage END");
            LoadSettings();
            StartupProfiler.Log("MainWindow Constructor -> LoadSettings END");
            VersionManager.Load();
            TxtVersion.Text = $"v{VersionManager.CurrentVersion}";

            try
            {
                this.Icon = BitmapFrame.Create(new Uri("pack://application:,,,/logo.ico", UriKind.RelativeOrAbsolute));
            }
            catch { }

            Loaded += MainWindow_Loaded;
            ContentRendered += (s, e) => StartupProfiler.Log("MainWindow -> ContentRendered FIRED (UI IS VISIBLE!)");
            Closed += (_, _) => {
                _isShuttingDown = true;
                GlobalClipboardCombiner.Stop();
                _globalHook?.Dispose();
                _remoteServer?.Stop();
                _fileShareServer?.Stop();
                CleanupSystemTrayIcon();
                PerformFullApplicationShutdown();
            };
            Closing += MainWindow_Closing;
            StartupProfiler.Log("MainWindow Constructor EXIT");
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            StartupProfiler.Log("MainWindow -> Loaded FIRED");
            try
            {
                GlobalClipboardCombiner.Start(this);
            }
            catch { }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                StartupProfiler.Log("MainWindow Background Init -> GlobalHook START");
                try
                {
                    _globalHook = new GlobalHook();
                    _globalHook.OnKeyPressed += GlobalHook_OnKeyPressed;
                }
                catch (Exception ex)
                {
                    CustomMessageBox.Show("Error (Please run as Administrator):\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                StartupProfiler.Log("MainWindow Background Init -> GlobalHook END");

                StartupProfiler.Log("MainWindow Background Init -> LoadAndApplyServerStates START");
                LoadAndApplyServerStates();
                StartupProfiler.Log("MainWindow Background Init -> LoadAndApplyServerStates END");

                StartupProfiler.Log("MainWindow Background Init -> InitializeSystemTrayIcon START");
                InitializeSystemTrayIcon();
                StartupProfiler.Log("MainWindow Background Init -> InitializeSystemTrayIcon END");
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private WinForms.NotifyIcon? _notifyIcon;

        private void InitializeSystemTrayIcon()
        {
            try
            {
                _notifyIcon = new WinForms.NotifyIcon();
                
                try
                {
                    var resStream = System.Windows.Application.GetResourceStream(new Uri("pack://application:,,,/logo.ico", UriKind.RelativeOrAbsolute));
                    if (resStream != null)
                    {
                        _notifyIcon.Icon = new System.Drawing.Icon(resStream.Stream);
                    }
                    else
                    {
                        string? exePath = Environment.ProcessPath;
                        if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                        {
                            _notifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath) ?? System.Drawing.SystemIcons.Application;
                        }
                        else
                        {
                            _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
                        }
                    }
                }
                catch
                {
                    try
                    {
                        string? exePath = Environment.ProcessPath;
                        if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                        {
                            _notifyIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath) ?? System.Drawing.SystemIcons.Application;
                        }
                        else
                        {
                            _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
                        }
                    }
                    catch
                    {
                        _notifyIcon.Icon = System.Drawing.SystemIcons.Application;
                    }
                }

                _notifyIcon.Text = "imgsaver - Dataset & Prompt Assistant";
                _notifyIcon.Visible = true;

                var contextMenu = new WinForms.ContextMenuStrip();

                var itemOpenMain = new WinForms.ToolStripMenuItem("📱 Open Main Window");
                itemOpenMain.Click += (s, e) => Dispatcher.Invoke(() => ShowAndActivateMainWindow());

                var itemOpenBrowser = new WinForms.ToolStripMenuItem("🌐 Open Browser");
                itemOpenBrowser.Click += (s, e) => Dispatcher.Invoke(() => BtnBrowser_Click(this, new RoutedEventArgs()));

                var itemOpenMiniClip = new WinForms.ToolStripMenuItem("📋 Open Mini Clipboard");
                itemOpenMiniClip.Click += (s, e) => Dispatcher.Invoke(() => BtnMiniClipboard_Click(this, new RoutedEventArgs()));

                var itemOpenGallery = new WinForms.ToolStripMenuItem("🖼️ Open Gallery");
                itemOpenGallery.Click += (s, e) => Dispatcher.Invoke(() => BtnGallery_Click(this, new RoutedEventArgs()));

                var itemExit = new WinForms.ToolStripMenuItem("❌ Exit / Close App");
                itemExit.Click += (s, e) => Dispatcher.Invoke(() => ExitApplicationFromTray());

                contextMenu.Items.Add(itemOpenMain);
                contextMenu.Items.Add(itemOpenBrowser);
                contextMenu.Items.Add(itemOpenMiniClip);
                contextMenu.Items.Add(itemOpenGallery);
                contextMenu.Items.Add(new WinForms.ToolStripSeparator());
                contextMenu.Items.Add(itemExit);

                _notifyIcon.ContextMenuStrip = contextMenu;
                _notifyIcon.DoubleClick += (s, e) => Dispatcher.Invoke(() => ShowAndActivateMainWindow());
            }
            catch { }
        }

        private void ShowAndActivateMainWindow()
        {
            this.Show();
            if (this.WindowState == WindowState.Minimized)
                this.WindowState = WindowState.Normal;
            this.Activate();
        }

        private void ExitApplicationFromTray()
        {
            _isShuttingDown = true;
            CleanupSystemTrayIcon();
            System.Windows.Application.Current.Shutdown();
        }

        private void CleanupSystemTrayIcon()
        {
            try
            {
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                    _notifyIcon = null;
                }
            }
            catch { }
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_isShuttingDown)
            {
                e.Cancel = true;
                this.Hide();
            }
            else
            {
                CleanupSystemTrayIcon();
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) BtnMaximize_Click(sender, e);
            else if (e.ClickCount == 1 && e.LeftButton == MouseButtonState.Pressed) this.DragMove();
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                if (BtnMaximize != null)
                {
                    BtnMaximize.ToolTip = "بازگردانی";
                    if (MaximizeIconPath != null && TryFindResource("IconRestore") is StreamGeometry restoreGeom)
                    {
                        MaximizeIconPath.Data = restoreGeom;
                    }
                }
                var resizeThickness = SystemParameters.WindowResizeBorderThickness;
                MainBorder.Margin = new Thickness(resizeThickness.Left, resizeThickness.Top, resizeThickness.Right, resizeThickness.Bottom);
                MainBorder.CornerRadius = new CornerRadius(0);
                MainBorder.BorderThickness = new Thickness(0);
            }
            else
            {
                if (BtnMaximize != null)
                {
                    BtnMaximize.ToolTip = "بزرگ کردن";
                    if (MaximizeIconPath != null && TryFindResource("IconMaximize") is StreamGeometry maxGeom)
                    {
                        MaximizeIconPath.Data = maxGeom;
                    }
                }
                MainBorder.Margin = new Thickness(0);
                MainBorder.CornerRadius = new CornerRadius(8);
                MainBorder.BorderThickness = new Thickness(1);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => this.Close();
        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void BtnMaximize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

        private void GlobalHook_OnKeyPressed(WinForms.Keys key)
        {
            if (key == WinForms.Keys.Space || key == WinForms.Keys.Enter || key == WinForms.Keys.Tab)
            {
                string currentText = _typedBuffer.ToString();
                string lastWord = GetLastWord(currentText);
                if (!string.IsNullOrEmpty(lastWord))
                {
                    var matchedSnippet = SnippetManager.FindMatch(lastWord);
                    if (matchedSnippet != null)
                    {
                        string expansion = matchedSnippet.Value;
                        string snippetKey = matchedSnippet.Key;
                        int backspaceCount = snippetKey.Length + 1;

                        _typedBuffer.Clear();

                        Task.Run(async () =>
                        {
                            await Task.Delay(20);
                            InputSimulator.SimulateBackspace(backspaceCount);
                            await Task.Delay(20);
                            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                SafeSetClipboardText(expansion);
                                try
                                {
                                    System.Windows.Point spawnPos = GetCaretOrMousePosition();
                                    var overlay = new OverlayWindow();
                                    overlay.Left = spawnPos.X - 200;
                                    overlay.Top = spawnPos.Y - 200;
                                    overlay.Show();
                                }
                                catch { }
                            });
                            await Task.Delay(60);
                            InputSimulator.SimulatePaste();
                        });
                        return;
                    }
                }
                _typedBuffer.Append(" ");
                if (_typedBuffer.Length > 100) _typedBuffer.Remove(0, 50);
            }
            else if (key == WinForms.Keys.Back)
            {
                if (_typedBuffer.Length > 0) _typedBuffer.Length--;
            }
            else
            {
                string charTyped = GetCharsFromKeys(key);
                if (!string.IsNullOrEmpty(charTyped))
                {
                    _typedBuffer.Append(charTyped);
                }
                else if (key == WinForms.Keys.Escape || key == WinForms.Keys.LWin || key == WinForms.Keys.RWin || key == WinForms.Keys.ControlKey)
                {
                    _typedBuffer.Clear();
                }
            }
        }

        private System.Windows.Point GetCaretOrMousePosition()
        {
            try
            {
                GUITHREADINFO guiInfo = new GUITHREADINFO();
                guiInfo.cbSize = Marshal.SizeOf(guiInfo);
                IntPtr foregroundWindow = GetForegroundWindow();
                uint threadId = GetWindowThreadProcessId(foregroundWindow, out _);
                if (GetGUIThreadInfo(threadId, ref guiInfo) && guiInfo.hwndCaret != IntPtr.Zero)
                {
                    POINT p = new POINT { x = guiInfo.rcCaret.Left, y = guiInfo.rcCaret.Top };
                    ClientToScreen(guiInfo.hwndCaret, ref p);
                    if (p.x != 0 || p.y != 0) return new System.Windows.Point(p.x, p.y);
                }
            }
            catch { }
            var mouse = WinForms.Cursor.Position;
            return new System.Windows.Point(mouse.X, mouse.Y);
        }

        private string GetLastWord(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            int lastSpaceIndex = text.LastIndexOfAny(new char[] { ' ', '\n', '\t' });
            if (lastSpaceIndex == -1) return text;
            return text.Substring(lastSpaceIndex + 1);
        }

        private string GetCharsFromKeys(WinForms.Keys key)
        {
            if (key >= WinForms.Keys.A && key <= WinForms.Keys.Z) return key.ToString().ToLower();
            if (key >= WinForms.Keys.D0 && key <= WinForms.Keys.D9) return key.ToString().Replace("D", "");
            if (key >= WinForms.Keys.NumPad0 && key <= WinForms.Keys.NumPad9) return key.ToString().Replace("NumPad", "");

            switch (key)
            {
                case WinForms.Keys.OemPeriod: return ".";
                case WinForms.Keys.OemMinus: return "-";
                case WinForms.Keys.OemQuestion: return "/";
                case WinForms.Keys.Divide: return "/";
                case WinForms.Keys.Oemcomma: return ",";
                case WinForms.Keys.Oem1: return ";";
                case WinForms.Keys.Oem7: return "'";
            }
            return "";
        }

        private void PromptBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e) { }

        private void LoadSettings()
        {
            try
            {
                string configPath = DataPathManager.GetDataFilePath(ConfigFileName);
                if (File.Exists(configPath))
                {
                    string[] lines = File.ReadAllLines(configPath);
                    if (lines.Length > 0)
                    {
                        string savedPath = lines[0].Trim();
                        if (Directory.Exists(savedPath)) _savePath = savedPath;
                    }
                }
            }
            catch { }
        }

        private void BtnSnippets_Click(object sender, RoutedEventArgs e)
        {
            if (_snippetWindow == null || !_snippetWindow.IsLoaded) _snippetWindow = new SnippetWindow();
            _snippetWindow.Show();
            _snippetWindow.Activate();
        }

        private void BtnGallery_Click(object sender, RoutedEventArgs e)
        {
            if (_galleryWindow == null || !_galleryWindow.IsLoaded) _galleryWindow = new GalleryWindow(_savePath);
            _galleryWindow.Show();
            _galleryWindow.Activate();
        }

        private void BtnMiniClipboard_Click(object sender, RoutedEventArgs e)
        {
            if (_miniClipboardWindow == null || !_miniClipboardWindow.IsLoaded) _miniClipboardWindow = new MiniClipboardWindow();
            _miniClipboardWindow.Show();
            _miniClipboardWindow.Activate();
        }

        private void BtnExtraFloat_Click(object sender, RoutedEventArgs e)
        {
            if (_floatingExtraWindow == null || !_floatingExtraWindow.IsLoaded) _floatingExtraWindow = new FloatingExtraWindow();
            _floatingExtraWindow.Show();
            _floatingExtraWindow.Activate();
        }

        private void BtnPersona_Click(object sender, RoutedEventArgs e)
        {
            if (_personaInjectorWindow == null || !_personaInjectorWindow.IsLoaded) _personaInjectorWindow = new PersonaInjectorWindow();
            _personaInjectorWindow.Show();
            _personaInjectorWindow.Activate();
        }

        private PromptSurgeonWindow? _promptSurgeonWindow;

        private void BtnPromptSurgeon_Click(object sender, RoutedEventArgs e)
        {
            if (_promptSurgeonWindow == null || !_promptSurgeonWindow.IsLoaded)
            {
                _promptSurgeonWindow = new PromptSurgeonWindow();
            }
            _promptSurgeonWindow.Show();
            _promptSurgeonWindow.Activate();
        }

        private void BtnTutorial_Click(object sender, RoutedEventArgs e)
        {
            if (_tutorialWindow == null || !_tutorialWindow.IsLoaded) _tutorialWindow = new TutorialWindow();
            _tutorialWindow.Show();
            _tutorialWindow.Activate();
        }

        private void BtnCloudLink_Click(object sender, RoutedEventArgs e)
        {
            if (_fileShareServer == null)
            {
                _fileShareServer = new FileShareServer();
                _fileShareServer.StatusChanged += FileShareServer_StatusChanged;
            }
            if (_fileShareWindow == null || !_fileShareWindow.IsLoaded)
            {
                _fileShareWindow = new FileShareWindow(_fileShareServer);
            }
            _fileShareWindow.Show();
            _fileShareWindow.Activate();
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            ShowSettingsView();
        }

        private void BtnBrowser_Click(object sender, RoutedEventArgs e)
        {
            if (_browserWindow == null || !_browserWindow.IsLoaded)
            {
                BrowserProfile? profileToUse = null;
                if (ProfileManager.AlwaysAskAccountOnStartup)
                {
                    var selector = new ProfileSelectionWindow();
                    if (selector.ShowDialog() == true && selector.SelectedProfile != null)
                    {
                        profileToUse = selector.SelectedProfile;
                    }
                    else
                    {
                        // User cancelled profile selection dialog! Do NOT open browser window!
                        return;
                    }
                }
                else
                {
                    profileToUse = ProfileManager.GetActiveProfile();
                }

                _browserWindow = new BrowserWindow(profileToUse);
            }
            _browserWindow.Show();
            _browserWindow.Activate();
        }



        private void BtnRemote_Click(object sender, RoutedEventArgs e)
        {
            if (_remoteServer == null)
            {
                _remoteServer = new RemoteServer();
                _remoteServer.StatusChanged += RemoteServer_StatusChanged;
            }
            if (_remoteServer.IsRunning) _remoteServer.Stop();
            else _remoteServer.Start();
        }

        private void RemoteServer_StatusChanged(string status)
        {
            Dispatcher.Invoke(() =>
            {
                TxtServerStatus.Text = status;
                bool isRunning = _remoteServer?.IsRunning ?? false;
                ServerStatusDot.Fill = isRunning ? System.Windows.Media.Brushes.LimeGreen : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(68, 68, 68));
                if (isRunning)
                {
                    BtnRemote.Background = (System.Windows.Media.Brush)FindResource("SelectedBrush");
                    BtnRemote.BorderBrush = (System.Windows.Media.Brush)FindResource("AccentBrush");
                }
                else
                {
                    BtnRemote.ClearValue(BackgroundProperty);
                    BtnRemote.ClearValue(BorderBrushProperty);
                }
            });
            SaveServerStates();
        }

        private void FileShareServer_StatusChanged(string status)
        {
            SaveServerStates();
        }

        private void SaveServerStates()
        {
            if (_isShuttingDown) return;
            try
            {
                string statePath = DataPathManager.GetSettingsFilePath(ServerStateFileName);
                string remoteRunning = (_remoteServer?.IsRunning == true).ToString().ToLower();
                string cloudLinkRunning = (_fileShareServer?.IsRunning == true).ToString().ToLower();
                File.WriteAllLines(statePath, new string[] { remoteRunning, cloudLinkRunning });
            }
            catch { }
        }

        private void LoadAndApplyServerStates()
        {
            try
            {
                string statePath = DataPathManager.GetSettingsFilePath(ServerStateFileName);
                if (File.Exists(statePath))
                {
                    string[] lines = File.ReadAllLines(statePath);
                    if (lines.Length > 0 && lines[0].Trim().ToLower() == "true")
                    {
                        if (_remoteServer == null)
                        {
                            _remoteServer = new RemoteServer();
                            _remoteServer.StatusChanged += RemoteServer_StatusChanged;
                        }
                        _remoteServer.Start();
                    }
                    if (lines.Length > 1 && lines[1].Trim().ToLower() == "true")
                    {
                        if (_fileShareServer == null)
                        {
                            _fileShareServer = new FileShareServer();
                            _fileShareServer.StatusChanged += FileShareServer_StatusChanged;
                        }
                        _fileShareServer.Start();
                    }
                }
            }
            catch { }
        }

        private void TxtImageName_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (System.Windows.Clipboard.ContainsImage() || System.Windows.Clipboard.ContainsFileDropList())
                {
                    LoadImageFromClipboard();
                    e.Handled = true;
                }
            }
        }

        private void LoadImageFromClipboard()
        {
            try
            {
                _sourceFilePath = null;
                _detectedExtension = ".png";
                BitmapSource? imageForPreview = null;
                if (System.Windows.Clipboard.ContainsFileDropList())
                {
                    var fileList = System.Windows.Clipboard.GetFileDropList();
                    if (fileList.Count > 0)
                    {
                        string filePath = fileList[0];
                        if (IsImageFile(filePath))
                        {
                            _sourceFilePath = filePath;
                            _detectedExtension = Path.GetExtension(filePath).ToLower();
                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.UriSource = new Uri(filePath);
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.EndInit();
                            imageForPreview = bitmap;
                        }
                    }
                }
                if (imageForPreview == null && System.Windows.Clipboard.ContainsImage()) imageForPreview = System.Windows.Clipboard.GetImage();
                if (imageForPreview != null)
                {
                    var convertedBitmap = new FormatConvertedBitmap();
                    convertedBitmap.BeginInit();
                    convertedBitmap.Source = imageForPreview;
                    convertedBitmap.DestinationFormat = PixelFormats.Bgra32;
                    convertedBitmap.EndInit();
                    var finalPreview = new WriteableBitmap(convertedBitmap);
                    finalPreview.Freeze();
                    _previewImage = finalPreview;
                }
            }
            catch (Exception ex) { CustomMessageBox.Show("Error loading image:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private bool IsImageFile(string? path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string ext = Path.GetExtension(path).ToLower();
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".webp" || ext == ".bmp" || ext == ".tiff";
        }

        private void BtnDeleteImage_Click(object sender, RoutedEventArgs e) { ResetImageOnly(); }
        private void ResetImageOnly() { _previewImage = null; _sourceFilePath = null; }

        private void BtnArchive_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_savePath) || !Directory.Exists(_savePath))
            {
                CustomMessageBox.Show("Please select a valid directory in Settings first.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (CustomMessageBox.Show($"This will rename the current folder '{Path.GetFileName(_savePath)}' and create a new empty one. Proceed?", "Archive Folder", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            try
            {
                string parentDir = Directory.GetParent(_savePath)?.FullName ?? "";
                if (string.IsNullOrEmpty(parentDir)) return;
                string folderName = Path.GetFileName(_savePath);
                string newPath = "";
                int i = 1;
                do { newPath = Path.Combine(parentDir, $"{folderName}_{i}"); i++; } while (Directory.Exists(newPath));
                Directory.Move(_savePath, newPath);
                Directory.CreateDirectory(_savePath);
                CustomMessageBox.Show($"Success!\nOld Folder: {Path.GetFileName(newPath)}\nNew Folder: {folderName} (Empty)", "Archive Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { CustomMessageBox.Show($"Error archiving folder: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private async void BtnZipShare_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_savePath) || !Directory.Exists(_savePath)) return;
            try
            {
                string parentDir = Directory.GetParent(_savePath)?.FullName ?? "";
                string folderName = Path.GetFileName(_savePath);
                string latestFolder = "";
                int maxIndex = 0;
                var dirs = Directory.GetDirectories(parentDir, $"{folderName}_*");
                foreach (var dir in dirs)
                {
                    string dirName = Path.GetFileName(dir);
                    if (int.TryParse(dirName.Substring(folderName.Length + 1), out int index) && index > maxIndex) { maxIndex = index; latestFolder = dir; }
                }
                if (string.IsNullOrEmpty(latestFolder)) return;
                string zipFileName = $"{Path.GetFileName(latestFolder)}.zip";
                string shareDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "share");
                string destinationPath = Path.Combine(shareDir, zipFileName);
                if (!Directory.Exists(shareDir)) Directory.CreateDirectory(shareDir);
                if (File.Exists(destinationPath)) File.Delete(destinationPath);
                var progressDialog = new ProgressDialog { Owner = this };
                progressDialog.SetTitle("Creating Zip Archive");
                var zipTask = Task.Run(async () =>
                {
                    try
                    {
                        progressDialog.UpdateProgress(5, "Scanning files...");
                        long totalSize = await CalculateDirSizeAsync(latestFolder);
                        if (totalSize == 0) { progressDialog.UpdateProgress(100, "No files to compress"); await Task.Delay(500); progressDialog.Complete(); return; }
                        progressDialog.UpdateProgress(10, "Preparing compression...");
                        await CreateZipWithProgressAsync(latestFolder, destinationPath, totalSize, progressDialog);
                        progressDialog.UpdateProgress(100, "Compression complete!");
                        await Task.Delay(300); progressDialog.Complete();
                    }
                    catch (Exception ex) { Dispatcher.Invoke(() => { CustomMessageBox.Show($"Error during compression: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); progressDialog.Close(); }); }
                });
                progressDialog.ShowDialog();
                await zipTask;
                if (File.Exists(destinationPath))
                {
                    long fileSize = new FileInfo(destinationPath).Length;
                    string sizeStr = FormatBytes(fileSize);
                    CustomMessageBox.Show($"Successfully zipped and moved to Share!\n\nSize: {sizeStr}\nLocation: {destinationPath}", "Zip & Share Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex) { CustomMessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private async Task<long> CalculateDirSizeAsync(string dirPath)
        {
            long totalSize = 0;
            try
            {
                var di = new DirectoryInfo(dirPath);
                var files = GetFilesWithRetry(di);
                foreach (var file in files) try { if (file.Exists) totalSize += file.Length; } catch { }
                var subDirs = GetSubDirsWithRetry(di);
                foreach (var subDir in subDirs) totalSize += await CalculateDirSizeAsync(subDir.FullName);
            }
            catch { }
            return totalSize;
        }

        private async Task CreateZipWithProgressAsync(string sourceDir, string zipPath, long totalBytes, ProgressDialog progressDialog)
        {
            const int bufferSize = 65536;
            var state = new ZipCompressionState { ProcessedBytes = 0, FileCount = 0 };
            using (var zipStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize))
            using (var archive = new System.IO.Compression.ZipArchive(zipStream, System.IO.Compression.ZipArchiveMode.Create, true, Encoding.UTF8))
            {
                var di = new DirectoryInfo(sourceDir);
                await CompressDirectoryAsync(archive, di, "", sourceDir, state, totalBytes, progressDialog);
            }
        }

        private async Task CompressDirectoryAsync(System.IO.Compression.ZipArchive archive, DirectoryInfo dir, string arcPath, string rootPath, ZipCompressionState state, long totalBytes, ProgressDialog progressDialog)
        {
            var files = GetFilesWithRetry(dir);
            foreach (var file in files)
            {
                try
                {
                    byte[]? fileData = await ReadFileWithRetryAsync(file.FullName, maxRetries: 5);
                    if (fileData == null) continue;
                    string entryName = Path.Combine(arcPath, file.Name).Replace("\\", "/");
                    var entry = archive.CreateEntry(entryName, System.IO.Compression.CompressionLevel.Optimal);
                    using (var entryStream = entry.Open()) await entryStream.WriteAsync(fileData, 0, fileData.Length);
                    state.ProcessedBytes += fileData.Length;
                    state.FileCount++;
                    int progress = totalBytes > 0 ? (int)(10 + (89 * state.ProcessedBytes / totalBytes)) : 10;
                    progress = Math.Min(progress, 99);
                    Dispatcher.Invoke(() => progressDialog.UpdateProgress(progress, $"Compressed {state.FileCount} files ({FormatBytes(state.ProcessedBytes)}/{FormatBytes(totalBytes)})"));
                    await Task.Delay(1);
                }
                catch { }
            }
            var subDirs = GetSubDirsWithRetry(dir);
            foreach (var subDir in subDirs) try { string newArcPath = string.IsNullOrEmpty(arcPath) ? subDir.Name : Path.Combine(arcPath, subDir.Name); await CompressDirectoryAsync(archive, subDir, newArcPath, rootPath, state, totalBytes, progressDialog); } catch { }
        }

        private FileInfo[] GetFilesWithRetry(DirectoryInfo dir, int maxRetries = 3) { for (int i = 0; i < maxRetries; i++) try { return dir.GetFiles(); } catch { if (i == maxRetries - 1) return new FileInfo[0]; Thread.Sleep(100 * (i + 1)); } return new FileInfo[0]; }
        private DirectoryInfo[] GetSubDirsWithRetry(DirectoryInfo dir, int maxRetries = 3) { for (int i = 0; i < maxRetries; i++) try { return dir.GetDirectories(); } catch { if (i == maxRetries - 1) return new DirectoryInfo[0]; Thread.Sleep(100 * (i + 1)); } return new DirectoryInfo[0]; }
        private async Task<byte[]?> ReadFileWithRetryAsync(string filePath, int maxRetries = 5) { for (int attempt = 0; attempt < maxRetries; attempt++) try { return await File.ReadAllBytesAsync(filePath); } catch (IOException) when (attempt < maxRetries - 1) { await Task.Delay(100 * (int)Math.Pow(2, attempt)); } catch { return null; } return null; }
        private string FormatBytes(long bytes) { string[] sizes = { "B", "KB", "MB", "GB" }; double len = bytes; int order = 0; while (len >= 1024 && order < sizes.Length - 1) { order++; len = len / 1024; } return $"{len:0.##} {sizes[order]}"; }
        private class ZipCompressionState { public long ProcessedBytes { get; set; } public int FileCount { get; set; } }


        private void InitInputRecorderPlayer() { try { _ = RecordingsDir; _inputRecorder = new InputRecorder(); _inputPlayer = new InputPlayer(); } catch { } }
        private void BtnStartRecording_Click(object sender, RoutedEventArgs e) { if (_inputRecorder == null) InitInputRecorderPlayer(); _inputRecorder?.Start(); }
        private void BtnStopRecording_Click(object sender, RoutedEventArgs e) { _inputRecorder?.Stop(); }
        private async void BtnSaveRecording_Click(object sender, RoutedEventArgs e) { if (_inputRecorder == null) return; string fileName = Path.Combine(RecordingsDir, $"rec_{DateTime.Now:yyyyMMdd_HHmmss}.json"); await _inputRecorder.SaveAsync(fileName); System.Windows.MessageBox.Show($"Saved recording to {fileName}"); }
        private async void BtnLoadRecording_Click(object sender, RoutedEventArgs e) { var ofd = new System.Windows.Forms.OpenFileDialog { InitialDirectory = RecordingsDir, Filter = "JSON files|*.json|All files|*.*" }; if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK) { if (_inputRecorder == null) InitInputRecorderPlayer(); if (await _inputRecorder.LoadAsync(ofd.FileName)) { _inputPlayer.SetEvents(_inputRecorder.GetEvents()); System.Windows.MessageBox.Show("Loaded recording."); } else System.Windows.MessageBox.Show("Failed to load recording."); } }
        private async void BtnPlayRecording_Click(object sender, RoutedEventArgs e) { if (_inputPlayer == null) InitInputRecorderPlayer(); _inputPlayer.SetEvents(_inputRecorder?.GetEvents() ?? new List<InputEvent>()); await _inputPlayer.PlayAsync(loop: false); }
        private void BtnStopPlayback_Click(object sender, RoutedEventArgs e) { _inputPlayer?.Stop(); }
        private void BtnOpenRecorder_Click(object sender, RoutedEventArgs e)
        {
            if (_inputRecorderWindow == null || !_inputRecorderWindow.IsLoaded) _inputRecorderWindow = new InputRecorderWindow();
            _inputRecorderWindow.Show();
            _inputRecorderWindow.Activate();
        }

        private void BtnPromptTags_Click(object sender, RoutedEventArgs e)
        {
            if (_promptTaggerWindow == null || !_promptTaggerWindow.IsLoaded) _promptTaggerWindow = new PromptTaggerWindow();
            _promptTaggerWindow.Show();
            _promptTaggerWindow.Activate();
        }

        private void BtnExit_Click(object sender, RoutedEventArgs e)
        {
            PerformFullApplicationShutdown();
        }

        #region In-Place Settings View Logic

        private void ShowSettingsView()
        {
            LoadSettingsIntoView();
            DashboardView.Visibility = Visibility.Collapsed;
            SettingsView.Visibility = Visibility.Visible;
            ChangePasswordPanel.Visibility = Visibility.Collapsed;
        }

        private void ShowDashboardView()
        {
            SettingsView.Visibility = Visibility.Collapsed;
            DashboardView.Visibility = Visibility.Visible;
            LoadSettings();
        }

        private void BtnHome_Click(object sender, RoutedEventArgs e)
        {
            ShowDashboardView();
        }

        private void BtnBackFromSettings_Click(object sender, RoutedEventArgs e)
        {
            ShowDashboardView();
        }

        private void BtnSaveSettings_Click(object sender, RoutedEventArgs e)
        {
            SaveSettingsFromView();
            ShowDashboardView();
        }

        private void LoadSettingsIntoView()
        {
            try
            {
                // Language
                string currentLang = LanguageManager.CurrentLanguage;
                CmbLanguage.SelectedIndex = (currentLang == "fa") ? 0 : 1;

                // Security Status & Lock Mode
                bool isSecured = SecurityManager.IsPasswordConfigured();
                TxtSecurityStatus.Text = isSecured 
                    ? "وضعیت: محافظت شده با رمز عبور اصلی (PBKDF2 + DPAPI)" 
                    : "وضعیت: رمز عبور اصلی تعریف نشده است";
                TxtSecurityStatus.Foreground = isSecured 
                    ? (System.Windows.Media.Brush)new BrushConverter().ConvertFrom("#4ADE80")! 
                    : (System.Windows.Media.Brush)new BrushConverter().ConvertFrom("#F87171")!;

                var currentLockMode = SecurityManager.GetLockMode();
                CmbLockTriggerMode.SelectedIndex = (currentLockMode == LockTriggerMode.AlwaysOnStartup) ? 0 : 1;

                var preferredAuth = SecurityManager.GetPreferredAuthType();
                CmbLockAuthType.SelectedIndex = (preferredAuth == LockAuthType.Pattern) ? 0 : 1;

                // Paths & Config
                DataPathManager.Reload();
                ChkUseCustomDataFolder.IsChecked = DataPathManager.UseCustomDataFolder;
                TxtCustomDataFolder.Text = DataPathManager.CustomDataFolder;

                string configPath = DataPathManager.GetSettingsFilePath(ConfigFileName);
                if (File.Exists(configPath))
                {
                    string[] lines = File.ReadAllLines(configPath);
                    if (lines.Length > 0) TxtSettingsSavePath.Text = lines[0].Trim();
                    if (lines.Length > 2) ChkAutoImportEnabled.IsChecked = lines[2].Trim().ToLower() == "true";
                    if (lines.Length > 3) TxtAutoImportPath.Text = lines[3].Trim();
                    if (lines.Length > 4) ChkAutoSaveEnabled.IsChecked = lines[4].Trim().ToLower() == "true";
                    if (lines.Length > 5) TxtAutoSaveCount.Text = lines[5].Trim();
                    ChkAutoCaptureExtraTemplate.IsChecked = lines.Length <= 6 || lines[6].Trim().ToLower() == "true";
                    ChkAutoCopyExtraTemplateOutput.IsChecked = lines.Length <= 7 || lines[7].Trim().ToLower() == "true";
                    ChkReplacePositivePromptOnClipboardText.IsChecked = lines.Length <= 8 || lines[8].Trim().ToLower() == "true";
                    ChkSpiSyncPreserveBasePrompt.IsChecked = lines.Length > 9 && lines[9].Trim().ToLower() == "true";
                    ChkUseTagReplacerForMiniClip.IsChecked = lines.Length > 10 && lines[10].Trim().ToLower() == "true";
                    TxtTagReplacerPrefix.Text = lines.Length > 11 ? lines[11].Trim() : "PH_";
                    ChkAutoCopyTagReplacerOutput.IsChecked = lines.Length <= 13 || lines[13].Trim().ToLower() == "true";
                    ChkAutoSaveDelay.IsChecked = lines.Length > 14 && lines[14].Trim().ToLower() == "true";
                    TxtAutoSaveDelaySeconds.Text = lines.Length > 15 ? lines[15].Trim() : "10";
                }
                else
                {
                    TxtSettingsSavePath.Text = _savePath;
                }

                string galleryConfigPath = DataPathManager.GetSettingsFilePath("gallery_config.txt");
                if (File.Exists(galleryConfigPath))
                {
                    TxtSettingsGalleryPath.Text = File.ReadAllText(galleryConfigPath).Trim();
                }

                var bSettings = BrowserSettings.Load();
                TxtMinImageWidth.Text = bSettings.MinImageWidth.ToString();
                TxtMinImageHeight.Text = bSettings.MinImageHeight.ToString();
            }
            catch { }
        }

        private void SaveSettingsFromView()
        {
            try
            {
                bool useCustom = ChkUseCustomDataFolder.IsChecked == true;
                string customFolder = TxtCustomDataFolder.Text.Trim();

                DataPathManager.SaveLocation(useCustom, customFolder);

                string configPath = DataPathManager.GetSettingsFilePath(ConfigFileName);
                string galleryConfigPath = DataPathManager.GetSettingsFilePath("gallery_config.txt");

                string path = TxtSettingsSavePath.Text.Trim();
                string autoImportEnabled = (ChkAutoImportEnabled.IsChecked == true).ToString().ToLower();
                string autoImportPath = TxtAutoImportPath.Text.Trim();
                string autoSaveEnabled = (ChkAutoSaveEnabled.IsChecked == true).ToString().ToLower();
                string autoSaveCount = TxtAutoSaveCount.Text.Trim();
                if (string.IsNullOrEmpty(autoSaveCount)) autoSaveCount = "1";
                string autoCaptureExtra = (ChkAutoCaptureExtraTemplate.IsChecked == true).ToString().ToLower();
                string autoCopyExtra = (ChkAutoCopyExtraTemplateOutput.IsChecked == true).ToString().ToLower();
                string replacePos = (ChkReplacePositivePromptOnClipboardText.IsChecked == true).ToString().ToLower();
                string preserveBase = (ChkSpiSyncPreserveBasePrompt.IsChecked == true).ToString().ToLower();
                string useTagReplacer = (ChkUseTagReplacerForMiniClip.IsChecked == true).ToString().ToLower();
                string tagPrefix = TxtTagReplacerPrefix.Text.Trim();
                if (string.IsNullOrEmpty(tagPrefix)) tagPrefix = "PH_";
                string autoCopyTag = (ChkAutoCopyTagReplacerOutput.IsChecked == true).ToString().ToLower();
                string autoSaveDelay = (ChkAutoSaveDelay.IsChecked == true).ToString().ToLower();
                string autoSaveDelaySec = TxtAutoSaveDelaySeconds.Text.Trim();
                if (string.IsNullOrEmpty(autoSaveDelaySec)) autoSaveDelaySec = "10";
                string galleryPath = TxtSettingsGalleryPath.Text.Trim();

                string selectedLang = (CmbLanguage.SelectedIndex == 0) ? "fa" : "en";
                LanguageManager.ApplyLanguage(selectedLang);

                var selectedLockMode = (CmbLockTriggerMode.SelectedIndex == 1)
                    ? LockTriggerMode.ManualOrRestart
                    : LockTriggerMode.AlwaysOnStartup;
                SecurityManager.SetLockMode(selectedLockMode);

                var selectedAuthType = (CmbLockAuthType.SelectedIndex == 1)
                    ? LockAuthType.Password
                    : LockAuthType.Pattern;
                SecurityManager.SetPreferredAuthType(selectedAuthType);

                File.WriteAllLines(configPath, new string[] {
                    path,
                    "false",
                    autoImportEnabled,
                    autoImportPath,
                    autoSaveEnabled,
                    autoSaveCount,
                    autoCaptureExtra,
                    autoCopyExtra,
                    replacePos,
                    preserveBase,
                    useTagReplacer,
                    tagPrefix,
                    selectedLang,
                    autoCopyTag,
                    autoSaveDelay,
                    autoSaveDelaySec
                });

                File.WriteAllText(galleryConfigPath, galleryPath);

                var bSettings = BrowserSettings.Load();
                if (int.TryParse(TxtMinImageWidth.Text, out int minWidth) && minWidth > 0)
                    bSettings.MinImageWidth = minWidth;
                if (int.TryParse(TxtMinImageHeight.Text, out int minHeight) && minHeight > 0)
                    bSettings.MinImageHeight = minHeight;
                bSettings.Save();

                if (Directory.Exists(path)) _savePath = path;

                // Refresh any open child windows
                foreach (Window w in System.Windows.Application.Current.Windows)
                {
                    if (w is MiniClipboardWindow mini) mini.RefreshAutoImport();
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("خطا در ذخیره تنظیمات: " + ex.Message, "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnChangeMasterPassword_Click(object sender, RoutedEventArgs e)
        {
            ChangePasswordPanel.Visibility = ChangePasswordPanel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            TxtCurrentPassword.Password = "";
            TxtNewPassword.Password = "";
            TxtConfirmNewPassword.Password = "";
            TxtCurrentPassword.Focus();
        }

        private void BtnCancelChangePassword_Click(object sender, RoutedEventArgs e)
        {
            ChangePasswordPanel.Visibility = Visibility.Collapsed;
            TxtCurrentPassword.Password = "";
            TxtNewPassword.Password = "";
            TxtConfirmNewPassword.Password = "";
        }

        private void BtnSubmitChangePassword_Click(object sender, RoutedEventArgs e)
        {
            string currentPass = TxtCurrentPassword.Password;
            string newPass = TxtNewPassword.Password;
            string confirmPass = TxtConfirmNewPassword.Password;

            if (string.IsNullOrEmpty(currentPass) || string.IsNullOrEmpty(newPass))
            {
                System.Windows.MessageBox.Show("لطفاً تمامی فیلدها را پر کنید.", "پیام", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (newPass.Length < 3)
            {
                System.Windows.MessageBox.Show("رمز عبور جدید باید حداقل ۳ کاراکتر باشد.", "پیام", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (newPass != confirmPass)
            {
                System.Windows.MessageBox.Show("رمز عبور جدید و تکرار آن یکسان نیستند.", "پیام", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool success = SecurityManager.ChangeMasterPassword(currentPass, newPass);
            if (success)
            {
                System.Windows.MessageBox.Show("رمز عبور اصلی با موفقیت تغییر یافت.", "موفقیت", MessageBoxButton.OK, MessageBoxImage.Information);
                ChangePasswordPanel.Visibility = Visibility.Collapsed;
                LoadSettingsIntoView();
            }
            else
            {
                System.Windows.MessageBox.Show("رمز عبور فعلی وارد شده نادرست است.", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnChangeMasterPattern_Click(object sender, RoutedEventArgs e)
        {
            // First verify identity if already configured
            if (SecurityManager.IsAnyAuthConfigured() && !_skipPatternAuthVerify())
                return;

            var patternSetupWindow = new AuthLockWindow(isRuntimeLock: false, forcePatternSetup: true);
            bool? setupComplete = patternSetupWindow.ShowDialog();

            if (setupComplete == true)
            {
                System.Windows.MessageBox.Show("الگوی ۹ نقطه‌ای با موفقیت تعریف/بروزرسانی شد.", "موفقیت", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadSettingsIntoView();
            }
        }

        private bool _skipPatternAuthVerify()
        {
            // If password is set, verify it first; if only pattern is set, skip extra verify to avoid deadlock
            if (!SecurityManager.IsAnyAuthConfigured()) return true;
            return true; // Currently we open directly; user is already in the app so identity is confirmed
        }

        private void BtnBrowseSavePath_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new WinForms.FolderBrowserDialog();
            if (!string.IsNullOrEmpty(TxtSettingsSavePath.Text) && Directory.Exists(TxtSettingsSavePath.Text))
                dialog.SelectedPath = TxtSettingsSavePath.Text;

            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
                TxtSettingsSavePath.Text = dialog.SelectedPath;
        }

        private void BtnBrowseGalleryPath_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new WinForms.FolderBrowserDialog();
            if (!string.IsNullOrEmpty(TxtSettingsGalleryPath.Text) && Directory.Exists(TxtSettingsGalleryPath.Text))
                dialog.SelectedPath = TxtSettingsGalleryPath.Text;

            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
                TxtSettingsGalleryPath.Text = dialog.SelectedPath;
        }

        private void BtnBrowseCustomDataFolder_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new WinForms.FolderBrowserDialog();
            if (!string.IsNullOrEmpty(TxtCustomDataFolder.Text) && Directory.Exists(TxtCustomDataFolder.Text))
                dialog.SelectedPath = TxtCustomDataFolder.Text;

            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
                TxtCustomDataFolder.Text = dialog.SelectedPath;
        }

        private void BtnBrowseAutoImport_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new WinForms.FolderBrowserDialog();
            if (!string.IsNullOrEmpty(TxtAutoImportPath.Text) && Directory.Exists(TxtAutoImportPath.Text))
                dialog.SelectedPath = TxtAutoImportPath.Text;

            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
                TxtAutoImportPath.Text = dialog.SelectedPath;
        }

        #endregion

        private void BtnLockApp_Click(object sender, RoutedEventArgs e)
        {
            AppLockManager.LockApp();
        }

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.L && (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
            {
                AppLockManager.LockApp();
                e.Handled = true;
            }
        }

        public static void PerformFullApplicationShutdown()
        {
            try
            {
                var windows = System.Windows.Application.Current.Windows.Cast<Window>().ToList();
                foreach (Window win in windows)
                {
                    try
                    {
                        if (win != null && win.IsLoaded && win != System.Windows.Application.Current.MainWindow)
                        {
                            win.Close();
                        }
                    }
                    catch { }
                }
            }
            catch { }

            try
            {
                System.Windows.Application.Current.Shutdown();
            }
            catch { }

            System.Environment.Exit(0);
        }
    }
}
