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
        private const string ConfigFileName = "config.txt";
        private const string GalleryConfigFileName = "gallery_config.txt";

        public SettingsWindow()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            try
            {
                DataPathManager.Reload();
                ChkUseCustomDataFolder.IsChecked = DataPathManager.UseCustomDataFolder;
                TxtCustomDataFolder.Text = DataPathManager.CustomDataFolder;

                string configPath = DataPathManager.GetSettingsFilePath(ConfigFileName);
                if (File.Exists(configPath))
                {
                    string[] lines = File.ReadAllLines(configPath);
                    if (lines.Length > 0) TxtSavePath.Text = lines[0].Trim();
                    if (lines.Length > 1) ChkRandomOnlyFavorites.IsChecked = lines[1].Trim().ToLower() == "true";
                    if (lines.Length > 2) ChkAutoImportEnabled.IsChecked = lines[2].Trim().ToLower() == "true";
                    if (lines.Length > 3) TxtAutoImportPath.Text = lines[3].Trim();
                    if (lines.Length > 4) ChkAutoSaveEnabled.IsChecked = lines[4].Trim().ToLower() == "true";
                    if (lines.Length > 5) TxtAutoSaveCount.Text = lines[5].Trim();
                    if (lines.Length > 6) ChkAutoCaptureExtraTemplate.IsChecked = lines[6].Trim().ToLower() == "true";
                    if (lines.Length > 7) ChkAutoCopyExtraTemplateOutput.IsChecked = lines[7].Trim().ToLower() == "true";
                    ChkReplacePositivePromptOnClipboardText.IsChecked = lines.Length <= 8 || lines[8].Trim().ToLower() == "true";
                    ChkSpiSyncPreserveBasePrompt.IsChecked = lines.Length > 9 && lines[9].Trim().ToLower() == "true";
                    ChkUseTagReplacerForMiniClip.IsChecked = lines.Length > 10 && lines[10].Trim().ToLower() == "true";
                    TxtTagReplacerPrefix.Text = lines.Length > 11 ? lines[11].Trim() : "PH_";
                }

                string galleryConfigPath = DataPathManager.GetSettingsFilePath(GalleryConfigFileName);
                if (File.Exists(galleryConfigPath))
                {
                    TxtGalleryPath.Text = File.ReadAllText(galleryConfigPath).Trim();
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
                DataPathManager.SaveLocation(ChkUseCustomDataFolder.IsChecked == true, TxtCustomDataFolder.Text.Trim());

                ReloadSharedDataStores();

                string configPath = DataPathManager.GetSettingsFilePath(ConfigFileName);
                string galleryConfigPath = DataPathManager.GetSettingsFilePath(GalleryConfigFileName);

                string path = TxtSavePath.Text.Trim();
                string onlyFavs = (ChkRandomOnlyFavorites.IsChecked == true).ToString().ToLower();
                string autoImportEnabled = (ChkAutoImportEnabled.IsChecked == true).ToString().ToLower();
                string autoImportPath = TxtAutoImportPath.Text.Trim();
                string autoSaveEnabled = (ChkAutoSaveEnabled.IsChecked == true).ToString().ToLower();
                string autoSaveCount = TxtAutoSaveCount.Text.Trim();
                string autoCaptureExtraTemplate = (ChkAutoCaptureExtraTemplate.IsChecked == true).ToString().ToLower();
                string autoCopyExtraTemplateOutput = (ChkAutoCopyExtraTemplateOutput.IsChecked == true).ToString().ToLower();
                string replacePositivePromptOnClipboardText = (ChkReplacePositivePromptOnClipboardText.IsChecked == true).ToString().ToLower();
                string spiSyncPreserveBasePrompt = (ChkSpiSyncPreserveBasePrompt.IsChecked == true).ToString().ToLower();
                string useTagReplacer = (ChkUseTagReplacerForMiniClip.IsChecked == true).ToString().ToLower();
                string tagReplacerPrefix = TxtTagReplacerPrefix.Text.Trim();
                if (string.IsNullOrEmpty(tagReplacerPrefix)) tagReplacerPrefix = "PH_";
                string galleryPath = TxtGalleryPath.Text.Trim();
                if (string.IsNullOrEmpty(autoSaveCount)) autoSaveCount = "1";

                File.WriteAllLines(configPath, new string[] {
                    path,
                    onlyFavs,
                    autoImportEnabled,
                    autoImportPath,
                    autoSaveEnabled,
                    autoSaveCount,
                    autoCaptureExtraTemplate,
                    autoCopyExtraTemplateOutput,
                    replacePositivePromptOnClipboardText,
                    spiSyncPreserveBasePrompt,
                    useTagReplacer,
                    tagReplacerPrefix
                });

                File.WriteAllText(galleryConfigPath, galleryPath);

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

        private void BtnBrowseCustomDataFolder_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new WinForms.FolderBrowserDialog();
            if (!string.IsNullOrEmpty(TxtCustomDataFolder.Text) && Directory.Exists(TxtCustomDataFolder.Text))
            {
                dialog.SelectedPath = TxtCustomDataFolder.Text;
            }

            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                TxtCustomDataFolder.Text = dialog.SelectedPath;
                ChkUseCustomDataFolder.IsChecked = true;
            }
        }

        private void BtnBrowseGallery_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new WinForms.FolderBrowserDialog();
            if (!string.IsNullOrEmpty(TxtGalleryPath.Text) && Directory.Exists(TxtGalleryPath.Text))
            {
                dialog.SelectedPath = TxtGalleryPath.Text;
            }

            if (dialog.ShowDialog() == WinForms.DialogResult.OK)
            {
                TxtGalleryPath.Text = dialog.SelectedPath;
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

        private void ReloadSharedDataStores()
        {
            BasePromptManager.Unload();
            CharacterManager.Unload();
            ExtraManager.Unload();
            ExtraPromptManager.Unload();
            BasePromptManager.Load();
            CharacterManager.Load();
            ExtraManager.Load();
            ExtraPromptManager.Load();
        }
    }
}
