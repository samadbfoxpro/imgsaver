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
        private string? _selectedCustomImage = null;
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

        private string _currentViewMode = "Grid";
        private System.Windows.Point _dragStartPoint;
        private BrowserProfile? _draggedProfile = null;
        private bool _isDragging = false;

        public ProfileSelectionWindow()
        {
            InitializeComponent();
            ChkAlwaysAsk.IsChecked = ProfileManager.AlwaysAskAccountOnStartup;

            _currentViewMode = ProfileManager.ProfileViewMode;

            Loaded += (s, e) => DwmHelper.UseImmersiveDarkMode(this);
            KeyDown += ProfileSelectionWindow_KeyDown;

            PopulateColorPicker();
            PopulateIconPicker();
            ApplyViewModeUI();
            LoadProfilesList();
        }

        private void BtnViewGrid_Click(object sender, RoutedEventArgs e)
        {
            _currentViewMode = "Grid";
            ProfileManager.ProfileViewMode = _currentViewMode;
            ApplyViewModeUI();
            RenderProfileCards();
        }

        private void BtnViewList_Click(object sender, RoutedEventArgs e)
        {
            _currentViewMode = "List";
            ProfileManager.ProfileViewMode = _currentViewMode;
            ApplyViewModeUI();
            RenderProfileCards();
        }

        private void ApplyViewModeUI()
        {
            if (_currentViewMode == "List")
            {
                ProfilesWrapPanel.Visibility = Visibility.Collapsed;
                ProfilesListWrapPanel.Visibility = Visibility.Visible;

                BtnViewList.Background = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#3B82F6"));
                if (BtnViewList.Template.FindName("bd", BtnViewList) is Border bdList && bdList.Child is TextBlock tbList)
                    tbList.Foreground = MediaBrushes.White;

                BtnViewGrid.Background = MediaBrushes.Transparent;
                if (BtnViewGrid.Template.FindName("bd", BtnViewGrid) is Border bdGrid && bdGrid.Child is TextBlock tbGrid)
                    tbGrid.Foreground = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#949BA4"));
            }
            else
            {
                ProfilesWrapPanel.Visibility = Visibility.Visible;
                ProfilesListWrapPanel.Visibility = Visibility.Collapsed;

                BtnViewGrid.Background = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#3B82F6"));
                if (BtnViewGrid.Template.FindName("bd", BtnViewGrid) is Border bdGrid && bdGrid.Child is TextBlock tbGrid)
                    tbGrid.Foreground = MediaBrushes.White;

                BtnViewList.Background = MediaBrushes.Transparent;
                if (BtnViewList.Template.FindName("bd", BtnViewList) is Border bdList && bdList.Child is TextBlock tbList)
                    tbList.Foreground = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#949BA4"));
            }
        }

        private void LoadProfilesList()
        {
            _profiles = ProfileManager.LoadProfiles();
            RenderProfileCards();
        }

        private void RenderProfileCards()
        {
            if (_currentViewMode == "List")
            {
                var toRemove = ProfilesListWrapPanel.Children.OfType<Border>()
                    .Where(b => b != BtnAddProfileListItem).ToList();

                foreach (var item in toRemove)
                {
                    ProfilesListWrapPanel.Children.Remove(item);
                }

                foreach (var profile in _profiles)
                {
                    var item = CreateProfileListItem(profile, _profiles.Count > 1);
                    var addIndex = ProfilesListWrapPanel.Children.IndexOf(BtnAddProfileListItem);
                    ProfilesListWrapPanel.Children.Insert(addIndex, item);
                }
            }
            else
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
        }

        private void AttachDragDropEvents(Border element, BrowserProfile profile)
        {
            element.AllowDrop = true;

            element.PreviewMouseLeftButtonDown += (s, e) =>
            {
                // If clicked on button or child controls, don't initiate drag
                if (e.OriginalSource is DependencyObject dep)
                {
                    var btn = FindVisualParent<System.Windows.Controls.Button>(dep);
                    if (btn != null) return;
                }
                _dragStartPoint = e.GetPosition(this);
                _draggedProfile = profile;
                _isDragging = false;
            };

            element.PreviewMouseMove += (s, e) =>
            {
                if (e.LeftButton == MouseButtonState.Pressed && _draggedProfile != null && !_isDragging)
                {
                    System.Windows.Point currentPosition = e.GetPosition(this);
                    Vector diff = _dragStartPoint - currentPosition;
                    if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                        Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                    {
                        _isDragging = true;
                        var data = new System.Windows.DataObject("BrowserProfileId", _draggedProfile.Id);
                        DragDrop.DoDragDrop(element, data, System.Windows.DragDropEffects.Move);
                        _isDragging = false;
                        _draggedProfile = null;
                    }
                }
            };

            element.DragOver += (s, e) =>
            {
                if (e.Data.GetDataPresent("BrowserProfileId"))
                {
                    e.Effects = System.Windows.DragDropEffects.Move;
                    element.BorderBrush = (MediaBrush)FindResource("AccentColor");
                }
                else
                {
                    e.Effects = System.Windows.DragDropEffects.None;
                }
                e.Handled = true;
            };

            element.DragLeave += (s, e) =>
            {
                element.BorderBrush = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#36393E"));
            };

            element.Drop += (s, e) =>
            {
                element.BorderBrush = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#36393E"));
                if (e.Data.GetDataPresent("BrowserProfileId"))
                {
                    string sourceId = (string)e.Data.GetData("BrowserProfileId");
                    var sourceProfile = _profiles.FirstOrDefault(p => p.Id == sourceId);
                    var targetProfile = profile;

                    if (sourceProfile != null && targetProfile != null && sourceProfile != targetProfile)
                    {
                        int oldIndex = _profiles.IndexOf(sourceProfile);
                        int newIndex = _profiles.IndexOf(targetProfile);
                        if (oldIndex >= 0 && newIndex >= 0)
                        {
                            _profiles.RemoveAt(oldIndex);
                            _profiles.Insert(newIndex, sourceProfile);
                            ProfileManager.SaveProfiles(_profiles);
                            RenderProfileCards();
                        }
                    }
                }
                e.Handled = true;
            };
        }

        private static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parentObj = VisualTreeHelper.GetParent(child);
            if (parentObj == null) return null;
            if (parentObj is T parent) return parent;
            return FindVisualParent<T>(parentObj);
        }

        private Border CreateProfileListItem(BrowserProfile profile, bool canDelete)
        {
            var border = new Border
            {
                Width = 320,
                Height = 62,
                Margin = new Thickness(8),
                Background = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#2B2D30")),
                BorderBrush = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString("#36393E")),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(12),
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
                if (!_isDragging) LaunchWithProfile(profile);
            };

            AttachDragDropEvents(border, profile);

            var grid = new Grid { Margin = new Thickness(12, 0, 8, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Avatar
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // Name & Info
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto }); // Actions

            // Avatar Circle
            MediaColor avatarColor;
            try { avatarColor = (MediaColor)MediaColorConverter.ConvertFromString(profile.ColorHex); }
            catch { avatarColor = MediaColor.FromRgb(59, 130, 246); }

            var avatarCircle = new Border
            {
                Width = 40,
                Height = 40,
                CornerRadius = new CornerRadius(20),
                Background = new SolidColorBrush(avatarColor),
                Margin = new Thickness(0, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center,
                ClipToBounds = true
            };

            bool hasImg = !string.IsNullOrWhiteSpace(profile.CustomImagePath) && System.IO.File.Exists(profile.CustomImagePath);
            if (hasImg)
            {
                try
                {
                    var bi = new System.Windows.Media.Imaging.BitmapImage();
                    bi.BeginInit();
                    bi.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bi.UriSource = new Uri(profile.CustomImagePath!);
                    bi.EndInit();
                    bi.Freeze();

                    var img = new System.Windows.Controls.Image
                    {
                        Source = bi,
                        Width = 40,
                        Height = 40,
                        Stretch = Stretch.UniformToFill,
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                        VerticalAlignment = System.Windows.VerticalAlignment.Center
                    };
                    avatarCircle.Child = img;
                }
                catch
                {
                    hasImg = false;
                }
            }

            if (!hasImg)
            {
                var vectorPath = new System.Windows.Shapes.Path
                {
                    Data = ProfileVectorHelper.GetGeometry(profile.Icon),
                    Fill = MediaBrushes.White,
                    Width = 20,
                    Height = 20,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center
                };
                avatarCircle.Child = vectorPath;
            }

            Grid.SetColumn(avatarCircle, 0);
            grid.Children.Add(avatarCircle);

            // Name & Status
            var textStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            textStack.Children.Add(new TextBlock
            {
                Text = profile.Name,
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                Foreground = (MediaBrush)FindResource("TextPrimary"),
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 160
            });
            textStack.Children.Add(new TextBlock
            {
                Text = profile.IsDefault ? "اکانت پیش‌فرض" : "اکانت مرورگر",
                FontSize = 10,
                Foreground = (MediaBrush)FindResource("TextSecondary"),
                Margin = new Thickness(0, 2, 0, 0)
            });

            Grid.SetColumn(textStack, 1);
            grid.Children.Add(textStack);

            // Action Buttons
            var actionBtns = new StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            var editBtn = new System.Windows.Controls.Button
            {
                Style = (Style)FindResource("CardEditBtnStyle"),
                Margin = new Thickness(0, 0, 4, 0),
                ToolTip = "ویرایش این اکانت",
                Tag = profile
            };
            editBtn.Click += (s, e) =>
            {
                e.Handled = true;
                OpenEditProfileModal(profile);
            };
            actionBtns.Children.Add(editBtn);

            if (canDelete)
            {
                var delBtn = new System.Windows.Controls.Button
                {
                    Style = (Style)FindResource("CardCloseBtnStyle"),
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
                actionBtns.Children.Add(delBtn);
            }

            Grid.SetColumn(actionBtns, 2);
            grid.Children.Add(actionBtns);

            border.Child = grid;
            return border;
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
                if (!_isDragging) LaunchWithProfile(profile);
            };

            AttachDragDropEvents(border, profile);

            var grid = new Grid();

            // Action buttons on top of card (Edit and Delete)
            var actionBtnsPanel = new StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                VerticalAlignment = System.Windows.VerticalAlignment.Top,
                Margin = new Thickness(0, 6, 6, 0)
            };

            // Edit button (pencil)
            var editBtn = new System.Windows.Controls.Button
            {
                Style = (Style)FindResource("CardEditBtnStyle"),
                Margin = new Thickness(0, 0, 4, 0),
                ToolTip = "ویرایش نام، رنگ و آیکون این اکانت",
                Tag = profile
            };
            editBtn.Click += (s, e) =>
            {
                e.Handled = true;
                OpenEditProfileModal(profile);
            };
            actionBtnsPanel.Children.Add(editBtn);

            // Delete button on top-right if deletable
            if (canDelete)
            {
                var delBtn = new System.Windows.Controls.Button
                {
                    Style = (Style)FindResource("CardCloseBtnStyle"),
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
                actionBtnsPanel.Children.Add(delBtn);
            }

            grid.Children.Add(actionBtnsPanel);

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
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                ClipToBounds = true
            };

            bool hasImg = !string.IsNullOrWhiteSpace(profile.CustomImagePath) && System.IO.File.Exists(profile.CustomImagePath);
            if (hasImg)
            {
                try
                {
                    var bi = new System.Windows.Media.Imaging.BitmapImage();
                    bi.BeginInit();
                    bi.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bi.UriSource = new Uri(profile.CustomImagePath!);
                    bi.EndInit();
                    bi.Freeze();

                    var img = new System.Windows.Controls.Image
                    {
                        Source = bi,
                        Width = 68,
                        Height = 68,
                        Stretch = Stretch.UniformToFill,
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                        VerticalAlignment = System.Windows.VerticalAlignment.Center
                    };
                    avatarCircle.Child = img;
                }
                catch
                {
                    hasImg = false;
                }
            }

            if (!hasImg)
            {
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
            }

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
            _selectedCustomImage = null;

            UpdateModalPreview();
            HighlightSelectedColor();
            HighlightSelectedIcon();
            AddProfileModal.Visibility = Visibility.Visible;
            TxtModalProfileName.Focus();
            TxtModalProfileName.SelectAll();
        }

        private void OpenEditProfileModal(BrowserProfile profile)
        {
            _editingProfile = profile;
            TxtModalTitle.Text = $"ویرایش اکانت «{profile.Name}»";
            TxtModalProfileName.Text = profile.Name;
            _selectedColor = string.IsNullOrWhiteSpace(profile.ColorHex) ? "#3B82F6" : profile.ColorHex;
            _selectedIcon = string.IsNullOrWhiteSpace(profile.Icon) ? "user" : profile.Icon;
            _selectedCustomImage = profile.CustomImagePath;

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

        private void BtnBrowseCustomImage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "انتخاب آیکون یا تصویر پروفایل",
                    Filter = "Image Files|*.png;*.jpg;*.jpeg;*.ico;*.bmp;*.webp|All Files|*.*"
                };

                if (dialog.ShowDialog() == true)
                {
                    string chosenFile = dialog.FileName;
                    if (System.IO.File.Exists(chosenFile))
                    {
                        // Copy image into profile directory or app data directory to make it persistent
                        string targetProfileId = _editingProfile?.Id ?? Guid.NewGuid().ToString("N");
                        string destFolder = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "profiles", targetProfileId);
                        if (!System.IO.Directory.Exists(destFolder))
                            System.IO.Directory.CreateDirectory(destFolder);

                        string ext = System.IO.Path.GetExtension(chosenFile);
                        string destPath = System.IO.Path.Combine(destFolder, $"avatar_{DateTime.Now.Ticks}{ext}");
                        System.IO.File.Copy(chosenFile, destPath, true);

                        _selectedCustomImage = destPath;
                        UpdateModalPreview();
                    }
                }
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show($"خطا در بارگذاری تصویر: {ex.Message}", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnClearCustomImage_Click(object sender, RoutedEventArgs e)
        {
            _selectedCustomImage = null;
            UpdateModalPreview();
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
                _editingProfile.CustomImagePath = _selectedCustomImage;
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
                    CustomImagePath = _selectedCustomImage,
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

            bool hasImg = !string.IsNullOrWhiteSpace(_selectedCustomImage) && System.IO.File.Exists(_selectedCustomImage);
            if (hasImg && AvatarPreviewImage != null)
            {
                try
                {
                    var bi = new System.Windows.Media.Imaging.BitmapImage();
                    bi.BeginInit();
                    bi.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bi.UriSource = new Uri(_selectedCustomImage!);
                    bi.EndInit();
                    bi.Freeze();

                    AvatarPreviewImage.Source = bi;
                    AvatarPreviewImage.Visibility = Visibility.Visible;
                    AvatarPreviewPath.Visibility = Visibility.Collapsed;
                    if (BtnClearCustomImage != null) BtnClearCustomImage.Visibility = Visibility.Visible;
                }
                catch
                {
                    hasImg = false;
                }
            }

            if (!hasImg)
            {
                if (AvatarPreviewImage != null)
                {
                    AvatarPreviewImage.Source = null;
                    AvatarPreviewImage.Visibility = Visibility.Collapsed;
                }
                AvatarPreviewPath.Visibility = Visibility.Visible;
                AvatarPreviewPath.Data = ProfileVectorHelper.GetGeometry(_selectedIcon);
                if (BtnClearCustomImage != null) BtnClearCustomImage.Visibility = Visibility.Collapsed;
            }
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
