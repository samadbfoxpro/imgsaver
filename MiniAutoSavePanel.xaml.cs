using System;
using System.Windows;
using System.Windows.Controls;

namespace imgsaver
{
    public partial class MiniAutoSavePanel : Window
    {
        private readonly IMiniClipHost _parent;
        private bool _isUpdatingUI = false;

        public MiniAutoSavePanel(IMiniClipHost parent)
        {
            InitializeComponent();
            _parent = parent;
            LanguageManager.ApplyWindowLanguage(this);
            Loaded += MiniAutoSavePanel_Loaded;
        }

        private void MiniAutoSavePanel_Loaded(object sender, RoutedEventArgs e)
        {
            _isUpdatingUI = true;
            try
            {
                ChkEnabled.IsChecked = _parent.IsAutoSaveEnabled;
                TxtThreshold.Text = _parent.AutoSaveThreshold.ToString();
                ChkDelay.IsChecked = _parent.IsAutoSaveDelayEnabled;
                TxtDelaySeconds.Text = _parent.AutoSaveDelaySeconds.ToString();
            }
            finally
            {
                _isUpdatingUI = false;
            }
        }

        private void SettingChanged(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUI || _parent == null) return;

            if (sender == ChkEnabled)
            {
                _parent.IsAutoSaveEnabled = ChkEnabled.IsChecked == true;
            }
            else if (sender == ChkDelay)
            {
                _parent.IsAutoSaveDelayEnabled = ChkDelay.IsChecked == true;
            }
            else if (sender == TxtThreshold)
            {
                if (int.TryParse(TxtThreshold.Text.Trim(), out int val) && val > 0)
                {
                    _parent.AutoSaveThreshold = val;
                }
            }
            else if (sender == TxtDelaySeconds)
            {
                if (int.TryParse(TxtDelaySeconds.Text.Trim(), out int val) && val >= 0)
                {
                    _parent.AutoSaveDelaySeconds = val;
                }
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
        }
    }
}
