using System.Windows;
using System.Windows.Input;

namespace imgsaver
{
    public partial class ExtraPromptEditorWindow : Window
    {
        private ExtraPrompt? _editingPrompt = null;
        private bool _isEditing = false;

        public ExtraPromptEditorWindow(string? promptId = null, string? initialPromptText = null)
        {
            InitializeComponent();
            LoadPrompts();

            if (promptId != null)
            {
                var prompt = ExtraPromptManager.GetAll().Find(p => p.Id == promptId);
                if (prompt != null)
                {
                    PromptList.SelectedItem = prompt;
                }
            }
            else
            {
                ClearForm();
                if (!string.IsNullOrEmpty(initialPromptText))
                {
                    TxtPromptText.Text = initialPromptText;
                    TxtPromptName.Focus();
                }
            }
        }

        private void LoadPrompts()
        {
            PromptList.ItemsSource = null;
            PromptList.ItemsSource = ExtraPromptManager.GetAll();
        }

        private void ClearForm()
        {
            _editingPrompt = null;
            _isEditing = false;
            TxtFormTitle.Text = "ADD NEW EXTRA PROMPT";
            TxtPromptName.Text = "";
            TxtPromptText.Text = "";
            PromptList.SelectedItem = null;
        }

        private void PromptList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (PromptList.SelectedItem is ExtraPrompt prompt)
            {
                _editingPrompt = prompt;
                _isEditing = true;
                TxtFormTitle.Text = "EDIT EXTRA PROMPT";
                TxtPromptName.Text = prompt.Name;
                TxtPromptText.Text = prompt.PromptText;
            }
        }

        private void BtnAddNew_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
            TxtPromptName.Focus();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            string name = TxtPromptName.Text.Trim();
            string text = TxtPromptText.Text.Trim();

            if (string.IsNullOrEmpty(name))
            {
                System.Windows.MessageBox.Show("Please enter a template name.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtPromptName.Focus();
                return;
            }

            if (string.IsNullOrEmpty(text))
            {
                System.Windows.MessageBox.Show("Please enter prompt text.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtPromptText.Focus();
                return;
            }

            if (_isEditing && _editingPrompt != null)
            {
                _editingPrompt.Name = name;
                _editingPrompt.PromptText = text;
                ExtraPromptManager.Update(_editingPrompt);
            }
            else
            {
                ExtraPromptManager.Add(new ExtraPrompt { Name = name, PromptText = text });
            }

            LoadPrompts();
            ClearForm();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.Tag is string id)
            {
                var result = System.Windows.MessageBox.Show("Are you sure you want to delete this prompt template?", "Confirm Delete",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    ExtraPromptManager.Delete(id);
                    LoadPrompts();
                    ClearForm();
                }
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount != 2) DragMove();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
