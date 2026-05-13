using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Globalization;

namespace imgsaver
{
    public static class RecordingManager
    {
        private static List<InputEvent> _slot1 = new List<InputEvent>();
        private static List<InputEvent> _slot2 = new List<InputEvent>();
        private static readonly string DataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
        private static readonly string Slot1Path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "rec_slot1.json");
        private static readonly string Slot2Path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "rec_slot2.json");
        private static readonly string SettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "rec_settings.json");

        public static double PlaybackSpeed { get; set; } = 1.0;
        public static bool SequentialMode { get; set; } = false;
        public static int SelectedSlot { get; set; } = 1;

        static RecordingManager()
        {
            if (!Directory.Exists(DataDir)) Directory.CreateDirectory(DataDir);
            LoadState();
        }

        public static IReadOnlyList<InputEvent> GetEvents(int slot) => (slot == 1 ? _slot1 : _slot2).AsReadOnly();

        public static bool HasEvents(int slot) => (slot == 1 ? _slot1 : _slot2).Count > 0;

        public static void SetEvents(int slot, IEnumerable<InputEvent> events)
        {
            if (slot == 1) _slot1 = new List<InputEvent>(events ?? Array.Empty<InputEvent>());
            else _slot2 = new List<InputEvent>(events ?? Array.Empty<InputEvent>());
            SaveState();
        }

        public static void SaveState()
        {
            try
            {
                var opts = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(Slot1Path, JsonSerializer.Serialize(_slot1, opts));
                File.WriteAllText(Slot2Path, JsonSerializer.Serialize(_slot2, opts));

                var settings = new Dictionary<string, string>
                {
                    { "PlaybackSpeed", PlaybackSpeed.ToString("F1", CultureInfo.InvariantCulture) },
                    { "SequentialMode", SequentialMode.ToString() },
                    { "SelectedSlot", SelectedSlot.ToString() }
                };
                File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings));
            }
            catch { }
        }

        public static void LoadState()
        {
            try
            {
                if (File.Exists(Slot1Path)) _slot1 = JsonSerializer.Deserialize<List<InputEvent>>(File.ReadAllText(Slot1Path)) ?? new List<InputEvent>();
                if (File.Exists(Slot2Path)) _slot2 = JsonSerializer.Deserialize<List<InputEvent>>(File.ReadAllText(Slot2Path)) ?? new List<InputEvent>();

                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    var settings = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (settings != null)
                    {
                        if (settings.TryGetValue("PlaybackSpeed", out var speedStr) && double.TryParse(speedStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double speed)) PlaybackSpeed = speed;
                        if (settings.TryGetValue("SequentialMode", out var seqStr) && bool.TryParse(seqStr, out bool seq)) SequentialMode = seq;
                        if (settings.TryGetValue("SelectedSlot", out var slotStr) && int.TryParse(slotStr, out int slot)) SelectedSlot = slot;
                    }
                }
            }
            catch { }
        }

        public static bool LoadFromFile(int slot, string path, out string? error)
        {
            error = null;
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) { error = "File not found"; return false; }
                var loaded = JsonSerializer.Deserialize<List<InputEvent>>(File.ReadAllText(path));
                if (loaded == null) { error = "Invalid file format"; return false; }
                if (slot == 1) _slot1 = loaded; else _slot2 = loaded;
                SaveState();
                return true;
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }
    }
}