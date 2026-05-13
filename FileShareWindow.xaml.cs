using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Diagnostics;

namespace imgsaver
{
    public partial class FileShareWindow : Window
    {
        private readonly FileShareServer _server;

        public FileShareWindow()
        {
            InitializeComponent();
            _server = new FileShareServer();
            _server.StatusChanged += Server_StatusChanged;
            _server.FileReceived += Server_FileReceived;
        }

        private void Server_StatusChanged(string status)
        {
            Dispatcher.Invoke(() =>
            {
                TxtStatusLabel.Text = status;
                if (_server.IsRunning)
                {
                    TxtIpDisplay.Visibility = Visibility.Visible;
                    TxtIpDisplay.Text = $"http://{_server.GetLocalIPAddress()}:9896";
                    TxtStatusLabel.Text = "Server State: Running";
                    TxtStatusLabel.Foreground = (System.Windows.Media.Brush)FindResource("SuccessBrush");
                }
                else
                {
                    TxtIpDisplay.Visibility = Visibility.Collapsed;
                    TxtStatusLabel.Text = "Server State: Stopped";
                    TxtStatusLabel.Foreground = (System.Windows.Media.Brush)FindResource("ForegroundMutedBrush");
                }
            });
        }

        private void Server_FileReceived(string fileName)
        {
            Dispatcher.Invoke(() =>
            {
                // You could show a notification here if you had a Notification system
                Debug.WriteLine($"Received file: {fileName}");
            });
        }

        private void ToggleServer_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ToggleServer.IsChecked == true)
                {
                    _server.Start();
                }
                else
                {
                    _server.Stop();
                }
            }
            catch (Exception ex)
            {
                ToggleServer.IsChecked = false;
                CustomMessageBox.Show(ex.Message, "Server Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "share");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            Process.Start("explorer.exe", path);
        }

        private void BtnCopyUrl_Click(object sender, RoutedEventArgs e)
        {
            if (_server.IsRunning)
            {
                System.Windows.Clipboard.SetText(TxtIpDisplay.Text);
                CustomMessageBox.Show("URL copied to clipboard!", "Cloud Link", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                CustomMessageBox.Show("Please start the server first.", "Cloud Link", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            _server.Stop();
            Close();
        }
    }
}
