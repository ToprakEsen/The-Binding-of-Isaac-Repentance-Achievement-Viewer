using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using IsaacRepentancePlusAchievements.Models;

namespace IsaacRepentancePlusAchievements
{
    public partial class UnlockToastWindow : Window
    {
        private static readonly List<UnlockToastWindow> ActiveToasts = new List<UnlockToastWindow>();
        private const int MaxSimultaneousToasts = 4;
        private const double DisplayDurationSeconds = 5.5;
        private static byte[] _cachedUnlockWavBytes;

        // Win32 Native Constants & Imports for Borderless Fullscreen Overlay Support
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TOPMOST = 0x00000008;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_SHOWWINDOW = 0x0040;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int X,
            int Y,
            int cx,
            int cy,
            uint uFlags);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr ProcessId);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        private readonly AchievementItem _achievement;
        private readonly Action<AchievementItem> _onClicked;
        private DispatcherTimer _closeTimer;
        private DispatcherTimer _topmostEnforceTimer;
        private IntPtr _hwnd = IntPtr.Zero;
        private bool _isClosingAnimated = false;

        public UnlockToastWindow(AchievementItem item, bool isTurkish, Action<AchievementItem> onClicked)
        {
            InitializeComponent();

            _achievement = item;
            _onClicked = onClicked;

            ImgToastIcon.Source = item.IconSource;
            TxtToastHeader.Text = isTurkish ? "🏆 YENİ BAŞARIM AÇILDI!" : "🏆 SECRET UNLOCKED!";
            TxtToastId.Text = item.DisplayId;
            TxtToastTitle.Text = item.Name;
            TxtToastDesc.Text = item.DisplayInGameDesc;

            SourceInitialized += UnlockToastWindow_SourceInitialized;
            Loaded += UnlockToastWindow_Loaded;
            Closed += UnlockToastWindow_Closed;
        }

        private void UnlockToastWindow_SourceInitialized(object sender, EventArgs e)
        {
            try
            {
                _hwnd = new WindowInteropHelper(this).Handle;
                if (_hwnd != IntPtr.Zero)
                {
                    int exStyle = GetWindowLong(_hwnd, GWL_EXSTYLE);
                    SetWindowLong(_hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
                    EnforceTopmostZOrder();
                }
            }
            catch { }
        }

        private void EnforceTopmostZOrder()
        {
            if (_hwnd == IntPtr.Zero) return;
            try
            {
                IntPtr fgHwnd = GetForegroundWindow();
                uint fgThread = fgHwnd != IntPtr.Zero ? GetWindowThreadProcessId(fgHwnd, IntPtr.Zero) : 0;
                uint curThread = GetCurrentThreadId();
                bool attached = false;

                if (fgThread != 0 && fgThread != curThread)
                {
                    attached = AttachThreadInput(curThread, fgThread, true);
                }

                try
                {
                    SetWindowPos(
                        _hwnd,
                        HWND_TOPMOST,
                        0, 0, 0, 0,
                        SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
                }
                finally
                {
                    if (attached)
                    {
                        AttachThreadInput(curThread, fgThread, false);
                    }
                }
            }
            catch { }
        }

        public static UnlockToastWindow ShowToast(AchievementItem item, bool isTurkish, Action<AchievementItem> onClicked)
        {
            if (item == null) return null;

            // Play the achievement unlock chime
            PlayUnlockChimeSafe();

            // Keep max 4 visible toasts on screen so the screen never floods
            while (ActiveToasts.Count >= MaxSimultaneousToasts)
            {
                UnlockToastWindow oldest = ActiveToasts[0];
                ActiveToasts.RemoveAt(0);
                try
                {
                    oldest.Close();
                }
                catch { }
            }

            UnlockToastWindow toast = new UnlockToastWindow(item, isTurkish, onClicked);
            ActiveToasts.Add(toast);
            RepositionAllToasts();
            toast.Show();
            toast.EnforceTopmostZOrder();
            return toast;
        }

        private static void PlayUnlockChimeSafe()
        {
            try
            {
                if (_cachedUnlockWavBytes == null)
                {
                    _cachedUnlockWavBytes = GenerateSecretUnlockedWav();
                }
                MemoryStream ms = new MemoryStream(_cachedUnlockWavBytes, false);
                SoundPlayer player = new SoundPlayer(ms);
                player.Play();
            }
            catch
            {
                try
                {
                    SystemSounds.Asterisk.Play();
                }
                catch { }
            }
        }

        /// <summary>
        /// Synthesizes a rich, Isaac/Steam-inspired "Secret Unlocked" 4-note resonant chime
        /// as a 44.1kHz 16-bit stereo PCM WAV in memory (zero external audio files needed).
        /// </summary>
        private static byte[] GenerateSecretUnlockedWav()
        {
            const int sampleRate = 44100;
            const short channels = 2;
            const short bitsPerSample = 16;
            const double durationSeconds = 1.05;
            int totalSamples = (int)(sampleRate * durationSeconds);

            // 4-note mystical/triumphant arpeggio: D5 (587.33 Hz), F#5 (739.99 Hz), A5 (880.00 Hz), D6 (1174.66 Hz)
            double[] noteFreqs = new double[] { 587.33, 739.99, 880.00, 1174.66 };
            double[] noteStarts = new double[] { 0.00, 0.09, 0.18, 0.28 };
            double[] noteDecays = new double[] { 4.8, 4.5, 4.2, 3.2 };

            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter bw = new BinaryWriter(ms))
            {
                int byteRate = sampleRate * channels * (bitsPerSample / 8);
                short blockAlign = (short)(channels * (bitsPerSample / 8));
                int dataSize = totalSamples * blockAlign;

                // RIFF WAVE Header
                bw.Write(new char[] { 'R', 'I', 'F', 'F' });
                bw.Write(36 + dataSize);
                bw.Write(new char[] { 'W', 'A', 'V', 'E' });
                bw.Write(new char[] { 'f', 'm', 't', ' ' });
                bw.Write(16);
                bw.Write((short)1); // PCM
                bw.Write(channels);
                bw.Write(sampleRate);
                bw.Write(byteRate);
                bw.Write(blockAlign);
                bw.Write(bitsPerSample);
                bw.Write(new char[] { 'd', 'a', 't', 'a' });
                bw.Write(dataSize);

                for (int i = 0; i < totalSamples; i++)
                {
                    double t = (double)i / sampleRate;
                    double sampleVal = 0.0;

                    for (int n = 0; n < noteFreqs.Length; n++)
                    {
                        double dt = t - noteStarts[n];
                        if (dt >= 0.0)
                        {
                            // Smooth attack (8ms) + exponential bell decay
                            double attack = Math.Min(1.0, dt / 0.008);
                            double env = attack * Math.Exp(-dt * noteDecays[n]);

                            double freq = noteFreqs[n];
                            // Fundamental + warm octave sub-harmonic + bell 2nd & 3rd overtones
                            double wave =
                                0.60 * Math.Sin(2.0 * Math.PI * freq * dt) +
                                0.22 * Math.Sin(2.0 * Math.PI * (freq * 0.5) * dt) +
                                0.13 * Math.Sin(2.0 * Math.PI * (freq * 2.0) * dt) +
                                0.05 * Math.Sin(2.0 * Math.PI * (freq * 3.0) * dt);

                            sampleVal += wave * env * 0.28;
                        }
                    }

                    if (sampleVal > 0.95) sampleVal = 0.95;
                    if (sampleVal < -0.95) sampleVal = -0.95;

                    short pcm = (short)(sampleVal * 32767);
                    bw.Write(pcm); // Left channel
                    bw.Write(pcm); // Right channel
                }

                bw.Flush();
                return ms.ToArray();
            }
        }

        public void SaveToastRenderPng(string filePath)
        {
            try
            {
                int w = (int)Math.Max(1, ActualWidth);
                int h = (int)Math.Max(1, ActualHeight);
                System.Windows.Media.Imaging.RenderTargetBitmap rtb =
                    new System.Windows.Media.Imaging.RenderTargetBitmap(w, h, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
                rtb.Render(ToastBorder);
                System.Windows.Media.Imaging.PngBitmapEncoder encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
                using (FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                {
                    encoder.Save(fs);
                }
            }
            catch { }
        }

        private static void RepositionAllToasts()
        {
            Rect workArea = SystemParameters.WorkArea;
            const double rightMargin = 16.0;
            const double bottomMargin = 16.0;
            const double verticalSpacing = 110.0;

            for (int i = 0; i < ActiveToasts.Count; i++)
            {
                UnlockToastWindow t = ActiveToasts[i];
                int stackOrderFromBottom = ActiveToasts.Count - 1 - i;
                t.Left = workArea.Right - t.Width - rightMargin;
                t.Top = workArea.Bottom - t.Height - bottomMargin - (stackOrderFromBottom * verticalSpacing);
                t.EnforceTopmostZOrder();
            }
        }

        private void UnlockToastWindow_Loaded(object sender, RoutedEventArgs e)
        {
            RepositionAllToasts();
            EnforceTopmostZOrder();

            // Periodically re-assert HWND_TOPMOST every 150ms so borderless fullscreen games never cover the toast
            _topmostEnforceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(150)
            };
            _topmostEnforceTimer.Tick += (s, args) => EnforceTopmostZOrder();
            _topmostEnforceTimer.Start();

            // 1. Fade-in + Slide-in animation (260ms)
            DoubleAnimation fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(260));
            BeginAnimation(OpacityProperty, fadeIn);

            DoubleAnimation slideIn = new DoubleAnimation(36.0, 0.0, TimeSpan.FromMilliseconds(260))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            SlideTransform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, slideIn);

            // 2. Countdown bar shrink animation over DisplayDurationSeconds
            DoubleAnimation barShrink = new DoubleAnimation(370.0, 0.0, TimeSpan.FromSeconds(DisplayDurationSeconds));
            CountdownFillBar.BeginAnimation(WidthProperty, barShrink);

            // 3. Auto-dismiss timer after DisplayDurationSeconds
            _closeTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(DisplayDurationSeconds)
            };
            _closeTimer.Tick += (s, args) =>
            {
                _closeTimer.Stop();
                BeginFadeOutAndClose();
            };
            _closeTimer.Start();
        }

        private void BeginFadeOutAndClose()
        {
            if (_isClosingAnimated) return;
            _isClosingAnimated = true;

            if (_closeTimer != null)
            {
                _closeTimer.Stop();
            }
            if (_topmostEnforceTimer != null)
            {
                _topmostEnforceTimer.Stop();
            }

            DoubleAnimation fadeOut = new DoubleAnimation(Opacity, 0.0, TimeSpan.FromMilliseconds(380));
            fadeOut.Completed += (s, args) =>
            {
                try
                {
                    Close();
                }
                catch { }
            };
            BeginAnimation(OpacityProperty, fadeOut);

            DoubleAnimation slideOut = new DoubleAnimation(0.0, 28.0, TimeSpan.FromMilliseconds(380));
            SlideTransform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, slideOut);
        }

        private void ToastBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_onClicked != null && _achievement != null)
            {
                _onClicked(_achievement);
            }
            BeginFadeOutAndClose();
        }

        private void BtnDismiss_Click(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            BeginFadeOutAndClose();
        }

        private void UnlockToastWindow_Closed(object sender, EventArgs e)
        {
            if (_closeTimer != null)
            {
                _closeTimer.Stop();
            }
            if (_topmostEnforceTimer != null)
            {
                _topmostEnforceTimer.Stop();
            }
            if (ActiveToasts.Contains(this))
            {
                ActiveToasts.Remove(this);
                RepositionAllToasts();
            }
        }
    }
}
