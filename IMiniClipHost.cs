using System;

namespace imgsaver
{
    public interface IMiniClipHost
    {
        // Negative Prompt
        string NegativePrompt { get; set; }
        bool IsNegativeLocked { get; set; }

        // Auto Save
        bool IsAutoSaveEnabled { get; set; }
        int AutoSaveThreshold { get; set; }
        bool IsAutoSaveDelayEnabled { get; set; }
        int AutoSaveDelaySeconds { get; set; }
        void SaveConfigSettings();

        // Extra Options
        bool IsAutoFillEnabled { get; set; }
        bool IsSaveBasePromptEnabled { get; set; }
        
        bool IsDescriptionEnabled { get; set; }
        string DescriptionText { get; set; }
        bool IsDescriptionLocked { get; set; }

        bool IsAdditionalTitleEnabled { get; set; }
        string AdditionalTitle { get; set; }
        bool IsAdditionalTitleLocked { get; set; }

        int ExtraMenuPage { get; set; }
        bool IsTitleLocked { get; set; }
    }
}
