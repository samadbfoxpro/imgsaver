using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.Globalization;

namespace imgsaver
{
    public partial class InputRecorderWindow : Window
    {
        private InputRecorder _recorder = new InputRecorder();
        private InputPlayer _player = new InputPlayer();
        private string _recordingsDir = DataPathManager.GetDataSubfolderPath("recordings");
        private bool _isInternalPlaying = false;
        private bool _isUpdatingUiInternally = false;

        public InputRecorderWindow()
        {
            InitializeComponent();
            if (!Directory.Exists(_recordingsDir)) Directory.CreateDirectory(_recordingsDir);

            // Load persistent state
            _isUpdatingUiInternally = true;
            SldSpeed.Value = RecordingManager.PlaybackSpeed;
            ChkSequential.IsChecked = RecordingManager.SequentialMode;
            if (RecordingManager.SelectedSlot == 1) RadSlot1.IsChecked = true; else RadSlot2.IsChecked = true;
            _isUpdatingUiInternally = false;

            UpdateEventCountDisplay();

            if (RecordingManager.HasEvents(1) || RecordingManager.HasEvents(2)) BtnPlay.IsEnabled = true;

            SldSpeed.ValueChanged += (s, e) => {
                if (TxtSpeed == null) return;
                RecordingManager.PlaybackSpeed = SldSpeed.Value;
                TxtSpeed.Text = SldSpeed.Value.ToString("0.0", CultureInfo.InvariantCulture) + "x";
                RecordingManager.SaveState();
            };

            _recorder.OnStopRequested += () => {
                Dispatcher.Invoke(() => { if (_recorder.IsRecording) StopRecording(); });
            };

            _player.OnInterventionStop += () => {
                Dispatcher.Invoke(() => {
                    _isInternalPlaying = false;
                    TxtStatus.Text = "Stopped (ESC/Interrupted)";
                });
            };
        }

        private int GetSelectedSlot()
        {
            if (RadSlot1 == null) return 1;
            return RadSlot1.IsChecked == true ? 1 : 2;
        }

        private void UpdateEventCountDisplay()
        {
            if (TxtEventCount == null || BtnPlay == null) return;
            var events = RecordingManager.GetEvents(GetSelectedSlot());
            TxtEventCount.Text = (events?.Count ?? 0).ToString();
            BtnPlay.IsEnabled = RecordingManager.HasEvents(1) || RecordingManager.HasEvents(2);
        }

        private void RadSlot_Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingUiInternally) return;
            RecordingManager.SelectedSlot = GetSelectedSlot();
            RecordingManager.SaveState();
            UpdateEventCountDisplay();
        }

        private void ChkSequential_Changed(object sender, RoutedEventArgs e)
        {
            if (ChkSequential == null || _isUpdatingUiInternally) return;
            RecordingManager.SequentialMode = ChkSequential.IsChecked == true;
            RecordingManager.SaveState();
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            _recorder.Start();
            SetControlsEnabled(false);
            BtnStop.IsEnabled = true;
            TxtStatus.Text = $"Recording Slot {GetSelectedSlot()}...";
            RecIndicator.Opacity = 1.0;
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e) => StopRecording();

        private void StopRecording()
        {
            if (!_recorder.IsRecording) return;
            _recorder.Stop();
            RecordingManager.SetEvents(GetSelectedSlot(), _recorder.GetEvents());
            SetControlsEnabled(true);
            BtnStop.IsEnabled = false;
            TxtStatus.Text = "Stopped";
            RecIndicator.Opacity = 0.0;
            UpdateEventCountDisplay();
        }

        private void SetControlsEnabled(bool enabled)
        {
            BtnStart.IsEnabled = enabled;
            BtnSave.IsEnabled = enabled;
            BtnLoad.IsEnabled = enabled;
            BtnPlay.IsEnabled = enabled;
            RadSlot1.IsEnabled = enabled;
            RadSlot2.IsEnabled = enabled;
            ChkSequential.IsEnabled = enabled;
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            string fileName = Path.Combine(_recordingsDir, $"slot{GetSelectedSlot()}_{DateTime.Now:yyyyMMdd_HHmmss}.json");
            await _recorder.SaveAsync(fileName);
            TxtStatus.Text = "Saved: " + Path.GetFileName(fileName);
        }

        private async void BtnLoad_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new OpenFileDialog { InitialDirectory = _recordingsDir, Filter = "JSON files|*.json|All files|*.*" };
            if (ofd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                if (RecordingManager.LoadFromFile(GetSelectedSlot(), ofd.FileName, out _))
                {
                    TxtStatus.Text = $"Loaded into Slot {GetSelectedSlot()}";
                    UpdateEventCountDisplay();
                }
                else TxtStatus.Text = "Failed to load";
            }
        }

        private async void BtnPlay_Click(object sender, RoutedEventArgs e)
        {
            if (_isInternalPlaying) return;
            _isInternalPlaying = true;

            SetControlsEnabled(false);
            BtnStopPlay.IsEnabled = true;

            try
            {
                int currentSlot = GetSelectedSlot();
                int startSlot = currentSlot;

                while (_isInternalPlaying)
                {
                    if (!RecordingManager.HasEvents(currentSlot))
                    {
                        if (ChkSequential.IsChecked == true)
                        {
                            currentSlot = (currentSlot == 1) ? 2 : 1;
                            if (!RecordingManager.HasEvents(currentSlot)) break;
                        }
                        else break;
                    }

                    _isUpdatingUiInternally = true;
                    if (currentSlot == 1) RadSlot1.IsChecked = true; else RadSlot2.IsChecked = true;
                    UpdateEventCountDisplay();
                    _isUpdatingUiInternally = false;

                    TxtStatus.Text = $"Playing Slot {currentSlot}... (Press ESC to Cancel)";

                    _player.SetEvents(RecordingManager.GetEvents(currentSlot));
                    _player.SetSpeed(RecordingManager.PlaybackSpeed);

                    await _player.PlayAsync(false);

                    if (!_isInternalPlaying) break;

                    if (ChkSequential.IsChecked == true)
                    {
                        currentSlot = (currentSlot == 1) ? 2 : 1;
                        if (currentSlot == startSlot && ChkLoop.IsChecked != true) break;
                    }
                    else if (ChkLoop.IsChecked != true)
                    {
                        break;
                    }
                }
            }
            catch { }
            finally
            {
                _isInternalPlaying = false;
                SetControlsEnabled(true);
                BtnStopPlay.IsEnabled = false;
                TxtStatus.Text = "Idle";
            }
        }

        private void BtnStopPlay_Click(object sender, RoutedEventArgs e)
        {
            _isInternalPlaying = false;
            _player.Stop();
            TxtStatus.Text = "Stopped";
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            _recorder.Dispose();
            _player.Stop();
            this.Close();
        }

        private void Window_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }
    }
}
