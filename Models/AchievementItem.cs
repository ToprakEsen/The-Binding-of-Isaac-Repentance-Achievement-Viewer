using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace IsaacRepentancePlusAchievements.Models
{
    public class AchievementJsonDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Dlc { get; set; }
        public string Category { get; set; }
        public string InGameDescEn { get; set; }
        public string InGameDescTr { get; set; }
        public string UnlockByEn { get; set; }
        public string UnlockByTr { get; set; }
        public string IconFile { get; set; }
        public string SteamIconUrl { get; set; }
        public bool Hidden { get; set; }
    }

    public class AchievementItem : INotifyPropertyChanged
    {
        private bool _isUnlocked;
        private ImageSource _iconSource;

        public int Id { get; set; }
        public string Name { get; set; }
        public string Dlc { get; set; }
        public string Category { get; set; }
        public string InGameDescEn { get; set; }
        public string InGameDescTr { get; set; }
        public string UnlockByEn { get; set; }
        public string UnlockByTr { get; set; }
        public string IconFullPath { get; set; }
        public string SteamIconUrl { get; set; }
        public bool Hidden { get; set; }

        public string DisplayId
        {
            get { return string.Format("#{0:000}", Id); }
        }

        public ImageSource IconSource
        {
            get { return _iconSource; }
            set
            {
                _iconSource = value;
                OnPropertyChanged("IconSource");
            }
        }

        public bool IsUnlocked
        {
            get { return _isUnlocked; }
            set
            {
                if (_isUnlocked != value)
                {
                    _isUnlocked = value;
                    OnPropertyChanged("IsUnlocked");
                    OnPropertyChanged("LockOverlayVisibility");
                    OnPropertyChanged("UnlockedBadgeVisibility");
                    OnPropertyChanged("StatusText");
                    OnPropertyChanged("StatusDetailText");
                    OnPropertyChanged("ImageOpacity");
                    OnPropertyChanged("CardBorderBrush");
                    OnPropertyChanged("CardBackgroundBrush");
                    OnPropertyChanged("StatusBadgeBrush");
                    OnPropertyChanged("SecretBannerTitle");
                }
            }
        }

        public Visibility LockOverlayVisibility
        {
            get { return _isUnlocked ? Visibility.Collapsed : Visibility.Visible; }
        }

        public Visibility UnlockedBadgeVisibility
        {
            get { return _isUnlocked ? Visibility.Visible : Visibility.Collapsed; }
        }

        public string StatusText
        {
            get { return _isUnlocked ? "AÇIK" : "KİLİTLİ"; }
        }

        public string StatusDetailText
        {
            get { return _isUnlocked ? "BU BAŞARIM KAYIT DOSYASINDA AÇILMIŞ" : "BU BAŞARIM HENÜZ KİLİTLİ"; }
        }

        public string SecretBannerTitle
        {
            get { return _isUnlocked ? "SECRET FOUND! (SIR AÇILDI)" : "LOCKED SECRET (KİLİTLİ SIR)"; }
        }

        public double ImageOpacity
        {
            get { return _isUnlocked ? 1.0 : 0.42; }
        }

        public Brush CardBorderBrush
        {
            get
            {
                if (_isUnlocked)
                {
                    return new SolidColorBrush(Color.FromRgb(186, 142, 62)); // Aged Gold
                }
                return new SolidColorBrush(Color.FromRgb(74, 52, 43)); // Dark Cellar Wood
            }
        }

        public Brush CardBackgroundBrush
        {
            get
            {
                if (_isUnlocked)
                {
                    return new SolidColorBrush(Color.FromRgb(43, 33, 28)); // Warm Dark Parchment Stone
                }
                return new SolidColorBrush(Color.FromRgb(27, 21, 18)); // Deep Basement Shadow
            }
        }

        public Brush StatusBadgeBrush
        {
            get
            {
                if (_isUnlocked)
                {
                    return new SolidColorBrush(Color.FromRgb(58, 118, 52)); // Isaac Poison/Emerald Green
                }
                return new SolidColorBrush(Color.FromRgb(138, 32, 32)); // Isaac Blood Red
            }
        }

        public Brush DlcBadgeBrush
        {
            get
            {
                if (Dlc == "Repentance+") return new SolidColorBrush(Color.FromRgb(176, 40, 40));
                if (Dlc == "Repentance") return new SolidColorBrush(Color.FromRgb(128, 36, 56));
                if (Dlc == "Afterbirth+") return new SolidColorBrush(Color.FromRgb(92, 62, 115));
                if (Dlc == "Afterbirth") return new SolidColorBrush(Color.FromRgb(48, 90, 122));
                return new SolidColorBrush(Color.FromRgb(110, 82, 52));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propName)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(propName));
            }
        }
    }
}
