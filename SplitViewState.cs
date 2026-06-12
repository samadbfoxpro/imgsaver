using System;
using System.Collections.Generic;

namespace imgsaver
{
    /// <summary>
    /// Represents the split view configuration and state
    /// </summary>
    public class SplitViewState
    {
        /// <summary>
        /// Unique identifier for this split group
        /// </summary>
        public string GroupId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// The orientation of the split (Horizontal or Vertical)
        /// </summary>
        public SplitOrientation Orientation { get; set; } = SplitOrientation.Vertical;

        /// <summary>
        /// The ratio of the left/top panel size (0.0 to 1.0)
        /// </summary>
        public double SplitRatio { get; set; } = 0.5;

        /// <summary>
        /// The left/top panel's split state (if nested)
        /// </summary>
        public SplitViewState? LeftTopPanel { get; set; }

        /// <summary>
        /// The right/bottom panel's split state (if nested)
        /// </summary>
        public SplitViewState? RightBottomPanel { get; set; }

        /// <summary>
        /// URL for this panel (only if it's a leaf panel)
        /// </summary>
        public string? TabUrl { get; set; }

        /// <summary>
        /// Whether this panel contains multiple tabs or just one
        /// </summary>
        public List<string> TabUrls { get; set; } = new List<string>();

        /// <summary>
        /// Index of currently selected tab in this panel
        /// </summary>
        public int SelectedTabIndex { get; set; } = 0;

        /// <summary>
        /// Check if this is a leaf panel (no nested splits)
        /// </summary>
        public bool IsLeafPanel => LeftTopPanel == null && RightBottomPanel == null;

        /// <summary>
        /// Get all leaf panels recursively
        /// </summary>
        public List<SplitViewState> GetLeafPanels()
        {
            var leaves = new List<SplitViewState>();
            
            if (IsLeafPanel)
            {
                leaves.Add(this);
            }
            else
            {
                if (LeftTopPanel != null)
                    leaves.AddRange(LeftTopPanel.GetLeafPanels());
                if (RightBottomPanel != null)
                    leaves.AddRange(RightBottomPanel.GetLeafPanels());
            }
            
            return leaves;
        }

        /// <summary>
        /// Find a leaf panel by ID
        /// </summary>
        public SplitViewState? FindLeafPanel(string panelId)
        {
            if (GroupId == panelId && IsLeafPanel)
                return this;

            if (LeftTopPanel != null)
            {
                var found = LeftTopPanel.FindLeafPanel(panelId);
                if (found != null) return found;
            }

            if (RightBottomPanel != null)
            {
                var found = RightBottomPanel.FindLeafPanel(panelId);
                if (found != null) return found;
            }

            return null;
        }

        /// <summary>
        /// Remove a leaf panel and collapse if needed
        /// </summary>
        public bool RemoveLeafPanel(string panelId)
        {
            if (IsLeafPanel)
                return false;

            if (LeftTopPanel != null && LeftTopPanel.GroupId == panelId && LeftTopPanel.IsLeafPanel)
            {
                // Collapse: move right/bottom panel up
                if (RightBottomPanel != null)
                {
                    Orientation = RightBottomPanel.Orientation;
                    SplitRatio = RightBottomPanel.SplitRatio;
                    LeftTopPanel = RightBottomPanel.LeftTopPanel;
                    RightBottomPanel = RightBottomPanel.RightBottomPanel;
                    TabUrl = RightBottomPanel?.TabUrl;
                    TabUrls = RightBottomPanel?.TabUrls ?? new List<string>();
                    SelectedTabIndex = RightBottomPanel?.SelectedTabIndex ?? 0;
                    return true;
                }
                else
                {
                    // Only left panel, just remove it
                    LeftTopPanel = null;
                    return true;
                }
            }

            if (RightBottomPanel != null && RightBottomPanel.GroupId == panelId && RightBottomPanel.IsLeafPanel)
            {
                // Collapse: move left/top panel down
                if (LeftTopPanel != null)
                {
                    Orientation = LeftTopPanel.Orientation;
                    SplitRatio = LeftTopPanel.SplitRatio;
                    RightBottomPanel = LeftTopPanel.RightBottomPanel;
                    LeftTopPanel = LeftTopPanel.LeftTopPanel;
                    TabUrl = LeftTopPanel?.TabUrl;
                    TabUrls = LeftTopPanel?.TabUrls ?? new List<string>();
                    SelectedTabIndex = LeftTopPanel?.SelectedTabIndex ?? 0;
                    return true;
                }
                else
                {
                    // Only right panel, just remove it
                    RightBottomPanel = null;
                    return true;
                }
            }

            // Recurse
            if (LeftTopPanel != null && LeftTopPanel.RemoveLeafPanel(panelId))
                return true;
            if (RightBottomPanel != null && RightBottomPanel.RemoveLeafPanel(panelId))
                return true;

            return false;
        }
    }

    /// <summary>
    /// Orientation of split panels
    /// </summary>
    public enum SplitOrientation
    {
        Vertical,   // Left and Right panels
        Horizontal  // Top and Bottom panels
    }
}
