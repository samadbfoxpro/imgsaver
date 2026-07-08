using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace imgsaver
{
    /// <summary>
    /// Compact Extra panel that attaches to MiniClipboardWindow's bottom-right corner.
    /// It has its own lifecycle and is repositioned by MiniClipboardWindow whenever it moves.
    /// </summary>
    public partial class MiniExtraPanel : Window
    {
        private readonly ObservableCollection<SelectableExtra> _extras = new();
        private readonly System.Collections.Generic.HashSet<int> _activeTouchIds = new();

        public MiniExtraPanel()
        {
            InitializeComponent();
            LanguageManager.ApplyWindowLanguage(this);
            
            TouchRightClickHelper.Register(TxtCustomText);
            TouchRightClickHelper.Register(TxtCustomTitle);
            TouchRightClickHelper.Register(TxtTaggerValues);

            Loaded += MiniExtraPanel_Loaded;
        }

        private void MiniExtraPanel_Loaded(object sender, RoutedEventArgs e)
        {
            try { ExtraManager.Load(); } catch { }
            RefreshExtraList();

            TxtTaggerValues.Text = PromptTaggerStore.ManualValues;
            if (!string.IsNullOrEmpty(PromptTaggerStore.ManualTitle))
            {
                TxtCustomTitle.Text = PromptTaggerStore.ManualTitle;
            }
        }

        // ──────────────────────── List management ────────────────────────

        private void RefreshExtraList()
        {
            var selectedIds   = _extras.Where(x => x.IsSelected).Select(x => x.Extra.Id).ToHashSet();
            var confirmedIds  = _extras.Where(x => x.IsConfirmedSelected).Select(x => x.Extra.Id).ToHashSet();

            _extras.Clear();
            var sortedAll = ExtraManager.GetSortedAll();
            for (int i = 0; i < sortedAll.Count; i++)
            {
                var extra = sortedAll[i];
                _extras.Add(new SelectableExtra(extra)
                {
                    OriginalIndex = i,
                    IsSelected          = selectedIds.Contains(extra.Id),
                    IsConfirmedSelected = confirmedIds.Contains(extra.Id)
                });
            }
            ApplySearchFilter();
        }

        private void ApplySearchFilter()
        {
            string query = TxtSearch?.Text?.Trim() ?? "";
            var list = string.IsNullOrEmpty(query)
                ? _extras.ToList()
                : _extras.Where(x =>
                    x.ShortName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    x.Text.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

            LstExtras.ItemsSource = list
                .OrderByDescending(x => x.IsConfirmedSelected)
                .ThenBy(x => x.OriginalIndex)
                .ToList();
        }

        // ──────────────────────── UI Handlers ────────────────────────────

        private void ChkUseCustomText_Changed(object sender, RoutedEventArgs e)
        {
            if (ChkUseCustomText.IsChecked == true)
            {
                ChkUseTaggerValues.IsChecked = false;
            }
            UpdateViewMode();
        }

        private void ChkUseTaggerValues_Changed(object sender, RoutedEventArgs e)
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

            GridCustomText.Visibility   = custom ? Visibility.Visible : Visibility.Collapsed;
            GridTaggerValues.Visibility = tagger ? Visibility.Visible : Visibility.Collapsed;
            
            bool listMode = !custom && !tagger;
            BorderExtraList.Visibility  = listMode ? Visibility.Visible : Visibility.Collapsed;
            GridSearch.Visibility       = listMode ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ExtraCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.CheckBox cb &&
                cb.DataContext is SelectableExtra sel && cb.IsChecked == false)
            {
                sel.IsConfirmedSelected = false;
                ApplySearchFilter();
            }
        }

        private void TxtSearch_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
            => ApplySearchFilter();

        private void BtnClearSearch_Click(object sender, RoutedEventArgs e)
            => TxtSearch.Text = "";

        private void BtnSaveToLibrary_Click(object sender, RoutedEventArgs e)
        {
            string text  = TxtCustomText.Text?.Trim() ?? "";
            string title = TxtCustomTitle.Text?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(text))  { Warn("Enter custom Extra text first.");   return; }
            if (string.IsNullOrWhiteSpace(title)) { Warn("Enter a title for the Extra first."); return; }

            try
            {
                var newExtra = new ExtraItem { ShortName = title, Text = text };
                ExtraManager.Add(newExtra);
                RefreshExtraList();
                ChkUseCustomText.IsChecked = false;

                var added = _extras.FirstOrDefault(x => x.Extra.ShortName == title && x.Extra.Text == text);
                if (added != null) added.IsSelected = true;

                TxtCustomText.Text  = "";
                TxtCustomTitle.Text = "";
            }
            catch (Exception ex) { Warn("Error saving Extra: " + ex.Message); }
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            string finalText, finalTitle, extraId = "";

            if (ChkUseCustomText.IsChecked == true)
            {
                finalText = TxtCustomText.Text?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(finalText)) { Warn("Enter your custom Extra text first."); return; }
                string ct = TxtCustomTitle.Text?.Trim() ?? "";
                finalTitle = string.IsNullOrWhiteSpace(ct) ? "Custom Extra" : ct;
                PromptTaggerStore.UseManualValuesMode = false;
                PromptTaggerStore.Save();
            }
            else if (ChkUseTaggerValues.IsChecked == true)
            {
                string valuesText = TxtTaggerValues.Text?.Trim() ?? "";
                string ct = TxtCustomTitle.Text?.Trim() ?? "";
                finalTitle = string.IsNullOrWhiteSpace(ct) ? "Tagger Prompt" : ct;

                // Save values & title & mode to store
                PromptTaggerStore.ManualValues = valuesText;
                PromptTaggerStore.ManualTitle = ct;
                PromptTaggerStore.UseManualValuesMode = true;
                PromptTaggerStore.Save();

                string template = (PromptTaggerStore.Template ?? "").Replace("\r", " ").Replace("\n", " ");
                while (template.Contains("  ")) template = template.Replace("  ", " ");

                if (string.IsNullOrWhiteSpace(template))
                {
                    Warn("Please define a Template in the Prompt Tagger tab first.");
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
                if (selected.Count == 0) { Warn("Select at least one Extra from the list."); return; }

                finalText  = string.Join(", ", selected.Select(x => x.Text).Where(t => !string.IsNullOrWhiteSpace(t)));
                string ct  = TxtCustomTitle.Text?.Trim() ?? "";
                string com = string.Join(" + ", selected.Select(x => x.ShortName).Where(n => !string.IsNullOrWhiteSpace(n)));
                finalTitle = string.IsNullOrWhiteSpace(ct) ? com : ct;
                if (selected.Count == 1) extraId = selected[0].Extra.Id ?? "";
                PromptTaggerStore.UseManualValuesMode = false;
                PromptTaggerStore.Save();
            }

            if (string.IsNullOrWhiteSpace(finalText)) { Warn("The resulting Extra text is empty."); return; }

            foreach (var ex in _extras) ex.IsConfirmedSelected = ex.IsSelected;
            ApplySearchFilter();

            LastExtraSelectionStore.Save(extraId, finalTitle, finalText, false);
            ExtraFloatBridge.NotifyConfirmed(finalTitle);
        }

        // ──────────────────────── Touch context menu ──────────────────────

        private void MenuCut_Click(object sender, RoutedEventArgs e)       => TxtCustomText.Cut();
        private void MenuCopy_Click(object sender, RoutedEventArgs e)      => TxtCustomText.Copy();
        private void MenuPaste_Click(object sender, RoutedEventArgs e)     => TxtCustomText.Paste();
        private void MenuSelectAll_Click(object sender, RoutedEventArgs e) => TxtCustomText.SelectAll();

        // ──────────────────────── Helper ─────────────────────────────────

        private void Warn(string msg)
        {
            try { CustomMessageBox.Show(msg, "Extra Panel", MessageBoxButton.OK, MessageBoxImage.Warning); }
            catch { System.Windows.MessageBox.Show(msg, "Extra Panel"); }
        }
    }
}
