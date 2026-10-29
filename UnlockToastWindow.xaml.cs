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
        /// Synthesizes Sound #8 (PS5 Platinum Trophy Style: Silky Shimmer Swoosh + Futuristic E-Major-9 Glassy Chord + Stereo Reverb)
        /// as a 44.1kHz 16-bit stereo PCM WAV in memory (zero external audio files needed).
        /// </summary>
        private static byte[] GenerateSecretUnlockedWav()
        {
            const int sampleRate = 44100;
            const short channels = 2;
            const short bitsPerSample = 16;
            int totalSamples = (int)(sampleRate * 1.80);

            double[] left = new double[totalSamples];
            double[] right = new double[totalSamples];
            Random rng = new Random(42);
            double lpL = 0.0, lpR = 0.0;

            // E Major 9th futuristic glass chord at t = 0.16s
            double[] chord = new double[] { 164.81, 329.63, 493.88, 622.25, 739.99, 987.77, 1318.51 };

            for (int i = 0; i < totalSamples; i++)
            {
                double t = (double)i / sampleRate;

                // 1. Rising silky swoosh (0.00 - 0.28s)
                if (t < 0.28)
                {
                    double swEnv = Math.Sin(Math.PI * (t / 0.28));
                    double sweepFreq = 220.0 + 1800.0 * Math.Pow(t / 0.28, 2.0);
                    double noiseL = (rng.NextDouble() * 2.0 - 1.0) * 0.18;
                    double noiseR = (rng.NextDouble() * 2.0 - 1.0) * 0.18;
                    lpL = lpL * 0.82 + noiseL * 0.18;
                    lpR = lpR * 0.82 + noiseR * 0.18;
                    double toneSweep = Math.Sin(2.0 * Math.PI * sweepFreq * t) * 0.15;
                    left[i] += (lpL + toneSweep) * swEnv;
                    right[i] += (lpR + toneSweep) * swEnv;
                }

                // 2. Glassy chord impact at 0.16s
                double dt = t - 0.16;
                if (dt >= 0.0)
                {
                    double cEnv = Math.Min(1.0, dt / 0.008) * Math.Exp(-dt * 2.3);
                    for (int k = 0; k < chord.Length; k++)
                    {
                        double f = chord[k];
                        double mod = Math.Sin(2.0 * Math.PI * f * 2.0 * dt) * Math.Exp(-dt * 12.0) * 0.7;
                        double sig = Math.Sin(2.0 * Math.PI * f * dt + mod) * cEnv * 0.15;
                        left[i] += sig * (k % 2 == 0 ? 1.0 : 0.75);
                        right[i] += sig * (k % 2 == 0 ? 0.75 : 1.0);
                    }
                }
            }

            // 3. Stereo Reverb & Peak Normalization (-1.0 dBFS)
            int d1 = (int)(sampleRate * 0.078);
            int d2 = (int)(sampleRate * (0.078 * 1.37));
            for (int i = d1; i < totalSamples; i++)
            {
                left[i] += right[i - d1] * 0.36;
                right[i] += left[i - d1] * 0.36;
            }
            for (int i = d2; i < totalSamples; i++)
            {
                left[i] += left[i - d2] * (0.36 * 0.55);
                right[i] += right[i - d2] * (0.36 * 0.55);
            }

            double maxPeak = 0.01;
            for (int i = 0; i < totalSamples; i++)
            {
                if (Math.Abs(left[i]) > maxPeak) maxPeak = Math.Abs(left[i]);
                if (Math.Abs(right[i]) > maxPeak) maxPeak = Math.Abs(right[i]);
            }
            double scale = 0.88 / maxPeak;

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
                    short sL = (short)(Math.Max(-1.0, Math.Min(1.0, left[i] * scale)) * 32767);
                    short sR = (short)(Math.Max(-1.0, Math.Min(1.0, right[i] * scale)) * 32767);
                    bw.Write(sL);
                    bw.Write(sR);
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
