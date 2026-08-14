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
        private const string ExtraPlaceholderTag = "[extra]";
        private readonly DispatcherTimer _feedbackTimer;
        private readonly DispatcherTimer _searchDebounceTimer;
        private readonly DispatcherTimer _promptSearchDebounceTimer;
        private readonly DispatcherTimer _extraSearchDebounceTimer;
        private readonly DispatcherTimer _extraPromptSearchDebounceTimer;
        private readonly DispatcherTimer _extraFeedbackTimer;

        private ICollectionView _characterView;
        private ICollectionView _promptView;
        private ICollectionView _extraView;
        private ICollectionView _extraPromptView;
        private Random _random = new Random();
        private readonly Dictionary<string, HashSet<string>> _randomHistoryByPool = new();

        private bool _isPromptLocked = false;
        private bool _isCharacterLocked = false;
        private bool _isExtraPromptLocked = false;
        private bool _isExtraLocked = false;
        private bool _isUsingCustomExtra = false;
        private string _currentCustomExtraText = "";
        private CharacterPersona _currentPersona = null;
        private BasePrompt _currentPrompt = null;
        private ExtraItem _currentExtra = null;
        private ExtraPrompt _currentExtraPrompt = null;

        public PersonaInjectorWindow()
        {
            InitializeComponent();
            LanguageManager.ApplyWindowLanguage(this);

            // Set dynamic max height based on screen working area
            this.MaxHeight = SystemParameters.WorkArea.Height;

            // Initialize search timers
            _searchDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _searchDebounceTimer.Tick += (s, e) => { _searchDebounceTimer.Stop(); _characterView?.Refresh(); };

            _promptSearchDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _promptSearchDebounceTimer.Tick += (s, e) => { _promptSearchDebounceTimer.Stop(); _promptView?.Refresh(); };

            _extraSearchDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _extraSearchDebounceTimer.Tick += (s, e) => { _extraSearchDebounceTimer.Stop(); _extraView?.Refresh(); };

            _extraPromptSearchDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            _extraPromptSearchDebounceTimer.Tick += (s, e) => { _extraPromptSearchDebounceTimer.Stop(); _extraPromptView?.Refresh(); };

            // Initialize feedback timer
            _feedbackTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _feedbackTimer.Tick += FeedbackTimer_Tick;

            _extraFeedbackTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _extraFeedbackTimer.Tick += ExtraFeedbackTimer_Tick;

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
                UpdateLoadingProgress(55);
                await Task.Delay(100);

                if (LoadingText != null) LoadingText.Text = "Loading extras...";
                UpdateLoadingProgress(60);
                await Task.Run(() =>
                {
                    ExtraManager.Load();
                    ExtraPromptManager.Load();
                });
                UpdateLoadingProgress(70);
                await Task.Delay(100);

                if (LoadingText != null) LoadingText.Text = "Setting up UI...";
                UpdateLoadingProgress(75);
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

                    _extraView = CollectionViewSource.GetDefaultView(ExtraManager.GetAll());
                    _extraView.SortDescriptions.Add(new SortDescription("IsFavorite", ListSortDirection.Descending));
                    _extraView.SortDescriptions.Add(new SortDescription("ShortName", ListSortDirection.Ascending));
                    _extraView.Filter = ExtraFilter;
                    ExtraList.ItemsSource = _extraView;

                    _extraPromptView = CollectionViewSource.GetDefaultView(ExtraPromptManager.GetAll());
                    _extraPromptView.SortDescriptions.Add(new SortDescription("IsFavorite", ListSortDirection.Descending));
                    _extraPromptView.SortDescriptions.Add(new SortDescription("LastModified", ListSortDirection.Descending));
                    _extraPromptView.Filter = ExtraPromptFilter;
                    ExtraBasePrompts.ItemsSource = _extraPromptView;

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
                string configPath = DataPathManager.GetSettingsFilePath("config.txt");
                if (File.Exists(configPath))
                {
                    string[] lines = File.ReadAllLines(configPath);
                    if (lines.Length > 1) return lines[1].Trim().ToLower() == "true";
                }
            }
            catch { }
            return false;
        }

        private List<T> ApplyFavoriteRandomPool<T>(IEnumerable<T> source, Func<T, bool> isFavorite)
        {
            var pool = source?.ToList() ?? new List<T>();
            if (!ShouldOnlyRandomizeFavorites()) return pool;

            var favorites = pool.Where(isFavorite).ToList();
            return favorites.Count > 0 ? favorites : pool;
        }

        private T PickRandomNonRepeating<T>(IList<T> pool, string poolKey, Func<T, string> getId)
        {
            if (!_randomHistoryByPool.TryGetValue(poolKey, out var history))
            {
                history = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _randomHistoryByPool[poolKey] = history;
            }

            var available = pool
                .Where(item => !history.Contains(getId(item) ?? ""))
                .ToList();

            if (available.Count == 0)
            {
                history.Clear();
                available = pool.ToList();
            }

            var selected = available[_random.Next(available.Count)];
            string id = getId(selected) ?? "";
            if (!string.IsNullOrWhiteSpace(id)) history.Add(id);
            return selected;
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

        private bool ExtraFilter(object item)
        {
            if (item is ExtraItem extra)
            {
                string query = TxtExtraSearch?.Text;
                if (string.IsNullOrWhiteSpace(query)) return true;
                var terms = query.ToLower().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                string target = (extra.ShortName + " " + extra.Text).ToLower();
                foreach (var term in terms)
                {
                    if (!target.Contains(term)) return false;
                }
                return true;
            }
            return false;
        }

        private bool ExtraPromptFilter(object item)
        {
            if (item is ExtraPrompt prompt)
            {
                string query = TxtExtraSearchPrompts?.Text;
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

        private void TxtExtraSearchPrompts_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (IsLoaded)
            {
                _extraPromptSearchDebounceTimer.Stop();
                _extraPromptSearchDebounceTimer.Start();
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

        private void ExtraBasePrompts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ExtraBasePrompts.SelectedItem is ExtraPrompt selectedPrompt)
            {
                _currentExtraPrompt = selectedPrompt;
                _isSettingExtraPrompt = true;
                TxtExtraRawPrompt.Text = selectedPrompt.PromptText;
                _isSettingExtraPrompt = false;
                TxtCurrentExtraPromptName.Text = $"Source: {selectedPrompt.Name}";
                ExtraBasePrompts.SelectedItem = null;
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

        private void BtnManageExtraPrompts_Click(object sender, RoutedEventArgs e)
        {
            var editorWindow = new ExtraPromptEditorWindow();
            editorWindow.Owner = this;
            editorWindow.Closed += (s, args) => RefreshExtraPrompts();
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

        private void TxtExtraSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (IsLoaded)
            {
                _extraSearchDebounceTimer.Stop();
                _extraSearchDebounceTimer.Start();
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

        private void BtnInsertExtraTag_Click(object sender, RoutedEventArgs e)
        {
            int caretIndex = TxtExtraRawPrompt.CaretIndex;
            string currentText = TxtExtraRawPrompt.Text;
            string newText = currentText.Insert(caretIndex, ExtraPlaceholderTag);
            TxtExtraRawPrompt.Text = newText;
            TxtExtraRawPrompt.CaretIndex = caretIndex + ExtraPlaceholderTag.Length;
            TxtExtraRawPrompt.Focus();
        }

        private bool _isSettingPrompt = false;
        private bool _isSettingExtraPrompt = false;

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

        private void TxtExtraRawPrompt_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TxtExtraCopiedFeedback != null)
                TxtExtraCopiedFeedback.Visibility = Visibility.Collapsed;

            if (!_isSettingExtraPrompt && TxtCurrentExtraPromptName != null)
            {
                TxtCurrentExtraPromptName.Text = "";
                _currentExtraPrompt = null;
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

        private void BtnManageExtra_Click(object sender, RoutedEventArgs e)
        {
            var editorWindow = new ExtraItemEditorWindow();
            editorWindow.Owner = this;
            editorWindow.Closed += (s, args) => RefreshExtras();
            editorWindow.Show();
        }

        private void BtnClearRaw_Click(object sender, RoutedEventArgs e)
        {
            TxtRawPrompt.Clear();
            TxtRawPrompt.Focus();
        }

        private void BtnExtraClearRaw_Click(object sender, RoutedEventArgs e)
        {
            TxtExtraRawPrompt.Clear();
            TxtExtraRawPrompt.Focus();
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

        private void BtnExtraPasteRaw_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (System.Windows.Clipboard.ContainsText())
                {
                    TxtExtraRawPrompt.Text = System.Windows.Clipboard.GetText();
                    TxtExtraRawPrompt.Focus();
                    TxtExtraRawPrompt.CaretIndex = TxtExtraRawPrompt.Text.Length;
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

        private void BtnExtraSaveRaw_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtExtraRawPrompt.Text))
            {
                CustomMessageBox.Show("Please enter some text in the raw prompt box before saving it as an extra template.", "Validation", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var editorWindow = new ExtraPromptEditorWindow(initialPromptText: TxtExtraRawPrompt.Text);
            editorWindow.Owner = this;
            editorWindow.Closed += (s, args) => RefreshExtraPrompts();
            editorWindow.Show();
        }

        private void BtnBackupData_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string dataDir = DataPathManager.SharedPromptDataDirectory;
                string shareDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "share");
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

        private void BtnExtraBackupData_Click(object sender, RoutedEventArgs e)
        {
            BtnBackupData_Click(sender, e);
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

        private void ExtraList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ExtraList.SelectedItem is ExtraItem selectedExtra)
            {
                _isUsingCustomExtra = false;
                _currentCustomExtraText = "";
                _currentExtra = selectedExtra;
                PerformExtraInjection(selectedExtra);
                SaveCurrentExtraSelection();
                if (TxtCurrentExtraName != null) TxtCurrentExtraName.Text = selectedExtra.ShortName ?? "Unknown";
                ExtraList.SelectedItem = null;
            }
        }

        private void BtnUseCustomExtra_Click(object sender, RoutedEventArgs e)
        {
            UseCustomExtraText();
        }

        private void TxtCustomExtraText_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsLoaded) return;
            if (_isUsingCustomExtra) UseCustomExtraText();
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

        private void BtnStarExtraPrompt_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string id)
            {
                ExtraPromptManager.ToggleFavorite(id);
                _extraPromptView.Refresh();
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

        private void BtnEditExtraPrompt_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string id)
            {
                var editorWindow = new ExtraPromptEditorWindow(promptId: id);
                editorWindow.Owner = this;
                editorWindow.Closed += (s, args) => RefreshExtraPrompts();
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

        private void BtnStarExtra_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string id)
            {
                ExtraManager.ToggleFavorite(id);
                _extraView.Refresh();
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

        private void BtnEditExtra_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string id)
            {
                var editorWindow = new ExtraItemEditorWindow(extraId: id);
                editorWindow.Owner = this;
                editorWindow.Closed += (s, args) => RefreshExtras();
                editorWindow.Show();
            }
        }

        private bool _isBlinking = false;

        private void BtnRandomPrompt_Click(object sender, RoutedEventArgs e)
        {
            var pool = ApplyFavoriteRandomPool(BasePromptManager.GetAll(), p => p.IsFavorite);
            if (pool.Count == 0) return;
            var randomPrompt = PickRandomNonRepeating(pool, "basePrompt", p => p.Id);
            _currentPrompt = randomPrompt;
            _isSettingPrompt = true;
            TxtRawPrompt.Text = randomPrompt.PromptText;
            _isSettingPrompt = false;
            TxtCurrentPromptName.Text = $"Source: {randomPrompt.Name}";
        }

        private void BtnExtraRandomPrompt_Click(object sender, RoutedEventArgs e)
        {
            var pool = ApplyFavoriteRandomPool(ExtraPromptManager.GetAll(), p => p.IsFavorite);
            if (pool.Count == 0) return;
            var randomPrompt = PickRandomNonRepeating(pool, "extraPrompt", p => p.Id);
            _currentExtraPrompt = randomPrompt;
            _isSettingExtraPrompt = true;
            TxtExtraRawPrompt.Text = randomPrompt.PromptText;
            _isSettingExtraPrompt = false;
            TxtCurrentExtraPromptName.Text = $"Source: {randomPrompt.Name}";
        }

        private void BtnRandomCharacter_Click(object sender, RoutedEventArgs e)
        {
            var pool = ApplyFavoriteRandomPool(CharacterManager.GetAll(), c => c.IsFavorite);
            if (pool.Count == 0) return;
            var randomCharacter = PickRandomNonRepeating(pool, "character", c => c.Id);
            _currentPersona = randomCharacter;
            PerformInjection(randomCharacter);
            if (TxtCurrentCharacterName != null) TxtCurrentCharacterName.Text = randomCharacter.ShortName ?? "Unknown";
        }

        private void BtnRandomExtra_Click(object sender, RoutedEventArgs e)
        {
            var pool = ApplyFavoriteRandomPool(ExtraManager.GetAll(), c => c.IsFavorite);
            if (pool.Count == 0) return;
            var randomExtra = PickRandomNonRepeating(pool, "extra", c => c.Id);
            _currentExtra = randomExtra;
            PerformExtraInjection(randomExtra);
            SaveCurrentExtraSelection();
            if (TxtCurrentExtraName != null) TxtCurrentExtraName.Text = randomExtra.ShortName ?? "Unknown";
        }

        private void BtnRandomBoth_Click(object sender, RoutedEventArgs e) => PerformRandomBoth();

        private void BtnRandomExtraBoth_Click(object sender, RoutedEventArgs e) => PerformRandomExtraBoth();

        public void PerformRandomForCurrentTab()
        {
            bool preserveMiniClipTitle = ShouldPreserveMiniClipTitleOnSpiSync();
            if (MainTabControl?.SelectedIndex == 1) PerformRandomExtraBoth(preserveMiniClipTitle);
            else PerformRandomBoth(preserveMiniClipTitle);
        }

        public void PerformRandomBoth(bool preserveMiniClipTitle = false)
        {
            var promptPool = ApplyFavoriteRandomPool(BasePromptManager.GetAll(), p => p.IsFavorite);
            var charPool = ApplyFavoriteRandomPool(CharacterManager.GetAll(), c => c.IsFavorite);
            if (promptPool.Count == 0 || charPool.Count == 0) return;
            if (!_isPromptLocked)
            {
                if (!preserveMiniClipTitle)
                {
                    var randomPrompt = PickRandomNonRepeating(promptPool, "basePrompt", p => p.Id);
                    _currentPrompt = randomPrompt;
                    _isSettingPrompt = true;
                    TxtRawPrompt.Text = randomPrompt.PromptText;
                    _isSettingPrompt = false;
                    TxtCurrentPromptName.Text = $"Source: {randomPrompt.Name}";
                }
            }
            if (!_isCharacterLocked)
            {
                var randomCharacter = PickRandomNonRepeating(charPool, "character", c => c.Id);
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
                    string characterName = _currentPersona?.ShortName ?? "";
                    if (characterName.Equals("Unknown", StringComparison.OrdinalIgnoreCase)) characterName = "";
                    string promptName = _currentPrompt?.Name ?? "";
                    if (promptName.Equals("Unknown", StringComparison.OrdinalIgnoreCase)) promptName = "";
                    ClipboardMetadata.Set(characterName, promptName, preserveMiniClipTitle);
                    ShowCopyFeedback();
                }
                catch { }
            }
        }

        public void PerformRandomExtraBoth(bool preserveMiniClipTitle = false)
        {
            var promptPool = ApplyFavoriteRandomPool(ExtraPromptManager.GetAll(), p => p.IsFavorite);
            var extraPool = ApplyFavoriteRandomPool(ExtraManager.GetAll(), c => c.IsFavorite);
            if (promptPool.Count == 0 || extraPool.Count == 0) return;
            if (!_isExtraPromptLocked)
            {
                if (!preserveMiniClipTitle)
                {
                    var randomPrompt = PickRandomNonRepeating(promptPool, "extraPrompt", p => p.Id);
                    _currentExtraPrompt = randomPrompt;
                    _isSettingExtraPrompt = true;
                    TxtExtraRawPrompt.Text = randomPrompt.PromptText;
                    _isSettingExtraPrompt = false;
                    TxtCurrentExtraPromptName.Text = $"Source: {randomPrompt.Name}";
                }
            }
            if (!_isExtraLocked)
            {
                var randomExtra = PickRandomNonRepeating(extraPool, "extra", c => c.Id);
                _isUsingCustomExtra = false;
                _currentCustomExtraText = "";
                _currentExtra = randomExtra;
                if (TxtCurrentExtraName != null) TxtCurrentExtraName.Text = randomExtra.ShortName ?? "Unknown";
            }
            if (_currentExtra != null)
            {
                PerformExtraInjection(_currentExtra);
                SaveCurrentExtraSelection();
            }
            string outputText = TxtExtraFinalOutput.Text;
            if (!string.IsNullOrWhiteSpace(outputText) && !outputText.StartsWith("Select an extra") && !outputText.StartsWith("⚠"))
            {
                try
                {
                    System.Windows.Clipboard.SetText(outputText);
                    string extraName = _currentExtra?.ShortName ?? "";
                    if (extraName.Equals("Unknown", StringComparison.OrdinalIgnoreCase)) extraName = "";
                    string promptName = _currentExtraPrompt?.Name ?? "";
                    if (promptName.Equals("Unknown", StringComparison.OrdinalIgnoreCase)) promptName = "";
                    ClipboardMetadata.Set(extraName, promptName, preserveMiniClipTitle);
                    ShowExtraCopyFeedback();
                }
                catch { }
            }
        }

        private bool ShouldPreserveMiniClipTitleOnSpiSync()
        {
            try
            {
                string configPath = DataPathManager.GetSettingsFilePath("config.txt");
                if (!File.Exists(configPath)) return false;
                string[] lines = File.ReadAllLines(configPath);
                return lines.Length > 9 && lines[9].Trim().ToLower() == "true";
            }
            catch { }
            return false;
        }

        public bool TryGetCurrentExtraText(out string extraText, out string errorMessage)
        {
            extraText = "";
            errorMessage = "";

            if (_isUsingCustomExtra)
            {
                extraText = GetPreparedExtraText(_currentCustomExtraText);
                if (string.IsNullOrWhiteSpace(extraText))
                {
                    errorMessage = "The custom Extra text is empty.";
                    return false;
                }

                return true;
            }

            if (_currentExtra == null || string.IsNullOrWhiteSpace(_currentExtra.Text))
            {
                errorMessage = "Persona Injector must be open and an Extra item must be selected.";
                return false;
            }

            extraText = GetPreparedExtraText(_currentExtra.Text);

            if (string.IsNullOrWhiteSpace(extraText))
            {
                errorMessage = "The selected Extra text is empty.";
                return false;
            }

            return true;
        }

        private void SaveCurrentExtraSelection()
        {
            if (_isUsingCustomExtra)
            {
                SaveCurrentCustomExtraSelection();
                return;
            }

            if (_currentExtra == null) return;
            LastExtraSelectionStore.Save(_currentExtra, ChkExtraNameOnly.IsChecked == true);
        }

        private void ChkExtraNameOnly_Changed(object sender, RoutedEventArgs e)
        {
            SaveCurrentExtraSelection();
            if (_isUsingCustomExtra) PerformExtraInjection(_currentCustomExtraText);
            else if (_currentExtra != null) PerformExtraInjection(_currentExtra);
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

        private void PerformExtraInjection(ExtraItem extra)
        {
            PerformExtraInjection(extra.Text);
        }

        private void PerformExtraInjection(string extraText)
        {
            string rawPrompt = TxtExtraRawPrompt.Text;
            if (!rawPrompt.Contains(ExtraPlaceholderTag))
            {
                TxtExtraFinalOutput.Text = $"⚠ No '{ExtraPlaceholderTag}' placeholder found in the raw prompt.\n\nClick the '📌 Insert [extra]' button to add the placeholder at your cursor position.";
                TxtExtraFinalOutput.Foreground = (System.Windows.Media.Brush)FindResource("WarningBrush");
                return;
            }
            extraText = GetPreparedExtraText(extraText);
            string result = rawPrompt.Replace(ExtraPlaceholderTag, extraText);
            TxtExtraFinalOutput.Text = result;
            TxtExtraFinalOutput.Foreground = (System.Windows.Media.Brush)FindResource("ForegroundBrush");
            ResetExtraCopyFeedback();
        }

        private void UseCustomExtraText()
        {
            string customText = TxtCustomExtraText?.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(customText))
            {
                TxtExtraFinalOutput.Text = "Enter custom extra text first, or select an extra from the library above.";
                TxtExtraFinalOutput.Foreground = (System.Windows.Media.Brush)FindResource("WarningBrush");
                return;
            }

            _isUsingCustomExtra = true;
            _currentCustomExtraText = customText;
            _currentExtra = null;
            if (TxtCurrentExtraName != null) TxtCurrentExtraName.Text = "Custom Extra";
            PerformExtraInjection(customText);
            SaveCurrentCustomExtraSelection();
        }

        private void SaveCurrentCustomExtraSelection()
        {
            if (string.IsNullOrWhiteSpace(_currentCustomExtraText)) return;
            LastExtraSelectionStore.Save("", "Custom Extra", _currentCustomExtraText, ChkExtraNameOnly.IsChecked == true);
        }

        private string GetPreparedExtraText(string extraText)
        {
            return LastExtraSelectionStore.ApplyTextOnly(extraText ?? "", ChkExtraNameOnly.IsChecked == true);
        }

        private void OutputBorder_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            string outputText = TxtFinalOutput.Text;
            if (string.IsNullOrWhiteSpace(outputText) || outputText.StartsWith("Select a character") || outputText.StartsWith("⚠")) return;
            try
            {
                System.Windows.Clipboard.SetText(outputText);
                string characterName = TxtCurrentCharacterName?.Text ?? "";
                if (characterName.Equals("Unknown", StringComparison.OrdinalIgnoreCase)) characterName = "";
                string promptName = _currentPrompt?.Name ?? (TxtCurrentPromptName?.Text ?? "");
                if (promptName.StartsWith("Source: ")) promptName = promptName.Replace("Source: ", "").Trim();
                if (promptName.Equals("Unknown", StringComparison.OrdinalIgnoreCase)) promptName = "";
                ClipboardMetadata.Set(characterName, promptName);
                ShowCopyFeedback();
            }
            catch { }
        }

        private void ExtraOutputBorder_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            string outputText = TxtExtraFinalOutput.Text;
            if (string.IsNullOrWhiteSpace(outputText) || outputText.StartsWith("Select an extra") || outputText.StartsWith("⚠")) return;
            try
            {
                System.Windows.Clipboard.SetText(outputText);
                string extraName = TxtCurrentExtraName?.Text ?? "";
                if (extraName.Equals("Unknown", StringComparison.OrdinalIgnoreCase)) extraName = "";
                string promptName = _currentExtraPrompt?.Name ?? (TxtCurrentExtraPromptName?.Text ?? "");
                if (promptName.StartsWith("Source: ")) promptName = promptName.Replace("Source: ", "").Trim();
                if (promptName.Equals("Unknown", StringComparison.OrdinalIgnoreCase)) promptName = "";
                ClipboardMetadata.Set(extraName, promptName);
                ShowExtraCopyFeedback();
            }
            catch { }
        }

        private void ShowCopyFeedback()
        {
            TxtOutputLabel.Visibility = Visibility.Collapsed;
            TxtCopiedFeedback.Visibility = Visibility.Visible;
            AnimateGreenBlink();
        }

        private void ShowExtraCopyFeedback()
        {
            TxtExtraOutputLabel.Visibility = Visibility.Collapsed;
            TxtExtraCopiedFeedback.Visibility = Visibility.Visible;
            AnimateExtraGreenBlink();
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

        private bool _isExtraBlinking = false;

        private void AnimateExtraGreenBlink()
        {
            _isExtraBlinking = true;
            _extraFeedbackTimer.Stop();
            _extraFeedbackTimer.Interval = TimeSpan.FromMilliseconds(500);
            ExtraOutputBorder.BorderBrush = (System.Windows.Media.Brush)FindResource("SuccessBrush");
            ExtraOutputBorder.BorderThickness = new Thickness(2);
            _extraFeedbackTimer.Start();
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

        private void ExtraFeedbackTimer_Tick(object? sender, EventArgs e)
        {
            _extraFeedbackTimer.Stop();
            _isExtraBlinking = false;
            if (ExtraOutputBorder.IsMouseOver)
            {
                ExtraOutputBorder.BorderBrush = (System.Windows.Media.Brush)FindResource("WarningBrush");
                ExtraOutputBorder.BorderThickness = new Thickness(2);
            }
            else
            {
                ExtraOutputBorder.BorderBrush = (System.Windows.Media.Brush)FindResource("BorderBrush");
                ExtraOutputBorder.BorderThickness = new Thickness(1);
            }
        }

        private void ResetCopyFeedback()
        {
            TxtOutputLabel.Visibility = Visibility.Visible;
            TxtCopiedFeedback.Visibility = Visibility.Collapsed;
        }

        private void ResetExtraCopyFeedback()
        {
            TxtExtraOutputLabel.Visibility = Visibility.Visible;
            TxtExtraCopiedFeedback.Visibility = Visibility.Collapsed;
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

        private void ExtraOutputBorder_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isExtraBlinking) return;
            string outputText = TxtExtraFinalOutput.Text;
            if (!string.IsNullOrWhiteSpace(outputText) && !outputText.StartsWith("Select an extra") && !outputText.StartsWith("⚠"))
            {
                ExtraOutputBorder.BorderBrush = (System.Windows.Media.Brush)FindResource("WarningBrush");
                ExtraOutputBorder.BorderThickness = new Thickness(2);
            }
        }

        private void OutputBorder_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isBlinking) return;
            OutputBorder.BorderBrush = (System.Windows.Media.Brush)FindResource("BorderBrush");
            OutputBorder.BorderThickness = new Thickness(1);
        }

        private void ExtraOutputBorder_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isExtraBlinking) return;
            ExtraOutputBorder.BorderBrush = (System.Windows.Media.Brush)FindResource("BorderBrush");
            ExtraOutputBorder.BorderThickness = new Thickness(1);
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
            ExtraBasePrompts.ItemsSource = null;
            ExtraList.ItemsSource = null;
            CharacterManager.Unload();
            BasePromptManager.Unload();
            ExtraManager.Unload();
            ExtraPromptManager.Unload();
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

        private void BtnExtraLockPrompt_Click(object sender, RoutedEventArgs e)
        {
            _isExtraPromptLocked = !_isExtraPromptLocked;
            TxtExtraLockPromptIcon.Text = _isExtraPromptLocked ? "🔒" : "🔓";
            BtnExtraLockPrompt.Background = _isExtraPromptLocked ? (System.Windows.Media.Brush)FindResource("SelectedBrush") : System.Windows.Media.Brushes.Transparent;
        }

        private void BtnLockExtra_Click(object sender, RoutedEventArgs e)
        {
            _isExtraLocked = !_isExtraLocked;
            TxtLockExtraIcon.Text = _isExtraLocked ? "🔒" : "🔓";
            BtnLockExtra.Background = _isExtraLocked ? (System.Windows.Media.Brush)FindResource("SelectedBrush") : System.Windows.Media.Brushes.Transparent;
        }

        private void RefreshExtraPrompts()
        {
            ExtraPromptManager.Load();
            _extraPromptView = CollectionViewSource.GetDefaultView(ExtraPromptManager.GetAll());
            _extraPromptView.SortDescriptions.Add(new SortDescription("IsFavorite", ListSortDirection.Descending));
            _extraPromptView.SortDescriptions.Add(new SortDescription("LastModified", ListSortDirection.Descending));
            _extraPromptView.Filter = ExtraPromptFilter;
            ExtraBasePrompts.ItemsSource = _extraPromptView;
        }

        private void RefreshExtras()
        {
            ExtraManager.Load();
            _extraView = CollectionViewSource.GetDefaultView(ExtraManager.GetAll());
            _extraView.SortDescriptions.Add(new SortDescription("IsFavorite", ListSortDirection.Descending));
            _extraView.SortDescriptions.Add(new SortDescription("ShortName", ListSortDirection.Ascending));
            _extraView.Filter = ExtraFilter;
            ExtraList.ItemsSource = _extraView;
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                if (BtnMaximize != null) BtnMaximize.Content = "🗗";
                MainBorder.Margin = new Thickness(8);
                MainBorder.CornerRadius = new CornerRadius(0);
                MainBorder.BorderThickness = new Thickness(0);
            }
            else
            {
                if (BtnMaximize != null) BtnMaximize.Content = "🗖";
                MainBorder.Margin = new Thickness(0);
                MainBorder.CornerRadius = new CornerRadius(8);
                MainBorder.BorderThickness = new Thickness(1);
            }
        }
    }
}
