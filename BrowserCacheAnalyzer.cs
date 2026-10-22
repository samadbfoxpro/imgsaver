using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace imgsaver
{
    public class CacheCategoryStats
    {
        public string Key { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Icon { get; set; } = "📁";
        public string ColorHex { get; set; } = "#3B82F6";
        public long SizeBytes { get; set; }
        public int FileCount { get; set; }
        public double Percentage { get; set; }
        public string FormattedSize => BrowserCacheAnalyzer.FormatBytes(SizeBytes);
    }

    public class CachedSiteStats
    {
        public string Domain { get; set; } = "";
        public long SizeBytes { get; set; }
        public int FileCount { get; set; }
        public string FormattedSize => BrowserCacheAnalyzer.FormatBytes(SizeBytes);
    }

    public class CacheAnalysisResult
    {
        public long TotalSizeBytes { get; set; }
        public int TotalFileCount { get; set; }
        public int TotalSiteCount { get; set; }
        public string FormattedTotalSize => BrowserCacheAnalyzer.FormatBytes(TotalSizeBytes);
        public string CacheFolderPath { get; set; } = "";
        public Dictionary<string, CacheCategoryStats> Categories { get; set; } = new();
        public List<CachedSiteStats> Sites { get; set; } = new();
    }

    public static class BrowserCacheAnalyzer
    {
        // Category Definitions
        private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".webp", ".png", ".jpg", ".jpeg", ".gif", ".avif", ".ico", ".svg", ".bmp", ".tif", ".tiff"
        };

        private static readonly HashSet<string> MediaExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".webm", ".mp3", ".wav", ".ogg", ".m4a", ".aac", ".flac", ".avi", ".mkv", ".mov", ".m4v"
        };

        private static readonly HashSet<string> WebCodeExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".js", ".mjs", ".cjs", ".css", ".json", ".wasm", ".html", ".htm", ".map"
        };

        private static readonly HashSet<string> FontExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".woff2", ".woff", ".ttf", ".otf", ".eot"
        };

        public static Task<CacheAnalysisResult> AnalyzeAsync(string cacheFolderPath)
        {
            return Task.Run(() => Analyze(cacheFolderPath));
        }

        public static CacheAnalysisResult Analyze(string cacheFolderPath)
        {
            var result = new CacheAnalysisResult
            {
                CacheFolderPath = cacheFolderPath
            };

            var categories = new Dictionary<string, CacheCategoryStats>
            {
                ["images"] = new CacheCategoryStats
                {
                    Key = "images",
                    Title = "تصاویر و عکس‌ها (Images)",
                    Description = "فایل‌های WEBP, PNG, JPG, GIF, AVIF, SVG و سایر آیکون‌ها",
                    Icon = "🖼️",
                    ColorHex = "#38BDF8"
                },
                ["media"] = new CacheCategoryStats
                {
                    Key = "media",
                    Title = "ویدیو و رسانه‌ها (Media)",
                    Description = "فایل‌های ویدیویی و صوتی نظیر MP4, WEBM, MP3, WAV",
                    Icon = "🎬",
                    ColorHex = "#A855F7"
                },
                ["webCode"] = new CacheCategoryStats
                {
                    Key = "webCode",
                    Title = "کدهای وب و اسکریپت‌ها (Web Code)",
                    Description = "فایل‌های جاوااسکریپت (JS)، استایل‌ها (CSS)، داده‌های JSON و WASM",
                    Icon = "💻",
                    ColorHex = "#10B981"
                },
                ["fonts"] = new CacheCategoryStats
                {
                    Key = "fonts",
                    Title = "فونت‌های وب (Web Fonts)",
                    Description = "قلم‌های اینترنتی نظیر WOFF2, WOFF, TTF, OTF",
                    Icon = "🔤",
                    ColorHex = "#F59E0B"
                },
                ["other"] = new CacheCategoryStats
                {
                    Key = "other",
                    Title = "سایر داده‌ها و کش سیستمی (Other)",
                    Description = "فایل‌های موقت، کش باینری Chromium و سایر منابع",
                    Icon = "📦",
                    ColorHex = "#EC4899"
                }
            };

            var siteMap = new Dictionary<string, CachedSiteStats>(StringComparer.OrdinalIgnoreCase);

            if (!Directory.Exists(cacheFolderPath))
            {
                result.Categories = categories;
                return result;
            }

            try
            {
                var rootDir = new DirectoryInfo(cacheFolderPath);

                // Analyze by host/domain subdirectories
                foreach (var dir in rootDir.GetDirectories())
                {
                    if (dir.Name.Equals("chromium_internal", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string domainName = dir.Name;
                    long domainSize = 0;
                    int domainFiles = 0;

                    FileInfo[] files;
                    try
                    {
                        files = dir.GetFiles("*", SearchOption.AllDirectories);
                    }
                    catch
                    {
                        continue;
                    }

                    foreach (var file in files)
                    {
                        long len = 0;
                        try { len = file.Length; } catch { }

                        string ext = file.Extension;
                        string catKey = GetCategoryForExtension(ext);

                        categories[catKey].SizeBytes += len;
                        categories[catKey].FileCount++;

                        result.TotalSizeBytes += len;
                        result.TotalFileCount++;

                        domainSize += len;
                        domainFiles++;
                    }

                    if (domainFiles > 0)
                    {
                        siteMap[domainName] = new CachedSiteStats
                        {
                            Domain = domainName,
                            SizeBytes = domainSize,
                            FileCount = domainFiles
                        };
                    }
                }

                // Check any loose files directly in root
                foreach (var file in rootDir.GetFiles("*", SearchOption.TopDirectoryOnly))
                {
                    long len = 0;
                    try { len = file.Length; } catch { }

                    string ext = file.Extension;
                    string catKey = GetCategoryForExtension(ext);

                    categories[catKey].SizeBytes += len;
                    categories[catKey].FileCount++;

                    result.TotalSizeBytes += len;
                    result.TotalFileCount++;
                }
            }
            catch { }

            // Calculate percentages
            if (result.TotalSizeBytes > 0)
            {
                foreach (var cat in categories.Values)
                {
                    cat.Percentage = Math.Round((double)cat.SizeBytes / result.TotalSizeBytes * 100.0, 1);
                }
            }

            result.Categories = categories;
            result.Sites = siteMap.Values.OrderByDescending(s => s.SizeBytes).ToList();
            result.TotalSiteCount = result.Sites.Count;

            return result;
        }

        private static string GetCategoryForExtension(string ext)
        {
            if (string.IsNullOrEmpty(ext)) return "other";

            if (ImageExtensions.Contains(ext)) return "images";
            if (MediaExtensions.Contains(ext)) return "media";
            if (WebCodeExtensions.Contains(ext)) return "webCode";
            if (FontExtensions.Contains(ext)) return "fonts";

            return "other";
        }

        public static Task<CacheAnalysisResult> DeleteCategoryAsync(string cacheFolderPath, string categoryKey)
        {
            return Task.Run(() =>
            {
                if (!Directory.Exists(cacheFolderPath)) return Analyze(cacheFolderPath);

                if (categoryKey.Equals("all", StringComparison.OrdinalIgnoreCase))
                {
                    DeleteAllContents(cacheFolderPath);
                    return Analyze(cacheFolderPath);
                }

                try
                {
                    var rootDir = new DirectoryInfo(cacheFolderPath);
                    var files = rootDir.GetFiles("*", SearchOption.AllDirectories);

                    foreach (var file in files)
                    {
                        try
                        {
                            if (file.FullName.Contains("chromium_internal", StringComparison.OrdinalIgnoreCase))
                                continue;

                            string ext = file.Extension;
                            string cat = GetCategoryForExtension(ext);

                            if (cat.Equals(categoryKey, StringComparison.OrdinalIgnoreCase))
                            {
                                file.Delete();
                            }
                        }
                        catch { }
                    }

                    CleanEmptyDirectories(cacheFolderPath);
                }
                catch { }

                return Analyze(cacheFolderPath);
            });
        }

        public static Task<CacheAnalysisResult> DeleteSiteAsync(string cacheFolderPath, string domain)
        {
            return Task.Run(() =>
            {
                if (!Directory.Exists(cacheFolderPath) || string.IsNullOrWhiteSpace(domain))
                    return Analyze(cacheFolderPath);

                if (domain.Equals("chromium_internal", StringComparison.OrdinalIgnoreCase))
                    return Analyze(cacheFolderPath);

                try
                {
                    string sitePath = Path.Combine(cacheFolderPath, domain);
                    if (Directory.Exists(sitePath))
                    {
                        Directory.Delete(sitePath, true);
                    }
                }
                catch { }

                return Analyze(cacheFolderPath);
            });
        }

        public static void DeleteAllContents(string folderPath)
        {
            if (!Directory.Exists(folderPath)) return;

            try
            {
                var dir = new DirectoryInfo(folderPath);
                foreach (var f in dir.GetFiles())
                {
                    try { f.Delete(); } catch { }
                }
                foreach (var d in dir.GetDirectories())
                {
                    if (d.Name.Equals("chromium_internal", StringComparison.OrdinalIgnoreCase))
                        continue;
                    try { d.Delete(true); } catch { }
                }
            }
            catch { }
        }

        public static void CleanEmptyDirectories(string startLocation)
        {
            if (!Directory.Exists(startLocation)) return;

            foreach (var directory in Directory.GetDirectories(startLocation))
            {
                if (directory.Contains("chromium_internal", StringComparison.OrdinalIgnoreCase))
                    continue;

                CleanEmptyDirectories(directory);
                if (Directory.GetFiles(directory).Length == 0 &&
                    Directory.GetDirectories(directory).Length == 0)
                {
                    try { Directory.Delete(directory, false); } catch { }
                }
            }
        }

        public static string FormatBytes(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double value = bytes;
            int unitIndex = 0;
            while (value >= 1024 && unitIndex < units.Length - 1)
            {
                value /= 1024;
                unitIndex++;
            }

            return unitIndex == 0 ? $"{bytes} {units[unitIndex]}" : $"{value:0.##} {units[unitIndex]}";
        }
    }
}
