using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;

// Resolve ambiguities between WPF and WinForms/Drawing
using MessageBox = System.Windows.MessageBox;
using FontFamily = System.Windows.Media.FontFamily;
using Color = System.Windows.Media.Color;

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

        private void TxtValue_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateStats();
        }

        private void UpdateStats()
        {
            if (TxtStats == null) return;
            string text = TxtValue?.Text ?? "";
            int chars = text.Length;
            int lines = string.IsNullOrEmpty(text) ? 0 : text.Split('\n').Length;
            TxtStats.Text = $"{chars:N0} کاراکتر • {lines} سطر";
        }

        private void UpdateEditorState(Snippet? selected)
        {
            if (selected != null)
            {
                TxtEditorTitle.Text = $"ویرایش اسنیپت ({selected.Key})";
                PathEditorMode.Data = (StreamGeometry)FindResource("IconEdit");
                PathEditorMode.Fill = new SolidColorBrush(Color.FromRgb(56, 189, 248)); // Sky blue
                BtnDelete.IsEnabled = true;
                BtnDelete.Opacity = 1.0;
            }
            else
            {
                TxtEditorTitle.Text = "ایجاد اسنیپت جدید";
                PathEditorMode.Data = (StreamGeometry)FindResource("IconPlus");
                PathEditorMode.Fill = new SolidColorBrush(Color.FromRgb(52, 211, 153)); // Emerald green
                BtnDelete.IsEnabled = false;
                BtnDelete.Opacity = 0.35;
            }
            UpdateStats();
        }

        private void RefreshList(Snippet? selectItem = null)
        {
            ApplyFilter(selectItem);
        }

        private void ApplyFilter(Snippet? selectItem = null)
        {
            string q = TxtSearch?.Text.Trim() ?? "";
            List<Snippet> list;
            if (string.IsNullOrEmpty(q))
            {
                list = SnippetManager.Snippets;
                if (BtnClearSearch != null) BtnClearSearch.Visibility = Visibility.Collapsed;
            }
            else
            {
                list = SnippetManager.Snippets
                    .Where(s => (!string.IsNullOrEmpty(s.Key) && s.Key.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                                (!string.IsNullOrEmpty(s.Value) && s.Value.Contains(q, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
                if (BtnClearSearch != null) BtnClearSearch.Visibility = Visibility.Visible;
            }

            LstSnippets.ItemsSource = null;
            LstSnippets.ItemsSource = list;
            TxtSnippetCount.Text = $"{list.Count} اسنیپت";

            if (selectItem != null && list.Contains(selectItem))
            {
                LstSnippets.SelectedItem = selectItem;
                LstSnippets.ScrollIntoView(selectItem);
            }
            else if (selectItem != null)
            {
                var found = list.FirstOrDefault(s => s.Key.Equals(selectItem.Key, StringComparison.OrdinalIgnoreCase));
                if (found != null)
                {
                    LstSnippets.SelectedItem = found;
                    LstSnippets.ScrollIntoView(found);
                }
            }
            else if (LstSnippets.SelectedItem == null && list.Count > 0)
            {
                LstSnippets.SelectedIndex = 0;
            }
            else if (list.Count == 0)
            {
                TxtKey.Clear();
                TxtValue.Clear();
                UpdateEditorState(null);
            }
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void BtnClearSearch_Click(object sender, RoutedEventArgs e)
        {
            TxtSearch.Clear();
            TxtSearch.Focus();
        }

        private async void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            string text = TxtValue.Text;
            if (string.IsNullOrEmpty(text)) return;
            try
            {
                System.Windows.Clipboard.SetText(text);
                TxtCopyLabel.Text = "کپی شد! ✓";
                PathCopyIcon.Data = (StreamGeometry)FindResource("IconCheck");
                PathCopyIcon.Fill = new SolidColorBrush(Color.FromRgb(52, 211, 153)); // Green
                await Task.Delay(1300);
                TxtCopyLabel.Text = "کپی متن";
                PathCopyIcon.Data = (StreamGeometry)FindResource("IconCopy");
                PathCopyIcon.Fill = new SolidColorBrush(Color.FromRgb(148, 163, 184));
            }
            catch { }
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

                int count = rnd.Next(3, 6);
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

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void LstSnippets_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstSnippets.SelectedItem is Snippet selected)
            {
                TxtKey.Text = selected.Key;
                TxtValue.Text = selected.Value;
                UpdateEditorState(selected);
            }
            else
            {
                UpdateEditorState(null);
            }
        }

        private void BtnNew_Click(object sender, RoutedEventArgs e)
        {
            LstSnippets.SelectedItem = null;
            TxtKey.Clear();
            TxtValue.Clear();
            UpdateEditorState(null);
            TxtKey.Focus();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            string key = TxtKey.Text.Trim();
            string value = TxtValue.Text; // Preserve intentional spaces and newlines

            if (string.IsNullOrEmpty(key))
            {
                MessageBox.Show("لطفاً کلیدواژه میانبر را وارد نمایید.", "خطا", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Snippet? existing = SnippetManager.Snippets.Find(s => s.Key.Trim().Equals(key, StringComparison.OrdinalIgnoreCase));
            Snippet targetToSelect;

            if (LstSnippets.SelectedItem is Snippet selected)
            {
                if (existing != null && existing != selected)
                {
                    MessageBox.Show("این کلیدواژه قبلاً برای اسنیپت دیگری ثبت شده است!", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                selected.Key = key;
                selected.Value = value;
                targetToSelect = selected;
            }
            else
            {
                if (existing != null)
                {
                    MessageBox.Show("این کلیدواژه از قبل وجود دارد!", "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                var newSnippet = new Snippet { Key = key, Value = value };
                SnippetManager.Snippets.Add(newSnippet);
                targetToSelect = newSnippet;
            }

            SnippetManager.Save();
            RefreshList(targetToSelect);
            MessageBox.Show("اسنیپت با موفقیت ذخیره شد.", "موفقیت", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (LstSnippets.SelectedItem is Snippet selected)
            {
                if (MessageBox.Show($"آیا از حذف اسنیپت '{selected.Key}' اطمینان دارید؟", "تأیید حذف", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    SnippetManager.Snippets.Remove(selected);
                    SnippetManager.Save();
                    RefreshList();
                    BtnNew_Click(sender, e);
                }
            }
            else
            {
                MessageBox.Show("لطفاً ابتدا یک اسنیپت را از لیست انتخاب کنید.", "توجه");
            }
        }
    }
}
