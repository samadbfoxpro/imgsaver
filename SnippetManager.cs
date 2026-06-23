using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace imgsaver
{
    public class Snippet
    {
        public string Key { get; set; } = "";
        public string Value { get; set; } = "";
    }

    public static class SnippetManager
    {
        public static List<Snippet> Snippets { get; private set; } = new List<Snippet>();

        static SnippetManager()
        {
            Load();
        }

        public static void Load()
        {
            try
            {
                string dataDir = DataPathManager.ActiveDataDirectory;
                string path = DataPathManager.GetDataFilePath("snippets.json");
                string oldPath = DataPathManager.GetLegacyRootFilePath("snippets.json");

                if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);

                // Migration logic for older versions
                if (File.Exists(oldPath) && !File.Exists(path))
                {
                    try { File.Move(oldPath, path); } catch { }
                }

                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    Snippets = JsonSerializer.Deserialize<List<Snippet>>(json) ?? new List<Snippet>();
                }
            }
            catch
            {
                Snippets = new List<Snippet>();
            }
        }

        public static void Save()
        {
            try
            {
                string dataDir = DataPathManager.ActiveDataDirectory;
                if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);

                string path = DataPathManager.GetDataFilePath("snippets.json");
                string json = JsonSerializer.Serialize(Snippets, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch { }
        }

        public static string? GetExpansion(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            return Snippets.FirstOrDefault(s => s.Key.Trim().Equals(key.Trim(), StringComparison.OrdinalIgnoreCase))?.Value;
        }

        /// <summary>
        /// Find snippet with suffix matching.
        /// Improved with 'Longest Suffix Match' to ensure that if you have '/s' and '/save',
        /// typing '/save' won't accidentally trigger '/s' first.
        /// </summary>
        public static Snippet? FindMatch(string word)
        {
            if (string.IsNullOrEmpty(word)) return null;

            // Find all snippets that are a suffix of the current buffer
            var matches = Snippets
                .Where(s => !string.IsNullOrEmpty(s.Key) && word.EndsWith(s.Key, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matches.Count == 0) return null;

            // Return the LONGEST match. This is the key to high accuracy.
            // It prevents partial matches from triggering prematurely.
            return matches.OrderByDescending(m => m.Key.Length).FirstOrDefault();
        }

        /// <summary>
        /// Checks if the current word is a prefix of any existing snippet.
        /// This helps the keyboard hook decide if it should wait for more characters.
        /// </summary>
        public static bool IsPotentialMatch(string word)
        {
            if (string.IsNullOrEmpty(word)) return false;
            return Snippets.Any(s => s.Key.StartsWith(word, StringComparison.OrdinalIgnoreCase) && s.Key.Length > word.Length);
        }
    }
}
