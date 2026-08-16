using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace imgsaver
{
    public partial class AuthLockWindow : Window
    {
        private readonly bool _isSetupMode;
        private readonly bool _isRuntimeLock;
        private readonly bool _forcePatternSetup;
        private bool _isPasswordRevealed = false;
        private bool _isClosingAnimated = false;
        private LockAuthType _currentMode = LockAuthType.Pattern;

        // Pattern State
        private bool _isPatternDrawing = false;
        private List<int> _currentPattern = new List<int>();
        private List<Border> _patternNodes = new List<Border>();
        private Polyline _patternLine;
        private Line _activeSegment;
        
        // Setup Pattern state
        private string _setupFirstPattern = null;

        public AuthLockWindow(bool isRuntimeLock = false, bool forcePatternSetup = false)
        {
            InitializeComponent();

            _isRuntimeLock = isRuntimeLock;
            _forcePatternSetup = forcePatternSetup;
            _isSetupMode = !SecurityManager.IsAnyAuthConfigured() || forcePatternSetup;

            // Determine default mode based on what's configured or preferred
            if (_isSetupMode || _forcePatternSetup)
            {
                _currentMode = LockAuthType.Pattern;
            }
            else
            {
                _currentMode = SecurityManager.GetPreferredAuthType();
                if (_currentMode == LockAuthType.Pattern && !SecurityManager.IsPatternConfigured())
                    _currentMode = LockAuthType.Password;
                else if (_currentMode == LockAuthType.Password && !SecurityManager.IsPasswordConfigured())
                    _currentMode = LockAuthType.Pattern;
            }

            // Global key listener: any printable key while in Pattern mode → smoothly switch to Password
            PreviewKeyDown += Window_PreviewKeyDown;

            Loaded += (_, _) =>
            {
                InitializePatternGrid();
                ConfigureUIForMode();

                if (_currentMode == LockAuthType.Password)
                    TxtPasswordBox.Focus();

                Opacity = 0;
                ContainerScale.ScaleX = 0.94;
                ContainerScale.ScaleY = 0.94;

                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                var scaleInX = new DoubleAnimation(0.94, 1.0, TimeSpan.FromMilliseconds(220))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                var scaleInY = new DoubleAnimation(0.94, 1.0, TimeSpan.FromMilliseconds(220))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                BeginAnimation(OpacityProperty, fadeIn);
                ContainerScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleInX);
                ContainerScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleInY);
            };
        }

        private void InitializePatternGrid()
        {
            PatternCanvas.Children.Clear();
            _patternNodes.Clear();

            double nodeSize = 16;
            double hitSize = 48; // larger hit area
            double padding = 20;
            double canvasWidth = PatternCanvas.Width;
            double stepX = (canvasWidth - padding * 2 - hitSize) / 2;
            double stepY = (PatternCanvas.Height - padding * 2 - hitSize) / 2;

            _patternLine = new Polyline
            {
                Stroke = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0ea5e9")),
                StrokeThickness = 4,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                IsHitTestVisible = false
            };
            PatternCanvas.Children.Add(_patternLine);

            _activeSegment = new Line
            {
                Stroke = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0ea5e9")),
                StrokeThickness = 4,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed
            };
            PatternCanvas.Children.Add(_activeSegment);

            for (int i = 0; i < 9; i++)
            {
                int row = i / 3;
                int col = i % 3;

                double x = padding + col * stepX;
                double y = padding + row * stepY;

                Border hitArea = new Border
                {
                    Width = hitSize,
                    Height = hitSize,
                    Background = System.Windows.Media.Brushes.Transparent,
                    Tag = i
                };
                Canvas.SetLeft(hitArea, x);
                Canvas.SetTop(hitArea, y);

                Border dot = new Border
                {
                    Width = nodeSize,
                    Height = nodeSize,
                    Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#334155")),
                    CornerRadius = new CornerRadius(nodeSize / 2),
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center,
                    IsHitTestVisible = false
                };

                hitArea.Child = dot;
                _patternNodes.Add(hitArea);
                PatternCanvas.Children.Add(hitArea);
            }
        }

        // ─────────────────────────────────────────
        // Global key intercept – typing switches mode
        // ─────────────────────────────────────────
        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Only react when currently showing Pattern and not in an animation
            if (_currentMode != LockAuthType.Pattern || _isClosingAnimated || _isPatternDrawing) return;
            // Ignore modifier-only, navigation, function keys
            var key = e.Key;
            if (key == System.Windows.Input.Key.System) key = e.SystemKey;
            bool isPrintable = (key >= System.Windows.Input.Key.A && key <= System.Windows.Input.Key.Z)
                            || (key >= System.Windows.Input.Key.D0 && key <= System.Windows.Input.Key.D9)
                            || (key >= System.Windows.Input.Key.NumPad0 && key <= System.Windows.Input.Key.NumPad9)
                            || (key >= System.Windows.Input.Key.OemSemicolon && key <= System.Windows.Input.Key.OemTilde)
                            || key == System.Windows.Input.Key.Space
                            || (key >= System.Windows.Input.Key.Oem1 && key <= System.Windows.Input.Key.Oem102);

            if (!isPrintable) return;

            // Get the character that was typed
            string firstChar = GetCharFromKey(key);

            // Animate to password mode, then inject the first char
            SwitchToPasswordWithAnimation(firstChar);
            e.Handled = true;
        }

        private static string GetCharFromKey(System.Windows.Input.Key key)
        {
            try
            {
                bool shift = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Shift) != 0;
                System.Windows.Input.KeyConverter kc = new System.Windows.Input.KeyConverter();
                string s = kc.ConvertToString(key) ?? "";
                return (s.Length == 1) ? (shift ? s.ToUpper() : s.ToLower()) : "";
            }
            catch { return ""; }
        }

        // ─────────────────────────────────────────
        // Animated Switch: Pattern → Password
        // ─────────────────────────────────────────
        private bool _isSwitchAnimating = false;

        private void SwitchToPasswordWithAnimation(string seedChar = "")
        {
            if (_isSwitchAnimating) return;
            _isSwitchAnimating = true;
            _currentMode = LockAuthType.Password;

            // Update metadata labels
            ApplyModeLabels();

            // Prepare Password panel: off-screen right, visible but transparent
            PasswordViewContainer.Visibility = Visibility.Visible;
            PasswordViewTranslate.X = ActualWidth > 0 ? ActualWidth : 420;
            PasswordViewContainer.Opacity = 0;

            // Duration
            var dur = TimeSpan.FromMilliseconds(340);
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            // Pattern slides OUT to the left
            var patSlide = new DoubleAnimation(0, -(ActualWidth > 0 ? ActualWidth : 420), dur) { EasingFunction = ease };
            var patFade  = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(220)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };

            // Password slides IN from the right
            var passSlide = new DoubleAnimation(PasswordViewTranslate.X, 0, dur) { EasingFunction = ease };
            var passFade  = new DoubleAnimation(0, 1, dur) { EasingFunction = ease };

            patFade.Completed += (_, _) =>
            {
                PatternViewContainer.Visibility = Visibility.Collapsed;
                PatternViewTranslate.X = 0;
                PatternViewContainer.Opacity = 1;
                _isSwitchAnimating = false;

                // Inject first char if any
                if (!string.IsNullOrEmpty(seedChar))
                {
                    TxtPasswordBox.Password = seedChar;
                    // Move caret to end by re-focusing
                }
                TxtPasswordBox.Focus();

                // Update switch-mode button icon
                if (TryFindResource("IconPattern") is StreamGeometry patGeom)
                    SwitchIconPath.Data = patGeom;
                TxtSwitchAuthMode.Text = "ورود با الگوی ۹ نقطه‌ای";
                BtnSwitchAuthMode.Visibility = (SecurityManager.IsPasswordConfigured() && SecurityManager.IsPatternConfigured() && !_forcePatternSetup)
                    ? Visibility.Visible : Visibility.Collapsed;
            };

            PatternViewTranslate.BeginAnimation(TranslateTransform.XProperty, patSlide);
            PatternViewContainer.BeginAnimation(UIElement.OpacityProperty, patFade);
            PasswordViewTranslate.BeginAnimation(TranslateTransform.XProperty, passSlide);
            PasswordViewContainer.BeginAnimation(UIElement.OpacityProperty, passFade);
        }

        // ─────────────────────────────────────────
        // Animated Switch: Password → Pattern
        // ─────────────────────────────────────────
        private void SwitchToPatternWithAnimation()
        {
            if (_isSwitchAnimating) return;
            _isSwitchAnimating = true;
            _currentMode = LockAuthType.Pattern;

            ApplyModeLabels();
            ResetPatternGrid();

            // Prepare Pattern panel: off-screen left, visible
            PatternViewContainer.Visibility = Visibility.Visible;
            PatternViewTranslate.X = -(ActualWidth > 0 ? ActualWidth : 420);
            PatternViewContainer.Opacity = 0;

            var dur  = TimeSpan.FromMilliseconds(340);
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };

            // Password slides OUT to the right
            var passSlide = new DoubleAnimation(0, (ActualWidth > 0 ? ActualWidth : 420), dur) { EasingFunction = ease };
            var passFade  = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(220)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };

            // Pattern slides IN from the left
            var patSlide = new DoubleAnimation(PatternViewTranslate.X, 0, dur) { EasingFunction = ease };
            var patFade  = new DoubleAnimation(0, 1, dur) { EasingFunction = ease };

            passFade.Completed += (_, _) =>
            {
                PasswordViewContainer.Visibility = Visibility.Collapsed;
                PasswordViewTranslate.X = 0;
                PasswordViewContainer.Opacity = 1;
                _isSwitchAnimating = false;

                if (TryFindResource("IconKey") is StreamGeometry keyGeom)
                    SwitchIconPath.Data = keyGeom;
                TxtSwitchAuthMode.Text = "ورود با رمز عبور (Password)";
                BtnSwitchAuthMode.Visibility = (SecurityManager.IsPasswordConfigured() && SecurityManager.IsPatternConfigured() && !_forcePatternSetup)
                    ? Visibility.Visible : Visibility.Collapsed;
            };

            PasswordViewTranslate.BeginAnimation(TranslateTransform.XProperty, passSlide);
            PasswordViewContainer.BeginAnimation(UIElement.OpacityProperty, passFade);
            PatternViewTranslate.BeginAnimation(TranslateTransform.XProperty, patSlide);
            PatternViewContainer.BeginAnimation(UIElement.OpacityProperty, patFade);
        }

        private void ApplyModeLabels()
        {
            if (_isSetupMode || _forcePatternSetup)
            {
                TxtTitle.Text   = _currentMode == LockAuthType.Pattern ? "تعریف الگوی امنیتی" : "تعریف رمز عبور اصلی";
                TxtSubtitle.Text = "برای امنیت بیشتر برنامه، یک روش ورود تعیین کنید.";
                TxtSubmitButton.Text = "ذخیره و ورود به برنامه";
                ConfirmPasswordPanel.Visibility = _currentMode == LockAuthType.Password ? Visibility.Visible : Visibility.Collapsed;
                Height = _currentMode == LockAuthType.Pattern ? 520 : 580;
            }
            else
            {
                TxtTitle.Text    = _isRuntimeLock ? "قفل امن برنامه" : "ورود امن به برنامه";
                TxtSubtitle.Text = _isRuntimeLock
                    ? "برنامه قفل شده است. برای بازگشت به صفحات، تایید هویت کنید."
                    : "برای دسترسی به محیط برنامه، تایید هویت کنید.";
                TxtSubmitButton.Text = "بازگشایی و ورود (Unlock)";
                ConfirmPasswordPanel.Visibility = Visibility.Collapsed;
                Height = _currentMode == LockAuthType.Pattern ? 500 : 530;
            }
            ErrorBadge.Visibility = Visibility.Collapsed;
        }

        private void ConfigureUIForMode()
        {
            ApplyModeLabels();

            if (_currentMode == LockAuthType.Pattern)
            {
                PasswordViewContainer.Visibility = Visibility.Collapsed;
                PasswordViewTranslate.X = 0;
                PasswordViewContainer.Opacity = 1;
                PatternViewContainer.Visibility = Visibility.Visible;
                PatternViewTranslate.X = 0;
                PatternViewContainer.Opacity = 1;

                if (TryFindResource("IconKey") is StreamGeometry keyGeom)
                    SwitchIconPath.Data = keyGeom;
                TxtSwitchAuthMode.Text = "ورود با رمز عبور (Password)";
            }
            else
            {
                PatternViewContainer.Visibility = Visibility.Collapsed;
                PatternViewTranslate.X = 0;
                PatternViewContainer.Opacity = 1;
                PasswordViewContainer.Visibility = Visibility.Visible;
                PasswordViewTranslate.X = 0;
                PasswordViewContainer.Opacity = 1;

                if (TryFindResource("IconPattern") is StreamGeometry patGeom)
                    SwitchIconPath.Data = patGeom;
                TxtSwitchAuthMode.Text = "ورود با الگوی ۹ نقطه‌ای";
                TxtPasswordBox.Focus();
            }

            BtnSwitchAuthMode.Visibility = _forcePatternSetup ? Visibility.Collapsed
                : (!_isSetupMode && SecurityManager.IsPasswordConfigured() && SecurityManager.IsPatternConfigured()) ? Visibility.Visible
                : (_isSetupMode) ? Visibility.Visible
                : Visibility.Collapsed;

            ResetPatternGrid();
        }

        private void BtnSwitchAuthMode_Click(object sender, RoutedEventArgs e)
        {
            if (_isSwitchAnimating) return;
            if (_currentMode == LockAuthType.Pattern)
                SwitchToPasswordWithAnimation();
            else
                SwitchToPatternWithAnimation();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left) DragMove();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnTogglePeek_Click(object sender, RoutedEventArgs e)
        {
            _isPasswordRevealed = !_isPasswordRevealed;
            if (_isPasswordRevealed)
            {
                TxtPasswordPlain.Text = TxtPasswordBox.Password;
                TxtPasswordPlain.Visibility = Visibility.Visible;
                TxtPasswordBox.Visibility = Visibility.Collapsed;
                TxtPasswordPlain.Focus();
                TxtPasswordPlain.CaretIndex = TxtPasswordPlain.Text.Length;
                if (TryFindResource("IconEyeOff") is StreamGeometry offGeom) PeekIconPath.Data = offGeom;
            }
            else
            {
                TxtPasswordBox.Password = TxtPasswordPlain.Text;
                TxtPasswordBox.Visibility = Visibility.Visible;
                TxtPasswordPlain.Visibility = Visibility.Collapsed;
                TxtPasswordBox.Focus();
                if (TryFindResource("IconEye") is StreamGeometry onGeom) PeekIconPath.Data = onGeom;
            }
        }

        private string GetCurrentPassword() => _isPasswordRevealed ? TxtPasswordPlain.Text : TxtPasswordBox.Password;

        private void Input_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                ProcessPasswordAuthentication();
                e.Handled = true;
            }
        }

        private void BtnSubmit_Click(object sender, RoutedEventArgs e) => ProcessPasswordAuthentication();

        private void ProcessPasswordAuthentication()
        {
            if (_isClosingAnimated) return;

            string password = GetCurrentPassword();

            if (string.IsNullOrEmpty(password))
            {
                ShowError("لطفاً رمز عبور را وارد کنید.");
                return;
            }

            if (_isSetupMode)
            {
                if (password.Length < 3)
                {
                    ShowError("رمز عبور باید حداقل ۳ کاراکتر باشد.");
                    return;
                }
                if (password != TxtConfirmPasswordBox.Password)
                {
                    ShowError("رمز عبور و تکرار آن یکسان نیستند.");
                    return;
                }

                if (SecurityManager.SetMasterPassword(password)) AnimateSuccessAndClose();
                else ShowError("خطا در ذخیره‌سازی رمز عبور امنیتی.");
            }
            else
            {
                if (SecurityManager.ValidatePassword(password))
                {
                    ErrorBadge.Visibility = Visibility.Collapsed;
                    AnimateSuccessAndClose();
                }
                else
                {
                    ShowError("رمز عبور وارد شده اشتباه است.");
                    ShakeWindow();
                    TxtPasswordBox.Password = "";
                    TxtPasswordPlain.Text = "";
                    TxtPasswordBox.Focus();
                }
            }
        }

        // --- Pattern Drawing Logic ---

        private System.Windows.Point GetNodeCenter(Border node)
        {
            double x = Canvas.GetLeft(node) + node.Width / 2;
            double y = Canvas.GetTop(node) + node.Height / 2;
            return new System.Windows.Point(x, y);
        }

        private void HandlePointerDown(System.Windows.Point pos)
        {
            if (_isClosingAnimated) return;
            ResetPatternGrid();
            _isPatternDrawing = true;
            HandlePointerMove(pos);
        }

        private void HandlePointerMove(System.Windows.Point pos)
        {
            if (!_isPatternDrawing || _isClosingAnimated) return;

            // Update active segment
            if (_currentPattern.Count > 0)
            {
                var lastNode = _patternNodes[_currentPattern.Last()];
                var center = GetNodeCenter(lastNode);
                _activeSegment.X1 = center.X;
                _activeSegment.Y1 = center.Y;
                _activeSegment.X2 = pos.X;
                _activeSegment.Y2 = pos.Y;
                _activeSegment.Visibility = Visibility.Visible;
            }

            // Check hit
            foreach (var node in _patternNodes)
            {
                double left = Canvas.GetLeft(node);
                double top = Canvas.GetTop(node);
                Rect rect = new Rect(left, top, node.Width, node.Height);

                if (rect.Contains(pos))
                {
                    int idx = (int)node.Tag;
                    if (!_currentPattern.Contains(idx))
                    {
                        _currentPattern.Add(idx);
                        
                        var dot = (Border)node.Child;
                        dot.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0ea5e9"));
                        dot.Width = 20;
                        dot.Height = 20;
                        dot.CornerRadius = new CornerRadius(10);
                        
                        // Add shadow effect to active dot
                        dot.Effect = new System.Windows.Media.Effects.DropShadowEffect
                        {
                            Color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0ea5e9"),
                            BlurRadius = 10,
                            ShadowDepth = 0
                        };

                        _patternLine.Points.Add(GetNodeCenter(node));
                    }
                    break; // can only hit one at a time
                }
            }
        }

        private async void HandlePointerUp()
        {
            if (!_isPatternDrawing || _isClosingAnimated) return;
            _isPatternDrawing = false;
            _activeSegment.Visibility = Visibility.Collapsed;

            if (_currentPattern.Count < 4)
            {
                ShowPatternFeedback(false);
                TxtPatternStatus.Text = "الگو باید حداقل ۴ نقطه باشد.";
                TxtPatternStatus.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FCA5A5"));
                ShakeWindow();
                await Task.Delay(500);
                ResetPatternGrid();
                return;
            }

            string patternStr = string.Join("-", _currentPattern);

            if (_isSetupMode || (_currentMode == LockAuthType.Pattern && (!SecurityManager.IsPatternConfigured() || _forcePatternSetup)))
            {
                if (_setupFirstPattern == null)
                {
                    _setupFirstPattern = patternStr;
                    ShowPatternFeedback(true);
                    TxtPatternStatus.Text = "حالا الگو را دوباره رسم کنید";
                    TxtPatternStatus.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0ea5e9"));
                    await Task.Delay(500);
                    ResetPatternGrid();
                }
                else
                {
                    if (_setupFirstPattern == patternStr)
                    {
                        ShowPatternFeedback(true);
                        TxtPatternStatus.Text = "الگو تأیید شد.";
                        TxtPatternStatus.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#10B981"));
                        
                        if (SecurityManager.SetMasterPattern(patternStr))
                        {
                            AnimateSuccessAndClose();
                        }
                        else
                        {
                            TxtPatternStatus.Text = "خطا در ذخیره الگو!";
                            TxtPatternStatus.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FCA5A5"));
                        }
                    }
                    else
                    {
                        ShowPatternFeedback(false);
                        TxtPatternStatus.Text = "الگوها مطابقت ندارند. دوباره تلاش کنید.";
                        TxtPatternStatus.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FCA5A5"));
                        ShakeWindow();
                        _setupFirstPattern = null; // reset
                        await Task.Delay(500);
                        ResetPatternGrid();
                    }
                }
            }
            else // Validation Mode
            {
                if (SecurityManager.ValidatePattern(patternStr))
                {
                    ShowPatternFeedback(true);
                    TxtPatternStatus.Text = "ورود موفق";
                    TxtPatternStatus.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#10B981"));
                    AnimateSuccessAndClose();
                }
                else
                {
                    ShowPatternFeedback(false);
                    TxtPatternStatus.Text = "الگوی اشتباه است.";
                    TxtPatternStatus.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FCA5A5"));
                    ShakeWindow();
                    await Task.Delay(500);
                    ResetPatternGrid();
                }
            }
        }

        private void ResetPatternGrid()
        {
            _currentPattern.Clear();
            if (_patternLine != null) _patternLine.Points.Clear();
            if (_activeSegment != null) _activeSegment.Visibility = Visibility.Collapsed;

            foreach (var node in _patternNodes)
            {
                var dot = (Border)node.Child;
                dot.Background = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#334155"));
                dot.Width = 16;
                dot.Height = 16;
                dot.CornerRadius = new CornerRadius(8);
                dot.Effect = null;
            }

            if (_patternLine != null)
                _patternLine.Stroke = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#0ea5e9"));
            
            bool isSettingUpPattern = _isSetupMode || (_currentMode == LockAuthType.Pattern && !SecurityManager.IsPatternConfigured());
            
            if (isSettingUpPattern && _setupFirstPattern == null)
            {
                TxtPatternStatus.Text = "الگوی خود را رسم کنید";
                TxtPatternStatus.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#94A3B8"));
            }
            else if (!isSettingUpPattern)
            {
                TxtPatternStatus.Text = "الگوی خود را رسم کنید";
                TxtPatternStatus.Foreground = new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#94A3B8"));
            }
        }

        private void ShowPatternFeedback(bool success)
        {
            System.Windows.Media.Color color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(success ? "#10B981" : "#EF4444");
            SolidColorBrush brush = new SolidColorBrush(color);

            _patternLine.Stroke = brush;
            foreach (var idx in _currentPattern)
            {
                var dot = (Border)_patternNodes[idx].Child;
                dot.Background = brush;
                dot.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = color,
                    BlurRadius = 10,
                    ShadowDepth = 0
                };
            }
        }

        // Pointer Events routing
        private void PatternCanvas_PointerDown(object sender, System.Windows.Input.InputEventArgs e)
        {
            if (e is System.Windows.Input.MouseButtonEventArgs me) HandlePointerDown(me.GetPosition(PatternCanvas));
            else if (e is System.Windows.Input.TouchEventArgs te) HandlePointerDown(te.GetTouchPoint(PatternCanvas).Position);
            
            if (e is System.Windows.Input.MouseButtonEventArgs m) PatternCanvas.CaptureMouse();
            else if (e is System.Windows.Input.TouchEventArgs t) PatternCanvas.CaptureTouch(t.TouchDevice);
        }

        private void PatternCanvas_PointerMove(object sender, System.Windows.Input.InputEventArgs e)
        {
            if (e is System.Windows.Input.MouseEventArgs me) HandlePointerMove(me.GetPosition(PatternCanvas));
            else if (e is System.Windows.Input.TouchEventArgs te) HandlePointerMove(te.GetTouchPoint(PatternCanvas).Position);
        }

        private void PatternCanvas_PointerUp(object sender, System.Windows.Input.InputEventArgs e)
        {
            HandlePointerUp();
            if (e is System.Windows.Input.MouseButtonEventArgs m) PatternCanvas.ReleaseMouseCapture();
            else if (e is System.Windows.Input.TouchEventArgs t) PatternCanvas.ReleaseTouchCapture(t.TouchDevice);
        }

        private void PatternCanvas_PointerLeave(object sender, System.Windows.Input.InputEventArgs e)
        {
            if (_isPatternDrawing) HandlePointerUp();
        }

        // --- Window Animation ---

        private void AnimateSuccessAndClose()
        {
            _isClosingAnimated = true;

            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            var scaleOutX = new DoubleAnimation(1.0, 0.94, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            var scaleOutY = new DoubleAnimation(1.0, 0.94, TimeSpan.FromMilliseconds(220))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            fadeOut.Completed += (_, _) =>
            {
                DialogResult = true;
                Close();
            };

            BeginAnimation(OpacityProperty, fadeOut);
            ContainerScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleOutX);
            ContainerScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleOutY);
        }

        private void ShowError(string message)
        {
            TxtErrorMessage.Text = message;
            ErrorBadge.Visibility = Visibility.Visible;
        }

        private void ShakeWindow()
        {
            var anim = new DoubleAnimation
            {
                From = 0,
                To = 10,
                Duration = TimeSpan.FromMilliseconds(45),
                AutoReverse = true,
                RepeatBehavior = new RepeatBehavior(3)
            };
            WindowShakeTransform.BeginAnimation(TranslateTransform.XProperty, anim);
        }
    }
}
