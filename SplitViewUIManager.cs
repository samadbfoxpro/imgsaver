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
        private readonly Dictionary<string, List<WpfTabItem>> _panelTabs = new();
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
            _panelTabs[rootPanelId] = new List<WpfTabItem>();
        }

        /// <summary>
        /// Add a new panel when split is created
        /// </summary>
        public void RegisterNewPanel(string panelId)
        {
            if (!_panelTabs.ContainsKey(panelId))
            {
                _panelTabs[panelId] = new List<WpfTabItem>();
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
        public void AddTabToPanel(string panelId, WpfTabItem tabItem)
        {
            if (!_panelTabs.ContainsKey(panelId))
                _panelTabs[panelId] = new List<WpfTabItem>();

            _panelTabs[panelId].Add(tabItem);

            var tabControl = GetPanelTabControl(panelId);
            if (tabControl != null)
            {
                tabControl.Items.Add(tabItem);
                tabControl.SelectedItem = tabItem; // Select newly added tab
            }
        }

        /// <summary>
        /// Remove a tab from a panel
        /// </summary>
        public void RemoveTabFromPanel(string panelId, WpfTabItem tabItem)
        {
            if (_panelTabs.TryGetValue(panelId, out var tabs))
            {
                tabs.Remove(tabItem);
            }

            var tabControl = GetPanelTabControl(panelId);
            if (tabControl != null && tabControl.Items.Contains(tabItem))
            {
                tabControl.Items.Remove(tabItem);
            }
        }

        /// <summary>
        /// Transfer a tab from one panel to another
        /// </summary>
        public bool TransferTabBetweenPanels(string sourcePanelId, string targetPanelId, WpfTabItem tabItem)
        {
            if (!_panelTabs.ContainsKey(targetPanelId))
                return false;

            RemoveTabFromPanel(sourcePanelId, tabItem);
            AddTabToPanel(targetPanelId, tabItem);
            return true;
        }

        /// <summary>
        /// Get all tabs in a panel
        /// </summary>
        public List<WpfTabItem> GetPanelTabs(string panelId)
        {
            return _panelTabs.TryGetValue(panelId, out var tabs) ? tabs : new List<WpfTabItem>();
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
    }
}
