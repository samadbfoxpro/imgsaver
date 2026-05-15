using System.Windows;
using System.Windows.Input;

namespace imgsaver
{
    public partial class PromptEditorWindow : Window
    {
        private BasePrompt? _editingPrompt = null;
        private bool _isEditing = false;

        public PromptEditorWindow(string? promptId = null, string? initialPromptText = null)
        {
            InitializeComponent();
            LoadPrompts();

            if (promptId != null)
            {
                var prompt = BasePromptManager.GetAll().Find(p => p.Id == promptId);
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
            PromptList.ItemsSource = BasePromptManager.GetAll();
        }

        private void ClearForm()
        {
            _editingPrompt = null;
            _isEditing = false;
            TxtFormTitle.Text = "ADD NEW PROMPT TEMPLATE";
            TxtPromptName.Text = "";
            TxtPromptText.Text = "";
            PromptList.SelectedItem = null;
        }

        private void PromptList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (PromptList.SelectedItem is BasePrompt prompt)
            {
                _editingPrompt = prompt;
                _isEditing = true;
                TxtFormTitle.Text = "EDIT PROMPT TEMPLATE";
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
                BasePromptManager.Update(_editingPrompt);
            }
            else
            {
                BasePromptManager.Add(new BasePrompt { Name = name, PromptText = text });
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
                    BasePromptManager.Delete(id);
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
