using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace imgsaver
{
    public partial class ImageViewerWindow : Window
    {
        private string _imagePath;
        private string _detectedExtension = ".png"; // Default fallback
        private System.Collections.Generic.List<string> _allImages;
        private int _currentIndex;

        private string _originalPositive = "";
        private string _originalBase = "";
        private string _originalNegative = "";
        private string _originalDescription = "";

        private bool _isLocallyRevealed = false;
        private string _currentFileName = "";

        public ImageViewerWindow(string imagePath, System.Collections.Generic.List<string> allImages = null, int index = -1)
        {
            InitializeComponent();

            // Responsive sizing: ensure window fits screen
            double screenWidth = SystemParameters.WorkArea.Width;
            double screenHeight = SystemParameters.WorkArea.Height;
            if (Width > screenWidth) Width = screenWidth * 0.95;
            if (Height > screenHeight) Height = screenHeight * 0.95;

            _imagePath = imagePath;
            _allImages = allImages ?? new System.Collections.Generic.List<string> { imagePath };
            _currentIndex = index != -1 ? index : _allImages.IndexOf(imagePath);
            
            if (File.Exists(_imagePath))
            {
                _detectedExtension = Path.GetExtension(_imagePath).ToLower();
            }

            // Sync with Privacy Mode
            GalleryWindow.PrivacyModeChanged += OnPrivacyModeChanged;
            UpdatePrivacyOverlay();

            Loaded += async (_, _) => await LoadImageAsync();
        }

        private void OnPrivacyModeChanged(object sender, EventArgs e)
        {
            UpdatePrivacyOverlay();
        }

        private void UpdatePrivacyOverlay()
        {
            bool shouldShowOverlay = GalleryWindow.IsPrivacyMode && !_isLocallyRevealed;
            PrivacyOverlay.Visibility = shouldShowOverlay ? Visibility.Visible : Visibility.Collapsed;
            
            // Show/Hide Reveal button
            BtnRevealPrivacy.Visibility = GalleryWindow.IsPrivacyMode ? Visibility.Visible : Visibility.Collapsed;
            
            // Update Reveal button icon
            if (BtnRevealPrivacy.Template.FindName("txt", BtnRevealPrivacy) is TextBlock txt)
            {
                txt.Text = _isLocallyRevealed ? "🔒" : "👁️";
            }
        }

        private void BtnRevealPrivacy_Click(object sender, RoutedEventArgs e)
        {
            _isLocallyRevealed = !_isLocallyRevealed;
            UpdatePrivacyOverlay();
        }

        protected override void OnClosed(EventArgs e)
        {
            GalleryWindow.PrivacyModeChanged -= OnPrivacyModeChanged;
            base.OnClosed(e);
        }

        private async Task LoadImageAsync()
        {
            try
            {
                // Small delay to ensure UI renders "Loading..." state before heavy work
                await Task.Delay(10);

                // Update file info
                var fileInfo = new FileInfo(_imagePath);
                TxtFileName.Text = fileInfo.Name;
                TxtFileInfo.Text = $"{FormatFileSize(fileInfo.Length)} • {_detectedExtension.Replace(".", "").ToUpper()}";
                TxtDate.Text = fileInfo.LastWriteTime.ToString("MMM dd, yyyy HH:mm");

                // Make filename interactive (clickable to copy)
                MakeFileNameInteractive(fileInfo.Name);

                // Load image safely
                BitmapImage? bitmap = null;
                await Task.Run(() =>
                {
                    try
                    {
                        using (var stream = new FileStream(_imagePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.CacheOption = BitmapCacheOption.OnLoad; // Loads image into memory and closes stream
                            bitmap.StreamSource = stream;
                            
                            // Optimization: Downscale huge images to fit standard screen height (e.g. 1440p)
                            // This saves HUGE amounts of memory for 4k/8k images
                            bitmap.DecodePixelHeight = 1440; 
                            
                            bitmap.EndInit();
                            bitmap.Freeze(); // Must freeze to pass to UI thread
                        }
                    }
                    catch (Exception) 
                    {
                        bitmap = null;
                    }
                });

                // Load real pixel dimensions of original image
                int originalWidth = 0;
                int originalHeight = 0;
                await Task.Run(() =>
                {
                    try
                    {
                        using (var stream = File.OpenRead(_imagePath))
                        {
                            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.IgnoreColorProfile, BitmapCacheOption.None);
                            if (decoder.Frames.Count > 0)
                            {
                                originalWidth = decoder.Frames[0].PixelWidth;
                                originalHeight = decoder.Frames[0].PixelHeight;
                            }
                        }
                    }
                    catch { }
                });

                TxtWidth.Text = originalWidth > 0 ? originalWidth.ToString() : "--";
                TxtHeight.Text = originalHeight > 0 ? originalHeight.ToString() : "--";
                TxtWidth.Tag = originalWidth.ToString();
                TxtHeight.Tag = originalHeight.ToString();

                if (bitmap != null)
                {
                    ImgDisplay.Source = bitmap;
                    LoadingPanel.Visibility = Visibility.Collapsed;
                }
                else
                {
                    // Fallback for corrupt internal loading
                    System.Windows.MessageBox.Show("Failed to load image data.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }

                // Load associated text file
                await LoadTextContentAsync();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error loading image: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void Navigate(int delta)
        {
            if (_allImages == null || _allImages.Count <= 1) return;

            int newIndex = _currentIndex + delta;
            if (newIndex < 0 || newIndex >= _allImages.Count) return;

            _currentIndex = newIndex;
            _imagePath = _allImages[_currentIndex];
            _detectedExtension = Path.GetExtension(_imagePath).ToLower();

            // Reset edit and reveal state when navigating
            ResetEditState();
            _isLocallyRevealed = false;
            UpdatePrivacyOverlay();

            // Clear current view
            ImgDisplay.Source = null;
            LoadingPanel.Visibility = Visibility.Visible;

            // Remove old event handlers before loading new image
            TxtFileName.MouseEnter -= OnFileNameMouseEnter;
            TxtFileName.MouseLeave -= OnFileNameMouseLeave;
            TxtFileName.MouseLeftButtonDown -= OnFileNameMouseDown;

            await LoadImageAsync();
        }

        private void ResetEditState()
        {
            TxtPositive.Visibility = Visibility.Visible;
            TxtPositiveEditor.Visibility = Visibility.Collapsed;
            BtnEditPositive.Content = "✏️";
            BtnSavePositive.Visibility = Visibility.Collapsed;

            TxtBasePrompt.Visibility = Visibility.Visible;
            TxtBasePromptEditor.Visibility = Visibility.Collapsed;
            BtnEditBasePrompt.Content = "✏️";
            BtnSaveBasePrompt.Visibility = Visibility.Collapsed;

            TxtNegative.Visibility = Visibility.Visible;
            TxtNegativeEditor.Visibility = Visibility.Collapsed;
            BtnEditNegative.Content = "✏️";
            BtnSaveNegative.Visibility = Visibility.Collapsed;

            TxtDescription.Visibility = Visibility.Visible;
            TxtDescriptionEditor.Visibility = Visibility.Collapsed;
            BtnEditDescription.Content = "✏️";
            BtnSaveDescription.Visibility = Visibility.Collapsed;
            
            _originalPositive = "";
            _originalBase = "";
            _originalNegative = "";
            _originalDescription = "";
        }

        private async Task LoadTextContentAsync()
        {
            try
            {
                PromptsPanel.Visibility = Visibility.Collapsed;
                TxtPositive.Text = "No positive prompt found.";
                TxtNegative.Text = "No negative prompt found.";

                string baseName = Path.GetFileNameWithoutExtension(_imagePath);
                string? directory = Path.GetDirectoryName(_imagePath);
                if (string.IsNullOrEmpty(directory)) return;

                string txtPath = Path.Combine(directory, baseName + ".txt");

                if (File.Exists(txtPath))
                {
                    string content = await File.ReadAllTextAsync(txtPath);
                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        // Parse positive, base, negative, and description
                        // Run parsing on background thread to avoid freezing UI for large text files
                        await Task.Run(() => 
                        {
                            var lines = content.Split('\n');
                            string positivePrompt = "";
                            string basePrompt = "";
                            string negativePrompt = "";
                            string description = "";
                            bool isPositive = false;
                            bool isBase = false;
                            bool isNegative = false;
                            bool isDescription = false;
                            bool hasContent = false;

                            foreach (var line in lines)
                            {
                                string currentLine = line.Trim();
                                if (string.IsNullOrWhiteSpace(currentLine)) continue;

                                // Robust case-insensitive check
                                string lowerLine = currentLine.ToLower();

                                if (lowerLine.StartsWith("positive prompt"))
                                {
                                    isPositive = true;
                                    isBase = false;
                                    isNegative = false;
                                    isDescription = false;
                                    var colonIndex = currentLine.IndexOf(':');
                                    if (colonIndex != -1 && colonIndex < currentLine.Length - 1)
                                    {
                                        positivePrompt += currentLine.Substring(colonIndex + 1).Trim() + "\n";
                                    }
                                    continue;
                                }
                                else if (lowerLine.StartsWith("base prompt"))
                                {
                                    isBase = true;
                                    isPositive = false;
                                    isNegative = false;
                                    isDescription = false;
                                    var colonIndex = currentLine.IndexOf(':');
                                    if (colonIndex != -1 && colonIndex < currentLine.Length - 1)
                                    {
                                        basePrompt += currentLine.Substring(colonIndex + 1).Trim() + "\n";
                                    }
                                    continue;
                                }
                                else if (lowerLine.StartsWith("negative prompt"))
                                {
                                    isNegative = true;
                                    isPositive = false;
                                    isBase = false;
                                    isDescription = false;
                                    var colonIndex = currentLine.IndexOf(':');
                                    if (colonIndex != -1 && colonIndex < currentLine.Length - 1)
                                    {
                                        negativePrompt += currentLine.Substring(colonIndex + 1).Trim() + "\n";
                                    }
                                    continue;
                                }
                                else if (lowerLine.StartsWith("description"))
                                {
                                    isDescription = true;
                                    isPositive = false;
                                    isBase = false;
                                    isNegative = false;
                                    var colonIndex = currentLine.IndexOf(':');
                                    if (colonIndex != -1 && colonIndex < currentLine.Length - 1)
                                    {
                                        description += currentLine.Substring(colonIndex + 1).Trim() + "\n";
                                    }
                                    continue;
                                }

                                if (isPositive)
                                    positivePrompt += currentLine + "\n";
                                else if (isBase)
                                    basePrompt += currentLine + "\n";
                                else if (isNegative)
                                    negativePrompt += currentLine + "\n";
                                else if (isDescription)
                                    description += currentLine + "\n";
                            }

                            // Update UI on main thread
                            Dispatcher.Invoke(() =>
                            {
                                string finalPos = positivePrompt.Trim();
                                string finalBase = basePrompt.Trim();
                                string finalNeg = negativePrompt.Trim();
                                string finalDesc = description.Trim();

                                if (!string.IsNullOrEmpty(finalPos))
                                {
                                    SetInteractiveText(TxtPositive, finalPos, (System.Windows.Media.Brush)FindResource("ForegroundBrush"));
                                    hasContent = true;
                                }

                                if (!string.IsNullOrEmpty(finalBase))
                                {
                                    SetInteractiveText(TxtBasePrompt, finalBase, (System.Windows.Media.Brush)FindResource("ForegroundBrush"));
                                    BasePromptGrid.Visibility = Visibility.Visible;
                                    hasContent = true;
                                }
                                else
                                {
                                    BasePromptGrid.Visibility = Visibility.Collapsed;
                                }

                                if (!string.IsNullOrEmpty(finalNeg))
                                {
                                    SetInteractiveText(TxtNegative, finalNeg, (System.Windows.Media.Brush)FindResource("ForegroundBrush"));
                                    hasContent = true;
                                }

                                if (!string.IsNullOrEmpty(finalDesc))
                                {
                                    SetInteractiveText(TxtDescription, finalDesc, (System.Windows.Media.Brush)FindResource("ForegroundBrush")); 
                                    DescriptionGrid.Visibility = Visibility.Visible;
                                    hasContent = true;
                                }
                                else
                                {
                                    DescriptionGrid.Visibility = Visibility.Collapsed;
                                }
                                
                                if (hasContent)
                                {
                                    _originalPositive = finalPos;
                                    _originalBase = finalBase;
                                    _originalNegative = finalNeg;
                                    _originalDescription = finalDesc;
                                    PromptsPanel.Visibility = Visibility.Visible;
                                }
                            });
                        });
                    }
                }
            }
            catch { }
        }

        private void SetInteractiveText(TextBlock textBlock, string text, System.Windows.Media.Brush defaultColor)
        {
            textBlock.Inlines.Clear();
            var segments = text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            
            for (int i = 0; i < segments.Length; i++)
            {
                string segment = segments[i];
                // Preserve leading/trailing spaces for visual flow, but trim for copy
                string trimSegment = segment.Trim();
                
                if (string.IsNullOrWhiteSpace(trimSegment)) continue;

                Run run = new Run(segment); // Use original segment to keep spaces if any (though split eats commas)
                run.Foreground = defaultColor; // Use passed default color
                run.Cursor = System.Windows.Input.Cursors.Hand;
                
                // Events for interactivity
                run.MouseEnter += (s, e) => { if(s is Run r) r.Foreground = System.Windows.Media.Brushes.Yellow; };
                run.MouseLeave += (s, e) => { if(s is Run r) r.Foreground = defaultColor; };
                run.MouseLeftButtonDown += (s, e) => 
                { 
                    if(s is Run r) 
                    {
                        try 
                        {
                            System.Windows.Clipboard.SetText(trimSegment);
                            // Visual feedback: Flash Green
                            r.Foreground = System.Windows.Media.Brushes.LimeGreen;
                            Task.Delay(300).ContinueWith(_ => Dispatcher.Invoke(() => 
                            { 
                                // Reset to hover color if mouse is still over, or default if not
                                r.Foreground = r.IsMouseOver ? System.Windows.Media.Brushes.Yellow : defaultColor; 
                            }));
                        }
                        catch {}
                    }
                };

                textBlock.Inlines.Add(run);

                // Add comma back visually
                if (i < segments.Length - 1)
                {
                    textBlock.Inlines.Add(new Run(", ") { Foreground = System.Windows.Media.Brushes.Gray });
                }
            }
        }

        private void BtnCopyPositive_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(TxtPositive.Text) && TxtPositive.Text != "No positive prompt found.")
            {
                System.Windows.Clipboard.SetText(TxtPositive.Text);
                var originalContent = BtnCopyPositive.Content;
                BtnCopyPositive.Content = "✓";
                Task.Delay(1500).ContinueWith(_ => Dispatcher.Invoke(() => BtnCopyPositive.Content = "Copy"));
            }
        }

        private void BtnCopyBasePrompt_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(TxtBasePrompt.Text))
            {
                System.Windows.Clipboard.SetText(TxtBasePrompt.Text);
                BtnCopyBasePrompt.Content = "✓";
                Task.Delay(1500).ContinueWith(_ => Dispatcher.Invoke(() => BtnCopyBasePrompt.Content = "Copy"));
            }
        }

        private void BtnCopyNegative_Click(object sender, RoutedEventArgs e)
        {
             if (!string.IsNullOrEmpty(TxtNegative.Text) && TxtNegative.Text != "No negative prompt found.")
            {
                System.Windows.Clipboard.SetText(TxtNegative.Text);
                var originalContent = BtnCopyNegative.Content;
                BtnCopyNegative.Content = "✓";
                Task.Delay(1500).ContinueWith(_ => Dispatcher.Invoke(() => BtnCopyNegative.Content = "Copy"));
            }
        }

        private void BtnCopyDescription_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(TxtDescription.Text))
            {
                System.Windows.Clipboard.SetText(TxtDescription.Text);
                BtnCopyDescription.Content = "✓";
                Task.Delay(1500).ContinueWith(_ => Dispatcher.Invoke(() => BtnCopyDescription.Content = "Copy"));
            }
        }

        private void BtnEditPositive_Click(object sender, RoutedEventArgs e) => ToggleEdit(TxtPositive, TxtPositiveEditor, BtnEditPositive, _originalPositive);
        private void BtnEditBasePrompt_Click(object sender, RoutedEventArgs e) => ToggleEdit(TxtBasePrompt, TxtBasePromptEditor, BtnEditBasePrompt, _originalBase);
        private void BtnEditNegative_Click(object sender, RoutedEventArgs e) => ToggleEdit(TxtNegative, TxtNegativeEditor, BtnEditNegative, _originalNegative);
        private void BtnEditDescription_Click(object sender, RoutedEventArgs e) => ToggleEdit(TxtDescription, TxtDescriptionEditor, BtnEditDescription, _originalDescription);

        private void ToggleEdit(TextBlock display, System.Windows.Controls.TextBox editor, System.Windows.Controls.Button editBtn, string originalValue)
        {
            if (editor.Visibility == Visibility.Collapsed)
            {
                display.Visibility = Visibility.Collapsed;
                editor.Visibility = Visibility.Visible;
                editor.Text = originalValue;
                editor.Focus();
                editBtn.Content = "✕"; // Change to cancel icon
            }
            else
            {
                display.Visibility = Visibility.Visible;
                editor.Visibility = Visibility.Collapsed;
                editBtn.Content = "✏️";
                // Reset save button visibility if cancelled
                if (editor == TxtPositiveEditor) BtnSavePositive.Visibility = Visibility.Collapsed;
                if (editor == TxtBasePromptEditor) BtnSaveBasePrompt.Visibility = Visibility.Collapsed;
                if (editor == TxtNegativeEditor) BtnSaveNegative.Visibility = Visibility.Collapsed;
                if (editor == TxtDescriptionEditor) BtnSaveDescription.Visibility = Visibility.Collapsed;
                
                // Restore focus to window to re-enable keyboard navigation
                this.Focus();
            }
        }

        private void PromptEditor_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox editor)
            {
                if (editor == TxtPositiveEditor) BtnSavePositive.Visibility = editor.Text != _originalPositive ? Visibility.Visible : Visibility.Collapsed;
                else if (editor == TxtBasePromptEditor) BtnSaveBasePrompt.Visibility = editor.Text != _originalBase ? Visibility.Visible : Visibility.Collapsed;
                else if (editor == TxtNegativeEditor) BtnSaveNegative.Visibility = editor.Text != _originalNegative ? Visibility.Visible : Visibility.Collapsed;
                else if (editor == TxtDescriptionEditor) BtnSaveDescription.Visibility = editor.Text != _originalDescription ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private async void BtnSavePositive_Click(object sender, RoutedEventArgs e) => await SavePromptChange(TxtPositiveEditor.Text, "positive");
        private async void BtnSaveBasePrompt_Click(object sender, RoutedEventArgs e) => await SavePromptChange(TxtBasePromptEditor.Text, "base");
        private async void BtnSaveNegative_Click(object sender, RoutedEventArgs e) => await SavePromptChange(TxtNegativeEditor.Text, "negative");
        private async void BtnSaveDescription_Click(object sender, RoutedEventArgs e) => await SavePromptChange(TxtDescriptionEditor.Text, "description");

        private async Task SavePromptChange(string newValue, string type)
        {
            try
            {
                string baseName = Path.GetFileNameWithoutExtension(_imagePath);
                string? directory = Path.GetDirectoryName(_imagePath);
                if (string.IsNullOrEmpty(directory)) return;

                string txtPath = Path.Combine(directory, baseName + ".txt");

                if (type == "positive") _originalPositive = newValue;
                else if (type == "base") _originalBase = newValue;
                else if (type == "negative") _originalNegative = newValue;
                else if (type == "description") _originalDescription = newValue;

                string content = "";
                if (!string.IsNullOrWhiteSpace(_originalPositive)) content += $"Positive Prompt:\n{_originalPositive}\n\n";
                if (!string.IsNullOrWhiteSpace(_originalBase)) content += $"Base Prompt:\n{_originalBase}\n\n";
                if (!string.IsNullOrWhiteSpace(_originalNegative)) content += $"Negative Prompt:\n{_originalNegative}\n\n";
                if (!string.IsNullOrWhiteSpace(_originalDescription)) content += $"Description:\n{_originalDescription}\n";

                await File.WriteAllTextAsync(txtPath, content.TrimEnd());

                // Reset UI
                if (type == "positive")
                {
                    ToggleEdit(TxtPositive, TxtPositiveEditor, BtnEditPositive, _originalPositive);
                    SetInteractiveText(TxtPositive, _originalPositive, (System.Windows.Media.Brush)FindResource("ForegroundBrush"));
                }
                else if (type == "base")
                {
                    ToggleEdit(TxtBasePrompt, TxtBasePromptEditor, BtnEditBasePrompt, _originalBase);
                    SetInteractiveText(TxtBasePrompt, _originalBase, (System.Windows.Media.Brush)FindResource("ForegroundBrush"));
                }
                else if (type == "negative")
                {
                    ToggleEdit(TxtNegative, TxtNegativeEditor, BtnEditNegative, _originalNegative);
                    SetInteractiveText(TxtNegative, _originalNegative, (System.Windows.Media.Brush)FindResource("ForegroundBrush"));
                }
                else if (type == "description")
                {
                    ToggleEdit(TxtDescription, TxtDescriptionEditor, BtnEditDescription, _originalDescription);
                    SetInteractiveText(TxtDescription, _originalDescription, (System.Windows.Media.Brush)FindResource("ForegroundBrush"));
                }

                // Ensure focus is on the window to enable keyboard navigation
                this.Focus();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error saving changes: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        
        private void DragWindow(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string? dir = Path.GetDirectoryName(_imagePath);
                if (dir != null && Directory.Exists(dir))
                {
                    System.Diagnostics.Process.Start("explorer.exe", dir);
                }
            }
            catch { }
        }

        private void BtnDeleteImage_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(_imagePath) || !File.Exists(_imagePath)) return;

                var res = System.Windows.MessageBox.Show("Delete this image and its associated .txt file?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (res != MessageBoxResult.Yes) return;

                string? txtPath = null;
                try
                {
                    txtPath = Path.Combine(Path.GetDirectoryName(_imagePath) ?? "", Path.GetFileNameWithoutExtension(_imagePath) + ".txt");
                    if (File.Exists(_imagePath)) File.Delete(_imagePath);
                    if (!string.IsNullOrEmpty(txtPath) && File.Exists(txtPath)) File.Delete(txtPath);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Failed to delete: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Notify gallery to refresh if open
                try
                {
                    foreach (Window w in global::System.Windows.Application.Current.Windows)
                    {
                        if (w is GalleryWindow gw)
                        {
                            // trigger async refresh
                            _ = gw.RefreshAfterExternalChange();
                        }
                    }
                }
                catch { }

                // Close viewer after deletion
                this.Close();
            }
            catch { }
        }

        private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
            else if (e.Key == Key.Left)
            {
                Navigate(-1);
            }
            else if (e.Key == Key.Right)
            {
                Navigate(1);
            }
        }

        private void BtnPrevious_Click(object sender, RoutedEventArgs e) => Navigate(-1);

        private void BtnNext_Click(object sender, RoutedEventArgs e) => Navigate(1);

        private void MakeFileNameInteractive(string fileName)
        {
            // Store only the filename without extension
           _currentFileName = Path.GetFileNameWithoutExtension(fileName);
     TxtFileName.Cursor = System.Windows.Input.Cursors.Hand;
            TxtFileName.MouseEnter += OnFileNameMouseEnter;
   TxtFileName.MouseLeave += OnFileNameMouseLeave;
     TxtFileName.MouseLeftButtonDown += OnFileNameMouseDown;
        }

        private void OnFileNameMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
    if (sender is TextBlock tb)
              tb.Foreground = System.Windows.Media.Brushes.Yellow;
  }

      private void OnFileNameMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
     {
    if (sender is TextBlock tb)
    tb.Foreground = (System.Windows.Media.Brush)FindResource("ForegroundBrush");
 }

        private void OnFileNameMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                System.Windows.Clipboard.SetText(_currentFileName);
                // Visual feedback: Flash Green
                if (sender is TextBlock tb)
                {
                    tb.Foreground = System.Windows.Media.Brushes.LimeGreen;
                    Task.Delay(300).ContinueWith(_ => Dispatcher.Invoke(() =>
                    {
                        tb.Foreground = tb.IsMouseOver ? System.Windows.Media.Brushes.Yellow : (System.Windows.Media.Brush)FindResource("ForegroundBrush");
                    }));
                }
            }
            catch { }
        }

        private void Dimension_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is TextBlock tb)
            {
                tb.Foreground = System.Windows.Media.Brushes.Yellow;
            }
        }

        private void Dimension_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (sender is TextBlock tb)
            {
                tb.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(238, 238, 238));
            }
        }

        private void TxtWidth_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                string widthVal = TxtWidth.Tag as string ?? TxtWidth.Text;
                if (!string.IsNullOrEmpty(widthVal) && widthVal != "--")
                {
                    System.Windows.Clipboard.SetText(widthVal);
                    // Flash Green on click
                    TxtWidth.Foreground = System.Windows.Media.Brushes.LimeGreen;
                    Task.Delay(350).ContinueWith(_ => Dispatcher.Invoke(() =>
                    {
                        TxtWidth.Foreground = TxtWidth.IsMouseOver ? System.Windows.Media.Brushes.Yellow : new SolidColorBrush(System.Windows.Media.Color.FromRgb(238, 238, 238));
                    }));
                }
            }
            catch { }
        }

        private void TxtHeight_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                string heightVal = TxtHeight.Tag as string ?? TxtHeight.Text;
                if (!string.IsNullOrEmpty(heightVal) && heightVal != "--")
                {
                    System.Windows.Clipboard.SetText(heightVal);
                    // Flash Green on click
                    TxtHeight.Foreground = System.Windows.Media.Brushes.LimeGreen;
                    Task.Delay(350).ContinueWith(_ => Dispatcher.Invoke(() =>
                    {
                        TxtHeight.Foreground = TxtHeight.IsMouseOver ? System.Windows.Media.Brushes.Yellow : new SolidColorBrush(System.Windows.Media.Color.FromRgb(238, 238, 238));
                    }));
                }
            }
            catch { }
        }
    }
}
