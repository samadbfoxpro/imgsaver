using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Brushes = System.Windows.Media.Brushes;
using Cursors = System.Windows.Input.Cursors;

namespace imgsaver
{
    public partial class DarkConfirmDialog : Window
    {
        public DarkConfirmDialog(string title, string message, string confirmText = "Confirm", string cancelText = "Cancel", bool isWarning = false)
        {
            InitializeComponent();
            TxtTitle.Text = title;
            TxtMessage.Text = message;
            BtnConfirm.Content = confirmText;
            BtnCancel.Content = cancelText;

            if (isWarning)
            {
                TxtHeaderIcon.Text = "🗑️";
                BtnConfirm.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8B0000"));
                BtnConfirm.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E81123"));
            }
            else
            {
                TxtHeaderIcon.Text = "ℹ️";
                BtnCancel.Visibility = string.IsNullOrEmpty(cancelText) ? Visibility.Collapsed : Visibility.Visible;
            }

            this.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    DialogResult = false;
                    Close();
                }
                else if (e.Key == Key.Enter)
                {
                    DialogResult = true;
                    Close();
                }
            };
        }

        public static bool ShowConfirm(string title, string message, Window? owner = null, bool isWarning = true, string confirmText = "Delete", string cancelText = "Cancel")
        {
            var dlg = new DarkConfirmDialog(title, message, confirmText, cancelText, isWarning);
            if (owner != null) dlg.Owner = owner;
            return dlg.ShowDialog() == true;
        }

        public static void ShowMessage(string title, string message, Window? owner = null)
        {
            var dlg = new DarkConfirmDialog(title, message, "OK", "", false);
            if (owner != null) dlg.Owner = owner;
            dlg.ShowDialog();
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
