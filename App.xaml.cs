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

            string lang = LanguageManager.LoadLanguageFromConfig();
            LanguageManager.ApplyLanguage(lang);

            // Register a global handler for all TextBoxes to select one word on double-click (WPF default)
            // and select all text on triple-click.
            EventManager.RegisterClassHandler(typeof(System.Windows.Controls.TextBox),
                System.Windows.Controls.Control.PreviewMouseLeftButtonDownEvent,
                new System.Windows.Input.MouseButtonEventHandler(TextBox_PreviewMouseLeftButtonDown));

            BrowserRecordingFloatingWindowManager.SyncWithSettings(BrowserSettings.Load());
        }

        private void TextBox_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                if (e.ClickCount == 3)
                {
                    textBox.SelectAll();
                    e.Handled = true;
                }
            }
        }
    }
}
