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

        public FloatingExtraWindow()
        {
            InitializeComponent();
            Loaded += FloatingExtraWindow_Loaded;
        }

        private void FloatingExtraWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try { ExtraManager.Load(); } catch { }

            RefreshExtraList();

            if (!string.IsNullOrEmpty(ExtraFloatBridge.LastConfirmedTitle))
                TxtLastConfirmedTitle.Text = ExtraFloatBridge.LastConfirmedTitle;
        }

        private void RefreshExtraList()
        {
            _extras.Clear();
            foreach (var extra in ExtraManager.GetSortedAll())
                _extras.Add(new SelectableExtra(extra));
            LstExtras.ItemsSource = _extras;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1) DragMove();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        private void ChkUseCustomText_CheckedChanged(object sender, RoutedEventArgs e)
        {
            bool useCustom = ChkUseCustomText.IsChecked == true;
            PanelCustomText.Visibility = useCustom ? Visibility.Visible : Visibility.Collapsed;
            BorderExtraList.Visibility = useCustom ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ExtraCheck_Changed(object sender, RoutedEventArgs e)
        {
            // No-op: selection state is bound directly via SelectableExtra.IsSelected.
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
            }

            if (string.IsNullOrWhiteSpace(finalText))
            {
                ShowValidationMessage("The resulting Extra text is empty.");
                return;
            }

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
    }
}
