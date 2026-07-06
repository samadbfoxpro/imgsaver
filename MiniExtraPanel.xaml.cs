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
            Loaded += MiniExtraPanel_Loaded;
        }

        private void MiniExtraPanel_Loaded(object sender, RoutedEventArgs e)
        {
            try { ExtraManager.Load(); } catch { }
            RefreshExtraList();
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
            bool custom = ChkUseCustomText.IsChecked == true;
            GridCustomText.Visibility  = custom ? Visibility.Visible : Visibility.Collapsed;
            BorderExtraList.Visibility = custom ? Visibility.Collapsed : Visibility.Visible;
            GridSearch.Visibility      = custom ? Visibility.Collapsed : Visibility.Visible;
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
