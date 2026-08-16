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
            StartupProfiler.Log("App.OnStartup ENTER");
            base.OnStartup(e);

            StartupProfiler.Log("App.OnStartup -> Loading Language Config");
            string lang = LanguageManager.LoadLanguageFromConfig();
            StartupProfiler.Log($"App.OnStartup -> Applying Language ({lang})");
            LanguageManager.ApplyLanguage(lang);

            // Register a global handler for all TextBoxes to select one word on double-click (WPF default)
            // and select all text on triple-click.
            EventManager.RegisterClassHandler(typeof(System.Windows.Controls.TextBox),
                System.Windows.Controls.Control.PreviewMouseLeftButtonDownEvent,
                new System.Windows.Input.MouseButtonEventHandler(TextBox_PreviewMouseLeftButtonDown));

            // Prevent WPF from automatically terminating when the AuthLockWindow closes
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // Master Password Authentication Gate
            if (SecurityManager.ShouldPromptPasswordOnStartup())
            {
                var authWindow = new AuthLockWindow(isRuntimeLock: false);
                bool? authenticated = authWindow.ShowDialog();

                if (authenticated != true)
                {
                    Shutdown();
                    return;
                }
            }

            // Authentication succeeded: create and assign MainWindow
            var mainWindow = new MainWindow();
            MainWindow = mainWindow;
            
            // Re-enable normal window closure shutdown mode
            ShutdownMode = ShutdownMode.OnMainWindowClose;
            
            mainWindow.Opacity = 0.0;
            mainWindow.Show();

            var fadeIn = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 0.0,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(280),
                EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };
            mainWindow.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            // Defer heavy I/O (browser settings JSON read + floating window init) to AFTER the UI is visible
            Dispatcher.BeginInvoke(new Action(() =>
            {
                StartupProfiler.Log("App.OnStartup Background Task -> SyncWithSettings START");
                BrowserRecordingFloatingWindowManager.SyncWithSettings(BrowserSettings.Load());
                StartupProfiler.Log("App.OnStartup Background Task -> SyncWithSettings END");
            }), System.Windows.Threading.DispatcherPriority.Background);

            StartupProfiler.Log("App.OnStartup EXIT");
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
        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                // Close all open windows to fire their Closed events and unhook Win32 event listeners
                foreach (Window win in System.Windows.Application.Current.Windows)
                {
                    try
                    {
                        if (win != null && win.IsLoaded)
                        {
                            win.Close();
                        }
                    }
                    catch { }
                }
            }
            catch { }

            base.OnExit(e);

            // Force kill the process so no zombie threads or unmanaged User32 hooks linger in Windows
            System.Environment.Exit(0);
        }
    }
}
