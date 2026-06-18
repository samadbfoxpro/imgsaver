using System;
using System.IO;
using Newtonsoft.Json;

namespace imgsaver
{
    public class LastExtraSelection
    {
        public string ExtraId { get; set; } = "";
        public string ShortName { get; set; } = "";
        public string Text { get; set; } = "";
        public bool TextOnly { get; set; }
        public DateTime SavedAt { get; set; } = DateTime.Now;
    }

    public static class LastExtraSelectionStore
    {
        private static readonly string FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "last_extra_selection.json");

        public static void Save(ExtraItem extra, bool textOnly)
        {
            if (extra == null || string.IsNullOrWhiteSpace(extra.Text)) return;

            Save(extra.Id ?? "", extra.ShortName ?? "", extra.Text, textOnly);
        }

        public static void Save(string extraId, string shortName, string text, bool textOnly)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            try
            {
                string? dataDir = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(dataDir) && !Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);

                var selection = new LastExtraSelection
                {
                    ExtraId = extraId ?? "",
                    ShortName = shortName ?? "",
                    Text = text,
                    TextOnly = textOnly,
                    SavedAt = DateTime.Now
                };

                File.WriteAllText(FilePath, JsonConvert.SerializeObject(selection, Formatting.Indented));
            }
            catch { }
        }

        public static bool TryGetText(out string extraText, out string errorMessage)
        {
            extraText = "";
            errorMessage = "";

            try
            {
                if (!File.Exists(FilePath))
                {
                    errorMessage = "Select an Extra in Persona Injector once, then try again.";
                    return false;
                }

                var json = File.ReadAllText(FilePath);
                var selection = JsonConvert.DeserializeObject<LastExtraSelection>(json);
                if (selection == null || string.IsNullOrWhiteSpace(selection.Text))
                {
                    errorMessage = "The saved Extra selection is empty.";
                    return false;
                }

                extraText = ApplyTextOnly(selection.Text, selection.TextOnly);
                if (string.IsNullOrWhiteSpace(extraText))
                {
                    errorMessage = "The saved Extra text is empty.";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        public static string ApplyTextOnly(string text, bool textOnly)
        {
            if (!textOnly) return text;

            int commaIndex = text.IndexOf(',');
            return commaIndex > 0 ? text.Substring(0, commaIndex).Trim() : text;
        }
    }
}
