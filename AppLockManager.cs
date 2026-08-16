using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace imgsaver
{
    public static class AppLockManager
    {
        private static readonly object _lock = new object();
        private static readonly List<Window> _hiddenWindows = new List<Window>();
        private static AuthLockWindow? _activeLockWindow = null;

        public static bool IsLocked { get; private set; } = false;

        public static event EventHandler<bool>? LockStateChanged;

        public static void LockApp()
        {
            if (System.Windows.Application.Current == null) return;

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                lock (_lock)
                {
                    if (IsLocked)
                    {
                        // Already locked; bring active lock window to front if existing
                        _activeLockWindow?.Activate();
                        return;
                    }

                    IsLocked = true;
                    SecurityManager.RevokeSession();
                    _hiddenWindows.Clear();

                    // Collect all currently open and visible windows
                    foreach (Window win in System.Windows.Application.Current.Windows)
                    {
                        if (win != null && win is not AuthLockWindow && win.IsVisible)
                        {
                            _hiddenWindows.Add(win);
                            win.Visibility = Visibility.Hidden;
                        }
                    }

                    LockStateChanged?.Invoke(null, true);

                    // Show AuthLockWindow
                    _activeLockWindow = new AuthLockWindow(isRuntimeLock: true);
                    _activeLockWindow.Closed += (s, e) =>
                    {
                        _activeLockWindow = null;
                    };

                    bool? result = _activeLockWindow.ShowDialog();

                    if (result == true)
                    {
                        UnlockApp();
                    }
                    else
                    {
                        // If the lock window was closed without unlocking during runtime lock,
                        // prompt again or shutdown if user intended to exit.
                        if (IsLocked)
                        {
                            System.Windows.Application.Current.Shutdown();
                        }
                    }
                }
            });
        }

        public static void UnlockApp()
        {
            if (System.Windows.Application.Current == null) return;

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                lock (_lock)
                {
                    IsLocked = false;

                    // Restore visibility of all previously active windows with smooth revealing animation
                    foreach (var win in _hiddenWindows)
                    {
                        try
                        {
                            if (win != null)
                            {
                                win.Opacity = 0.0;
                                win.Visibility = Visibility.Visible;

                                var fadeIn = new System.Windows.Media.Animation.DoubleAnimation
                                {
                                    From = 0.0,
                                    To = 1.0,
                                    Duration = TimeSpan.FromMilliseconds(260),
                                    EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
                                };
                                win.BeginAnimation(UIElement.OpacityProperty, fadeIn);
                            }
                        }
                        catch { }
                    }

                    // Activate the primary or first window
                    var primary = _hiddenWindows.FirstOrDefault(w => w is MainWindow) ?? _hiddenWindows.FirstOrDefault();
                    primary?.Activate();

                    _hiddenWindows.Clear();

                    LockStateChanged?.Invoke(null, false);
                }
            });
        }
    }
}
