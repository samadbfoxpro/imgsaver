using System;
using System.IO;
using System.Text.Json;
using System.Windows;

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

        public static string SharedPromptDataDirectory
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
            return GetSettingsFilePath(fileName);
        }

        public static string GetPromptDataFilePath(string fileName)
        {
            return GetLocalDataFilePath(fileName);
        }

        public static string GetSettingsFilePath(string fileName)
        {
            return GetLocalDataFilePath(fileName);
        }

        public static string GetDataSubfolderPath(string folderName)
        {
            string path = Path.Combine(SharedPromptDataDirectory, folderName);
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            return path;
        }

        public static string GetLocalDataFilePath(string fileName)
        {
            string dir = SharedPromptDataDirectory;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            string targetPath = Path.Combine(dir, fileName);

            // If custom data folder is enabled and file does not exist in target yet,
            // but exists in local data folder, copy it over automatically so data isn't lost.
            if (UseCustomDataFolder && !File.Exists(targetPath))
            {
                string localFallback = Path.Combine(LocalDataDirectory, fileName);
                if (File.Exists(localFallback))
                {
                    try
                    {
                        File.Copy(localFallback, targetPath, false);
                    }
                    catch { }
                }
            }

            return targetPath;
        }

        public static string GetLegacyRootFilePath(string fileName) => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);

        public static void Reload()
        {
            _settings = LoadBootstrapSettings();
            EnsureSharedPromptDataDirectory();
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

                // Auto-migrate any existing JSON data files to the new custom location without overwriting
                MigrateLocalJsonFiles(LocalDataDirectory, customDataFolder);
            }

            _settings = new DataLocationSettings
            {
                UseCustomDataFolder = useCustomDataFolder,
                CustomDataFolder = customDataFolder
            };

            string json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(BootstrapFilePath, json);
            EnsureSharedPromptDataDirectory();
        }

        public static void ReloadAllDataStores()
        {
            Reload();

            // 1. Reload prompt and persona stores
            try { BasePromptManager.Unload(); } catch { }
            try { CharacterManager.Unload(); } catch { }
            try { ExtraManager.Unload(); } catch { }
            try { ExtraPromptManager.Unload(); } catch { }

            try { BasePromptManager.Load(); } catch { }
            try { CharacterManager.Load(); } catch { }
            try { ExtraManager.Load(); } catch { }
            try { ExtraPromptManager.Load(); } catch { }

            // 2. Reload prompt combiner store
            try { PromptCombinerStore.Reload(); } catch { }

            // 3. Reload snippets
            try { SnippetManager.Load(); } catch { }

            // 4. Reload tagger config
            try { PromptTaggerStore.Load(); } catch { }

            // 5. Reload recording slots & settings
            try { RecordingManager.LoadState(); } catch { }

            // 6. Reload browser settings
            try { BrowserSettings.Load(); } catch { }

            // 7. Refresh open windows
            try
            {
                if (System.Windows.Application.Current != null)
                {
                    foreach (System.Windows.Window w in System.Windows.Application.Current.Windows)
                    {
                        if (w is MiniClipboardWindow mini)
                        {
                            try { mini.RefreshAutoImport(); } catch { }
                        }
                        else if (w is BrowserWindow browser)
                        {
                            try { browser.Dispatcher.Invoke(() => browser.RefreshSettings()); } catch { }
                        }
                        else if (w is PersonaInjectorWindow persona)
                        {
                            try { persona.Dispatcher.Invoke(() => persona.RefreshAllData()); } catch { }
                        }
                    }
                }
            }
            catch { }
        }

        private static void MigrateLocalJsonFiles(string sourceDir, string targetDir)
        {
            try
            {
                if (!Directory.Exists(sourceDir) || !Directory.Exists(targetDir)) return;
                if (string.Equals(Path.GetFullPath(sourceDir), Path.GetFullPath(targetDir), StringComparison.OrdinalIgnoreCase)) return;

                // Copy root JSON files
                foreach (var file in Directory.GetFiles(sourceDir, "*.json"))
                {
                    string destFile = Path.Combine(targetDir, Path.GetFileName(file));
                    if (!File.Exists(destFile))
                    {
                        try { File.Copy(file, destFile, false); } catch { }
                    }
                }

                // Copy recordings subfolder JSON files
                string sourceRecDir = Path.Combine(sourceDir, "recordings");
                if (Directory.Exists(sourceRecDir))
                {
                    string targetRecDir = Path.Combine(targetDir, "recordings");
                    if (!Directory.Exists(targetRecDir)) Directory.CreateDirectory(targetRecDir);
                    foreach (var recFile in Directory.GetFiles(sourceRecDir, "*.json"))
                    {
                        string destRecFile = Path.Combine(targetRecDir, Path.GetFileName(recFile));
                        if (!File.Exists(destRecFile))
                        {
                            try { File.Copy(recFile, destRecFile, false); } catch { }
                        }
                    }
                }
            }
            catch { }
        }

        public static void EnsureSharedPromptDataDirectory()
        {
            string dir = SharedPromptDataDirectory;
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
