using System.Windows;
using System.Windows.Controls;

namespace imgsaver
{
    public partial class TutorialWindow : Window
    {
        public TutorialWindow()
        {
            InitializeComponent();
            // Start with overview
            UpdateContent(0);
        }

        private void LstNav_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstNav != null)
            {
                UpdateContent(LstNav.SelectedIndex);
            }
        }

        private void UpdateContent(int index)
        {
            if (ContentArea == null) return;

            string templateKey = index switch
            {
                0 => "OverviewTemplate",
                1 => "ManualSavingTemplate",
                2 => "ShortcutsTemplate",
                3 => "MiniClipboardTemplate",
                4 => "QuickSaveTemplate",
                5 => "GalleryTemplate",
                6 => "PersonaInjectorTemplate",
                _ => "OverviewTemplate"
            };

            var template = FindResource(templateKey) as DataTemplate;
            ContentArea.ContentTemplate = template;
            
            // Reset scroll position
            if (ScollContent != null) ScollContent.ScrollToHome();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }
    }
}
