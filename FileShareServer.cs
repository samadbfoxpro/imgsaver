using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace imgsaver
{
    public class FileShareServer
    {
        private readonly ConcurrentDictionary<string, CloudDownloadJob> _remoteDownloads = new();
        private readonly HttpClient _httpClient;
        private HttpListener? _listener;
        private bool _isRunning;
        private const int Port = 9896;
        private const int BufferSize = 1024 * 128;
        private const string GalleryConfigFileName = "gallery_config.txt";
        private static readonly string SharePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "share");
        private static readonly string WebGalleryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "WebGallery");
        private static readonly string[] GalleryImageExtensions = { ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".tiff", ".gif" };

        public event Action<string>? StatusChanged;
        public event Action<string>? FileReceived;

        public bool IsRunning => _isRunning;

        static FileShareServer()
        {
            Directory.CreateDirectory(SharePath);
        }

        public FileShareServer()
        {
            _httpClient = new HttpClient(new HttpClientHandler
            {
                AllowAutoRedirect = true,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
            })
            {
                Timeout = Timeout.InfiniteTimeSpan
            };
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("imgsaver-cloud-link/1.0");
        }

        public void Start()
        {
            if (_isRunning) return;

            try
            {
                _listener = new HttpListener();
                string ip = GetLocalIPAddress();

                _listener.Prefixes.Add($"http://+:{Port}/");
                _listener.Prefixes.Add($"http://*:{Port}/");
                _listener.Prefixes.Add($"http://localhost:{Port}/");
                _listener.Prefixes.Add($"http://{ip}:{Port}/");

                _listener.Start();
                _isRunning = true;

                Task.Run(() => Listen());
                StatusChanged?.Invoke($"File Server active at {ip}:{Port}");
            }
            catch (Exception ex)
            {
                _isRunning = false;
                throw new Exception($"Failed to start File Server (Administrator privileges might be required):\n{ex.Message}");
            }
        }

        public void Stop()
        {
            if (!_isRunning) return;
            _isRunning = false;
            foreach (var job in _remoteDownloads.Values)
                job.Cancel();
            try { _listener?.Stop(); } catch { }
            try { _listener?.Close(); } catch { }
            StatusChanged?.Invoke("File Server stopped");
        }

        private async Task Listen()
        {
            while (_isRunning && _listener != null)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(() => ProcessRequest(context));
                }
                catch when (!_isRunning) { }
                catch { }
            }
        }

        private async Task ProcessRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            try
            {
                string path = WebUtility.UrlDecode(request.Url?.AbsolutePath ?? "").TrimStart('/');

                if (request.HttpMethod == "GET")
                {
                    if (string.IsNullOrEmpty(path))
                        await ServeInterface(response);
                    else if (path.Equals("gallery", StringComparison.OrdinalIgnoreCase))
                        await ServeGalleryInterface(response);
                    else if (path.Equals("gallery/random", StringComparison.OrdinalIgnoreCase))
                        await ServeRandomGalleryInterface(response);
                    else if (path.StartsWith("gallery/assets/", StringComparison.OrdinalIgnoreCase))
                        await ServeWebGalleryAsset(path["gallery/assets/".Length..], request, response);
                    else if (path == "api/files")
                        await ServeFileList(response);
                    else if (path == "api/gallery/months")
                        await ServeGalleryMonths(response);
                    else if (path == "api/gallery/days")
                        await ServeGalleryDays(response);
                    else if (path.StartsWith("api/gallery/images", StringComparison.OrdinalIgnoreCase))
                        await ServeGalleryImages(request, response);
                    else if (path.StartsWith("api/gallery/random", StringComparison.OrdinalIgnoreCase))
                        await ServeGalleryRandom(request, response);
                    else if (path.StartsWith("api/gallery/metadata/", StringComparison.OrdinalIgnoreCase))
                        await ServeGalleryMetadata(path["api/gallery/metadata/".Length..], response);
                    else if (path.StartsWith("gallery/text/", StringComparison.OrdinalIgnoreCase))
                        await ServeGalleryText(path["gallery/text/".Length..], request, response);
                    else if (path.StartsWith("gallery/image/", StringComparison.OrdinalIgnoreCase))
                        await ServeGalleryImage(path["gallery/image/".Length..], request, response);
                    else if (path == "api/remote-downloads")
                        await ServeRemoteDownloads(response);
                    else if (path.StartsWith("download/", StringComparison.OrdinalIgnoreCase))
                        await ServeFile(path["download/".Length..], request, response);
                    else
                        await WriteJson(response, new { status = "error", message = "Not found" }, 404);
                }
                else if (request.HttpMethod == "POST" && path == "api/upload")
                {
                    await HandleUpload(request, response);
                }
                else if (request.HttpMethod == "POST" && path == "api/remote-download")
                {
                    await StartRemoteDownload(request, response);
                }
                else if (request.HttpMethod == "POST" && path == "api/gallery/rename")
                {
                    await RenameGalleryImage(request, response);
                }
                else if (request.HttpMethod == "POST" && path == "api/gallery/delete")
                {
                    await DeleteGalleryImages(request, response);
                }
                else if (request.HttpMethod == "DELETE" && path.StartsWith("api/files/", StringComparison.OrdinalIgnoreCase))
                {
                    await DeleteSharedFile(path["api/files/".Length..], response);
                }
                else if (request.HttpMethod == "DELETE" && path.StartsWith("api/remote-downloads/", StringComparison.OrdinalIgnoreCase))
                {
                    await CancelRemoteDownload(path["api/remote-downloads/".Length..], response);
                }
                else
                {
                    await WriteJson(response, new { status = "error", message = "Not found" }, 404);
                }
            }
            catch (Exception ex)
            {
                try { await WriteJson(response, new { status = "error", message = ex.Message }, 500); } catch { }
            }
            finally
            {
                try { response.Close(); } catch { }
            }
        }

        private async Task ServeInterface(HttpListenerResponse response)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(GetHtmlInterface());
            response.ContentType = "text/html; charset=utf-8";
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        }

        private async Task ServeFileList(HttpListenerResponse response)
        {
            var files = Directory.GetFiles(SharePath)
                .Where(f => !f.EndsWith(".part", StringComparison.OrdinalIgnoreCase))
                .Select(TryCreateFileListItem)
                .Where(f => f != null)
                .OrderByDescending(f => f!.Date)
                .Select(f => f!);

            await WriteJson(response, files);
        }

        private sealed class FileListItem
        {
            public string Name { get; init; } = "";
            public long Size { get; init; }
            public DateTime Date { get; init; }
        }

        private static FileListItem? TryCreateFileListItem(string path)
        {
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists)
                    return null;

                return new FileListItem
                {
                    Name = info.Name,
                    Size = info.Length,
                    Date = info.LastWriteTime
                };
            }
            catch (FileNotFoundException)
            {
                return null;
            }
            catch (DirectoryNotFoundException)
            {
                return null;
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        private async Task ServeRemoteDownloads(HttpListenerResponse response)
        {
            var jobs = _remoteDownloads.Values
                .OrderByDescending(j => j.StartedAt)
                .Select(j => new
                {
                    id = j.Id,
                    fileName = j.FileName,
                    url = j.Url,
                    status = j.Status,
                    downloaded = j.DownloadedBytes,
                    total = j.TotalBytes,
                    speed = j.SpeedBytesPerSecond,
                    progress = j.TotalBytes > 0 ? Math.Round(j.DownloadedBytes * 100.0 / j.TotalBytes, 1) : 0,
                    message = j.Message
                });

            await WriteJson(response, jobs);
        }

        private async Task ServeGalleryInterface(HttpListenerResponse response)
        {
            await ServeWebGalleryFile("gallery-view.html", null, response);
        }

        private async Task ServeRandomGalleryInterface(HttpListenerResponse response)
        {
            await ServeWebGalleryFile("random-gallery.html", null, response);
        }

        private async Task ServeWebGalleryAsset(string assetPath, HttpListenerRequest request, HttpListenerResponse response)
        {
            await ServeWebGalleryFile(assetPath, request, response);
        }

        private async Task ServeWebGalleryFile(string relativePath, HttpListenerRequest? request, HttpListenerResponse response)
        {
            string safeRelative = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            string fullPath = Path.GetFullPath(Path.Combine(WebGalleryPath, safeRelative));
            string webRoot = Path.GetFullPath(WebGalleryPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;

            if (!fullPath.StartsWith(webRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
            {
                response.StatusCode = 404;
                return;
            }

            if (request == null)
            {
                byte[] buffer = await File.ReadAllBytesAsync(fullPath);
                response.ContentType = GetStaticContentType(fullPath);
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                return;
            }

            await ServePhysicalFile(fullPath, request, response, GetStaticContentType(fullPath), null);
        }

        private async Task ServeGalleryMonths(HttpListenerResponse response)
        {
            var images = ScanGalleryImages();
            var months = images
                .GroupBy(i => i.MonthKey)
                .OrderByDescending(g => g.Key)
                .Select(g => new
                {
                    month = g.Key,
                    count = g.Count(),
                    days = g.GroupBy(i => i.DateKey)
                        .OrderByDescending(d => d.Key)
                        .Select(d => new { date = d.Key, count = d.Count() })
                });

            await WriteJson(response, new { total = images.Count, months });
        }

        private async Task ServeGalleryDays(HttpListenerResponse response)
        {
            var images = ScanGalleryImages();
            var days = images
                .GroupBy(i => i.DateKey)
                .OrderByDescending(g => g.Key)
                .Select((g, index) => new
                {
                    date = g.Key,
                    page = index + 1,
                    count = g.Count(),
                    month = g.First().MonthKey
                });

            await WriteJson(response, new { total = images.Count, days });
        }

        private async Task ServeGalleryImages(HttpListenerRequest request, HttpListenerResponse response)
        {
            var query = ParseQuery(request.Url?.Query ?? "");
            query.TryGetValue("month", out string? month);
            query.TryGetValue("date", out string? date);
            query.TryGetValue("q", out string? search);
            int page = 1;
            if (query.TryGetValue("page", out string? rawPage) && int.TryParse(rawPage, out int parsedPage))
                page = Math.Max(1, parsedPage);

            int limit = 120;
            if (query.TryGetValue("limit", out string? rawLimit) && int.TryParse(rawLimit, out int parsedLimit))
                limit = Math.Clamp(parsedLimit, 1, 500);

            var allImages = ScanGalleryImages();
            var filtered = allImages
                .Where(i => string.IsNullOrWhiteSpace(month) || i.MonthKey == month)
                .Where(i => string.IsNullOrWhiteSpace(date) || i.DateKey == date);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchTerms = search.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                filtered = filtered.Where(i => GalleryImageMatchesSearch(i, searchTerms));
            }

            if (string.IsNullOrWhiteSpace(date) && string.IsNullOrWhiteSpace(month) && string.IsNullOrWhiteSpace(search))
            {
                var days = allImages.GroupBy(i => i.DateKey).OrderByDescending(g => g.Key).ToList();
                var day = days.Skip(page - 1).FirstOrDefault();
                filtered = day ?? Enumerable.Empty<GalleryFileItem>();
            }

            var ordered = filtered.OrderByDescending(i => i.LastWriteTime).ToList();
            var images = ordered.Take(limit).Select(ToGalleryImageDto);

            await WriteJson(response, new
            {
                total = ordered.Count,
                page,
                images
            });
        }

        private async Task ServeGalleryRandom(HttpListenerRequest request, HttpListenerResponse response)
        {
            int count = 5;
            string? rawCount = HttpUtility.ParseQueryString(request.Url?.Query ?? "").Get("count");
            if (int.TryParse(rawCount, out int requestedCount))
            {
                count = Math.Clamp(requestedCount, 1, 100);
            }

            var random = new Random();
            var images = ScanGalleryImages()
                .OrderBy(_ => random.Next())
                .Take(count)
                .Select(ToGalleryImageDto);

            await WriteJson(response, new { success = true, count, images });
        }

        private async Task ServeGalleryMetadata(string id, HttpListenerResponse response)
        {
            string? fullPath = DecodeGalleryImageId(id);
            if (fullPath == null || !IsSafeGalleryImagePath(fullPath) || !File.Exists(fullPath))
            {
                await WriteJson(response, new { error = "Image not found" }, 404);
                return;
            }

            await WriteJson(response, ReadGalleryMetadata(fullPath));
        }

        private async Task ServeGalleryText(string id, HttpListenerRequest request, HttpListenerResponse response)
        {
            string? fullPath = DecodeGalleryImageId(id);
            string? textPath = GetGalleryTextPath(fullPath);
            if (textPath == null || !File.Exists(textPath))
            {
                response.StatusCode = 404;
                return;
            }

            await ServePhysicalFile(textPath, request, response, "text/plain; charset=utf-8", $"attachment; filename=\"{WebUtility.UrlEncode(Path.GetFileName(textPath))}\"");
        }

        private async Task ServeGalleryImage(string id, HttpListenerRequest request, HttpListenerResponse response)
        {
            string? fullPath = DecodeGalleryImageId(id);
            if (fullPath == null || !IsSafeGalleryImagePath(fullPath) || !File.Exists(fullPath))
            {
                response.StatusCode = 404;
                return;
            }

            await ServePhysicalFile(fullPath, request, response, GetImageContentType(fullPath), null);
        }

        private async Task RenameGalleryImage(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                string body;
                using (var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8))
                    body = await reader.ReadToEndAsync();

                var payload = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(body) ?? new();
                payload.TryGetValue("id", out string? id);
                payload.TryGetValue("newBaseName", out string? newBaseName);

                string? oldPath = string.IsNullOrWhiteSpace(id) ? null : DecodeGalleryImageId(id);
                if (oldPath == null || !IsSafeGalleryImagePath(oldPath) || !File.Exists(oldPath))
                    throw new Exception("Image not found");

                newBaseName = SanitizeGalleryBaseName(newBaseName ?? "");
                if (string.IsNullOrWhiteSpace(newBaseName))
                    throw new Exception("New name is empty");

                string directory = Path.GetDirectoryName(oldPath) ?? "";
                string extension = Path.GetExtension(oldPath);
                string newPath = Path.Combine(directory, newBaseName + extension);
                int counter = 1;
                while (File.Exists(newPath) && !newPath.Equals(oldPath, StringComparison.OrdinalIgnoreCase))
                    newPath = Path.Combine(directory, $"{newBaseName}_{counter++}{extension}");

                string? oldText = GetGalleryTextPath(oldPath);
                string newText = Path.Combine(directory, Path.GetFileNameWithoutExtension(newPath) + ".txt");

                File.Move(oldPath, newPath);
                if (oldText != null && File.Exists(oldText))
                    File.Move(oldText, newText, true);

                var item = CreateGalleryFileItem(newPath);
                await WriteJson(response, new { success = true, image = ToGalleryImageDto(item) });
            }
            catch (Exception ex)
            {
                await WriteJson(response, new { success = false, error = ex.Message }, 400);
            }
        }

        private async Task DeleteGalleryImages(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                string body;
                using (var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8))
                    body = await reader.ReadToEndAsync();

                var payload = Newtonsoft.Json.JsonConvert.DeserializeObject<GalleryDeleteRequest>(body) ?? new GalleryDeleteRequest();
                int deleted = 0;

                foreach (string id in payload.Ids ?? new List<string>())
                {
                    string? path = DecodeGalleryImageId(id);
                    if (path == null || !IsSafeGalleryImagePath(path) || !File.Exists(path))
                        continue;

                    File.Delete(path);
                    string? textPath = GetGalleryTextPath(path);
                    if (textPath != null && File.Exists(textPath))
                        File.Delete(textPath);
                    deleted++;
                }

                await WriteJson(response, new { success = true, deleted });
            }
            catch (Exception ex)
            {
                await WriteJson(response, new { success = false, error = ex.Message }, 400);
            }
        }

        private async Task ServeFile(string fileName, HttpListenerRequest request, HttpListenerResponse response)
        {
            string? fullPath = GetSafeSharePath(fileName);
            if (fullPath == null || !File.Exists(fullPath) || fullPath.EndsWith(".part", StringComparison.OrdinalIgnoreCase))
            {
                response.StatusCode = 404;
                return;
            }

            await ServePhysicalFile(fullPath, request, response, "application/octet-stream", $"attachment; filename=\"{WebUtility.UrlEncode(Path.GetFileName(fullPath))}\"");
        }

        private async Task ServePhysicalFile(string fullPath, HttpListenerRequest request, HttpListenerResponse response, string contentType, string? contentDisposition)
        {
            var fileInfo = new FileInfo(fullPath);
            long start = 0;
            long end = fileInfo.Length - 1;
            bool partial = TryParseRange(request.Headers["Range"], fileInfo.Length, out start, out end);
            long contentLength = end - start + 1;

            response.ContentType = contentType;
            response.AddHeader("Accept-Ranges", "bytes");
            if (!string.IsNullOrWhiteSpace(contentDisposition))
                response.AddHeader("Content-Disposition", contentDisposition);
            response.ContentLength64 = contentLength;

            if (partial)
            {
                response.StatusCode = 206;
                response.AddHeader("Content-Range", $"bytes {start}-{end}/{fileInfo.Length}");
            }

            byte[] buffer = new byte[BufferSize];
            using var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, true);
            fileStream.Seek(start, SeekOrigin.Begin);

            long remaining = contentLength;
            while (remaining > 0)
            {
                int read = await fileStream.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)));
                if (read <= 0) break;
                await response.OutputStream.WriteAsync(buffer.AsMemory(0, read));
                remaining -= read;
            }
        }

        private async Task HandleUpload(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                string contentType = request.ContentType ?? "";
                string boundary = GetBoundary(contentType);
                if (string.IsNullOrWhiteSpace(boundary))
                    throw new Exception("Invalid multipart upload");

                var form = await ReadMultipartBody(request.InputStream, boundary, request.ContentEncoding ?? Encoding.UTF8);
                if (form.FileName == null || form.FileBytes == null)
                    throw new Exception("No file found");

                string fileName = GetUniqueFileName(SanitizeFileName(form.FileName));
                string fullPath = Path.Combine(SharePath, fileName);

                await File.WriteAllBytesAsync(fullPath, form.FileBytes);
                FileReceived?.Invoke(fileName);
                await WriteJson(response, new { status = "success", fileName });
            }
            catch (Exception ex)
            {
                await WriteJson(response, new { status = "error", message = ex.Message }, 400);
            }
        }

        private async Task StartRemoteDownload(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                string body;
                using (var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8))
                    body = await reader.ReadToEndAsync();

                var payload = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(body) ?? new();
                payload.TryGetValue("url", out string? url);

                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
                    throw new Exception("Enter a valid http or https link");

                string id = Guid.NewGuid().ToString("N");
                var job = new CloudDownloadJob(id, uri.ToString());
                _remoteDownloads[id] = job;
                _ = Task.Run(() => DownloadRemoteFile(job));
                await WriteJson(response, new { status = "started", id });
            }
            catch (Exception ex)
            {
                await WriteJson(response, new { status = "error", message = ex.Message }, 400);
            }
        }

        private async Task DownloadRemoteFile(CloudDownloadJob job)
        {
            string? tempPath = null;
            try
            {
                job.Status = "connecting";
                using var request = new HttpRequestMessage(HttpMethod.Get, job.Url);
                using var httpResponse = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, job.Token);
                httpResponse.EnsureSuccessStatusCode();

                job.TotalBytes = httpResponse.Content.Headers.ContentLength ?? 0;
                job.FileName = GetUniqueFileName(ResolveRemoteFileName(httpResponse.Content.Headers, httpResponse.RequestMessage?.RequestUri ?? new Uri(job.Url)));
                string finalPath = Path.Combine(SharePath, job.FileName);
                tempPath = finalPath + ".part";

                job.Status = "downloading";
                job.Message = "Downloading to this device";
                var sw = Stopwatch.StartNew();
                long previousBytes = 0;
                var lastSpeedCheck = Stopwatch.StartNew();

                await using var source = await httpResponse.Content.ReadAsStreamAsync(job.Token);
                await using var target = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.Read, BufferSize, true);
                byte[] buffer = new byte[BufferSize];

                while (true)
                {
                    int read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), job.Token);
                    if (read <= 0) break;

                    await target.WriteAsync(buffer.AsMemory(0, read), job.Token);
                    job.DownloadedBytes += read;

                    if (lastSpeedCheck.ElapsedMilliseconds >= 500)
                    {
                        job.SpeedBytesPerSecond = (job.DownloadedBytes - previousBytes) / Math.Max(0.001, lastSpeedCheck.Elapsed.TotalSeconds);
                        previousBytes = job.DownloadedBytes;
                        lastSpeedCheck.Restart();
                    }
                }

                await target.FlushAsync(job.Token);
                File.Move(tempPath, finalPath);
                job.Status = "completed";
                job.SpeedBytesPerSecond = 0;
                job.Message = $"Ready to download from this device in {sw.Elapsed:mm\\:ss}";
                FileReceived?.Invoke(job.FileName);
            }
            catch (OperationCanceledException)
            {
                job.Status = "cancelled";
                job.Message = "Cancelled";
                TryDeleteFile(tempPath);
            }
            catch (Exception ex)
            {
                job.Status = "failed";
                job.Message = ex.Message;
                TryDeleteFile(tempPath);
            }
        }

        private async Task DeleteSharedFile(string fileName, HttpListenerResponse response)
        {
            string? fullPath = GetSafeSharePath(fileName);
            if (fullPath == null || !File.Exists(fullPath))
            {
                await WriteJson(response, new { status = "error", message = "File not found" }, 404);
                return;
            }

            File.Delete(fullPath);
            await WriteJson(response, new { status = "deleted" });
        }

        private async Task CancelRemoteDownload(string id, HttpListenerResponse response)
        {
            if (_remoteDownloads.TryGetValue(id, out var job) && job.Status is "connecting" or "downloading")
            {
                job.Cancel();
                await WriteJson(response, new { status = "cancelled" });
                return;
            }

            await WriteJson(response, new { status = "error", message = "Download is not active" }, 404);
        }

        private static string GetBoundary(string contentType)
        {
            const string marker = "boundary=";
            int index = contentType.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0) return "";
            string boundary = contentType[(index + marker.Length)..].Trim();
            if (boundary.StartsWith("\"") && boundary.EndsWith("\""))
                boundary = boundary[1..^1];
            return boundary;
        }

        private static async Task<MultipartUpload> ReadMultipartBody(Stream input, string boundary, Encoding encoding)
        {
            using var ms = new MemoryStream();
            byte[] buffer = new byte[BufferSize];
            int read;
            while ((read = await input.ReadAsync(buffer, 0, buffer.Length)) > 0)
                await ms.WriteAsync(buffer, 0, read);

            byte[] data = ms.ToArray();
            byte[] headerEnd = encoding.GetBytes("\r\n\r\n");
            int headerEndIndex = FindBytes(data, headerEnd, 0);
            if (headerEndIndex < 0) throw new Exception("Invalid multipart format");

            string headerPart = encoding.GetString(data, 0, headerEndIndex);
            string? fileName = ExtractFileName(headerPart);
            byte[] footer = encoding.GetBytes("\r\n--" + boundary);
            int footerIndex = FindBytesReverse(data, footer, data.Length - 1);
            if (footerIndex <= headerEndIndex) throw new Exception("Invalid multipart data");

            int start = headerEndIndex + headerEnd.Length;
            byte[] fileBytes = new byte[footerIndex - start];
            Array.Copy(data, start, fileBytes, 0, fileBytes.Length);
            return new MultipartUpload(fileName, fileBytes);
        }

        private static string? ExtractFileName(string headerPart)
        {
            const string marker = "filename=\"";
            int start = headerPart.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (start < 0) return null;
            start += marker.Length;
            int end = headerPart.IndexOf("\"", start, StringComparison.Ordinal);
            if (end < 0) return null;
            return headerPart[start..end];
        }

        private static string SanitizeFileName(string fileName)
        {
            string safe = Path.GetFileName(WebUtility.UrlDecode(fileName));
            foreach (char c in Path.GetInvalidFileNameChars())
                safe = safe.Replace(c, '_');
            return string.IsNullOrWhiteSpace(safe) ? $"file_{Guid.NewGuid():N}.bin" : safe;
        }

        private static string GetUniqueFileName(string fileName)
        {
            fileName = SanitizeFileName(fileName);
            string candidate = fileName;
            string name = Path.GetFileNameWithoutExtension(fileName);
            string extension = Path.GetExtension(fileName);
            int index = 1;

            while (File.Exists(Path.Combine(SharePath, candidate)) || File.Exists(Path.Combine(SharePath, candidate + ".part")))
                candidate = $"{name} ({index++}){extension}";

            return candidate;
        }

        private static string ResolveRemoteFileName(HttpContentHeaders headers, Uri uri)
        {
            string? headerName = headers.ContentDisposition?.FileNameStar ?? headers.ContentDisposition?.FileName;
            if (!string.IsNullOrWhiteSpace(headerName))
                return SanitizeFileName(headerName.Trim('"'));

            string pathName = Path.GetFileName(uri.LocalPath);
            if (!string.IsNullOrWhiteSpace(pathName))
                return SanitizeFileName(pathName);

            string extension = headers.ContentType?.MediaType?.ToLowerInvariant() switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                "application/pdf" => ".pdf",
                "application/zip" => ".zip",
                "video/mp4" => ".mp4",
                _ => ".bin"
            };
            return $"remote_{DateTime.Now:yyyyMMdd_HHmmss}{extension}";
        }

        private static string? GetSafeSharePath(string fileName)
        {
            string safeName = SanitizeFileName(fileName);
            string fullPath = Path.GetFullPath(Path.Combine(SharePath, safeName));
            string shareRoot = Path.GetFullPath(SharePath) + Path.DirectorySeparatorChar;
            return fullPath.StartsWith(shareRoot, StringComparison.OrdinalIgnoreCase) ? fullPath : null;
        }

        private static string LoadGalleryPath()
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, GalleryConfigFileName);
                if (!File.Exists(configPath))
                    return "";

                string path = File.ReadAllText(configPath).Trim();
                return Directory.Exists(path) ? Path.GetFullPath(path) : "";
            }
            catch
            {
                return "";
            }
        }

        private static List<GalleryFileItem> ScanGalleryImages()
        {
            string galleryPath = LoadGalleryPath();
            if (string.IsNullOrWhiteSpace(galleryPath))
                return new List<GalleryFileItem>();

            try
            {
                return Directory.GetFiles(galleryPath, "*.*", SearchOption.AllDirectories)
                    .Where(IsGalleryImageFile)
                    .Select(CreateGalleryFileItem)
                    .OrderByDescending(i => i.LastWriteTime)
                    .ToList();
            }
            catch
            {
                return new List<GalleryFileItem>();
            }
        }

        private static bool IsGalleryImageFile(string path)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            return GalleryImageExtensions.Contains(extension);
        }

        private static object ToGalleryImageDto(GalleryFileItem image)
        {
            var metadata = ReadGalleryMetadata(image.Path);
            string? textPath = GetGalleryTextPath(image.Path);
            string id = EncodeGalleryImageId(image.Path);
            return new
            {
                id,
                fileName = image.FileName,
                baseName = image.BaseName,
                extension = Path.GetExtension(image.FileName).TrimStart('.'),
                date = image.DateKey,
                month = image.MonthKey,
                size = image.Size,
                url = "/gallery/image/" + id,
                textUrl = textPath != null && File.Exists(textPath) ? "/gallery/text/" + id : "",
                positive = metadata.Positive,
                negative = metadata.Negative,
                description = metadata.Description
            };
        }

        private static GalleryFileItem CreateGalleryFileItem(string path)
        {
            var info = new FileInfo(path);
            return new GalleryFileItem
            {
                Path = Path.GetFullPath(path),
                FileName = info.Name,
                BaseName = Path.GetFileNameWithoutExtension(info.Name),
                DateKey = info.LastWriteTime.ToString("yyyy-MM-dd"),
                MonthKey = info.LastWriteTime.ToString("yyyy-MM"),
                LastWriteTime = info.LastWriteTime,
                Size = info.Length
            };
        }

        private static GalleryMetadata ReadGalleryMetadata(string imagePath)
        {
            string positive = "";
            string negative = "";
            string description = "";
            string? textPath = GetGalleryTextPath(imagePath);

            if (textPath == null || !File.Exists(textPath))
                return new GalleryMetadata(positive, negative, description);

            try
            {
                string content = File.ReadAllText(textPath);
                positive = ExtractMetadataSection(content, "Positive Prompt");
                negative = ExtractMetadataSection(content, "Negative Prompt");
                description = ExtractMetadataSection(content, "Description");

                if (string.IsNullOrWhiteSpace(positive) || string.IsNullOrWhiteSpace(negative))
                {
                    string[] lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                    if (string.IsNullOrWhiteSpace(positive) && lines.Length > 0)
                        positive = lines[0].Trim();
                    if (string.IsNullOrWhiteSpace(negative) && lines.Length > 1)
                        negative = lines[1].Trim();
                }
            }
            catch { }

            return new GalleryMetadata(positive, negative, description);
        }

        private static string ExtractMetadataSection(string content, string label)
        {
            string marker = label + ":";
            int start = content.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
                return "";

            start += marker.Length;
            int end = content.IndexOf("\n\n", start, StringComparison.Ordinal);
            if (end < 0)
                end = content.Length;

            return content[start..end].Trim();
        }

        private static string? GetGalleryTextPath(string? imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !IsSafeGalleryImagePath(imagePath))
                return null;

            string directory = Path.GetDirectoryName(imagePath) ?? "";
            return Path.Combine(directory, Path.GetFileNameWithoutExtension(imagePath) + ".txt");
        }

        private static bool GalleryImageMatchesSearch(GalleryFileItem image, string[] searchTerms)
        {
            var metadata = ReadGalleryMetadata(image.Path);
            string haystack = string.Join(" ", image.FileName, metadata.Positive, metadata.Negative, metadata.Description);
            return searchTerms.All(term => haystack.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        private static string SanitizeGalleryBaseName(string value)
        {
            string safe = value.Trim();
            foreach (char c in Path.GetInvalidFileNameChars())
                safe = safe.Replace(c, '_');
            return safe.Length > 200 ? safe[..200] : safe;
        }

        private static string EncodeGalleryImageId(string path)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(Path.GetFullPath(path)))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }

        private static string? DecodeGalleryImageId(string id)
        {
            try
            {
                string value = id.Replace('-', '+').Replace('_', '/');
                value = value.PadRight(value.Length + (4 - value.Length % 4) % 4, '=');
                return Path.GetFullPath(Encoding.UTF8.GetString(Convert.FromBase64String(value)));
            }
            catch
            {
                return null;
            }
        }

        private static bool IsSafeGalleryImagePath(string fullPath)
        {
            string galleryPath = LoadGalleryPath();
            if (string.IsNullOrWhiteSpace(galleryPath) || !IsGalleryImageFile(fullPath))
                return false;

            string galleryRoot = Path.GetFullPath(galleryPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string candidate = Path.GetFullPath(fullPath);
            return candidate.StartsWith(galleryRoot, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetImageContentType(string path)
        {
            return Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                ".bmp" => "image/bmp",
                ".gif" => "image/gif",
                ".tif" or ".tiff" => "image/tiff",
                _ => "application/octet-stream"
            };
        }

        private static string GetStaticContentType(string path)
        {
            return Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".html" => "text/html; charset=utf-8",
                ".css" => "text/css; charset=utf-8",
                ".js" => "application/javascript; charset=utf-8",
                ".json" => "application/json; charset=utf-8",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".webp" => "image/webp",
                ".svg" => "image/svg+xml",
                ".ico" => "image/x-icon",
                ".ttf" => "font/ttf",
                ".woff" => "font/woff",
                ".woff2" => "font/woff2",
                _ => "application/octet-stream"
            };
        }

        private static Dictionary<string, string> ParseQuery(string query)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(query))
                return result;

            foreach (string part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] pieces = part.Split('=', 2);
                string key = WebUtility.UrlDecode(pieces[0]);
                string value = pieces.Length > 1 ? WebUtility.UrlDecode(pieces[1]) : "";
                if (!string.IsNullOrWhiteSpace(key))
                    result[key] = value;
            }

            return result;
        }

        private static bool TryParseRange(string? rangeHeader, long fileLength, out long start, out long end)
        {
            start = 0;
            end = fileLength - 1;
            if (string.IsNullOrWhiteSpace(rangeHeader) || !rangeHeader.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase))
                return false;

            string[] parts = rangeHeader[6..].Split('-', 2);
            if (parts.Length != 2) return false;

            if (!string.IsNullOrWhiteSpace(parts[0]) && long.TryParse(parts[0], out long parsedStart))
                start = parsedStart;

            if (!string.IsNullOrWhiteSpace(parts[1]) && long.TryParse(parts[1], out long parsedEnd))
                end = parsedEnd;

            if (start < 0 || end < start || start >= fileLength)
            {
                start = 0;
                end = fileLength - 1;
                return false;
            }

            end = Math.Min(end, fileLength - 1);
            return true;
        }

        private static async Task WriteJson(HttpListenerResponse response, object data, int statusCode = 200)
        {
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(data);
            byte[] buffer = Encoding.UTF8.GetBytes(json);
            response.StatusCode = statusCode;
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
        }

        private static int FindBytes(byte[] source, byte[] pattern, int startIndex)
        {
            for (int i = startIndex; i <= source.Length - pattern.Length; i++)
            {
                bool found = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (source[i + j] != pattern[j])
                    {
                        found = false;
                        break;
                    }
                }
                if (found) return i;
            }
            return -1;
        }

        private static int FindBytesReverse(byte[] source, byte[] pattern, int startIndex)
        {
            for (int i = startIndex - pattern.Length + 1; i >= 0; i--)
            {
                bool found = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    if (source[i + j] != pattern[j])
                    {
                        found = false;
                        break;
                    }
                }
                if (found) return i;
            }
            return -1;
        }

        private static void TryDeleteFile(string? path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    File.Delete(path);
            }
            catch { }
        }

        public string GetLocalIPAddress()
        {
            try
            {
                var interfaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
                foreach (var ni in interfaces)
                {
                    if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211 &&
                        ni.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Ethernet) continue;

                    string name = ni.Name.ToLower();
                    if (name.Contains("vpn") || name.Contains("tun")) continue;

                    var props = ni.GetIPProperties();
                    foreach (var ip in props.UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            string addr = ip.Address.ToString();
                            if (addr.StartsWith("192.168.") || addr.StartsWith("10.") || addr.StartsWith("172."))
                                return addr;
                        }
                    }
                }
            }
            catch { }
            return "127.0.0.1";
        }

        private string GetHtmlInterface()
        {
            return @"<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Cloud Link - File Share</title>
    <style>
        :root {
            --bg: #10151f;
            --surface: #171f2d;
            --surface-soft: #202a3a;
            --primary: #2f80ed;
            --danger: #ef4444;
            --text: #f7fafc;
            --text-muted: #9aa8bb;
            --success: #22c55e;
            --border: rgba(255,255,255,0.1);
        }
        * { box-sizing: border-box; }
        body { margin: 0; font-family: Arial, Helvetica, sans-serif; background: var(--bg); color: var(--text); padding: 18px; }
        .container { max-width: 900px; margin: 0 auto; }
        header { display: flex; justify-content: space-between; align-items: center; gap: 12px; margin-bottom: 22px; padding-bottom: 16px; border-bottom: 1px solid var(--border); }
        h1 { font-size: 1.35rem; margin: 0; letter-spacing: 0; }
        h2 { font-size: 1rem; margin: 0; }
        .online { color: var(--success); font-size: 0.8rem; font-weight: 700; white-space: nowrap; }
        .panel { background: var(--surface); border: 1px solid var(--border); border-radius: 8px; padding: 16px; margin-bottom: 16px; }
        .drop { border: 2px dashed var(--border); text-align: center; transition: border-color .2s, background .2s; }
        .drop.drag-over { border-color: var(--primary); background: rgba(47,128,237,.08); }
        .row { display: flex; gap: 10px; align-items: center; }
        .row.wrap { flex-wrap: wrap; }
        input[type='url'] { min-width: 0; flex: 1; background: var(--surface-soft); color: var(--text); border: 1px solid var(--border); border-radius: 6px; padding: 12px; font-size: 0.95rem; }
        button, .button { border: 0; border-radius: 6px; padding: 10px 12px; color: white; background: var(--primary); cursor: pointer; font-weight: 700; text-decoration: none; display: inline-flex; align-items: center; justify-content: center; min-height: 38px; }
        button.secondary, .button.secondary { background: var(--surface-soft); color: var(--text); border: 1px solid var(--border); }
        button.danger { background: rgba(239,68,68,.12); color: #fecaca; border: 1px solid rgba(239,68,68,.35); }
        .muted { color: var(--text-muted); font-size: .82rem; }
        .progress { width: 100%; height: 7px; background: rgba(255,255,255,.08); border-radius: 999px; overflow: hidden; margin-top: 10px; }
        .progress > div { height: 100%; width: 0%; background: var(--primary); transition: width .2s; }
        .list { display: grid; gap: 10px; }
        .item { background: var(--surface); border: 1px solid var(--border); border-radius: 8px; padding: 12px; display: flex; justify-content: space-between; align-items: center; gap: 12px; }
        .name { font-weight: 700; overflow-wrap: anywhere; }
        .actions { display: flex; gap: 8px; flex-shrink: 0; }
        .empty { color: var(--text-muted); text-align: center; padding: 18px; border: 1px dashed var(--border); border-radius: 8px; }
        @media (max-width: 640px) {
            body { padding: 12px; }
            .row { align-items: stretch; }
            .row, .item { flex-direction: column; }
            .actions, button, .button, input[type='url'] { width: 100%; }
        }
    </style>
</head>
<body>
    <div class='container'>
        <header>
            <h1>Cloud Link</h1>
            <span class='online'>ONLINE</span>
        </header>

        <section class='panel drop' id='drop-zone'>
            <h2>Upload from this device</h2>
            <p class='muted'>Drop a file here or choose one from your device.</p>
            <input type='file' id='file-input' hidden>
            <button onclick='document.getElementById(""file-input"").click()'>Choose File</button>
            <div class='progress' id='upload-progress' style='display:none'><div></div></div>
            <p class='muted' id='upload-status'></p>
        </section>

        <section class='panel'>
            <h2>Download a web link to this computer</h2>
            <div class='row wrap' style='margin-top:12px'>
                <input id='remote-url' type='url' placeholder='https://example.com/file.zip'>
                <button onclick='startRemoteDownload()'>Start</button>
            </div>
            <p class='muted'>The file appears below only after the download is complete.</p>
            <div class='list' id='remote-list'></div>
        </section>

        <div class='row' style='justify-content:space-between;margin:18px 0 10px'>
            <h2>Shared Files</h2>
            <button class='secondary' onclick='loadFiles()'>Refresh</button>
        </div>
        <div class='list' id='file-list'></div>
    </div>

    <script>
        const dropZone = document.getElementById('drop-zone');
        const fileInput = document.getElementById('file-input');

        dropZone.ondragover = (e) => { e.preventDefault(); dropZone.classList.add('drag-over'); };
        dropZone.ondragleave = () => dropZone.classList.remove('drag-over');
        dropZone.ondrop = (e) => {
            e.preventDefault();
            dropZone.classList.remove('drag-over');
            handleFiles(e.dataTransfer.files);
        };
        fileInput.onchange = (e) => handleFiles(e.target.files);

        function escapeHtml(value) {
            return String(value).replace(/[&<>""']/g, ch => ({'&':'&amp;','<':'&lt;','>':'&gt;','""':'&quot;',""'"":'&#39;'}[ch]));
        }

        function handleFiles(files) {
            if (!files.length) return;
            const file = files[0];
            const formData = new FormData();
            formData.append('file', file);

            const progress = document.getElementById('upload-progress');
            const bar = progress.querySelector('div');
            const status = document.getElementById('upload-status');
            progress.style.display = 'block';
            status.textContent = 'Uploading ' + file.name;

            const xhr = new XMLHttpRequest();
            xhr.open('POST', '/api/upload', true);
            xhr.upload.onprogress = (e) => {
                if (!e.lengthComputable) return;
                const percent = Math.round((e.loaded / e.total) * 100);
                bar.style.width = percent + '%';
                status.textContent = percent + '% - ' + formatSize(e.loaded) + ' / ' + formatSize(e.total);
            };
            xhr.onload = () => {
                progress.style.display = 'none';
                bar.style.width = '0%';
                status.textContent = xhr.status === 200 ? 'Upload complete' : 'Upload failed';
                fileInput.value = '';
                loadFiles();
            };
            xhr.onerror = () => { status.textContent = 'Upload failed'; };
            xhr.send(formData);
        }

        async function startRemoteDownload() {
            const input = document.getElementById('remote-url');
            const url = input.value.trim();
            if (!url) return;
            const res = await fetch('/api/remote-download', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ url })
            });
            const data = await res.json();
            if (!res.ok) {
                alert(data.message || 'Download failed to start');
                return;
            }
            input.value = '';
            loadRemoteDownloads();
        }

        async function cancelRemoteDownload(id) {
            await fetch('/api/remote-downloads/' + encodeURIComponent(id), { method: 'DELETE' });
            loadRemoteDownloads();
        }

        async function deleteFile(name) {
            if (!confirm('Delete ' + name + '?')) return;
            const res = await fetch('/api/files/' + encodeURIComponent(name), { method: 'DELETE' });
            if (!res.ok) alert('Delete failed');
            await loadFiles();
        }

        async function loadRemoteDownloads() {
            const res = await fetch('/api/remote-downloads');
            const jobs = await res.json();
            const list = document.getElementById('remote-list');
            const active = jobs.filter(j => j.status !== 'completed' || Date.now() - (window._loadedAt || 0) < 3000);
            list.innerHTML = '';
            active.forEach(j => {
                const div = document.createElement('div');
                div.className = 'item';
                const percent = j.total > 0 ? j.progress : 0;
                div.innerHTML = `
                    <div style='width:100%'>
                        <div class='name'>${escapeHtml(j.fileName || j.url)}</div>
                        <div class='muted'>${escapeHtml(j.status)} - ${formatSize(j.downloaded)}${j.total ? ' / ' + formatSize(j.total) : ''} - ${formatSize(j.speed || 0)}/s</div>
                        <div class='progress'><div style='width:${percent}%'></div></div>
                        <div class='muted'>${escapeHtml(j.message || '')}</div>
                    </div>
                    ${j.status === 'downloading' || j.status === 'connecting' ? `<button class='danger' onclick='cancelRemoteDownload(""${j.id}"")'>Cancel</button>` : ''}
                `;
                list.appendChild(div);
            });
        }

        async function loadFiles() {
            const res = await fetch('/api/files');
            const files = await res.json();
            const list = document.getElementById('file-list');
            list.innerHTML = '';
            if (!files.length) {
                list.innerHTML = `<div class='empty'>No shared files yet.</div>`;
                return;
            }
            files.forEach(f => {
                const name = f.name || f.Name;
                const size = f.size || f.Size || 0;
                const date = f.date || f.Date || '';
                const div = document.createElement('div');
                div.className = 'item';
                div.innerHTML = `
                    <div>
                        <div class='name'>${escapeHtml(name)}</div>
                        <div class='muted'>${formatSize(size)} - ${escapeHtml(date)}</div>
                    </div>
                    <div class='actions'>
                        <a href='/download/${encodeURIComponent(name)}' class='button secondary' download>Download</a>
                        <button class='danger' onclick='deleteFile(""${escapeHtml(name).replace(/""/g, '&quot;')}"")'>Delete</button>
                    </div>
                `;
                list.appendChild(div);
            });
        }

        function formatSize(bytes) {
            bytes = Number(bytes || 0);
            if (bytes <= 0) return '0 B';
            const sizes = ['B', 'KB', 'MB', 'GB', 'TB'];
            const i = Math.min(Math.floor(Math.log(bytes) / Math.log(1024)), sizes.length - 1);
            return (bytes / Math.pow(1024, i)).toFixed(i === 0 ? 0 : 1) + ' ' + sizes[i];
        }

        window._loadedAt = Date.now();
        loadFiles();
        loadRemoteDownloads();
        setInterval(() => { loadRemoteDownloads(); loadFiles(); }, 1500);
    </script>
</body>
</html>";
        }

        private sealed record MultipartUpload(string? FileName, byte[]? FileBytes);

        private sealed record GalleryMetadata(string Positive, string Negative, string Description);

        private sealed class GalleryDeleteRequest
        {
            public List<string>? Ids { get; set; }
        }

        private sealed class GalleryFileItem
        {
            public string Path { get; init; } = "";
            public string FileName { get; init; } = "";
            public string BaseName { get; init; } = "";
            public string DateKey { get; init; } = "";
            public string MonthKey { get; init; } = "";
            public DateTime LastWriteTime { get; init; }
            public long Size { get; init; }
        }

        private sealed class CloudDownloadJob
        {
            private readonly CancellationTokenSource _cts = new();

            public CloudDownloadJob(string id, string url)
            {
                Id = id;
                Url = url;
                FileName = "Preparing...";
            }

            public string Id { get; }
            public string Url { get; }
            public string FileName { get; set; }
            public string Status { get; set; } = "pending";
            public string Message { get; set; } = "";
            public long DownloadedBytes { get; set; }
            public long TotalBytes { get; set; }
            public double SpeedBytesPerSecond { get; set; }
            public DateTime StartedAt { get; } = DateTime.Now;
            public CancellationToken Token => _cts.Token;

            public void Cancel() => _cts.Cancel();
        }
    }
}
