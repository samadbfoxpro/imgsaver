using System;
using System.Diagnostics;
using System.IO;

namespace imgsaver
{
    public static class StartupProfiler
    {
        private static readonly Stopwatch _sw = Stopwatch.StartNew();
        private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_perf.log");

        static StartupProfiler()
        {
            try
            {
                File.WriteAllText(LogPath, $"=== STARTUP PROFILER LOG [{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] ===\n");
            }
            catch { }
        }

        public static void Log(string stepName)
        {
            try
            {
                long ms = _sw.ElapsedMilliseconds;
                string line = $"[{ms,5} ms] {stepName}\n";
                File.AppendAllText(LogPath, line);
                Debug.WriteLine(line);
            }
            catch { }
        }
    }
}
