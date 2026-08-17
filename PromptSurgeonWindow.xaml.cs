using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace imgsaver
{
    public partial class PromptSurgeonWindow : Window
    {
        private bool _isSyncing = false;
        private bool _isHovered = false;
        private bool _isFlashing = false;

        private static readonly System.Windows.Media.Brush HighlightBgBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(50, 46, 204, 113)); // subtle green background
        private static readonly System.Windows.Media.Brush HighlightFgBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(46, 204, 113));     // vibrant green text
        private static readonly System.Windows.Media.Brush NormalFgBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(224, 224, 224));
        private static readonly System.Windows.Media.Brush HoverYellowFgBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 215, 0));  // Gold/Yellow on hover
        private static readonly System.Windows.Media.Brush FlashGreenFgBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(39, 232, 126));   // Flash green

        public PromptSurgeonWindow()
        {
            InitializeComponent();
            this.SourceInitialized += PromptSurgeonWindow_SourceInitialized;
        }

        #region WM_GETMINMAXINFO Window Maximize Taskbar Fix

        private void PromptSurgeonWindow_SourceInitialized(object? sender, EventArgs e)
        {
            var handle = new WindowInteropHelper(this).Handle;
            if (handle != IntPtr.Zero)
            {
                var source = HwndSource.FromHwnd(handle);
                source?.AddHook(WindowProc);
            }
        }

        private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_GETMINMAXINFO = 0x0024;
            if (msg == WM_GETMINMAXINFO)
            {
                WmGetMinMaxInfo(hwnd, lParam);
                handled = true;
            }
            return IntPtr.Zero;
        }

        private static void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
        {
            MINMAXINFO mmi = (MINMAXINFO)Marshal.PtrToStructure(lParam, typeof(MINMAXINFO))!;
            const int MONITOR_DEFAULTTONEAREST = 0x00000002;
            IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);

            if (monitor != IntPtr.Zero)
            {
                MONITORINFO monitorInfo = new MONITORINFO();
                monitorInfo.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
                if (GetMonitorInfo(monitor, ref monitorInfo))
                {
                    RECT rcWorkArea = monitorInfo.rcWork;
                    RECT rcMonitorArea = monitorInfo.rcMonitor;
                    mmi.ptMaxPosition.X = Math.Abs(rcWorkArea.Left - rcMonitorArea.Left);
                    mmi.ptMaxPosition.Y = Math.Abs(rcWorkArea.Top - rcMonitorArea.Top);
                    mmi.ptMaxSize.X = Math.Abs(rcWorkArea.Right - rcWorkArea.Left);
                    mmi.ptMaxSize.Y = Math.Abs(rcWorkArea.Bottom - rcWorkArea.Top);
                }
            }

            Marshal.StructureToPtr(mmi, lParam, true);
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr handle, int flags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        #endregion

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                BtnMaximize_Click(sender, e);
            }
            else
            {
                DragMove();
            }
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = (WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized;
        }

        private void Window_StateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                var resizeThickness = SystemParameters.WindowResizeBorderThickness;
                MainBorder.Margin = new Thickness(resizeThickness.Left, resizeThickness.Top, resizeThickness.Right, resizeThickness.Bottom);
                MainBorder.CornerRadius = new CornerRadius(0);
                MainBorder.BorderThickness = new Thickness(0);
            }
            else
            {
                MainBorder.Margin = new Thickness(12);
                MainBorder.CornerRadius = new CornerRadius(12);
                MainBorder.BorderThickness = new Thickness(1);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void TxtInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isSyncing) return;

            _isSyncing = true;
            try
            {
                string inputText = TxtInput.Text;
                TxtEdit.Text = inputText;
                RenderOutput();
            }
            finally
            {
                _isSyncing = false;
            }
        }

        private void TxtEdit_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isSyncing) return;

            RenderOutput();
        }

        private void RenderOutput()
        {
            string rawInput = TxtInput.Text;
            string editedText = TxtEdit.Text;

            RtfOutput.Document.Blocks.Clear();
            var paragraph = new Paragraph { Margin = new Thickness(0) };

            if (string.IsNullOrEmpty(editedText))
            {
                paragraph.Inlines.Add(new Run(""));
                RtfOutput.Document.Blocks.Add(paragraph);
                return;
            }

            if (string.IsNullOrEmpty(rawInput))
            {
                var fgBrush = _isFlashing ? FlashGreenFgBrush : (_isHovered ? HoverYellowFgBrush : HighlightFgBrush);
                var run = new Run(editedText)
                {
                    Foreground = fgBrush,
                    Background = _isFlashing ? new SolidColorBrush(System.Windows.Media.Color.FromArgb(70, 46, 204, 113)) : HighlightBgBrush,
                    FontWeight = FontWeights.SemiBold
                };
                paragraph.Inlines.Add(run);
                RtfOutput.Document.Blocks.Add(paragraph);
                return;
            }

            var origTokens = Tokenize(rawInput);
            var editTokens = Tokenize(editedText);
            var diff = ComputeDiff(origTokens, editTokens);

            foreach (var chunk in diff)
            {
                if (chunk.IsModified)
                {
                    var fgBrush = _isFlashing ? FlashGreenFgBrush : (_isHovered ? HoverYellowFgBrush : HighlightFgBrush);
                    var run = new Run(chunk.Text)
                    {
                        Foreground = fgBrush,
                        Background = HighlightBgBrush,
                        FontWeight = FontWeights.SemiBold
                    };
                    paragraph.Inlines.Add(run);
                }
                else
                {
                    var fgBrush = _isFlashing ? FlashGreenFgBrush : (_isHovered ? HoverYellowFgBrush : NormalFgBrush);
                    var run = new Run(chunk.Text)
                    {
                        Foreground = fgBrush,
                        FontWeight = _isHovered ? FontWeights.Medium : FontWeights.Normal
                    };
                    paragraph.Inlines.Add(run);
                }
            }

            RtfOutput.Document.Blocks.Add(paragraph);
        }

        private class DiffChunk
        {
            public string Text { get; set; } = "";
            public bool IsModified { get; set; }
        }

        private static List<string> Tokenize(string text)
        {
            var tokens = new List<string>();
            var matches = Regex.Matches(text, @"(\s+|[a-zA-Z0-9_\u0600-\u06FF]+|[^\s\w])");
            foreach (Match m in matches)
            {
                tokens.Add(m.Value);
            }
            if (tokens.Count == 0 && !string.IsNullOrEmpty(text))
            {
                tokens.Add(text);
            }
            return tokens;
        }

        private static List<DiffChunk> ComputeDiff(List<string> original, List<string> edited)
        {
            int n = original.Count;
            int m = edited.Count;
            int[,] dp = new int[n + 1, m + 1];

            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < m; j++)
                {
                    if (original[i] == edited[j])
                    {
                        dp[i + 1, j + 1] = dp[i, j] + 1;
                    }
                    else
                    {
                        dp[i + 1, j + 1] = Math.Max(dp[i + 1, j], dp[i, j + 1]);
                    }
                }
            }

            var tempChunks = new List<DiffChunk>();
            int x = n;
            int y = m;

            while (x > 0 || y > 0)
            {
                if (x > 0 && y > 0 && original[x - 1] == edited[y - 1])
                {
                    tempChunks.Add(new DiffChunk { Text = edited[y - 1], IsModified = false });
                    x--;
                    y--;
                }
                else if (y > 0 && (x == 0 || dp[x, y - 1] >= dp[x - 1, y]))
                {
                    tempChunks.Add(new DiffChunk { Text = edited[y - 1], IsModified = true });
                    y--;
                }
                else if (x > 0 && (y == 0 || dp[x, y - 1] < dp[x - 1, y]))
                {
                    x--;
                }
            }

            tempChunks.Reverse();

            var chunks = new List<DiffChunk>();
            foreach (var chunk in tempChunks)
            {
                if (chunks.Count > 0 && chunks[chunks.Count - 1].IsModified == chunk.IsModified)
                {
                    chunks[chunks.Count - 1].Text += chunk.Text;
                }
                else
                {
                    chunks.Add(new DiffChunk { Text = chunk.Text, IsModified = chunk.IsModified });
                }
            }

            return chunks;
        }

        #region Hover & Click-to-Copy Feedback

        private void RtfOutput_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isFlashing) return;
            _isHovered = true;
            BorderOutput.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(160, 255, 215, 0));
            RenderOutput();
        }

        private void RtfOutput_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isFlashing) return;
            _isHovered = false;
            BorderOutput.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 47, 54));
            RenderOutput();
        }

        private async void RtfOutput_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            string textToCopy = TxtEdit.Text;
            if (string.IsNullOrEmpty(textToCopy)) return;

            try
            {
                System.Windows.Clipboard.SetText(textToCopy);
            }
            catch { }

            await FlashCopyFeedbackAsync();
        }

        private async Task FlashCopyFeedbackAsync()
        {
            _isFlashing = true;
            BorderOutput.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(46, 204, 113));
            BorderOutput.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 46, 204, 113));
            RenderOutput();

            await Task.Delay(180);

            BorderOutput.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(22, 23, 25));
            _isFlashing = false;

            if (_isHovered)
            {
                BorderOutput.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(160, 255, 215, 0));
            }
            else
            {
                BorderOutput.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(45, 47, 54));
            }

            RenderOutput();
        }

        #endregion

        #region Toolbar Buttons

        private void BtnPaste_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (System.Windows.Clipboard.ContainsText())
                {
                    TxtInput.Text = System.Windows.Clipboard.GetText();
                }
            }
            catch { }
        }

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            RtfOutput_PreviewMouseLeftButtonDown(sender, null!);
        }

        private void BtnCopyInput_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(TxtInput.Text))
                    System.Windows.Clipboard.SetText(TxtInput.Text);
            }
            catch { }
        }

        private void BtnCopyEdit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(TxtEdit.Text))
                    System.Windows.Clipboard.SetText(TxtEdit.Text);
            }
            catch { }
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            _isSyncing = true;
            try
            {
                TxtInput.Clear();
                TxtEdit.Clear();
                RtfOutput.Document.Blocks.Clear();
            }
            finally
            {
                _isSyncing = false;
            }
        }

        private void BtnResetEdit_Click(object sender, RoutedEventArgs e)
        {
            TxtEdit.Text = TxtInput.Text;
        }

        #endregion

        #region Google Translate Surgery Logic

        private static readonly System.Net.Http.HttpClient _httpClient = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        private string _targetSelectedText = "";
        private int _targetSelectionStart = -1;
        private int _targetSelectionLength = -1;

        private bool _isInsertMode = false;

        private void MenuSendToTranslate_Click(object sender, RoutedEventArgs e)
        {
            string selected = TxtEdit.SelectedText;
            if (string.IsNullOrWhiteSpace(selected))
            {
                TxtTranslateStatus.Text = "⚠️ لطفاً ابتدا بخشی از متن پرامپت را انتخاب (Highlight) کنید یا از گزینه «درج در موقعیت» استفاده نمایید.";
                TxtTranslateStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 185, 0));
                return;
            }

            _isInsertMode = false;
            _targetSelectedText = selected.Trim();
            _targetSelectionStart = TxtEdit.SelectionStart;
            _targetSelectionLength = TxtEdit.SelectionLength;

            TxtSelectedTargetPreview.Text = $"جایگزینی: \"{_targetSelectedText}\"";
            TxtSelectedTargetPreview.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 215, 0));

            TxtTranslateStatus.Text = "متن انتخابی دریافت شد. متن فارسی جایگزین را بنویسید و دکمه را بزنید.";
            TxtTranslateStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(46, 204, 113));

            TxtPersianInput.Focus();
            TxtPersianInput.SelectAll();
        }

        private void MenuInsertAtCursor_Click(object sender, RoutedEventArgs e)
        {
            _isInsertMode = true;
            _targetSelectionStart = TxtEdit.SelectionStart;
            _targetSelectionLength = 0;
            _targetSelectedText = "";

            // Show cursor position preview in chip
            string current = TxtEdit.Text;
            string posInfo = $"کاراکتر #{_targetSelectionStart}";
            if (_targetSelectionStart > 0 && _targetSelectionStart <= current.Length)
            {
                int startSnippet = Math.Max(0, _targetSelectionStart - 10);
                string snippet = current.Substring(startSnippet, _targetSelectionStart - startSnippet);
                posInfo = $"بعد از \"...{snippet}\"";
            }
            else if (_targetSelectionStart == 0)
            {
                posInfo = "ابتدای متن پرامپت";
            }

            TxtSelectedTargetPreview.Text = $"📍 درج در: {posInfo}";
            TxtSelectedTargetPreview.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 210, 211));

            TxtTranslateStatus.Text = $"موقعیت مکان‌نما ثبت شد. متن فارسی جدید را بنویسید تا ترجمه انگلیسی در همین نقطه درج شود.";
            TxtTranslateStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 210, 211));

            TxtPersianInput.Focus();
            TxtPersianInput.SelectAll();
        }

        private void BtnClearTarget_Click(object sender, RoutedEventArgs e)
        {
            _isInsertMode = false;
            _targetSelectedText = "";
            _targetSelectionStart = -1;
            _targetSelectionLength = -1;
            TxtSelectedTargetPreview.Text = "(بدون انتخاب)";
            TxtSelectedTargetPreview.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(136, 136, 136));
            TxtTranslateStatus.Text = "متنی را در کادر اصلاح بالا سلکت یا مکان‌نما را مشخص کنید";
            TxtTranslateStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(136, 136, 136));
        }

        private void TxtPersianInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                BtnTranslateAndReplace_Click(sender, e);
                e.Handled = true;
            }
        }

        private async void BtnTranslateAndReplace_Click(object sender, RoutedEventArgs e)
        {
            string persianText = TxtPersianInput.Text.Trim();
            if (string.IsNullOrWhiteSpace(persianText))
            {
                TxtTranslateStatus.Text = "⚠️ لطفاً متن فارسی را وارد کنید";
                TxtTranslateStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 185, 0));
                return;
            }

            TxtTranslateStatus.Text = "⏳ در حال ترجمه از طریق Google Translate...";
            TxtTranslateStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(55, 148, 255));
            BtnTranslateAndReplace.IsEnabled = false;

            try
            {
                string englishTranslation = await TranslatePersianToEnglishAsync(persianText);

                if (string.IsNullOrWhiteSpace(englishTranslation))
                {
                    TxtTranslateStatus.Text = "❌ خطایی در ترجمه رخ داد. لطفاً اتصال اینترنت را بررسی کنید.";
                    TxtTranslateStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(231, 76, 60));
                    return;
                }

                // Perform surgery on TxtEdit
                string currentPrompt = TxtEdit.Text;

                if (_isInsertMode && _targetSelectionStart >= 0 && _targetSelectionStart <= currentPrompt.Length)
                {
                    // Smart spacing/comma insertion at cursor position
                    string before = currentPrompt.Substring(0, _targetSelectionStart);
                    string after = currentPrompt.Substring(_targetSelectionStart);

                    string insertText = englishTranslation;
                    if (!string.IsNullOrEmpty(before) && !before.EndsWith(" ") && !before.EndsWith(","))
                    {
                        insertText = ", " + insertText;
                    }
                    if (!string.IsNullOrEmpty(after) && !after.StartsWith(" ") && !after.StartsWith(","))
                    {
                        insertText = insertText + ", ";
                    }

                    int insertedLength = insertText.Length;
                    currentPrompt = before + insertText + after;
                    TxtEdit.Text = currentPrompt;

                    // Advance cursor insertion point right after what was just inserted
                    // Keep _isInsertMode = true so subsequent typed texts continue to be added after comma!
                    _targetSelectionStart = before.Length + insertedLength;
                    _targetSelectionLength = 0;
                    _targetSelectedText = "";

                    TxtSelectedTargetPreview.Text = $"📍 درج در ادامه: \"...{englishTranslation}\"";
                    TxtSelectedTargetPreview.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 210, 211));

                    TxtTranslateStatus.Text = $"✅ درج شد: \"{englishTranslation}\" (آماده افزودن متن بعدی در ادامه آن)";
                    TxtTranslateStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 210, 211));
                }
                else if (!string.IsNullOrEmpty(_targetSelectedText) && currentPrompt.Contains(_targetSelectedText))
                {
                    // Replace the exact targeted phrase (first instance or based on position)
                    if (_targetSelectionStart >= 0 && _targetSelectionStart + _targetSelectionLength <= currentPrompt.Length 
                        && currentPrompt.Substring(_targetSelectionStart, _targetSelectionLength) == _targetSelectedText)
                    {
                        currentPrompt = currentPrompt.Remove(_targetSelectionStart, _targetSelectionLength).Insert(_targetSelectionStart, englishTranslation);
                    }
                    else
                    {
                        // Fallback: replace first occurrence
                        int index = currentPrompt.IndexOf(_targetSelectedText, StringComparison.OrdinalIgnoreCase);
                        if (index >= 0)
                        {
                            currentPrompt = currentPrompt.Remove(index, _targetSelectedText.Length).Insert(index, englishTranslation);
                        }
                    }

                    TxtEdit.Text = currentPrompt;

                    // Update the target to the newly inserted translation so subsequent translations replace it seamlessly!
                    _targetSelectedText = englishTranslation;
                    _targetSelectionLength = englishTranslation.Length;

                    TxtSelectedTargetPreview.Text = $"جایگزینی: \"{_targetSelectedText}\"";
                    TxtSelectedTargetPreview.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 215, 0));

                    TxtTranslateStatus.Text = $"✅ جایگزین شد: \"{englishTranslation}\" (آماده برای ویرایش‌های بعدی در همین بخش)";
                    TxtTranslateStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(46, 204, 113));
                }
                else if (TxtEdit.SelectionLength > 0)
                {
                    // Direct selection replacement if active
                    int selStart = TxtEdit.SelectionStart;
                    int selLen = TxtEdit.SelectionLength;
                    currentPrompt = currentPrompt.Remove(selStart, selLen).Insert(selStart, englishTranslation);
                    TxtEdit.Text = currentPrompt;

                    _targetSelectedText = englishTranslation;
                    _targetSelectionLength = englishTranslation.Length;
                    _targetSelectionStart = selStart;

                    TxtSelectedTargetPreview.Text = $"جایگزینی: \"{_targetSelectedText}\"";
                    TxtSelectedTargetPreview.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 215, 0));

                    TxtTranslateStatus.Text = $"✅ جایگزین شد: \"{englishTranslation}\"";
                    TxtTranslateStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(46, 204, 113));
                }
                else
                {
                    // Append to prompt if no target was selected
                    if (!string.IsNullOrWhiteSpace(currentPrompt) && !currentPrompt.TrimEnd().EndsWith(","))
                    {
                        currentPrompt = currentPrompt.TrimEnd() + ", " + englishTranslation;
                    }
                    else
                    {
                        currentPrompt = currentPrompt + " " + englishTranslation;
                    }

                    TxtEdit.Text = currentPrompt;

                    _targetSelectedText = englishTranslation;
                    _targetSelectionLength = englishTranslation.Length;

                    TxtSelectedTargetPreview.Text = $"جایگزینی: \"{_targetSelectedText}\"";
                    TxtSelectedTargetPreview.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 215, 0));

                    TxtTranslateStatus.Text = $"✅ به انتهای پرامپت اضافه شد: \"{englishTranslation}\"";
                    TxtTranslateStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(46, 204, 113));
                }

                // Automatically clear the Persian text box so user can immediately type the next sentence!
                TxtPersianInput.Text = "";
                TxtPersianInput.Focus();
            }
            catch (Exception ex)
            {
                TxtTranslateStatus.Text = $"❌ خطا در ترجمه: {ex.Message}";
                TxtTranslateStatus.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(231, 76, 60));
            }
            finally
            {
                BtnTranslateAndReplace.IsEnabled = true;
            }
        }

        private static async Task<string> TranslatePersianToEnglishAsync(string text)
        {
            return await TranslateGoogleAsync(text, fromLang: "auto", toLang: "en");
        }

        private static async Task<string> TranslateEnglishToPersianAsync(string text)
        {
            return await TranslateGoogleAsync(text, fromLang: "en", toLang: "fa");
        }

        private static async Task<string> TranslateGoogleAsync(string text, string fromLang, string toLang)
        {
            try
            {
                string encoded = Uri.EscapeDataString(text);
                string url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl={fromLang}&tl={toLang}&dt=t&q={encoded}";

                using var req = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, url);
                req.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

                var res = await _httpClient.SendAsync(req);
                if (!res.IsSuccessStatusCode) return "";

                string json = await res.Content.ReadAsStringAsync();

                // Format: [[["translated text","source text",null,null,1]],...]
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.ValueKind == System.Text.Json.JsonValueKind.Array && root.GetArrayLength() > 0)
                {
                    var firstArr = root[0];
                    if (firstArr.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        var sb = new System.Text.StringBuilder();
                        foreach (var item in firstArr.EnumerateArray())
                        {
                            if (item.ValueKind == System.Text.Json.JsonValueKind.Array && item.GetArrayLength() > 0)
                            {
                                sb.Append(item[0].GetString());
                            }
                        }
                        return sb.ToString().Trim();
                    }
                }
            }
            catch { }

            return "";
        }

        private async void MenuTranslateSelectionToPersian_Click(object sender, RoutedEventArgs e)
        {
            string selected = TxtEdit.SelectedText;
            if (string.IsNullOrWhiteSpace(selected))
            {
                TxtPersianMeaningDisplay.Text = "⚠️ لطفاً ابتدا کلمه یا عبارتی را در کادر اصلاح انتخاب (Highlight) کنید.";
                return;
            }

            string englishPhrase = selected.Trim();
            TxtEnSourcePreview.Text = $"\"{englishPhrase}\"";
            TxtPersianMeaningDisplay.Text = "⏳ در حال ترجمه به فارسی...";

            string persianMeaning = await TranslateEnglishToPersianAsync(englishPhrase);

            if (!string.IsNullOrWhiteSpace(persianMeaning))
            {
                TxtPersianMeaningDisplay.Text = persianMeaning;
            }
            else
            {
                TxtPersianMeaningDisplay.Text = "❌ خطایی در ترجمه رخ داد. اتصال اینترنت را بررسی کنید.";
            }
        }

        private void BtnCopyPersianTranslation_Click(object sender, RoutedEventArgs e)
        {
            string meaning = TxtPersianMeaningDisplay.Text;
            if (!string.IsNullOrWhiteSpace(meaning) && !meaning.StartsWith("⏳") && !meaning.StartsWith("⚠️") && !meaning.StartsWith("❌"))
            {
                try
                {
                    System.Windows.Clipboard.SetText(meaning);
                    BtnCopyPersianTranslation.Content = "✅ کپی شد!";
                    var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.2) };
                    timer.Tick += (s, ev) =>
                    {
                        BtnCopyPersianTranslation.Content = "📑 کپی معنی فارسی";
                        timer.Stop();
                    };
                    timer.Start();
                }
                catch { }
            }
        }

        #endregion
    }
}
