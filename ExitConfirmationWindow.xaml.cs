using System.Windows;

namespace imgsaver
{
    public partial class ExitConfirmationWindow : Window
    {
        public ExitConfirmationWindow()
        {
            InitializeComponent();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnConfirmExit_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
