using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Interop;

namespace imgsaver
{
    /// <summary>
    /// Global application-level Clipboard listener for Smart Prompt Combiner.
    /// Operates completely decoupled from MiniClipboardWindow or BrowserWindow UI states.
    /// Runs continuously as long as the application is running whenever Combiner is enabled.
    /// </summary>
    public static class GlobalClipboardCombiner
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        private const int WM_CLIPBOARDUPDATE = 0x031D;

        private static HwndSource? _hwndSource;
        private static IntPtr _windowHandle = IntPtr.Zero;
        private static bool _isProcessing = false;
        private static string _lastCombinedText = "";

        public static void Start(Window window)
        {
            if (window == null) return;
            try
            {
                var helper = new WindowInteropHelper(window);
                _windowHandle = helper.EnsureHandle();
                if (_windowHandle != IntPtr.Zero && _hwndSource == null)
                {
                    _hwndSource = HwndSource.FromHwnd(_windowHandle);
                    _hwndSource?.AddHook(WndProc);
                    AddClipboardFormatListener(_windowHandle);
                }
            }
            catch { }
        }

        public static void Stop()
        {
            try
            {
                if (_windowHandle != IntPtr.Zero)
                {
                    RemoveClipboardFormatListener(_windowHandle);
                    _hwndSource?.RemoveHook(WndProc);
                    _hwndSource = null;
                    _windowHandle = IntPtr.Zero;
                }
            }
            catch { }
        }

        private static IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_CLIPBOARDUPDATE)
            {
                OnClipboardUpdate();
            }
            return IntPtr.Zero;
        }

        private static void OnClipboardUpdate()
        {
            if (_isProcessing) return;

            try
            {
                var combinerData = PromptCombinerStore.Load();
                if (combinerData == null || !combinerData.IsEnabled)
                {
                    if (AppLogManager.IsRecording)
                    {
                        AppLogManager.Log("Combiner", "رویداد کلیپ‌بورد گلوبال", "کمباینر هوشمند در تنظیمات غیرفعال است.");
                    }
                    return;
                }

                // Check host availability: Combine works if IsStandaloneGlobalEnabled is true, OR MiniClipboardWindow is open, OR BrowserWindow is open!
                if (!combinerData.IsStandaloneGlobalEnabled)
                {
                    bool isHostActive = false;
                    try
                    {
                        foreach (Window win in System.Windows.Application.Current.Windows)
                        {
                            if ((win is MiniClipboardWindow mc && mc.IsLoaded) || (win is BrowserWindow bw && bw.IsLoaded))
                            {
                                isHostActive = true;
                                break;
                            }
                        }
                    }
                    catch { }

                    if (!isHostActive)
                    {
                        if (AppLogManager.IsRecording)
                        {
                            AppLogManager.Log("Combiner", "عدم حضور پنجره فعال", "هیچ‌کدام از پنجره‌های MiniClipboard یا BrowserWindow باز نیستند و حالت Standalone نیز غیرفعال است.");
                        }
                        return;
                    }
                }

                string rawText = SafeClipboardGetText();
                if (string.IsNullOrWhiteSpace(rawText)) return;

                // Ignore if marked with zero-width space (already combined by any component)
                if (rawText.EndsWith("\u200B") || rawText.Contains("\u200B"))
                {
                    AppLogManager.Log("Combiner", "تشخیص نشانگر ZWSP", "متن قبلاً ترکیب شده است (\u200B). از ترکیب مجدد جلوگیری شد.", "INFO", rawText);
                    return;
                }

                // Ignore Persian / Arabic text (it is meant as a title for MiniClipboard, NOT a prompt to combine!)
                if (PromptCombinerEngine.IsPersianText(rawText))
                {
                    AppLogManager.Log("Combiner", "تشخیص متن فارسی/عربی", "متن فارسی به عنوان عنوان برای مینی‌کلیپ‌برد استفاده می‌شود نه پرامپت.", "INFO", rawText);
                    return;
                }

                string text = rawText.Trim();
                if (string.IsNullOrWhiteSpace(text)) return;

                AppLogManager.Log("Combiner", "دریافت متن در کمباینر", $"طول متن: {text.Length} کاراکتر", "INFO", text);

                // 1. Auto Base Prompt Capture (Runs whenever AutoCaptureBasePrompt is enabled!)
                if (combinerData.AutoCaptureBasePrompt)
                {
                    try
                    {
                        string configPath = DataPathManager.GetSettingsFilePath("base_combiner_config.json");
                        string dir = System.IO.Path.GetDirectoryName(configPath);
                        if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
                            System.IO.Directory.CreateDirectory(dir);
                        System.IO.File.WriteAllText(configPath, text);

                        AppLogManager.Log("Combiner", "ضبط خودکار Base Prompt", "پرامپت پایه به‌روزرسانی شد.", "SUCCESS", text);

                        // Notify open windows to refresh Base Prompt editor UI
                        System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            foreach (Window win in System.Windows.Application.Current.Windows)
                            {
                                if (win is BrowserWindow bw)
                                {
                                    bw.RefreshInlineBasePromptUI();
                                }
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        AppLogManager.Log("Combiner", "خطا در ضبط Base Prompt", ex.Message, "ERROR");
                    }
                }

                // 2. Snippet Combining
                var activeItems = (combinerData.Items ?? new List<PromptCombinerItem>())
                    .Where(i => combinerData.ActiveItemIds != null && combinerData.ActiveItemIds.Contains(i.Id))
                    .Select(i => i.Text)
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .ToList();

                var customTexts = (combinerData.Folders ?? new List<PromptCombinerFolder>())
                    .Where(f => f.IsCustomInput && !string.IsNullOrWhiteSpace(f.CustomInputText))
                    .Select(f => f.CustomInputText.Trim())
                    .ToList();

                if (activeItems.Count == 0 && customTexts.Count == 0)
                {
                    AppLogManager.Log("Combiner", "اسنیپت فعالی انتخاب نشده", "هیچ اسنیپت یا متن سفارشی فعالی برای ترکیب وجود ندارد.", "WARN");
                    return;
                }

                string combined;
                if (combinerData.PlacementMode == CombinerPlacementMode.PerFolder)
                {
                    combined = PromptCombinerEngine.CombinePerFolder(text, combinerData);
                }
                else
                {
                    var allSnippetTexts = new List<string>(activeItems);
                    allSnippetTexts.AddRange(customTexts);
                    combined = PromptCombinerEngine.Combine(text, allSnippetTexts, combinerData.PlacementMode, combinerData.CommaIndex, combinerData.Separator);
                }

                if (!string.IsNullOrWhiteSpace(combined) && combined != text)
                {
                    _isProcessing = true;
                    SafeClipboardSetText(combined + "\u200B");
                    CursorBadgeNotification.ShowCombiner("⚡ Combined!");

                    AppLogManager.Log("Combiner", "ترکیب پرامپت با موفقیت انجام شد", 
                        $"متن پرامپت با {activeItems.Count} اسنیپت و {customTexts.Count} ورودی پوشه ترکیب شد.", "SUCCESS",
                        $"متن ورودی:\n{text}\n\nمتن نهایی روی کلیپ‌بورد:\n{combined}");

                    try
                    {
                        foreach (Window win in System.Windows.Application.Current.Windows)
                        {
                            if (win is BrowserWindow bw)
                            {
                                bw.FlashCombinerSuccess();
                                AppLogManager.Log("Bridge", "اطلاع‌رسانی به مرورگر", "فلش موفقیت‌آمیز در پنجره مرورگر اجرا شد.");
                            }
                            else if (win is MiniClipboardWindow mc)
                            {
                                mc.ApplyCombinerTitles(combinerData);
                                AppLogManager.Log("Bridge", "اعمال عناوین کمباینر به MiniClip", "عناوین اسنیپت‌ها در مینی‌کلیپ‌برد اعمال شدند.");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        AppLogManager.Log("Bridge", "خطا در اطلاع‌رسانی بین پنجره‌ها", ex.Message, "ERROR");
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogManager.Log("Combiner", "خطا در پردازش کمباینر", ex.Message, "ERROR");
            }
            finally
            {
                _isProcessing = false;
            }
        }

        private static string SafeClipboardGetText()
        {
            for (int i = 0; i < 8; i++)
            {
                try
                {
                    if (System.Windows.Clipboard.ContainsText())
                    {
                        return System.Windows.Clipboard.GetText();
                    }
                    return string.Empty;
                }
                catch
                {
                    System.Threading.Thread.Sleep(20);
                }
            }
            return string.Empty;
        }

        private static void SafeClipboardSetText(string text)
        {
            for (int i = 0; i < 8; i++)
            {
                try
                {
                    System.Windows.Clipboard.SetText(text);
                    _lastCombinedText = text;
                    return;
                }
                catch
                {
                    System.Threading.Thread.Sleep(20);
                }
            }
        }
    }
}
