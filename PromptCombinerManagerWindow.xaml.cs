using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;
using Orientation = System.Windows.Controls.Orientation;
using HorizontalAlignment = System.Windows.HorizontalAlignment;
using VerticalAlignment = System.Windows.VerticalAlignment;
using MessageBox = System.Windows.MessageBox;

namespace imgsaver
{
    public partial class PromptCombinerManagerWindow : Window
    {
        private PromptCombinerData _data;
        private ObservableCollection<PromptCombinerFolder> _folders = new ObservableCollection<PromptCombinerFolder>();
        private ObservableCollection<PromptCombinerItem> _currentFolderItems = new ObservableCollection<PromptCombinerItem>();

        public PromptCombinerManagerWindow()
        {
            InitializeComponent();
            _data = PromptCombinerStore.Load();
            LoadDataToUI();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 1) DragMove();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private bool _isUpdatingFolderUI = false;

        private void LoadDataToUI()
        {
            _folders.Clear();
            foreach (var f in _data.Folders.OrderBy(f => f.Order))
            {
                _folders.Add(f);
            }
            LstFolders.ItemsSource = _folders;

            if (_folders.Count > 0)
            {
                var selected = _folders.FirstOrDefault(f => f.Id == _data.ActiveFolderId) ?? _folders[0];
                LstFolders.SelectedItem = selected;
            }

            // Placement Rules
            if (_data.PlacementMode == CombinerPlacementMode.AtBeginning)
                RadAtStart.IsChecked = true;
            else if (_data.PlacementMode == CombinerPlacementMode.AtEnd)
                RadAtEnd.IsChecked = true;
            else if (_data.PlacementMode == CombinerPlacementMode.PerFolder)
                RadPerFolder.IsChecked = true;
            else
                RadAfterComma.IsChecked = true;

            TxtCommaIndex.Text = _data.CommaIndex > 0 ? _data.CommaIndex.ToString() : "1";
            UpdateFolderRuleVisibility();
        }

        private void UpdateFolderRuleVisibility()
        {
            bool isPerFolder = RadPerFolder != null && RadPerFolder.IsChecked == true;
            if (FolderRulePanel != null)
            {
                FolderRulePanel.Visibility = isPerFolder ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void RadPlacementMode_Changed(object sender, RoutedEventArgs e)
        {
            UpdateFolderRuleVisibility();
        }

        private void TxtCommaIndex_TextChanged(object sender, TextChangedEventArgs e)
        {
        }

        private void LstFolders_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstFolders.SelectedItem is PromptCombinerFolder folder)
            {
                TxtCurrentFolderHeader.Text = $"Prompt Buttons in '{folder.Name}'";
                RefreshFolderItems(folder.Id);
                LoadFolderPlacementRules(folder);
            }
            else
            {
                _currentFolderItems.Clear();
                TxtCurrentFolderHeader.Text = "Select a Category";
            }
        }

        private void LoadFolderPlacementRules(PromptCombinerFolder folder)
        {
            if (folder == null || CboFolderPlacement == null || TxtFolderCommaIndex == null) return;
            _isUpdatingFolderUI = true;
            try
            {
                if (folder.PlacementMode == CombinerPlacementMode.AtBeginning)
                    CboFolderPlacement.SelectedIndex = 1;
                else if (folder.PlacementMode == CombinerPlacementMode.AtEnd)
                    CboFolderPlacement.SelectedIndex = 2;
                else
                    CboFolderPlacement.SelectedIndex = 0;

                TxtFolderCommaIndex.Text = folder.CommaIndex > 0 ? folder.CommaIndex.ToString() : "1";
                if (PnlFolderCommaIndex != null)
                {
                    PnlFolderCommaIndex.Visibility = (CboFolderPlacement.SelectedIndex == 0) ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            finally
            {
                _isUpdatingFolderUI = false;
            }
        }

        private void CboFolderPlacement_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingFolderUI || CboFolderPlacement == null) return;
            if (LstFolders != null && LstFolders.SelectedItem is PromptCombinerFolder folder)
            {
                int idx = CboFolderPlacement.SelectedIndex;
                if (idx == 1) folder.PlacementMode = CombinerPlacementMode.AtBeginning;
                else if (idx == 2) folder.PlacementMode = CombinerPlacementMode.AtEnd;
                else folder.PlacementMode = CombinerPlacementMode.AfterComma;

                if (PnlFolderCommaIndex != null)
                {
                    PnlFolderCommaIndex.Visibility = (idx == 0) ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        private void TxtFolderCommaIndex_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdatingFolderUI || TxtFolderCommaIndex == null) return;
            if (LstFolders != null && LstFolders.SelectedItem is PromptCombinerFolder folder)
            {
                if (int.TryParse(TxtFolderCommaIndex.Text, out int cIdx) && cIdx > 0)
                {
                    folder.CommaIndex = cIdx;
                }
            }
        }

        private void RefreshFolderItems(string folderId)
        {
            _currentFolderItems.Clear();
            var items = _data.Items.Where(i => i.FolderId == folderId).OrderBy(i => i.Order).ToList();
            foreach (var item in items)
            {
                _currentFolderItems.Add(item);
            }
            LstItems.ItemsSource = _currentFolderItems;
        }

        private void BtnAddFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new InputDialogWindow("New Category", "Enter category name:");
            dialog.Owner = this;
            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.InputText))
            {
                var folder = new PromptCombinerFolder
                {
                    Name = dialog.InputText.Trim(),
                    Order = _folders.Count
                };
                _data.Folders.Add(folder);
                _folders.Add(folder);
                LstFolders.SelectedItem = folder;
            }
        }

        private void BtnDeleteFolder_Click(object sender, RoutedEventArgs e)
        {
            if (LstFolders.SelectedItem is PromptCombinerFolder folder)
            {
                if (_folders.Count <= 1)
                {
                    MessageBox.Show("You must keep at least one category.", "Notice", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                if (MessageBox.Show($"Are you sure you want to delete category '{folder.Name}' and all its buttons?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    _data.Items.RemoveAll(i => i.FolderId == folder.Id);
                    _data.Folders.Remove(folder);
                    _folders.Remove(folder);
                    if (_folders.Count > 0) LstFolders.SelectedIndex = 0;
                }
            }
        }

        private void BtnAddItem_Click(object sender, RoutedEventArgs e)
        {
            if (LstFolders.SelectedItem is PromptCombinerFolder folder)
            {
                var dialog = new SnippetEditDialogWindow("Add Prompt Snippet Button", "", "");
                dialog.Owner = this;
                if (dialog.ShowDialog() == true)
                {
                    var item = new PromptCombinerItem
                    {
                        FolderId = folder.Id,
                        Title = dialog.ItemTitle,
                        Text = dialog.ItemText,
                        Order = _currentFolderItems.Count
                    };
                    _data.Items.Add(item);
                    _currentFolderItems.Add(item);
                }
            }
        }

        private void BtnEditItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement el && el.Tag is PromptCombinerItem item)
            {
                var dialog = new SnippetEditDialogWindow("Edit Prompt Snippet Button", item.Title, item.Text);
                dialog.Owner = this;
                if (dialog.ShowDialog() == true)
                {
                    item.Title = dialog.ItemTitle;
                    item.Text = dialog.ItemText;
                    RefreshFolderItems(item.FolderId);
                }
            }
        }

        private void BtnDeleteItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement el && el.Tag is PromptCombinerItem item)
            {
                _data.Items.Remove(item);
                _currentFolderItems.Remove(item);
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (RadAtStart.IsChecked == true)
                _data.PlacementMode = CombinerPlacementMode.AtBeginning;
            else if (RadAtEnd.IsChecked == true)
                _data.PlacementMode = CombinerPlacementMode.AtEnd;
            else if (RadPerFolder.IsChecked == true)
                _data.PlacementMode = CombinerPlacementMode.PerFolder;
            else
                _data.PlacementMode = CombinerPlacementMode.AfterComma;

            if (int.TryParse(TxtCommaIndex.Text, out int commaIdx) && commaIdx > 0)
                _data.CommaIndex = commaIdx;
            else
                _data.CommaIndex = 1;

            if (LstFolders.SelectedItem is PromptCombinerFolder selFolder)
            {
                _data.ActiveFolderId = selFolder.Id;
            }

            PromptCombinerStore.Save(_data);
            DialogResult = true;
            Close();
        }
    }

    // Helper dialog for simple single line text input
    public class InputDialogWindow : Window
    {
        public string InputText { get; private set; } = "";
        private System.Windows.Controls.TextBox _txtInput;

        public InputDialogWindow(string title, string promptText)
        {
            Title = title; Width = 380; Height = 170; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = System.Windows.Media.Brushes.Transparent;

            var border = new Border
            {
                Background = GetResBrush("BackgroundBrush", "#1E1E1E"),
                BorderBrush = GetResBrush("BorderBrush", "#3E3E42"),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(14)
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var lbl = new TextBlock { Text = promptText, FontSize = 12, FontWeight = FontWeights.Bold, Foreground = GetResBrush("ForegroundBrush", "#FFFFFF"), Margin = new Thickness(0,0,0,8) };
            Grid.SetRow(lbl, 0); grid.Children.Add(lbl);

            _txtInput = new System.Windows.Controls.TextBox { Height = 30, FontSize = 12, Background = GetResBrush("InputBrush", "#2D2D30"), Foreground = GetResBrush("ForegroundBrush", "#FFFFFF"), BorderBrush = GetResBrush("BorderBrush", "#3E3E42"), Padding = new Thickness(6,4,6,4) };
            Grid.SetRow(_txtInput, 1); grid.Children.Add(_txtInput);

            var sp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0,12,0,0) };
            var btnOk = new Button { Content = "OK", Style = GetResStyle("PrimaryButtonStyle"), Height = 28, Padding = new Thickness(14,0,14,0), Margin = new Thickness(0,0,6,0) };
            btnOk.Click += (s, e) => { InputText = _txtInput.Text; DialogResult = true; Close(); };
            var btnCancel = new Button { Content = "Cancel", Style = GetResStyle("SecondaryButtonStyle"), Height = 28, Padding = new Thickness(14,0,14,0) };
            btnCancel.Click += (s, e) => { DialogResult = false; Close(); };
            sp.Children.Add(btnOk); sp.Children.Add(btnCancel);
            Grid.SetRow(sp, 2); grid.Children.Add(sp);

            border.Child = grid; Content = border;
        }

        private static System.Windows.Media.Brush GetResBrush(string key, string fallbackHex)
        {
            try { var r = Application.Current?.TryFindResource(key); if (r is System.Windows.Media.Brush b) return b; } catch { }
            return new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(fallbackHex));
        }

        private static Style GetResStyle(string key)
        {
            try { var r = Application.Current?.TryFindResource(key); if (r is Style s) return s; } catch { }
            return null;
        }
    }

    // Helper dialog for editing prompt button (Title & Snippet Text)
    public class SnippetEditDialogWindow : Window
    {
        public string ItemTitle { get; private set; } = "";
        public string ItemText { get; private set; } = "";
        private System.Windows.Controls.TextBox _txtTitle;
        private System.Windows.Controls.TextBox _txtText;

        public SnippetEditDialogWindow(string windowTitle, string initialTitle, string initialText)
        {
            Title = windowTitle; Width = 440; Height = 250; WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None; AllowsTransparency = true; Background = System.Windows.Media.Brushes.Transparent;

            var border = new Border
            {
                Background = GetResBrush("BackgroundBrush", "#1E1E1E"),
                BorderBrush = GetResBrush("BorderBrush", "#3E3E42"),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(14)
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var lbl1 = new TextBlock { Text = "Button Title / Label (e.g., 'Masterpiece 🌟'):", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = GetResBrush("ForegroundBrush", "#FFFFFF"), Margin = new Thickness(0,0,0,4) };
            Grid.SetRow(lbl1, 0); grid.Children.Add(lbl1);

            _txtTitle = new System.Windows.Controls.TextBox { Text = initialTitle, Height = 28, FontSize = 12, Background = GetResBrush("InputBrush", "#2D2D30"), Foreground = GetResBrush("ForegroundBrush", "#FFFFFF"), BorderBrush = GetResBrush("BorderBrush", "#3E3E42"), Padding = new Thickness(6,2,6,2), Margin = new Thickness(0,0,0,10) };
            Grid.SetRow(_txtTitle, 1); grid.Children.Add(_txtTitle);

            var lbl2 = new TextBlock { Text = "Prompt Snippet Text (e.g., 'masterpiece, best quality, 8k'):", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = GetResBrush("ForegroundBrush", "#FFFFFF"), Margin = new Thickness(0,0,0,4) };
            Grid.SetRow(lbl2, 2); grid.Children.Add(lbl2);

            _txtText = new System.Windows.Controls.TextBox { Text = initialText, Height = 56, FontSize = 12, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, Background = GetResBrush("InputBrush", "#2D2D30"), Foreground = GetResBrush("ForegroundBrush", "#FFFFFF"), BorderBrush = GetResBrush("BorderBrush", "#3E3E42"), Padding = new Thickness(6,4,6,4) };
            Grid.SetRow(_txtText, 3); grid.Children.Add(_txtText);

            var sp = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0,12,0,0) };
            var btnOk = new Button { Content = "Save", Style = GetResStyle("PrimaryButtonStyle"), Height = 28, Padding = new Thickness(16,0,16,0), Margin = new Thickness(0,0,6,0) };
            btnOk.Click += (s, e) =>
            {
                ItemTitle = _txtTitle.Text.Trim();
                ItemText = _txtText.Text.Trim();
                if (string.IsNullOrWhiteSpace(ItemTitle)) ItemTitle = "Snippet";
                DialogResult = true; Close();
            };
            var btnCancel = new Button { Content = "Cancel", Style = GetResStyle("SecondaryButtonStyle"), Height = 28, Padding = new Thickness(16,0,16,0) };
            btnCancel.Click += (s, e) => { DialogResult = false; Close(); };
            sp.Children.Add(btnOk); sp.Children.Add(btnCancel);
            Grid.SetRow(sp, 4); grid.Children.Add(sp);

            border.Child = grid; Content = border;
        }

        private static System.Windows.Media.Brush GetResBrush(string key, string fallbackHex)
        {
            try { var r = Application.Current?.TryFindResource(key); if (r is System.Windows.Media.Brush b) return b; } catch { }
            return new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(fallbackHex));
        }

        private static Style GetResStyle(string key)
        {
            try { var r = Application.Current?.TryFindResource(key); if (r is Style s) return s; } catch { }
            return null;
        }
    }
}
