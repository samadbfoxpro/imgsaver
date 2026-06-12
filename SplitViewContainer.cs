using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;
using Microsoft.Web.WebView2.Wpf;
using WpfTabControl = System.Windows.Controls.TabControl;

namespace imgsaver
{
    /// <summary>
    /// A container that can render split view panels
    /// </summary>
    public class SplitViewContainer : Grid
    {
        private SplitViewManager _manager;
        private Dictionary<string, Grid> _panelGrids = new();
        private Dictionary<string, object> _panelControls = new();

        public event EventHandler<string>? OnPanelClosed;
        public event EventHandler<string>? OnPanelActivated;

        public SplitViewContainer()
        {
            _manager = new SplitViewManager();
            InitializeRootPanel();
        }

        private void InitializeRootPanel()
        {
            this.Background = (System.Windows.Media.Brush)System.Windows.Application.Current.FindResource("BackgroundBrush");
            var rootState = _manager.GetRootState();
            var rootGrid = CreatePanelGrid(rootState.GroupId);
            this.Children.Add(rootGrid);
        }

        private Grid CreatePanelGrid(string panelId)
        {
            var grid = new Grid
            {
                Background = (System.Windows.Media.Brush)System.Windows.Application.Current.FindResource("BackgroundBrush")
            };

            var state = _manager.GetRootState().FindLeafPanel(panelId);
            if (state == null)
                return grid;

            if (state.IsLeafPanel)
            {
                // Leaf panel - create TabControl
                var tabControl = new WpfTabControl
                {
                    Background = System.Windows.Media.Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Name = $"Tab_{panelId}"
                };
                grid.Children.Add(tabControl);
                _panelGrids[panelId] = grid;
                _panelControls[panelId] = tabControl;
            }
            else
            {
                // Split panel - create two child grids with GridSplitter
                if (state.Orientation == SplitOrientation.Vertical)
                {
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(state.SplitRatio, GridUnitType.Star) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1 - state.SplitRatio, GridUnitType.Star) });

                    var leftGrid = CreatePanelGrid(state.LeftTopPanel!.GroupId);
                    Grid.SetColumn(leftGrid, 0);
                    grid.Children.Add(leftGrid);

                    var splitter = new GridSplitter
                    {
                        Width = 4,
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                        VerticalAlignment = System.Windows.VerticalAlignment.Stretch,
                        Background = (System.Windows.Media.Brush)System.Windows.Application.Current.FindResource("BorderBrush"),
                        Cursor = System.Windows.Input.Cursors.SizeWE
                    };
                    Grid.SetColumn(splitter, 1);
                    grid.Children.Add(splitter);

                    var rightGrid = CreatePanelGrid(state.RightBottomPanel!.GroupId);
                    Grid.SetColumn(rightGrid, 2);
                    grid.Children.Add(rightGrid);
                }
                else // Horizontal
                {
                    grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(state.SplitRatio, GridUnitType.Star) });
                    grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1 - state.SplitRatio, GridUnitType.Star) });

                    var topGrid = CreatePanelGrid(state.LeftTopPanel!.GroupId);
                    Grid.SetRow(topGrid, 0);
                    grid.Children.Add(topGrid);

                    var splitter = new GridSplitter
                    {
                        Height = 4,
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                        VerticalAlignment = System.Windows.VerticalAlignment.Stretch,
                        Background = (System.Windows.Media.Brush)System.Windows.Application.Current.FindResource("BorderBrush"),
                        Cursor = System.Windows.Input.Cursors.SizeNS
                    };
                    Grid.SetRow(splitter, 1);
                    grid.Children.Add(splitter);

                    var bottomGrid = CreatePanelGrid(state.RightBottomPanel!.GroupId);
                    Grid.SetRow(bottomGrid, 2);
                    grid.Children.Add(bottomGrid);
                }

                _panelGrids[panelId] = grid;
            }

            return grid;
        }

        public WpfTabControl? GetTabControl(string panelId)
        {
            if (_panelControls.TryGetValue(panelId, out var control) && control is WpfTabControl tc)
                return tc;
            return null;
        }

        public void CreateSplit(string sourcePanelId, SplitOrientation orientation)
        {
            var newPanel = _manager.CreateSplit(sourcePanelId, orientation);
            if (newPanel != null)
            {
                // Rebuild UI
                this.Children.Clear();
                InitializeRootPanel();
            }
        }

        public void ClosePanel(string panelId)
        {
            if (_manager.ClosePanel(panelId))
            {
                this.Children.Clear();
                InitializeRootPanel();
                OnPanelClosed?.Invoke(this, panelId);
            }
        }

        public List<string> GetAllPanelIds()
        {
            var ids = new List<string>();
            foreach (var panel in _manager.GetAllLeafPanels())
            {
                ids.Add(panel.GroupId);
            }
            return ids;
        }

        public bool IsInSplitMode => _manager.IsInSplitMode;

        public int GetPanelCount => _manager.GetPanelCount();
    }
}
