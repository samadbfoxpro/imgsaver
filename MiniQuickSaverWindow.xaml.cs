using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace imgsaver
{
    public partial class MiniQuickSaverWindow : Window
    {
        private string _savePath;
        private HwndSource _hwndSource;
        private IntPtr _nextClipboardViewer;
        private IntPtr _windowHandle;
        private bool _ignoreNextClipboardChange = true;
        private DateTime _lastClipboardTime = DateTime.MinValue;
        private bool _isSaving = false;

        private InputPlayer _miniPlayer = new InputPlayer();

        public static readonly DependencyProperty IsDisabledProperty =
            DependencyProperty.Register("IsDisabled", typeof(bool), typeof(MiniQuickSaverWindow), new PropertyMetadata(false));

        public bool IsDisabled
        {
            get { return (bool)GetValue(IsDisabledProperty); }
            set { SetValue(IsDisabledProperty, value); }
        }


        // Win32 APIs
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetClipboardViewer(IntPtr hWndNewViewer);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ChangeClipboardChain(IntPtr hWndRemove, IntPtr hWndNewNext);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        private const int WM_DRAWCLIPBOARD = 0x0308;
        private const int WM_CHANGECBCHAIN = 0x030D;

        public MiniQuickSaverWindow(string savePath)
        {
            InitializeComponent();
            _savePath = savePath;

            Loaded += MiniQuickSaverWindow_Loaded;
            Closing += MiniQuickSaverWindow_Closing;
        }

        private void MiniQuickSaverWindow_Loaded(object sender, RoutedEventArgs e)
        {
            var helper = new WindowInteropHelper(this);
            _windowHandle = helper.Handle;
            _hwndSource = HwndSource.FromHwnd(_windowHandle);
            _hwndSource?.AddHook(WndProc);

            _ignoreNextClipboardChange = true;
            _nextClipboardViewer = SetClipboardViewer(_windowHandle);
        }

        private void MiniQuickSaverWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_hwndSource != null)
            {
                ChangeClipboardChain(_windowHandle, _nextClipboardViewer);
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
                    if (wParam == _nextClipboardViewer)
                        _nextClipboardViewer = lParam;
                    else if (_nextClipboardViewer != IntPtr.Zero)
                        SendMessage(_nextClipboardViewer, msg, wParam, lParam);
                    break;
            }
            return IntPtr.Zero;
        }

        private void OnClipboardChanged()
        {
            try
            {
                if (IsDisabled) return;

                if (_ignoreNextClipboardChange)
                {
                    _ignoreNextClipboardChange = false;
                    return;
                }

                // Debounce clipboard changes (some apps fire multiple events)
                if ((DateTime.Now - _lastClipboardTime).TotalMilliseconds < 750)
                {
                    return;
                }
                _lastClipboardTime = DateTime.Now;

                if (System.Windows.Clipboard.ContainsImage())
                {
                    var image = System.Windows.Clipboard.GetImage();
                    if (image != null)
                    {
                        SaveImage(image);
                    }
                }
            }
            catch { }
        }

        private void SaveImage(BitmapSource image)
        {
            if (_isSaving) return;
            _isSaving = true;

            try
            {
                if (string.IsNullOrEmpty(_savePath) || !Directory.Exists(_savePath))
                {
                    TxtStatus.Text = "Error: Invalid Path";
                    TxtStatus.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E81123"));
                    return;
                }

                // Show preview
                var convertedBitmap = new FormatConvertedBitmap();
                convertedBitmap.BeginInit();
                convertedBitmap.Source = image;
                convertedBitmap.DestinationFormat = PixelFormats.Bgra32;
                convertedBitmap.EndInit();
                var finalImage = new WriteableBitmap(convertedBitmap);
                finalImage.Freeze();

                ImgPreview.Source = finalImage;
                ImgPreview.Visibility = Visibility.Visible;
                PnlNoImage.Visibility = Visibility.Collapsed;

                // Find next filename
                string fileName = GetNextFileName();
                string fullPath = Path.Combine(_savePath, fileName);

                // Save
                using (var fs = new FileStream(fullPath, FileMode.Create))
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(finalImage));
                    encoder.Save(fs);
                }

                // Update status
                TxtStatus.Text = $"Saved: {fileName}";
                TxtStatus.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#89D185")); // Green

                FlashSuccess();
            }
            catch (Exception ex)
            {
                TxtStatus.Text = "Save Failed!";
                TxtStatus.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#E81123")); // Red
            }
            finally
            {
                _isSaving = false;
            }
        }

        private string GetNextFileName()
        {
            // Simple logic: Image_001.png, Image_002.png etc.
            int i = 1;
            while (true)
            {
                string name = $"QuickSaved_{i:D3}.png";
                if (!File.Exists(Path.Combine(_savePath, name))) return name;
                i++;
            }
        }

        private async void FlashSuccess()
        {
            var originalBg = this.Background;
            // Flash effect can be added here if needed, or handled by XAML animations
            await System.Threading.Tasks.Task.Delay(100);
        }

        private void ImagePreview_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            ImgPreview.Source = null;
            ImgPreview.Visibility = Visibility.Collapsed;
            PnlNoImage.Visibility = Visibility.Visible;
            TxtStatus.Text = "";
        }

        private async void BtnPlayRecordingMini_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Default to Slot 1 for MiniQuickSaver
                if (!RecordingManager.HasEvents(1))
                {
                    TxtStatus.Text = "No recording loaded";
                    return;
                }

                TxtStatus.Text = "Playing recording...";
                _miniPlayer.SetEvents(RecordingManager.GetEvents(1));
                _miniPlayer.SetSpeed(1.0);
                // Play once and do not block UI thread
                _ = _miniPlayer.PlayAsync(loop: false);
            }
            catch { }
        }

        private void BtnDisable_Click(object sender, RoutedEventArgs e)
        {
            IsDisabled = !IsDisabled;
            if (IsDisabled)
            {
                TxtStatus.Text = "Monitoring Paused";
                TxtStatus.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#969696"));
            }
            else
            {
                TxtStatus.Text = "Ready";
                TxtStatus.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#89D185"));
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1)
            {
                DragMove();
            }
        }


        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
