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
                if (combinerData == null || !combinerData.IsEnabled) return;

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

                    if (!isHostActive) return;
                }

                string rawText = SafeClipboardGetText();
                if (string.IsNullOrWhiteSpace(rawText)) return;

                // Ignore if marked with zero-width space (already combined by any component)
                if (rawText.EndsWith("\u200B")) return;

                // Ignore repeat processing
                if (rawText == _lastCombinedText) return;

                string text = rawText.Trim();
                if (string.IsNullOrWhiteSpace(text)) return;

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
                    catch { }
                }

                // 2. Snippet Combining
                var activeItems = combinerData.Items
                    .Where(i => combinerData.ActiveItemIds != null && combinerData.ActiveItemIds.Contains(i.Id))
                    .Select(i => i.Text)
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .ToList();

                var customTexts = combinerData.Folders
                    .Where(f => f.IsCustomInput && f.IsCustomInputActive && !string.IsNullOrWhiteSpace(f.CustomInputText))
                    .Select(f => f.CustomInputText.Trim())
                    .ToList();

                if (activeItems.Count == 0 && customTexts.Count == 0) return;

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
                    _lastCombinedText = combined;

                    SafeClipboardSetText(combined + "\u200B");

                    CursorBadgeNotification.ShowCombiner("⚡ Combined!");

                    try
                    {
                        foreach (Window win in System.Windows.Application.Current.Windows)
                        {
                            if (win is BrowserWindow bw)
                            {
                                bw.FlashCombinerSuccess();
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }
            finally
            {
                _isProcessing = false;
            }
        }

        private static string SafeClipboardGetText()
        {
            for (int i = 0; i < 6; i++)
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
                    System.Threading.Thread.Sleep(25);
                }
            }
            return string.Empty;
        }

        private static void SafeClipboardSetText(string text)
        {
            for (int i = 0; i < 6; i++)
            {
                try
                {
                    System.Windows.Clipboard.SetText(text);
                    return;
                }
                catch
                {
                    System.Threading.Thread.Sleep(25);
                }
            }
        }
    }
}
