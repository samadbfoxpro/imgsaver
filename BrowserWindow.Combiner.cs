using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Application = System.Windows.Application;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Brush = System.Windows.Media.Brush;
using Control = System.Windows.Controls.Control;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;

namespace imgsaver
{
    public partial class BrowserWindow
    {
        private PromptCombinerData _combinerData;
        private bool _isProcessingCombinerClipboard = false;

        public void InitializeCombiner()
        {
            try
            {
                _combinerData = PromptCombinerStore.Load();
                if (_combinerData == null) return;

                if (ChkCombinerEnable != null)
                {
                    ChkCombinerEnable.IsChecked = _combinerData.IsEnabled;
                }

                PopulateCombinerFolders();
                UpdateCombinerRuleBadge();
                UpdateCombinerActiveCountBadge();
            }
            catch { }
        }

        private double _combinerButtonsScrollTargetOffset = 0;
        private System.Windows.Threading.DispatcherTimer? _combinerButtonsSmoothScrollTimer;

        private void CombinerButtonsScrollViewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (sender is ScrollViewer scrollViewer)
            {
                e.Handled = true;

                if (_combinerButtonsSmoothScrollTimer == null)
                {
                    _combinerButtonsScrollTargetOffset = scrollViewer.HorizontalOffset;
                    _combinerButtonsSmoothScrollTimer = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Render)
                    {
                        Interval = TimeSpan.FromMilliseconds(12)
                    };
                    _combinerButtonsSmoothScrollTimer.Tick += (s, ev) =>
                    {
                        if (scrollViewer == null) return;
                        double diff = _combinerButtonsScrollTargetOffset - scrollViewer.HorizontalOffset;
                        if (Math.Abs(diff) < 0.3)
                        {
                            scrollViewer.ScrollToHorizontalOffset(_combinerButtonsScrollTargetOffset);
                            _combinerButtonsSmoothScrollTimer.Stop();
                        }
                        else
                        {
                            scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset + diff * 0.22);
                        }
                    };
                }

                double scrollStep = 45.0;
                double delta = e.Delta > 0 ? -scrollStep : scrollStep;

                if (!_combinerButtonsSmoothScrollTimer.IsEnabled)
                {
                    _combinerButtonsScrollTargetOffset = scrollViewer.HorizontalOffset;
                }

                _combinerButtonsScrollTargetOffset = Math.Max(0, Math.Min(scrollViewer.ScrollableWidth, _combinerButtonsScrollTargetOffset + delta));

                if (!_combinerButtonsSmoothScrollTimer.IsEnabled)
                {
                    _combinerButtonsSmoothScrollTimer.Start();
                }
            }
        }

        private double _folderScrollTargetOffset = 0;
        private System.Windows.Threading.DispatcherTimer? _folderSmoothScrollTimer;

        private void ComboBoxItem_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            // Block automatic scrolling when mouse hovers over items in ComboBox dropdown
            e.Handled = true;
        }

        private void CombinerFolderScrollViewer_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            // Block automatic scrolling when mouse hovers over partially visible items
            e.Handled = true;
        }

        private void CombinerFolderScrollViewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (sender is ScrollViewer scrollViewer)
            {
                e.Handled = true;

                if (_folderSmoothScrollTimer == null)
                {
                    _folderScrollTargetOffset = scrollViewer.VerticalOffset;
                    _folderSmoothScrollTimer = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Render)
                    {
                        Interval = TimeSpan.FromMilliseconds(12)
                    };
                    _folderSmoothScrollTimer.Tick += (s, ev) =>
                    {
                        if (scrollViewer == null) return;
                        double diff = _folderScrollTargetOffset - scrollViewer.VerticalOffset;
                        if (Math.Abs(diff) < 0.3)
                        {
                            scrollViewer.ScrollToVerticalOffset(_folderScrollTargetOffset);
                            _folderSmoothScrollTimer.Stop();
                        }
                        else
                        {
                            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + diff * 0.22);
                        }
                    };
                }

                double scrollStep = 32.0;
                double delta = e.Delta > 0 ? -scrollStep : scrollStep;

                if (!_folderSmoothScrollTimer.IsEnabled)
                {
                    _folderScrollTargetOffset = scrollViewer.VerticalOffset;
                }

                _folderScrollTargetOffset = Math.Max(0, Math.Min(scrollViewer.ScrollableHeight, _folderScrollTargetOffset + delta));

                if (!_folderSmoothScrollTimer.IsEnabled)
                {
                    _folderSmoothScrollTimer.Start();
                }
            }
        }

        private void UpdateCombinerActiveCountBadge()
        {
            try
            {
                if (ChkCombinerEnable == null) return;
                int count = _combinerData != null && _combinerData.ActiveItemIds != null ? _combinerData.ActiveItemIds.Count : 0;
                
                var badgeText = ChkCombinerEnable.Template?.FindName("TxtCombinerActiveCount", ChkCombinerEnable) as TextBlock;
                var badgeBorder = ChkCombinerEnable.Template?.FindName("bdCountBadge", ChkCombinerEnable) as Border;

                if (badgeText != null)
                {
                    badgeText.Text = count.ToString();
                }

                if (badgeBorder != null)
                {
                    bool isEnabled = _combinerData != null && _combinerData.IsEnabled;
                    if (isEnabled)
                    {
                        // When enabled: vibrant green if items selected (#2ECC71), dark muted if 0
                        badgeBorder.Background = count > 0 
                            ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2ECC71"))
                            : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2A5A3D"));
                    }
                    else
                    {
                        // When disabled: dark gray background
                        badgeBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#454545"));
                    }
                }

                // Update individual folder counts
                if (_combinerData != null && _combinerData.Folders != null && _combinerData.Items != null)
                {
                    foreach (var folder in _combinerData.Folders)
                    {
                        folder.ActiveCount = _combinerData.Items
                            .Count(i => i.FolderId == folder.Id && _combinerData.ActiveItemIds.Contains(i.Id));
                    }

                    if (CboCombinerFolders != null)
                    {
                        CboCombinerFolders.Items.Refresh();
                    }
                }
            }
            catch { }
        }

        private void BtnToggleCombinerBar_Click(object sender, RoutedEventArgs e)
        {
            if (CombinerBar != null)
            {
                bool isCurrentlyVisible = CombinerBar.Visibility == Visibility.Visible;
                CombinerBar.Visibility = isCurrentlyVisible ? Visibility.Collapsed : Visibility.Visible;
                if (!isCurrentlyVisible)
                {
                    InitializeCombiner();
                }
            }
        }

        private void ChkCombinerEnable_Changed(object sender, RoutedEventArgs e)
        {
            if (_combinerData == null) return;
            _combinerData.IsEnabled = (ChkCombinerEnable.IsChecked == true);
            PromptCombinerStore.Save(_combinerData);

            if (ChkCombinerEnable != null)
            {
                ChkCombinerEnable.ClearValue(System.Windows.Controls.Control.BorderBrushProperty);
                ChkCombinerEnable.ClearValue(System.Windows.Controls.Control.ForegroundProperty);
            }

            UpdateCombinerActiveCountBadge();

            if (_combinerData.IsEnabled)
            {
                UpdateStatus("🧩 Smart Prompt Combiner Enabled", "Combiner");
            }
            else
            {
                UpdateStatus("🧩 Smart Prompt Combiner Disabled", "Combiner");
            }
        }

        private void PopulateCombinerFolders()
        {
            if (_combinerData == null || CboCombinerFolders == null) return;

            CboCombinerFolders.ItemsSource = null;
            CboCombinerFolders.ItemsSource = _combinerData.Folders.OrderBy(f => f.Order).ToList();

            var activeFolder = _combinerData.Folders.FirstOrDefault(f => f.Id == _combinerData.ActiveFolderId) 
                              ?? _combinerData.Folders.FirstOrDefault();

            if (activeFolder != null)
            {
                _combinerData.ActiveFolderId = activeFolder.Id;
                CboCombinerFolders.SelectedItem = activeFolder;
            }

            RenderCombinerButtons();
        }

        private void CboCombinerFolders_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_combinerData == null) return;
            if (CboCombinerFolders.SelectedItem is PromptCombinerFolder folder)
            {
                _combinerData.ActiveFolderId = folder.Id;
                PromptCombinerStore.Save(_combinerData);
                RenderCombinerButtons();
            }
        }

        private void RenderCombinerButtons()
        {
            if (_combinerData == null || PnlCombinerButtons == null) return;

            PnlCombinerButtons.Children.Clear();

            string currentFolderId = _combinerData.ActiveFolderId;
            var items = _combinerData.Items
                .Where(i => i.FolderId == currentFolderId)
                .OrderBy(i => i.Order)
                .ToList();

            foreach (var item in items)
            {
                var btn = new ToggleButton
                {
                    Content = item.Title,
                    Height = 26,
                    Margin = new Thickness(0, 0, 6, 0),
                    Padding = new Thickness(10, 0, 10, 0),
                    FontSize = 11,
                    FontWeight = FontWeights.SemiBold,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    Tag = item.Id,
                    IsChecked = _combinerData.ActiveItemIds.Contains(item.Id),
                    ToolTip = $"Text: {item.Text}"
                };

                // Custom Pill Styling
                btn.Style = CreateCombinerButtonStyle(btn.IsChecked == true);

                btn.Click += (s, e) =>
                {
                    bool isChecked = btn.IsChecked == true;
                    if (isChecked)
                    {
                        if (!_combinerData.ActiveItemIds.Contains(item.Id))
                        {
                            _combinerData.ActiveItemIds.Add(item.Id);
                        }
                    }
                    else
                    {
                        _combinerData.ActiveItemIds.Remove(item.Id);
                    }

                    btn.Style = CreateCombinerButtonStyle(isChecked);
                    PromptCombinerStore.Save(_combinerData);
                    UpdateCombinerActiveCountBadge();

                    if (isChecked)
                    {
                        UpdateStatus($"➕ Added '{item.Title}' to Active Snippets", "Combiner");
                    }
                };

                PnlCombinerButtons.Children.Add(btn);
            }

            UpdateCombinerActiveCountBadge();
        }

        private Style CreateCombinerButtonStyle(bool isActive)
        {
            var style = new Style(typeof(ToggleButton));

            Brush borderBrush = isActive 
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2ECC71"))
                : GetResourceBrush("BorderBrush", "#3E3E42");

            Brush bgBrush = isActive
                ? new SolidColorBrush(Color.FromArgb(60, 46, 204, 113))
                : GetResourceBrush("SurfaceBrush", "#252526");

            Brush fgBrush = isActive
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2ECC71"))
                : GetResourceBrush("ForegroundBrush", "#FFFFFF");

            var template = new ControlTemplate(typeof(ToggleButton));
            var factory = new FrameworkElementFactory(typeof(Border));
            factory.SetValue(Border.BackgroundProperty, bgBrush);
            factory.SetValue(Border.BorderBrushProperty, borderBrush);
            factory.SetValue(Border.BorderThicknessProperty, new Thickness(1.2));
            factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(13));
            factory.SetValue(Border.PaddingProperty, new Thickness(10, 0, 10, 0));

            var textFactory = new FrameworkElementFactory(typeof(TextBlock));
            textFactory.SetValue(TextBlock.TextProperty, new TemplateBindingExtension(ContentControl.ContentProperty));
            textFactory.SetValue(TextBlock.ForegroundProperty, fgBrush);
            textFactory.SetValue(TextBlock.FontSizeProperty, 11.0);
            textFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            textFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            textFactory.SetValue(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center);

            factory.AppendChild(textFactory);
            template.VisualTree = factory;

            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            return style;
        }

        private Brush GetResourceBrush(string resourceKey, string fallbackHex)
        {
            try
            {
                var resource = this.TryFindResource(resourceKey) ?? Application.Current?.TryFindResource(resourceKey);
                if (resource is Brush b) return b;
                if (resource is Color c) return new SolidColorBrush(c);
            }
            catch { }
            try
            {
                var col = (Color)ColorConverter.ConvertFromString(fallbackHex);
                return new SolidColorBrush(col);
            }
            catch
            {
                return System.Windows.Media.Brushes.Gray;
            }
        }

        private void UpdateCombinerRuleBadge()
        {
            if (_combinerData == null || BtnCombinerRuleBadge == null) return;

            switch (_combinerData.PlacementMode)
            {
                case CombinerPlacementMode.AtBeginning:
                    BtnCombinerRuleBadge.Content = "📍 At Start";
                    break;
                case CombinerPlacementMode.AtEnd:
                    BtnCombinerRuleBadge.Content = "📍 At End";
                    break;
                case CombinerPlacementMode.PerFolder:
                    BtnCombinerRuleBadge.Content = "📍 Per-Folder Rule";
                    break;
                case CombinerPlacementMode.AfterComma:
                default:
                    BtnCombinerRuleBadge.Content = $"📍 After Comma #{_combinerData.CommaIndex}";
                    break;
            }
        }

        private void BtnCombinerRuleBadge_Click(object sender, RoutedEventArgs e)
        {
            if (_combinerData == null) return;

            // Cycle through placement modes: AfterComma -> AtBeginning -> AtEnd -> PerFolder -> AfterComma
            if (_combinerData.PlacementMode == CombinerPlacementMode.AfterComma)
                _combinerData.PlacementMode = CombinerPlacementMode.AtBeginning;
            else if (_combinerData.PlacementMode == CombinerPlacementMode.AtBeginning)
                _combinerData.PlacementMode = CombinerPlacementMode.AtEnd;
            else if (_combinerData.PlacementMode == CombinerPlacementMode.AtEnd)
                _combinerData.PlacementMode = CombinerPlacementMode.PerFolder;
            else
                _combinerData.PlacementMode = CombinerPlacementMode.AfterComma;

            PromptCombinerStore.Save(_combinerData);
            UpdateCombinerRuleBadge();
            UpdateStatus($"📍 Combiner Placement Rule: {BtnCombinerRuleBadge.Content}", "Combiner");
        }

        private void BtnOpenCombinerManager_Click(object sender, RoutedEventArgs e)
        {
            var managerWindow = new PromptCombinerManagerWindow();
            managerWindow.Owner = this;
            if (managerWindow.ShowDialog() == true)
            {
                _combinerData = PromptCombinerStore.Load();
                PopulateCombinerFolders();
                UpdateCombinerRuleBadge();
                UpdateStatus("⚙ Prompt Combiner settings updated", "Combiner");
            }
        }

        public bool TryProcessCombinerText(string rawText, out string combinedResult)
        {
            combinedResult = rawText;
            if (_isProcessingCombinerClipboard) return false;
            if (_combinerData == null || !_combinerData.IsEnabled) return false;
            if (_combinerData.ActiveItemIds == null || _combinerData.ActiveItemIds.Count == 0) return false;

            try
            {
                var activeItems = _combinerData.Items
                    .Where(i => _combinerData.ActiveItemIds.Contains(i.Id))
                    .Select(i => i.Text)
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .ToList();

                if (activeItems.Count == 0) return false;

                _isProcessingCombinerClipboard = true;
                if (_combinerData.PlacementMode == CombinerPlacementMode.PerFolder)
                {
                    combinedResult = PromptCombinerEngine.CombinePerFolder(rawText, _combinerData);
                }
                else
                {
                    combinedResult = PromptCombinerEngine.Combine(rawText, activeItems, _combinerData.PlacementMode, _combinerData.CommaIndex, _combinerData.Separator);
                }
                _isProcessingCombinerClipboard = false;

                if (combinedResult != rawText)
                {
                    UpdateStatus($"⚡ Smart Combiner: Added {activeItems.Count} snippet(s)!", "Combiner");
                    FlashCombinerSuccess();
                    return true;
                }
            }
            catch
            {
                _isProcessingCombinerClipboard = false;
            }

            return false;
        }

        public void FlashCombinerSuccess()
        {
            Dispatcher.InvokeAsync(() =>
            {
                if (ChkCombinerEnable == null) return;
                try
                {
                    var vividOrange = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FF6A00"); // Bold, bright orange
                    var normalGreen = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2ECC71");

                    var borderBrush = new System.Windows.Media.SolidColorBrush(normalGreen);
                    var textBrush = new System.Windows.Media.SolidColorBrush(normalGreen);

                    ChkCombinerEnable.BorderBrush = borderBrush;
                    ChkCombinerEnable.Foreground = textBrush;

                    var animation = new System.Windows.Media.Animation.ColorAnimation
                    {
                        From = normalGreen,
                        To = vividOrange,
                        Duration = new System.Windows.Duration(TimeSpan.FromMilliseconds(250)),
                        AutoReverse = true,
                        RepeatBehavior = new System.Windows.Media.Animation.RepeatBehavior(3)
                    };

                    borderBrush.BeginAnimation(System.Windows.Media.SolidColorBrush.ColorProperty, animation);
                    textBrush.BeginAnimation(System.Windows.Media.SolidColorBrush.ColorProperty, animation);
                }
                catch { }
            });
        }
    }
}
