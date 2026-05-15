using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System;

// Resolve ambiguities between WPF and WinForms/Drawing
using MessageBox = System.Windows.MessageBox;
using FontFamily = System.Windows.Media.FontFamily;

namespace imgsaver
{
    public partial class SnippetWindow : Window
    {
        public SnippetWindow()
        {
            InitializeComponent();
            SnippetManager.Load(); // Ensure fresh data
            RefreshList();
            TxtKey.TextChanged += TxtKey_TextChanged;
        }

        private void TxtKey_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TxtKey.IsFocused && TxtKey.Text.Length > 0)
            {
                SpawnParticles();
            }
        }

        private void SpawnParticles()
        {
            try
            {
                if (!this.IsLoaded) return;

                System.Windows.Point relativePoint = TxtKey.TranslatePoint(new System.Windows.Point(0, 0), ParticleCanvas);
                double startX = relativePoint.X + (TxtKey.ActualWidth > 20 ? TxtKey.ActualWidth - 20 : TxtKey.ActualWidth / 2);
                double startY = relativePoint.Y + TxtKey.ActualHeight / 2;

                Random rnd = new Random();
                string[] particles = { "⚡", "✨", "🔥" };

                int count = rnd.Next(3, 6); // Reduced count for better performance
                for (int i = 0; i < count; i++)
                {
                    TextBlock p = new TextBlock
                    {
                        Text = particles[rnd.Next(particles.Length)],
                        FontSize = rnd.Next(10, 14),
                        RenderTransformOrigin = new System.Windows.Point(0.5, 0.5),
                        IsHitTestVisible = false,
                        FontFamily = new FontFamily("Segoe UI Emoji")
                    };

                    Canvas.SetLeft(p, startX);
                    Canvas.SetTop(p, startY);

                    TransformGroup group = new TransformGroup();
                    TranslateTransform trans = new TranslateTransform();
                    RotateTransform rot = new RotateTransform();
                    ScaleTransform scale = new ScaleTransform { ScaleX = 0, ScaleY = 0 };

                    group.Children.Add(scale);
                    group.Children.Add(rot);
                    group.Children.Add(trans);
                    p.RenderTransform = group;

                    ParticleCanvas.Children.Add(p);
                    AnimateParticle(p, trans, rot, scale, rnd);
                }
            }
            catch { }
        }

        private void AnimateParticle(TextBlock particle, TranslateTransform trans, RotateTransform rot, ScaleTransform scale, Random rnd)
        {
            double durationSec = rnd.NextDouble() * 0.3 + 0.2;
            Duration duration = new Duration(TimeSpan.FromSeconds(durationSec));

            double angle = rnd.NextDouble() * 2 * Math.PI;
            double speed = rnd.Next(80, 180);

            DoubleAnimation animX = new DoubleAnimation(0, Math.Cos(angle) * speed, duration);
            DoubleAnimation animY = new DoubleAnimation(0, Math.Sin(angle) * speed + 20, duration);
            animX.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            animY.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };

            trans.BeginAnimation(TranslateTransform.XProperty, animX);
            trans.BeginAnimation(TranslateTransform.YProperty, animY);

            rot.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(0, rnd.Next(-180, 180), duration));

            DoubleAnimation animScale = new DoubleAnimation(0, 1, new Duration(TimeSpan.FromSeconds(0.1)));
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, animScale);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, animScale);

            DoubleAnimation animFade = new DoubleAnimation(1, 0, duration);
            animFade.BeginTime = TimeSpan.FromSeconds(durationSec * 0.4);
            animFade.Completed += (s, e) => { ParticleCanvas.Children.Remove(particle); };
            particle.BeginAnimation(UIElement.OpacityProperty, animFade);
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void RefreshList()
        {
            LstSnippets.ItemsSource = null;
            LstSnippets.ItemsSource = SnippetManager.Snippets;
        }

        private void LstSnippets_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstSnippets.SelectedItem is Snippet selected)
            {
                TxtKey.Text = selected.Key;
                TxtValue.Text = selected.Value;
            }
        }

        private void BtnNew_Click(object sender, RoutedEventArgs e)
        {
            TxtKey.Clear();
            TxtValue.Clear();
            LstSnippets.SelectedItem = null;
            TxtKey.Focus();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            string key = TxtKey.Text.Trim();
            string value = TxtValue.Text.Trim();

            if (string.IsNullOrEmpty(key))
            {
                MessageBox.Show("Please enter a shortcut key.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (LstSnippets.SelectedItem is Snippet selected)
            {
                selected.Key = key;
                selected.Value = value;
            }
            else
            {
                if (SnippetManager.GetExpansion(key) != null)
                {
                    MessageBox.Show("This shortcut already exists!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                SnippetManager.Snippets.Add(new Snippet { Key = key, Value = value });
            }

            SnippetManager.Save();
            RefreshList();
            MessageBox.Show("Saved!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (LstSnippets.SelectedItem is Snippet selected)
            {
                if (MessageBox.Show("Are you sure you want to delete this shortcut?", "Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    SnippetManager.Snippets.Remove(selected);
                    SnippetManager.Save();
                    RefreshList();
                    BtnNew_Click(null, null);
                }
            }
            else
            {
                MessageBox.Show("Please select an item from the list.", "Attention");
            }
        }
    }
}