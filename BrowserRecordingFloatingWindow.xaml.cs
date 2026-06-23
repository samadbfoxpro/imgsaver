using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace imgsaver
{
    public partial class BrowserRecordingFloatingWindow : Window
    {
        private bool _isPlaying;

        public BrowserRecordingFloatingWindow()
        {
            InitializeComponent();
            Loaded += (_, _) => PlaceNearWorkAreaCorner();
        }

        private void PlaceNearWorkAreaCorner()
        {
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Right - Width - 24;
            Top = workArea.Bottom - Height - 96;
        }

        private void WindowDrag_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private async void BtnPlayRecording_Click(object sender, RoutedEventArgs e)
        {
            if (_isPlaying) return;

            var browser = System.Windows.Application.Current.Windows
                .OfType<BrowserWindow>()
                .FirstOrDefault(window => window.IsLoaded && window.IsVisible);

            if (browser == null)
            {
                CustomMessageBox.Show("Browser window is not open.", "Browser Recording");
                return;
            }

            await PlayAsync(browser);
        }

        private async Task PlayAsync(BrowserWindow browser)
        {
            _isPlaying = true;
            BtnPlayRecording.IsEnabled = false;
            Topmost = true;

            try
            {
                await browser.PlayBrowserRecordingAsync();
            }
            finally
            {
                BtnPlayRecording.IsEnabled = true;
                _isPlaying = false;
                Topmost = true;
            }
        }
    }
}
