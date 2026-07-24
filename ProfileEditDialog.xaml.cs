using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;
using Brushes = System.Windows.Media.Brushes;
using Cursors = System.Windows.Input.Cursors;

namespace imgsaver
{
    public partial class ProfileEditDialog : Window
    {
        public string AccountName { get; private set; } = "";
        public string SelectedIcon { get; private set; } = "👤";
        public string SelectedColor { get; private set; } = "#2ECC71";

        private readonly List<string> _availableIcons = new() { "👤", "💼", "🚀", "⚡", "⭐", "🔥", "🎯", "👑", "💎" };
        private readonly List<string> _availableColors = new()
        {
            "#2ECC71", "#3498DB", "#9B59B6", "#E74C3C", "#F1C40F", "#1ABC9C", "#E67E22", "#E91E63", "#00BCD4"
        };

        public ProfileEditDialog(string initialName = "", string initialIcon = "👤", string initialColor = "#2ECC71", string title = "Add Account Profile")
        {
            InitializeComponent();
            TxtHeaderTitle.Text = title;
            TxtAccountName.Text = initialName;
            SelectedIcon = string.IsNullOrEmpty(initialIcon) ? "👤" : initialIcon;
            SelectedColor = string.IsNullOrEmpty(initialColor) ? "#2ECC71" : initialColor;

            PopulateIconPicker();
            PopulateColorPicker();

            this.Loaded += (s, e) =>
            {
                TxtAccountName.Focus();
                TxtAccountName.SelectAll();
            };

            this.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    DialogResult = false;
                    Close();
                }
                else if (e.Key == Key.Enter)
                {
                    SaveAndClose();
                }
            };
        }

        private void PopulateIconPicker()
        {
            IconsHost.Items.Clear();
            foreach (var icon in _availableIcons)
            {
                var border = new Border
                {
                    Width = 34,
                    Height = 34,
                    CornerRadius = new CornerRadius(6),
                    Margin = new Thickness(0, 0, 8, 6),
                    Cursor = Cursors.Hand,
                    Tag = icon
                };

                var textBlock = new TextBlock
                {
                    Text = icon,
                    FontSize = 16,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center,
                    Margin = new Thickness(0, -2, 0, 0)
                };

                border.Child = textBlock;
                UpdateIconButtonState(border, icon == SelectedIcon);

                border.MouseDown += (s, e) =>
                {
                    SelectedIcon = icon;
                    TxtHeaderIcon.Text = icon;
                    foreach (Border item in IconsHost.Items)
                    {
                        UpdateIconButtonState(item, (string)item.Tag == SelectedIcon);
                    }
                };

                IconsHost.Items.Add(border);
            }
        }

        private void UpdateIconButtonState(Border border, bool isSelected)
        {
            border.Background = isSelected ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2ECC71")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#252525"));
            border.BorderBrush = isSelected ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2ECC71")) : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#444444"));
            border.BorderThickness = new Thickness(isSelected ? 1.5 : 1);

            if (border.Child is TextBlock tb)
            {
                tb.Foreground = isSelected ? Brushes.Black : Brushes.White;
            }
        }

        private void PopulateColorPicker()
        {
            ColorsHost.Items.Clear();
            foreach (var hex in _availableColors)
            {
                var border = new Border
                {
                    Width = 26,
                    Height = 26,
                    CornerRadius = new CornerRadius(13),
                    Margin = new Thickness(0, 0, 6, 6),
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)),
                    Cursor = Cursors.Hand,
                    Tag = hex
                };

                UpdateColorButtonState(border, hex == SelectedColor);

                border.MouseDown += (s, e) =>
                {
                    SelectedColor = hex;
                    foreach (Border item in ColorsHost.Items)
                    {
                        UpdateColorButtonState(item, (string)item.Tag == SelectedColor);
                    }
                };

                ColorsHost.Items.Add(border);
            }
        }

        private void UpdateColorButtonState(Border border, bool isSelected)
        {
            border.BorderBrush = isSelected ? Brushes.White : Brushes.Transparent;
            border.BorderThickness = new Thickness(isSelected ? 2.5 : 0);
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            SaveAndClose();
        }

        private void SaveAndClose()
        {
            string name = TxtAccountName.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                DarkConfirmDialog.ShowMessage("Validation", "Please enter an account name.", this);
                return;
            }

            AccountName = name;
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
