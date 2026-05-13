using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using WinForms = System.Windows.Forms;

namespace imgsaver
{
    public partial class ClipboardSaverWindow : Window
    {
        private string _clipboardSavePath = "";
        private int _imageCount = 0;
        private const int MaxImages = 50;
        private const string ClipboardConfigFileName = "clipboard_config.txt";

        public ClipboardSaverWindow()
        {
            InitializeComponent();
            LoadClipboardSettings();
            UpdateCounter();
            
            // Focus the window to enable Ctrl+V
            Loaded += (_, _) => this.Focus();
            
            // Handle Ctrl+V globally in this window
            this.PreviewKeyDown += Window_PreviewKeyDown;
        }

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Check for Ctrl+V
            if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                HandleClipboardPaste();
                e.Handled = true;
            }
        }

        private void HandleClipboardPaste()
        {
            // Check if path is set
            if (string.IsNullOrEmpty(_clipboardSavePath) || !Directory.Exists(_clipboardSavePath))
            {
                System.Windows.MessageBox.Show("Please select a storage folder first.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Check if limit reached
            if (_imageCount >= MaxImages)
            {
                System.Windows.MessageBox.Show($"Maximum of {MaxImages} images saved.\nClear session to continue.", "Limit Reached", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                BitmapSource? imageToSave = null;
                string? sourceFilePath = null;
                string extension = ".png";

                // Try to get image from clipboard (file drop)
                if (System.Windows.Clipboard.ContainsFileDropList())
                {
                    var fileList = System.Windows.Clipboard.GetFileDropList();
                    if (fileList.Count > 0)
                    {
                        string filePath = fileList[0];
                        if (IsImageFile(filePath))
                        {
                            sourceFilePath = filePath;
                            extension = Path.GetExtension(filePath).ToLower();
                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.UriSource = new Uri(filePath);
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.EndInit();
                            imageToSave = bitmap;
                        }
                    }
                }

                // Try to get image from clipboard (direct image)
                if (imageToSave == null && System.Windows.Clipboard.ContainsImage())
                {
                    imageToSave = System.Windows.Clipboard.GetImage();
                    extension = ".png";
                }

                if (imageToSave == null)
                {
                    System.Windows.MessageBox.Show("No image found in clipboard.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Generate file name with sequential number
                _imageCount++;
                string fileName = $"{_imageCount:D3}{extension}"; // 001.png, 002.png, etc.
                string fullPath = Path.Combine(_clipboardSavePath, fileName);

                // Save the image
                if (sourceFilePath != null && File.Exists(sourceFilePath))
                {
                    // Copy from source file
                    File.Copy(sourceFilePath, fullPath, overwrite: true);
                }
                else
                {
                    // Save from clipboard image
                    using (var fs = new FileStream(fullPath, FileMode.Create))
                    {
                        BitmapEncoder encoder;
                        if (extension == ".jpg" || extension == ".jpeg")
                            encoder = new JpegBitmapEncoder();
                        else if (extension == ".bmp")
                            encoder = new BmpBitmapEncoder();
                        else
                            encoder = new PngBitmapEncoder();

                        encoder.Frames.Add(BitmapFrame.Create(imageToSave));
                        encoder.Save(fs);
                    }
                }

                // Update UI
                UpdateCounter();
                ShowPreview(imageToSave);
                TxtStatus.Text = $"Saved: {fileName}";

                // Flash effect (optional)
                FlashStatus();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error saving: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FlashStatus()
        {
            // Simple flash animation for status
            var originalOpacity = TxtStatus.Opacity;
            TxtStatus.Opacity = 1.0;
            
            var timer = new System.Windows.Threading.DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(100);
            int count = 0;
            timer.Tick += (s, e) =>
            {
                count++;
                TxtStatus.Opacity = count % 2 == 0 ? 1.0 : 0.5;
                if (count >= 4)
                {
                    timer.Stop();
                    TxtStatus.Opacity = originalOpacity;
                }
            };
            timer.Start();
        }

        private void ShowPreview(BitmapSource image)
        {
            ImgPreview.Source = image;
            ImgPreview.Visibility = Visibility.Visible;
            PlaceholderPanel.Visibility = Visibility.Collapsed;
        }

        private void UpdateCounter()
        {
            TxtCounter.Text = $"{_imageCount} / {MaxImages}";
            TxtNextNumber.Text = _imageCount < MaxImages 
                ? $"Next: {(_imageCount + 1):D3}.png" 
                : "Limit Reached";

            // Change color when approaching limit
            if (_imageCount >= MaxImages)
            {
                TxtCounter.Foreground = (System.Windows.Media.Brush)FindResource("ErrorBrush");
            }
            else if (_imageCount >= MaxImages * 0.8) // 80% = 40 images
            {
                TxtCounter.Foreground = (System.Windows.Media.Brush)FindResource("SecondaryBrush");
            }
            else
            {
                TxtCounter.Foreground = (System.Windows.Media.Brush)FindResource("PrimaryBrush");
            }
        }

        private bool IsImageFile(string path)
        {
            string ext = Path.GetExtension(path).ToLower();
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".webp" || ext == ".bmp" || ext == ".tiff";
        }

        private void LoadClipboardSettings()
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ClipboardConfigFileName);
                if (File.Exists(configPath))
                {
                    string savedPath = File.ReadAllText(configPath).Trim();
                    if (Directory.Exists(savedPath))
                    {
                        _clipboardSavePath = savedPath;
                        TxtClipboardPath.Text = _clipboardSavePath;
                    }
                }
            }
            catch { }
        }

        private void SaveClipboardSettings(string path)
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ClipboardConfigFileName);
                File.WriteAllText(configPath, path);
            }
            catch { }
        }

        // Window Controls
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void BtnMiniMode_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_clipboardSavePath) || !Directory.Exists(_clipboardSavePath))
            {
                System.Windows.MessageBox.Show("Please select a storage folder first.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var mini = new MiniQuickSaverWindow(_clipboardSavePath);
            mini.Show();
            this.Close();
        }

        // Path Selection
        private void BtnSelectClipboardFolder_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new WinForms.FolderBrowserDialog();
            if (!string.IsNullOrEmpty(_clipboardSavePath)) 
                dialog.SelectedPath = _clipboardSavePath;
            
            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                _clipboardSavePath = dialog.SelectedPath;
                TxtClipboardPath.Text = _clipboardSavePath;
                SaveClipboardSettings(_clipboardSavePath);
                TxtStatus.Text = "Path Updated";
            }
        }

        // Clear Session
        private void BtnClearSession_Click(object sender, RoutedEventArgs e)
        {
            var result = System.Windows.MessageBox.Show(
                "Do you want to reset the counter?\nPrevious images will not be deleted, the counter will reset to 001.",
                "Clear Session",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _imageCount = 0;
                UpdateCounter();
                ImgPreview.Source = null;
                ImgPreview.Visibility = Visibility.Collapsed;
                PlaceholderPanel.Visibility = Visibility.Visible;
                TxtStatus.Text = "Session Cleared";
            }
        }
    }
}
