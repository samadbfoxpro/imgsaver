using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;

using WinForms = System.Windows.Forms;
using Color = System.Windows.Media.Color;

namespace imgsaver
{
    public partial class GalleryWindow : Window
    {
        private const string PrivacyConfigFileName = "gallery_privacy.txt";
        public static bool IsPrivacyMode { get; private set; } = LoadPrivacyMode();
        public static event EventHandler PrivacyModeChanged;

        private static bool LoadPrivacyMode()
        {
            try
            {
                string configPath = DataPathManager.GetSettingsFilePath(PrivacyConfigFileName);
                if (File.Exists(configPath))
                {
                    string content = File.ReadAllText(configPath).Trim();
                    if (bool.TryParse(content, out bool val)) return val;
                }
            }
            catch { }
            return false;
        }

        private static void SavePrivacyMode(bool isPrivacy)
        {
            try
            {
                string configPath = DataPathManager.GetSettingsFilePath(PrivacyConfigFileName);
                File.WriteAllText(configPath, isPrivacy.ToString());
            }
            catch { }
        }

        private string _galleryPath = "";
        private const string GalleryConfigFileName = "gallery_config.txt";
        private const int RecentWindowDays = 31;
        
        private Dictionary<string, List<string>> _imagesByDate = new Dictionary<string, List<string>>();
        private Dictionary<string, GalleryImageInfo> _imageIndexByPath = new Dictionary<string, GalleryImageInfo>(StringComparer.OrdinalIgnoreCase);
        private Dictionary<string, string> _imagePrompts = new Dictionary<string, string>(); // imagePath -> prompt content
        private List<GalleryImageInfo> _allImages = new List<GalleryImageInfo>();
        private List<string> _availableDates = new List<string>();
        private int _currentDateIndex = 0;
        private DateTime _currentCalendarMonth = DateTime.Now;
        private bool _isSearchMode = false;
        private bool _isRandomMode = false;
        private bool _isSelectionMode = false;
        private HashSet<string> _selectedImages = new HashSet<string>();
        
        // Paging fields
        private List<string> _currentImagesList = new List<string>();
        private int _currentPage = 1;
        private const int PageSize = 24;
        
        // Optimizations
        private static SemaphoreSlim _thumbnailSemaphore = new SemaphoreSlim(6); // Max 6 parallel loads


        public GalleryWindow(string defaultPath)
        {
            InitializeComponent();

            SourceInitialized += (s, e) => WindowResizingHelper.ApplyModernWindowStyle(this);

            SizeChanged += Window_SizeChanged;

            // Responsive sizing: ensure window fits screen
            double screenWidth = SystemParameters.WorkArea.Width;
            double screenHeight = SystemParameters.WorkArea.Height;
            if (Width > screenWidth) Width = screenWidth * 0.95;
            if (Height > screenHeight) Height = screenHeight * 0.95;
            
            // Try load saved path independently
            string savedPath = LoadGalleryPath();
            if (!string.IsNullOrEmpty(savedPath) && Directory.Exists(savedPath))
            {
                _galleryPath = savedPath;
            }
            else
            {
                _galleryPath = defaultPath ?? "";
            }

            Loaded += async (_, _) =>
            {
                UpdatePrivacyButtonUI();
                UpdateGridColumns();
                await LoadGalleryAsync();
            };
        }

        private string LoadGalleryPath()
        {
            try
            {
                string configPath = DataPathManager.GetDataFilePath(GalleryConfigFileName);
                if (File.Exists(configPath))
                {
                    return File.ReadAllText(configPath).Trim();
                }
            }
            catch { }
            return "";
        }

        private void SaveGalleryPath(string path)
        {
            try
            {
                string configPath = DataPathManager.GetDataFilePath(GalleryConfigFileName);
                File.WriteAllText(configPath, path);
            }
            catch { }
        }

        private void BtnSelectFolder_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new WinForms.FolderBrowserDialog();
            if (!string.IsNullOrEmpty(_galleryPath)) dialog.SelectedPath = _galleryPath;
            
            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                _galleryPath = dialog.SelectedPath;
                TxtGalleryPath.Text = _galleryPath;
                SaveGalleryPath(_galleryPath);
                
                // Reload gallery
                _ = LoadGalleryAsync();
            }
        }

        private void TxtGalleryPath_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                string path = TxtGalleryPath.Text.Trim();
                if (Directory.Exists(path))
                {
                    _galleryPath = path;
                    SaveGalleryPath(_galleryPath);
                    _ = LoadGalleryAsync();
                    
                    // Remove focus from textbox to indicate submission
                    ImageGrid.Focus(); 
                }
                else
                {
                    System.Windows.MessageBox.Show("پوشه مورد نظر یافت نشد.", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async Task LoadGalleryAsync()
        {
            Dispatcher.Invoke(() => TxtGalleryPath.Text = _galleryPath);

            LoadingIndicator.Visibility = Visibility.Visible;
            ImageGrid.Children.Clear();

            bool showUnorganized = ChkShowUnorganized.IsChecked == true;
            await Task.Run(() => ScanImageIndex(showUnorganized));

            if (_imagesByDate.Count == 0)
            {
                LoadingIndicator.Visibility = Visibility.Collapsed;
                EmptyState.Visibility = Visibility.Visible;
                TxtEmptyMessage.Text = "هیچ تصویری در پوشه انتخاب‌شده یافت نشد";
                return;
            }

            _availableDates = _imagesByDate.Keys.OrderByDescending(d => d).ToList();
            _currentDateIndex = 0;

            int totalImages = _allImages.Count;
            TxtImageCount.Text = $"{totalImages} تصویر";

            MoveToRecentStart();
            ShowDate(_availableDates[_currentDateIndex]);

            LoadingIndicator.Visibility = Visibility.Collapsed;
        }

        private void ScanImageIndex(bool showUnorganized)
        {
            if (string.IsNullOrEmpty(_galleryPath) || !Directory.Exists(_galleryPath))
                return;

            _imagesByDate.Clear();
            _imagePrompts.Clear();
            _imageIndexByPath.Clear();
            _allImages.Clear();

            var imageExtensions = new[] { ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".tiff" };
            var files = Directory.GetFiles(_galleryPath, "*.*", SearchOption.AllDirectories)
                .Where(f => imageExtensions.Contains(Path.GetExtension(f).ToLower()))
                .OrderByDescending(f => File.GetLastWriteTime(f))
                .ToList();

            foreach (var file in files)
            {
                string txtPath = Path.Combine(Path.GetDirectoryName(file) ?? "", Path.GetFileNameWithoutExtension(file) + ".txt");
                bool hasPrompt = File.Exists(txtPath);

                if (!showUnorganized && !hasPrompt)
                    continue;

                var lastWriteTime = File.GetLastWriteTime(file);
                var dateKey = lastWriteTime.ToString("yyyy-MM-dd");
                var info = new GalleryImageInfo
                {
                    Path = file,
                    FileName = Path.GetFileNameWithoutExtension(file),
                    DateKey = dateKey,
                    MonthKey = lastWriteTime.ToString("yyyy-MM"),
                    LastWriteTime = lastWriteTime,
                    HasPrompt = hasPrompt,
                    PromptPath = txtPath
                };

                _allImages.Add(info);
                _imageIndexByPath[file] = info;

                if (!_imagesByDate.ContainsKey(dateKey))
                {
                    _imagesByDate[dateKey] = new List<string>();
                }
                _imagesByDate[dateKey].Add(file);
            }
        }

        private void MoveToRecentStart()
        {
            DateTime recentStart = DateTime.Now.Date.AddDays(-RecentWindowDays + 1);
            int recentIndex = _availableDates.FindIndex(date =>
                DateTime.TryParse(date, out DateTime parsed) && parsed.Date >= recentStart);

            _currentDateIndex = recentIndex >= 0 ? recentIndex : 0;
        }


        private void ChkShowUnorganized_Changed(object sender, RoutedEventArgs e)
        {
             if (IsLoaded)
                _ = LoadGalleryAsync();
        }

        private void ChkSelectMode_Changed(object sender, RoutedEventArgs e)
        {
            _isSelectionMode = ChkSelectMode.IsChecked == true;
            BtnDeleteSelected.Visibility = _isSelectionMode ? Visibility.Visible : Visibility.Collapsed;
            
            if (!_isSelectionMode)
            {
                _selectedImages.Clear();
            }

            // Refresh the grid to show/hide checkboxes
            if (_isSearchMode)
            {
                string query = TxtSearch.Text.Trim();
                if (!string.IsNullOrEmpty(query)) PerformSearchAsync(query);
            }
            else if (_availableDates.Count > 0)
            {
                ShowDate(_availableDates[_currentDateIndex]);
            }
        }



        #region Search

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = TxtSearch.Text.Trim();
            
            // Update placeholder visibility
            SearchPlaceholder.Visibility = string.IsNullOrEmpty(query) ? Visibility.Visible : Visibility.Collapsed;
            BtnClearSearch.Visibility = string.IsNullOrEmpty(query) ? Visibility.Collapsed : Visibility.Visible;

            if (string.IsNullOrEmpty(query) && _isSearchMode)
            {
                _isSearchMode = false;
                TxtSearchResults.Text = "";
                if (_availableDates.Count > 0)
                    ShowDate(_availableDates[_currentDateIndex]);
            }
        }

        private void TxtSearch_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                string query = TxtSearch.Text.Trim();
                if (!string.IsNullOrEmpty(query))
                {
                    _isSearchMode = true;
                    PerformSearchAsync(query);
                }
                e.Handled = true;
            }
        }

        private async void PerformSearchAsync(string query)
        {
            LoadingIndicator.Visibility = Visibility.Visible;
            ImageGrid.Children.Clear();
            EmptyState.Visibility = Visibility.Collapsed;
            PaginationPanel.Visibility = Visibility.Collapsed;

            await Task.Run(async () =>
            {
                // Split query into words for multi-word search
                var searchWords = query.ToLower().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                var results = new List<string>();

                foreach (var image in _allImages)
                {
                    string fileName = image.FileName.ToLower();
                    string promptContent = GetPromptForImage(image.Path);

                    // Check if ALL search words are found in filename OR prompt
                    bool allWordsFound = searchWords.All(word => 
                        fileName.Contains(word) || promptContent.Contains(word));

                    if (allWordsFound)
                    {
                        results.Add(image.Path);
                    }
                }

                await Dispatcher.InvokeAsync(() =>
                {
                    TxtSearchResults.Text = $"{results.Count} تصویر یافت شد";

                    _currentImagesList = results;
                    _currentPage = 1;
                    DisplayCurrentPage();

                    LoadingIndicator.Visibility = Visibility.Collapsed;
                });
            });
        }

        private void BtnClearSearch_Click(object sender, RoutedEventArgs e)
        {
            TxtSearch.Text = "";
            TxtSearch.Focus();
        }

        #endregion

        #region Calendar

        private void BuildCalendar()
        {
            CalendarDaysGrid.Children.Clear();
            TxtCalendarMonth.Text = _currentCalendarMonth.ToString("MMMM yyyy");

            DateTime firstDayOfMonth = new DateTime(_currentCalendarMonth.Year, _currentCalendarMonth.Month, 1);
            int daysInMonth = DateTime.DaysInMonth(_currentCalendarMonth.Year, _currentCalendarMonth.Month);
            int startDayOfWeek = (int)firstDayOfMonth.DayOfWeek;

            // Fill empty slots before start of month
            for (int i = 0; i < startDayOfWeek; i++)
            {
                CalendarDaysGrid.Children.Add(new Border { Height = 45 });
            }

            // Fill days
            for (int day = 1; day <= daysInMonth; day++)
            {
                DateTime date = new DateTime(_currentCalendarMonth.Year, _currentCalendarMonth.Month, day);
                string dateKey = date.ToString("yyyy-MM-dd");
                bool hasImages = _imagesByDate.ContainsKey(dateKey);
                int imageCount = hasImages ? _imagesByDate[dateKey].Count : 0;

                var dayBorder = new Border
                {
                    Height = 45,
                    Margin = new Thickness(2),
                    CornerRadius = new CornerRadius(8),
                    Background = hasImages ? new SolidColorBrush(Color.FromRgb(18, 32, 64)) : System.Windows.Media.Brushes.Transparent,
                    BorderBrush = hasImages ? new SolidColorBrush(Color.FromRgb(29, 78, 216)) : System.Windows.Media.Brushes.Transparent,
                    BorderThickness = hasImages ? new Thickness(1) : new Thickness(0),
                    Cursor = hasImages ? System.Windows.Input.Cursors.Hand : System.Windows.Input.Cursors.Arrow,
                    Tag = dateKey
                };

                var dayStack = new StackPanel { VerticalAlignment = System.Windows.VerticalAlignment.Center };

                var dayText = new TextBlock
                {
                    Text = day.ToString(),
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    Foreground = hasImages ? System.Windows.Media.Brushes.White : new SolidColorBrush(Color.FromRgb(71, 85, 105))
                };
                dayStack.Children.Add(dayText);

                if (hasImages)
                {
                    var countText = new TextBlock
                    {
                        Text = $"{imageCount}",
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                        FontSize = 9.5,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                        Opacity = 0.95
                    };
                    dayStack.Children.Add(countText);

                    dayBorder.MouseEnter += (s, e) =>
                    {
                        dayBorder.Background = new SolidColorBrush(Color.FromRgb(29, 78, 216));
                        dayBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248));
                    };
                    dayBorder.MouseLeave += (s, e) =>
                    {
                        dayBorder.Background = new SolidColorBrush(Color.FromRgb(18, 32, 64));
                        dayBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(29, 78, 216));
                    };

                    dayBorder.MouseLeftButtonUp += (s, e) =>
                    {
                        if (s is Border b && b.Tag is string clickedDate && _availableDates.Contains(clickedDate))
                        {
                            _currentDateIndex = _availableDates.IndexOf(clickedDate);
                            _isSearchMode = false;
                            TxtSearch.Text = "";
                            ShowDate(clickedDate);
                            CalendarPanel.Visibility = Visibility.Collapsed;
                        }
                        e.Handled = true;
                    };
                }

                dayBorder.Child = dayStack;
                CalendarDaysGrid.Children.Add(dayBorder);
            }
        }

        private void BtnPrevMonth_Click(object sender, RoutedEventArgs e)
        {
            _currentCalendarMonth = _currentCalendarMonth.AddMonths(-1);
            BuildCalendar();
        }

        private void BtnNextMonth_Click(object sender, RoutedEventArgs e)
        {
            _currentCalendarMonth = _currentCalendarMonth.AddMonths(1);
            BuildCalendar();
        }

        private void BtnToggleCalendar_Click(object sender, RoutedEventArgs e)
        {
            if (CalendarPanel.Visibility == Visibility.Visible)
            {
                CalendarPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                BuildCalendar();
                CalendarPanel.Visibility = Visibility.Visible;
            }
        }

        private void CalendarOverlay_Click(object sender, MouseButtonEventArgs e)
        {
            CalendarPanel.Visibility = Visibility.Collapsed;
        }

        private void CalendarPanel_Click(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true; // Prevent closing when clicking inside calendar
        }

        #endregion

        #region Date Navigation

        private void ShowDate(string date)
        {
            if (_isSearchMode) return;

            ImageGrid.Children.Clear();
            PaginationPanel.Visibility = Visibility.Collapsed;
            
            if (!_imagesByDate.ContainsKey(date))
            {
                _currentImagesList = new List<string>();
                EmptyState.Visibility = Visibility.Visible;
                TxtEmptyMessage.Text = "هیچ تصویری برای این تاریخ یافت نشد";
                return;
            }

            EmptyState.Visibility = Visibility.Collapsed;

            var images = _imagesByDate[date];
            TxtCurrentDate.Text = FormatDate(date);
            TxtDateInfo.Text = $"{images.Count} تصویر";

            BtnPrevDate.IsEnabled = _currentDateIndex < _availableDates.Count - 1;
            BtnNextDate.IsEnabled = _currentDateIndex > 0;

            _currentImagesList = images.ToList();
            _currentPage = 1;
            DisplayCurrentPage();
        }

        private void BtnPrevDate_Click(object sender, RoutedEventArgs e)
        {
            if (_currentDateIndex < _availableDates.Count - 1)
            {
                _currentDateIndex++;
                _isSearchMode = false;
                TxtSearch.Text = "";
                ShowDate(_availableDates[_currentDateIndex]);
            }
        }

        private void BtnNextDate_Click(object sender, RoutedEventArgs e)
        {
            if (_currentDateIndex > 0)
            {
                _currentDateIndex--;
                _isSearchMode = false;
                TxtSearch.Text = "";
                ShowDate(_availableDates[_currentDateIndex]);
            }
        }

        #endregion

        #region Random Gallery

        private void BtnRandomGallery_Click(object sender, RoutedEventArgs e)
        {
            _isRandomMode = !_isRandomMode;
            BtnRandomGallery.Tag = _isRandomMode ? "Active" : "";
            
            if (_isRandomMode)
            {
                _isSearchMode = false;
                TxtSearch.Text = "";
                BtnRefreshRandom.Visibility = Visibility.Visible;
                BtnPrevDate.Visibility = Visibility.Collapsed;
                BtnNextDate.Visibility = Visibility.Collapsed;
                BtnShowCalendar.Visibility = Visibility.Collapsed;
                ShowRandomImages();
            }
            else
            {
                BtnRefreshRandom.Visibility = Visibility.Collapsed;
                BtnPrevDate.Visibility = Visibility.Visible;
                BtnNextDate.Visibility = Visibility.Visible;
                BtnShowCalendar.Visibility = Visibility.Visible;
                if (_availableDates.Count > 0)
                    ShowDate(_availableDates[_currentDateIndex]);
            }
        }

        private void BtnRefreshRandom_Click(object sender, RoutedEventArgs e)
        {
            if (_isRandomMode)
                ShowRandomImages();
        }

        private void ShowRandomImages()
        {
            ImageGrid.Children.Clear();
            EmptyState.Visibility = Visibility.Collapsed;
            PaginationPanel.Visibility = Visibility.Collapsed;
            
            if (_allImages.Count == 0)
            {
                _currentImagesList = new List<string>();
                EmptyState.Visibility = Visibility.Visible;
                TxtEmptyMessage.Text = "هیچ تصویری در گالری یافت نشد";
                return;
            }

            TxtCurrentDate.Text = "تصاویر تصادفی (Random)";
            
            // Pick from the whole lightweight index, including months not yet viewed.
            var random = new Random();
            var randomImages = _allImages.OrderBy(x => random.Next()).Take(10).Select(x => x.Path).ToList();
            
            TxtDateInfo.Text = $"{randomImages.Count} تصویر تصادفی";

            _currentImagesList = randomImages;
            _currentPage = 1;
            DisplayCurrentPage();
        }

        #endregion

        #region Image Cards

        private static readonly Color CardBgNormal = Color.FromRgb(12, 18, 34);
        private static readonly Color CardBgHover = Color.FromRgb(20, 30, 56);
        private static readonly Color CardBorderNormal = Color.FromRgb(28, 39, 68);
        private static readonly Color CardBorderHover = Color.FromRgb(56, 189, 248);
        private static readonly Color CardBorderSelected = Color.FromRgb(56, 189, 248);
        private static readonly Color ShadowGlowSelected = Color.FromRgb(56, 189, 248);
        private static readonly Color ShadowGlowHover = Color.FromArgb(140, 56, 189, 248);
        private static readonly Color ShadowNormal = Color.FromRgb(0, 0, 0);
        private static readonly IEasingFunction CardEase = new QuadraticEase { EasingMode = EasingMode.EaseOut };

        private Border CreateImageCard(string imagePath)
        {
            bool isSelected = _selectedImages.Contains(imagePath);

            var bgBrush = new SolidColorBrush(CardBgNormal);
            var borderBrush = new SolidColorBrush(isSelected ? CardBorderSelected : CardBorderNormal);
            var dropShadow = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = isSelected ? 14 : 10,
                Color = isSelected ? ShadowGlowSelected : ShadowNormal,
                Opacity = isSelected ? 0.45 : 0.35,
                ShadowDepth = 2
            };

            var card = new Border
            {
                Background = bgBrush,
                BorderBrush = borderBrush,
                BorderThickness = isSelected ? new Thickness(2) : new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Margin = new Thickness(6),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = imagePath,
                Effect = dropShadow
            };

            var grid = new Grid();
            card.Child = grid;

            var cardStack = new StackPanel();
            grid.Children.Add(cardStack);

            if (_isSelectionMode)
            {
                var checkBox = new System.Windows.Controls.CheckBox
                {
                    IsChecked = isSelected,
                    Margin = new Thickness(8),
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                    VerticalAlignment = System.Windows.VerticalAlignment.Top,
                    IsHitTestVisible = false // Click on card handles it
                };
                grid.Children.Add(checkBox);
            }

            var thumbnailBorder = new Border
            {
                Height = 185,
                Background = new SolidColorBrush(Color.FromRgb(5, 8, 17)),
                CornerRadius = new CornerRadius(12, 12, 0, 0),
                ClipToBounds = true
            };

            var thumbnailScale = new ScaleTransform(1.0, 1.0);
            var thumbnail = new System.Windows.Controls.Image
            {
                Stretch = Stretch.Uniform,
                Margin = new Thickness(4),
                RenderTransformOrigin = new System.Windows.Point(0.5, 0.5),
                RenderTransform = thumbnailScale
            };

            // Badge indicator for HasPrompt
            if (_imageIndexByPath.TryGetValue(imagePath, out var imgInfo) && imgInfo.HasPrompt)
            {
                var promptBadge = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(29, 78, 216)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(59, 130, 246)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(6, 2, 6, 2),
                    Margin = new Thickness(8),
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                    VerticalAlignment = System.Windows.VerticalAlignment.Top,
                    IsHitTestVisible = false
                };
                var promptBadgeText = new TextBlock
                {
                    Text = "✨ پرامپت",
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    Foreground = System.Windows.Media.Brushes.White
                };
                promptBadge.Child = promptBadgeText;
                grid.Children.Add(promptBadge);
            }
            
            // Resolution Badge (e.g. HD/4K)
            try
            {
                var fileInfo = new FileInfo(imagePath);
                string resTag = fileInfo.Length > 2 * 1024 * 1024 ? "4K" : "HD";
                var resBadge = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(200, 9, 21, 44)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(30, 58, 138)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(4, 1, 4, 1),
                    Margin = new Thickness(8),
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                    VerticalAlignment = System.Windows.VerticalAlignment.Top,
                    IsHitTestVisible = false
                };
                var resBadgeText = new TextBlock
                {
                    Text = resTag,
                    FontSize = 9,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248))
                };
                resBadge.Child = resBadgeText;
                grid.Children.Add(resBadge);
            }
            catch { }

            var privacyOverlay = new Border
            {
                Name = "PrivacyOverlay",
                Background = new SolidColorBrush(Color.FromArgb(230, 5, 7, 15)),
                CornerRadius = new CornerRadius(12, 12, 0, 0),
                Height = 185,
                VerticalAlignment = VerticalAlignment.Top,
                Visibility = IsPrivacyMode ? Visibility.Visible : Visibility.Collapsed,
                IsHitTestVisible = false
            };
            
            var eyeIcon = new TextBlock
            {
                Text = "👁️‍🗨️",
                FontSize = 48,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = System.Windows.VerticalAlignment.Center,
                Opacity = 0.6
            };
            privacyOverlay.Child = eyeIcon;
            grid.Children.Add(privacyOverlay);

            Task.Run(async () =>
            {
                await _thumbnailSemaphore.WaitAsync();
                try
                {
                    await Task.Delay(1); // Yield to let UI update
                    BitmapImage? bitmap = null;

                    using (var stream = new FileStream(imagePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                         bitmap = new BitmapImage();
                         bitmap.BeginInit();
                         bitmap.CacheOption = BitmapCacheOption.OnLoad;
                         bitmap.DecodePixelWidth = 250; // Optimization: Only load thumbnail size
                         bitmap.StreamSource = stream;
                         bitmap.EndInit();
                         bitmap.Freeze();
                    }

                    if (bitmap != null)
                        Dispatcher.Invoke(() => thumbnail.Source = bitmap);
                }
                catch { }
                finally
                {
                    _thumbnailSemaphore.Release();
                }
            });

            thumbnailBorder.Child = thumbnail;
            cardStack.Children.Add(thumbnailBorder);

            var fileName = new TextBlock
            {
                Text = Path.GetFileNameWithoutExtension(imagePath),
                Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(8, 6, 8, 6),
                TextTrimming = TextTrimming.CharacterEllipsis,
                ToolTip = Path.GetFileName(imagePath)
            };
            cardStack.Children.Add(fileName);
            
            card.MouseLeftButtonUp += (s, e) => 
            {
                if (_isSelectionMode)
                {
                    if (_selectedImages.Contains(imagePath))
                    {
                        _selectedImages.Remove(imagePath);
                        card.BorderThickness = new Thickness(1);

                        borderBrush.BeginAnimation(SolidColorBrush.ColorProperty, 
                            new ColorAnimation(CardBorderNormal, TimeSpan.FromMilliseconds(200)) { EasingFunction = CardEase });
                        dropShadow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.ColorProperty, 
                            new ColorAnimation(ShadowNormal, TimeSpan.FromMilliseconds(200)) { EasingFunction = CardEase });
                        dropShadow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.OpacityProperty, 
                            new DoubleAnimation(0.35, TimeSpan.FromMilliseconds(200)) { EasingFunction = CardEase });
                        dropShadow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.BlurRadiusProperty, 
                            new DoubleAnimation(10, TimeSpan.FromMilliseconds(200)) { EasingFunction = CardEase });

                        // Find and uncheck checkbox if exists
                        if (card.Child is Grid g)
                        {
                            foreach (var child in g.Children)
                            {
                                if (child is System.Windows.Controls.CheckBox cb) cb.IsChecked = false;
                            }
                        }
                    }
                    else
                    {
                        _selectedImages.Add(imagePath);
                        card.BorderThickness = new Thickness(2);

                        borderBrush.BeginAnimation(SolidColorBrush.ColorProperty, 
                            new ColorAnimation(CardBorderSelected, TimeSpan.FromMilliseconds(200)) { EasingFunction = CardEase });
                        dropShadow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.ColorProperty, 
                            new ColorAnimation(ShadowGlowSelected, TimeSpan.FromMilliseconds(200)) { EasingFunction = CardEase });
                        dropShadow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.OpacityProperty, 
                            new DoubleAnimation(0.45, TimeSpan.FromMilliseconds(200)) { EasingFunction = CardEase });
                        dropShadow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.BlurRadiusProperty, 
                            new DoubleAnimation(14, TimeSpan.FromMilliseconds(200)) { EasingFunction = CardEase });

                        // Find and check checkbox if exists
                        if (card.Child is Grid g)
                        {
                            foreach (var child in g.Children)
                            {
                                if (child is System.Windows.Controls.CheckBox cb) cb.IsChecked = true;
                            }
                        }
                    }
                }
                else
                {
                    OpenImageViewer(imagePath);
                }
            };

            card.MouseEnter += (s, e) =>
            {
                // Smooth background fade
                bgBrush.BeginAnimation(SolidColorBrush.ColorProperty, 
                    new ColorAnimation(CardBgHover, TimeSpan.FromMilliseconds(220)) { EasingFunction = CardEase });

                // Smooth border & glow fade if not selected
                if (!_selectedImages.Contains(imagePath))
                {
                    borderBrush.BeginAnimation(SolidColorBrush.ColorProperty, 
                        new ColorAnimation(CardBorderHover, TimeSpan.FromMilliseconds(220)) { EasingFunction = CardEase });
                    dropShadow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.ColorProperty, 
                        new ColorAnimation(ShadowGlowHover, TimeSpan.FromMilliseconds(220)) { EasingFunction = CardEase });
                    dropShadow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.OpacityProperty, 
                        new DoubleAnimation(0.55, TimeSpan.FromMilliseconds(220)) { EasingFunction = CardEase });
                    dropShadow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.BlurRadiusProperty, 
                        new DoubleAnimation(16, TimeSpan.FromMilliseconds(220)) { EasingFunction = CardEase });
                }

                // Smooth thumbnail zoom
                thumbnailScale.BeginAnimation(ScaleTransform.ScaleXProperty, 
                    new DoubleAnimation(1.045, TimeSpan.FromMilliseconds(220)) { EasingFunction = CardEase });
                thumbnailScale.BeginAnimation(ScaleTransform.ScaleYProperty, 
                    new DoubleAnimation(1.045, TimeSpan.FromMilliseconds(220)) { EasingFunction = CardEase });
            };

            card.MouseLeave += (s, e) =>
            {
                // Smooth background fade back
                bgBrush.BeginAnimation(SolidColorBrush.ColorProperty, 
                    new ColorAnimation(CardBgNormal, TimeSpan.FromMilliseconds(260)) { EasingFunction = CardEase });

                // Smooth border & glow fade back if not selected
                if (!_selectedImages.Contains(imagePath))
                {
                    borderBrush.BeginAnimation(SolidColorBrush.ColorProperty, 
                        new ColorAnimation(CardBorderNormal, TimeSpan.FromMilliseconds(260)) { EasingFunction = CardEase });
                    dropShadow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.ColorProperty, 
                        new ColorAnimation(ShadowNormal, TimeSpan.FromMilliseconds(260)) { EasingFunction = CardEase });
                    dropShadow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.OpacityProperty, 
                        new DoubleAnimation(0.35, TimeSpan.FromMilliseconds(260)) { EasingFunction = CardEase });
                    dropShadow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.BlurRadiusProperty, 
                        new DoubleAnimation(10, TimeSpan.FromMilliseconds(260)) { EasingFunction = CardEase });
                }

                // Smooth thumbnail zoom back
                thumbnailScale.BeginAnimation(ScaleTransform.ScaleXProperty, 
                    new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(260)) { EasingFunction = CardEase });
                thumbnailScale.BeginAnimation(ScaleTransform.ScaleYProperty, 
                    new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(260)) { EasingFunction = CardEase });
            };

            return card;
        }

        private void BtnPrivacyMode_Click(object sender, RoutedEventArgs e)
        {
            IsPrivacyMode = !IsPrivacyMode;
            SavePrivacyMode(IsPrivacyMode);
            
            UpdatePrivacyButtonUI();
            UpdatePrivacyUI();

            // Notify listeners (like ImageViewer)
            PrivacyModeChanged?.Invoke(this, EventArgs.Empty);
        }

        private void UpdatePrivacyButtonUI()
        {
            if (BtnPrivacyMode != null)
            {
                BtnPrivacyMode.Tag = IsPrivacyMode ? "Active" : "";
                BtnPrivacyMode.ToolTip = IsPrivacyMode ? "غیرفعال‌سازی حالت حریم خصوصی" : "حالت حریم خصوصی (تار کردن تصاویر)";
                if (BtnPrivacyMode.Template?.FindName("PrivacyIconPath", BtnPrivacyMode) is System.Windows.Shapes.Path iconPath)
                {
                    if (IsPrivacyMode && TryFindResource("IconEyeOff") is StreamGeometry offGeom)
                        iconPath.Data = offGeom;
                    else if (!IsPrivacyMode && TryFindResource("IconEye") is StreamGeometry onGeom)
                        iconPath.Data = onGeom;
                }
            }
        }

        private void UpdatePrivacyUI()
        {
            foreach (var child in ImageGrid.Children)
            {
                if (child is Border card && card.Child is Grid grid)
                {
                    var overlay = grid.Children.OfType<Border>().FirstOrDefault(b => b.Name == "PrivacyOverlay");
                    if (overlay != null)
                    {
                        overlay.Visibility = IsPrivacyMode ? Visibility.Visible : Visibility.Collapsed;
                    }
                }
            }
        }

        private void OpenImageViewer(string imagePath)
        {
            try
            {
                // Get all current images in the grid for navigation
                var allImages = new List<string>();
                // If we're in search mode or random mode, the visible ImageGrid contains the
                // proper result set in the displayed order — extract from the grid.
                if (_isSearchMode || _isRandomMode)
                {
                    foreach (var child in ImageGrid.Children)
                    {
                        if (child is Border b && b.Tag is string path) allImages.Add(path);
                    }
                }
                else if (_availableDates.Count >0)
                {
                    allImages = _imagesByDate[_availableDates[_currentDateIndex]].ToList();
                }

                int index = allImages.IndexOf(imagePath);
                var viewer = new ImageViewerWindow(imagePath, allImages, index);
                viewer.Show();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error displaying image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnDeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedImages.Count == 0) return;

            var result = System.Windows.MessageBox.Show(
                $"آیا از حذف {_selectedImages.Count} تصویر و فایل‌های متنی متناظر اطمینان دارید؟",
                "تأیید حذف",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            LoadingIndicator.Visibility = Visibility.Visible;

            await Task.Run(() =>
            {
                foreach (var path in _selectedImages.ToList())
                {
                    try
                    {
                        // Delete image
                        if (File.Exists(path)) File.Delete(path);

                        // Delete text file
                        string txtPath = Path.Combine(Path.GetDirectoryName(path) ?? "", Path.GetFileNameWithoutExtension(path) + ".txt");
                        if (File.Exists(txtPath)) File.Delete(txtPath);
                    }
                    catch { }
                }
            });

            _selectedImages.Clear();
            await LoadGalleryAsync();
        }

        #endregion

        #region Helpers

        private string FormatDate(string dateStr)
        {
            if (DateTime.TryParse(dateStr, out DateTime date))
            {
                var today = DateTime.Now.Date;
                if (date.Date == today)
                    return "امروز";
                else if (date.Date == today.AddDays(-1))
                    return "دیروز";
                else
                    return date.ToString("yyyy/MM/dd");
            }
            return dateStr;
        }

        private string GetPromptForImage(string imagePath)
        {
            if (_imagePrompts.TryGetValue(imagePath, out string cachedPrompt))
                return cachedPrompt;

            if (!_imageIndexByPath.TryGetValue(imagePath, out GalleryImageInfo? info) || !info.HasPrompt)
                return "";

            try
            {
                string prompt = File.Exists(info.PromptPath) ? File.ReadAllText(info.PromptPath).ToLower() : "";
                _imagePrompts[imagePath] = prompt;
                return prompt;
            }
            catch
            {
                _imagePrompts[imagePath] = "";
                return "";
            }
        }

        #endregion

        #region Window Controls

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            }
            else if (e.ClickCount == 1 && e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void BtnLockApp_Click(object sender, RoutedEventArgs e)
        {
            AppLockManager.LockApp();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        #endregion

        /// <summary>
        /// Called by other windows when images have been changed externally (deleted/moved)
        /// Reloads the gallery asynchronously.
        /// </summary>
        public async Task RefreshAfterExternalChange()
        {
        await LoadGalleryAsync();
        }

        #region Paging implementation

        private void DisplayCurrentPage()
        {
            // Release memory of previous page thumbnail images before clearing UI
            foreach (UIElement child in ImageGrid.Children)
            {
                if (child is Border card && card.Child is Grid grid)
                {
                    var stack = grid.Children.OfType<StackPanel>().FirstOrDefault();
                    var thumbBorder = stack?.Children.OfType<Border>().FirstOrDefault();
                    if (thumbBorder?.Child is System.Windows.Controls.Image img)
                    {
                        img.Source = null;
                    }
                }
            }

            ImageGrid.Children.Clear();
            EmptyState.Visibility = Visibility.Collapsed;

            if (_currentImagesList.Count == 0)
            {
                EmptyState.Visibility = Visibility.Visible;
                TxtEmptyMessage.Text = "هیچ تصویری یافت نشد";
                PaginationPanel.Visibility = Visibility.Collapsed;
                return;
            }

            int totalItems = _currentImagesList.Count;
            int totalPages = (int)Math.Ceiling((double)totalItems / PageSize);

            if (_currentPage < 1) _currentPage = 1;
            if (_currentPage > totalPages) _currentPage = totalPages;

            PaginationPanel.Visibility = totalPages > 1 ? Visibility.Visible : Visibility.Collapsed;
            TxtPageInfo.Text = $"صفحه {_currentPage} از {totalPages}";
            BtnPrevPage.IsEnabled = _currentPage > 1;
            BtnNextPage.IsEnabled = _currentPage < totalPages;

            int startIndex = (_currentPage - 1) * PageSize;
            var pageImages = _currentImagesList.Skip(startIndex).Take(PageSize).ToList();

            UpdateGridColumns();

            foreach (var imagePath in pageImages)
            {
                var card = CreateImageCard(imagePath);
                ImageGrid.Children.Add(card);
            }

            // Smooth page transition animation
            ImageGrid.BeginAnimation(UIElement.OpacityProperty, 
                new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(180)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        }

        private void BtnPrevPage_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                DisplayCurrentPage();
            }
        }

        private void BtnNextPage_Click(object sender, RoutedEventArgs e)
        {
            int totalPages = (int)Math.Ceiling((double)_currentImagesList.Count / PageSize);
            if (_currentPage < totalPages)
            {
                _currentPage++;
                DisplayCurrentPage();
            }
        }

        #endregion

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateGridColumns();
        }

        private void UpdateGridColumns()
        {
            if (ImageGrid == null) return;
            double availableWidth = ActualWidth - 56;
            if (availableWidth > 200)
            {
                int cols = Math.Clamp((int)(availableWidth / 240), 3, 10);
                if (ImageGrid.Columns != cols)
                {
                    ImageGrid.Columns = cols;
                }
            }
        }

        private sealed class GalleryImageInfo
        {
            public string Path { get; init; } = "";
            public string FileName { get; init; } = "";
            public string DateKey { get; init; } = "";
            public string MonthKey { get; init; } = "";
            public DateTime LastWriteTime { get; init; }
            public bool HasPrompt { get; init; }
            public string PromptPath { get; init; } = "";
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                if (BtnMaximize != null)
                {
                    BtnMaximize.ToolTip = "بازگردانی";
                    if (MaximizeIconPath != null && TryFindResource("IconRestore") is StreamGeometry restoreGeom)
                    {
                        MaximizeIconPath.Data = restoreGeom;
                    }
                }
                var resizeThickness = SystemParameters.WindowResizeBorderThickness;
                MainBorder.Margin = new Thickness(resizeThickness.Left, resizeThickness.Top, resizeThickness.Right, resizeThickness.Bottom);
                MainBorder.CornerRadius = new CornerRadius(0);
                MainBorder.BorderThickness = new Thickness(0);
            }
            else
            {
                if (BtnMaximize != null)
                {
                    BtnMaximize.ToolTip = "بزرگ کردن";
                    if (MaximizeIconPath != null && TryFindResource("IconMaximize") is StreamGeometry maxGeom)
                    {
                        MaximizeIconPath.Data = maxGeom;
                    }
                }
                MainBorder.Margin = new Thickness(0);
                MainBorder.CornerRadius = new CornerRadius(8);
                MainBorder.BorderThickness = new Thickness(1);
            }

            UpdateGridColumns();
        }
    }
}
