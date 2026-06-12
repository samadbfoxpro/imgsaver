using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Wpf;
using WpfTabControl = System.Windows.Controls.TabControl;
using WpfTabItem = System.Windows.Controls.TabItem;

namespace imgsaver
{
    /// <summary>
    /// Manages UI rendering and WebView2 integration for split view
    /// Acts as a bridge between SplitViewContainer and BrowserWindow
    /// </summary>
    public class SplitViewUIManager
    {
        private readonly SplitViewContainer _container;
        private readonly Dictionary<string, List<BrowserTabState>> _panelTabs = new();
        private string _activePanelId = "";

        public SplitViewUIManager(SplitViewContainer container)
        {
            _container = container;
        }

        /// <summary>
        /// Initialize with the root panel ID
        /// </summary>
        public void InitializeRootPanel(string rootPanelId)
        {
            _activePanelId = rootPanelId;
            _panelTabs[rootPanelId] = new List<BrowserTabState>();
        }

        /// <summary>
        /// Add a new panel when split is created
        /// </summary>
        public void RegisterNewPanel(string panelId)
        {
            if (!_panelTabs.ContainsKey(panelId))
            {
                _panelTabs[panelId] = new List<BrowserTabState>();
            }
        }

        /// <summary>
        /// Get or create tab control for a panel
        /// </summary>
        public WpfTabControl? GetPanelTabControl(string panelId)
        {
            return _container.GetTabControl(panelId);
        }

        /// <summary>
        /// Add a tab to a specific panel
        /// </summary>
        public void AddTabToPanel(string panelId, BrowserTabState tabState)
        {
            if (!_panelTabs.ContainsKey(panelId))
                _panelTabs[panelId] = new List<BrowserTabState>();

            _panelTabs[panelId].Add(tabState);

            var tabControl = GetPanelTabControl(panelId);
            if (tabControl != null)
            {
                var tabItem = CreateTabItem(tabState);
                tabControl.Items.Add(tabItem);
                tabControl.SelectedItem = tabItem; // Select newly added tab
            }
        }

        /// <summary>
        /// Remove a tab from a panel
        /// </summary>
        public void RemoveTabFromPanel(string panelId, BrowserTabState tabState)
        {
            if (_panelTabs.TryGetValue(panelId, out var tabs))
            {
                tabs.Remove(tabState);
            }

            var tabControl = GetPanelTabControl(panelId);
            if (tabControl != null)
            {
                var itemToRemove = tabControl.Items.Cast<WpfTabItem>()
                    .FirstOrDefault(ti => (ti.DataContext as BrowserTabState) == tabState);
                if (itemToRemove != null)
                {
                    tabControl.Items.Remove(itemToRemove);
                }
            }
        }

        /// <summary>
        /// Transfer a tab from one panel to another
        /// </summary>
        public bool TransferTabBetweenPanels(string sourcePanelId, string targetPanelId, BrowserTabState tabState)
        {
            if (!_panelTabs.ContainsKey(targetPanelId))
                return false;

            RemoveTabFromPanel(sourcePanelId, tabState);
            AddTabToPanel(targetPanelId, tabState);
            return true;
        }

        /// <summary>
        /// Get all tabs in a panel
        /// </summary>
        public List<BrowserTabState> GetPanelTabs(string panelId)
        {
            return _panelTabs.TryGetValue(panelId, out var tabs) ? tabs : new List<BrowserTabState>();
        }

        /// <summary>
        /// Get active panel ID
        /// </summary>
        public string GetActivePanelId() => _activePanelId;

        /// <summary>
        /// Set active panel
        /// </summary>
        public void SetActivePanel(string panelId)
        {
            if (_panelTabs.ContainsKey(panelId))
            {
                _activePanelId = panelId;
            }
        }

        /// <summary>
        /// Get all panel IDs
        /// </summary>
        public List<string> GetAllPanelIds() => new List<string>(_panelTabs.Keys);

        /// <summary>
        /// Clear all panels
        /// </summary>
        public void ClearAll()
        {
            _panelTabs.Clear();
            _activePanelId = "";
        }

        /// <summary>
        /// Remove a panel (called when panel is closed)
        /// </summary>
        public void UnregisterPanel(string panelId)
        {
            _panelTabs.Remove(panelId);
            if (_activePanelId == panelId)
            {
                _activePanelId = _panelTabs.Keys.FirstOrDefault() ?? "";
            }
        }

        /// <summary>
        /// Create a TabItem UI element for a BrowserTabState
        /// </summary>
        private WpfTabItem CreateTabItem(BrowserTabState tabState)
        {
            var tabItem = new WpfTabItem
            {
                Header = GetTabHeader(tabState),
                DataContext = tabState,
                Content = tabState.PrimaryWebView,
                AllowDrop = true
            };

            return tabItem;
        }

        /// <summary>
        /// Get display header for a tab
        /// </summary>
        private string GetTabHeader(BrowserTabState tabState)
        {
            if (tabState?.Tab != null)
                return tabState.Tab.Header?.ToString() ?? "New Tab";
            return "New Tab";
        }
    }
}
