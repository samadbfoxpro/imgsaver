using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Windows.Controls;

namespace imgsaver
{
    public enum SplitOrientation
    {
        Vertical,
        Horizontal
    }

    public class SplitViewState
    {
        public string GroupId { get; set; } = Guid.NewGuid().ToString("N");
        public bool IsLeafPanel { get; set; } = true;
        public SplitOrientation Orientation { get; set; } = SplitOrientation.Vertical;
        public double SplitRatio { get; set; } = 0.5;
        public SplitViewState? LeftTopPanel { get; set; }
        public SplitViewState? RightBottomPanel { get; set; }
    }

    public class SplitViewManager
    {
        private SplitViewState _root = NewLeaf();

        public bool IsInSplitMode => !_root.IsLeafPanel;

        public SplitViewState GetRootState() => _root;

        public SplitViewState? CreateSplit(string panelId, SplitOrientation orientation)
        {
            SplitViewState? target = FindPanel(_root, panelId);
            if (target == null || !target.IsLeafPanel) return null;

            var originalId = target.GroupId;
            var newPanel = NewLeaf();
            target.IsLeafPanel = false;
            target.Orientation = orientation;
            target.SplitRatio = 0.5;
            target.LeftTopPanel = new SplitViewState { GroupId = originalId };
            target.RightBottomPanel = newPanel;
            return newPanel;
        }

        public bool ClosePanel(string panelId)
        {
            if (_root.GroupId == panelId)
            {
                ResetToSinglePanel();
                return true;
            }

            return CollapseParent(_root, panelId);
        }

        public int GetPanelCount() => GetAllLeafPanels().Count;

        public List<SplitViewState> GetAllLeafPanels()
        {
            var panels = new List<SplitViewState>();
            AddLeafPanels(_root, panels);
            return panels;
        }

        public void ResetToSinglePanel()
        {
            _root = NewLeaf();
        }

        public string SerializeState()
        {
            return JsonSerializer.Serialize(_root);
        }

        public bool DeserializeState(string state)
        {
            try
            {
                var restored = JsonSerializer.Deserialize<SplitViewState>(state);
                if (restored == null) return false;
                _root = restored;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static SplitViewState NewLeaf() => new SplitViewState();

        private static SplitViewState? FindPanel(SplitViewState? state, string panelId)
        {
            if (state == null) return null;
            if (state.GroupId == panelId) return state;
            return FindPanel(state.LeftTopPanel, panelId) ?? FindPanel(state.RightBottomPanel, panelId);
        }

        private static void AddLeafPanels(SplitViewState? state, List<SplitViewState> panels)
        {
            if (state == null) return;
            if (state.IsLeafPanel) panels.Add(state);
            else
            {
                AddLeafPanels(state.LeftTopPanel, panels);
                AddLeafPanels(state.RightBottomPanel, panels);
            }
        }

        private static bool CollapseParent(SplitViewState parent, string panelId)
        {
            if (parent.IsLeafPanel) return false;

            if (parent.LeftTopPanel?.GroupId == panelId && parent.RightBottomPanel != null)
            {
                CopyState(parent.RightBottomPanel, parent);
                return true;
            }

            if (parent.RightBottomPanel?.GroupId == panelId && parent.LeftTopPanel != null)
            {
                CopyState(parent.LeftTopPanel, parent);
                return true;
            }

            return (parent.LeftTopPanel != null && CollapseParent(parent.LeftTopPanel, panelId)) ||
                   (parent.RightBottomPanel != null && CollapseParent(parent.RightBottomPanel, panelId));
        }

        private static void CopyState(SplitViewState source, SplitViewState target)
        {
            target.GroupId = source.GroupId;
            target.IsLeafPanel = source.IsLeafPanel;
            target.Orientation = source.Orientation;
            target.SplitRatio = source.SplitRatio;
            target.LeftTopPanel = source.LeftTopPanel;
            target.RightBottomPanel = source.RightBottomPanel;
        }
    }

    public class SplitViewUIManager
    {
        private readonly SplitViewContainer _container;
        private readonly HashSet<string> _panelIds = new();

        public SplitViewUIManager(SplitViewContainer container)
        {
            _container = container;
        }

        public void InitializeRootPanel(string panelId) => RegisterNewPanel(panelId);
        public void RegisterNewPanel(string panelId) => _panelIds.Add(panelId);
        public void UnregisterPanel(string panelId) => _panelIds.Remove(panelId);
        public void SetActivePanel(string panelId) => _container.RaisePanelActivated(panelId);

        public void ClearAll()
        {
            _panelIds.Clear();
            _container.Children.Clear();
        }
    }

    public class SplitViewContainer : Grid
    {
        public event EventHandler<string>? OnPanelClosed;
        public event EventHandler<string>? OnPanelActivated;

        public void RaisePanelClosed(string panelId) => OnPanelClosed?.Invoke(this, panelId);
        public void RaisePanelActivated(string panelId) => OnPanelActivated?.Invoke(this, panelId);
    }
}
