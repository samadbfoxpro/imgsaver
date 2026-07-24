using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace imgsaver
{
    public static class ProfileManager
    {
        private static readonly string ProfilesDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "profiles");
        private static readonly string ProfilesConfigFile = Path.Combine(ProfilesDirectory, "profiles.json");
        private static readonly string LastUsedFile = Path.Combine(ProfilesDirectory, "last_used.txt");

        private static readonly List<string> ProfileColors = new()
        {
            "#2ECC71", // Emerald Green
            "#3498DB", // Dodger Blue
            "#9B59B6", // Amethyst Purple
            "#E74C3C", // Red
            "#F1C40F", // Gold Yellow
            "#1ABC9C", // Turquoise
            "#E67E22", // Orange
            "#E91E63", // Pink
            "#00BCD4"  // Cyan
        };

        private static readonly List<string> ProfileIcons = new()
        {
            "👤", "💼", "🚀", "⚡", "⭐", "🔥", "🎯", "👑", "💎"
        };

        public static List<BrowserProfile> LoadProfiles()
        {
            try
            {
                if (!Directory.Exists(ProfilesDirectory))
                    Directory.CreateDirectory(ProfilesDirectory);

                if (File.Exists(ProfilesConfigFile))
                {
                    string json = File.ReadAllText(ProfilesConfigFile);
                    var list = JsonSerializer.Deserialize<List<BrowserProfile>>(json);
                    if (list != null && list.Count > 0)
                        return list;
                }
            }
            catch { }

            // Default 5 pre-configured account profiles if none exist!
            var defaultProfiles = new List<BrowserProfile>();
            for (int i = 1; i <= 5; i++)
            {
                defaultProfiles.Add(new BrowserProfile
                {
                    Id = $"account_{i}",
                    Name = $"Account {i}",
                    ColorHex = ProfileColors[(i - 1) % ProfileColors.Count],
                    Icon = ProfileIcons[(i - 1) % ProfileIcons.Count],
                    IsDefault = (i == 1),
                    LastUsed = DateTime.Now.AddMinutes(-i)
                });
            }

            SaveProfiles(defaultProfiles);
            return defaultProfiles;
        }

        public static void SaveProfiles(List<BrowserProfile> profiles)
        {
            try
            {
                if (!Directory.Exists(ProfilesDirectory))
                    Directory.CreateDirectory(ProfilesDirectory);

                string json = JsonSerializer.Serialize(profiles, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ProfilesConfigFile, json);
            }
            catch { }
        }

        public static BrowserProfile GetActiveProfile()
        {
            var profiles = LoadProfiles();
            string lastId = "";
            try
            {
                if (File.Exists(LastUsedFile))
                    lastId = File.ReadAllText(LastUsedFile).Trim();
            }
            catch { }

            var active = profiles.FirstOrDefault(p => p.Id == lastId)
                      ?? profiles.FirstOrDefault(p => p.IsDefault)
                      ?? profiles.FirstOrDefault()
                      ?? new BrowserProfile();

            return active;
        }

        public static void SetActiveProfile(BrowserProfile profile)
        {
            try
            {
                if (!Directory.Exists(ProfilesDirectory))
                    Directory.CreateDirectory(ProfilesDirectory);

                profile.LastUsed = DateTime.Now;
                File.WriteAllText(LastUsedFile, profile.Id);

                var list = LoadProfiles();
                var existing = list.FirstOrDefault(p => p.Id == profile.Id);
                if (existing != null)
                {
                    existing.LastUsed = DateTime.Now;
                    existing.Name = profile.Name;
                    existing.ColorHex = profile.ColorHex;
                    existing.Icon = profile.Icon;
                    SaveProfiles(list);
                }
            }
            catch { }
        }

        public static string GetUserDataFolder(BrowserProfile profile)
        {
            string folder = Path.Combine(ProfilesDirectory, profile.Id, "browser_profile");
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            return folder;
        }

        public static string SharedCacheFolder
        {
            get
            {
                string folder = Path.Combine(ProfilesDirectory, "shared_web_cache");
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                return folder;
            }
        }

        public static string GetCacheFolder(BrowserProfile profile)
        {
            return SharedCacheFolder;
        }

        public static bool AlwaysAskAccountOnStartup
        {
            get
            {
                try
                {
                    string path = Path.Combine(ProfilesDirectory, "always_ask.txt");
                    if (File.Exists(path)) return bool.Parse(File.ReadAllText(path).Trim());
                }
                catch { }
                return true; // Default to true as requested by user!
            }
            set
            {
                try
                {
                    if (!Directory.Exists(ProfilesDirectory)) Directory.CreateDirectory(ProfilesDirectory);
                    string path = Path.Combine(ProfilesDirectory, "always_ask.txt");
                    File.WriteAllText(path, value.ToString());
                }
                catch { }
            }
        }
    }
}
