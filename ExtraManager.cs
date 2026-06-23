using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace imgsaver
{
    public class ExtraItem : INotifyPropertyChanged
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ShortName { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;

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

    public static class ExtraManager
    {
        private static string FilePath => DataPathManager.GetDataFilePath("extras.json");
        private static List<ExtraItem> _extras = new List<ExtraItem>();

        public static List<ExtraItem> GetAll() => _extras;

        public static List<ExtraItem> GetSortedAll()
        {
            return _extras.OrderByDescending(e => e.IsFavorite)
                          .ThenBy(e => e.ShortName)
                          .ToList();
        }

        public static void Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    string json = File.ReadAllText(FilePath);
                    var loaded = JsonConvert.DeserializeObject<List<ExtraItem>>(json);
                    if (loaded != null && loaded.Count > 0)
                    {
                        var seenIds = new HashSet<string>();
                        bool wasDirty = false;
                        foreach (var extra in loaded)
                        {
                            if (string.IsNullOrEmpty(extra.Id) || seenIds.Contains(extra.Id))
                            {
                                extra.Id = Guid.NewGuid().ToString();
                                wasDirty = true;
                            }
                            seenIds.Add(extra.Id);
                        }

                        _extras = loaded;
                        if (wasDirty) Save();
                        return;
                    }
                }
            }
            catch { }

            _extras = GetDefaultExtras();
            Save();
        }

        public static void Save()
        {
            try
            {
                string? dataDir = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(dataDir) && !Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);
                string json = JsonConvert.SerializeObject(_extras, Formatting.Indented);
                File.WriteAllText(FilePath, json);
            }
            catch { }
        }

        public static void Unload()
        {
            _extras.Clear();
        }

        public static void Add(ExtraItem extra)
        {
            if (extra == null) return;
            if (string.IsNullOrEmpty(extra.Id)) extra.Id = Guid.NewGuid().ToString();
            _extras.Add(extra);
            Save();
        }

        public static void Update(ExtraItem extra)
        {
            if (extra == null) return;
            var index = _extras.FindIndex(e => e.Id == extra.Id);
            if (index >= 0)
            {
                _extras[index] = extra;
                Save();
            }
        }

        public static void Delete(string id)
        {
            _extras.RemoveAll(e => e.Id == id);
            Save();
        }

        public static void ToggleFavorite(string id)
        {
            var extra = _extras.Find(e => e.Id == id);
            if (extra != null)
            {
                extra.IsFavorite = !extra.IsFavorite;
                Save();
            }
        }

        private static List<ExtraItem> GetDefaultExtras()
        {
            return new List<ExtraItem>
            {
                new ExtraItem { ShortName = "Red Leaves", Text = "red leaves" },
                new ExtraItem { ShortName = "Blue Light", Text = "soft blue light" },
                new ExtraItem { ShortName = "Wet Ground", Text = "wet ground reflections" }
            };
        }
    }
}
