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

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        private const int WM_CLIPBOARDUPDATE_COMBINER = 0x031D;
        private System.Windows.Interop.HwndSource? _combinerHwndSource;
        private string _lastCombinerClipboardText = "";
        private bool _ignoreNextCombinerClipboardChange = false;

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
                RefreshBasePromptToolTip();
                RegisterCombinerClipboardListener();
            }
            catch { }
        }

        private void RegisterCombinerClipboardListener()
        {
            if (_combinerHwndSource != null) return;
            try
            {
                var helper = new System.Windows.Interop.WindowInteropHelper(this);
                IntPtr handle = helper.EnsureHandle();
                if (handle != IntPtr.Zero)
                {
                    _combinerHwndSource = System.Windows.Interop.HwndSource.FromHwnd(handle);
                    _combinerHwndSource?.AddHook(CombinerWndProc);
                    AddClipboardFormatListener(handle);
                }
            }
            catch { }
        }

        private IntPtr CombinerWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_CLIPBOARDUPDATE_COMBINER)
            {
                OnCombinerClipboardUpdate();
            }
            return IntPtr.Zero;
        }

        private void OnCombinerClipboardUpdate()
        {
            try
            {
                if (_ignoreNextCombinerClipboardChange)
                {
                    _ignoreNextCombinerClipboardChange = false;
                    return;
                }

                _combinerData = PromptCombinerStore.Load();
                if (_combinerData == null || !_combinerData.IsEnabled) return;

                string rawText = SafeBrowserClipboardGetText();
                if (string.IsNullOrWhiteSpace(rawText) || rawText == _lastCombinerClipboardText) return;

                if (TryProcessCombinerText(rawText, out string combinedResult))
                {
                    _ignoreNextCombinerClipboardChange = true;
                    _lastCombinerClipboardText = combinedResult;
                    SafeBrowserClipboardSetText(combinedResult + "\u200B");
                }
            }
            catch { }
        }

        private static string SafeBrowserClipboardGetText()
        {
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    if (System.Windows.Clipboard.ContainsText())
                    {
                        return System.Windows.Clipboard.GetText();
                    }
                    return string.Empty;
                }
                catch
                {
                    System.Threading.Thread.Sleep(20);
                }
            }
            return string.Empty;
        }

        private static void SafeBrowserClipboardSetText(string text)
        {
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    System.Windows.Clipboard.SetText(text);
                    return;
                }
                catch
                {
                    System.Threading.Thread.Sleep(20);
                }
            }
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
                int customActiveCount = _combinerData != null && _combinerData.Folders != null 
                    ? _combinerData.Folders.Count(f => f.IsCustomInput && f.IsCustomInputActive && !string.IsNullOrWhiteSpace(f.CustomInputText)) 
                    : 0;
                int totalCount = count + customActiveCount;
                
                var badgeText = ChkCombinerEnable.Template?.FindName("TxtCombinerActiveCount", ChkCombinerEnable) as TextBlock;
                var badgeBorder = ChkCombinerEnable.Template?.FindName("bdCountBadge", ChkCombinerEnable) as Border;

                if (badgeText != null)
                {
                    badgeText.Text = totalCount.ToString();
                }

                if (badgeBorder != null)
                {
                    bool isEnabled = _combinerData != null && _combinerData.IsEnabled;
                    if (isEnabled)
                    {
                        // When enabled: vibrant green if items selected (#2ECC71), dark muted if 0
                        badgeBorder.Background = totalCount > 0 
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
                        if (folder.IsCustomInput)
                        {
                            folder.ActiveCount = (folder.IsCustomInputActive && !string.IsNullOrWhiteSpace(folder.CustomInputText)) ? 1 : 0;
                        }
                        else
                        {
                            folder.ActiveCount = _combinerData.Items
                                .Count(i => i.FolderId == folder.Id && _combinerData.ActiveItemIds.Contains(i.Id));
                        }
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

        private bool _isUpdatingCustomInputText = false;

        private void RenderCombinerButtons()
        {
            if (_combinerData == null || PnlCombinerButtons == null) return;

            string currentFolderId = _combinerData.ActiveFolderId;
            var activeFolder = _combinerData.Folders.FirstOrDefault(f => f.Id == currentFolderId);

            if (activeFolder != null && activeFolder.IsCustomInput)
            {
                if (SvcCombinerButtons != null) SvcCombinerButtons.Visibility = Visibility.Collapsed;
                if (PnlCombinerCustomInput != null) PnlCombinerCustomInput.Visibility = Visibility.Visible;

                _isUpdatingCustomInputText = true;
                if (TxtCombinerCustomInput != null)
                {
                    TxtCombinerCustomInput.Text = activeFolder.CustomInputText ?? "";
                }
                if (TxtCombinerCustomTitle != null)
                {
                    TxtCombinerCustomTitle.Text = activeFolder.CustomTitle ?? "";
                }
                _isUpdatingCustomInputText = false;

                UpdateCombinerActiveCountBadge();
                return;
            }

            if (SvcCombinerButtons != null) SvcCombinerButtons.Visibility = Visibility.Visible;
            if (PnlCombinerCustomInput != null) PnlCombinerCustomInput.Visibility = Visibility.Collapsed;

            PnlCombinerButtons.Children.Clear();

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

        private void TxtCombinerCustomInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingCustomInputText || _combinerData == null) return;
            var activeFolder = _combinerData.Folders.FirstOrDefault(f => f.Id == _combinerData.ActiveFolderId);
            if (activeFolder != null && activeFolder.IsCustomInput && TxtCombinerCustomInput != null)
            {
                activeFolder.CustomInputText = TxtCombinerCustomInput.Text;
                PromptCombinerStore.Save(_combinerData);
                UpdateCombinerActiveCountBadge();
            }
        }

        private void TxtCombinerCustomTitle_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingCustomInputText || _combinerData == null) return;
            var activeFolder = _combinerData.Folders.FirstOrDefault(f => f.Id == _combinerData.ActiveFolderId);
            if (activeFolder != null && activeFolder.IsCustomInput && TxtCombinerCustomTitle != null)
            {
                activeFolder.CustomTitle = TxtCombinerCustomTitle.Text;
                PromptCombinerStore.Save(_combinerData);
            }
        }

        private void BtnClearCustomInput_Click(object sender, RoutedEventArgs e)
        {
            if (TxtCombinerCustomInput != null)
            {
                TxtCombinerCustomInput.Text = "";
            }
            if (TxtCombinerCustomTitle != null)
            {
                TxtCombinerCustomTitle.Text = "";
            }
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
            UpdateStatus($"📍 Combiner Placement Rule: {BtnCombinerRuleBadge?.Content}", "Combiner");
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
            if (rawText.EndsWith("\u200B"))
            {
                combinedResult = rawText.TrimEnd('\u200B');
                return false;
            }
            if (_isProcessingCombinerClipboard) return false;

            _combinerData = PromptCombinerStore.Load();
            if (_combinerData == null || !_combinerData.IsEnabled) return false;

            var activeItems = _combinerData.Items
                .Where(i => _combinerData.ActiveItemIds.Contains(i.Id))
                .Select(i => i.Text)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .ToList();

            var customTexts = _combinerData.Folders
                .Where(f => f.IsCustomInput && f.IsCustomInputActive && !string.IsNullOrWhiteSpace(f.CustomInputText))
                .Select(f => f.CustomInputText.Trim())
                .ToList();

            if (activeItems.Count == 0 && customTexts.Count == 0) return false;

            try
            {
                _isProcessingCombinerClipboard = true;
                if (_combinerData.PlacementMode == CombinerPlacementMode.PerFolder)
                {
                    combinedResult = PromptCombinerEngine.CombinePerFolder(rawText, _combinerData);
                }
                else
                {
                    var allSnippetTexts = new List<string>(activeItems);
                    allSnippetTexts.AddRange(customTexts);
                    combinedResult = PromptCombinerEngine.Combine(rawText, allSnippetTexts, _combinerData.PlacementMode, _combinerData.CommaIndex, _combinerData.Separator);
                }
                _isProcessingCombinerClipboard = false;

                if (combinedResult != rawText)
                {
                    int total = activeItems.Count + customTexts.Count;
                    UpdateStatus($"⚡ Smart Combiner: Added {total} snippet(s)/text!", "Combiner");
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
                CursorCombinerBadge.Show("⚡ Combined!");
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

        private MiniBaseCombinerPanel? _browserBaseCombinerPanel;

        private void BtnCombBase_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string configPath = DataPathManager.GetSettingsFilePath("base_combiner_config.json");
                string baseText = "";
                if (System.IO.File.Exists(configPath))
                {
                    baseText = System.IO.File.ReadAllText(configPath);
                }

                if (string.IsNullOrWhiteSpace(baseText))
                {
                    OpenBaseCombinerEditorPanel();
                    CursorBadgeNotification.ShowCombiner("⚠️ Set Base Prompt first!");
                    return;
                }

                var combinerData = PromptCombinerStore.Load();
                string combinedText = PromptCombinerEngine.CombinePerFolder(baseText, combinerData);

                string clipboardPayload = combinedText + "\u200B";
                System.Windows.Clipboard.SetText(clipboardPayload);

                FlashCombinerSuccess();
                CursorBadgeNotification.ShowCombiner("⚡ Combined Base Prompt!");
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show("Error combining base prompt:\n" + ex.Message, "Error");
            }
        }

        private void BtnCombBase_MouseRightButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            OpenBaseCombinerEditorPanel();
        }

        private void BtnViewBasePrompt_Click(object sender, RoutedEventArgs e)
        {
            OpenBaseCombinerEditorPanel();
        }

        public void RefreshBasePromptToolTip()
        {
            try
            {
                string configPath = DataPathManager.GetSettingsFilePath("base_combiner_config.json");
                string baseText = "";
                if (System.IO.File.Exists(configPath))
                {
                    baseText = System.IO.File.ReadAllText(configPath);
                }

                string preview = string.IsNullOrWhiteSpace(baseText) ? "(No Base Text Set)" : baseText.Trim();
                if (preview.Length > 120) preview = preview.Substring(0, 120) + "...";

                if (BtnCombBase != null)
                {
                    BtnCombBase.ToolTip = $"⚡ MIX Base Prompt\n\n📌 Current Base Text:\n\"{preview}\"\n\n• Click ⚡ MIX: Combine Base Prompt with active snippets & Copy\n• Click 📝 (or Right-Click): View & Edit Base Prompt";
                }
            }
            catch { }
        }

        private bool _isUpdatingInlineBase = false;

        private void ToggleInlineBaseCombinerPanel()
        {
            if (PopInlineBaseCombiner == null) return;

            if (PopInlineBaseCombiner.IsOpen)
            {
                PopInlineBaseCombiner.IsOpen = false;
            }
            else
            {
                LoadInlineBaseData();
                PopInlineBaseCombiner.IsOpen = true;
            }
            RefreshBasePromptToolTip();
        }

        private void LoadInlineBaseData()
        {
            try
            {
                _isUpdatingInlineBase = true;
                string configPath = DataPathManager.GetSettingsFilePath("base_combiner_config.json");
                if (System.IO.File.Exists(configPath))
                {
                    TxtBrowserBasePrompt.Text = System.IO.File.ReadAllText(configPath);
                }
                else
                {
                    TxtBrowserBasePrompt.Text = "";
                }

                if (_combinerData != null && ChkBrowserAutoBase != null)
                {
                    ChkBrowserAutoBase.IsChecked = _combinerData.AutoCaptureBasePrompt;
                }

                UpdateInlineCombinerSummary();
            }
            catch { }
            finally
            {
                _isUpdatingInlineBase = false;
            }
        }

        private void UpdateInlineCombinerSummary()
        {
            try
            {
                var combinerData = _combinerData ?? PromptCombinerStore.Load();
                if (combinerData == null || !combinerData.IsEnabled)
                {
                    TxtBrowserCombinerIcon.Text = "⚠️";
                    TxtBrowserCombinerSummary.Text = "Combiner is currently OFF (Enable Combiner in toolbar)";
                    TxtBrowserCombinerSummary.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 113, 113));
                    return;
                }

                int activeCount = combinerData.ActiveItemIds != null ? combinerData.ActiveItemIds.Count : 0;
                int customActive = combinerData.Folders.Count(f => f.IsCustomInput && f.IsCustomInputActive && !string.IsNullOrWhiteSpace(f.CustomInputText));
                int total = activeCount + customActive;

                if (total == 0)
                {
                    TxtBrowserCombinerIcon.Text = "⚠️";
                    TxtBrowserCombinerSummary.Text = "No active prompt snippets selected (Click buttons in Combiner bar)";
                    TxtBrowserCombinerSummary.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(251, 146, 60));
                }
                else
                {
                    TxtBrowserCombinerIcon.Text = "⚡";
                    TxtBrowserCombinerSummary.Text = $"Ready! {total} active snippet(s)/custom text will be combined with Base";
                    TxtBrowserCombinerSummary.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(56, 189, 248));
                }
            }
            catch { }
        }

        private void OpenBaseCombinerEditorPanel()
        {
            ToggleInlineBaseCombinerPanel();
        }

        private void BtnCloseInlineBaseCombiner_Click(object sender, RoutedEventArgs e)
        {
            if (PopInlineBaseCombiner != null)
            {
                PopInlineBaseCombiner.IsOpen = false;
            }
        }

        private void TxtBrowserBasePrompt_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingInlineBase) return;
            try
            {
                string configPath = DataPathManager.GetSettingsFilePath("base_combiner_config.json");
                string dir = System.IO.Path.GetDirectoryName(configPath);
                if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);
                System.IO.File.WriteAllText(configPath, TxtBrowserBasePrompt.Text);
                RefreshBasePromptToolTip();
            }
            catch { }
        }

        private void ChkBrowserAutoBase_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var combinerData = _combinerData ?? PromptCombinerStore.Load();
                if (combinerData != null)
                {
                    combinerData.AutoCaptureBasePrompt = (ChkBrowserAutoBase.IsChecked == true);
                    PromptCombinerStore.Save(combinerData);
                    _combinerData = combinerData;
                }
            }
            catch { }
        }

        private void BtnBrowserCombineAndCopy_Click(object sender, RoutedEventArgs e)
        {
            BtnCombBase_Click(sender, e);
        }

        private void BtnBrowserCopyBaseOnly_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string text = TxtBrowserBasePrompt.Text;
                if (string.IsNullOrWhiteSpace(text)) return;
                System.Windows.Clipboard.SetText(text);
                CursorBadgeNotification.ShowCombiner("📋 Copied Base!");
            }
            catch { }
        }

        private void BtnBrowserClearBase_Click(object sender, RoutedEventArgs e)
        {
            TxtBrowserBasePrompt.Text = "";
            CursorBadgeNotification.ShowCombiner("🗑️ Base Cleared");
        }

        private bool _wasBaseCombinerOpenBeforeMinimize = false;

        public void OnBrowserWindowStateChanged()
        {
            if (PopInlineBaseCombiner == null) return;

            if (WindowState == WindowState.Minimized)
            {
                if (PopInlineBaseCombiner.IsOpen)
                {
                    _wasBaseCombinerOpenBeforeMinimize = true;
                    PopInlineBaseCombiner.IsOpen = false;
                }
            }
            else
            {
                if (_wasBaseCombinerOpenBeforeMinimize)
                {
                    _wasBaseCombinerOpenBeforeMinimize = false;
                    LoadInlineBaseData();
                    PopInlineBaseCombiner.IsOpen = true;
                }
                RepositionBaseCombinerPopup();
            }
        }

        public void OnBrowserWindowVisibilityChanged(bool isVisibleOrActive)
        {
            if (PopInlineBaseCombiner == null) return;

            if (!isVisibleOrActive)
            {
                if (PopInlineBaseCombiner.IsOpen)
                {
                    _wasBaseCombinerOpenBeforeMinimize = true;
                    PopInlineBaseCombiner.IsOpen = false;
                }
            }
            else
            {
                if (WindowState != WindowState.Minimized && _wasBaseCombinerOpenBeforeMinimize)
                {
                    _wasBaseCombinerOpenBeforeMinimize = false;
                    PopInlineBaseCombiner.IsOpen = true;
                    RepositionBaseCombinerPopup();
                }
            }
        }

        public void RepositionBaseCombinerPopup()
        {
            if (PopInlineBaseCombiner != null && PopInlineBaseCombiner.IsOpen)
            {
                var offset = PopInlineBaseCombiner.HorizontalOffset;
                PopInlineBaseCombiner.HorizontalOffset = offset + 0.001;
                PopInlineBaseCombiner.HorizontalOffset = offset;
            }
        }
        public void RefreshInlineBasePromptUI()
        {
            if (TxtBrowserBasePrompt == null) return;
            try
            {
                string configPath = DataPathManager.GetSettingsFilePath("base_combiner_config.json");
                if (System.IO.File.Exists(configPath))
                {
                    string text = System.IO.File.ReadAllText(configPath);
                    if (TxtBrowserBasePrompt.Text != text)
                    {
                        _isUpdatingInlineBase = true;
                        TxtBrowserBasePrompt.Text = text;
                        _isUpdatingInlineBase = false;
                    }
                }
            }
            catch { }
        }
    }
}
