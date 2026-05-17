using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace imgsaver
{
    public partial class DownloadManagerWindow : Window
    {
        private DownloadManagerService _downloadService = null!;
        private readonly ObservableCollection<DownloadTask> _displayedDownloads = new();
        private DispatcherTimer _updateTimer = null!;
        private bool _showingActive = true;

        public DownloadManagerWindow(DownloadManagerService downloadService)
        {
            InitializeComponent();
            _downloadService = downloadService;
            InitializeDownloadService();
            InitializeUI();
            UpdateUI();
        }

        private void UpdateUI()
        {
            UpdateStats();
            UpdateEmptyState();
        }

        private void InitializeDownloadService()
        {
            _downloadService.OnDownloadAdded += Download_Added;
            _downloadService.OnDownloadCompleted += Download_Completed;
            _downloadService.OnDownloadFailed += Download_Failed;

            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _updateTimer.Tick += (s, e) => UpdateStats();
            _updateTimer.Start();
        }

        private void InitializeUI()
        {
            LstDownloads.ItemsSource = _displayedDownloads;
            BtnTabActive_Click(this, new RoutedEventArgs());
        }

        private void Download_Added(DownloadTask task)
        {
            Dispatcher.Invoke(() =>
            {
                if (_showingActive && !_displayedDownloads.Contains(task))
                    _displayedDownloads.Add(task);
                UpdateUI();
            });
        }

        private void Download_Completed(DownloadTask task)
        {
            Dispatcher.Invoke(() =>
            {
                if (_showingActive && _displayedDownloads.Contains(task))
                    _displayedDownloads.Remove(task);
                UpdateUI();
            });
        }

        private void Download_Failed(DownloadTask task)
        {
            Dispatcher.Invoke(() =>
            {
                if (_showingActive && !_displayedDownloads.Contains(task))
                    _displayedDownloads.Add(task);
                UpdateUI();
            });
        }

        private void BtnTabActive_Click(object sender, RoutedEventArgs e)
        {
            _showingActive = true;
            var primary = TryFindResource("PrimaryButtonStyle") as Style;
            var secondary = TryFindResource("SecondaryButtonStyle") as Style;
            if (primary != null && secondary != null)
            {
                BtnTabActive.Style = primary;
                BtnTabHistory.Style = secondary;
            }
            RefreshDownloadList();
        }

        private void BtnTabHistory_Click(object sender, RoutedEventArgs e)
        {
            _showingActive = false;
            var primary = TryFindResource("PrimaryButtonStyle") as Style;
            var secondary = TryFindResource("SecondaryButtonStyle") as Style;
            if (primary != null && secondary != null)
            {
                BtnTabActive.Style = secondary;
                BtnTabHistory.Style = primary;
            }
            RefreshDownloadList();
        }

        private void RefreshDownloadList()
        {
            _displayedDownloads.Clear();
            var downloads = _showingActive
                ? _downloadService.GetActiveDownloads()
                : _downloadService.GetCompletedDownloads();

            foreach (var download in downloads)
                _displayedDownloads.Add(download);

            UpdateUI();
        }

        private void UpdateEmptyState()
        {
            if (_displayedDownloads.Count == 0)
            {
                EmptyState.Visibility = Visibility.Visible;
                LstDownloads.Visibility = Visibility.Collapsed;
                TxtEmptyIcon.Text = _showingActive ? "DM" : "OK";
            }
            else
            {
                EmptyState.Visibility = Visibility.Collapsed;
                LstDownloads.Visibility = Visibility.Visible;
            }
        }

        private void UpdateStats()
        {
            var active = _downloadService.GetActiveDownloads().ToList();
            var downloading = active.Count(d => d.Status == DownloadStatus.Downloading);
            var total = _displayedDownloads.Count;

            TxtActiveCount.Text = downloading.ToString();

            if (_showingActive)
            {
                var totalSize = active.Sum(d => d.TotalSize);
                var downloadedSize = active.Sum(d => d.DownloadedSize);
                var totalSpeed = active.Sum(d => d.Speed);

                TxtStats.Text = $"{total} downloads - " +
                    $"{FormatBytes(downloadedSize)} / {FormatBytes(totalSize)} - " +
                    $"{FormatBytes((long)totalSpeed)}/s - " +
                    $"Parts: {_downloadService.Settings.PartCount} - " +
                    $"Proxy: {_downloadService.ProxySettings.DisplayText} - " +
                    _downloadService.DownloadFolder;
            }
            else
            {
                var totalDownloaded = _downloadService.GetTotalDownloaded();
                TxtStats.Text = $"Total downloaded: {FormatBytes(totalDownloaded)} - {total} files";
            }
        }

        private void BtnPauseResume_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if ((sender as System.Windows.Controls.Button)?.Tag is DownloadTask task)
                {
                    if (task.Status == DownloadStatus.Downloading)
                        task.Pause();
                    else if (task.Status == DownloadStatus.Paused || task.Status == DownloadStatus.Failed)
                        _downloadService.ResumeDownload(task);

                    RefreshDownloadList();
                }
            }
            catch { }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if ((sender as System.Windows.Controls.Button)?.Tag is DownloadTask task)
                {
                    _downloadService.CancelDownload(task);
                    _displayedDownloads.Remove(task);
                    RefreshDownloadList();
                }
            }
            catch { }
        }

        private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            _downloadService.OpenDownloadFolder();
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.Form
            {
                Text = "Download Settings",
                Width = 560,
                Height = 190,
                StartPosition = System.Windows.Forms.FormStartPosition.CenterParent,
                TopMost = true,
                FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var partsLabel = new System.Windows.Forms.Label { Text = "Parts per download:", Left = 12, Top = 18, Width = 130 };
            var partsInput = new System.Windows.Forms.NumericUpDown
            {
                Left = 150,
                Top = 14,
                Width = 80,
                Minimum = 1,
                Maximum = 32,
                Value = _downloadService.Settings.PartCount
            };

            var folderLabel = new System.Windows.Forms.Label { Text = "Download folder:", Left = 12, Top = 58, Width = 130 };
            var folderInput = new System.Windows.Forms.TextBox
            {
                Left = 150,
                Top = 54,
                Width = 300,
                Text = _downloadService.DownloadFolder
            };

            var browseBtn = new System.Windows.Forms.Button { Text = "Browse", Left = 460, Top = 52, Width = 70 };
            browseBtn.Click += (s, args) =>
            {
                using var folderDialog = new System.Windows.Forms.FolderBrowserDialog
                {
                    SelectedPath = System.IO.Directory.Exists(folderInput.Text) ? folderInput.Text : _downloadService.DownloadFolder
                };

                if (folderDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    folderInput.Text = folderDialog.SelectedPath;
            };

            var okBtn = new System.Windows.Forms.Button { Text = "Save", Left = 370, Top = 100, Width = 75 };
            var cancelBtn = new System.Windows.Forms.Button { Text = "Cancel", Left = 455, Top = 100, Width = 75 };

            okBtn.Click += (s, args) =>
            {
                _downloadService.UpdateSettings((int)partsInput.Value, folderInput.Text);
                dialog.DialogResult = System.Windows.Forms.DialogResult.OK;
                dialog.Close();
            };
            cancelBtn.Click += (s, args) => dialog.Close();

            dialog.Controls.Add(partsLabel);
            dialog.Controls.Add(partsInput);
            dialog.Controls.Add(folderLabel);
            dialog.Controls.Add(folderInput);
            dialog.Controls.Add(browseBtn);
            dialog.Controls.Add(okBtn);
            dialog.Controls.Add(cancelBtn);

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                UpdateStats();
        }

        private void BtnClearHistory_Click(object sender, RoutedEventArgs e)
        {
            if (_showingActive) return;

            if (CustomMessageBox.Show("Clear all download history?", "Clear History", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _downloadService.ClearHistory();
                RefreshDownloadList();
            }
        }

        private void BtnAddDownload_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.SaveFileDialog
            {
                Title = "Select download destination",
                InitialDirectory = _downloadService.DownloadFolder
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                ShowUrlDialog(dialog.FileName);
        }

        private void ShowUrlDialog(string destinationPath)
        {
            var urlDialog = new System.Windows.Forms.Form
            {
                Text = "Enter Download URL",
                Width = 400,
                Height = 150,
                StartPosition = System.Windows.Forms.FormStartPosition.CenterParent,
                TopMost = true
            };

            var label = new System.Windows.Forms.Label { Text = "URL:", Left = 10, Top = 10, Width = 360 };
            var textbox = new System.Windows.Forms.TextBox { Left = 10, Top = 30, Width = 360, Height = 20 };
            var okBtn = new System.Windows.Forms.Button { Text = "OK", Left = 250, Top = 60, Width = 50 };
            var cancelBtn = new System.Windows.Forms.Button { Text = "Cancel", Left = 310, Top = 60, Width = 50 };

            okBtn.Click += (s, e) => { urlDialog.DialogResult = System.Windows.Forms.DialogResult.OK; urlDialog.Close(); };
            cancelBtn.Click += (s, e) => urlDialog.Close();

            urlDialog.Controls.Add(label);
            urlDialog.Controls.Add(textbox);
            urlDialog.Controls.Add(okBtn);
            urlDialog.Controls.Add(cancelBtn);

            if (urlDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(textbox.Text))
            {
                _downloadService.AddDownload(textbox.Text, System.IO.Path.GetFileName(destinationPath), destinationPath: destinationPath);
                RefreshDownloadList();
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double value = bytes;
            while (value >= 1024 && order < sizes.Length - 1)
            {
                order++;
                value /= 1024;
            }
            return $"{value:0.##} {sizes[order]}";
        }

        protected override void OnClosed(EventArgs e)
        {
            _updateTimer?.Stop();
            base.OnClosed(e);
        }
    }
}
