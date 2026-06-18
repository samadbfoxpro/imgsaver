using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Interop;

namespace imgsaver
{
    public partial class BrowserWindow
    {
        public bool IsCurrentWebViewTarget()
        {
            if (!IsVisible || WindowState == WindowState.Minimized) return false;

            var helper = new WindowInteropHelper(this);
            if (helper.Handle == IntPtr.Zero || GetForegroundWindow() != helper.Handle) return false;

            var browser = GetCurrentBrowser();
            return browser != null && TxtUrl?.IsKeyboardFocusWithin != true;
        }

        public async Task PlayBrowserRecordingAsync()
        {
            if (_browserInputRecorder.IsRecording)
            {
                StopBrowserRecordingAndSave();
            }

            RecordingManager.LoadState();
            int slotToPlay = RecordingManager.SelectedSlot;
            if (!RecordingManager.HasEvents(slotToPlay))
            {
                int other = (slotToPlay == 1) ? 2 : 1;
                if (RecordingManager.HasEvents(other)) slotToPlay = other;
                else return;
            }

            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }

            Activate();
            Focus();
            GetCurrentBrowser()?.Focus();

            _browserRecordingPlayer.SetEvents(RecordingManager.GetEvents(slotToPlay));
            _browserRecordingPlayer.SetSpeed(RecordingManager.PlaybackSpeed);
            await _browserRecordingPlayer.PlayAsync(false);
        }

        private void BtnBrowserRecord_Click(object? sender, RoutedEventArgs e)
        {
            if (_browserInputRecorder.IsRecording)
            {
                StopBrowserRecordingAndSave();
                return;
            }

            Activate();
            Focus();
            GetCurrentBrowser()?.Focus();

            _browserInputRecorder.Start();
            SetBrowserRecordButtonState(true);
        }

        private async void BtnBrowserPlayRec_Click(object? sender, RoutedEventArgs e)
        {
            BtnBrowserPlayRec.IsEnabled = false;
            try
            {
                await PlayBrowserRecordingAsync();
            }
            finally
            {
                BtnBrowserPlayRec.IsEnabled = true;
            }
        }

        private void StopBrowserRecordingAndSave()
        {
            if (!_browserInputRecorder.IsRecording) return;

            _browserInputRecorder.Stop();
            RecordingManager.LoadState();
            RecordingManager.SetEvents(RecordingManager.SelectedSlot, _browserInputRecorder.GetEvents());
            SetBrowserRecordButtonState(false);
        }

        private void SetBrowserRecordButtonState(bool isRecording)
        {
            if (BtnBrowserRecord != null)
            {
                BtnBrowserRecord.ToolTip = isRecording ? "Stop and save browser recording" : "Record browser mouse and keyboard";
            }

            if (TxtBrowserRecordIcon != null)
            {
                TxtBrowserRecordIcon.Text = isRecording ? "■" : "●";
                TxtBrowserRecordIcon.Foreground = new SolidColorBrush(
                    isRecording
                        ? (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#FFD400")
                        : (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#F14C4C"));
            }
        }
    }
}
