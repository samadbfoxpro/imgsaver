using System;
using System.Collections.Generic;

namespace imgsaver
{
    public enum CombinerPlacementMode
    {
        AfterComma = 0,
        AtBeginning = 1,
        AtEnd = 2,
        PerFolder = 3
    }

    public class PromptCombinerItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string FolderId { get; set; } = "";
        public string Title { get; set; } = "";
        public string Text { get; set; } = "";
        public bool IsEnabled { get; set; } = false;
        public int Order { get; set; } = 0;
    }

    public class PromptCombinerFolder
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "";
        public int Order { get; set; } = 0;

        // Custom Placement Rules per Folder
        public CombinerPlacementMode PlacementMode { get; set; } = CombinerPlacementMode.AfterComma;
        public int CommaIndex { get; set; } = 1;
    }

    public class PromptCombinerData
    {
        public bool IsEnabled { get; set; } = false;
        public string ActiveFolderId { get; set; } = "";
        public List<string> ActiveItemIds { get; set; } = new List<string>();
        public CombinerPlacementMode PlacementMode { get; set; } = CombinerPlacementMode.AfterComma;
        public int CommaIndex { get; set; } = 1;
        public string Separator { get; set; } = ", ";
        public List<PromptCombinerFolder> Folders { get; set; } = new List<PromptCombinerFolder>();
        public List<PromptCombinerItem> Items { get; set; } = new List<PromptCombinerItem>();
    }
}
