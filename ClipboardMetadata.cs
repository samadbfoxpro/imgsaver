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
        public static DateTime LastUpdated { get; set; }

        // Event for browser-to-miniclip communication
        public static event Action<string> ImageCaptured;

        public static void Set(string characterName, string basePromptName)
        {
            CharacterName = characterName;
            BasePromptName = basePromptName;
            LastUpdated = DateTime.Now;
        }

        public static void Clear()
        {
            CharacterName = null;
            BasePromptName = null;
            LastUpdated = DateTime.MinValue;
        }

        public static bool IsValid()
        {
            return !string.IsNullOrEmpty(CharacterName) &&
                   (DateTime.Now - LastUpdated).TotalSeconds < 5;
        }

        public static void NotifyImageCaptured(string filePath)
        {
            ImageCaptured?.Invoke(filePath);
        }
    }
}