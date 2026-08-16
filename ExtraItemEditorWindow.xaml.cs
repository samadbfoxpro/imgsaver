using System.Windows;
using System.Windows.Input;

namespace imgsaver
{
    public partial class ExtraItemEditorWindow : Window
    {
        private ExtraItem? _editingExtra = null;
        private bool _isEditing = false;

        public ExtraItemEditorWindow(string? extraId = null)
        {
            InitializeComponent();
            LoadCharacters();
            
            if (extraId != null)
            {
                var extra = ExtraManager.GetAll().Find(c => c.Id == extraId);
                if (extra != null)
                {
                    CharacterList.SelectedItem = extra;
                }
            }
            else
            {
                ClearForm();
            }
        }

        /// <summary>
        /// Loads all characters into the list.
        /// </summary>
        private void LoadCharacters()
        {
            CharacterList.ItemsSource = null;
            CharacterList.ItemsSource = ExtraManager.GetAll();
        }

        /// <summary>
        /// Clears the edit form for new entry.
        /// </summary>
        private void ClearForm()
        {
            _editingExtra = null;
            _isEditing = false;
            TxtFormTitle.Text = "ADD NEW EXTRA";
            TxtCharacterName.Text = "";
            TxtCharacterPersona.Text = "";
            CharacterList.SelectedItem = null;
        }

        /// <summary>
        /// Fills form with selected character for editing.
        /// </summary>
        private void CharacterList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (CharacterList.SelectedItem is ExtraItem extra)
            {
                _editingExtra = extra;
                _isEditing = true;
                TxtFormTitle.Text = "EDIT EXTRA";
                TxtCharacterName.Text = extra.ShortName;
                TxtCharacterPersona.Text = extra.Text;
            }
        }

        /// <summary>
        /// Add new button - clears form.
        /// </summary>
        private void BtnAddNew_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
            TxtCharacterName.Focus();
        }

        /// <summary>
        /// Save button - adds or updates character.
        /// </summary>
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            string name = TxtCharacterName.Text.Trim();
            string extraText = TxtCharacterPersona.Text.Trim();

            if (string.IsNullOrEmpty(name))
            {
                CustomMessageBox.Show("Please enter an extra name.", "Validation", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtCharacterName.Focus();
                return;
            }

            if (string.IsNullOrEmpty(extraText))
            {
                CustomMessageBox.Show("Please enter extra text.", "Validation", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtCharacterPersona.Focus();
                return;
            }

            if (_isEditing && _editingExtra != null)
            {
                _editingExtra.ShortName = name;
                _editingExtra.Text = extraText;
                ExtraManager.Update(_editingExtra);
            }
            else
            {
                var newExtra = new ExtraItem
                {
                    ShortName = name,
                    Text = extraText
                };
                ExtraManager.Add(newExtra);
            }

            LoadCharacters();
            ClearForm();
        }

        /// <summary>
        /// Cancel button - clears form.
        /// </summary>
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
        }

        /// <summary>
        /// Delete button - removes character with confirmation.
        /// </summary>
        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.Tag is string id)
            {
                var result = CustomMessageBox.Show(
                    "Are you sure you want to delete this extra?", 
                    "Confirm Delete",
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    ExtraManager.Delete(id);
                    LoadCharacters();
                    ClearForm();
                }
            }
        }

        /// <summary>
        /// Handles title bar drag.
        /// </summary>
        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized 
                    ? WindowState.Normal 
                    : WindowState.Maximized;
            }
            else
            {
                DragMove();
            }
        }

        /// <summary>
        /// Close button.
        /// </summary>
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                var resizeThickness = SystemParameters.WindowResizeBorderThickness;
                MainBorder.Margin = new Thickness(resizeThickness.Left, resizeThickness.Top, resizeThickness.Right, resizeThickness.Bottom);
                MainBorder.CornerRadius = new CornerRadius(0);
                MainBorder.BorderThickness = new Thickness(0);
            }
            else
            {
                MainBorder.Margin = new Thickness(0);
                MainBorder.CornerRadius = new CornerRadius(8);
                MainBorder.BorderThickness = new Thickness(1);
            }
        }
    }
}
