using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace imgsaver
{
    public class CachedSiteItem
    {
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public string Info { get; set; } = "";
    }

    public partial class BrowserSettingsWindow : Window
    {
        public bool RequestClearData { get; private set; } = false;
        private readonly string _permanentCacheFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "web_cache");

        public BrowserSettingsWindow()
        {
            InitializeComponent();
            LoadCurrentSettings();
            LoadCachedSites();
        }

        private void LoadCurrentSettings()
        {
            var settings = BrowserSettings.Load();
            ChkLoadImages.IsChecked = settings.LoadImages;
            ChkLoadMedia.IsChecked = settings.LoadMedia;
            ChkEnableJS.IsChecked = settings.EnableJavaScript;
            ChkMuteAudio.IsChecked = settings.MuteAudio;
            ChkAutoHideStatus.IsChecked = settings.AutoHideStatus;

            ChkEnableProxy.IsChecked = settings.ProxyEnabled;
            TxtProxyAddress.Text = settings.ProxyAddress;
            TxtProxyPort.Text = settings.ProxyPort;

            if (settings.ProxyType == "socks5") CmbProxyType.SelectedIndex = 1;
            else CmbProxyType.SelectedIndex = 0;

            // Load no-cache sites
            if (settings.NoCacheHosts != null && settings.NoCacheHosts.Count > 0)
            {
                LstNoCacheSites.ItemsSource = new List<string>(settings.NoCacheHosts);
            }
        }

        private void LoadCachedSites()
        {
            try
            {
                if (!Directory.Exists(_permanentCacheFolder)) return;

                var sites = new List<CachedSiteItem>();
                var dirs = Directory.GetDirectories(_permanentCacheFolder);

                foreach (var dir in dirs)
                {
                    var dirInfo = new DirectoryInfo(dir);
                    int fileCount = dirInfo.GetFiles("*", SearchOption.AllDirectories).Length;
                    sites.Add(new CachedSiteItem
                    {
                        Name = dirInfo.Name,
                        Path = dir,
                        Info = $"{fileCount} files"
                    });
                }

                LstCachedSites.ItemsSource = sites.OrderBy(s => s.Name).ToList();
            }
            catch { }
        }

        private void BtnClearSelectedSite_Click(object? sender, RoutedEventArgs e)
        {
            if (LstCachedSites.SelectedItem is CachedSiteItem selected)
            {
                if (CustomMessageBox.Show($"Clear all cached data for {selected.Name}?", "Clear Site Cache", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    try
                    {
                        if (Directory.Exists(selected.Path))
                        {
                            Directory.Delete(selected.Path, true);
                        }
                        LoadCachedSites();
                        CustomMessageBox.Show($"Cache for {selected.Name} cleared.", "Success");
                    }
                    catch (Exception ex)
                    {
                        CustomMessageBox.Show($"Error clearing cache: {ex.Message}", "Error");
                    }
                }
            }
            else
            {
                CustomMessageBox.Show("Please select a site from the list first.", "No Selection");
            }
        }

        private void BtnSave_Click(object? sender, RoutedEventArgs e)
        {
            var settings = BrowserSettings.Load();
            settings.LoadImages = ChkLoadImages.IsChecked == true;
            settings.LoadMedia = ChkLoadMedia.IsChecked == true;
            settings.EnableJavaScript = ChkEnableJS.IsChecked == true;
            settings.MuteAudio = ChkMuteAudio.IsChecked == true;
            settings.AutoHideStatus = ChkAutoHideStatus.IsChecked == true;

            settings.ProxyEnabled = ChkEnableProxy.IsChecked == true;
            settings.ProxyAddress = TxtProxyAddress.Text.Trim();
            settings.ProxyPort = TxtProxyPort.Text.Trim();
            settings.ProxyType = (CmbProxyType.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "http";

            // Save no-cache sites
            settings.NoCacheHosts = new List<string>(LstNoCacheSites.Items.Cast<string>());

            settings.Save();
            this.DialogResult = true;
            this.Close();
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
            if (items.Contains(site))
            {
                CustomMessageBox.Show("This site is already in the no-cache list.", "Duplicate");
                return;
            }

            items.Add(site);
            LstNoCacheSites.ItemsSource = items;
            TxtNewNoCacheSite.Clear();
            
            // Clear existing cache for this site
            try
            {
                string siteCacheDir = Path.Combine(_permanentCacheFolder, SanitizeHostForCache(site));
                if (Directory.Exists(siteCacheDir))
                {
                    Directory.Delete(siteCacheDir, true);
                }
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

        private void BtnClearData_Click(object? sender, RoutedEventArgs e)
        {
            if (CustomMessageBox.Show("This will clear all cookies, global cache, and local storage. You will be logged out of all websites. Continue?", "Clear Global Browser Data", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                RequestClearData = true;
                this.DialogResult = true;
                this.Close();
            }
        }

        private void BtnCancel_Click(object? sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private string SanitizeHostForCache(string host)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                host = host.Replace(c, '_');
            return host;
        }

        private void TitleBar_MouseLeftButtonDown(object? sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) this.DragMove();
        }
    }
}
