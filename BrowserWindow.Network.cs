using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace imgsaver
{
    public partial class BrowserWindow
    {
        private class TabNetworkInfo
        {
            public long CachedBytes { get; set; }
            public long DownloadedBytes { get; set; }
            public long TotalBytes => CachedBytes + DownloadedBytes;
            public HashSet<string> CacheKeys { get; } = new(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> DownloadKeys { get; } = new(StringComparer.OrdinalIgnoreCase);
            public HashSet<string> SkippedHosts { get; } = new(StringComparer.OrdinalIgnoreCase);
        }

        private TabItem? GetTabItemForCoreWebView2(CoreWebView2? core)
        {
            if (core == null) return null;
            return _coreWebViewTabMap.TryGetValue(core, out var tab) ? tab : null;
        }

        private void InitializeTabNetworkStats(TabItem tabItem)
        {
            _tabNetworkStats[tabItem] = new TabNetworkInfo();
        }

        private void ResetTabNetworkStats(TabItem tabItem)
        {
            if (_tabNetworkStats.TryGetValue(tabItem, out var stats))
            {
                stats.CachedBytes = 0;
                stats.DownloadedBytes = 0;
                stats.CacheKeys.Clear();
                stats.DownloadKeys.Clear();
                stats.SkippedHosts.Clear();
            }
            else
            {
                _tabNetworkStats[tabItem] = new TabNetworkInfo();
            }
            if (BrowserTabs.SelectedItem == tabItem) UpdateTabStatusOverlay(tabItem);
        }

        private void AddTabCachedBytes(TabItem tabItem, long bytes)
        {
            AddTabCachedBytes(tabItem, Guid.NewGuid().ToString("N"), bytes);
        }

        private void AddTabCachedBytes(TabItem tabItem, string key, long bytes)
        {
            if (!_tabNetworkStats.TryGetValue(tabItem, out var stats))
            {
                stats = new TabNetworkInfo();
                _tabNetworkStats[tabItem] = stats;
            }
            if (bytes <= 0 || !stats.CacheKeys.Add(key)) return;
            stats.CachedBytes += bytes;
            if (BrowserTabs.SelectedItem == tabItem) UpdateTabStatusOverlay(tabItem);
        }

        private void AddTabDownloadedBytes(TabItem tabItem, long bytes)
        {
            AddTabDownloadedBytes(tabItem, Guid.NewGuid().ToString("N"), bytes);
        }

        private void AddTabDownloadedBytes(TabItem tabItem, string key, long bytes)
        {
            if (bytes <= 0) return;
            if (!_tabNetworkStats.TryGetValue(tabItem, out var stats))
            {
                stats = new TabNetworkInfo();
                _tabNetworkStats[tabItem] = stats;
            }
            if (!stats.DownloadKeys.Add(key)) return;
            stats.DownloadedBytes += bytes;
            if (BrowserTabs.SelectedItem == tabItem) UpdateTabStatusOverlay(tabItem);
        }

        private void UpdateTabStatusOverlay(TabItem? tabItem = null, string? currentUrl = null)
        {
            tabItem ??= BrowserTabs.SelectedItem as TabItem;
            if (tabItem == null)
            {
                StatusOverlay.Visibility = Visibility.Collapsed;
                return;
            }

            if (!_tabNetworkStats.TryGetValue(tabItem, out var stats))
            {
                stats = new TabNetworkInfo();
                _tabNetworkStats[tabItem] = stats;
            }

            TxtStatusUrl.Text = currentUrl ?? "Current tab";
            TxtCacheUsage.Text = $"Cache: {FormatBytes(stats.CachedBytes)}";
            TxtDownloadUsage.Text = $"Download: {FormatBytes(stats.DownloadedBytes)}";
            TxtTotalUsage.Text = $"Total: {FormatBytes(stats.TotalBytes)}";

            if (_currentSettings.AutoHideStatus)
            {
                StatusOverlay.Visibility = Visibility.Collapsed;
                StatusOverlay.Opacity = 0;
            }
            else
            {
                StatusOverlay.Visibility = Visibility.Visible;
                StatusOverlay.Opacity = 1;
            }
        }
        private void CoreWebView2_WebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
        {
            var tabItem = GetTabItemForCoreWebView2(sender as CoreWebView2) ?? BrowserTabs.SelectedItem as TabItem;
            if (tabItem == null) return;

            string uri = e.Request.Uri;
            string lowerUri = uri.ToLowerInvariant();
            _lastRequestUrl = uri;

            // Check dynamically skipped/blocked hosts for this tab
            if (tabItem != null && _tabNetworkStats.TryGetValue(tabItem, out var stats))
            {
                if (stats.SkippedHosts.Any(sh => lowerUri.Contains(sh.ToLowerInvariant())))
                {
                    if (sender is CoreWebView2 wv)
                    {
                        e.Response = wv.Environment.CreateWebResourceResponse(null, 403, "Forbidden", "");
                    }
                    return;
                }
            }

            UpdateStatus($"Requesting: {uri}", "Queued");

            // Allow Google APIs and essential scripts for Colab
            if (lowerUri.Contains("gstatic.com") || lowerUri.Contains("googleapis.com") || lowerUri.Contains("google.com/accounts"))
            {
                return; // Allow the request by not setting e.Response
            }

            if (_currentSettings == null) return;
            var ctx = e.ResourceContext;
            if (IsTrackerOrAd(lowerUri)) { if (sender is CoreWebView2 wv) e.Response = wv.Environment.CreateWebResourceResponse(null, 403, "Forbidden", ""); return; }
            if (!_currentSettings.LoadImages && IsImageContext(ctx, lowerUri)) { if (sender is CoreWebView2 wv) e.Response = wv.Environment.CreateWebResourceResponse(null, 403, "Forbidden", ""); return; }
            if (!_currentSettings.LoadMedia && IsMediaContext(ctx, lowerUri)) { if (sender is CoreWebView2 wv) e.Response = wv.Environment.CreateWebResourceResponse(null, 403, "Forbidden", ""); return; }
            if (IsHostNoCached(uri) || e.Request.Method != "GET") return;
            if (IsCacheableRequest(ctx, lowerUri))
            {
                string? cachePath = GetCacheFilePath(uri);
                if (!string.IsNullOrEmpty(cachePath) && File.Exists(cachePath))
                {
                    try
                    {
                        if (sender is CoreWebView2 wv && TryCreateCachedResponse(wv, e.Request, ctx, lowerUri, cachePath, out var response, out var servedBytes))
                        {
                            e.Response = response;
                            AddTabCachedBytes(tabItem, $"cache:{uri}:{GetRequestRange(e.Request)}", servedBytes);
                        }
                        UpdateStatus(uri, "Disk cache");
                        return;
                    }
                    catch { }
                }
            }
        }

        private async void CoreWebView2_WebResourceResponseReceived(object? sender, CoreWebView2WebResourceResponseReceivedEventArgs e)
        {
            var tabItem = GetTabItemForCoreWebView2(sender as CoreWebView2) ?? BrowserTabs.SelectedItem as TabItem;
            if (tabItem == null) return;

            string uri = e.Request.Uri;
            string lowerUri = uri.ToLowerInvariant();
            long size = 0;
            if (e.Response.Headers.Contains("Content-Length")) { long.TryParse(e.Response.Headers.GetHeader("Content-Length"), out size); }
            if (size <= 0)
            {
                try
                {
                    using (var content = await e.Response.GetContentAsync())
                    {
                        if (content != null) size = content.Length;
                    }
                }
                catch { }
            }
            bool servedFromDiskCache = HasImgSaverCacheHit(e.Response);
            if (size > 0)
            {
                if (servedFromDiskCache)
                    AddTabCachedBytes(tabItem, $"cache-response:{uri}:{GetRequestRange(e.Request)}", size);
                else
                    AddTabDownloadedBytes(tabItem, $"network:{uri}", size);
            }
            UpdateStatus(uri, FormatBytes(size));
            if (e.Response.StatusCode != 200 || e.Request.Method != "GET") return;

            if (HasAttachmentDisposition(e.Response))
            {
                return; // Don't cache downloads
            }

            // Skip caching if host is in no-cache list
            if (IsHostNoCached(uri)) return;

            if (IsCacheableResponse(lowerUri, e.Response))
                await SaveCacheResponseAsync(sender as CoreWebView2, tabItem, uri, e.Response, size);
        }

        private bool HasAttachmentDisposition(CoreWebView2WebResourceResponseView response)
        {
            try
            {
                return response.Headers.Contains("Content-Disposition") &&
                    response.Headers.GetHeader("Content-Disposition")
                        .Contains("attachment", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private async Task SaveCacheResponseAsync(CoreWebView2? coreWebView2, TabItem? tabItem, string uri, CoreWebView2WebResourceResponseView response, long contentLength)
        {
            try
            {
                bool isImage = IsImageResponse(response);
                bool shouldImportImage = isImage && !IsComfyUiTransientImageUri(uri);
                if (ShouldSkipDiskCache(response, contentLength))
                {
                    if (shouldImportImage)
                        await SaveTemporaryImageImportAsync(coreWebView2, uri, response);
                    return;
                }

                string? cachePath = GetCacheFilePath(uri, response);
                if (string.IsNullOrWhiteSpace(cachePath)) return;
                if (File.Exists(cachePath))
                {
                    if (shouldImportImage)
                        await ImportImageToMiniClipAsync(coreWebView2, uri, cachePath);
                    return;
                }

                string? dir = Path.GetDirectoryName(cachePath);
                if (string.IsNullOrWhiteSpace(dir)) return;
                Directory.CreateDirectory(dir);

                string tempPath = cachePath + ".tmp";
                long savedBytes;
                using (var content = await response.GetContentAsync())
                {
                    if (content == null) return;
                    using var output = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
                    await content.CopyToAsync(output);
                    savedBytes = output.Length;
                    if (output.Length == 0 || output.Length > MaxDiskCacheItemBytes)
                    {
                        output.Close();
                        try { File.Delete(tempPath); } catch { }
                        return;
                    }
                }

                if (!File.Exists(cachePath))
                    File.Move(tempPath, cachePath);
                else
                    File.Delete(tempPath);

                if (tabItem != null && contentLength <= 0)
                    AddTabDownloadedBytes(tabItem, $"network:{uri}", savedBytes);

                if (shouldImportImage)
                    await ImportImageToMiniClipAsync(coreWebView2, uri, cachePath);
            }
            catch { }
        }

        private bool IsComfyUiTransientImageUri(string uri)
        {
            try
            {
                var parsedUri = new Uri(uri);
                string path = parsedUri.AbsolutePath.TrimEnd('/').ToLowerInvariant();
                if (!path.EndsWith("/view") && !path.EndsWith("/api/view")) return false;

                var query = ParseQueryString(parsedUri.Query);
                query.TryGetValue("type", out string? type);
                query.TryGetValue("filename", out string? filename);
                type ??= "";
                filename ??= "";

                if (type.Equals("output", StringComparison.OrdinalIgnoreCase)) return false;
                if (type.Equals("temp", StringComparison.OrdinalIgnoreCase) ||
                    type.Equals("preview", StringComparison.OrdinalIgnoreCase))
                    return true;

                return filename.Contains("temp", StringComparison.OrdinalIgnoreCase) ||
                       filename.Contains("preview", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private static Dictionary<string, string> ParseQueryString(string query)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(query)) return values;

            foreach (string part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] pair = part.Split('=', 2);
                string key = Uri.UnescapeDataString(pair[0].Replace("+", " "));
                if (string.IsNullOrWhiteSpace(key)) continue;

                string value = pair.Length > 1
                    ? Uri.UnescapeDataString(pair[1].Replace("+", " "))
                    : "";
                values[key] = value;
            }
            return values;
        }

        private async Task SaveTemporaryImageImportAsync(CoreWebView2? coreWebView2, string uri, CoreWebView2WebResourceResponseView response)
        {
            try
            {
                if (_miniClipImportedImageUris.Contains(uri)) return;

                Directory.CreateDirectory(_miniClipImportFolder);
                string tempPath = Path.Combine(_miniClipImportFolder, $"{CreateStableHash(uri)}{GetExtensionFromResponse(response)}");

                using (var content = await response.GetContentAsync())
                {
                    if (content == null) return;
                    using var output = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
                    await content.CopyToAsync(output);
                    if (output.Length == 0 || output.Length > MaxDiskCacheItemBytes)
                    {
                        output.Close();
                        try { File.Delete(tempPath); } catch { }
                        return;
                    }
                }

                await ImportImageToMiniClipAsync(coreWebView2, uri, tempPath);
            }
            catch { }
        }

        private static string CreateStableHash(string value)
        {
            using MD5 md5 = MD5.Create();
            byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(value));
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        private async Task ImportImageToMiniClipAsync(CoreWebView2? coreWebView2, string uri, string cachePath, bool force = false)
        {
            if (!force)
            {
                var settings = _currentSettings ?? BrowserSettings.Load();
                if (!settings.AutoImportImagesToMiniClip) return;
            }

            if (IsAiWorkflowImageContext(coreWebView2, uri))
                await TryImportDomConfirmedImageToMiniClipAsync(coreWebView2, uri, cachePath, force);
            else
                await TryImportCachedImageToMiniClipAsync(uri, cachePath, force);
        }

        private async Task TryImportDomConfirmedImageToMiniClipAsync(CoreWebView2? coreWebView2, string uri, string cachePath, bool force = false)
        {
            try
            {
                await Task.Delay(1000);
                if (!force && !await IsImageLoadedInCurrentPageAsync(coreWebView2, uri)) return;
                await TryImportCachedImageToMiniClipAsync(uri, cachePath, force);
            }
            catch { }
        }

        private bool IsAiWorkflowImageContext(CoreWebView2? coreWebView2, string uri)
        {
            try
            {
                string pageUrl = coreWebView2?.Source ?? "";
                string title = coreWebView2?.DocumentTitle ?? "";
                string combined = $"{pageUrl}\n{title}\n{uri}".ToLowerInvariant();

                return combined.Contains("comfyui") ||
                       combined.Contains("comfy-ui") ||
                       combined.Contains("/workflow") && combined.Contains("seaart.") ||
                       combined.Contains("/api/view") ||
                       combined.Contains("/view?");
            }
            catch { return false; }
        }

        private async Task<bool> IsImageLoadedInCurrentPageAsync(CoreWebView2? coreWebView2, string uri)
        {
            if (coreWebView2 == null) return true;
            try
            {
                string target = System.Text.Json.JsonSerializer.Serialize(uri);
                string script = $@"
(() => {{
  const target = {target};
  const minRendered = 96;
  const normalize = (value) => {{
    try {{ return new URL(value || '', document.baseURI).href; }}
    catch {{ return value || ''; }}
  }};
  return Array.from(document.images).some(img => {{
    if (!img.complete || img.naturalWidth <= 0 || img.naturalHeight <= 0) return false;
    if (normalize(img.currentSrc || img.src) !== target) return false;

    const rect = img.getBoundingClientRect();
    const style = getComputedStyle(img);
    return rect.width >= minRendered &&
      rect.height >= minRendered &&
      rect.bottom > 0 &&
      rect.right > 0 &&
      rect.top < innerHeight &&
      rect.left < innerWidth &&
      style.visibility !== 'hidden' &&
      style.display !== 'none';
  }});
}})()";
                string result = await coreWebView2.ExecuteScriptAsync(script);
                return result.Equals("true", StringComparison.OrdinalIgnoreCase);
            }
            catch { return true; }
        }

        private async Task TryImportCachedImageToMiniClipAsync(string uri, string cachePath, bool force = false)
        {
            try
            {
                if (!force && _miniClipImportedImageUris.Contains(uri)) return;
                if (!File.Exists(cachePath)) return;

                var settings = _currentSettings ?? BrowserSettings.Load();
                string? imageSignature = GetImageImportSignature(cachePath, settings.MinImageWidth, settings.MinImageHeight);
                if (string.IsNullOrEmpty(imageSignature)) return;
                if (!force && _miniClipImportedImageSignatures.Contains(imageSignature)) return;

                _miniClipImportedImageUris.Add(uri);
                _miniClipImportedImageSignatures.Add(imageSignature);

                await Dispatcher.InvokeAsync(() =>
                {
                    var miniClip = GetOpenMiniClipboardWindow();
                    if (miniClip != null)
                    {
                        miniClip.ImportBrowserImage(cachePath, settings.MinImageWidth, settings.MinImageHeight, settings.ReplaceMiniClipImageOnImport);
                    }
                });
            }
            catch { }
        }

        private void CoreWebView2_ContextMenuRequested(object? sender, CoreWebView2ContextMenuRequestedEventArgs e)
        {
            try
            {
                string imageUri = e.ContextMenuTarget?.SourceUri ?? "";
                if (string.IsNullOrWhiteSpace(imageUri)) return;
                if (!Uri.TryCreate(imageUri, UriKind.Absolute, out var parsed) ||
                    (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
                    return;

                var coreWebView2 = sender as CoreWebView2;
                var importItem = coreWebView2?.Environment.CreateContextMenuItem(
                    "Import image to Mini Clip again",
                    null,
                    CoreWebView2ContextMenuItemKind.Command);
                if (importItem == null) return;

                importItem.CustomItemSelected += async (_, _) =>
                {
                    await ManualImportImageToMiniClipAsync(coreWebView2, imageUri);
                };

                e.MenuItems.Insert(0, importItem);
            }
            catch { }
        }

        private async Task ManualImportImageToMiniClipAsync(CoreWebView2? coreWebView2, string uri)
        {
            try
            {
                string? cachePath = GetCacheFilePath(uri);
                if (!string.IsNullOrWhiteSpace(cachePath) && File.Exists(cachePath))
                {
                    await ImportImageToMiniClipAsync(coreWebView2, uri, cachePath, force: true);
                    return;
                }

                string tempPath = await DownloadImageForManualImportAsync(coreWebView2, uri);
                if (!string.IsNullOrWhiteSpace(tempPath) && File.Exists(tempPath))
                    await ImportImageToMiniClipAsync(coreWebView2, uri, tempPath, force: true);
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Could not import image: {ex.Message}", "Mini Clip Import", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async Task<string> DownloadImageForManualImportAsync(CoreWebView2? coreWebView2, string uri)
        {
            Directory.CreateDirectory(_miniClipImportFolder);
            string extension = GetImageExtensionFromUri(uri);
            string tempPath = Path.Combine(_miniClipImportFolder, $"{CreateStableHash(uri)}_manual_{DateTime.UtcNow:yyyyMMddHHmmssfff}{extension}");

            using var client = new System.Net.Http.HttpClient();
            var headers = await GetDownloadHeadersAsync(coreWebView2, uri);
            if (headers != null)
            {
                foreach (var header in headers)
                    client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
            }

            using var response = await client.GetAsync(uri);
            response.EnsureSuccessStatusCode();

            string contentType = response.Content.Headers.ContentType?.MediaType ?? "";
            if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The selected resource is not an image.");

            await using var output = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
            await response.Content.CopyToAsync(output);
            if (output.Length == 0 || output.Length > MaxDiskCacheItemBytes)
                throw new InvalidOperationException("The selected image could not be saved for import.");

            return tempPath;
        }

        private string GetImageExtensionFromUri(string uri)
        {
            try
            {
                string extension = Path.GetExtension(new Uri(uri).AbsolutePath);
                if (extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                    extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                    extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
                    extension.Equals(".webp", StringComparison.OrdinalIgnoreCase) ||
                    extension.Equals(".gif", StringComparison.OrdinalIgnoreCase) ||
                    extension.Equals(".avif", StringComparison.OrdinalIgnoreCase) ||
                    extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase))
                    return extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ? ".jpg" : extension.ToLowerInvariant();
            }
            catch { }

            return ".img";
        }

        private string? GetImageImportSignature(string path, int minWidth, int minHeight)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                var frame = decoder.Frames.FirstOrDefault();
                if (frame == null || frame.PixelWidth < minWidth || frame.PixelHeight < minHeight) return null;

                BitmapSource source = frame;
                if (source.Format != PixelFormats.Bgra32)
                {
                    var converted = new FormatConvertedBitmap();
                    converted.BeginInit();
                    converted.Source = source;
                    converted.DestinationFormat = PixelFormats.Bgra32;
                    converted.EndInit();
                    source = converted;
                }

                int stride = source.PixelWidth * 4;
                byte[] pixels = new byte[stride * source.PixelHeight];
                source.CopyPixels(pixels, stride, 0);

                using MD5 md5 = MD5.Create();
                byte[] hash = md5.ComputeHash(pixels);
                return $"{source.PixelWidth}x{source.PixelHeight}:{BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant()}";
            }
            catch { return null; }
        }

        private MiniClipboardWindow? GetOpenMiniClipboardWindow()
        {
            try
            {
                foreach (Window window in System.Windows.Application.Current.Windows)
                {
                    if (window is MiniClipboardWindow miniClip && miniClip.IsLoaded)
                        return miniClip;
                }
            }
            catch { }
            return null;
        }

        private bool IsImageResponse(CoreWebView2WebResourceResponseView response)
        {
            try
            {
                if (!response.Headers.Contains("Content-Type")) return false;
                return response.Headers.GetHeader("Content-Type")
                    .StartsWith("image/", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private bool ShouldSkipDiskCache(CoreWebView2WebResourceResponseView response, long contentLength)
        {
            try
            {
                if (contentLength > MaxDiskCacheItemBytes) return true;
                if (response.Headers.Contains("Cache-Control"))
                {
                    string cacheControl = response.Headers.GetHeader("Cache-Control").ToLowerInvariant();
                    if (cacheControl.Contains("no-store"))
                        return true;
                }
                if (response.Headers.Contains("Pragma") &&
                    response.Headers.GetHeader("Pragma").Contains("no-cache", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            catch { }
            return false;
        }



        private async Task<Dictionary<string, string>?> GetDownloadHeadersAsync(CoreWebView2? coreWebView2, string uri)
        {
            if (coreWebView2 == null) return null;
            try
            {
                var headers = new Dictionary<string, string>();
                var tabItem = GetTabItemForCoreWebView2(coreWebView2);
                if (tabItem != null && TryGetTabState(tabItem, out var state) && state.PrimaryWebView?.Source != null)
                {
                    headers["Referer"] = GetAsciiUriHeader(state.PrimaryWebView.Source);
                }

                var cookies = await coreWebView2.CookieManager.GetCookiesAsync(uri);
                if (cookies != null && cookies.Count > 0)
                {
                    string cookieHeader = string.Join("; ", cookies
                        .Where(c => IsAsciiHeaderName(c.Name))
                        .Select(c => $"{c.Name}={EscapeCookieValue(c.Value)}"));
                    if (!string.IsNullOrEmpty(cookieHeader))
                        headers["Cookie"] = cookieHeader;
                }

                return headers.Count > 0 ? headers : null;
            }
            catch { return null; }
        }

        private static string GetAsciiUriHeader(Uri uri)
        {
            try
            {
                return uri.GetComponents(UriComponents.AbsoluteUri, UriFormat.UriEscaped);
            }
            catch
            {
                return RemoveNonAscii(uri.AbsoluteUri);
            }
        }

        private static string EscapeCookieValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return IsAsciiHeaderValue(value)
                ? value
                : Uri.EscapeDataString(value);
        }

        private static bool IsAsciiHeaderName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            return value.All(c => c > 32 && c < 127 && "()<>@,;:\\\"/[]?={} \t".IndexOf(c) < 0);
        }

        private static bool IsAsciiHeaderValue(string value)
        {
            return value.All(c => c == '\t' || c == '\r' || c == '\n' || (c >= 32 && c < 127));
        }

        private static string RemoveNonAscii(string value)
        {
            return new string(value.Where(c => c == '\t' || (c >= 32 && c < 127)).ToArray());
        }

        private string GetDownloadFileName(string uri, CoreWebView2WebResourceResponseView? response = null)
        {
            try
            {
                if (response != null && response.Headers.Contains("Content-Disposition"))
                {
                    string header = response.Headers.GetHeader("Content-Disposition");
                    var match = Regex.Match(header, "filename\\*?=(?:UTF-8''?)?\\\"?([^\\\";]+)\\\"?", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        string filename = Uri.UnescapeDataString(match.Groups[1].Value.Trim('"'));
                        if (!string.IsNullOrWhiteSpace(filename)) return filename;
                    }
                }

                var parsedUri = new Uri(uri);
                string fileName = Path.GetFileName(parsedUri.LocalPath);
                if (!string.IsNullOrWhiteSpace(fileName)) return fileName;
            }
            catch { }
            return $"download_{Guid.NewGuid():N}.bin";
        }

        private void CoreWebView2_DownloadStarting(object? sender, CoreWebView2DownloadStartingEventArgs e)
        {
            // Standard browser download handling (unhandled)
        }

        private bool IsHostNoCached(string uri)
        {
            try
            {
                Uri parsedUri = new Uri(uri);
                string host = parsedUri.Host;
                if (_currentSettings?.NoCacheHosts != null && _currentSettings.NoCacheHosts.Count > 0)
                {
                    return _currentSettings.NoCacheHosts.Any(h =>
                        host.Equals(h, StringComparison.OrdinalIgnoreCase) ||
                        host.EndsWith("." + h, StringComparison.OrdinalIgnoreCase)
                    );
                }
                return false;
            }
            catch { return false; }
        }

        private void UpdateStatus(string url, string sizeInfo)
        {
            Dispatcher.Invoke(() => {
                TxtStatusUrl.Text = $"{url} - {sizeInfo}";
                UpdateTabStatusOverlay(BrowserTabs.SelectedItem as TabItem, TxtStatusUrl.Text);
            });
        }

        private void HideStatus()
        {
            if (_currentSettings.AutoHideStatus)
            {
                StatusOverlay.Visibility = Visibility.Collapsed;
                StatusOverlay.Opacity = 0;
            }
            else
            {
                StatusOverlay.Visibility = Visibility.Visible;
                StatusOverlay.Opacity = 1;
            }
        }

        private void BtnSkipRequest_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_lastRequestUrl)) return;

            try
            {
                var uriObj = new Uri(_lastRequestUrl);
                string host = uriObj.Host;
                if (!string.IsNullOrEmpty(host))
                {
                    var tabItem = BrowserTabs.SelectedItem as TabItem;
                    if (tabItem != null)
                    {
                        if (!_tabNetworkStats.TryGetValue(tabItem, out var stats))
                        {
                            stats = new TabNetworkInfo();
                            _tabNetworkStats[tabItem] = stats;
                        }
                        stats.SkippedHosts.Add(host);
                    }

                    UpdateStatus($"Temporarily Skipped: {host} (Cleared on reload)", "Bypassed");

                    var browser = GetCurrentBrowser();
                    if (browser != null)
                    {
                        browser.CoreWebView2.Stop();
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Skip failed: {ex.Message}", "Error");
            }
        }

        private string FormatBytes(long bytes)
        {
            string[] Suffix = { "B", "KB", "MB", "GB" };
            int i; double dblSByte = bytes;
            for (i = 0; i < Suffix.Length && bytes >= 1024; i++, bytes /= 1024) { dblSByte = bytes / 1024.0; }
            return string.Format("{0:0.##} {1}", dblSByte, Suffix[i]);
        }

        private bool IsTrackerOrAd(string uri) => uri.Contains("google-analytics.com") || uri.Contains("doubleclick.net") || uri.Contains("googletagmanager.com") || uri.Contains("facebook.net") || uri.Contains("adservice.google") || uri.Contains("analytics.") || uri.Contains("/ads/") || uri.Contains("pixel.");
        private bool IsCacheableRequest(CoreWebView2WebResourceContext ctx, string uri)
        {
            if (IsStaticAssetExtension(uri)) return true;

            return ctx == CoreWebView2WebResourceContext.Script ||
                ctx == CoreWebView2WebResourceContext.Stylesheet ||
                ctx == CoreWebView2WebResourceContext.Font ||
                ctx == CoreWebView2WebResourceContext.Image ||
                ctx == CoreWebView2WebResourceContext.Media;
        }

        private bool IsCacheableResponse(string uri, CoreWebView2WebResourceResponseView response)
        {
            if (IsImageResponse(response)) return true;
            if (IsStaticAssetExtension(uri)) return true;

            string contentType = GetContentType(response);
            if (contentType.StartsWith("font/", StringComparison.OrdinalIgnoreCase)) return true;
            if (contentType.Contains("javascript", StringComparison.OrdinalIgnoreCase)) return true;
            if (contentType.Equals("text/css", StringComparison.OrdinalIgnoreCase)) return true;
            if (contentType.Equals("application/wasm", StringComparison.OrdinalIgnoreCase)) return true;

            return false;
        }

        private bool IsCacheableContext(CoreWebView2WebResourceContext ctx, string uri) =>
            ctx == CoreWebView2WebResourceContext.Script ||
            ctx == CoreWebView2WebResourceContext.Stylesheet ||
            ctx == CoreWebView2WebResourceContext.Font ||
            ctx == CoreWebView2WebResourceContext.Image ||
            ctx == CoreWebView2WebResourceContext.Media ||
            ctx == CoreWebView2WebResourceContext.Fetch ||
            ctx == CoreWebView2WebResourceContext.XmlHttpRequest ||
            IsStaticAssetExtension(uri);

        private bool IsCacheableExtension(string uri) => IsStaticAssetExtension(uri);

        private bool IsStaticAssetExtension(string uri) =>
            uri.Contains(".js") || uri.Contains(".mjs") || uri.Contains(".css") ||
            uri.Contains(".woff2") || uri.Contains(".woff") ||
            uri.Contains(".ttf") || uri.Contains(".otf") || uri.Contains(".eot") ||
            uri.Contains(".wasm") || uri.Contains(".json") ||
            uri.Contains(".svg") || uri.Contains(".webp") ||
            uri.Contains(".png") || uri.Contains(".jpg") ||
            uri.Contains(".jpeg") || uri.Contains(".gif") ||
            uri.Contains(".avif") || uri.Contains(".ico") ||
            uri.Contains(".mp4") || uri.Contains(".webm") ||
            uri.Contains(".mp3") || uri.Contains(".m4a");
        private bool IsImageContext(CoreWebView2WebResourceContext ctx, string uri) => ctx == CoreWebView2WebResourceContext.Image || uri.EndsWith(".jpg") || uri.EndsWith(".png") || uri.EndsWith(".webp") || uri.EndsWith(".gif");
        private bool IsMediaContext(CoreWebView2WebResourceContext ctx, string uri) => ctx == CoreWebView2WebResourceContext.Media || uri.EndsWith(".mp4") || uri.EndsWith(".webm") || uri.EndsWith(".mp3");

        private string? GetCacheFilePath(string uri, CoreWebView2WebResourceResponseView? response = null)
        {
            try
            {
                Uri parsedUri = new Uri(uri);
                string host = SanitizeHostForCache(parsedUri.Host);
                string siteFolder = Path.Combine(_permanentCacheFolder, host);
                string lowerUri = uri.ToLowerInvariant();
                using (MD5 md5 = MD5.Create())
                {
                    byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(uri));
                    string filename = BitConverter.ToString(hash).Replace("-", "").ToLower();
                    if (lowerUri.Contains(".webp")) filename += ".webp";
                    else if (lowerUri.Contains(".png")) filename += ".png";
                    else if (lowerUri.Contains(".jpg")) filename += ".jpg";
                    else if (lowerUri.Contains(".jpeg")) filename += ".jpg";
                    else if (lowerUri.Contains(".gif")) filename += ".gif";
                    else if (lowerUri.Contains(".avif")) filename += ".avif";
                    else if (lowerUri.Contains(".ico")) filename += ".ico";
                    else if (lowerUri.Contains(".wasm")) filename += ".wasm";
                    else if (lowerUri.Contains(".mjs")) filename += ".mjs";
                    else if (lowerUri.Contains(".js")) filename += ".js";
                    else if (lowerUri.Contains(".css")) filename += ".css";
                    else if (lowerUri.Contains(".svg")) filename += ".svg";
                    else if (lowerUri.Contains(".json")) filename += ".json";
                    else if (lowerUri.Contains(".woff2")) filename += ".woff2";
                    else if (lowerUri.Contains(".woff")) filename += ".woff";
                    else if (lowerUri.Contains(".ttf")) filename += ".ttf";
                    else if (lowerUri.Contains(".otf")) filename += ".otf";
                    else if (lowerUri.Contains(".eot")) filename += ".eot";
                    else if (lowerUri.Contains(".mp4")) filename += ".mp4";
                    else if (lowerUri.Contains(".webm")) filename += ".webm";
                    else if (lowerUri.Contains(".mp3")) filename += ".mp3";
                    else filename += GetExtensionFromResponse(response);
                    return Path.Combine(siteFolder, filename);
                }
            }
            catch { return null; }
        }

        private string GetExtensionFromResponse(CoreWebView2WebResourceResponseView? response)
        {
            try
            {
                string contentType = GetContentType(response).ToLowerInvariant();
                return contentType switch
                {
                    "application/javascript" => ".js",
                    "text/javascript" => ".js",
                    "text/css" => ".css",
                    "application/wasm" => ".wasm",
                    "application/json" => ".json",
                    "font/woff2" => ".woff2",
                    "font/woff" => ".woff",
                    "application/font-woff" => ".woff",
                    "application/font-woff2" => ".woff2",
                    "application/x-font-ttf" => ".ttf",
                    "font/ttf" => ".ttf",
                    "font/otf" => ".otf",
                    "image/jpeg" => ".jpg",
                    "image/png" => ".png",
                    "image/webp" => ".webp",
                    "image/gif" => ".gif",
                    "image/bmp" => ".bmp",
                    "image/tiff" => ".tiff",
                    "image/avif" => ".avif",
                    "image/svg+xml" => ".svg",
                    _ => ""
                };
            }
            catch { return ""; }
        }

        private string GetMimeType(CoreWebView2WebResourceContext ctx, string uri)
        {
            if (uri.Contains(".js")) return "application/javascript";
            if (uri.Contains(".css")) return "text/css";
            if (uri.Contains(".wasm")) return "application/wasm";
            if (uri.Contains(".json")) return "application/json";
            if (uri.Contains(".woff2")) return "font/woff2";
            if (uri.Contains(".woff")) return "font/woff";
            if (uri.Contains(".svg")) return "image/svg+xml";
            if (uri.Contains(".webp")) return "image/webp";
            if (uri.Contains(".png")) return "image/png";
            if (uri.Contains(".jpg") || uri.Contains(".jpeg")) return "image/jpeg";
            if (uri.Contains(".gif")) return "image/gif";
            if (uri.Contains(".avif")) return "image/avif";
            if (uri.Contains(".ico")) return "image/x-icon";
            if (uri.Contains(".mp4")) return "video/mp4";
            if (uri.Contains(".webm")) return "video/webm";
            if (uri.Contains(".mp3")) return "audio/mpeg";
            return "application/octet-stream";
        }

        private string GetContentType(CoreWebView2WebResourceResponseView? response)
        {
            try
            {
                if (response == null || !response.Headers.Contains("Content-Type")) return "";
                return response.Headers.GetHeader("Content-Type").Split(';')[0].Trim();
            }
            catch { return ""; }
        }

        private bool HasImgSaverCacheHit(CoreWebView2WebResourceResponseView response)
        {
            try
            {
                return response.Headers.Contains("X-ImgSaver-Cache") &&
                    response.Headers.GetHeader("X-ImgSaver-Cache").Equals("HIT", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private string GetRequestRange(CoreWebView2WebResourceRequest request)
        {
            try
            {
                return request.Headers.Contains("Range") ? request.Headers.GetHeader("Range") : "";
            }
            catch { return ""; }
        }

        private bool TryCreateCachedResponse(CoreWebView2 coreWebView2, CoreWebView2WebResourceRequest request, CoreWebView2WebResourceContext ctx, string lowerUri, string cachePath, out CoreWebView2WebResourceResponse response, out long servedBytes)
        {
            response = null!;
            servedBytes = 0;

            var stream = File.OpenRead(cachePath);
            long fileLength = stream.Length;
            string mime = GetMimeType(ctx, lowerUri);
            string range = GetRequestRange(request);

            if (TryParseRange(range, fileLength, out long start, out long end))
            {
                stream.Position = start;
                servedBytes = end - start + 1;
                var rangedStream = new LimitedReadStream(stream, servedBytes);
                string headers =
                    $"Content-Type: {mime}\n" +
                    $"Content-Length: {servedBytes}\n" +
                    $"Content-Range: bytes {start}-{end}/{fileLength}\n" +
                    "Accept-Ranges: bytes\n" +
                    "Cache-Control: public, max-age=31536000, immutable\n" +
                    "Access-Control-Allow-Origin: *\n" +
                    "Timing-Allow-Origin: *\n" +
                    "X-ImgSaver-Cache: HIT";
                response = coreWebView2.Environment.CreateWebResourceResponse(rangedStream, 206, "Partial Content", headers);
                return true;
            }

            servedBytes = fileLength;
            string fullHeaders =
                $"Content-Type: {mime}\n" +
                $"Content-Length: {fileLength}\n" +
                "Accept-Ranges: bytes\n" +
                "Cache-Control: public, max-age=31536000, immutable\n" +
                "Access-Control-Allow-Origin: *\n" +
                "Timing-Allow-Origin: *\n" +
                "X-ImgSaver-Cache: HIT";
            response = coreWebView2.Environment.CreateWebResourceResponse(stream, 200, "OK", fullHeaders);
            return true;
        }

        private bool TryParseRange(string range, long fileLength, out long start, out long end)
        {
            start = 0;
            end = fileLength - 1;
            if (string.IsNullOrWhiteSpace(range) || !range.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase)) return false;

            string value = range.Substring("bytes=".Length).Split(',')[0].Trim();
            string[] parts = value.Split('-', 2);
            if (parts.Length != 2) return false;

            if (string.IsNullOrWhiteSpace(parts[0]))
            {
                if (!long.TryParse(parts[1], out long suffixLength) || suffixLength <= 0) return false;
                start = Math.Max(0, fileLength - suffixLength);
                end = fileLength - 1;
                return start <= end;
            }

            if (!long.TryParse(parts[0], out start)) return false;
            if (!string.IsNullOrWhiteSpace(parts[1]) && !long.TryParse(parts[1], out end)) return false;
            end = Math.Min(end, fileLength - 1);
            return start >= 0 && start <= end;
        }

        private sealed class LimitedReadStream : Stream
        {
            private readonly Stream _inner;
            private long _remaining;

            public LimitedReadStream(Stream inner, long length)
            {
                _inner = inner;
                _remaining = length;
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (_remaining <= 0) return 0;
                int read = _inner.Read(buffer, offset, (int)Math.Min(count, _remaining));
                _remaining -= read;
                return read;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing) _inner.Dispose();
                base.Dispose(disposing);
            }
        }


        private async void BtnBrowserSettings_Click(object? sender, RoutedEventArgs e)
        {
            var settingsWin = new BrowserSettingsWindow();
            settingsWin.Owner = this;
            settingsWin.ShowDialog();

            if (settingsWin.RequestClearData)
            {
                var browser = GetCurrentBrowser();
                if (browser?.CoreWebView2 != null)
                {
                    if (settingsWin.RequestDeleteLoginData)
                        await browser.CoreWebView2.Profile.ClearBrowsingDataAsync();

                    DeleteDirectoryContents(_permanentCacheFolder);
                    CustomMessageBox.Show(settingsWin.RequestDeleteLoginData
                        ? "Browser cache, cookies, and login data have been cleared."
                        : "Browser cache has been cleared.",
                        "Success");
                    browser.Reload();
                }
            }

            // Check if proxy settings changed BEFORE refreshing
            bool proxyChanged = false;
            var oldSettings = _currentSettings;
            var newSettings = BrowserSettings.Load();
            proxyChanged = (oldSettings.ProxyMode ?? "system") != (newSettings.ProxyMode ?? "system") ||
                          oldSettings.ProxyAddress != newSettings.ProxyAddress ||
                          oldSettings.ProxyPort != newSettings.ProxyPort ||
                          oldSettings.ProxyType != newSettings.ProxyType;

            RefreshSettings();
            BrowserRecordingFloatingWindowManager.SyncWithSettings(newSettings);

            if (proxyChanged)
            {
                SyncDownloadProxySettings();
                CustomMessageBox.Show("Proxy settings updated instantly. Active tabs do not need to be reloaded.", "Proxy Updated");
            }
            else
            {
                // For other settings, just apply them to existing tabs
                foreach (TabItem tab in BrowserTabs.Items)
                {
                    if (TryGetTabState(tab, out var state) && state.PrimaryWebView != null) ApplyBrowserSettingsTo(state.PrimaryWebView);
                }
            }
        }

        private async void BtnClearSiteData_Click(object? sender, RoutedEventArgs e)
        {
            var browser = GetCurrentBrowser();
            if (browser == null || browser.Source == null) return;
            string host = browser.Source.Host;
            if (string.IsNullOrEmpty(host)) return;
            if (CustomMessageBox.Show($"Clear all cached data and cookies for {host}?", "Clear Site Data", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                try
                {
                    DeleteDiskCacheForHost(host);
                }
                catch { }
                try
                {
                    var cookieManager = browser.CoreWebView2.CookieManager;
                    var cookies = await cookieManager.GetCookiesAsync(browser.Source.ToString());
                    foreach (var cookie in cookies) { cookieManager.DeleteCookie(cookie); }
                }
                catch { }
                try
                {
                    await ClearCurrentSiteClientStorageAsync(browser);
                }
                catch { }
                CustomMessageBox.Show($"Data for {host} has been cleared.", "Success");
                browser.Reload();
            }
        }

        private void DeleteDiskCacheForHost(string host)
        {
            string sanitizedHost = SanitizeHostForCache(host);
            string targetDir = Path.Combine(_permanentCacheFolder, sanitizedHost);
            if (Directory.Exists(targetDir))
                Directory.Delete(targetDir, true);
        }

        private string SanitizeHostForCache(string host)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                host = host.Replace(c, '_');
            return host;
        }

        private void DeleteDirectoryContents(string folder)
        {
            try
            {
                if (!Directory.Exists(folder)) return;
                foreach (var file in Directory.GetFiles(folder))
                    File.Delete(file);
                foreach (var dir in Directory.GetDirectories(folder))
                    Directory.Delete(dir, true);
            }
            catch { }
        }

        private async Task ClearCurrentSiteClientStorageAsync(WebView2 browser)
        {
            if (browser.CoreWebView2 == null) return;
            string script = """
(async () => {
  try { localStorage.clear(); } catch {}
  try { sessionStorage.clear(); } catch {}
  try {
    if (window.caches && caches.keys) {
      const keys = await caches.keys();
      await Promise.all(keys.map(k => caches.delete(k)));
    }
  } catch {}
  try {
    if (indexedDB && indexedDB.databases) {
      const dbs = await indexedDB.databases();
      await Promise.all(dbs.filter(db => db && db.name).map(db => new Promise(resolve => {
        const req = indexedDB.deleteDatabase(db.name);
        req.onsuccess = req.onerror = req.onblocked = () => resolve();
      })));
    }
  } catch {}
})();
""";
            await browser.CoreWebView2.ExecuteScriptAsync(script);
        }
    }
}
