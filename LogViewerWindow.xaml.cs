using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Color = System.Windows.Media.Color;
using WpfClipboard = System.Windows.Clipboard;

namespace imgsaver
{
    public partial class LogViewerWindow : Window
    {
        private readonly ObservableCollection<LogEntry> _displayedLogs = new ObservableCollection<LogEntry>();
        private ICollectionView _collectionView;
        private string _selectedCategory = "ALL";
        private string _searchKeyword = string.Empty;
        private DispatcherTimer? _recordingPulseTimer;
        private bool _pulseToggle = false;
        private bool _isLoaded = false;

        public LogViewerWindow()
        {
            InitializeComponent();
            _collectionView = CollectionViewSource.GetDefaultView(_displayedLogs);
            _collectionView.Filter = FilterLogEntry;
            ListLogs.ItemsSource = _collectionView;

            Loaded += LogViewerWindow_Loaded;
            Unloaded += LogViewerWindow_Unloaded;
        }

        private void LogViewerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = true;
            AppLogManager.LogAdded += OnLogAdded;
            AppLogManager.LogsCleared += OnLogsCleared;
            AppLogManager.RecordingStateChanged += OnRecordingStateChanged;

            // Load existing logs
            ReloadAllLogs();
            UpdateRecordingUI(AppLogManager.IsRecording);
            InitPulseTimer();
        }

        private void LogViewerWindow_Unloaded(object sender, RoutedEventArgs e)
        {
            AppLogManager.LogAdded -= OnLogAdded;
            AppLogManager.LogsCleared -= OnLogsCleared;
            AppLogManager.RecordingStateChanged -= OnRecordingStateChanged;

            _recordingPulseTimer?.Stop();
            _recordingPulseTimer = null;
        }

        private void InitPulseTimer()
        {
            _recordingPulseTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(650)
            };
            _recordingPulseTimer.Tick += (s, e) =>
            {
                if (AppLogManager.IsRecording)
                {
                    _pulseToggle = !_pulseToggle;
                    RecordingIndicatorDot.Background = _pulseToggle 
                        ? new SolidColorBrush(Color.FromRgb(239, 68, 68)) 
                        : new SolidColorBrush(Color.FromRgb(75, 85, 99));
                    RecordingIndicatorGlow.Opacity = _pulseToggle ? 0.9 : 0.2;
                }
            };
            _recordingPulseTimer.Start();
        }

        private void ReloadAllLogs()
        {
            _displayedLogs.Clear();
            var entries = AppLogManager.GetEntries();
            foreach (var entry in entries)
            {
                _displayedLogs.Add(entry);
            }
            UpdateCounts();

            if (ChkAutoScroll.IsChecked == true && _displayedLogs.Count > 0)
            {
                ListLogs.ScrollIntoView(_displayedLogs.Last());
            }
        }

        private void OnLogAdded(LogEntry entry)
        {
            Dispatcher.InvokeAsync(() =>
            {
                _displayedLogs.Add(entry);
                UpdateCounts();

                if (ChkAutoScroll.IsChecked == true)
                {
                    ListLogs.ScrollIntoView(entry);
                }
            }, DispatcherPriority.Background);
        }

        private void OnLogsCleared()
        {
            Dispatcher.InvokeAsync(() =>
            {
                _displayedLogs.Clear();
                TxtDetailMeta.Text = string.Empty;
                TxtDetailContent.Text = string.Empty;
                UpdateCounts();
            });
        }

        private void OnRecordingStateChanged(bool isRecording)
        {
            Dispatcher.InvokeAsync(() =>
            {
                UpdateRecordingUI(isRecording);
            });
        }

        private void UpdateRecordingUI(bool isRecording)
        {
            if (TxtRecordingStatus == null || RecordingIndicatorDot == null || RecordingIndicatorGlow == null || BtnToggleRecord == null || TxtRecordIcon == null || TxtRecordBtnLabel == null) return;

            if (isRecording)
            {
                TxtRecordingStatus.Text = "وضعیت: در حال ضبط رویدادها...";
                TxtRecordingStatus.Foreground = new SolidColorBrush(Color.FromRgb(248, 113, 113));
                RecordingIndicatorDot.Background = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                RecordingIndicatorGlow.Opacity = 0.85;

                BtnToggleRecord.Background = new SolidColorBrush(Color.FromRgb(55, 20, 26));
                BtnToggleRecord.BorderBrush = new SolidColorBrush(Color.FromRgb(220, 38, 38));
                BtnToggleRecord.Foreground = new SolidColorBrush(Color.FromRgb(254, 202, 202));
                TxtRecordIcon.Text = "⏹";
                TxtRecordBtnLabel.Text = "توقف ضبط لاگ";
            }
            else
            {
                TxtRecordingStatus.Text = "وضعیت: ضبط متوقف است";
                TxtRecordingStatus.Foreground = (System.Windows.Media.Brush)FindResource("ForegroundMutedBrush");
                RecordingIndicatorDot.Background = new SolidColorBrush(Color.FromRgb(75, 85, 99));
                RecordingIndicatorGlow.Opacity = 0;

                BtnToggleRecord.Background = new SolidColorBrush(Color.FromRgb(31, 35, 43));
                BtnToggleRecord.BorderBrush = new SolidColorBrush(Color.FromRgb(45, 51, 63));
                BtnToggleRecord.Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225));
                TxtRecordIcon.Text = "⏺";
                TxtRecordBtnLabel.Text = "شروع ضبط لاگ";
            }
        }

        private bool FilterLogEntry(object obj)
        {
            if (obj is not LogEntry entry) return false;

            // Category filter
            if (_selectedCategory != "ALL")
            {
                if (!string.Equals(entry.Category, _selectedCategory, StringComparison.OrdinalIgnoreCase))
                {
                    // Special case for Browser/Bridge
                    if (_selectedCategory == "Browser" && string.Equals(entry.Category, "Bridge", StringComparison.OrdinalIgnoreCase))
                    {
                        // include
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            // Text search keyword filter
            if (!string.IsNullOrWhiteSpace(_searchKeyword))
            {
                string kw = _searchKeyword.Trim();
                bool matches = (!string.IsNullOrEmpty(entry.Action) && entry.Action.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                               (!string.IsNullOrEmpty(entry.Message) && entry.Message.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                               (!string.IsNullOrEmpty(entry.Payload) && entry.Payload.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                               (!string.IsNullOrEmpty(entry.Category) && entry.Category.Contains(kw, StringComparison.OrdinalIgnoreCase));
                if (!matches) return false;
            }

            return true;
        }

        private void Filter_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;

            if (RadioFilterAll?.IsChecked == true) _selectedCategory = "ALL";
            else if (RadioFilterMiniClip?.IsChecked == true) _selectedCategory = "MiniClip";
            else if (RadioFilterBrowser?.IsChecked == true) _selectedCategory = "Browser";
            else if (RadioFilterCombiner?.IsChecked == true) _selectedCategory = "Combiner";
            else if (RadioFilterSystem?.IsChecked == true) _selectedCategory = "System";

            _collectionView?.Refresh();
            UpdateCounts();
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isLoaded) return;
            _searchKeyword = TxtSearch?.Text ?? string.Empty;
            _collectionView?.Refresh();
            UpdateCounts();
        }

        private void UpdateCounts()
        {
            if (TxtTotalCount == null || TxtFilteredCount == null || _displayedLogs == null) return;
            int total = _displayedLogs.Count;
            int filtered = _collectionView?.Cast<object>().Count() ?? total;
            TxtTotalCount.Text = $"کل رویدادها: {total}";
            TxtFilteredCount.Text = $"نمایش داده شده: {filtered}";
        }

        private void ListLogs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListLogs.SelectedItem is LogEntry entry)
            {
                TxtDetailMeta.Text = $"[{entry.TimeString}] | بخش: {entry.Category} | وضعیت: {entry.Level} | کد: #{entry.Id}";
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"عملیات: {entry.Action}");
                sb.AppendLine($"زمان: {entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}");
                sb.AppendLine($"پیام: {entry.Message}");
                if (!string.IsNullOrWhiteSpace(entry.Payload))
                {
                    sb.AppendLine(new string('-', 60));
                    sb.AppendLine("محتوا / Payload:");
                    sb.AppendLine(entry.Payload);
                }
                TxtDetailContent.Text = sb.ToString();
            }
        }

        private void BtnToggleRecord_Click(object sender, RoutedEventArgs e)
        {
            AppLogManager.IsRecording = !AppLogManager.IsRecording;
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            bool res = DarkConfirmDialog.ShowConfirm("پاکسازی لاگ‌ها", "آیا مطمئن هستید که می‌خواهید تمام لاگ‌های ثبت‌شده را پاک کنید؟", this, true, "پاک کردن", "انصراف");
            if (res)
            {
                AppLogManager.Clear();
            }
        }

        private void BtnSaveToFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "ذخیره فایل گزارش لاگ‌ها",
                    Filter = "فایل متنی (*.txt)|*.txt|همه فایل‌ها (*.*)|*.*",
                    FileName = $"imgsaver_log_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
                };

                if (dlg.ShowDialog(this) == true)
                {
                    string path = AppLogManager.SaveToFile(dlg.FileName);
                    TxtLastSavedPath.Text = $"✓ در فایل ذخیره شد: {Path.GetFileName(path)}";
                    CustomMessageBox.Show($"گزارش لاگ‌ها با موفقیت ذخیره شد:\n{path}", "ذخیره لاگ");
                }
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"خطا در ذخیره فایل لاگ: {ex.Message}", "خطا");
            }
        }

        private void BtnOpenLogsFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string logsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                if (!Directory.Exists(logsDir))
                {
                    Directory.CreateDirectory(logsDir);
                }
                Process.Start(new ProcessStartInfo
                {
                    FileName = logsDir,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"خطا در باز کردن پوشه: {ex.Message}", "خطا");
            }
        }

        private void BtnCopyAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string text = AppLogManager.ExportToText();
                WpfClipboard.SetText(text);
                TxtLastSavedPath.Text = "✓ تمام لاگ‌ها در کلیپ‌بورد کپی شد.";
                CustomMessageBox.Show("تمامی لاگ‌ها با موفقیت در کلیپ‌بورد کپی شدند.", "کپی لاگ‌ها");
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"خطا در کپی: {ex.Message}", "خطا");
            }
        }

        private void BtnCopyDetail_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(TxtDetailContent.Text))
                {
                    WpfClipboard.SetText(TxtDetailContent.Text);
                    TxtLastSavedPath.Text = "✓ جزئیات رویداد در کلیپ‌بورد کپی شد.";
                }
            }
            catch { }
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void BtnMaximize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
    }
}
