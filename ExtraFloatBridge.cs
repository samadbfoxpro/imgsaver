using System;

namespace imgsaver
{
    /// <summary>
    /// Lightweight pub/sub bridge used by the Extra Float window to automatically push
    /// the latest confirmed Extra title into any open Mini Clip window(s), without
    /// touching the clipboard or requiring any extra action from the user.
    /// </summary>
    public static class ExtraFloatBridge
    {
        public static string LastConfirmedTitle { get; private set; } = "";

        public static event Action<string>? ExtraTitleConfirmed;

        public static void NotifyConfirmed(string title)
        {
            LastConfirmedTitle = title ?? "";
            ExtraTitleConfirmed?.Invoke(LastConfirmedTitle);
        }
    }
}
