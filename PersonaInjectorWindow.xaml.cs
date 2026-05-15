using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.ComponentModel;
using System.Windows.Data;

namespace imgsaver
{
    public partial class PersonaInjectorWindow : Window
    {
        private const string PlaceholderTag = "[character]";
        private readonly DispatcherTimer _feedbackTimer;
        private readonly DispatcherTimer _searchDebounceTimer;
        private readonly DispatcherTimer _promptSearchDebounceTimer;

        private ICollectionView _characterView;
        private ICollectionView _promptView;
        private Random _random = new Random();

        private bool _isPromptLocked = false;
        private bool _isCharacterLocked = false;
        private CharacterPersona _currentPersona = null;
        private BasePrompt _currentPrompt = null;

        public PersonaInjectorWindow()
        {
            InitializeComponent();

            // Set dynamic max height based on screen working area
            this.MaxHeight = SystemParameters.WorkArea.Height;

            // Initialize search timers
            _searchDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _searchDebounceTimer.Tick += (s, e) => { _searchDebounceTimer.Stop(); _characterView?.Refresh(); };

            _promptSearchDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _promptSearchDebounceTimer.Tick += (s, e) => { _promptSearchDebounceTimer.Stop(); _promptView?.Refresh(); };

            // Initialize feedback timer
            _feedbackTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _feedbackTimer.Tick += FeedbackTimer_Tick;

            // Load data async
            Loaded += PersonaInjectorWindow_Loaded;
        }

        private async void PersonaInjectorWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (LoadingOverlay != null)
            {
                LoadingOverlay.Visibility = Visibility.Visible;
                LoadingText.Text = "Loading data...";
                UpdateLoadingProgress(5);
            }

            await Task.Delay(200);

            try
            {
                if (LoadingText != null) LoadingText.Text = "Loading characters...";
                UpdateLoadingProgress(10);
                await Task.Run(() => CharacterManager.Load());
                UpdateLoadingProgress(30);
                await Task.Delay(100);

                if (LoadingText != null) LoadingText.Text = "Loading prompts...";
                UpdateLoadingProgress(40);
                await Task.Run(() => BasePromptManager.Load());
                UpdateLoadingProgress(60);
                await Task.Delay(100);

                if (LoadingText != null) LoadingText.Text = "Setting up UI...";
                UpdateLoadingProgress(70);
                await Task.Delay(50);

                await Dispatcher.InvokeAsync(() =>
                {
                    _characterView = CollectionViewSource.GetDefaultView(CharacterManager.GetAll());
                    _characterView.SortDescriptions.Add(new SortDescription("IsFavorite", ListSortDirection.Descending));
                    _characterView.SortDescriptions.Add(new SortDescription("ShortName", ListSortDirection.Ascending));
                    _characterView.Filter = CharacterFilter;
                    CharacterList.ItemsSource = _characterView;

                    _promptView = CollectionViewSource.GetDefaultView(BasePromptManager.GetAll());
                    _promptView.SortDescriptions.Add(new SortDescription("IsFavorite", ListSortDirection.Descending));
                    _promptView.SortDescriptions.Add(new SortDescription("LastModified", ListSortDirection.Descending));
                    _promptView.Filter = PromptFilter;
                    ComboBasePrompts.ItemsSource = _promptView;

                    UpdateLoadingProgress(85);
                });

                await Task.Delay(100);
                if (LoadingText != null) LoadingText.Text = "Almost done...";
                UpdateLoadingProgress(95);
                await Task.Delay(150);
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    System.Windows.MessageBox.Show($"Error loading data: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
            finally
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    UpdateLoadingProgress(100);
                    if (LoadingText != null) LoadingText.Text = "Ready!";
                });

                await Task.Delay(300);

                await Dispatcher.InvokeAsync(() =>
                {
                    if (LoadingOverlay != null)
                    {
                        LoadingOverlay.Visibility = Visibility.Collapsed;
                    }
                });
            }
        }

        private bool ShouldOnlyRandomizeFavorites()
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "config.txt");
                if (File.Exists(configPath))
                {
                    string[] lines = File.ReadAllLines(configPath);
                    if (lines.Length > 1) return lines[1].Trim().ToLower() == "true";
                }
            }
            catch { }
            return false;
        }

        private bool CharacterFilter(object item)
        {
            if (item is CharacterPersona persona)
            {
                string query = TxtSearch?.Text;
                if (string.IsNullOrWhiteSpace(query)) return true;
                var terms = query.ToLower().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                string target = (persona.ShortName + " " + persona.FullPersona).ToLower();
                foreach (var term in terms)
                {
                    if (!target.Contains(term)) return false;
                }
                return true;
            }
            return false;
        }

        private bool PromptFilter(object item)
        {
            if (item is BasePrompt prompt)
            {
                string query = TxtSearchPrompts?.Text;
                if (string.IsNullOrWhiteSpace(query)) return true;
                var terms = query.ToLower().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                string target = (prompt.Name + " " + prompt.PromptText).ToLower();
                foreach (var term in terms)
                {
                    if (!target.Contains(term)) return false;
                }
                return true;
            }
            return false;
        }

        private void TxtSearchPrompts_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (IsLoaded)
            {
                _promptSearchDebounceTimer.Stop();
                _promptSearchDebounceTimer.Start();
            }
        }

        private void ComboBasePrompts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ComboBasePrompts.SelectedItem is BasePrompt selectedPrompt)
            {
                _currentPrompt = selectedPrompt;
                _isSettingPrompt = true;
                TxtRawPrompt.Text = selectedPrompt.PromptText;
                _isSettingPrompt = false;
                TxtCurrentPromptName.Text = $"Source: {selectedPrompt.Name}";
                ComboBasePrompts.SelectedItem = null;
            }
        }

        private void BtnManagePrompts_Click(object sender, RoutedEventArgs e)
        {
            var editorWindow = new PromptEditorWindow();
            editorWindow.Owner = this;
            editorWindow.Closed += (s, args) =>
            {
                BasePromptManager.Load();
                _promptView = CollectionViewSource.GetDefaultView(BasePromptManager.GetAll());
                _promptView.SortDescriptions.Add(new SortDescription("IsFavorite", ListSortDirection.Descending));
                _promptView.SortDescriptions.Add(new SortDescription("LastModified", ListSortDirection.Descending));
                _promptView.Filter = PromptFilter;
                ComboBasePrompts.ItemsSource = _promptView;
            };
            editorWindow.Show();
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (IsLoaded)
            {
                _searchDebounceTimer.Stop();
                _searchDebounceTimer.Start();
            }
        }

        private void BtnInsertTag_Click(object sender, RoutedEventArgs e)
        {
            int caretIndex = TxtRawPrompt.CaretIndex;
            string currentText = TxtRawPrompt.Text;
            string newText = currentText.Insert(caretIndex, PlaceholderTag);
            TxtRawPrompt.Text = newText;
            TxtRawPrompt.CaretIndex = caretIndex + PlaceholderTag.Length;
            TxtRawPrompt.Focus();
        }

        private bool _isSettingPrompt = false;

        private void TxtRawPrompt_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TxtCopiedFeedback != null)
                TxtCopiedFeedback.Visibility = Visibility.Collapsed;

            if (!_isSettingPrompt && TxtCurrentPromptName != null)
            {
                TxtCurrentPromptName.Text = "";
                _currentPrompt = null;
            }
        }

        private void BtnManage_Click(object sender, RoutedEventArgs e)
        {
            var editorWindow = new CharacterEditorWindow();
            editorWindow.Owner = this;
            editorWindow.Closed += (s, args) =>
            {
                CharacterManager.Load();
                _characterView = CollectionViewSource.GetDefaultView(CharacterManager.GetAll());
                _characterView.SortDescriptions.Add(new SortDescription("IsFavorite", ListSortDirection.Descending));
                _characterView.SortDescriptions.Add(new SortDescription("ShortName", ListSortDirection.Ascending));
                _characterView.Filter = CharacterFilter;
                CharacterList.ItemsSource = _characterView;
            };
            editorWindow.Show();
        }

        private void BtnClearRaw_Click(object sender, RoutedEventArgs e)
        {
            TxtRawPrompt.Clear();
            TxtRawPrompt.Focus();
        }

        private void BtnPasteRaw_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (System.Windows.Clipboard.ContainsText())
                {
                    TxtRawPrompt.Text = System.Windows.Clipboard.GetText();
                    TxtRawPrompt.Focus();
                    TxtRawPrompt.CaretIndex = TxtRawPrompt.Text.Length;
                }
            }
            catch { }
        }

        private void BtnSaveRaw_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtRawPrompt.Text))
            {
                CustomMessageBox.Show("Please enter some text in the raw prompt box before saving it as a template.", "Validation", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var editorWindow = new PromptEditorWindow(initialPromptText: TxtRawPrompt.Text);
            editorWindow.Owner = this;
            editorWindow.Closed += (s, args) =>
            {
                BasePromptManager.Load();
                _promptView = CollectionViewSource.GetDefaultView(BasePromptManager.GetAll());
                _promptView.SortDescriptions.Add(new SortDescription("IsFavorite", ListSortDirection.Descending));
                _promptView.SortDescriptions.Add(new SortDescription("LastModified", ListSortDirection.Descending));
                _promptView.Filter = PromptFilter;
                ComboBasePrompts.ItemsSource = _promptView;
            };
            editorWindow.Show();
        }

        private void BtnBackupData_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
                string shareDir = Path.Combine(dataDir, "share");
                if (!Directory.Exists(dataDir)) return;
                if (!Directory.Exists(shareDir)) Directory.CreateDirectory(shareDir);
                var jsonFiles = Directory.GetFiles(dataDir, "*.json");
                if (jsonFiles.Length == 0) return;
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backupFileName = $"Data_Backup_{timestamp}.zip";
                string destinationPath = Path.Combine(shareDir, backupFileName);
                string tempDir = Path.Combine(Path.GetTempPath(), $"ImgSaver_Backup_{Guid.NewGuid()}");
                Directory.CreateDirectory(tempDir);
                try
                {
                    foreach (var file in jsonFiles)
                    {
                        string destFile = Path.Combine(tempDir, Path.GetFileName(file));
                        File.Copy(file, destFile, overwrite: true);
                    }
                    if (File.Exists(destinationPath)) File.Delete(destinationPath);
                    System.IO.Compression.ZipFile.CreateFromDirectory(tempDir, destinationPath);
                    CustomMessageBox.Show($"Backup successful!\n\nFile: {backupFileName}", "Backup Data", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                finally { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
            }
            catch (Exception ex) { CustomMessageBox.Show($"Backup failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void CharacterList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CharacterList.SelectedItem is CharacterPersona selectedPersona)
            {
                _currentPersona = selectedPersona;
                PerformInjection(selectedPersona);
                if (TxtCurrentCharacterName != null) TxtCurrentCharacterName.Text = selectedPersona.ShortName ?? "Unknown";
                CharacterList.SelectedItem = null;
            }
        }

        private async void Indicator_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border border && border.Child is TextBlock textBlock)
            {
                string originalText = textBlock.Text;
                if (string.IsNullOrWhiteSpace(originalText)) return;
                string textToCopy = originalText.Replace("Source: ", "").Trim();
                try
                {
                    System.Windows.Clipboard.SetText(textToCopy);
                    textBlock.Text = "Copied!";
                    textBlock.Foreground = System.Windows.Media.Brushes.LightGreen;
                    await Task.Delay(1000);
                    if (textBlock != null)
                    {
                        textBlock.Text = originalText;
                        textBlock.Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom("#FFD700");
                    }
                }
                catch { }
            }
        }

        private void BtnStarPrompt_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string id)
            {
                BasePromptManager.ToggleFavorite(id);
                _promptView.Refresh();
            }
        }

        private void BtnEditPrompt_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string id)
            {
                var editorWindow = new PromptEditorWindow(promptId: id);
                editorWindow.Owner = this;
                editorWindow.Closed += (s, args) =>
                {
                    BasePromptManager.Load();
                    _promptView = CollectionViewSource.GetDefaultView(BasePromptManager.GetAll());
                    _promptView.SortDescriptions.Add(new SortDescription("IsFavorite", ListSortDirection.Descending));
                    _promptView.SortDescriptions.Add(new SortDescription("LastModified", ListSortDirection.Descending));
                    _promptView.Filter = PromptFilter;
                    ComboBasePrompts.ItemsSource = _promptView;
                };
                editorWindow.Show();
            }
        }

        private void BtnStarCharacter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string id)
            {
                CharacterManager.ToggleFavorite(id);
                _characterView.Refresh();
            }
        }

        private void BtnEditCharacter_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string id)
            {
                var editorWindow = new CharacterEditorWindow(characterId: id);
                editorWindow.Owner = this;
                editorWindow.Closed += (s, args) =>
                {
                    CharacterManager.Load();
                    _characterView = CollectionViewSource.GetDefaultView(CharacterManager.GetAll());
                    _characterView.SortDescriptions.Add(new SortDescription("IsFavorite", ListSortDirection.Descending));
                    _characterView.SortDescriptions.Add(new SortDescription("ShortName", ListSortDirection.Ascending));
                    _characterView.Filter = CharacterFilter;
                    CharacterList.ItemsSource = _characterView;
                };
                editorWindow.Show();
            }
        }

        private bool _isBlinking = false;

        private void BtnRandomPrompt_Click(object sender, RoutedEventArgs e)
        {
            var pool = BasePromptManager.GetAll();
            if (ShouldOnlyRandomizeFavorites())
            {
                var favorites = pool.Where(p => p.IsFavorite).ToList();
                if (favorites.Count == 0) return;
                pool = favorites;
            }
            if (pool.Count == 0) return;
            var randomPrompt = pool[_random.Next(pool.Count)];
            _currentPrompt = randomPrompt;
            _isSettingPrompt = true;
            TxtRawPrompt.Text = randomPrompt.PromptText;
            _isSettingPrompt = false;
            TxtCurrentPromptName.Text = $"Source: {randomPrompt.Name}";
        }

        private void BtnRandomCharacter_Click(object sender, RoutedEventArgs e)
        {
            var pool = CharacterManager.GetAll();
            if (ShouldOnlyRandomizeFavorites())
            {
                var favorites = pool.Where(c => c.IsFavorite).ToList();
                if (favorites.Count == 0) return;
                pool = favorites;
            }
            if (pool.Count == 0) return;
            var randomCharacter = pool[_random.Next(pool.Count)];
            _currentPersona = randomCharacter;
            PerformInjection(randomCharacter);
            if (TxtCurrentCharacterName != null) TxtCurrentCharacterName.Text = randomCharacter.ShortName ?? "Unknown";
        }

        private void BtnRandomBoth_Click(object sender, RoutedEventArgs e) => PerformRandomBoth();

        public void PerformRandomBoth()
        {
            var promptPool = BasePromptManager.GetAll();
            var charPool = CharacterManager.GetAll();
            if (ShouldOnlyRandomizeFavorites())
            {
                var favPrompts = promptPool.Where(p => p.IsFavorite).ToList();
                var favChars = charPool.Where(c => c.IsFavorite).ToList();
                if (favPrompts.Count == 0 || favChars.Count == 0) return;
                promptPool = favPrompts; charPool = favChars;
            }
            if (promptPool.Count == 0 || charPool.Count == 0) return;
            if (!_isPromptLocked)
            {
                var randomPrompt = promptPool[_random.Next(promptPool.Count)];
                _currentPrompt = randomPrompt;
                _isSettingPrompt = true;
                TxtRawPrompt.Text = randomPrompt.PromptText;
                _isSettingPrompt = false;
                TxtCurrentPromptName.Text = $"Source: {randomPrompt.Name}";
            }
            if (!_isCharacterLocked)
            {
                var randomCharacter = charPool[_random.Next(charPool.Count)];
                _currentPersona = randomCharacter;
                if (TxtCurrentCharacterName != null) TxtCurrentCharacterName.Text = randomCharacter.ShortName ?? "Unknown";
            }
            if (_currentPersona != null) PerformInjection(_currentPersona);
            string outputText = TxtFinalOutput.Text;
            if (!string.IsNullOrWhiteSpace(outputText) && !outputText.StartsWith("Select a character") && !outputText.StartsWith("⚠"))
            {
                try
                {
                    System.Windows.Clipboard.SetText(outputText);
                    string characterName = _currentPersona?.ShortName ?? "Unknown";
                    string promptName = _currentPrompt?.Name ?? "Unknown";
                    ClipboardMetadata.Set(characterName, promptName);
                    ShowCopyFeedback();
                }
                catch { }
            }
        }

        private void BtnCopyTitle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string text)
            {
                try { if (!string.IsNullOrEmpty(text)) System.Windows.Clipboard.SetText(text); } catch { }
            }
        }

        private void PerformInjection(CharacterPersona persona)
        {
            string rawPrompt = TxtRawPrompt.Text;
            if (!rawPrompt.Contains(PlaceholderTag))
            {
                TxtFinalOutput.Text = $"⚠ No '{PlaceholderTag}' placeholder found in the raw prompt.\n\nClick the '📌 Insert [character]' button to add the placeholder at your cursor position.";
                TxtFinalOutput.Foreground = (System.Windows.Media.Brush)FindResource("WarningBrush");
                return;
            }
            string personaText = persona.FullPersona;
            if (ChkNameOnly.IsChecked == true)
            {
                int fromIndex = personaText.IndexOf(" from ", StringComparison.OrdinalIgnoreCase);
                if (fromIndex > 0) personaText = personaText.Substring(0, fromIndex).Trim();
                else
                {
                    int commaIndex = personaText.IndexOf(',');
                    if (commaIndex > 0) personaText = personaText.Substring(0, commaIndex).Trim();
                }
            }
            string result = rawPrompt.Replace(PlaceholderTag, personaText);
            TxtFinalOutput.Text = result;
            TxtFinalOutput.Foreground = (System.Windows.Media.Brush)FindResource("ForegroundBrush");
            ResetCopyFeedback();
        }

        private void OutputBorder_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            string outputText = TxtFinalOutput.Text;
            if (string.IsNullOrWhiteSpace(outputText) || outputText.StartsWith("Select a character") || outputText.StartsWith("⚠")) return;
            try
            {
                System.Windows.Clipboard.SetText(outputText);
                string characterName = TxtCurrentCharacterName?.Text ?? "";
                string promptName = TxtCurrentPromptName?.Text ?? "";
                if (promptName.StartsWith("Source: ")) promptName = promptName.Replace("Source: ", "").Trim();
                ClipboardMetadata.Set(characterName, promptName);
                ShowCopyFeedback();
            }
            catch { }
        }

        private void ShowCopyFeedback()
        {
            TxtOutputLabel.Visibility = Visibility.Collapsed;
            TxtCopiedFeedback.Visibility = Visibility.Visible;
            AnimateGreenBlink();
        }

        private void AnimateGreenBlink()
        {
            _isBlinking = true;
            _feedbackTimer.Stop();
            _feedbackTimer.Interval = TimeSpan.FromMilliseconds(500);
            OutputBorder.BorderBrush = (System.Windows.Media.Brush)FindResource("SuccessBrush");
            OutputBorder.BorderThickness = new Thickness(2);
            _feedbackTimer.Start();
        }

        private void FeedbackTimer_Tick(object? sender, EventArgs e)
        {
            _feedbackTimer.Stop();
            _isBlinking = false;
            if (OutputBorder.IsMouseOver)
            {
                OutputBorder.BorderBrush = (System.Windows.Media.Brush)FindResource("WarningBrush");
                OutputBorder.BorderThickness = new Thickness(2);
            }
            else
            {
                OutputBorder.BorderBrush = (System.Windows.Media.Brush)FindResource("BorderBrush");
                OutputBorder.BorderThickness = new Thickness(1);
            }
        }

        private void ResetCopyFeedback()
        {
            TxtOutputLabel.Visibility = Visibility.Visible;
            TxtCopiedFeedback.Visibility = Visibility.Collapsed;
        }

        private void OutputBorder_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isBlinking) return;
            string outputText = TxtFinalOutput.Text;
            if (!string.IsNullOrWhiteSpace(outputText) && !outputText.StartsWith("Select a character") && !outputText.StartsWith("⚠"))
            {
                OutputBorder.BorderBrush = (System.Windows.Media.Brush)FindResource("WarningBrush");
                OutputBorder.BorderThickness = new Thickness(2);
            }
        }

        private void OutputBorder_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isBlinking) return;
            OutputBorder.BorderBrush = (System.Windows.Media.Brush)FindResource("BorderBrush");
            OutputBorder.BorderThickness = new Thickness(1);
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            else DragMove();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        protected override void OnClosing(CancelEventArgs e)
        {
            ComboBasePrompts.ItemsSource = null;
            CharacterList.ItemsSource = null;
            CharacterManager.Unload();
            BasePromptManager.Unload();
            base.OnClosing(e);
        }

        private void UpdateLoadingProgress(int percentage)
        {
            Dispatcher.Invoke(() =>
            {
                if (ProgressBar != null) ProgressBar.Width = (200 * percentage) / 100.0;
            });
        }

        private void BtnLockPrompt_Click(object sender, RoutedEventArgs e)
        {
            _isPromptLocked = !_isPromptLocked;
            TxtLockPromptIcon.Text = _isPromptLocked ? "🔒" : "🔓";
            BtnLockPrompt.Background = _isPromptLocked ? (System.Windows.Media.Brush)FindResource("SelectedBrush") : System.Windows.Media.Brushes.Transparent;
        }

        private void BtnLockCharacter_Click(object sender, RoutedEventArgs e)
        {
            _isCharacterLocked = !_isCharacterLocked;
            TxtLockCharacterIcon.Text = _isCharacterLocked ? "🔒" : "🔓";
            BtnLockCharacter.Background = _isCharacterLocked ? (System.Windows.Media.Brush)FindResource("SelectedBrush") : System.Windows.Media.Brushes.Transparent;
        }
    }
}