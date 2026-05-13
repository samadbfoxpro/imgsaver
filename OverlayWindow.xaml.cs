using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace imgsaver
{
    public partial class OverlayWindow : Window
    {
        private Random rnd = new Random();

        public OverlayWindow()
        {
            InitializeComponent();
            this.Loaded += OverlayWindow_Loaded;
        }

        private void OverlayWindow_Loaded(object sender, RoutedEventArgs e)
        {
            SpawnParticles();
        }

        private void SpawnParticles()
        {
            // Center of the400x400 window
            double startX =200;
            double startY =200;

            // Larger decorative emojis
            string[] particles = { "✨", "⭐", "🟡", "🔸", "⚡", "🔥" };
            int count = rnd.Next(12,18);

            // Smaller sparkle emojis (delicate sparks)
            string[] smallSparks = { "✨", "✦", "✶", "✺", "⋆", "•" };

            // Spawn main decorative particles
            for (int i =0; i < count; i++)
            {
                TextBlock p = new TextBlock();
                p.Text = particles[rnd.Next(particles.Length)];
                p.FontSize = rnd.Next(18,28);
                p.FontFamily = new System.Windows.Media.FontFamily("Segoe UI Emoji"); // ensure colored emoji font
                p.RenderTransformOrigin = new System.Windows.Point(0.5,0.5);
                p.IsHitTestVisible = false;
                p.Opacity =1;

                Canvas.SetLeft(p, startX);
                Canvas.SetTop(p, startY);

                TransformGroup group = new TransformGroup();
                TranslateTransform trans = new TranslateTransform();
                RotateTransform rot = new RotateTransform();
                ScaleTransform scale = new ScaleTransform { ScaleX =0, ScaleY =0 };

                group.Children.Add(scale);
                group.Children.Add(rot);
                group.Children.Add(trans);
                p.RenderTransform = group;

                ParticleCanvas.Children.Add(p);
                AnimateParticle(p, trans, rot, scale);
            }

            // Spawn some smaller sparkle particles for a 'sprinkle' effect (smaller and a bit faster)
            int smallCount = rnd.Next(10,16);
            for (int i =0; i < smallCount; i++)
            {
                TextBlock s = new TextBlock();
                s.Text = smallSparks[rnd.Next(smallSparks.Length)];
                s.FontSize = rnd.Next(6,12); // smaller size
                s.FontFamily = new System.Windows.Media.FontFamily("Segoe UI Emoji");
                s.RenderTransformOrigin = new System.Windows.Point(0.5,0.5);
                s.IsHitTestVisible = false;
                s.Opacity =1;

                Canvas.SetLeft(s, startX);
                Canvas.SetTop(s, startY);

                TransformGroup group = new TransformGroup();
                TranslateTransform trans = new TranslateTransform();
                RotateTransform rot = new RotateTransform();
                ScaleTransform scale = new ScaleTransform { ScaleX =0, ScaleY =0 };

                group.Children.Add(scale);
                group.Children.Add(rot);
                group.Children.Add(trans);
                s.RenderTransform = group;

                ParticleCanvas.Children.Add(s);

                // Use faster, shorter animations for small sparks
                AnimateParticle(s, trans, rot, scale, small: true);
            }

            // Spawn a few small lightning emojis
            int lightningCount = rnd.Next(2,5); // a few lightning bolts
            for (int i =0; i < lightningCount; i++)
            {
                TextBlock l = new TextBlock();
                l.Text = "⚡";
                l.FontSize = rnd.Next(8,12); // small lightning
                l.FontFamily = new System.Windows.Media.FontFamily("Segoe UI Emoji");
                l.RenderTransformOrigin = new System.Windows.Point(0.5,0.5);
                l.IsHitTestVisible = false;
                l.Opacity =1;

                Canvas.SetLeft(l, startX);
                Canvas.SetTop(l, startY);

                TransformGroup group = new TransformGroup();
                TranslateTransform trans = new TranslateTransform();
                RotateTransform rot = new RotateTransform();
                ScaleTransform scale = new ScaleTransform { ScaleX =0, ScaleY =0 };

                group.Children.Add(scale);
                group.Children.Add(rot);
                group.Children.Add(trans);
                l.RenderTransform = group;

                ParticleCanvas.Children.Add(l);

                // Lightning are small but faster and snappier
                AnimateParticle(l, trans, rot, scale, small: true, isLightning: true);
            }

            // Auto close window after animations finish
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.2) };
            timer.Tick += (s, e) => { timer.Stop(); this.Close(); };
            timer.Start();
        }

        private void AnimateParticle(TextBlock particle, TranslateTransform trans, RotateTransform rot, ScaleTransform scale, bool small = false, bool isLightning = false)
        {
            double durationSec;
            if (isLightning)
            {
                durationSec = rnd.NextDouble() *0.25 +0.18; // very snappy
            }
            else if (small)
            {
                durationSec = rnd.NextDouble() *0.35 +0.22; // small: ~0.22-0.57s (a bit faster)
            }
            else
            {
                durationSec = rnd.NextDouble() *0.4 +0.4; // normal:0.4-0.8s
            }

            Duration duration = new Duration(TimeSpan.FromSeconds(durationSec));

            double angle = rnd.NextDouble() *2 * Math.PI;
            double speed;
            if (isLightning)
                speed = rnd.Next(140,260); // lightning faster
            else if (small)
                speed = rnd.Next(90,180); // small slightly faster
            else
                speed = rnd.Next(60,160);

            DoubleAnimation animX = new DoubleAnimation(0, Math.Cos(angle) * speed, duration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            DoubleAnimation animY = new DoubleAnimation(0, Math.Sin(angle) * speed, duration) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            DoubleAnimation animRot = new DoubleAnimation(0, rnd.Next(-360,360), duration);

            // Scale target smaller for small particles
            double targetScale = isLightning ? (rnd.NextDouble() *0.5 +0.6) : (small ? (rnd.NextDouble() *0.5 +0.5) :1.2);
            DoubleAnimation animScale = new DoubleAnimation(0, targetScale, new Duration(TimeSpan.FromSeconds(0.12))) { EasingFunction = new BackEase { Amplitude =1, EasingMode = EasingMode.EaseOut } };
            DoubleAnimation animFade = new DoubleAnimation(1,0, duration) { BeginTime = TimeSpan.FromSeconds(durationSec *0.45) };

            trans.BeginAnimation(TranslateTransform.XProperty, animX);
            trans.BeginAnimation(TranslateTransform.YProperty, animY);
            rot.BeginAnimation(RotateTransform.AngleProperty, animRot);
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, animScale);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, animScale);
            particle.BeginAnimation(UIElement.OpacityProperty, animFade);
        }
    }
}