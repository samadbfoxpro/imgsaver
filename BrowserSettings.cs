using System;
using System.IO;
using System.Collections.Generic;

namespace imgsaver
{
    public class BookmarkItem { public string Name { get; set; } = ""; public string Url { get; set; } = ""; }

    public class BrowserSettings
    {
        public bool LoadImages { get; set; } = true;
        public bool LoadMedia { get; set; } = true;
        public bool EnableJavaScript { get; set; } = true;
        public bool MuteAudio { get; set; } = false;
        public bool AutoHideStatus { get; set; } = true;
        public string LastUrl { get; set; } = "";
        public List<string> OpenTabs { get; set; } = new List<string>();
        public List<BrowserTabSession> TabSessions { get; set; } = new List<BrowserTabSession>();
        public int SelectedTabIndex { get; set; } = 0;
        public List<BookmarkItem> Bookmarks { get; set; } = new List<BookmarkItem>();

        public bool ProxyEnabled { get; set; } = false;
        public string ProxyType { get; set; } = "http";
        public string ProxyAddress { get; set; } = "";
        public string ProxyPort { get; set; } = "";

        // Minimum image dimensions for import to Mini Clipboard
        public bool AutoImportImagesToMiniClip { get; set; } = true;
        public bool ShowMiniClipImageImportButtons { get; set; } = false;
        public bool ReplaceMiniClipImageOnImport { get; set; } = false;
        public int MinImageWidth { get; set; } = 50;
        public int MinImageHeight { get; set; } = 50;

        // List of hosts that should not use page cache (only cookies/login cache)
        public List<string> NoCacheHosts { get; set; } = new List<string>();

        private static string FilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "browser_settings.json");

        public static BrowserSettings Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    string json = File.ReadAllText(FilePath);
                    return System.Text.Json.JsonSerializer.Deserialize<BrowserSettings>(json) ?? new BrowserSettings();
                }
            }
            catch { }
            return new BrowserSettings();
        }

        public void Save()
        {
            try
            {
                string? dir = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string json = System.Text.Json.JsonSerializer.Serialize(this);
                File.WriteAllText(FilePath, json);
            }
            catch { }
        }
    }

    public class BrowserTabSession
    {
        public string Url { get; set; } = "";
        public bool IsPinned { get; set; }
    }
}
