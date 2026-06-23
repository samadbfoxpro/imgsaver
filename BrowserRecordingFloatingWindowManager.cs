using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace imgsaver
{
    public static class BrowserRecordingFloatingWindowManager
    {
        private static BrowserRecordingFloatingWindow? _window;
        private static bool _pendingShow;

        public static void SyncWithSettings(BrowserSettings settings)
        {
            ApplyDeferred(settings.ShowFloatingRecordPlayer);
        }

        private static void ApplyDeferred(bool shouldShow)
        {
            var app = System.Windows.Application.Current;
            if (app == null || app.Dispatcher.HasShutdownStarted || app.Dispatcher.HasShutdownFinished)
                return;

            _pendingShow = shouldShow;
            app.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    if (_pendingShow) Show();
                    else Hide();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Floating browser recording player failed: {ex.Message}");
                }
            }), DispatcherPriority.ApplicationIdle);
        }

        private static void Show()
        {
            if (_window == null)
            {
                _window = new BrowserRecordingFloatingWindow();
                _window.Closed += (_, _) => _window = null;
            }

            if (!_window.IsVisible)
                _window.Show();

            _window.Topmost = false;
            _window.Topmost = true;
        }

        private static void Hide()
        {
            if (_window == null) return;

            try
            {
                _window.Close();
            }
            finally
            {
                _window = null;
            }
        }
    }
}
