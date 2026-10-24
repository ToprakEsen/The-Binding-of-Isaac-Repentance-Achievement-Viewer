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
        private bool _isTurkish;
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
        public bool IsMilestone { get; set; }
        public string ShortMilestoneTitle { get; set; }

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

        public bool IsTurkish
        {
            get { return _isTurkish; }
            set
            {
                if (_isTurkish != value)
                {
                    _isTurkish = value;
                    RefreshLanguageBindings();
                }
            }
        }

        public void RefreshLanguageBindings()
        {
            OnPropertyChanged("DisplayInGameDesc");
            OnPropertyChanged("DisplayUnlockPrefix");
            OnPropertyChanged("DisplayUnlockBy");
            OnPropertyChanged("DisplayCategory");
            OnPropertyChanged("StatusText");
            OnPropertyChanged("MilestoneStatusText");
            OnPropertyChanged("StatusDetailText");
            OnPropertyChanged("SecretBannerTitle");
        }

        public string DisplayInGameDesc
        {
            get
            {
                if (_isTurkish)
                {
                    return !string.IsNullOrWhiteSpace(InGameDescTr) ? InGameDescTr : InGameDescEn;
                }
                if (string.IsNullOrWhiteSpace(InGameDescEn) || InGameDescEn == "-")
                {
                    return "Unlocks " + Name + ".";
                }
                return InGameDescEn;
            }
        }

        public string DisplayUnlockPrefix
        {
            get { return _isTurkish ? "Nasıl Açılır: " : "How to Unlock: "; }
        }

        public string DisplayUnlockBy
        {
            get
            {
                if (_isTurkish)
                {
                    return !string.IsNullOrWhiteSpace(UnlockByTr) ? UnlockByTr : UnlockByEn;
                }
                return !string.IsNullOrWhiteSpace(UnlockByEn) ? UnlockByEn : UnlockByTr;
            }
        }

        public string DisplayCategory
        {
            get
            {
                if (_isTurkish) return Category;
                return TranslateCategoryToEnglish(Category);
            }
        }

        public static string TranslateCategoryToEnglish(string catTr)
        {
            if (catTr == "Karakter") return "Character";
            if (catTr == "Eşya (Item)") return "Item";
            if (catTr == "Trinket") return "Trinket";
            if (catTr == "Boss & Bölüm") return "Boss & Chapter";
            if (catTr == "Challenge") return "Challenge";
            if (catTr == "Kart / Rune / Soul") return "Card / Rune / Soul";
            if (catTr == "Co-op Baby") return "Co-op Baby";
            if (catTr == "Repentance+ & Online") return "Repentance+ & Online";
            if (catTr == "Sır & Genel") return "Secret & Misc";
            return catTr;
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
                    OnPropertyChanged("MilestoneStatusText");
                    OnPropertyChanged("MilestoneStatusForeground");
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
            get
            {
                if (_isTurkish)
                {
                    return _isUnlocked ? "AÇIK" : "KİLİTLİ";
                }
                return _isUnlocked ? "UNLOCKED" : "LOCKED";
            }
        }

        public string MilestoneStatusText
        {
            get
            {
                if (_isTurkish)
                {
                    return _isUnlocked ? "✓ AÇIK" : "🔒 KİLİTLİ";
                }
                return _isUnlocked ? "✓ UNLOCKED" : "🔒 LOCKED";
            }
        }

        public Brush MilestoneStatusForeground
        {
            get
            {
                if (_isUnlocked)
                {
                    return new SolidColorBrush(Color.FromRgb(74, 222, 128)); // Soft Mint Emerald
                }
                return new SolidColorBrush(Color.FromRgb(148, 156, 176)); // Muted Cool Slate
            }
        }

        public string StatusDetailText
        {
            get
            {
                if (_isTurkish)
                {
                    return _isUnlocked ? "✓ KAYIT DOSYASINDA AÇIK" : "🔒 HENÜZ KİLİTLİ";
                }
                return _isUnlocked ? "✓ UNLOCKED IN SAVE" : "🔒 LOCKED IN SAVE";
            }
        }

        public string SecretBannerTitle
        {
            get
            {
                if (_isTurkish)
                {
                    return _isUnlocked ? "BAŞARIM DETAYI (AÇIK)" : "BAŞARIM DETAYI (KİLİTLİ)";
                }
                return _isUnlocked ? "ACHIEVEMENT DETAILS" : "LOCKED ACHIEVEMENT";
            }
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
                    return new SolidColorBrush(Color.FromRgb(54, 62, 82)); // Crisp Slate Border
                }
                return new SolidColorBrush(Color.FromRgb(38, 43, 56)); // Subtle Dark Slate
            }
        }

        public Brush CardBackgroundBrush
        {
            get
            {
                if (_isUnlocked)
                {
                    return new SolidColorBrush(Color.FromRgb(29, 33, 45)); // Elevated Slate Card
                }
                return new SolidColorBrush(Color.FromRgb(21, 24, 33)); // Muted Deep Slate Card
            }
        }

        public Brush StatusBadgeBrush
        {
            get
            {
                if (_isUnlocked)
                {
                    return new SolidColorBrush(Color.FromRgb(26, 66, 49)); // Deep Emerald Tint
                }
                return new SolidColorBrush(Color.FromRgb(46, 33, 42)); // Deep Muted Crimson-Slate
            }
        }

        public Brush DlcBadgeBrush
        {
            get
            {
                return new SolidColorBrush(Color.FromRgb(37, 42, 56)); // Unified Sleek Slate Pill
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
