using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace imgsaver
{
    public static class PromptCombinerStore
    {
        private static readonly object _lock = new object();
        private static PromptCombinerData _cache = null;

        private static string FilePath => DataPathManager.GetSettingsFilePath("prompt_combiner_data.json");

        public static PromptCombinerData Load()
        {
            lock (_lock)
            {
                if (_cache != null) return _cache;

                try
                {
                    string path = FilePath;
                    if (File.Exists(path))
                    {
                        string json = File.ReadAllText(path);
                        var data = JsonSerializer.Deserialize<PromptCombinerData>(json);
                        if (data != null)
                        {
                            EnsureValidData(data);
                            _cache = data;
                            return _cache;
                        }
                    }
                }
                catch { }

                _cache = CreateDefaultData();
                Save(_cache);
                return _cache;
            }
        }

        public static void Save(PromptCombinerData data)
        {
            if (data == null) return;
            lock (_lock)
            {
                try
                {
                    EnsureValidData(data);
                    _cache = data;
                    string path = FilePath;
                    string dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(path, json);
                }
                catch { }
            }
        }

        private static void EnsureValidData(PromptCombinerData data)
        {
            if (data.Folders == null) data.Folders = new List<PromptCombinerFolder>();
            if (data.Items == null) data.Items = new List<PromptCombinerItem>();
            if (data.ActiveItemIds == null) data.ActiveItemIds = new List<string>();
            if (string.IsNullOrWhiteSpace(data.Separator)) data.Separator = ", ";
            if (data.CommaIndex <= 0) data.CommaIndex = 1;

            if (data.Folders.Count == 0)
            {
                var defaultData = CreateDefaultData();
                data.Folders = defaultData.Folders;
                data.Items = defaultData.Items;
            }

            if (string.IsNullOrEmpty(data.ActiveFolderId) || !data.Folders.Any(f => f.Id == data.ActiveFolderId))
            {
                data.ActiveFolderId = data.Folders[0].Id;
            }
        }

        private static PromptCombinerData CreateDefaultData()
        {
            var folderQuality = new PromptCombinerFolder { Id = Guid.NewGuid().ToString(), Name = "Quality 💎", Order = 0 };
            var folderStyle   = new PromptCombinerFolder { Id = Guid.NewGuid().ToString(), Name = "Styles 🎨", Order = 1 };
            var folderLight   = new PromptCombinerFolder { Id = Guid.NewGuid().ToString(), Name = "Lighting 💡", Order = 2 };

            var items = new List<PromptCombinerItem>
            {
                new PromptCombinerItem { FolderId = folderQuality.Id, Title = "Masterpiece 🌟", Text = "masterpiece, best quality, ultra-detailed", Order = 0 },
                new PromptCombinerItem { FolderId = folderQuality.Id, Title = "8K HD 📷", Text = "8k resolution, photorealistic, sharp focus", Order = 1 },

                new PromptCombinerItem { FolderId = folderStyle.Id, Title = "Cinematic 🎬", Text = "cinematic atmosphere, dramatic lighting, 35mm photograph", Order = 0 },
                new PromptCombinerItem { FolderId = folderStyle.Id, Title = "Anime 🌸", Text = "vibrant anime style, detailed lineart, studio ghibli inspired", Order = 1 },

                new PromptCombinerItem { FolderId = folderLight.Id, Title = "Volumetric Rays ☀️", Text = "volumetric lighting, sunbeams, soft rim light", Order = 0 },
                new PromptCombinerItem { FolderId = folderLight.Id, Title = "Cyberpunk Glow 🌆", Text = "neon reflections, cyberpunk night glow, vibrant contrast", Order = 1 }
            };

            return new PromptCombinerData
            {
                IsEnabled = false,
                ActiveFolderId = folderQuality.Id,
                ActiveItemIds = new List<string>(),
                PlacementMode = CombinerPlacementMode.AfterComma,
                CommaIndex = 1,
                Separator = ", ",
                Folders = new List<PromptCombinerFolder> { folderQuality, folderStyle, folderLight },
                Items = items
            };
        }
    }
}
