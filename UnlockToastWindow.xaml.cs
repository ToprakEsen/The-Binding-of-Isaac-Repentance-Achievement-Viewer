using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using IsaacRepentancePlusAchievements.Models;

namespace IsaacRepentancePlusAchievements
{
    public partial class UnlockToastWindow : Window
    {
        private static readonly List<UnlockToastWindow> ActiveToasts = new List<UnlockToastWindow>();
        private const int MaxSimultaneousToasts = 4;
        private const double DisplayDurationSeconds = 5.0;

        private readonly AchievementItem _achievement;
        private readonly Action<AchievementItem> _onClicked;
        private DispatcherTimer _closeTimer;
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

            Loaded += UnlockToastWindow_Loaded;
            Closed += UnlockToastWindow_Closed;
        }

        public static UnlockToastWindow ShowToast(AchievementItem item, bool isTurkish, Action<AchievementItem> onClicked)
        {
            if (item == null) return null;

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
            return toast;
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
                using (System.IO.FileStream fs = new System.IO.FileStream(filePath, System.IO.FileMode.Create, System.IO.FileAccess.Write))
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
            }
        }

        private void UnlockToastWindow_Loaded(object sender, RoutedEventArgs e)
        {
            RepositionAllToasts();

            // 1. Fade-in + Slide-in animation (260ms)
            DoubleAnimation fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(260));
            BeginAnimation(OpacityProperty, fadeIn);

            DoubleAnimation slideIn = new DoubleAnimation(36.0, 0.0, TimeSpan.FromMilliseconds(260))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            SlideTransform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty, slideIn);

            // 2. Countdown bar shrink animation over 5.0 seconds
            DoubleAnimation barShrink = new DoubleAnimation(370.0, 0.0, TimeSpan.FromSeconds(DisplayDurationSeconds));
            CountdownFillBar.BeginAnimation(WidthProperty, barShrink);

            // 3. Auto-dismiss timer after 5.0 seconds
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
            if (ActiveToasts.Contains(this))
            {
                ActiveToasts.Remove(this);
                RepositionAllToasts();
            }
        }
    }
}
