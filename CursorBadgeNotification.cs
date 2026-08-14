using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace imgsaver
{
    /// <summary>
    /// Independent UI component that renders floating cursor notifications 
    /// completely decoupled from Combiner, TagReplacer, or ExtraTemplate logic.
    /// Includes thread-safe debouncing to prevent multiple stacked notifications on rapid events.
    /// </summary>
    public static class CursorBadgeNotification
    {
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        private static readonly object _lockObj = new object();
        private static DateTime _lastShownTime = DateTime.MinValue;
        private static Window? _activeWindow = null;

        public static void Show(string message, string borderHex = "#00E5FF", string textHex = "#00E5FF", string bgHex = "#F0181C24")
        {
            lock (_lockObj)
            {
                var now = DateTime.Now;
                // Debounce / Cooldown: Ignore rapid duplicate triggers within 500ms
                if ((now - _lastShownTime).TotalMilliseconds < 500)
                {
                    return;
                }
                _lastShownTime = now;
            }

            System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    // Close previous active badge window immediately if still visible
                    if (_activeWindow != null)
                    {
                        try { _activeWindow.Close(); } catch { }
                        _activeWindow = null;
                    }

                    GetCursorPos(out POINT pt);

                    var win = new Window
                    {
                        WindowStyle = WindowStyle.None,
                        AllowsTransparency = true,
                        Background = System.Windows.Media.Brushes.Transparent,
                        ShowInTaskbar = false,
                        Topmost = true,
                        SizeToContent = SizeToContent.WidthAndHeight,
                        Focusable = false,
                        IsHitTestVisible = false,
                        Left = pt.X + 15,
                        Top = pt.Y - 25
                    };

                    _activeWindow = win;

                    var border = new Border
                    {
                        Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(bgHex)),
                        BorderBrush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(borderHex)),
                        BorderThickness = new Thickness(1.2),
                        CornerRadius = new CornerRadius(10),
                        Padding = new Thickness(10, 4, 10, 4)
                    };

                    var dropShadow = new DropShadowEffect
                    {
                        BlurRadius = 12,
                        ShadowDepth = 2,
                        Opacity = 0.6,
                        Color = System.Windows.Media.Colors.Black
                    };
                    border.Effect = dropShadow;

                    var txt = new TextBlock
                    {
                        Text = message,
                        FontSize = 11.5,
                        FontWeight = FontWeights.Bold,
                        Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(textHex)),
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    border.Child = txt;
                    win.Content = border;
                    win.Show();

                    // Float upward and fade out animation
                    double startTop = win.Top;
                    var topAnim = new DoubleAnimation
                    {
                        From = startTop,
                        To = startTop - 35,
                        Duration = TimeSpan.FromMilliseconds(900),
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    };

                    var opacityAnim = new DoubleAnimation
                    {
                        From = 1.0,
                        To = 0.0,
                        Duration = TimeSpan.FromMilliseconds(900),
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
                    };

                    opacityAnim.Completed += (s, e) =>
                    {
                        try 
                        {
                            if (_activeWindow == win) _activeWindow = null;
                            win.Close(); 
                        } 
                        catch { }
                    };

                    win.BeginAnimation(Window.TopProperty, topAnim);
                    win.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
                }
                catch { }
            });
        }

        /// <summary>
        /// Floating notification for Combiner action (Neon Cyan)
        /// </summary>
        public static void ShowCombiner(string message = "⚡ Combined!")
        {
            Show(message, "#00E5FF", "#00E5FF", "#F0181C24");
        }

        /// <summary>
        /// Floating notification for PH Tag Replacement action (Emerald Green)
        /// </summary>
        public static void ShowTagReplaced(string message = "🧩 Tag Replaced!")
        {
            Show(message, "#10B981", "#00FF87", "#F00F241B");
        }

        /// <summary>
        /// Floating notification for Normal Copy action (Emerald Green)
        /// </summary>
        public static void ShowCopied(string message = "📋 Copied!")
        {
            Show(message, "#10B981", "#00FF87", "#F00F241B");
        }

        /// <summary>
        /// Floating notification for Replace action (Vivid Orange)
        /// </summary>
        public static void ShowReplaced(string message = "🔄 Replaced!")
        {
            Show(message, "#FF6A00", "#FF8C00", "#F0291705");
        }

        /// <summary>
        /// Floating notification for Extra Template action (Neon Violet/Magenta)
        /// </summary>
        public static void ShowExtraApplied(string message = "✨ Extra Applied!")
        {
            Show(message, "#C084FC", "#F472B6", "#F022142E");
        }
    }
}
