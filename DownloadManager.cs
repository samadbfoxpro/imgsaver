using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace imgsaver
{
    public enum DownloadStatus
    {
        Pending,
        Downloading,
        Paused,
        Completed,
        Failed,
        Cancelled
    }

    public class DownloadManagerSettings
    {
        public int PartCount { get; set; } = 4;
        public string DownloadFolder { get; set; } = "";
    }

    internal class DownloadPersistedTask
    {
        public string FileName { get; set; } = "";
        public string FilePath { get; set; } = "";
        public string Url { get; set; } = "";
        public long TotalSize { get; set; }
        public long DownloadedSize { get; set; }
        public DownloadStatus Status { get; set; }
        public string Category { get; set; } = "Other";
        public DateTime StartTime { get; set; }
        public Dictionary<string, string>? RequestHeaders { get; set; }
        public string TempFolder { get; set; } = "";
    }

    public class DownloadTask : INotifyPropertyChanged
    {
        private string _fileName = "";
        private long _totalSize;
        private long _downloadedSize;
        private double _progress;
        private double _speed;
        private DownloadStatus _status;
        private string _statusText = "";
        private string _remainingTime = "";
        private int _activePartCount;
        private DateTime _startTime;
        private DateTime _pauseTime;
        private TimeSpan _totalPausedTime;
        private CancellationTokenSource? _cancellationToken;
        private HttpClient? _httpClient;

        public string FileName
        {
            get => _fileName;
            set { if (_fileName != value) { _fileName = value; OnPropertyChanged(nameof(FileName)); } }
        }

        public string FilePath { get; set; } = "";
        public string Url { get; set; } = "";
        public string TempFolder { get; set; } = "";
        public string DisplayUrl
        {
            get
            {
                try { return new Uri(Url).Host; }
                catch { return Url; }
            }
        }

        public string DownloadedSizeText => FormatSize(DownloadedSize);
        public string TotalSizeText => TotalSize > 0 ? FormatSize(TotalSize) : "Unknown";
        public string SpeedText => Speed > 0 ? $"{FormatSize((long)Speed)}/s" : "0 B/s";
        public string ProgressText => TotalSize > 0 ? $"{Progress:0.0}%" : "Preparing";
        public string PartSummaryText => Parts.Count > 1 ? $"{CompletedPartCount}/{Parts.Count} done - {ActivePartCount} active" : "Single part";
        public int CompletedPartCount => Parts.Count(p => p.TotalBytes > 0 && p.DownloadedBytes >= p.TotalBytes);

        public int ActivePartCount
        {
            get => _activePartCount;
            private set
            {
                if (_activePartCount != value)
                {
                    _activePartCount = value;
                    OnPropertyChanged(nameof(ActivePartCount));
                    OnPropertyChanged(nameof(PartSummaryText));
                }
            }
        }

        public long TotalSize
        {
            get => _totalSize;
            set
            {
                if (_totalSize != value)
                {
                    _totalSize = value;
                    OnPropertyChanged(nameof(TotalSize));
                    OnPropertyChanged(nameof(TotalSizeText));
                    OnPropertyChanged(nameof(ProgressText));
                    OnPropertyChanged(nameof(PartSummaryText));
                }
            }
        }

        public long DownloadedSize
        {
            get => _downloadedSize;
            set
            {
                if (_downloadedSize != value)
                {
                    _downloadedSize = value;
                    OnPropertyChanged(nameof(DownloadedSize));
                    OnPropertyChanged(nameof(DownloadedSizeText));
                    OnPropertyChanged(nameof(PartSummaryText));
                }
            }
        }

        public double Progress
        {
            get => _progress;
            set
            {
                if (Math.Abs(_progress - value) > 0.01)
                {
                    _progress = value;
                    OnPropertyChanged(nameof(Progress));
                    OnPropertyChanged(nameof(ProgressText));
                }
            }
        }

        public double Speed
        {
            get => _speed;
            set
            {
                if (Math.Abs(_speed - value) > 0.1)
                {
                    _speed = value;
                    OnPropertyChanged(nameof(Speed));
                    OnPropertyChanged(nameof(SpeedText));
                }
            }
        }

        public DownloadStatus Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged(nameof(Status));
                    OnPropertyChanged(nameof(PauseResumeIcon));
                    OnPropertyChanged(nameof(CanPauseResume));
                    OnPropertyChanged(nameof(CanCancel));
                }
            }
        }

        public string StatusText
        {
            get => _statusText;
            set { if (_statusText != value) { _statusText = value; OnPropertyChanged(nameof(StatusText)); } }
        }

        public string RemainingTime
        {
            get => _remainingTime;
            set { if (_remainingTime != value) { _remainingTime = value; OnPropertyChanged(nameof(RemainingTime)); } }
        }

        public DateTime StartTime
        {
            get => _startTime;
            set { if (_startTime != value) { _startTime = value; OnPropertyChanged(nameof(StartTime)); } }
        }

        public int Priority { get; set; } = 0;
        public string Category { get; set; } = "Other";
        public Dictionary<string, string>? RequestHeaders { get; set; }
        public ObservableCollection<DownloadPartProgress> Parts { get; } = new();
        public bool IsCompleted => Status == DownloadStatus.Completed;
        public string PauseResumeIcon => Status == DownloadStatus.Downloading ? "||" : ">";
        public bool CanPauseResume => Status == DownloadStatus.Downloading || Status == DownloadStatus.Paused || Status == DownloadStatus.Failed;
        public bool CanCancel => Status == DownloadStatus.Downloading || Status == DownloadStatus.Paused || Status == DownloadStatus.Pending || Status == DownloadStatus.Failed;

        public string CategoryIcon => Category switch
        {
            "Image" => "IMG",
            "Video" => "VID",
            "Audio" => "AUD",
            "Document" => "DOC",
            "Archive" => "ZIP",
            "Executable" => "EXE",
            _ => "GET"
        };

        public void Cancel()
        {
            _cancellationToken?.Cancel();
            Status = DownloadStatus.Cancelled;
            StatusText = "Cancelled";
        }

        public void Pause()
        {
            if (Status == DownloadStatus.Downloading)
            {
                _pauseTime = DateTime.Now;
                Status = DownloadStatus.Paused;
                StatusText = "Paused";
            }
        }

        public void Resume()
        {
            if (Status == DownloadStatus.Paused || Status == DownloadStatus.Failed)
            {
                if (_pauseTime != default)
                    _totalPausedTime += DateTime.Now - _pauseTime;
                Status = DownloadStatus.Pending;
                StatusText = "Waiting...";
            }
        }

        public async Task Download(int partCount, Action<DownloadTask>? onProgress = null, Action<DownloadTask>? onCompleted = null)
        {
            EnsurePartSlots(Math.Clamp(partCount, 1, 32));
            _cancellationToken = new CancellationTokenSource();
            Status = DownloadStatus.Downloading;
            StatusText = "Downloading...";
            if (StartTime == default) StartTime = DateTime.Now;
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath) ?? AppDomain.CurrentDomain.BaseDirectory);
            Directory.CreateDirectory(TempFolder);

            var handler = new SocketsHttpHandler
            {
                MaxConnectionsPerServer = Math.Max(32, partCount + 4),
                PooledConnectionLifetime = TimeSpan.FromMinutes(10)
            };
            _httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(5) };
            AddDefaultHeaders(_httpClient);

            try
            {
                var metadata = await GetServerMetadata(_httpClient, _cancellationToken.Token);
                if (metadata.TotalSize > 0) TotalSize = metadata.TotalSize;

                if (metadata.SupportsRanges && TotalSize > 0 && partCount > 1)
                    await DownloadMultipart(Math.Clamp(partCount, 1, 32), onProgress, _cancellationToken.Token);
                else
                {
                    EnsurePartSlots(1);
                    await DownloadSinglePart(onProgress, _cancellationToken.Token);
                }

                if (Status == DownloadStatus.Cancelled) return;

                Status = DownloadStatus.Completed;
                StatusText = "Completed";
                if (TotalSize > 0)
                    DownloadedSize = TotalSize;
                Progress = 100;
                Speed = 0;
                onCompleted?.Invoke(this);
            }
            catch (OperationCanceledException)
            {
                if (Status != DownloadStatus.Cancelled)
                {
                    Status = DownloadStatus.Paused;
                    StatusText = "Paused";
                }
            }
            catch (Exception ex)
            {
                Status = DownloadStatus.Failed;
                StatusText = $"Error: {ex.Message}";
            }
            finally
            {
                _httpClient?.Dispose();
                _cancellationToken?.Dispose();
            }
        }

        private void AddDefaultHeaders(HttpClient client)
        {
            if (RequestHeaders == null) return;

            foreach (var header in RequestHeaders)
            {
                if (!IsAsciiHeaderName(header.Key) || string.IsNullOrEmpty(header.Value))
                    continue;

                var value = SanitizeHeaderValue(header.Value);
                if (!string.IsNullOrEmpty(value))
                    client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, value);
            }
        }

        private static bool IsAsciiHeaderName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            return value.All(c => c > 32 && c < 127 && "()<>@,;:\\\"/[]?={} \t".IndexOf(c) < 0);
        }

        private static string SanitizeHeaderValue(string value)
        {
            if (value.All(IsAsciiHeaderValueChar))
                return value;

            return new string(value
                .Where(c => c == '\t' || (c >= 32 && c < 127))
                .ToArray());
        }

        private static bool IsAsciiHeaderValueChar(char c)
        {
            return c == '\t' || c == '\r' || c == '\n' || (c >= 32 && c < 127);
        }

        private async Task<(long TotalSize, bool SupportsRanges)> GetServerMetadata(HttpClient client, CancellationToken token)
        {
            long totalSize = 0;
            var supportsRanges = false;

            try
            {
                using var headRequest = new HttpRequestMessage(HttpMethod.Head, Url);
                using var headResponse = await client.SendAsync(headRequest, HttpCompletionOption.ResponseHeadersRead, token);

                if (headResponse.IsSuccessStatusCode)
                {
                    totalSize = headResponse.Content.Headers.ContentLength ?? 0;
                    supportsRanges =
                        headResponse.Headers.AcceptRanges.Any(r => r.Equals("bytes", StringComparison.OrdinalIgnoreCase)) ||
                        headResponse.Headers.Contains("Accept-Ranges");
                }
            }
            catch (HttpRequestException)
            {
                supportsRanges = false;
            }

            if (!supportsRanges)
            {
                try
                {
                    using var rangeRequest = new HttpRequestMessage(HttpMethod.Get, Url);
                    rangeRequest.Headers.Range = new RangeHeaderValue(0, 0);
                    using var rangeResponse = await client.SendAsync(rangeRequest, HttpCompletionOption.ResponseHeadersRead, token);

                    supportsRanges = rangeResponse.StatusCode == HttpStatusCode.PartialContent;
                    if (supportsRanges)
                    {
                        totalSize = rangeResponse.Content.Headers.ContentRange?.Length ??
                            rangeResponse.Content.Headers.ContentLength ??
                            totalSize;
                    }
                    else if (totalSize <= 0 && rangeResponse.IsSuccessStatusCode)
                    {
                        totalSize = rangeResponse.Content.Headers.ContentLength ?? 0;
                    }
                }
                catch (HttpRequestException)
                {
                    supportsRanges = false;
                }
            }

            return (totalSize, supportsRanges);
        }

        private async Task DownloadSinglePart(Action<DownloadTask>? onProgress, CancellationToken token)
        {
            var tempFile = Path.Combine(TempFolder, "single.part");
            var existingBytes = File.Exists(tempFile) ? new FileInfo(tempFile).Length : 0;
            using var request = new HttpRequestMessage(HttpMethod.Get, Url);

            if (existingBytes > 0)
                request.Headers.Range = new RangeHeaderValue(existingBytes, null);

            using var response = await _httpClient!.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.PartialContent)
                throw new HttpRequestException($"HTTP {(int)response.StatusCode}");

            if (TotalSize <= 0)
                TotalSize = (response.Content.Headers.ContentLength ?? 0) + existingBytes;

            SetPartTotal(0, TotalSize);
            SetPartState(0, "Downloading");
            using var contentStream = await response.Content.ReadAsStreamAsync(token);
            using var fileStream = new FileStream(tempFile, FileMode.Append, FileAccess.Write, FileShare.Read, 81920, true);
            await CopyStreamWithProgress(contentStream, fileStream, existingBytes, TotalSize, onProgress, token);
            SetPartState(0, "Done");
            SetPartSpeed(0, 0);

            File.Copy(tempFile, FilePath, true);
            TryDeleteDirectory(TempFolder);
        }

        private async Task DownloadMultipart(int partCount, Action<DownloadTask>? onProgress, CancellationToken token)
        {
            var ranges = BuildRanges(TotalSize, partCount).ToList();
            EnsurePartSlots(ranges.Count);
            for (int i = 0; i < ranges.Count; i++)
            {
                SetPartTotal(i, ranges[i].End - ranges[i].Start + 1);
                SetPartState(i, "Ready");
            }

            var sw = Stopwatch.StartNew();
            var lastDownloaded = GetDownloadedPartBytes(ranges.Count, syncPartProgress: true);
            DownloadedSize = lastDownloaded;

            var tasks = ranges.Select((range, index) => DownloadPart(index, range.Start, range.End, token)).ToArray();
            var allParts = Task.WhenAll(tasks);

            while (!allParts.IsCompleted)
            {
                while (Status == DownloadStatus.Paused)
                    await Task.Delay(250, token);

                await Task.Delay(500, token);
                var current = Parts.Take(ranges.Count).Sum(p => p.DownloadedBytes);
                UpdateProgress(current, TotalSize, sw, lastDownloaded, onProgress);
                lastDownloaded = current;
            }

            await allParts;
            ActivePartCount = 0;
            for (int i = 0; i < ranges.Count; i++)
                SetPartState(i, "Done");
            UpdateProgress(TotalSize, TotalSize, sw, lastDownloaded, onProgress);
            await MergeParts(ranges.Count, token);
            TryDeleteDirectory(TempFolder);
        }

        private async Task DownloadPart(int index, long start, long end, CancellationToken token)
        {
            var partFile = Path.Combine(TempFolder, $"{index}.part");
            var existingBytes = File.Exists(partFile) ? new FileInfo(partFile).Length : 0;
            SetPartDownloaded(index, existingBytes);
            SetPartState(index, existingBytes > 0 ? "Resuming" : "Connecting");
            var nextStart = start + existingBytes;
            if (nextStart > end)
            {
                SetPartState(index, "Done");
                return;
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, Url);
            request.Headers.Range = new RangeHeaderValue(nextStart, end);

            using var response = await _httpClient!.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            if (response.StatusCode != HttpStatusCode.PartialContent)
                throw new HttpRequestException($"Server ignored byte range for part {index + 1} (HTTP {(int)response.StatusCode})");

            using var contentStream = await response.Content.ReadAsStreamAsync(token);
            using var fileStream = new FileStream(partFile, FileMode.Append, FileAccess.Write, FileShare.Read, 81920, true);
            var buffer = new byte[81920];
            var partSw = Stopwatch.StartNew();
            var lastPartBytes = existingBytes;
            var lastPartUpdate = partSw.Elapsed;
            int bytesRead;
            SetPartState(index, "Downloading");

            while ((bytesRead = await contentStream.ReadAsync(buffer, token)) > 0)
            {
                while (Status == DownloadStatus.Paused)
                    await Task.Delay(250, token);

                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), token);
                SetPartDownloaded(index, fileStream.Length);
                if (partSw.Elapsed - lastPartUpdate >= TimeSpan.FromMilliseconds(500))
                {
                    var delta = fileStream.Length - lastPartBytes;
                    SetPartSpeed(index, Math.Max(0, delta / Math.Max(0.001, (partSw.Elapsed - lastPartUpdate).TotalSeconds)));
                    lastPartBytes = fileStream.Length;
                    lastPartUpdate = partSw.Elapsed;
                }
            }

            SetPartDownloaded(index, fileStream.Length);
            SetPartSpeed(index, 0);
            SetPartState(index, "Done");
        }

        private async Task MergeParts(int partCount, CancellationToken token)
        {
            using var output = new FileStream(FilePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
            for (int i = 0; i < partCount; i++)
            {
                var partFile = Path.Combine(TempFolder, $"{i}.part");
                using var input = new FileStream(partFile, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
                await input.CopyToAsync(output, token);
            }
        }

        private async Task CopyStreamWithProgress(Stream input, Stream output, long initialBytes, long totalBytes, Action<DownloadTask>? onProgress, CancellationToken token)
        {
            var buffer = new byte[81920];
            var totalRead = initialBytes;
            var sw = Stopwatch.StartNew();
            var lastRead = totalRead;
            var lastUpdate = sw.Elapsed;
            int bytesRead;

            while ((bytesRead = await input.ReadAsync(buffer, token)) > 0)
            {
                while (Status == DownloadStatus.Paused)
                    await Task.Delay(250, token);

                await output.WriteAsync(buffer.AsMemory(0, bytesRead), token);
                totalRead += bytesRead;

                if (sw.Elapsed - lastUpdate >= TimeSpan.FromMilliseconds(500))
                {
                    SetPartDownloaded(0, totalRead);
                    SetPartSpeed(0, Math.Max(0, (totalRead - lastRead) / Math.Max(0.001, (sw.Elapsed - lastUpdate).TotalSeconds)));
                    UpdateProgress(totalRead, totalBytes, sw, lastRead, onProgress);
                    lastRead = totalRead;
                    lastUpdate = sw.Elapsed;
                }
            }

            SetPartDownloaded(0, totalRead);
        }

        private void UpdateProgress(long downloaded, long total, Stopwatch sw, long previousDownloaded, Action<DownloadTask>? onProgress)
        {
            DownloadedSize = downloaded;
            Progress = total > 0 ? downloaded * 100.0 / total : 0;
            Speed = Math.Max(0, (downloaded - previousDownloaded) / 0.5);

            if (Speed > 0 && total > 0)
            {
                var remainingSeconds = (total - downloaded) / Speed;
                RemainingTime = FormatTimespan(TimeSpan.FromSeconds(Math.Max(0, remainingSeconds)));
            }

            onProgress?.Invoke(this);
        }

        public void EnsurePartSlots(int count)
        {
            count = Math.Clamp(count, 1, 32);
            while (Parts.Count < count)
                Parts.Add(new DownloadPartProgress { PartNumber = Parts.Count + 1 });
            while (Parts.Count > count)
                Parts.RemoveAt(Parts.Count - 1);
        }

        private void SetPartTotal(int index, long totalBytes)
        {
            if (index < 0 || index >= Parts.Count) return;
            Parts[index].TotalBytes = totalBytes;
            OnPropertyChanged(nameof(PartSummaryText));
        }

        private void SetPartDownloaded(int index, long downloadedBytes)
        {
            if (index < 0 || index >= Parts.Count) return;
            Parts[index].DownloadedBytes = downloadedBytes;
            OnPropertyChanged(nameof(CompletedPartCount));
            OnPropertyChanged(nameof(PartSummaryText));
        }

        private void SetPartState(int index, string state)
        {
            if (index < 0 || index >= Parts.Count) return;
            Parts[index].State = state;
            ActivePartCount = Parts.Count(p => p.State is "Connecting" or "Resuming" or "Downloading");
        }

        private void SetPartSpeed(int index, double bytesPerSecond)
        {
            if (index < 0 || index >= Parts.Count) return;
            Parts[index].Speed = bytesPerSecond;
        }

        private long GetDownloadedPartBytes(int partCount, bool syncPartProgress = false)
        {
            long total = 0;
            for (int i = 0; i < partCount; i++)
            {
                var partFile = Path.Combine(TempFolder, $"{i}.part");
                if (File.Exists(partFile))
                {
                    var length = new FileInfo(partFile).Length;
                    total += length;
                    if (syncPartProgress)
                        SetPartDownloaded(i, length);
                }
            }
            return total;
        }

        private static IEnumerable<(long Start, long End)> BuildRanges(long totalSize, int partCount)
        {
            var partSize = totalSize / partCount;
            for (int i = 0; i < partCount; i++)
            {
                var start = i * partSize;
                var end = i == partCount - 1 ? totalSize - 1 : start + partSize - 1;
                yield return (start, end);
            }
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch { }
        }

        public string FormatSize(long bytes)
        {
            string[] suffix = { "B", "KB", "MB", "GB" };
            int i = 0;
            double value = bytes;
            while (value >= 1024 && i < suffix.Length - 1)
            {
                value /= 1024;
                i++;
            }
            return $"{value:0.##} {suffix[i]}";
        }

        private string FormatTimespan(TimeSpan ts)
        {
            if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}";
            if (ts.TotalMinutes >= 1) return $"{(int)ts.TotalMinutes}:{ts.Seconds:D2}";
            return $"{ts.Seconds}s";
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class DownloadPartProgress : INotifyPropertyChanged
    {
        private long _downloadedBytes;
        private long _totalBytes;
        private double _speed;
        private string _state = "Waiting";

        public int PartNumber { get; set; }
        public string PartLabel => $"Part {PartNumber}";
        public string DownloadedText => FormatSize(DownloadedBytes);
        public string TotalText => TotalBytes > 0 ? FormatSize(TotalBytes) : "Unknown";
        public string ProgressText => TotalBytes > 0 ? $"{Progress:0}%" : "--";
        public string SpeedText => Speed > 0 ? $"{FormatSize((long)Speed)}/s" : "";

        public long DownloadedBytes
        {
            get => _downloadedBytes;
            set
            {
                if (_downloadedBytes != value)
                {
                    _downloadedBytes = value;
                    OnPropertyChanged(nameof(DownloadedBytes));
                    OnPropertyChanged(nameof(Progress));
                    OnPropertyChanged(nameof(DownloadedText));
                    OnPropertyChanged(nameof(ProgressText));
                }
            }
        }

        public long TotalBytes
        {
            get => _totalBytes;
            set
            {
                if (_totalBytes != value)
                {
                    _totalBytes = value;
                    OnPropertyChanged(nameof(TotalBytes));
                    OnPropertyChanged(nameof(Progress));
                    OnPropertyChanged(nameof(TotalText));
                    OnPropertyChanged(nameof(ProgressText));
                }
            }
        }

        public double Speed
        {
            get => _speed;
            set
            {
                if (Math.Abs(_speed - value) > 0.1)
                {
                    _speed = value;
                    OnPropertyChanged(nameof(Speed));
                    OnPropertyChanged(nameof(SpeedText));
                }
            }
        }

        public string State
        {
            get => _state;
            set
            {
                if (_state != value)
                {
                    _state = value;
                    OnPropertyChanged(nameof(State));
                }
            }
        }

        public double Progress => TotalBytes > 0 ? Math.Min(100, DownloadedBytes * 100.0 / TotalBytes) : 0;

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private static string FormatSize(long bytes)
        {
            string[] suffix = { "B", "KB", "MB", "GB" };
            int i = 0;
            double value = bytes;
            while (value >= 1024 && i < suffix.Length - 1)
            {
                value /= 1024;
                i++;
            }
            return $"{value:0.##} {suffix[i]}";
        }
    }

    public class DownloadManagerService
    {
        private readonly string _dataFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
        private readonly string _defaultDownloadFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "downloads");
        private readonly string _tempRootFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "download_temp");
        private readonly string _historyFile;
        private readonly string _queueFile;
        private readonly string _settingsFile;
        private readonly List<DownloadTask> _activeDownloads = new();
        private readonly List<DownloadTask> _history = new();
        private readonly SemaphoreSlim _downloadSemaphore = new(3, 3);

        public event Action<DownloadTask>? OnDownloadAdded;
        public event Action<DownloadTask>? OnDownloadCompleted;
        public event Action<DownloadTask>? OnDownloadFailed;

        public DownloadManagerSettings Settings { get; private set; } = new();
        public string DownloadFolder => Settings.DownloadFolder;
        public string TempRootFolder => _tempRootFolder;

        public DownloadManagerService()
        {
            _historyFile = Path.Combine(_dataFolder, "download_history.json");
            _queueFile = Path.Combine(_dataFolder, "download_queue.json");
            _settingsFile = Path.Combine(_dataFolder, "download_settings.json");

            Directory.CreateDirectory(_dataFolder);
            Directory.CreateDirectory(_defaultDownloadFolder);
            Directory.CreateDirectory(_tempRootFolder);

            LoadSettings();
            LoadHistory();
            LoadQueue();
        }

        public DownloadTask AddDownload(string url, string? fileName = null, Dictionary<string, string>? requestHeaders = null, string? destinationPath = null)
        {
            fileName ??= Path.GetFileName(new Uri(url).LocalPath);
            if (string.IsNullOrWhiteSpace(fileName))
                fileName = $"download_{Guid.NewGuid():N}.bin";

            var filePath = !string.IsNullOrWhiteSpace(destinationPath)
                ? destinationPath
                : GetUniqueFilePath(Path.Combine(Settings.DownloadFolder, fileName));

            var task = new DownloadTask
            {
                Url = url,
                FileName = Path.GetFileName(filePath),
                FilePath = filePath,
                TempFolder = CreateTempFolder(),
                RequestHeaders = requestHeaders,
                Category = CategorizeFile(fileName),
                Status = DownloadStatus.Pending,
                StatusText = "Waiting...",
                StartTime = DateTime.Now
            };
            task.EnsurePartSlots(Settings.PartCount);

            _activeDownloads.Add(task);
            SaveQueue();
            OnDownloadAdded?.Invoke(task);
            StartDownload(task);
            return task;
        }

        public void ResumeDownload(DownloadTask task)
        {
            if (!_activeDownloads.Contains(task) || task.Status == DownloadStatus.Downloading)
                return;

            task.Resume();
            SaveQueue();
            StartDownload(task);
        }

        public void CancelDownload(DownloadTask task)
        {
            task.Cancel();
            _activeDownloads.Remove(task);
            TryDeleteDirectory(task.TempFolder);
            SaveQueue();
        }

        public void UpdateSettings(int partCount, string downloadFolder)
        {
            Settings.PartCount = Math.Clamp(partCount, 1, 32);
            Settings.DownloadFolder = string.IsNullOrWhiteSpace(downloadFolder) ? _defaultDownloadFolder : downloadFolder;
            Directory.CreateDirectory(Settings.DownloadFolder);
            SaveSettings();
        }

        public bool HasDownload(string url)
        {
            return _activeDownloads.Any(d =>
                    string.Equals(d.Url, url, StringComparison.OrdinalIgnoreCase) &&
                    d.Status is DownloadStatus.Pending or DownloadStatus.Downloading or DownloadStatus.Paused) ||
                _history.Any(d =>
                    string.Equals(d.Url, url, StringComparison.OrdinalIgnoreCase) &&
                    d.Status == DownloadStatus.Completed);
        }

        private async void StartDownload(DownloadTask task)
        {
            await _downloadSemaphore.WaitAsync();
            try
            {
                if (task.Status == DownloadStatus.Cancelled || task.Status == DownloadStatus.Completed)
                    return;

                await task.Download(
                    Settings.PartCount,
                    onProgress: _ => SaveQueue(),
                    onCompleted: t =>
                    {
                        _activeDownloads.Remove(t);
                        _history.RemoveAll(h => string.Equals(h.Url, t.Url, StringComparison.OrdinalIgnoreCase));
                        _history.Add(t);
                        SaveQueue();
                        SaveHistory();
                        OnDownloadCompleted?.Invoke(t);
                    });

                if (task.Status == DownloadStatus.Failed)
                {
                    SaveQueue();
                    OnDownloadFailed?.Invoke(task);
                }
                else if (task.Status == DownloadStatus.Paused)
                {
                    SaveQueue();
                }
            }
            finally
            {
                _downloadSemaphore.Release();
            }
        }

        public IEnumerable<DownloadTask> GetActiveDownloads() => _activeDownloads.Where(d =>
            d.Status == DownloadStatus.Downloading || d.Status == DownloadStatus.Paused || d.Status == DownloadStatus.Pending || d.Status == DownloadStatus.Failed);

        public IEnumerable<DownloadTask> GetCompletedDownloads() => _history.Where(d => d.Status == DownloadStatus.Completed);

        public IEnumerable<DownloadTask> GetAllHistory() => _history.OrderByDescending(d => d.StartTime);

        public void ClearHistory()
        {
            _history.Clear();
            SaveHistory();
        }

        public long GetTotalDownloaded() => _history.Where(d => d.IsCompleted).Sum(d => d.TotalSize);

        public void OpenDownloadFolder()
        {
            Directory.CreateDirectory(Settings.DownloadFolder);
            Process.Start(new ProcessStartInfo { FileName = Settings.DownloadFolder, UseShellExecute = true });
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsFile))
                {
                    Settings = JsonSerializer.Deserialize<DownloadManagerSettings>(File.ReadAllText(_settingsFile)) ?? new DownloadManagerSettings();
                }
            }
            catch
            {
                Settings = new DownloadManagerSettings();
            }

            if (string.IsNullOrWhiteSpace(Settings.DownloadFolder))
                Settings.DownloadFolder = _defaultDownloadFolder;

            Settings.PartCount = Math.Clamp(Settings.PartCount, 1, 32);
            Directory.CreateDirectory(Settings.DownloadFolder);
            SaveSettings();
        }

        private void SaveSettings()
        {
            try
            {
                File.WriteAllText(_settingsFile, JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        private void SaveQueue()
        {
            try
            {
                var data = _activeDownloads.Select(ToPersisted).ToList();
                File.WriteAllText(_queueFile, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        private void LoadQueue()
        {
            try
            {
                if (!File.Exists(_queueFile)) return;
                var data = JsonSerializer.Deserialize<List<DownloadPersistedTask>>(File.ReadAllText(_queueFile)) ?? new();
                foreach (var persisted in data.Where(d => d.Status != DownloadStatus.Completed && d.Status != DownloadStatus.Cancelled))
                {
                    var task = FromPersisted(persisted);
                    task.EnsurePartSlots(Settings.PartCount);
                    task.Status = DownloadStatus.Paused;
                    task.StatusText = "Paused";
                    _activeDownloads.Add(task);
                }
            }
            catch { }
        }

        private void SaveHistory()
        {
            try
            {
                var data = _history.Select(ToPersisted).ToList();
                File.WriteAllText(_historyFile, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { }
        }

        private void LoadHistory()
        {
            try
            {
                if (!File.Exists(_historyFile)) return;
                var data = JsonSerializer.Deserialize<List<DownloadPersistedTask>>(File.ReadAllText(_historyFile)) ?? new();
                foreach (var item in data)
                    _history.Add(FromPersisted(item));
            }
            catch { }
        }

        private DownloadPersistedTask ToPersisted(DownloadTask task) => new()
        {
            FileName = task.FileName,
            FilePath = task.FilePath,
            Url = task.Url,
            TotalSize = task.TotalSize,
            DownloadedSize = task.DownloadedSize,
            Status = task.Status,
            Category = task.Category,
            StartTime = task.StartTime,
            RequestHeaders = task.RequestHeaders,
            TempFolder = task.TempFolder
        };

        private DownloadTask FromPersisted(DownloadPersistedTask persisted) => new()
        {
            FileName = persisted.FileName,
            FilePath = persisted.FilePath,
            Url = persisted.Url,
            TotalSize = persisted.TotalSize,
            DownloadedSize = persisted.DownloadedSize,
            Progress = persisted.TotalSize > 0 ? persisted.DownloadedSize * 100.0 / persisted.TotalSize : 0,
            Status = persisted.Status,
            StatusText = persisted.Status.ToString(),
            Category = persisted.Category,
            StartTime = persisted.StartTime,
            RequestHeaders = persisted.RequestHeaders,
            TempFolder = string.IsNullOrWhiteSpace(persisted.TempFolder) ? CreateTempFolder() : persisted.TempFolder
        };

        private string CreateTempFolder()
        {
            var path = Path.Combine(_tempRootFolder, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private string GetUniqueFilePath(string filePath)
        {
            if (!File.Exists(filePath)) return filePath;

            var directory = Path.GetDirectoryName(filePath) ?? Settings.DownloadFolder;
            var name = Path.GetFileNameWithoutExtension(filePath);
            var extension = Path.GetExtension(filePath);
            var index = 1;
            string candidate;

            do
            {
                candidate = Path.Combine(directory, $"{name} ({index++}){extension}");
            }
            while (File.Exists(candidate));

            return candidate;
        }

        private string CategorizeFile(string fileName)
        {
            var ext = Path.GetExtension(fileName).ToLower();
            return ext switch
            {
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".bmp" => "Image",
                ".mp4" or ".mkv" or ".avi" or ".mov" or ".flv" => "Video",
                ".mp3" or ".wav" or ".flac" or ".aac" or ".m4a" => "Audio",
                ".pdf" or ".doc" or ".docx" or ".txt" or ".xls" or ".xlsx" => "Document",
                ".zip" or ".rar" or ".7z" or ".tar" or ".gz" => "Archive",
                ".exe" or ".msi" or ".app" or ".deb" => "Executable",
                _ => "Other"
            };
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch { }
        }
    }
}
