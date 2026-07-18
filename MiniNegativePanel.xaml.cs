using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace imgsaver
{
    public partial class MiniNegativePanel : Window
    {
        public class NegativePreset
        {
            public string Name { get; set; } = "";
            public string Content { get; set; } = "";
            public string ShortContent
            {
                get
                {
                    if (string.IsNullOrEmpty(Content)) return "";
                    var words = Content.Split(new[] { ' ', ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    if (words.Length <= 4) return Content;
                    return string.Join(" ", words.Take(4)) + "...";
                }
            }
        }

        private ObservableCollection<NegativePreset> _presets = new ObservableCollection<NegativePreset>();
        private MiniClipboardWindow? _parent;

        public MiniNegativePanel(MiniClipboardWindow parent)
        {
            InitializeComponent();
            _parent = parent;
            this.Owner = parent;
            
            LoadPresets();
            LstPresets.ItemsSource = _presets;
            UpdateActiveText();
        }

        public void UpdateActiveText()
        {
            if (_parent != null)
            {
                TxtActiveNeg.Text = string.IsNullOrEmpty(_parent.NegativePrompt) ? "Empty" : _parent.NegativePrompt;
            }
        }

        private string PresetsPath => DataPathManager.GetSettingsFilePath("negative_presets.json");

        private void LoadPresets()
        {
            try
            {
                string path = PresetsPath;
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var list = JsonSerializer.Deserialize<System.Collections.Generic.List<NegativePreset>>(json);
                    if (list != null)
                    {
                        _presets.Clear();
                        foreach (var item in list) _presets.Add(item);
                        return;
                    }
                }
            }
            catch { }

            // Add default presets if file not found or corrupted
            _presets.Add(new NegativePreset { Name = "Realistic Neg", Content = "blurry, low quality, worst quality, deformed, extra limbs, bad anatomy, bad hands, cartoon, 3d, render" });
            _presets.Add(new NegativePreset { Name = "Anime Neg", Content = "easynegative, worst quality, low quality, bad anatomy, bad hands, signature, watermark, username" });
            _presets.Add(new NegativePreset { Name = "Standard Neg", Content = "(worst quality, low quality:1.4), blurry, bad-handv4" });
            SavePresets();
        }

        private void SavePresets()
        {
            try
            {
                string path = PresetsPath;
                string dir = Path.GetDirectoryName(path) ?? "";
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                string json = JsonSerializer.Serialize(_presets.ToList(), new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch { }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
        }

        private void BtnCopyActive_Click(object sender, RoutedEventArgs e)
        {
            if (_parent != null && !string.IsNullOrEmpty(_parent.NegativePrompt))
            {
                try
                {
                    System.Windows.Clipboard.SetText(_parent.NegativePrompt);
                }
                catch { }
            }
        }

        private void BtnApplyPreset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.DataContext is NegativePreset preset)
            {
                if (_parent != null)
                {
                    _parent.NegativePrompt = preset.Content;
                    _parent.IsNegativeLocked = true;
                    UpdateActiveText();
                }
            }
        }

        private void BtnCopyPreset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.DataContext is NegativePreset preset)
            {
                try
                {
                    System.Windows.Clipboard.SetText(preset.Content);
                }
                catch { }
            }
        }

        private void BtnDeletePreset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.DataContext is NegativePreset preset)
            {
                _presets.Remove(preset);
                SavePresets();
            }
        }

        private void BtnAddPreset_Click(object sender, RoutedEventArgs e)
        {
            string name = TxtNewName.Text.Trim();
            string content = TxtNewContent.Text.Trim();

            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(content))
            {
                return;
            }

            _presets.Add(new NegativePreset { Name = name, Content = content });
            SavePresets();

            TxtNewName.Clear();
            TxtNewContent.Clear();
        }
    }
}
