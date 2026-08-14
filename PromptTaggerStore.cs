using System;
using System.IO;
using System.Text.Json;

namespace imgsaver
{
    public static class PromptTaggerStore
    {
        public static string Template { get; set; } = "";
        public static string Values { get; set; } = "";
        public static string Prefix { get; set; } = "PH_";

        // Persistent manual tagger values and title for floating extra panels
        public static string ManualValues { get; set; } = "";
        public static string ManualTitle { get; set; } = "";

        // Persistent fields for the main Prompt Tagger window tabs
        public static string DiffPromptA { get; set; } = "";
        public static string DiffPromptB { get; set; } = "";
        public static string DiffPrefix { get; set; } = "PH_";
        public static string ReplacerOutput { get; set; } = "";
        public static string DiffTemplateOutput { get; set; } = "";
        public static string DiffValuesOutput { get; set; } = "";
        public static bool UseManualValuesMode { get; set; } = false;

        // Persistent fields for Tab 3: Interactive Selection Tagger
        public static string InteractivePrompt { get; set; } = "";
        public static string InteractiveInitial { get; set; } = "";
        public static string InteractiveValues { get; set; } = "";
        public static string InteractivePrefix { get; set; } = "PH_";

        private static string FilePath => DataPathManager.GetSettingsFilePath("tagger_manual_config.json");

        static PromptTaggerStore()
        {
            Load();
        }

        public static void Load()
        {
            try
            {
                string path = FilePath;
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var data = JsonSerializer.Deserialize<TaggerConfigData>(json);
                    if (data != null)
                    {
                        Template = data.Template ?? "";
                        Values = data.Values ?? "";
                        Prefix = data.Prefix ?? "PH_";
                        ManualValues = data.ManualValues ?? "";
                        ManualTitle = data.ManualTitle ?? "";
                        DiffPromptA = data.DiffPromptA ?? "";
                        DiffPromptB = data.DiffPromptB ?? "";
                        DiffPrefix = data.DiffPrefix ?? "PH_";
                        ReplacerOutput = data.ReplacerOutput ?? "";
                        DiffTemplateOutput = data.DiffTemplateOutput ?? "";
                        DiffValuesOutput = data.DiffValuesOutput ?? "";
                        UseManualValuesMode = data.UseManualValuesMode;
                        InteractivePrompt = data.InteractivePrompt ?? "";
                        InteractiveInitial = data.InteractiveInitial ?? "";
                        InteractiveValues = data.InteractiveValues ?? "";
                        InteractivePrefix = data.InteractivePrefix ?? "PH_";
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error loading tagger manual config: " + ex.Message);
            }
        }

        public static void Save()
        {
            try
            {
                string path = FilePath;
                string dir = Path.GetDirectoryName(path) ?? "";
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var data = new TaggerConfigData
                {
                    Template = Template,
                    Values = Values,
                    Prefix = Prefix,
                    ManualValues = ManualValues,
                    ManualTitle = ManualTitle,
                    DiffPromptA = DiffPromptA,
                    DiffPromptB = DiffPromptB,
                    DiffPrefix = DiffPrefix,
                    ReplacerOutput = ReplacerOutput,
                    DiffTemplateOutput = DiffTemplateOutput,
                    DiffValuesOutput = DiffValuesOutput,
                    UseManualValuesMode = UseManualValuesMode,
                    InteractivePrompt = InteractivePrompt,
                    InteractiveInitial = InteractiveInitial,
                    InteractiveValues = InteractiveValues,
                    InteractivePrefix = InteractivePrefix
                };

                string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error saving tagger manual config: " + ex.Message);
            }
        }

        private class TaggerConfigData
        {
            public string? Template { get; set; }
            public string? Values { get; set; }
            public string? Prefix { get; set; }
            public string? ManualValues { get; set; }
            public string? ManualTitle { get; set; }
            public string? DiffPromptA { get; set; }
            public string? DiffPromptB { get; set; }
            public string? DiffPrefix { get; set; }
            public string? ReplacerOutput { get; set; }
            public string? DiffTemplateOutput { get; set; }
            public string? DiffValuesOutput { get; set; }
            public bool UseManualValuesMode { get; set; }
            public string? InteractivePrompt { get; set; }
            public string? InteractiveInitial { get; set; }
            public string? InteractiveValues { get; set; }
            public string? InteractivePrefix { get; set; }
        }
    }
}
