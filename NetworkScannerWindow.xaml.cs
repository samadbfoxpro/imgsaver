using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;

namespace imgsaver
{
    public partial class NetworkScannerWindow : Window
    {
        private sealed class ScanItem
        {
            public string Address { get; set; } = "";
            public string Status { get; set; } = "Pending";
            public string ConnectionText { get; set; } = "";
            public bool Responded => string.Equals(Status, "Up", StringComparison.OrdinalIgnoreCase);
        }

        private sealed class ScanResult
        {
            public string Address { get; set; } = "";
            public bool Responded { get; set; }
            public long ConnectionTimeMs { get; set; }
            public string? Error { get; set; }
        }

        private readonly BindingList<ScanItem> _items = new BindingList<ScanItem>();
        private readonly List<string> _respondedPreview = new List<string>();
        private readonly ConcurrentQueue<ScanResult> _pendingResults = new ConcurrentQueue<ScanResult>();
        private readonly DispatcherTimer _uiFlushTimer;
        private CancellationTokenSource? _scanCts;
        private StreamWriter? _respondedWriter;
        private string? _respondedTempFile;
        private const int MaxVisibleRows = 5000;
        private const int MaxRespondedTextRows = 5000;
        private const int MaxLogLines = 500;
        private const int MaxUiDrainPerTick = 1000;
        private const int RandomBatchSize = 100;
        private const int DefaultTimeoutMs = 1500;
        private const int MinTimeoutMs = 300;
        private const int MaxTimeoutMs = 10000;
        private const int HttpsPort = 443;
        private const int DefaultMaxConcurrentConnections = 75;
        private const int MinMaxConcurrentConnections = 1;
        private const int MaxMaxConcurrentConnections = 500;
        private const int DefaultPerIpDelayMs = 50;
        private const int MinPerIpDelayMs = 0;
        private const int MaxPerIpDelayMs = 2000;
        private const int DefaultBatchRestSeconds = 10;
        private const int MinBatchRestSeconds = 0;
        private const int MaxBatchRestSeconds = 120;
        private const double MinPageScale = 0.7;
        private const double MaxPageScale = 1.35;
        private const double PageScaleStep = 0.08;
        private int _totalScanned;
        private int _respondedCount;
        private int _logLines;
        private int _connectTimeoutMs = DefaultTimeoutMs;
        private int _maxConcurrentConnections = DefaultMaxConcurrentConnections;
        private int _perIpDelayMs = DefaultPerIpDelayMs;
        private int _batchRestSeconds = DefaultBatchRestSeconds;
        private bool _isApplyingWindowState;

        public NetworkScannerWindow()
        {
            InitializeComponent();
            LvResults.ItemsSource = _items;
            _uiFlushTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _uiFlushTimer.Tick += UiFlushTimer_Tick;
            Closing += (_, _) => {
                _scanCts?.Cancel();
                CloseRespondedWriter();
            };
            TxtIpList.Text = "8.8.8.8\n1.1.1.1\n192.168.1.1";
            TxtRangeStart.Text = "192.168.1.1";
            TxtRangeEnd.Text = "192.168.1.254";
            TxtTimeoutMs.Text = DefaultTimeoutMs.ToString();
            TxtMaxConcurrentConnections.Text = DefaultMaxConcurrentConnections.ToString();
            TxtPerIpDelayMs.Text = DefaultPerIpDelayMs.ToString();
            TxtBatchRestSeconds.Text = DefaultBatchRestSeconds.ToString();
            UpdateCounts();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) BtnMaximize_Click(sender, e);
            else DragMove();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                SystemCommands.RestoreWindow(this);
            }
            else
            {
                SystemCommands.MaximizeWindow(this);
            }
        }

        private void Window_StateChanged(object? sender, EventArgs e)
        {
            if (_isApplyingWindowState) return;
            ApplyWindowStateChrome();
        }

        private void ApplyWindowStateChrome()
        {
            _isApplyingWindowState = true;
            try
            {
                if (WindowState == WindowState.Maximized)
                {
                    MainBorder.Margin = new Thickness(0);
                    MainBorder.CornerRadius = new CornerRadius(0);
                    MainBorder.Effect = null;
                }
                else
                {
                    MainBorder.Margin = new Thickness(0);
                    MainBorder.CornerRadius = new CornerRadius(0);
                    MainBorder.Effect = null;
                }
            }
            finally
            {
                _isApplyingWindowState = false;
            }
        }

        private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control) return;

            var nextScale = PageScaleTransform.ScaleX + (e.Delta > 0 ? PageScaleStep : -PageScaleStep);
            nextScale = Math.Clamp(nextScale, MinPageScale, MaxPageScale);
            PageScaleTransform.ScaleX = nextScale;
            PageScaleTransform.ScaleY = nextScale;
            e.Handled = true;
        }

        private async void BtnScan_Click(object sender, RoutedEventArgs e)
        {
            _scanCts?.Cancel();
            _scanCts = new CancellationTokenSource();
            var token = _scanCts.Token;

            BtnCancel.IsEnabled = true;
            BtnScan.IsEnabled = false;
            BtnCopyResponded.IsEnabled = false;
            _connectTimeoutMs = GetPingTimeoutMs();
            _maxConcurrentConnections = GetMaxConcurrentConnections();
            _perIpDelayMs = GetPerIpDelayMs();
            _batchRestSeconds = GetBatchRestSeconds();
            _items.Clear();
            _respondedPreview.Clear();
            while (_pendingResults.TryDequeue(out _)) { }
            _totalScanned = 0;
            _respondedCount = 0;
            _logLines = 0;
            TxtTotal.Text = "0";
            TxtRespondedIps.Clear();
            TxtLog.Clear();
            PrepareRespondedWriter();
            _uiFlushTimer.Start();

            try
            {
                await ScanCurrentSourceAsync(token);
            }
            catch (OperationCanceledException)
            {
                AppendLog("Scan canceled.");
            }
            finally
            {
                DrainPendingResults();
                _uiFlushTimer.Stop();
                CloseRespondedWriter();
                BtnCancel.IsEnabled = false;
                BtnScan.IsEnabled = true;
                UpdateRespondedText();
                UpdateCounts();
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            _scanCts?.Cancel();
        }

        private void BtnCopyResponded_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(_respondedTempFile) && File.Exists(_respondedTempFile))
            {
                _respondedWriter?.Flush();
                using var stream = new FileStream(_respondedTempFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                System.Windows.Clipboard.SetText(reader.ReadToEnd());
            }
            else if (_respondedPreview.Count > 0)
            {
                System.Windows.Clipboard.SetText(string.Join(Environment.NewLine, _respondedPreview));
            }
        }

        private void BtnBrowseFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new WinForms.OpenFileDialog
            {
                Filter = "Text files|*.txt;*.csv;*.log|All files|*.*",
                Title = "Select IP list file"
            };

            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                TxtFilePath.Text = dialog.FileName;
            }
        }

        private void ScanSource_Checked(object sender, RoutedEventArgs e)
        {
            if (ManualPanel == null || FilePanel == null || RangePanel == null) return;
            ManualPanel.Visibility = RbManual.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            FilePanel.Visibility = RbFile.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            RangePanel.Visibility = RbRange.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private async Task ScanCurrentSourceAsync(CancellationToken token)
        {
            if (RbManual.IsChecked == true)
            {
                var addresses = TxtIpList.Text
                    .Split(new[] { '\r', '\n', ',', ';', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                    .Distinct(StringComparer.OrdinalIgnoreCase);
                await ScanAddressesAsync(addresses, token);
                return;
            }

            if (RbFile.IsChecked == true)
            {
                if (string.IsNullOrWhiteSpace(TxtFilePath.Text) || !File.Exists(TxtFilePath.Text))
                {
                    CustomMessageBox.Show("Please select a valid text file.", "Network Scanner", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                await ScanAddressesAsync(ReadAddressesFromFile(TxtFilePath.Text, token), token);
                return;
            }

            if (!TryParseIpv4(TxtRangeStart.Text, out uint start) || !TryParseIpv4(TxtRangeEnd.Text, out uint end) || start > end)
            {
                CustomMessageBox.Show("Please enter a valid IPv4 range.", "Network Scanner", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await ScanAddressesAsync(ReadAddressesFromRange(start, end, token), token);
        }

        private async Task ScanAddressesAsync(IEnumerable<string> addresses, CancellationToken token)
        {
            AppendLog("Loading IP list...");
            var randomList = addresses
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (randomList.Count == 0)
            {
                CustomMessageBox.Show("No valid IP or host found.", "Network Scanner", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Shuffle(randomList);
            AppendLog($"Loaded {randomList.Count:N0} targets. Scanning random batches of {RandomBatchSize:N0}.");
            AppendLog($"Timeout: {_connectTimeoutMs:N0} ms. Delay/IP: {_perIpDelayMs:N0} ms. Batch rest: {_batchRestSeconds:N0} seconds.");

            for (int index = 0; index < randomList.Count; index += RandomBatchSize)
            {
                token.ThrowIfCancellationRequested();
                var batch = randomList.Skip(index).Take(RandomBatchSize).ToList();
                AppendLog($"Random batch {(index / RandomBatchSize) + 1:N0}: scanning {batch.Count:N0} targets...");
                using var semaphore = new SemaphoreSlim(_maxConcurrentConnections, _maxConcurrentConnections);
                var running = new List<Task>();
                foreach (var address in batch)
                {
                    token.ThrowIfCancellationRequested();
                    await semaphore.WaitAsync(token);
                    running.Add(ScanOneAddressAsync(address, semaphore, token));
                    if (_perIpDelayMs > 0)
                    {
                        await Task.Delay(_perIpDelayMs, token);
                    }
                }

                await Task.WhenAll(running);
                DrainPendingResults();

                if (index + RandomBatchSize < randomList.Count && _batchRestSeconds > 0)
                {
                    AppendLog($"Resting {_batchRestSeconds:N0} seconds before next batch...");
                    await Task.Delay(TimeSpan.FromSeconds(_batchRestSeconds), token);
                }
            }
        }

        private IEnumerable<string> ReadAddressesFromFile(string filePath, CancellationToken token)
        {
            foreach (var line in File.ReadLines(filePath))
            {
                token.ThrowIfCancellationRequested();
                foreach (var part in line.Split(new[] { ',', ';', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    yield return part;
                }
            }
        }

        private IEnumerable<string> ReadAddressesFromRange(uint start, uint end, CancellationToken token)
        {
            for (uint current = start; current <= end; current++)
            {
                token.ThrowIfCancellationRequested();
                yield return FormatIpv4(current);
                if (current == uint.MaxValue) yield break;
            }
        }

        private async Task ScanOneAddressAsync(string address, SemaphoreSlim semaphore, CancellationToken token)
        {
            try
            {
                await ScanOneAsync(address, token);
            }
            finally
            {
                semaphore.Release();
            }
        }

        private async Task ScanOneAsync(string address, CancellationToken token)
        {
            try
            {
                using var tcpClient = new TcpClient();
                var stopwatch = Stopwatch.StartNew();
                var connectTask = tcpClient.ConnectAsync(address, HttpsPort);
                var timeoutTask = Task.Delay(_connectTimeoutMs, token);
                var completedTask = await Task.WhenAny(connectTask, timeoutTask);

                if (completedTask != connectTask)
                {
                    token.ThrowIfCancellationRequested();
                    throw new TimeoutException($"TCP connect to port {HttpsPort} timed out after {_connectTimeoutMs:N0} ms.");
                }

                await connectTask;
                stopwatch.Stop();

                _pendingResults.Enqueue(new ScanResult
                {
                    Address = address,
                    Responded = true,
                    ConnectionTimeMs = stopwatch.ElapsedMilliseconds
                });
            }
            catch (Exception ex) when (ex is SocketException or TimeoutException or OperationCanceledException)
            {
                if (ex is OperationCanceledException) throw;
                _pendingResults.Enqueue(new ScanResult
                {
                    Address = address,
                    Responded = false,
                    Error = ex.Message
                });
            }
        }

        private void AddVisibleItem(ScanItem item)
        {
            if (_items.Count < MaxVisibleRows || item.Responded)
            {
                _items.Add(item);
            }
        }

        private void UiFlushTimer_Tick(object? sender, EventArgs e)
        {
            DrainPendingResults();
        }

        private void DrainPendingResults()
        {
            var drained = 0;
            var listChanged = false;

            while (drained < MaxUiDrainPerTick && _pendingResults.TryDequeue(out var result))
            {
                drained++;
                _totalScanned++;

                var item = new ScanItem
                {
                    Address = result.Address,
                    Status = result.Responded ? "Up" : "Down",
                    ConnectionText = result.Responded ? $"{result.ConnectionTimeMs} ms" : "-"
                };

                if (result.Responded)
                {
                    _respondedCount++;
                    if (_respondedPreview.Count < MaxRespondedTextRows) _respondedPreview.Add(result.Address);
                    _respondedWriter?.WriteLine(result.Address);
                    AppendLog($"{result.Address} responded in {result.ConnectionTimeMs} ms");
                }
                else if (_totalScanned <= 200)
                {
                    AppendLog(result.Error == null ? $"{result.Address} did not respond" : $"{result.Address} error: {result.Error}");
                }

                AddVisibleItem(item);
                listChanged = true;
            }

            if (listChanged)
            {
                LvResults.Items.Refresh();
                UpdateRespondedText();
                UpdateCounts();
            }
        }

        private void UpdateCounts()
        {
            TxtTotal.Text = _totalScanned.ToString("N0");
            TxtResponded.Text = _respondedCount.ToString("N0");
            TxtWhitelisted.Text = _respondedCount.ToString("N0");
            BtnCopyResponded.IsEnabled = _respondedCount > 0;
        }

        private void UpdateRespondedText()
        {
            TxtRespondedIps.Text = string.Join(Environment.NewLine, _respondedPreview);
            if (_respondedCount > MaxRespondedTextRows)
            {
                TxtRespondedIps.AppendText($"{Environment.NewLine}... {_respondedCount - MaxRespondedTextRows:N0} more saved and copied by COPY UP");
            }
        }

        private void AppendLog(string message)
        {
            if (_logLines >= MaxLogLines) return;
            _logLines++;
            TxtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            if (_logLines == MaxLogLines)
            {
                TxtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] Log paused after {MaxLogLines:N0} lines to keep UI fast.{Environment.NewLine}");
            }
            TxtLog.ScrollToEnd();
        }

        private void PrepareRespondedWriter()
        {
            CloseRespondedWriter();
            var dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
            Directory.CreateDirectory(dataDir);
            _respondedTempFile = Path.Combine(dataDir, $"network_scanner_responded_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            var stream = new FileStream(_respondedTempFile, FileMode.Create, FileAccess.Write, FileShare.Read, 1024 * 64);
            _respondedWriter = new StreamWriter(stream, Encoding.UTF8, 1024 * 64);
            AppendLog($"Responded IPs will be streamed to: {_respondedTempFile}");
        }

        private void CloseRespondedWriter()
        {
            _respondedWriter?.Flush();
            _respondedWriter?.Dispose();
            _respondedWriter = null;
        }

        private static bool TryParseIpv4(string value, out uint result)
        {
            result = 0;
            var parts = value.Split('.');
            if (parts.Length != 4) return false;

            foreach (var part in parts)
            {
                if (!byte.TryParse(part, out var octet)) return false;
                result = (result << 8) + octet;
            }

            return true;
        }

        private static string FormatIpv4(uint value)
        {
            return $"{(value >> 24) & 255}.{(value >> 16) & 255}.{(value >> 8) & 255}.{value & 255}";
        }

        private int GetPingTimeoutMs()
        {
            return GetBoundedInt(TxtTimeoutMs, DefaultTimeoutMs, MinTimeoutMs, MaxTimeoutMs);
        }

        private int GetMaxConcurrentConnections()
        {
            return GetBoundedInt(TxtMaxConcurrentConnections, DefaultMaxConcurrentConnections, MinMaxConcurrentConnections, MaxMaxConcurrentConnections);
        }

        private int GetPerIpDelayMs()
        {
            return GetBoundedInt(TxtPerIpDelayMs, DefaultPerIpDelayMs, MinPerIpDelayMs, MaxPerIpDelayMs);
        }

        private int GetBatchRestSeconds()
        {
            return GetBoundedInt(TxtBatchRestSeconds, DefaultBatchRestSeconds, MinBatchRestSeconds, MaxBatchRestSeconds);
        }

        private static int GetBoundedInt(System.Windows.Controls.TextBox textBox, int defaultValue, int min, int max)
        {
            if (!int.TryParse(textBox.Text.Trim(), out var value))
            {
                textBox.Text = defaultValue.ToString();
                return defaultValue;
            }

            value = Math.Clamp(value, min, max);
            textBox.Text = value.ToString();
            return value;
        }

        private static void Shuffle<T>(IList<T> list)
        {
            var random = new Random();
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
