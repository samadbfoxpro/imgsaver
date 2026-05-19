using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace imgsaver
{
    public class ExtraPrompt : INotifyPropertyChanged
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "";
        public string PromptText { get; set; } = "";
        public DateTime LastModified { get; set; } = DateTime.Now;

        private bool _isFavorite;
        public bool IsFavorite
        {
            get => _isFavorite;
            set
            {
                if (_isFavorite != value)
                {
                    _isFavorite = value;
                    OnPropertyChanged(nameof(IsFavorite));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public static class ExtraPromptManager
    {
        private static readonly string FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "extra_prompts.json");
        private static List<ExtraPrompt> _prompts = new List<ExtraPrompt>();

        public static void Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    string json = File.ReadAllText(FilePath);
                    var loaded = JsonConvert.DeserializeObject<List<ExtraPrompt>>(json);
                    if (loaded != null && loaded.Count > 0)
                    {
                        var seenIds = new HashSet<string>();
                        bool wasDirty = false;
                        foreach (var prompt in loaded)
                        {
                            if (string.IsNullOrEmpty(prompt.Id) || seenIds.Contains(prompt.Id))
                            {
                                prompt.Id = Guid.NewGuid().ToString();
                                wasDirty = true;
                            }
                            seenIds.Add(prompt.Id);
                        }

                        _prompts = loaded;
                        if (wasDirty) Save();
                    }
                }

                if (_prompts.Count == 0) InitializeDefaults();
            }
            catch
            {
                InitializeDefaults();
            }
        }

        private static void InitializeDefaults()
        {
            _prompts = new List<ExtraPrompt>
            {
                new ExtraPrompt { Name = "Ground Detail", PromptText = "tree color [extra] on ground.", LastModified = DateTime.Now }
            };
            Save();
        }

        public static void Save()
        {
            try
            {
                string? dataDir = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(dataDir) && !Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);
                string json = JsonConvert.SerializeObject(_prompts, Formatting.Indented);
                File.WriteAllText(FilePath, json);
            }
            catch { }
        }

        public static void Unload()
        {
            _prompts.Clear();
        }

        public static List<ExtraPrompt> GetAll() => _prompts;

        public static List<ExtraPrompt> GetSortedAll()
        {
            return _prompts.OrderByDescending(p => p.IsFavorite)
                           .ThenByDescending(p => p.LastModified)
                           .ToList();
        }

        public static void Add(ExtraPrompt prompt)
        {
            try { Load(); } catch { }
            if (prompt == null) return;
            if (string.IsNullOrEmpty(prompt.Id)) prompt.Id = Guid.NewGuid().ToString();
            prompt.LastModified = DateTime.Now;
            _prompts.Add(prompt);
            Save();
        }

        public static void Update(ExtraPrompt updatedPrompt)
        {
            var index = _prompts.FindIndex(p => p.Id == updatedPrompt.Id);
            if (index != -1)
            {
                updatedPrompt.LastModified = DateTime.Now;
                _prompts[index] = updatedPrompt;
                Save();
            }
        }

        public static void Delete(string id)
        {
            _prompts.RemoveAll(p => p.Id == id);
            Save();
        }

        public static void ToggleFavorite(string id)
        {
            var prompt = _prompts.Find(p => p.Id == id);
            if (prompt != null)
            {
                prompt.IsFavorite = !prompt.IsFavorite;
                Save();
            }
        }
    }
}
