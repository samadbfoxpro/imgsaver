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

        private InputRecorder? _inputRecorder;
        private InputPlayer? _inputPlayer;

        private string RecordingsDir => DataPathManager.GetDataSubfolderPath("recordings");

        private SnippetWindow? _snippetWindow;
        private ClipboardSaverWindow? _clipboardSaverWindow;
        private GalleryWindow? _galleryWindow;
        private MiniClipboardWindow? _miniClipboardWindow;
        private MiniQuickSaverWindow? _miniQuickSaverWindow;
        private PersonaInjectorWindow? _personaInjectorWindow;
        private TutorialWindow? _tutorialWindow;
        private FileShareWindow? _fileShareWindow;
        private InputRecorderWindow? _inputRecorderWindow;
        private BrowserWindow? _browserWindow;
        private FloatingExtraWindow? _floatingExtraWindow;

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
            InitializeComponent();
            LoadSettings();
            this.MaxHeight = SystemParameters.WorkArea.Height;
            VersionManager.Load();
            TxtVersion.Text = $"v{VersionManager.CurrentVersion}";

            try
            {
                _globalHook = new GlobalHook();
                _globalHook.OnKeyPressed += GlobalHook_OnKeyPressed;
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show("Error (Please run as Administrator):\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            Loaded += (_, _) => TxtImageName.Focus();
            Closed += (_, _) => {
                _globalHook?.Dispose();
                _remoteServer?.Stop();
                System.Windows.Application.Current.Shutdown();
            };
            Closing += MainWindow_Closing;
            InitInputRecorderPlayer();
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (CustomMessageBox.Show("Are you sure you want to exit?", "Confirm Exit", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                e.Cancel = true;
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) BtnMaximize_Click(sender, e);
            else this.DragMove();
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                MainBorder.Margin = new Thickness(6);
                MainBorder.CornerRadius = new CornerRadius(0);
                MainBorder.BorderThickness = new Thickness(0);
            }
            else
            {
                MainBorder.Margin = new Thickness(20);
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

        private void BtnQuickClipboard_Click(object sender, RoutedEventArgs e)
        {
            if (_clipboardSaverWindow == null || !_clipboardSaverWindow.IsLoaded) _clipboardSaverWindow = new ClipboardSaverWindow();
            _clipboardSaverWindow.Show();
            _clipboardSaverWindow.Activate();
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

        private void BtnMiniQuickSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_savePath) || !Directory.Exists(_savePath))
            {
                CustomMessageBox.Show("Please select a save directory in Settings first.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (_miniQuickSaverWindow == null || !_miniQuickSaverWindow.IsLoaded) _miniQuickSaverWindow = new MiniQuickSaverWindow(_savePath);
            _miniQuickSaverWindow.Show();
            _miniQuickSaverWindow.Activate();
        }

        private void BtnPersona_Click(object sender, RoutedEventArgs e)
        {
            if (_personaInjectorWindow == null || !_personaInjectorWindow.IsLoaded) _personaInjectorWindow = new PersonaInjectorWindow();
            _personaInjectorWindow.Show();
            _personaInjectorWindow.Activate();
        }

        private void BtnTutorial_Click(object sender, RoutedEventArgs e)
        {
            if (_tutorialWindow == null || !_tutorialWindow.IsLoaded) _tutorialWindow = new TutorialWindow();
            _tutorialWindow.Show();
            _tutorialWindow.Activate();
        }

        private void BtnCloudLink_Click(object sender, RoutedEventArgs e)
        {
            if (_fileShareWindow == null || !_fileShareWindow.IsLoaded) _fileShareWindow = new FileShareWindow();
            _fileShareWindow.Show();
            _fileShareWindow.Activate();
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new SettingsWindow();
            settingsWindow.Owner = this;
            settingsWindow.ShowDialog();
            LoadSettings();
        }

        private void BtnBrowser_Click(object sender, RoutedEventArgs e)
        {
            if (_browserWindow == null || !_browserWindow.IsLoaded) _browserWindow = new BrowserWindow();
            _browserWindow.Show();
            _browserWindow.Activate();
        }

        private void BtnToggleInput_Click(object sender, RoutedEventArgs e)
        {
            if (InputPanel.Visibility == Visibility.Visible)
            {
                InputPanel.Visibility = Visibility.Collapsed;
                TxtToggleArrow.Text = "▼";
            }
            else
            {
                InputPanel.Visibility = Visibility.Visible;
                TxtToggleArrow.Text = "▲";
            }
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
                    ImgPreview.Source = _previewImage;
                    PreviewBorder.Visibility = Visibility.Visible;
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

        private void BtnDeleteImage_Click(object sender, RoutedEventArgs e) { ResetImageOnly(); TxtImageName.Focus(); }
        private void ResetImageOnly() { _previewImage = null; _sourceFilePath = null; ImgPreview.Source = null; PreviewBorder.Visibility = Visibility.Collapsed; }

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

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_savePath) || !Directory.Exists(_savePath)) { CustomMessageBox.Show("Please select a save directory in Settings first.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (_previewImage == null) return;
            string name = TxtImageName.Text.Trim();
            if (string.IsNullOrEmpty(name)) { TxtImageName.Focus(); return; }
            try
            {
                string baseName = name;
                string imgPath = Path.Combine(_savePath, baseName + _detectedExtension);
                string txtPath = Path.Combine(_savePath, baseName + ".txt");
                int i = 1;
                while (File.Exists(imgPath) || File.Exists(txtPath)) { baseName = $"{name} ({i})"; imgPath = Path.Combine(_savePath, baseName + _detectedExtension); txtPath = Path.Combine(_savePath, baseName + ".txt"); i++; }
                if (_sourceFilePath != null && File.Exists(_sourceFilePath)) File.Copy(_sourceFilePath, imgPath, true);
                else using (var fs = new FileStream(imgPath, FileMode.Create)) { var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(_previewImage)); encoder.Save(fs); }
                string content = $"Positive Prompt:\n{TxtPositive.Text.Trim()}\n\nNegative Prompt:\n{TxtNegative.Text.Trim()}";
                if (!string.IsNullOrEmpty(TxtDescription.Text.Trim())) content += $"\n\nDescription:\n{TxtDescription.Text.Trim()}";
                File.WriteAllText(txtPath, content);
                ResetAll();
            }
            catch (Exception ex) { CustomMessageBox.Show($"Error saving: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void ResetAll() { ResetImageOnly(); TxtImageName.Clear(); TxtPositive.Clear(); TxtNegative.Clear(); TxtDescription.Clear(); TxtImageName.Focus(); }
        private void ChkDescription_CheckedChanged(object sender, RoutedEventArgs e) { if (DescriptionPanel != null) DescriptionPanel.Visibility = ChkDescription.IsChecked == true ? Visibility.Visible : Visibility.Collapsed; }
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
    }
}
