using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.ComponentModel;
using Newtonsoft.Json;

namespace imgsaver
{
    /// <summary>
    /// Represents a character persona with a short name for UI and full persona text for injection.
    /// </summary>
    public class CharacterPersona : INotifyPropertyChanged
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ShortName { get; set; } = string.Empty;
        public string FullPersona { get; set; } = string.Empty;
        
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

    /// <summary>
    /// Manages character personas with CRUD operations and JSON persistence.
    /// </summary>
    public static class CharacterManager
    {
        private static readonly string PersonasFilePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "data", "personas.json");

        private static List<CharacterPersona> _characters = new List<CharacterPersona>();

        /// <summary>
        /// Gets all characters.
        /// </summary>
        public static List<CharacterPersona> GetAll()
        {
            return _characters;
        }

        public static List<CharacterPersona> GetSortedAll()
        {
            return _characters.OrderByDescending(c => c.IsFavorite)
                             .ThenBy(c => c.ShortName)
                             .ToList();
        }

        /// <summary>
        /// Loads characters from JSON file or creates default set.
        /// </summary>
        public static void Load()
        {
            try
            {
                // Migration logic
                string oldPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "personas.json");
                if (File.Exists(oldPath) && !File.Exists(PersonasFilePath))
                {
                    try
                    {
                        string dataDir = Path.GetDirectoryName(PersonasFilePath);
                        if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);
                        File.Move(oldPath, PersonasFilePath);
                    }
                    catch { }
                }

                if (File.Exists(PersonasFilePath))
                {
                    string json = File.ReadAllText(PersonasFilePath);
                    var loaded = JsonConvert.DeserializeObject<List<CharacterPersona>>(json);
                    if (loaded != null && loaded.Count > 0)
                    {
                        // Sanitize IDs: ensure all are unique and non-null
                        var seenIds = new HashSet<string>();
                        bool wasDirty = false;
                        foreach (var character in loaded)
                        {
                            if (string.IsNullOrEmpty(character.Id) || seenIds.Contains(character.Id))
                            {
                                character.Id = Guid.NewGuid().ToString();
                                wasDirty = true;
                            }
                            seenIds.Add(character.Id);
                        }

                        _characters = loaded;
                        
                        if (wasDirty) Save();
                        return;
                    }
                }
            }
            catch { }

            // Create default characters if none exist
            _characters = GetDefaultCharacters();
            Save();
        }

        /// <summary>
        /// Saves characters to JSON file.
        /// </summary>
        public static void Save()
        {
            try
            {
                string json = JsonConvert.SerializeObject(_characters, Formatting.Indented);
                File.WriteAllText(PersonasFilePath, json);
            }
            catch { }
        }

        /// <summary>
        /// Clears the memory cache.
        /// </summary>
        public static void Unload()
        {
            _characters.Clear();
        }

        /// <summary>
        /// Adds a new character.
        /// </summary>
        public static void Add(CharacterPersona character)
        {
            if (character == null) return;
            if (string.IsNullOrEmpty(character.Id))
                character.Id = Guid.NewGuid().ToString();
            _characters.Add(character);
            Save();
        }

        /// <summary>
        /// Updates an existing character.
        /// </summary>
        public static void Update(CharacterPersona character)
        {
            if (character == null) return;
            var index = _characters.FindIndex(c => c.Id == character.Id);
            if (index >= 0)
            {
                _characters[index] = character;
                Save();
            }
        }

        /// <summary>
        /// Deletes a character by ID.
        /// </summary>
        public static void Delete(string id)
        {
            _characters.RemoveAll(c => c.Id == id);
            Save();
        }

        /// <summary>
        /// Toggles the favorite status of a character.
        /// </summary>
        public static void ToggleFavorite(string id)
        {
            var character = _characters.Find(c => c.Id == id);
            if (character != null)
            {
                character.IsFavorite = !character.IsFavorite;
                Save();
            }
        }

        /// <summary>
        /// Searches characters by name (case-insensitive).
        /// </summary>
        public static List<CharacterPersona> Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return GetAll();

            query = query.ToLower();
            return _characters.Where(c => 
                c.ShortName.ToLower().Contains(query) || 
                c.FullPersona.ToLower().Contains(query))
                .OrderByDescending(c => c.IsFavorite)
                .ThenBy(c => c.ShortName)
                .ToList();
        }

        /// <summary>
        /// Returns default character set.
        /// </summary>
        private static List<CharacterPersona> GetDefaultCharacters()
        {
            return new List<CharacterPersona>
            {
                new CharacterPersona
                {
                    ShortName = "Professional Editor",
                    FullPersona = "You are an expert editor with 15 years of experience in polishing written content for clarity, grammar, and impact. You focus on improving readability while maintaining the author's voice."
                },
                new CharacterPersona
                {
                    ShortName = "Creative Writer",
                    FullPersona = "You are a creative fiction writer known for vivid imagery and compelling narratives. You excel at crafting engaging stories with rich characters and immersive worlds."
                },
                new CharacterPersona
                {
                    ShortName = "Code Reviewer",
                    FullPersona = "You are a senior software engineer who specializes in code reviews, focusing on best practices, clean code principles, and maintainability. You provide constructive feedback with clear explanations."
                },
                new CharacterPersona
                {
                    ShortName = "Marketing Expert",
                    FullPersona = "You are a marketing specialist with expertise in persuasive copywriting and brand messaging. You understand consumer psychology and create compelling content that drives engagement."
                },
                new CharacterPersona
                {
                    ShortName = "Technical Writer",
                    FullPersona = "You are a technical documentation specialist who excels at explaining complex concepts in clear, accessible language. You create well-structured guides and tutorials."
                },
                new CharacterPersona
                {
                    ShortName = "Research Analyst",
                    FullPersona = "You are a research analyst with expertise in gathering, analyzing, and synthesizing information from multiple sources. You provide thorough, well-cited analysis with balanced perspectives."
                }
            };
        }
    }
}
