using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace imgsaver
{
    /// <summary>
    /// Small selectable wrapper around ExtraItem so the list can support multi-select
    /// via checkboxes without touching ExtraManager / ExtraItem itself.
    /// </summary>
    public class SelectableExtra : INotifyPropertyChanged
    {
        public ExtraItem Extra { get; }
        public string ShortName => Extra.ShortName;
        public string Text => Extra.Text;
        public int OriginalIndex { get; set; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        private bool _isConfirmedSelected;
        public bool IsConfirmedSelected
        {
            get => _isConfirmedSelected;
            set
            {
                if (_isConfirmedSelected != value)
                {
                    _isConfirmedSelected = value;
                    OnPropertyChanged(nameof(IsConfirmedSelected));
                }
            }
        }

        public SelectableExtra(ExtraItem extra)
        {
            Extra = extra;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public partial class FloatingExtraWindow : Window
    {
        private readonly ObservableCollection<SelectableExtra> _extras = new ObservableCollection<SelectableExtra>();
        private readonly System.Collections.Generic.HashSet<int> _activeTouchIds = new System.Collections.Generic.HashSet<int>();

        public FloatingExtraWindow()
        {
            InitializeComponent();
            
            TouchRightClickHelper.Register(TxtCustomText);
            TouchRightClickHelper.Register(TxtCustomTitle);
            TouchRightClickHelper.Register(TxtTaggerValues);

            this.PreviewMouseWheel += FloatingExtraWindow_PreviewMouseWheel;

            Loaded += FloatingExtraWindow_Loaded;
        }

        private void FloatingExtraWindow_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (BorderExtraList.Visibility == Visibility.Visible)
            {
                var scrollViewer = FindVisualChild<System.Windows.Controls.ScrollViewer>(BorderExtraList);
                if (scrollViewer != null)
                {
                    if (e.Delta < 0)
                        scrollViewer.LineDown();
                    else
                        scrollViewer.LineUp();
                    e.Handled = true;
                }
            }
        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild) return typedChild;
                var result = FindVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }

        private void FloatingExtraWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try { ExtraManager.Load(); } catch { }

            RefreshExtraList();

            if (!string.IsNullOrEmpty(ExtraFloatBridge.LastConfirmedTitle))
                TxtLastConfirmedTitle.Text = ExtraFloatBridge.LastConfirmedTitle;

            TxtTaggerValues.Text = PromptTaggerStore.ManualValues;
            if (!string.IsNullOrEmpty(PromptTaggerStore.ManualTitle))
            {
                TxtCustomTitle.Text = PromptTaggerStore.ManualTitle;
            }
        }

        private void RefreshExtraList()
        {
            var selectedIds = _extras.Where(x => x.IsSelected).Select(x => x.Extra.Id).ToHashSet();
            var confirmedIds = _extras.Where(x => x.IsConfirmedSelected).Select(x => x.Extra.Id).ToHashSet();

            _extras.Clear();
            var sortedAll = ExtraManager.GetSortedAll();
            for (int i = 0; i < sortedAll.Count; i++)
            {
                var extra = sortedAll[i];
                var selectable = new SelectableExtra(extra)
                {
                    OriginalIndex = i,
                    IsSelected = selectedIds.Contains(extra.Id),
                    IsConfirmedSelected = confirmedIds.Contains(extra.Id)
                };
                _extras.Add(selectable);
            }
            ApplySearchFilter();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1) DragMove();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        private void ChkUseCustomText_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (ChkUseCustomText.IsChecked == true)
            {
                ChkUseTaggerValues.IsChecked = false;
            }
            UpdateViewMode();
        }

        private void ChkUseTaggerValues_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (ChkUseTaggerValues.IsChecked == true)
            {
                ChkUseCustomText.IsChecked = false;
            }
            UpdateViewMode();
        }

        private void UpdateViewMode()
        {
            bool custom = ChkUseCustomText.IsChecked == true;
            bool tagger = ChkUseTaggerValues.IsChecked == true;

            PanelCustomText.Visibility  = custom ? Visibility.Visible : Visibility.Collapsed;
            GridTaggerValues.Visibility = tagger ? Visibility.Visible : Visibility.Collapsed;
            
            bool listMode = !custom && !tagger;
            BorderExtraList.Visibility  = listMode ? Visibility.Visible : Visibility.Collapsed;
            GridSearch.Visibility       = listMode ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ExtraCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.CheckBox cb && cb.DataContext is SelectableExtra selectable)
            {
                if (cb.IsChecked == false)
                {
                    selectable.IsConfirmedSelected = false;
                    ApplySearchFilter();
                }
            }
        }

        private void TxtSearch_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            ApplySearchFilter();
        }

        private void BtnClearSearch_Click(object sender, RoutedEventArgs e)
        {
            TxtSearch.Text = "";
        }

        private void ApplySearchFilter()
        {
            string query = TxtSearch?.Text?.Trim() ?? "";
            var listToDisplay = string.IsNullOrEmpty(query)
                ? _extras.ToList()
                : _extras.Where(x => 
                    x.ShortName.Contains(query, StringComparison.OrdinalIgnoreCase) || 
                    x.Text.Contains(query, StringComparison.OrdinalIgnoreCase)
                ).ToList();

            var sorted = listToDisplay.OrderByDescending(x => x.IsConfirmedSelected)
                                      .ThenBy(x => x.OriginalIndex)
                                      .ToList();

            LstExtras.ItemsSource = sorted;
        }

        private void BtnSaveToLibrary_Click(object sender, RoutedEventArgs e)
        {
            string text = TxtCustomText.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(text))
            {
                ShowValidationMessage("Enter custom Extra text first.");
                return;
            }

            string title = TxtCustomTitle.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(title))
            {
                ShowValidationMessage("Enter a title for the Extra first.");
                return;
            }

            try
            {
                var newExtra = new ExtraItem { ShortName = title, Text = text };
                ExtraManager.Add(newExtra);
                
                // Reload and refresh the list
                RefreshExtraList();

                // Switch back to list mode so they can see the new item
                ChkUseCustomText.IsChecked = false;

                // Select the newly added item in the UI list
                var addedSelectable = _extras.FirstOrDefault(x => x.Extra.ShortName == title && x.Extra.Text == text);
                if (addedSelectable != null)
                {
                    addedSelectable.IsSelected = true;
                }

                // Clear input fields
                TxtCustomText.Text = "";
                TxtCustomTitle.Text = "";
            }
            catch (Exception ex)
            {
                ShowValidationMessage("Error saving Extra: " + ex.Message);
            }
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            string finalText;
            string finalTitle;
            string extraId = "";

            if (ChkUseCustomText.IsChecked == true)
            {
                finalText = TxtCustomText.Text?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(finalText))
                {
                    ShowValidationMessage("Enter your custom Extra text first.");
                    return;
                }

                string customTitle = TxtCustomTitle.Text?.Trim() ?? "";
                finalTitle = !string.IsNullOrWhiteSpace(customTitle) ? customTitle : "Custom Extra";
                PromptTaggerStore.UseManualValuesMode = false;
                PromptTaggerStore.Save();
            }
            else if (ChkUseTaggerValues.IsChecked == true)
            {
                string valuesText = TxtTaggerValues.Text?.Trim() ?? "";
                string customTitle = TxtCustomTitle.Text?.Trim() ?? "";
                finalTitle = !string.IsNullOrWhiteSpace(customTitle) ? customTitle : "Tagger Prompt";

                // Save values & title & mode to store
                PromptTaggerStore.ManualValues = valuesText;
                PromptTaggerStore.ManualTitle = customTitle;
                PromptTaggerStore.UseManualValuesMode = true;
                PromptTaggerStore.Save();

                string template = (PromptTaggerStore.Template ?? "").Replace("\r", " ").Replace("\n", " ");
                while (template.Contains("  ")) template = template.Replace("  ", " ");

                if (string.IsNullOrWhiteSpace(template))
                {
                    ShowValidationMessage("Please define a Template in the Prompt Tagger tab first.");
                    return;
                }

                var values = valuesText.Replace("\r", " ").Replace("\n", " ")
                                       .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                       .Select(v => v.Trim())
                                       .Where(v => !string.IsNullOrEmpty(v))
                                       .ToList();

                string prefix = PromptTaggerStore.Prefix ?? "PH_";
                var tagRegex = new System.Text.RegularExpressions.Regex($@"\[{System.Text.RegularExpressions.Regex.Escape(prefix)}\d+\]", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                int expectedCount = tagRegex.Matches(template).Count;
                if (values.Count != expectedCount)
                {
                    string warnMsg = LanguageManager.CurrentLanguage == "fa"
                        ? $"هشدار: تعداد مقادیر وارد شده ({values.Count}) با تعداد جایگاه‌های تگ ({expectedCount}) در قالب فعال مطابقت ندارد.\n\nلطفاً عملیات تگ‌گذار پرامپت را مجدداً اجرا کرده و خروجی جدید را ارسال کنید تا هماهنگ شوند."
                        : $"Warning: The number of values ({values.Count}) does not match the placeholder count ({expectedCount}) in the active template.\n\nPlease re-run the Prompt Tagger process and send the new text to keep them synchronized.";
                    
                    System.Windows.MessageBox.Show(warnMsg, 
                        LanguageManager.CurrentLanguage == "fa" ? "عدم هماهنگی تگ‌ها" : "Tag Mismatch", 
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                finalText = template;
                for (int i = 0; i < values.Count; i++)
                {
                    finalText = finalText.Replace($"[{prefix}{i + 1}]", values[i]);
                }
            }
            else
            {
                var selected = _extras.Where(x => x.IsSelected).ToList();
                if (selected.Count == 0)
                {
                    ShowValidationMessage("Select at least one Extra from the list.");
                    return;
                }

                finalText = string.Join(", ", selected.Select(x => x.Text).Where(t => !string.IsNullOrWhiteSpace(t)));
                string combinedTitle = string.Join(" + ", selected.Select(x => x.ShortName).Where(n => !string.IsNullOrWhiteSpace(n)));

                if (selected.Count == 1) extraId = selected[0].Extra.Id ?? "";

                string customTitle = TxtCustomTitle.Text?.Trim() ?? "";
                finalTitle = !string.IsNullOrWhiteSpace(customTitle) ? customTitle : combinedTitle;
                PromptTaggerStore.UseManualValuesMode = false;
                PromptTaggerStore.Save();
            }

            if (string.IsNullOrWhiteSpace(finalText))
            {
                ShowValidationMessage("The resulting Extra text is empty.");
                return;
            }

            // Update confirmed selections to bubble them to the top
            foreach (var extra in _extras)
            {
                extra.IsConfirmedSelected = extra.IsSelected;
            }
            ApplySearchFilter();

            // Reuse the exact same store the rest of the app already relies on for
            // "the last confirmed Extra" (e.g. Persona Injector / Mini Clip consumers).
            LastExtraSelectionStore.Save(extraId, finalTitle, finalText, false);

            TxtLastConfirmedTitle.Text = finalTitle;

            // Automatically push the title to any open Mini Clip window(s) — no extra
            // action required from the user, and no clipboard side effects.
            ExtraFloatBridge.NotifyConfirmed(finalTitle);
        }

        private void ShowValidationMessage(string message)
        {
            try { CustomMessageBox.Show(message, "Extra Float", MessageBoxButton.OK, MessageBoxImage.Warning); }
            catch { System.Windows.MessageBox.Show(message, "Extra Float"); }
        }

        private void PanelCustomText_TouchDown(object? sender, TouchEventArgs e)
        {
            _activeTouchIds.Add(e.TouchDevice.Id);
            if (_activeTouchIds.Count == 2)
            {
                if (TxtCustomText.ContextMenu != null)
                {
                    System.Windows.Point touchPoint = e.GetTouchPoint(TxtCustomText).Position;
                    TxtCustomText.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Relative;
                    TxtCustomText.ContextMenu.PlacementTarget = TxtCustomText;
                    TxtCustomText.ContextMenu.HorizontalOffset = touchPoint.X;
                    TxtCustomText.ContextMenu.VerticalOffset = touchPoint.Y;
                    TxtCustomText.ContextMenu.IsOpen = true;
                }
                e.Handled = true;
            }
        }

        private void PanelCustomText_TouchUp(object? sender, TouchEventArgs e)
        {
            _activeTouchIds.Remove(e.TouchDevice.Id);
        }

        private void PanelCustomText_TouchLeave(object? sender, TouchEventArgs e)
        {
            _activeTouchIds.Remove(e.TouchDevice.Id);
        }

        private void MenuCut_Click(object sender, RoutedEventArgs e)
        {
            TxtCustomText.Cut();
        }

        private void MenuCopy_Click(object sender, RoutedEventArgs e)
        {
            TxtCustomText.Copy();
        }

        private void MenuPaste_Click(object sender, RoutedEventArgs e)
        {
            TxtCustomText.Paste();
        }

        private void MenuSelectAll_Click(object sender, RoutedEventArgs e)
        {
            TxtCustomText.SelectAll();
        }
    }
}
