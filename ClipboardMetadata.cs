using System;

namespace imgsaver
{
    /// <summary>
    /// Shared metadata and events for communication between windows
    /// </summary>
    public static class ClipboardMetadata
    {
        public static string CharacterName { get; set; }
        public static string BasePromptName { get; set; }
        public static bool PreserveMiniClipTitle { get; set; }
        public static DateTime LastUpdated { get; set; }

        public static void Set(string characterName, string basePromptName, bool preserveMiniClipTitle = false)
        {
            CharacterName = characterName;
            BasePromptName = basePromptName;
            PreserveMiniClipTitle = preserveMiniClipTitle;
            LastUpdated = DateTime.Now;
        }

        public static void Clear()
        {
            CharacterName = null;
            BasePromptName = null;
            PreserveMiniClipTitle = false;
            LastUpdated = DateTime.MinValue;
        }

        public static bool IsValid()
        {
            return (!string.IsNullOrEmpty(CharacterName) || !string.IsNullOrEmpty(BasePromptName)) &&
                   (DateTime.Now - LastUpdated).TotalSeconds < 5;
        }

    }
}
