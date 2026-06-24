using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ComponentModel;
using Newtonsoft.Json;

namespace imgsaver
{
    public class BasePrompt : INotifyPropertyChanged
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

    public static class BasePromptManager
    {
        private static string FilePath => DataPathManager.GetPromptDataFilePath("base_prompts.json");
        private static List<BasePrompt> _prompts = new List<BasePrompt>();

        /// <summary>
        /// Loads prompts from JSON file or initializes with defaults.
        /// </summary>
        public static void Load()
        {
            try
            {
                // Migration logic
                string oldPath = DataPathManager.GetLegacyRootFilePath("base_prompts.json");
                if (File.Exists(oldPath) && !File.Exists(FilePath))
                {
                    try
                    {
                        string dataDir = Path.GetDirectoryName(FilePath);
                        if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);
                        File.Move(oldPath, FilePath);
                    }
                    catch { }
                }

                if (File.Exists(FilePath))
                {
                    string json = File.ReadAllText(FilePath);
                    var loaded = JsonConvert.DeserializeObject<List<BasePrompt>>(json);

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

                if (_prompts.Count == 0)
                {
                    InitializeDefaults();
                }
            }
            catch
            {
                InitializeDefaults();
            }
        }

        private static void InitializeDefaults()
        {
            _prompts = new List<BasePrompt>
            {
                new BasePrompt { Name = "Standard Helper", PromptText = "Hello [character], please help me with the following task:", LastModified = DateTime.Now.AddSeconds(-2) },
                new BasePrompt { Name = "Image Description", PromptText = "A high-quality image of [character], detailed, masterpiece, 8k.", LastModified = DateTime.Now.AddSeconds(-1) },
                new BasePrompt { Name = "Creative Writing", PromptText = "Write a story about [character] exploring a magical forest.", LastModified = DateTime.Now }
            };
            Save();
        }

        /// <summary>
        /// Saves current prompts to JSON file.
        /// </summary>
        public static void Save()
        {
            try
            {
                string json = JsonConvert.SerializeObject(_prompts, Formatting.Indented);
                File.WriteAllText(FilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving base prompts: {ex.Message}");
            }
        }

        /// <summary>
        /// Clears the memory cache.
        /// </summary>
        public static void Unload()
        {
            _prompts.Clear();
        }

        public static List<BasePrompt> GetAll() => _prompts;
        public static List<BasePrompt> GetSortedAll() => _prompts.OrderByDescending(p => p.IsFavorite).ThenByDescending(p => p.LastModified).ToList();

        public static void Add(BasePrompt prompt)
        {
            try
            {
                Load();
            }
            catch { }

            if (prompt == null) return;
            if (string.IsNullOrEmpty(prompt.Id)) prompt.Id = Guid.NewGuid().ToString();
            prompt.LastModified = DateTime.Now;
            _prompts.Add(prompt);
            Save();
        }

        public static void Update(BasePrompt updatedPrompt)
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
                // We don't necessarily update LastModified for just favoriting, 
                // but we could if we wanted favorited items to jump to top of their group.
                Save();
            }
        }

        public static List<BasePrompt> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return GetAll();

            var searchTerms = query.ToLower().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            return _prompts
                .Where(p =>
                {
                    string target = (p.Name + " " + p.PromptText).ToLower();
                    var targetWords = target.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                    return searchTerms.All(term =>
                    {
                        if (targetWords.Any(w => w.StartsWith(term))) return true;
                        if (target.Contains(term)) return true;
                        return false;
                    });
                })
                .OrderByDescending(p => p.IsFavorite)
                .ThenByDescending(p => p.LastModified)
                .ToList();
        }
    }
}
