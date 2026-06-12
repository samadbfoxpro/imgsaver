using System;
using System.Collections.Generic;
using System.Linq;

namespace imgsaver
{
    /// <summary>
    /// Manages split view panels and their relationships
    /// </summary>
    public class SplitViewManager
    {
        private SplitViewState _rootState;
        private Dictionary<string, object> _panelData = new();

        public SplitViewManager()
        {
            // Initialize with a single root panel
            _rootState = new SplitViewState();
        }

        /// <summary>
        /// Get the root state
        /// </summary>
        public SplitViewState GetRootState() => _rootState;

        /// <summary>
        /// Get all leaf panels
        /// </summary>
        public List<SplitViewState> GetAllLeafPanels() => _rootState.GetLeafPanels();

        /// <summary>
        /// Create a split from an existing panel
        /// </summary>
        public SplitViewState? CreateSplit(string sourcePanelId, SplitOrientation orientation, double ratio = 0.5)
        {
            if (ratio < 0.2 || ratio > 0.8)
                ratio = 0.5;

            var leafPanel = _rootState.FindLeafPanel(sourcePanelId);
            if (leafPanel == null || !leafPanel.IsLeafPanel)
                return null;

            // Convert leaf panel to split panel
            var newLeftTop = new SplitViewState
            {
                TabUrl = leafPanel.TabUrl,
                TabUrls = new List<string>(leafPanel.TabUrls),
                SelectedTabIndex = leafPanel.SelectedTabIndex
            };

            var newRightBottom = new SplitViewState
            {
                // New panel is empty, will be populated by caller
            };

            leafPanel.Orientation = orientation;
            leafPanel.SplitRatio = ratio;
            leafPanel.LeftTopPanel = newLeftTop;
            leafPanel.RightBottomPanel = newRightBottom;
            leafPanel.TabUrl = null; // No longer a leaf
            leafPanel.TabUrls.Clear();

            return newRightBottom;
        }

        /// <summary>
        /// Close a panel and merge with its sibling if possible
        /// </summary>
        public bool ClosePanel(string panelId)
        {
            if (_rootState.GroupId == panelId && _rootState.IsLeafPanel)
                return false; // Cannot close the last panel

            return _rootState.RemoveLeafPanel(panelId);
        }

        /// <summary>
        /// Update split ratio
        /// </summary>
        public bool UpdateSplitRatio(string panelId, double newRatio)
        {
            if (newRatio < 0.1 || newRatio > 0.9)
                return false;

            var panel = FindPanelContainingChild(panelId);
            if (panel != null && !panel.IsLeafPanel)
            {
                panel.SplitRatio = newRatio;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Find parent panel that contains the given child panel
        /// </summary>
        private SplitViewState? FindPanelContainingChild(string childPanelId)
        {
            return FindPanelContainingChildRecursive(_rootState, childPanelId);
        }

        private SplitViewState? FindPanelContainingChildRecursive(SplitViewState panel, string childPanelId)
        {
            if ((panel.LeftTopPanel?.GroupId == childPanelId) || (panel.RightBottomPanel?.GroupId == childPanelId))
                return panel;

            if (panel.LeftTopPanel != null)
            {
                var found = FindPanelContainingChildRecursive(panel.LeftTopPanel, childPanelId);
                if (found != null) return found;
            }

            if (panel.RightBottomPanel != null)
            {
                var found = FindPanelContainingChildRecursive(panel.RightBottomPanel, childPanelId);
                if (found != null) return found;
            }

            return null;
        }

        /// <summary>
        /// Store panel-specific data
        /// </summary>
        public void SetPanelData(string panelId, object data)
        {
            _panelData[panelId] = data;
        }

        /// <summary>
        /// Get panel-specific data
        /// </summary>
        public object? GetPanelData(string panelId)
        {
            return _panelData.TryGetValue(panelId, out var data) ? data : null;
        }

        /// <summary>
        /// Serialize state to JSON-friendly format for persistence
        /// </summary>
        public string SerializeState()
        {
            return System.Text.Json.JsonSerializer.Serialize(_rootState, new System.Text.Json.JsonSerializerOptions 
            { 
                WriteIndented = false 
            });
        }

        /// <summary>
        /// Deserialize state from JSON
        /// </summary>
        public bool DeserializeState(string json)
        {
            try
            {
                var state = System.Text.Json.JsonSerializer.Deserialize<SplitViewState>(json);
                if (state != null)
                {
                    _rootState = state;
                    return true;
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Check if we're in split view mode
        /// </summary>
        public bool IsInSplitMode => !_rootState.IsLeafPanel;

        /// <summary>
        /// Get number of leaf panels
        /// </summary>
        public int GetPanelCount() => GetAllLeafPanels().Count;

        /// <summary>
        /// Reset to single panel mode
        /// </summary>
        public void ResetToSinglePanel()
        {
            _rootState = new SplitViewState();
            _panelData.Clear();
        }
    }
}
