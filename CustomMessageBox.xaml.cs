using System.Windows;

namespace imgsaver
{
    public partial class CustomMessageBox : Window
    {
        public CustomMessageBox(string message, string title, MessageBoxButton buttons, MessageBoxImage icon)
        {
            InitializeComponent();
            TxtMessage.Text = message;
            TxtTitle.Text = title;
            SetupButtons(buttons);
            SetupIcon(icon);
        }

        private void SetupButtons(MessageBoxButton buttons)
        {
            BtnYes.Visibility = Visibility.Collapsed;
            BtnNo.Visibility = Visibility.Collapsed;
            BtnOK.Visibility = Visibility.Collapsed;
            BtnCancel.Visibility = Visibility.Collapsed;

            switch (buttons)
            {
                case MessageBoxButton.OK:
                    BtnOK.Visibility = Visibility.Visible;
                    BtnOK.IsDefault = true;
                    break;
                case MessageBoxButton.OKCancel:
                    BtnOK.Visibility = Visibility.Visible;
                    BtnCancel.Visibility = Visibility.Visible;
                    BtnOK.IsDefault = true;
                    BtnCancel.IsCancel = true;
                    break;
                case MessageBoxButton.YesNo:
                    BtnYes.Visibility = Visibility.Visible;
                    BtnNo.Visibility = Visibility.Visible;
                    BtnYes.IsDefault = true;
                    BtnNo.IsCancel = true;
                    break;
                case MessageBoxButton.YesNoCancel:
                    BtnYes.Visibility = Visibility.Visible;
                    BtnNo.Visibility = Visibility.Visible;
                    BtnCancel.Visibility = Visibility.Visible;
                    BtnYes.IsDefault = true;
                    BtnCancel.IsCancel = true;
                    break;
            }
        }

        private void SetupIcon(MessageBoxImage icon)
        {
            switch (icon)
            {
                case MessageBoxImage.Error: // Hand, Stop
                    TxtIcon.Text = "❌";
                    TxtIcon.Foreground = (System.Windows.Media.Brush)FindResource("ErrorBrush");
                    break;
                case MessageBoxImage.Question:
                    TxtIcon.Text = "❓";
                    TxtIcon.Foreground = (System.Windows.Media.Brush)FindResource("AccentBrush");
                    break;
                case MessageBoxImage.Warning: // Exclamation
                    TxtIcon.Text = "⚠️";
                    TxtIcon.Foreground = (System.Windows.Media.Brush)FindResource("WarningBrush");
                    break;
                case MessageBoxImage.Information: // Asterisk
                    TxtIcon.Text = "ℹ️";
                    TxtIcon.Foreground = (System.Windows.Media.Brush)FindResource("AccentBrush");
                    break;
                case MessageBoxImage.None:
                    TxtIcon.Visibility = Visibility.Collapsed;
                    break;
            }
        }

        private void BtnYes_Click(object sender, RoutedEventArgs e) { DialogResult = true; Close(); }
        private void BtnNo_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
        private void BtnOK_Click(object sender, RoutedEventArgs e) { DialogResult = true; Close(); }
        private void BtnCancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }

        public static MessageBoxResult Show(string message, string title = "Message", MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None)
        {
            var msgBox = new CustomMessageBox(message, title, buttons, icon);
            var result = msgBox.ShowDialog();

            if (buttons == MessageBoxButton.OK || buttons == MessageBoxButton.OKCancel)
                return result == true ? MessageBoxResult.OK : MessageBoxResult.Cancel;
            
            return result == true ? MessageBoxResult.Yes : MessageBoxResult.No;
        }

        public static MessageBoxResult Show(string message)
        {
            return Show(message, "Message", MessageBoxButton.OK, MessageBoxImage.None);
        }
    }
}
