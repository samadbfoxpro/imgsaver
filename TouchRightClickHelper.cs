using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace imgsaver
{
    public static class TouchRightClickHelper
    {
        private static DispatcherTimer? _timer;
        private static System.Windows.Controls.TextBox? _targetTextBox;
        private static System.Windows.Point _touchPoint;

        public static void Register(System.Windows.Controls.TextBox textBox)
        {
            if (textBox == null) return;
            
            // Disable the slow Windows OS press-and-hold touch right-click delay (default ~1000ms)
            Stylus.SetIsPressAndHoldEnabled(textBox, false);
            
            textBox.PreviewTouchDown += TextBox_TouchDown;
            textBox.PreviewTouchUp += TextBox_TouchUp;
            textBox.PreviewTouchMove += TextBox_TouchMove;
            textBox.TouchLeave += TextBox_TouchLeave;
        }

        private static void TextBox_TouchDown(object? sender, TouchEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox)
            {
                _targetTextBox = textBox;
                _touchPoint = e.GetTouchPoint(textBox).Position;

                _timer?.Stop();
                _timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(300) // Fast 300ms long-press registration!
                };
                _timer.Tick += Timer_Tick;
                _timer.Start();
            }
        }

        private static void TextBox_TouchUp(object? sender, TouchEventArgs e)
        {
            _timer?.Stop();
            _timer = null;
        }

        private static void TextBox_TouchMove(object? sender, TouchEventArgs e)
        {
            if (_timer != null && sender is System.Windows.Controls.TextBox textBox)
            {
                var currentPoint = e.GetTouchPoint(textBox).Position;
                // If user drags/moves finger more than 10 pixels, cancel long press (they are likely selecting or scrolling)
                if (Math.Abs(currentPoint.X - _touchPoint.X) > 10 || Math.Abs(currentPoint.Y - _touchPoint.Y) > 10)
                {
                    _timer.Stop();
                    _timer = null;
                }
            }
        }

        private static void TextBox_TouchLeave(object? sender, TouchEventArgs e)
        {
            _timer?.Stop();
            _timer = null;
        }

        private static void Timer_Tick(object? sender, EventArgs e)
        {
            _timer?.Stop();
            _timer = null;

            if (_targetTextBox != null && _targetTextBox.ContextMenu != null)
            {
                _targetTextBox.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Relative;
                _targetTextBox.ContextMenu.PlacementTarget = _targetTextBox;
                _targetTextBox.ContextMenu.HorizontalOffset = _touchPoint.X;
                _targetTextBox.ContextMenu.VerticalOffset = _touchPoint.Y;
                _targetTextBox.ContextMenu.IsOpen = true;
            }
        }
    }
}
