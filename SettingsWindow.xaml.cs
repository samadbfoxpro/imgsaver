using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using WinForms = System.Windows.Forms;

namespace imgsaver
{
    public partial class SettingsWindow : Window
    {
        private const string ConfigFileName = "data\\config.txt";

        public SettingsWindow()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);
                if (File.Exists(configPath))
                {
                    string[] lines = File.ReadAllLines(configPath);
                    if (lines.Length > 0) TxtSavePath.Text = lines[0].Trim();
                    if (lines.Length > 1) ChkRandomOnlyFavorites.IsChecked = lines[1].Trim().ToLower() == "true";
                    if (lines.Length > 2) ChkAutoImportEnabled.IsChecked = lines[2].Trim().ToLower() == "true";
                    if (lines.Length > 3) TxtAutoImportPath.Text = lines[3].Trim();
                    if (lines.Length > 4) ChkAutoSaveEnabled.IsChecked = lines[4].Trim().ToLower() == "true";
                    if (lines.Length > 5) TxtAutoSaveCount.Text = lines[5].Trim();
                }

                // Load minimum image dimensions from BrowserSettings
                var settings = BrowserSettings.Load();
                TxtMinImageWidth.Text = settings.MinImageWidth.ToString();
                TxtMinImageHeight.Text = settings.MinImageHeight.ToString();

                RecordingManager.LoadState();
                ChkSequentialMode.IsChecked = RecordingManager.SequentialMode;
                CmbDefaultSlot.SelectedIndex = RecordingManager.SelectedSlot - 1;
            }
            catch { }
        }

        private void SaveSettings()
        {
            try
            {
                string dataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
                if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);

                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);

                string path = TxtSavePath.Text.Trim();
                string onlyFavs = (ChkRandomOnlyFavorites.IsChecked == true).ToString().ToLower();
                string autoImportEnabled = (ChkAutoImportEnabled.IsChecked == true).ToString().ToLower();
                string autoImportPath = TxtAutoImportPath.Text.Trim();
                string autoSaveEnabled = (ChkAutoSaveEnabled.IsChecked == true).ToString().ToLower();
                string autoSaveCount = TxtAutoSaveCount.Text.Trim();
                if (string.IsNullOrEmpty(autoSaveCount)) autoSaveCount = "1";

                File.WriteAllLines(configPath, new string[] {
                    path,
                    onlyFavs,
                    autoImportEnabled,
                    autoImportPath,
                    autoSaveEnabled,
                    autoSaveCount
                });

                // Save minimum image dimensions to BrowserSettings
                var settings = BrowserSettings.Load();
                if (int.TryParse(TxtMinImageWidth.Text, out int minWidth) && minWidth > 0)
                    settings.MinImageWidth = minWidth;
                if (int.TryParse(TxtMinImageHeight.Text, out int minHeight) && minHeight > 0)
                    settings.MinImageHeight = minHeight;
                settings.Save();

                RecordingManager.SequentialMode = ChkSequentialMode.IsChecked == true;
                RecordingManager.SelectedSlot = CmbDefaultSlot.SelectedIndex + 1;
                RecordingManager.SaveState();

                // Notify MiniClipboard if open
                foreach (Window w in System.Windows.Application.Current.Windows)
                {
                    if (w is MiniClipboardWindow mini)
                    {
                        mini.RefreshAutoImport();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Error saving settings: " + ex.Message);
            }
        }

        private void BtnBrowse_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new WinForms.FolderBrowserDialog();
            if (!string.IsNullOrEmpty(TxtSavePath.Text) && Directory.Exists(TxtSavePath.Text))
            {
                dialog.SelectedPath = TxtSavePath.Text;
            }

            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                TxtSavePath.Text = dialog.SelectedPath;
            }
        }

        private void BtnBrowseAutoImport_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new WinForms.FolderBrowserDialog();
            if (!string.IsNullOrEmpty(TxtAutoImportPath.Text) && Directory.Exists(TxtAutoImportPath.Text))
            {
                dialog.SelectedPath = TxtAutoImportPath.Text;
            }

            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                TxtAutoImportPath.Text = dialog.SelectedPath;
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
            this.DialogResult = true;
            this.Close();
        }
    }
}