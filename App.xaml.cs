using System.Windows;
using System.Windows.Controls;
using System.Threading;

namespace imgsaver
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Register a global handler for all TextBoxes to select all text on double-click
            // Using fully qualified names to resolve ambiguity with System.Windows.Forms
            EventManager.RegisterClassHandler(typeof(System.Windows.Controls.TextBox),
                System.Windows.Controls.Control.PreviewMouseDoubleClickEvent,
                new RoutedEventHandler(TextBox_PreviewMouseDoubleClick));
        }

        private void TextBox_PreviewMouseDoubleClick(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                textBox.SelectAll();
                e.Handled = true; // Prevent the default word-selection behavior
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);
        }
    }
}