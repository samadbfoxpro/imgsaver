using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace imgsaver
{
    public partial class MiniBaseCombinerPanel : Window
    {
        private IMiniClipHost? _parent;
        private static string ConfigPath => DataPathManager.GetSettingsFilePath("base_combiner_config.json");

        public MiniBaseCombinerPanel(IMiniClipHost? parent = null)
        {
            InitializeComponent();
            _parent = parent;
            if (parent is Window win) this.Owner = win;

            LoadBasePrompt();
            UpdateCombinerSummary();
        }

        public void UpdateCombinerSummary()
        {
            try
            {
                var combinerData = PromptCombinerStore.Load();
                if (combinerData != null && combinerData.ActiveItemIds != null && combinerData.ActiveItemIds.Count > 0)
                {
                    int activeCount = combinerData.ActiveItemIds.Count;
                    TxtCombinerSummary.Text = $"Active Combiner: {activeCount} snippet(s) ready";
                    TxtCombinerSummary.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#00E5FF"));
                }
                else
                {
                    TxtCombinerSummary.Text = "No active combiner snippets selected";
                    TxtCombinerSummary.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#94A3B8"));
                }
            }
            catch
            {
                TxtCombinerSummary.Text = "Combiner ready";
            }
        }

        public void UpdateBasePromptText(string text)
        {
            Dispatcher.Invoke(() =>
            {
                TxtBasePrompt.Text = text;
            });
        }

        private void LoadBasePrompt()
        {
            try
            {
                var combinerData = PromptCombinerStore.Load();
                if (combinerData != null)
                {
                    ChkAutoCaptureBase.IsChecked = combinerData.AutoCaptureBasePrompt;
                }

                string path = ConfigPath;
                if (File.Exists(path))
                {
                    string text = File.ReadAllText(path);
                    TxtBasePrompt.Text = text ?? "";
                }
            }
            catch { }
        }

        private void ChkAutoCaptureBase_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var combinerData = PromptCombinerStore.Load();
                if (combinerData != null)
                {
                    combinerData.AutoCaptureBasePrompt = ChkAutoCaptureBase.IsChecked == true;
                    PromptCombinerStore.Save(combinerData);
                }
            }
            catch { }
        }

        private void SaveBasePrompt()
        {
            try
            {
                string path = ConfigPath;
                string dir = Path.GetDirectoryName(path) ?? "";
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllText(path, TxtBasePrompt.Text ?? "");
            }
            catch { }
        }

        private void TxtBasePrompt_TextChanged(object sender, TextChangedEventArgs e)
        {
            SaveBasePrompt();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
        }

        private void BtnCombineAndCopy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string baseText = TxtBasePrompt.Text;
                if (string.IsNullOrWhiteSpace(baseText))
                {
                    System.Windows.MessageBox.Show("Please enter a base prompt in the editor first!", "Base Combiner", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var combinerData = PromptCombinerStore.Load();
                string combinedText = PromptCombinerEngine.CombinePerFolder(baseText, combinerData);

                // Copy combined prompt to Clipboard with zero-width space marker to prevent loop
                string clipboardPayload = combinedText + "\u200B";
                System.Windows.Clipboard.SetText(clipboardPayload);

                CursorBadgeNotification.ShowCombiner("⚡ Combined with Base Prompt!");
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Error combining prompt:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCopyBaseOnly_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(TxtBasePrompt.Text))
            {
                try
                {
                    System.Windows.Clipboard.SetText(TxtBasePrompt.Text + "\u200B");
                    CursorBadgeNotification.ShowCombiner("📋 Base Prompt Copied!");
                }
                catch { }
            }
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            TxtBasePrompt.Text = "";
            SaveBasePrompt();
        }
    }
}
