using System;
using System.Windows;
using System.Windows.Media.Animation;

namespace imgsaver
{
    public partial class ProgressDialog : Window
    {
        private double _maxWidth;

        public ProgressDialog()
        {
            InitializeComponent();
            Loaded += (s, e) => _maxWidth = ProgressFill.ActualWidth;
        }

        public void UpdateProgress(int percentage, string status)
        {
            Dispatcher.Invoke(() =>
            {
                TxtPercentage.Text = $"{percentage}%";
                TxtStatus.Text = status;

                // Animate progress bar
                var animation = new DoubleAnimation
                {
                    To = (percentage / 100.0) * 390, // 390 is approximate max width (450 - 60 margin)
                    Duration = TimeSpan.FromMilliseconds(300),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                ProgressFill.BeginAnimation(WidthProperty, animation);
            });
        }

        public void SetTitle(string title)
        {
            Dispatcher.Invoke(() => TxtTitle.Text = title);
        }

        public void Complete()
        {
            Dispatcher.Invoke(() =>
            {
                UpdateProgress(100, "Complete!");
                System.Threading.Tasks.Task.Delay(500).ContinueWith(_ =>
                {
                    Dispatcher.Invoke(() => DialogResult = true);
                });
            });
        }
    }
}
