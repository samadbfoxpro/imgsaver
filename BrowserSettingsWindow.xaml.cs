using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace imgsaver
{
    public class CachedSiteItem
    {
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public long SizeBytes { get; set; }
        public int FileCount { get; set; }
        public string SizeText => BrowserSettingsWindow.FormatBytes(SizeBytes);
        public string Info => $"{SizeText} - {FileCount} files";
    }

    public partial class BrowserSettingsWindow : Window
    {
        public bool RequestClearData { get; private set; }
        public bool RequestDeleteLoginData => ChkDeleteLoginData.IsChecked == true && RequestClearData;

        private readonly string _permanentCacheFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "web_cache");
        private readonly List<CachedSiteItem> _cachedSites = new();
        private FrameworkElement? _activePanel;
        private System.Windows.Controls.Button? _activeNavButton;

        public BrowserSettingsWindow()
        {
            InitializeComponent();
            LoadCurrentSettings();
            LoadCachedSites();
            CmbCacheSort.SelectedIndex = 0;
            ShowPanel(GeneralPanel, BtnNavGeneral, false);
        }

        private void LoadCurrentSettings()
        {
            var settings = BrowserSettings.Load();
            ChkLoadImages.IsChecked = settings.LoadImages;
            ChkLoadMedia.IsChecked = settings.LoadMedia;
            ChkEnableJS.IsChecked = settings.EnableJavaScript;
            ChkMuteAudio.IsChecked = settings.MuteAudio;
            ChkAutoImportImagesToMiniClip.IsChecked = settings.AutoImportImagesToMiniClip;
            ChkShowMiniClipImageImportButtons.IsChecked = settings.ShowMiniClipImageImportButtons;
            ChkReplaceMiniClipImageOnImport.IsChecked = settings.ReplaceMiniClipImageOnImport;
            ChkAutoHideStatus.IsChecked = settings.AutoHideStatus;

            ChkEnableProxy.IsChecked = settings.ProxyEnabled;
            TxtProxyAddress.Text = settings.ProxyAddress;
            TxtProxyPort.Text = settings.ProxyPort;

            CmbProxyType.SelectedIndex = settings.ProxyType == "socks5" ? 1 : 0;
            LstNoCacheSites.ItemsSource = new List<string>(settings.NoCacheHosts ?? new List<string>());
        }

        private void LoadCachedSites()
        {
            _cachedSites.Clear();

            try
            {
                if (Directory.Exists(_permanentCacheFolder))
                {
                    foreach (string dir in Directory.GetDirectories(_permanentCacheFolder))
                    {
                        var dirInfo = new DirectoryInfo(dir);
                        var files = dirInfo.GetFiles("*", SearchOption.AllDirectories);
                        _cachedSites.Add(new CachedSiteItem
                        {
                            Name = dirInfo.Name,
                            Path = dir,
                            FileCount = files.Length,
                            SizeBytes = files.Sum(file => SafeFileLength(file))
                        });
                    }
                }
            }
            catch { }

            ApplyCacheSort();
            UpdateCacheSummary();
        }

        private void ApplyCacheSort()
        {
            string sort = (CmbCacheSort.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "name";
            var ordered = sort == "size"
                ? _cachedSites.OrderByDescending(site => site.SizeBytes).ThenBy(site => site.Name).ToList()
                : _cachedSites.OrderBy(site => site.Name).ToList();

            LstCachedSites.ItemsSource = ordered;
        }

        private void UpdateCacheSummary()
        {
            TxtTotalCacheSize.Text = FormatBytes(_cachedSites.Sum(site => site.SizeBytes));
            TxtCachedSiteCount.Text = _cachedSites.Count.ToString();
            TxtCachedFileCount.Text = _cachedSites.Sum(site => site.FileCount).ToString();
            BtnClearSelectedSite.IsEnabled = _cachedSites.Count > 0;
            BtnClearData.IsEnabled = _cachedSites.Count > 0 || ChkDeleteLoginData.IsChecked == true;
        }

        private static long SafeFileLength(FileInfo file)
        {
            try { return file.Length; }
            catch { return 0; }
        }

        public static string FormatBytes(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double value = bytes;
            int unitIndex = 0;
            while (value >= 1024 && unitIndex < units.Length - 1)
            {
                value /= 1024;
                unitIndex++;
            }

            return unitIndex == 0 ? $"{bytes} {units[unitIndex]}" : $"{value:0.##} {units[unitIndex]}";
        }

        private void SidebarNav_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is not System.Windows.Controls.Button button || button.Tag is not string panelName) return;
            if (FindName(panelName) is FrameworkElement panel)
                ShowPanel(panel, button, true);
        }

        private void ShowPanel(FrameworkElement panel, System.Windows.Controls.Button navButton, bool animate)
        {
            if (_activePanel == panel) return;

            foreach (FrameworkElement child in ContentHost.Children.OfType<FrameworkElement>())
            {
                child.Visibility = child == panel ? Visibility.Visible : Visibility.Collapsed;
                if (child != panel) child.Opacity = 0;
            }

            ResetNavButton(_activeNavButton);
            navButton.Background = FindResource("SelectedBrush") as System.Windows.Media.Brush;
            navButton.BorderBrush = FindResource("AccentBrush") as System.Windows.Media.Brush;

            _activePanel = panel;
            _activeNavButton = navButton;

            if (!animate)
            {
                panel.Opacity = 1;
                return;
            }

            panel.Opacity = 0;
            panel.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)));
        }

        private void ResetNavButton(System.Windows.Controls.Button? button)
        {
            if (button == null) return;
            button.ClearValue(BackgroundProperty);
            button.ClearValue(BorderBrushProperty);
        }

        private void BtnRefreshCache_Click(object? sender, RoutedEventArgs e)
        {
            LoadCachedSites();
        }

        private void CmbCacheSort_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (LstCachedSites == null) return;
            ApplyCacheSort();
        }

        private void ChkDeleteLoginData_Changed(object? sender, RoutedEventArgs e)
        {
            if (BtnClearData == null) return;
            UpdateCacheSummary();
        }

        private void BtnDeleteSiteCache_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && button.Tag is CachedSiteItem site)
                DeleteSiteCache(site);
        }

        private void BtnClearSelectedSite_Click(object? sender, RoutedEventArgs e)
        {
            if (LstCachedSites.SelectedItem is CachedSiteItem selected)
                DeleteSiteCache(selected);
            else
                CustomMessageBox.Show("Please select a site from the list first.", "No Selection");
        }

        private void DeleteSiteCache(CachedSiteItem selected)
        {
            if (CustomMessageBox.Show($"Delete cached files for {selected.Name}? Login sessions and cookies will be kept.", "Delete Site Cache", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                if (Directory.Exists(selected.Path))
                    Directory.Delete(selected.Path, true);

                LoadCachedSites();
                CustomMessageBox.Show($"Cache for {selected.Name} cleared.", "Success");
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Error clearing cache: {ex.Message}", "Error");
            }
        }

        private void BtnClearData_Click(object? sender, RoutedEventArgs e)
        {
            bool deleteLoginData = ChkDeleteLoginData.IsChecked == true;
            string message = deleteLoginData
                ? "Delete all cached files and also clear login data, cookies, and browser storage? You may be logged out of websites."
                : "Delete all cached files? Login sessions, cookies, and account data will be kept.";

            if (CustomMessageBox.Show(message, "Delete Browser Cache", MessageBoxButton.YesNo, deleteLoginData ? MessageBoxImage.Warning : MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                DeleteDirectoryContents(_permanentCacheFolder);
                LoadCachedSites();
            }
            catch (Exception ex)
            {
                CustomMessageBox.Show($"Error clearing cache: {ex.Message}", "Error");
                return;
            }

            if (deleteLoginData)
            {
                RequestClearData = true;
                DialogResult = true;
                Close();
                return;
            }

            CustomMessageBox.Show("All cached files have been cleared.", "Success");
        }

        private void BtnSave_Click(object? sender, RoutedEventArgs e)
        {
            var settings = BrowserSettings.Load();
            settings.LoadImages = ChkLoadImages.IsChecked == true;
            settings.LoadMedia = ChkLoadMedia.IsChecked == true;
            settings.EnableJavaScript = ChkEnableJS.IsChecked == true;
            settings.MuteAudio = ChkMuteAudio.IsChecked == true;
            settings.AutoImportImagesToMiniClip = ChkAutoImportImagesToMiniClip.IsChecked == true;
            settings.ShowMiniClipImageImportButtons = ChkShowMiniClipImageImportButtons.IsChecked == true;
            settings.ReplaceMiniClipImageOnImport = ChkReplaceMiniClipImageOnImport.IsChecked == true;
            settings.AutoHideStatus = ChkAutoHideStatus.IsChecked == true;

            settings.ProxyEnabled = ChkEnableProxy.IsChecked == true;
            settings.ProxyAddress = TxtProxyAddress.Text.Trim();
            settings.ProxyPort = TxtProxyPort.Text.Trim();
            settings.ProxyType = (CmbProxyType.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "http";
            settings.NoCacheHosts = new List<string>(LstNoCacheSites.Items.Cast<string>());

            settings.Save();
            DialogResult = true;
            Close();
        }

        private void BtnAddNoCacheSite_Click(object? sender, RoutedEventArgs e)
        {
            string site = TxtNewNoCacheSite.Text.Trim();
            if (string.IsNullOrWhiteSpace(site))
            {
                CustomMessageBox.Show("Please enter a site name (e.g., example.com)", "Empty Input");
                return;
            }

            var items = LstNoCacheSites.Items.Cast<string>().ToList();
            if (items.Contains(site, StringComparer.OrdinalIgnoreCase))
            {
                CustomMessageBox.Show("This site is already in the no-cache list.", "Duplicate");
                return;
            }

            items.Add(site);
            LstNoCacheSites.ItemsSource = items;
            TxtNewNoCacheSite.Clear();

            try
            {
                string siteCacheDir = Path.Combine(_permanentCacheFolder, SanitizeHostForCache(site));
                if (Directory.Exists(siteCacheDir))
                    Directory.Delete(siteCacheDir, true);
                LoadCachedSites();
            }
            catch { }

            TxtNewNoCacheSite.Focus();
        }

        private void BtnRemoveNoCacheSite_Click(object? sender, RoutedEventArgs e)
        {
            if (LstNoCacheSites.SelectedItem is string selected)
            {
                var items = LstNoCacheSites.Items.Cast<string>().ToList();
                items.Remove(selected);
                LstNoCacheSites.ItemsSource = items;
            }
            else
            {
                CustomMessageBox.Show("Please select a site to remove.", "No Selection");
            }
        }

        private void BtnCancel_Click(object? sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private string SanitizeHostForCache(string host)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                host = host.Replace(c, '_');
            return host;
        }

        private void DeleteDirectoryContents(string folder)
        {
            if (!Directory.Exists(folder)) return;

            foreach (string file in Directory.GetFiles(folder))
            {
                try { File.Delete(file); } catch { }
            }

            foreach (string dir in Directory.GetDirectories(folder))
            {
                try { Directory.Delete(dir, true); } catch { }
            }
        }

        private void TitleBar_MouseLeftButtonDown(object? sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }
    }
}
