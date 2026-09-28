using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace imgsaver
{
    public class LogEntry
    {
        public int Id { get; set; }
        public DateTime Timestamp { get; set; }
        public string TimeString => Timestamp.ToString("HH:mm:ss.fff");
        public string Category { get; set; } = "System"; // MiniClip, Browser, Combiner, Bridge, System
        public string Level { get; set; } = "INFO"; // INFO, SUCCESS, WARN, ERROR
        public string Action { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Payload { get; set; }

        public string LevelBadge => Level switch
        {
            "SUCCESS" => "✓",
            "WARN" => "⚠",
            "ERROR" => "✕",
            _ => "ℹ"
        };
    }

    public static class AppLogManager
    {
        private static readonly object _syncRoot = new object();
        private static readonly List<LogEntry> _entries = new List<LogEntry>();
        private static int _nextId = 1;
        private static bool _isRecording = false;

        public static bool IsRecording
        {
            get => _isRecording;
            set
            {
                if (_isRecording != value)
                {
                    _isRecording = value;
                    RecordingStateChanged?.Invoke(_isRecording);
                    
                    if (_isRecording)
                    {
                        LogInternal("System", "ضبط لاگ فعال شد", "از این لحظه کلیه رویدادهای مینی‌کلیپ‌برد، مرورگر و کمباینر ضبط می‌شوند.", "SUCCESS");
                    }
                    else
                    {
                        LogInternal("System", "ضبط لاگ متوقف شد", "ضبط رویدادها متوقف گردید.", "WARN");
                    }
                }
            }
        }

        public static event Action<LogEntry>? LogAdded;
        public static event Action? LogsCleared;
        public static event Action<bool>? RecordingStateChanged;

        public static void StartRecording()
        {
            IsRecording = true;
        }

        public static void StopRecording()
        {
            IsRecording = false;
        }

        public static void Clear()
        {
            lock (_syncRoot)
            {
                _entries.Clear();
                _nextId = 1;
            }
            LogsCleared?.Invoke();
        }

        public static List<LogEntry> GetEntries()
        {
            lock (_syncRoot)
            {
                return _entries.ToList();
            }
        }

        public static int Count
        {
            get
            {
                lock (_syncRoot)
                {
                    return _entries.Count;
                }
            }
        }

        /// <summary>
        /// Logs an event if recording is currently enabled.
        /// </summary>
        public static void Log(string category, string action, string message, string level = "INFO", string? payload = null)
        {
            if (!_isRecording && level != "ERROR") return;
            LogInternal(category, action, message, level, payload);
        }

        /// <summary>
        /// Always logs regardless of recording status (e.g. for start/stop system announcements).
        /// </summary>
        private static void LogInternal(string category, string action, string message, string level = "INFO", string? payload = null)
        {
            LogEntry entry;
            lock (_syncRoot)
            {
                entry = new LogEntry
                {
                    Id = _nextId++,
                    Timestamp = DateTime.Now,
                    Category = category,
                    Action = action,
                    Message = message,
                    Level = level,
                    Payload = payload
                };

                // Limit in-memory buffer to last 3,000 entries
                if (_entries.Count >= 3000)
                {
                    _entries.RemoveAt(0);
                }
                _entries.Add(entry);
            }

            try
            {
                LogAdded?.Invoke(entry);
            }
            catch { }
        }

        /// <summary>
        /// Formats all recorded logs into a structured readable text file.
        /// </summary>
        public static string ExportToText()
        {
            var sb = new StringBuilder();
            sb.AppendLine("================================================================================");
            sb.AppendLine("                       IMGSAVER SYSTEM & CLIPBOARD LOG REPORT                   ");
            sb.AppendLine("================================================================================");
            sb.AppendLine($"تاریخ تولید گزارش: {DateTime.Now:yyyy/MM/dd HH:mm:ss}");
            sb.AppendLine($"وضعیت ضبط هنگام خروجی: {(IsRecording ? "فعال (Recording)" : "متوقف (Stopped)")}");
            sb.AppendLine($"تعداد رویدادها: {Count}");
            sb.AppendLine($"سیستم‌عامل: {Environment.OSVersion}");
            sb.AppendLine($"دات‌نت: {Environment.Version}");
            sb.AppendLine("================================================================================");
            sb.AppendLine();

            List<LogEntry> list;
            lock (_syncRoot)
            {
                list = _entries.ToList();
            }

            if (list.Count == 0)
            {
                sb.AppendLine("هیچ رویدادی در این جلسه ضبط نشده است.");
                return sb.ToString();
            }

            foreach (var e in list)
            {
                sb.AppendLine($"[{e.TimeString}] [{e.Level,-7}] [{e.Category,-9}] {e.Action}");
                if (!string.IsNullOrWhiteSpace(e.Message))
                {
                    sb.AppendLine($"    پیام: {e.Message}");
                }
                if (!string.IsNullOrWhiteSpace(e.Payload))
                {
                    sb.AppendLine("    محتوا:");
                    string indentedPayload = string.Join(Environment.NewLine, e.Payload.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).Select(line => "        " + line));
                    sb.AppendLine(indentedPayload);
                }
                sb.AppendLine(new string('-', 80));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Saves log report directly to disk.
        /// </summary>
        public static string SaveToFile(string? customPath = null)
        {
            string exportPath = customPath ?? string.Empty;
            if (string.IsNullOrWhiteSpace(exportPath))
            {
                string logsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                if (!Directory.Exists(logsDir))
                {
                    Directory.CreateDirectory(logsDir);
                }
                string fileName = $"imgsaver_log_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                exportPath = Path.Combine(logsDir, fileName);
            }

            string content = ExportToText();
            File.WriteAllText(exportPath, content, Encoding.UTF8);
            return exportPath;
        }
    }
}
