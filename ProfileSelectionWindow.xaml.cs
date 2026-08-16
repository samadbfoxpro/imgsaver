using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;
using WpfCursors = System.Windows.Input.Cursors;
using WpfMessageBox = System.Windows.MessageBox;

namespace imgsaver
{
    public partial class ProfileSelectionWindow : Window
    {
        public BrowserProfile? SelectedProfile { get; private set; }
        private List<BrowserProfile> _profiles = new();

        private string _selectedColor = "#3B82F6";
        private string _selectedIcon = "user";
        private BrowserProfile? _editingProfile = null;

        private readonly string[] _availableColors = new[]
        {
            "#3B82F6", // Blue
            "#10B981", // Emerald Green
            "#8B5CF6", // Purple
            "#F59E0B", // Amber / Orange
            "#EF4444", // Red
            "#14B8A6", // Teal
            "#6366F1", // Indigo
            "#EC4899", // Pink
            "#06B6D4", // Cyan
            "#64748B"  // Slate Grey
        };

        public ProfileSelectionWindow()
        {
            InitializeComponent();
            ChkAlwaysAsk.IsChecked = ProfileManager.AlwaysAskAccountOnStartup;

            Loaded += (s, e) => DwmHelper.UseImmersiveDarkMode(this);
            KeyDown += ProfileSelectionWindow_KeyDown;

            PopulateColorPicker();
            PopulateIconPicker();
            LoadProfilesList();
        }

        private void LoadProfilesList()
        {
            _profiles = ProfileManager.LoadProfiles();
            RenderProfileCards();
        }

        private void RenderProfileCards()
        {
            var toRemove = ProfilesWrapPanel.Children.OfType<Border>()
                .Where(b => b != BtnAddProfileCard).ToList();

            foreach (var card in toRemove)
            {
                ProfilesWrapPanel.Children.Remove(card);
            }

            foreach (var profile in _profiles)
            {
                var card = CreateProfileCard(profile, _profiles.Count > 1);
                var addIndex = ProfilesWrapPanel.Children.IndexOf(BtnAddProfileCard);
                ProfilesWrapPanel.Children.Insert(addIndex, card);
            }
        }

        private Border CreateProfileCard(BrowserProfile profile, bool canDelete)
        {
            var border = new Border
            {
                Width = 160,
                Height = 190,
                Margin = new Thickness(12),
                Background = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#2B2D30")),
                BorderBrush = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#36393E")),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(14),
                Cursor = WpfCursors.Hand,
                Tag = profile
            };

            // Hover effect
            border.MouseEnter += (s, e) =>
            {
                border.Background = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#35373C"));
                border.BorderBrush = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#3B82F6"));
            };
            border.MouseLeave += (s, e) =>
            {
                border.Background = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#2B2D30"));
                border.BorderBrush = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#36393E"));
            };
            border.MouseLeftButtonDown += (s, e) =>
            {
                LaunchWithProfile(profile);
            };

            var grid = new Grid();

            // Delete button on top-right if deletable
            if (canDelete)
            {
                var delBtn = new System.Windows.Controls.Button
                {
                    Style = (Style)FindResource("CardCloseBtnStyle"),
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                    VerticalAlignment = System.Windows.VerticalAlignment.Top,
                    Margin = new Thickness(0, 8, 8, 0),
                    ToolTip = "حذف این اکانت",
                    Tag = profile
                };
                delBtn.Click += (s, e) =>
                {
                    e.Handled = true;
                    var res = WpfMessageBox.Show($"آیا از حذف اکانت «{profile.Name}» اطمینان دارید؟", "حذف کاربر", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                    if (res == MessageBoxResult.Yes)
                    {
                        _profiles.Remove(profile);
                        ProfileManager.SaveProfiles(_profiles);
                        RenderProfileCards();
                    }
                };
                grid.Children.Add(delBtn);
            }

            var stack = new StackPanel
            {
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };

            // Avatar Circle
            MediaColor avatarColor;
            try { avatarColor = (MediaColor)MediaColorConverter.ConvertFromString(profile.ColorHex); }
            catch { avatarColor = MediaColor.FromRgb(59, 130, 246); }

            var avatarCircle = new Border
            {
                Width = 68,
                Height = 68,
                CornerRadius = new CornerRadius(34),
                Background = new SolidColorBrush(avatarColor),
                Margin = new Thickness(0, 0, 0, 12),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center
            };

            var vectorPath = new System.Windows.Shapes.Path
            {
                Data = ProfileVectorHelper.GetGeometry(profile.Icon),
                Fill = MediaBrushes.White,
                Width = 32,
                Height = 32,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center
            };

            avatarCircle.Child = vectorPath;
            stack.Children.Add(avatarCircle);

            // Name
            stack.Children.Add(new TextBlock
            {
                Text = profile.Name,
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Foreground = (MediaBrush)FindResource("TextPrimary"),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                MaxWidth = 130,
                TextTrimming = TextTrimming.CharacterEllipsis
            });

            grid.Children.Add(stack);
            border.Child = grid;
            return border;
        }

        private void LaunchWithProfile(BrowserProfile profile)
        {
            SelectedProfile = profile;
            ProfileManager.AlwaysAskAccountOnStartup = ChkAlwaysAsk.IsChecked == true;
            ProfileManager.SetActiveProfile(profile);
            DialogResult = true;
            Close();
        }

        private void BtnGuestMode_Click(object sender, RoutedEventArgs e)
        {
            var first = _profiles.FirstOrDefault() ?? ProfileManager.GetActiveProfile();
            LaunchWithProfile(first);
        }

        private void BtnAddProfileCard_Click(object sender, MouseButtonEventArgs e)
        {
            _editingProfile = null;
            TxtModalTitle.Text = "ساخت اکانت جدید";
            TxtModalProfileName.Text = $"Account {_profiles.Count + 1}";
            _selectedColor = _availableColors[_profiles.Count % _availableColors.Length];
            _selectedIcon = ProfileVectorHelper.AvailableIcons[_profiles.Count % ProfileVectorHelper.AvailableIcons.Length].Key;
            UpdateModalPreview();
            HighlightSelectedColor();
            HighlightSelectedIcon();
            AddProfileModal.Visibility = Visibility.Visible;
            TxtModalProfileName.Focus();
            TxtModalProfileName.SelectAll();
        }

        private void CloseModal_Click(object sender, RoutedEventArgs e)
        {
            AddProfileModal.Visibility = Visibility.Collapsed;
        }

        private void BtnConfirmCreateProfile_Click(object sender, RoutedEventArgs e)
        {
            var name = TxtModalProfileName.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                WpfMessageBox.Show("لطفاً یک نام برای اکانت وارد کنید.", "خطا", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_editingProfile != null)
            {
                _editingProfile.Name = name;
                _editingProfile.ColorHex = _selectedColor;
                _editingProfile.Icon = _selectedIcon;
                ProfileManager.SaveProfiles(_profiles);
                AddProfileModal.Visibility = Visibility.Collapsed;
                RenderProfileCards();
            }
            else
            {
                var newProfile = new BrowserProfile
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = name,
                    ColorHex = _selectedColor,
                    Icon = _selectedIcon,
                    LastUsed = DateTime.Now
                };
                _profiles.Add(newProfile);
                ProfileManager.SaveProfiles(_profiles);
                AddProfileModal.Visibility = Visibility.Collapsed;
                LaunchWithProfile(newProfile);
            }
        }

        private void TxtModalProfileName_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateModalPreview();
        }

        private void PopulateColorPicker()
        {
            ColorPickerPanel.Children.Clear();
            foreach (var hex in _availableColors)
            {
                var color = (MediaColor)MediaColorConverter.ConvertFromString(hex);
                var btn = new Border
                {
                    Width = 28,
                    Height = 28,
                    CornerRadius = new CornerRadius(14),
                    Background = new SolidColorBrush(color),
                    Margin = new Thickness(4),
                    Cursor = WpfCursors.Hand,
                    BorderThickness = new Thickness(2),
                    BorderBrush = MediaBrushes.Transparent,
                    Tag = hex
                };

                btn.MouseLeftButtonDown += (s, e) =>
                {
                    _selectedColor = (string)((Border)s).Tag;
                    HighlightSelectedColor();
                    UpdateModalPreview();
                };

                ColorPickerPanel.Children.Add(btn);
            }
            HighlightSelectedColor();
        }

        private void HighlightSelectedColor()
        {
            foreach (Border b in ColorPickerPanel.Children)
            {
                bool isSelected = (string)b.Tag == _selectedColor;
                b.BorderBrush = isSelected ? MediaBrushes.White : MediaBrushes.Transparent;
            }
        }

        private void PopulateIconPicker()
        {
            IconPickerPanel.Children.Clear();
            foreach (var item in ProfileVectorHelper.AvailableIcons)
            {
                var btn = new Border
                {
                    Width = 34,
                    Height = 34,
                    CornerRadius = new CornerRadius(8),
                    Background = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#1E1F22")),
                    Margin = new Thickness(3),
                    Cursor = WpfCursors.Hand,
                    BorderThickness = new Thickness(1.5),
                    BorderBrush = MediaBrushes.Transparent,
                    ToolTip = item.Name,
                    Tag = item.Key
                };

                var path = new System.Windows.Shapes.Path
                {
                    Data = Geometry.Parse(item.GeometryData),
                    Fill = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#949BA4")),
                    Width = 18,
                    Height = 18,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center
                };

                btn.Child = path;

                btn.MouseLeftButtonDown += (s, e) =>
                {
                    _selectedIcon = (string)((Border)s).Tag;
                    HighlightSelectedIcon();
                    UpdateModalPreview();
                };

                IconPickerPanel.Children.Add(btn);
            }
            HighlightSelectedIcon();
        }

        private void HighlightSelectedIcon()
        {
            foreach (Border b in IconPickerPanel.Children)
            {
                bool isSelected = (string)b.Tag == _selectedIcon;
                b.BorderBrush = isSelected ? (MediaBrush)FindResource("AccentColor") : MediaBrushes.Transparent;
                b.Background = isSelected
                    ? new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#35373C"))
                    : new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#1E1F22"));

                if (b.Child is System.Windows.Shapes.Path p)
                {
                    p.Fill = isSelected
                        ? MediaBrushes.White
                        : new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#949BA4"));
                }
            }
        }

        private void UpdateModalPreview()
        {
            if (AvatarPreviewCircle == null || AvatarPreviewPath == null) return;

            MediaColor avatarColor;
            try { avatarColor = (MediaColor)MediaColorConverter.ConvertFromString(_selectedColor); }
            catch { avatarColor = MediaColor.FromRgb(59, 130, 246); }

            AvatarPreviewCircle.Background = new SolidColorBrush(avatarColor);
            AvatarPreviewPath.Data = ProfileVectorHelper.GetGeometry(_selectedIcon);
        }

        private void ProfileSelectionWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                if (AddProfileModal.Visibility == Visibility.Visible)
                {
                    AddProfileModal.Visibility = Visibility.Collapsed;
                    return;
                }
                DialogResult = false;
                Close();
            }
            else if (e.Key >= Key.D1 && e.Key <= Key.D9)
            {
                int index = e.Key - Key.D1;
                if (index >= 0 && index < _profiles.Count)
                {
                    LaunchWithProfile(_profiles[index]);
                }
            }
        }
    }
}
