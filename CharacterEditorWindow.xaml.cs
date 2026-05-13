using System.Windows;
using System.Windows.Input;

namespace imgsaver
{
    public partial class CharacterEditorWindow : Window
    {
        private CharacterPersona? _editingCharacter = null;
        private bool _isEditing = false;

        public CharacterEditorWindow(string? characterId = null)
        {
            InitializeComponent();
            LoadCharacters();
            
            if (characterId != null)
            {
                var character = CharacterManager.GetAll().Find(c => c.Id == characterId);
                if (character != null)
                {
                    CharacterList.SelectedItem = character;
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
            CharacterList.ItemsSource = CharacterManager.GetAll();
        }

        /// <summary>
        /// Clears the edit form for new entry.
        /// </summary>
        private void ClearForm()
        {
            _editingCharacter = null;
            _isEditing = false;
            TxtFormTitle.Text = "ADD NEW CHARACTER";
            TxtCharacterName.Text = "";
            TxtCharacterPersona.Text = "";
            CharacterList.SelectedItem = null;
        }

        /// <summary>
        /// Fills form with selected character for editing.
        /// </summary>
        private void CharacterList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (CharacterList.SelectedItem is CharacterPersona character)
            {
                _editingCharacter = character;
                _isEditing = true;
                TxtFormTitle.Text = "EDIT CHARACTER";
                TxtCharacterName.Text = character.ShortName;
                TxtCharacterPersona.Text = character.FullPersona;
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
            string persona = TxtCharacterPersona.Text.Trim();

            if (string.IsNullOrEmpty(name))
            {
                CustomMessageBox.Show("Please enter a character name.", "Validation", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtCharacterName.Focus();
                return;
            }

            if (string.IsNullOrEmpty(persona))
            {
                CustomMessageBox.Show("Please enter persona text.", "Validation", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtCharacterPersona.Focus();
                return;
            }

            if (_isEditing && _editingCharacter != null)
            {
                // Update existing
                _editingCharacter.ShortName = name;
                _editingCharacter.FullPersona = persona;
                CharacterManager.Update(_editingCharacter);
            }
            else
            {
                // Add new
                var newCharacter = new CharacterPersona
                {
                    ShortName = name,
                    FullPersona = persona
                };
                CharacterManager.Add(newCharacter);
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
                    "Are you sure you want to delete this character?", 
                    "Confirm Delete",
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    CharacterManager.Delete(id);
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
    }
}
