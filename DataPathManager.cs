using System;
using System.IO;
using System.Text.Json;

namespace imgsaver
{
    public class DataLocationSettings
    {
        public bool UseCustomDataFolder { get; set; }
        public string CustomDataFolder { get; set; } = "";
    }

    public static class DataPathManager
    {
        private static readonly string BootstrapFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data_location.json");
        private static DataLocationSettings _settings = LoadBootstrapSettings();

        public static string LocalDataDirectory => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");

        public static bool UseCustomDataFolder => _settings.UseCustomDataFolder;

        public static string CustomDataFolder => _settings.CustomDataFolder ?? "";

        public static string ActiveDataDirectory
        {
            get
            {
                if (_settings.UseCustomDataFolder && IsUsableDirectory(_settings.CustomDataFolder))
                    return _settings.CustomDataFolder;

                return LocalDataDirectory;
            }
        }

        public static string GetDataFilePath(string fileName)
        {
            EnsureActiveDataDirectory();
            return Path.Combine(ActiveDataDirectory, fileName);
        }

        public static string GetDataSubfolderPath(string folderName)
        {
            string path = Path.Combine(ActiveDataDirectory, folderName);
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            return path;
        }

        public static string GetLocalDataFilePath(string fileName)
        {
            if (!Directory.Exists(LocalDataDirectory)) Directory.CreateDirectory(LocalDataDirectory);
            return Path.Combine(LocalDataDirectory, fileName);
        }

        public static string GetLegacyRootFilePath(string fileName) => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);

        public static void Reload()
        {
            _settings = LoadBootstrapSettings();
            EnsureActiveDataDirectory();
        }

        public static void SaveLocation(bool useCustomDataFolder, string customDataFolder)
        {
            customDataFolder = (customDataFolder ?? "").Trim();
            if (useCustomDataFolder)
            {
                if (string.IsNullOrWhiteSpace(customDataFolder))
                    throw new InvalidOperationException("Please select a custom data folder first.");

                Directory.CreateDirectory(customDataFolder);
                customDataFolder = Path.GetFullPath(customDataFolder);
            }

            _settings = new DataLocationSettings
            {
                UseCustomDataFolder = useCustomDataFolder,
                CustomDataFolder = customDataFolder
            };

            string json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(BootstrapFilePath, json);
            EnsureActiveDataDirectory();
        }

        public static void EnsureActiveDataDirectory()
        {
            string dir = ActiveDataDirectory;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
        }

        private static DataLocationSettings LoadBootstrapSettings()
        {
            try
            {
                if (File.Exists(BootstrapFilePath))
                {
                    var settings = JsonSerializer.Deserialize<DataLocationSettings>(File.ReadAllText(BootstrapFilePath));
                    if (settings != null) return settings;
                }
            }
            catch { }

            return new DataLocationSettings();
        }

        private static bool IsUsableDirectory(string? path)
        {
            try
            {
                return !string.IsNullOrWhiteSpace(path) && Directory.Exists(path);
            }
            catch
            {
                return false;
            }
        }
    }
}
