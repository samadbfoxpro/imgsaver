using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json.Linq;

namespace imgsaver
{
    /// <summary>
    /// Thread-safe, non-blocking on-demand deep diagnostic logger for the internal browser (BrowserWindow).
    /// Records navigation lifecycle, frames, network errors, blocked requests, JS console/exceptions (via CDP),
    /// and injected scripts into Browser_Debug_Log.txt without degrading browser performance when disabled.
    /// </summary>
    public static class BrowserDiagnosticLogger
    {
        private static readonly object _syncLock = new object();
        private static readonly HashSet<CoreWebView2> _registeredWebViews = new HashSet<CoreWebView2>();
        private static readonly HashSet<CoreWebView2> _cdpHookedWebViews = new HashSet<CoreWebView2>();

        private static readonly Channel<string> _logChannel = Channel.CreateUnbounded<string>(new UnboundedChannelOptions
        {
            SingleReader = true
        });

        public static bool IsEnabled { get; private set; } = false;

        public static string LogFilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Browser_Debug_Log.txt");

        public static event Action<bool>? StateChanged;

        static BrowserDiagnosticLogger()
        {
            // Start background non-blocking file writer
            Task.Run(ProcessLogQueueAsync);
        }

        private static async Task ProcessLogQueueAsync()
        {
            var reader = _logChannel.Reader;
            while (await reader.WaitToReadAsync())
            {
                try
                {
                    using var stream = new FileStream(LogFilePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);
                    using var writer = new StreamWriter(stream, Encoding.UTF8);

                    while (reader.TryRead(out var line))
                    {
                        await writer.WriteLineAsync(line);
                    }
                    await writer.FlushAsync();
                }
                catch
                {
                    // Ignore write collisions or temporary file locks
                }
            }
        }

        public static void SetLogging(bool enable, BrowserWindow? window = null, string? envInfo = null)
        {
            lock (_syncLock)
            {
                if (IsEnabled == enable) return;
                IsEnabled = enable;
            }

            if (enable)
            {
                WriteEnvironmentHeader(window, envInfo);
                _ = EnableCdpOnAllWebViewsAsync();
            }
            else
            {
                WriteEnvironmentFooter();
                _ = DisableCdpOnAllWebViewsAsync();
            }

            try
            {
                StateChanged?.Invoke(enable);
            }
            catch { }
        }

        private static void WriteEnvironmentHeader(BrowserWindow? window, string? extraEnv)
        {
            string webViewVersion = "Unknown";
            try
            {
                webViewVersion = CoreWebView2Environment.GetAvailableBrowserVersionString();
            }
            catch { }

            var sb = new StringBuilder();
            sb.AppendLine("================================================================================");
            sb.AppendLine("IMG SAVER - BROWSER DEEP DIAGNOSTIC LOG (ON-DEMAND TRACE)");
            sb.AppendLine($"Session Started  : {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
            sb.AppendLine($"WebView2 Runtime : {webViewVersion}");
            sb.AppendLine($"OS Version       : {Environment.OSVersion} (64-bit OS: {Environment.Is64BitOperatingSystem}, 64-bit Process: {Environment.Is64BitProcess})");
            sb.AppendLine($"App Directory    : {AppDomain.CurrentDomain.BaseDirectory}");

            if (window != null)
            {
                try
                {
                    var s = window.CurrentSettings;
                    if (s != null)
                    {
                        sb.AppendLine($"Proxy Config     : Mode={s.ProxyMode}, Type={s.ProxyType}, Server={s.ProxyAddress}:{s.ProxyPort}, ProxyBridgePort={BrowserWindow.ProxyBridge.Port}");
                        sb.AppendLine($"Browser Settings : LoadImages={s.LoadImages}, LoadMedia={s.LoadMedia}, JS={s.EnableJavaScript}, DisableCache={s.DisableBrowserCache}, CacheMediaOnly={s.CacheMediaOnly}");
                    }
                    var curBrowser = window.GetCurrentBrowser();
                    if (curBrowser?.CoreWebView2 != null)
                    {
                        sb.AppendLine($"Active Tab URL   : {curBrowser.CoreWebView2.Source}");
                    }
                }
                catch { }
            }

            if (!string.IsNullOrWhiteSpace(extraEnv))
            {
                sb.AppendLine($"Extra Info       : {extraEnv}");
            }

            sb.AppendLine("Log Format       : [TIME] [CATEGORY] [LEVEL] Message | Details");
            sb.AppendLine("================================================================================");

            _logChannel.Writer.TryWrite(sb.ToString().TrimEnd());
        }

        private static void WriteEnvironmentFooter()
        {
            var sb = new StringBuilder();
            sb.AppendLine("================================================================================");
            sb.AppendLine($"Diagnostic Recording Stopped at: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
            sb.AppendLine("================================================================================");
            _logChannel.Writer.TryWrite(sb.ToString().TrimEnd());
        }

        /// <summary>
        /// Writes a categorized diagnostic message to the log buffer.
        /// Fast early-return if IsEnabled is false (zero overhead).
        /// </summary>
        public static void Log(string category, string level, string message, string? details = null)
        {
            if (!IsEnabled) return;

            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            string entry = $"[{timestamp}] [{category.ToUpperInvariant()}] [{level.ToUpperInvariant()}] {message}";
            if (!string.IsNullOrWhiteSpace(details))
            {
                entry += $" | {details}";
            }

            _logChannel.Writer.TryWrite(entry);
        }

        public static void ClearLog()
        {
            try
            {
                File.WriteAllText(LogFilePath, string.Empty);
            }
            catch { }

            if (IsEnabled)
            {
                WriteEnvironmentHeader(null, "Log file manually cleared while recording");
            }
        }

        public static void OpenLogFile()
        {
            try
            {
                if (!File.Exists(LogFilePath))
                {
                    File.WriteAllText(LogFilePath, $"IMG SAVER - Browser Diagnostic Log\nCreated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\nStatus: Idle\n");
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = LogFilePath,
                    UseShellExecute = true
                });
            }
            catch { }
        }

        /// <summary>
        /// Registers a CoreWebView2 instance to monitor lifecycle and DevTools protocol events.
        /// </summary>
        public static void RegisterCoreWebView2(CoreWebView2? core, Func<string>? titleGetter = null)
        {
            if (core == null) return;

            lock (_syncLock)
            {
                if (_registeredWebViews.Contains(core)) return;
                _registeredWebViews.Add(core);
            }

            string GetTabContext() => titleGetter?.Invoke() ?? "Tab";

            // 1. Navigation & Frame Lifecycle
            core.NavigationStarting += (s, e) =>
            {
                if (!IsEnabled) return;
                Log("NAV", "START", $"ID={e.NavigationId} [UserInit={e.IsUserInitiated}, Redir={e.IsRedirected}] -> {e.Uri}", $"Tab={GetTabContext()}");
            };

            core.SourceChanged += (s, e) =>
            {
                if (!IsEnabled) return;
                Log("NAV", "SOURCE_CHANGE", $"IsNewDoc={e.IsNewDocument} -> {core.Source}", $"Tab={GetTabContext()}");
            };

            core.ContentLoading += (s, e) =>
            {
                if (!IsEnabled) return;
                Log("NAV", "CONTENT_LOADING", $"ID={e.NavigationId} IsErrorPage={e.IsErrorPage}", $"Tab={GetTabContext()}");
            };

            core.HistoryChanged += (s, e) =>
            {
                if (!IsEnabled) return;
                Log("NAV", "HISTORY_CHANGED", $"CanGoBack={core.CanGoBack}, CanGoForward={core.CanGoForward}", $"Tab={GetTabContext()}");
            };

            core.NavigationCompleted += (s, e) =>
            {
                if (!IsEnabled) return;
                string level = e.IsSuccess ? "INFO" : "ERROR";
                Log("NAV", level, $"ID={e.NavigationId} Success={e.IsSuccess} HttpStatus={e.HttpStatusCode} WebError={e.WebErrorStatus}", $"Tab={GetTabContext()} | URL={core.Source}");
            };

            core.FrameNavigationStarting += (s, e) =>
            {
                if (!IsEnabled) return;
                Log("FRAME", "START", $"ID={e.NavigationId} UserInit={e.IsUserInitiated} -> {e.Uri}", $"Tab={GetTabContext()}");
            };

            core.FrameNavigationCompleted += (s, e) =>
            {
                if (!IsEnabled) return;
                string level = e.IsSuccess ? "INFO" : "WARN";
                Log("FRAME", level, $"ID={e.NavigationId} Success={e.IsSuccess} HttpStatus={e.HttpStatusCode} WebError={e.WebErrorStatus}", $"Tab={GetTabContext()}");
            };

            core.FrameCreated += (s, e) =>
            {
                if (!IsEnabled) return;
                Log("FRAME", "CREATED", $"Name='{e.Frame.Name}'", $"Tab={GetTabContext()}");
            };

            // 2. Browser Process & Permissions
            core.PermissionRequested += (s, e) =>
            {
                if (!IsEnabled) return;
                Log("SECURITY", "PERMISSION", $"Kind={e.PermissionKind} State={e.State} UserInit={e.IsUserInitiated} -> {e.Uri}", $"Tab={GetTabContext()}");
            };

            core.NewWindowRequested += (s, e) =>
            {
                if (!IsEnabled) return;
                Log("LIFECYCLE", "NEW_WINDOW", $"UserInit={e.IsUserInitiated} Handled={e.Handled} -> {e.Uri}", $"Tab={GetTabContext()}");
            };

            core.ScriptDialogOpening += (s, e) =>
            {
                if (!IsEnabled) return;
                Log("LIFECYCLE", "DIALOG", $"Kind={e.Kind} Message='{e.Message}' DefaultText='{e.DefaultText}' -> {e.Uri}", $"Tab={GetTabContext()}");
            };

            core.ProcessFailed += (s, e) =>
            {
                if (!IsEnabled) return;
                Log("LIFECYCLE", "CRASH_FAIL", $"ProcessFailedKind={e.ProcessFailedKind} ExitCode={e.ExitCode} Description='{e.ProcessDescription}'", $"Tab={GetTabContext()} | URL={core.Source}");
            };

            // 3. DevTools Protocol Listeners (CDP)
            HookCdpListeners(core, GetTabContext);

            if (IsEnabled)
            {
                _ = EnableCdpForCoreAsync(core);
            }
        }

        private static void HookCdpListeners(CoreWebView2 core, Func<string> tabContext)
        {
            lock (_syncLock)
            {
                if (_cdpHookedWebViews.Contains(core)) return;
                _cdpHookedWebViews.Add(core);
            }

            try
            {
                // Runtime.exceptionThrown (JavaScript unhandled exceptions)
                var excReceiver = core.GetDevToolsProtocolEventReceiver("Runtime.exceptionThrown");
                excReceiver.DevToolsProtocolEventReceived += (s, e) =>
                {
                    if (!IsEnabled) return;
                    try
                    {
                        var json = JObject.Parse(e.ParameterObjectAsJson);
                        var details = json["exceptionDetails"];
                        if (details != null)
                        {
                            string text = details["text"]?.ToString() ?? "Exception";
                            string expDesc = details["exception"]?["description"]?.ToString() ?? text;
                            string url = details["url"]?.ToString() ?? "inline";
                            int line = details["lineNumber"]?.Value<int>() ?? 0;
                            int col = details["columnNumber"]?.Value<int>() ?? 0;
                            Log("JS_ERR", "EXCEPTION", $"{expDesc}", $"File={url}:{line}:{col} | Tab={tabContext()}");
                        }
                    }
                    catch { }
                };

                // Log.entryAdded (Console messages, network warnings, security issues)
                var logReceiver = core.GetDevToolsProtocolEventReceiver("Log.entryAdded");
                logReceiver.DevToolsProtocolEventReceived += (s, e) =>
                {
                    if (!IsEnabled) return;
                    try
                    {
                        var json = JObject.Parse(e.ParameterObjectAsJson);
                        var entry = json["entry"];
                        if (entry != null)
                        {
                            string level = entry["level"]?.ToString() ?? "info";
                            string source = entry["source"]?.ToString() ?? "other";
                            string text = entry["text"]?.ToString() ?? "";
                            string url = entry["url"]?.ToString() ?? "";
                            int line = entry["lineNumber"]?.Value<int>() ?? 0;

                            string cat = (source == "security" || source == "csp") ? "SECURITY" :
                                         (source == "javascript") ? "CONSOLE" : "NET";

                            Log(cat, level, text, $"Source={source} | File={url}:{line} | Tab={tabContext()}");
                        }
                    }
                    catch { }
                };

                // Network.loadingFailed (CORS, BlockedByClient, connection timeouts)
                var netFailReceiver = core.GetDevToolsProtocolEventReceiver("Network.loadingFailed");
                netFailReceiver.DevToolsProtocolEventReceived += (s, e) =>
                {
                    if (!IsEnabled) return;
                    try
                    {
                        var json = JObject.Parse(e.ParameterObjectAsJson);
                        string err = json["errorText"]?.ToString() ?? "Failed";
                        string type = json["type"]?.ToString() ?? "Resource";
                        string blockedReason = json["blockedReason"]?.ToString() ?? "";
                        bool canceled = json["canceled"]?.Value<bool>() ?? false;

                        string detail = $"Type={type}, Canceled={canceled}";
                        if (!string.IsNullOrEmpty(blockedReason)) detail += $", BlockedReason={blockedReason}";
                        detail += $" | Tab={tabContext()}";

                        Log("NET", "FAILED", $"Request Failed: {err}", detail);
                    }
                    catch { }
                };
            }
            catch { }
        }

        private static async Task EnableCdpOnAllWebViewsAsync()
        {
            List<CoreWebView2> webViews;
            lock (_syncLock)
            {
                webViews = _registeredWebViews.ToList();
            }

            foreach (var core in webViews)
            {
                await EnableCdpForCoreAsync(core);
            }
        }

        private static async Task DisableCdpOnAllWebViewsAsync()
        {
            List<CoreWebView2> webViews;
            lock (_syncLock)
            {
                webViews = _registeredWebViews.ToList();
            }

            foreach (var core in webViews)
            {
                await DisableCdpForCoreAsync(core);
            }
        }

        private static async Task EnableCdpForCoreAsync(CoreWebView2 core)
        {
            try
            {
                await core.CallDevToolsProtocolMethodAsync("Runtime.enable", "{}");
                await core.CallDevToolsProtocolMethodAsync("Log.enable", "{}");
                await core.CallDevToolsProtocolMethodAsync("Network.enable", "{}");
            }
            catch { }
        }

        private static async Task DisableCdpForCoreAsync(CoreWebView2 core)
        {
            try
            {
                await core.CallDevToolsProtocolMethodAsync("Network.disable", "{}");
                await core.CallDevToolsProtocolMethodAsync("Log.disable", "{}");
                await core.CallDevToolsProtocolMethodAsync("Runtime.disable", "{}");
            }
            catch { }
        }

        public static void UnregisterCoreWebView2(CoreWebView2? core)
        {
            if (core == null) return;
            lock (_syncLock)
            {
                _registeredWebViews.Remove(core);
                _cdpHookedWebViews.Remove(core);
            }
        }

        /// <summary>
        /// Logs script execution and its result cleanly for diagnostic analysis.
        /// </summary>
        public static async Task<string> ExecuteScriptLoggedAsync(this CoreWebView2 core, string caller, string script)
        {
            if (!IsEnabled)
            {
                return await core.ExecuteScriptAsync(script);
            }

            string preview = script.Length > 120 ? script.Substring(0, 120).Replace("\r", " ").Replace("\n", " ") + "..." : script.Replace("\r", " ").Replace("\n", " ");
            try
            {
                string result = await core.ExecuteScriptAsync(script);
                bool hasError = !string.IsNullOrEmpty(result) && (result.Contains("\"Error") || result.Contains("\"error\"") || result.Contains("null"));
                Log("INJECT", hasError ? "WARN" : "INFO", $"ExecuteScript [{caller}]", $"Script={preview} | Result={result}");
                return result;
            }
            catch (Exception ex)
            {
                Log("INJECT", "ERROR", $"ExecuteScript FAILED [{caller}]", $"Script={preview} | Error={ex.Message}");
                throw;
            }
        }
    }
}
