using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
#if NET6_0_OR_GREATER
using System.Text.Json;
#else
using System.Web.Script.Serialization;
#endif
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using IsaacRepentancePlusAchievements.Models;
using IsaacRepentancePlusAchievements.Services;

namespace IsaacRepentancePlusAchievements
{
    public partial class MainWindow : Window
    {
        private readonly List<AchievementItem> _allAchievements = new List<AchievementItem>();
        private readonly List<AchievementItem> _milestoneAchievements = new List<AchievementItem>();
        private readonly Dictionary<int, HashSet<int>> _previousSlotUnlockedIds = new Dictionary<int, HashSet<int>>();
        private readonly HashSet<int> _sessionUnlockedIds = new HashSet<int>();
        private List<SaveSlotData> _saveSlots = new List<SaveSlotData>();
        private int _activeSlotIndex = 0; // 0 = Slot 1, 1 = Slot 2, 2 = Slot 3
        private string _statusFilter = "ALL"; // ALL, UNLOCKED, LOCKED, MILESTONES, RECENT
        private bool _isTurkish = false; // Default language: English (false)
        private bool _isUpdatingCharacterCombo = false;
        private FileSystemWatcher _saveWatcher;
        private DispatcherTimer _watcherDebounceTimer;
        private bool _isInitialized = false;

        // All 34 Playable Characters (17 Normal + 17 Tainted)
        private static readonly string[] PlayableCharacters = new string[]
        {
            "Isaac", "Magdalene", "Cain", "Judas", "???", "Eve", "Samson", "Azazel",
            "Lazarus", "Eden", "The Lost", "Lilith", "Keeper", "Apollyon", "The Forgotten", "Bethany", "Jacob and Esau",
            "Tainted Isaac", "Tainted Magdalene", "Tainted Cain", "Tainted Judas", "Tainted ???", "Tainted Eve", "Tainted Samson", "Tainted Azazel",
            "Tainted Lazarus", "Tainted Eden", "Tainted Lost", "Tainted Lilith", "Tainted Keeper", "Tainted Apollyon", "Tainted Forgotten", "Tainted Bethany", "Tainted Jacob"
        };

        private static readonly Dictionary<int, string> DirectCharacterUnlockIds = new Dictionary<int, string>
        {
            { 1, "Magdalene" }, { 2, "Cain" }, { 3, "Judas" }, { 32, "???" },
            { 42, "Eve" }, { 67, "Samson" }, { 79, "Azazel" }, { 80, "Lazarus" },
            { 81, "Eden" }, { 82, "The Lost" }, { 199, "Lilith" }, { 251, "Keeper" },
            { 340, "Apollyon" }, { 390, "The Forgotten" }, { 404, "Bethany" }, { 405, "Jacob and Esau" },
            { 474, "Tainted Isaac" }, { 475, "Tainted Magdalene" }, { 476, "Tainted Cain" }, { 477, "Tainted Judas" },
            { 478, "Tainted ???" }, { 479, "Tainted Eve" }, { 480, "Tainted Samson" }, { 481, "Tainted Azazel" },
            { 482, "Tainted Lazarus" }, { 483, "Tainted Eden" }, { 484, "Tainted Lost" }, { 485, "Tainted Lilith" },
            { 486, "Tainted Keeper" }, { 487, "Tainted Apollyon" }, { 488, "Tainted Forgotten" }, { 489, "Tainted Bethany" },
            { 490, "Tainted Jacob" }
        };

        // Ordered progression of 11 iconic Binding of Isaac milestones (for Milestones filter tab)
        private static readonly int[] MilestoneOrderIds = new int[]
        {
            34,  // It Lives!
            407, // A Secret Exit
            635, // A Strange Door
            69,  // !Platinum God!
            84,  // The Real Platinum God
            156, // Godhead
            235, // 1001%
            339, // 1000000%
            547, // Mega Mush
            636, // Death Certificate
            637  // Dead God
        };

        // 6 featured milestones displayed in the clean top showcase strip
        private static readonly int[] FeaturedMilestoneIds = new int[]
        {
            34,  // It Lives!
            635, // A Strange Door
            69,  // Platinum God
            84,  // Real Platinum God
            339, // 1000000%
            637  // Dead God
        };

        private static readonly Dictionary<int, string> MilestoneShortNames = new Dictionary<int, string>
        {
            { 34,  "It Lives!" },
            { 407, "Secret Exit" },
            { 635, "Strange Door" },
            { 69,  "Platinum God" },
            { 84,  "Real Platinum God" },
            { 156, "Godhead" },
            { 235, "1001%" },
            { 339, "1000000%" },
            { 547, "Mega Mush" },
            { 636, "Death Certificate" },
            { 637, "Dead God" }
        };

        // Canonical Turkish category names stored in achievements.json by index (1..9)
        private static readonly string[] CategoryKeysByIndex = new string[]
        {
            null,
            "Karakter",
            "Eşya (Item)",
            "Trinket",
            "Boss & Bölüm",
            "Challenge",
            "Kart / Rune / Soul",
            "Co-op Baby",
            "Repentance+ & Online",
            "Sır & Genel"
        };

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            Closed += (s, e) => Application.Current.Shutdown();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                LoadAchievementsCatalog();
                LoadAndApplySaveFiles();
                _isInitialized = true;
                ApplyLanguage();

                if (LstAchievements.Items.Count > 0)
                {
                    LstAchievements.SelectedIndex = 0;
                }

                string[] cmdArgs = Environment.GetCommandLineArgs();
                for (int i = 1; i < cmdArgs.Length; i++)
                {
                    if (cmdArgs[i] == "--lang-tr")
                    {
                        _isTurkish = true;
                        ApplyLanguage();
                    }
                    else if (cmdArgs[i] == "--filter-locked")
                    {
                        _statusFilter = "LOCKED";
                        UpdateStatusFilterButtonStyles();
                        ApplyFilters();
                        if (LstAchievements.Items.Count > 0) LstAchievements.SelectedIndex = 0;
                    }
                    else if (cmdArgs[i] == "--char-index" && i + 1 < cmdArgs.Length)
                    {
                        int cIdx;
                        if (int.TryParse(cmdArgs[i + 1], out cIdx) && cIdx >= 0 && cIdx < CmbCharacterFilter.Items.Count)
                        {
                            CmbCharacterFilter.SelectedIndex = cIdx;
                            ApplyFilters();
                            if (LstAchievements.Items.Count > 0) LstAchievements.SelectedIndex = 0;
                        }
                    }
                    else if (cmdArgs[i] == "--test-toast" && i + 1 < cmdArgs.Length)
                    {
                        string toastOutPath = cmdArgs[i + 1];
                        AchievementItem sample = (LstAchievements.SelectedItem as AchievementItem)
                            ?? _allAchievements.FirstOrDefault(a => a.Id == 29)
                            ?? _allAchievements[0];
                        _sessionUnlockedIds.Add(sample.Id);
                        sample.IsRecentlyUnlocked = true;
                        SelectSaveSlot(_activeSlotIndex);
                        UnlockToastWindow toastWin = UnlockToastWindow.ShowToast(sample, _isTurkish, OnToastAchievementClicked);
                        DispatcherTimer tTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(550) };
                        tTimer.Tick += (st, et) =>
                        {
                            tTimer.Stop();
                            if (toastWin != null) toastWin.SaveToastRenderPng(toastOutPath);
                        };
                        tTimer.Start();
                    }
                    else if (cmdArgs[i] == "--screenshot" && i + 1 < cmdArgs.Length)
                    {
                        string outPath = cmdArgs[i + 1];
                        DispatcherTimer snapTimer = new DispatcherTimer();
                        snapTimer.Interval = TimeSpan.FromMilliseconds(900);
                        snapTimer.Tick += (s2, e2) =>
                        {
                            snapTimer.Stop();
                            SaveWindowRenderPng(outPath);
                            Close();
                        };
                        snapTimer.Start();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Failed to load achievement data:\n" + ex.Message,
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void BtnLanguageToggle_Click(object sender, RoutedEventArgs e)
        {
            _isTurkish = !_isTurkish;
            ApplyLanguage();
        }

        private void ApplyLanguage()
        {
            // 1. Update Top-Right Flag Emoji & Language Code
            FlagUkCanvas.Visibility = _isTurkish ? Visibility.Collapsed : Visibility.Visible;
            FlagTrCanvas.Visibility = _isTurkish ? Visibility.Visible : Visibility.Collapsed;
            TxtLanguageCode.Text = _isTurkish ? "TR" : "EN";
            BtnLanguageToggle.ToolTip = _isTurkish
                ? "Dili İngilizceye çevir (Switch to English 🇬🇧)"
                : "Switch language to Turkish (Türkçeye geç 🇹🇷)";

            // 2. Update Window Title & Top Bar Labels
            Title = _isTurkish
                ? "The Binding of Isaac: Repentance+ - Başarım Günlüğü"
                : "The Binding of Isaac: Repentance+ - Achievement Viewer";
            TxtTitleBadge.Text = _isTurkish ? "BAŞARIM GÜNLÜĞÜ" : "ACHIEVEMENT VIEWER";
            TxtReadOnlyShield.Text = _isTurkish ? "🔒 Salt Okunur" : "🔒 Read-Only";
            TxtBtnTestToast.Text = _isTurkish ? "🔔 Bildirim" : "🔔 Test Toast";
            BtnTestToast.ToolTip = _isTurkish
                ? "Sağ altta başarım açıldı bildirimini (Toast) test et"
                : "Preview Bottom-Right Achievement Notification";
            TxtBtnRefresh.Text = _isTurkish ? "↻ Yenile" : "↻ Refresh";
            TxtBtnSelectSave.Text = _isTurkish ? "📂 Save Seç" : "📂 Open Save";

            // 3. Update Save Slot Headers & Stats Labels
            TxtSlot1Header.Text = _isTurkish ? "1. KAYIT" : "SAVE 1";
            TxtSlot2Header.Text = _isTurkish ? "2. KAYIT" : "SAVE 2";
            TxtSlot3Header.Text = _isTurkish ? "3. KAYIT" : "SAVE 3";

            LblStatMomKills.Text = "MOM KILLS";
            LblStatDeaths.Text = _isTurkish ? "ÖLÜM" : "DEATHS";
            LblStatStreak.Text = _isTurkish ? "SERİ" : "STREAK";
            LblStatEden.Text = "EDEN TOKENS";

            // 4. Update Milestones Bar Headers
            LblMilestonesTitle.Text = _isTurkish ? "🏆 KİLOMETRE TAŞLARI" : "🏆 MILESTONES";

            // 5. Update Search & Filter Controls
            TxtSearchPlaceholder.Text = _isTurkish
                ? "Başarım veya #ID ara..."
                : "Search or #ID...";
            LblCharacterFilter.Text = _isTurkish ? "Karakter:" : "Char:";
            CbiDlcAll.Content = _isTurkish ? "Tüm Paketler" : "All Packs";
            LblCategoryFilter.Text = _isTurkish ? "Kategori:" : "Category:";
            CbiCatAll.Content = _isTurkish ? "Tüm Kategoriler" : "All Categories";
            CbiCatChar.Content = _isTurkish ? "Karakter" : "Character";
            CbiCatItem.Content = _isTurkish ? "Eşya (Item)" : "Item";
            CbiCatTrinket.Content = "Trinket";
            CbiCatBoss.Content = _isTurkish ? "Boss & Bölüm" : "Boss & Chapter";
            CbiCatChallenge.Content = "Challenge";
            CbiCatCard.Content = _isTurkish ? "Kart / Rune / Soul" : "Card / Rune / Soul";
            CbiCatBaby.Content = "Co-op Baby";
            CbiCatRepPlus.Content = "Repentance+ & Online";
            CbiCatSecret.Content = _isTurkish ? "Sır & Genel" : "Secret & Misc";

            // 6. Update Detail Sheet Static Headers
            LblDetailDlc.Text = _isTurkish ? "PAKET (DLC)" : "DLC PACK";
            LblDetailCategory.Text = _isTurkish ? "KATEGORİ" : "CATEGORY";
            LblDetailDescSection.Text = _isTurkish ? "📜 BAŞARIM HAKKINDA AÇIKLAMA" : "📜 ACHIEVEMENT DESCRIPTION";
            LblDetailUnlockPrimary.Text = _isTurkish ? "🗝️ NASIL AÇILIR? (TÜRKÇE REHBER)" : "🗝️ HOW TO UNLOCK";
            LblDetailUnlockSecondary.Text = _isTurkish ? "🌐 ORİJİNAL KOŞUL (ENGLISH)" : "🌐 TURKISH GUIDE (TÜRKÇE REHBER)";
            TxtFooterRight.Text = _isTurkish
                ? "The Binding of Isaac: Repentance+ Achievement Viewer (.NET WPF • Salt Okunur)"
                : "The Binding of Isaac: Repentance+ Achievement Viewer (.NET WPF • Read-Only)";

            // 7. Update each AchievementItem's language flag
            foreach (AchievementItem item in _allAchievements)
            {
                item.IsTurkish = _isTurkish;
            }

            // 8. Refresh slot summaries & active slot banner
            if (_saveSlots.Count >= 3)
            {
                UpdateSlotButtonSummary(0, TxtSlot1Percent, TxtSlot1Count, TxtSlot1Rank);
                UpdateSlotButtonSummary(1, TxtSlot2Percent, TxtSlot2Count, TxtSlot2Rank);
                UpdateSlotButtonSummary(2, TxtSlot3Percent, TxtSlot3Count, TxtSlot3Rank);
            }
            SelectSaveSlot(_activeSlotIndex);
        }

        private string GetAppBaseDir()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            if (File.Exists(Path.Combine(baseDir, "Data", "achievements.json")))
            {
                return baseDir;
            }
            DirectoryInfo parent = Directory.GetParent(baseDir);
            while (parent != null)
            {
                if (File.Exists(Path.Combine(parent.FullName, "Data", "achievements.json")))
                {
                    return parent.FullName;
                }
                parent = parent.Parent;
            }

            // Standalone Single-EXE fallback: extract embedded assets_bundle.zip into LocalAppData cache
            string cacheDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "IsaacRepentancePlusAchievements",
                "v1.4.0");

            string cachedJson = Path.Combine(cacheDir, "Data", "achievements.json");
            if (File.Exists(cachedJson))
            {
                return cacheDir;
            }

            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                string resName = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("assets_bundle.zip", StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrEmpty(resName))
                {
                    if (Directory.Exists(cacheDir))
                    {
                        Directory.Delete(cacheDir, true);
                    }
                    Directory.CreateDirectory(cacheDir);
                    string tempZip = Path.Combine(cacheDir, "assets_bundle.zip");
                    using (Stream rs = asm.GetManifestResourceStream(resName))
                    using (FileStream fs = new FileStream(tempZip, FileMode.Create, FileAccess.Write))
                    {
                        rs.CopyTo(fs);
                    }
                    ZipFile.ExtractToDirectory(tempZip, cacheDir);
                    File.Delete(tempZip);
                    if (File.Exists(cachedJson))
                    {
                        return cacheDir;
                    }
                }
            }
            catch
            {
            }

            return baseDir;
        }

        private void LoadAchievementsCatalog()
        {
            _allAchievements.Clear();
            _milestoneAchievements.Clear();

            string baseDir = GetAppBaseDir();
            string jsonPath = Path.Combine(baseDir, "Data", "achievements.json");

            if (!File.Exists(jsonPath))
            {
                throw new FileNotFoundException("achievements.json not found: " + jsonPath);
            }

            string jsonText = File.ReadAllText(jsonPath, Encoding.UTF8);
#if NET6_0_OR_GREATER
            JsonSerializerOptions opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            List<AchievementJsonDto> dtos = JsonSerializer.Deserialize<List<AchievementJsonDto>>(jsonText, opts);
#else
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;
            List<AchievementJsonDto> dtos = serializer.Deserialize<List<AchievementJsonDto>>(jsonText);
#endif

            Dictionary<int, AchievementItem> byId = new Dictionary<int, AchievementItem>();

            foreach (AchievementJsonDto dto in dtos.OrderBy(d => d.Id))
            {
                string iconFullPath = Path.Combine(baseDir, dto.IconFile.Replace('/', '\\'));
                ImageSource bmp = LoadIconBitmapSafe(iconFullPath);

                string shortMsName;
                bool isMs = MilestoneShortNames.TryGetValue(dto.Id, out shortMsName);
                string assocChar = ResolveAssociatedCharacter(dto.Id, dto.Name, dto.UnlockByEn);

                AchievementItem item = new AchievementItem
                {
                    Id = dto.Id,
                    Name = dto.Name ?? ("Secret #" + dto.Id),
                    Dlc = dto.Dlc ?? "Rebirth",
                    Category = dto.Category ?? "Sır & Genel",
                    InGameDescEn = string.IsNullOrWhiteSpace(dto.InGameDescEn) ? "-" : dto.InGameDescEn,
                    InGameDescTr = string.IsNullOrWhiteSpace(dto.InGameDescTr) ? "-" : dto.InGameDescTr,
                    UnlockByEn = string.IsNullOrWhiteSpace(dto.UnlockByEn) ? "-" : dto.UnlockByEn,
                    UnlockByTr = string.IsNullOrWhiteSpace(dto.UnlockByTr) ? "-" : dto.UnlockByTr,
                    IconFullPath = iconFullPath,
                    SteamIconUrl = dto.SteamIconUrl,
                    Hidden = dto.Hidden,
                    IconSource = bmp,
                    IsTurkish = _isTurkish,
                    IsUnlocked = false,
                    IsMilestone = isMs,
                    ShortMilestoneTitle = isMs ? shortMsName : (dto.Name ?? ""),
                    AssociatedCharacter = assocChar
                };

                _allAchievements.Add(item);
                byId[item.Id] = item;
            }

            foreach (int msId in MilestoneOrderIds)
            {
                AchievementItem msItem;
                if (byId.TryGetValue(msId, out msItem))
                {
                    _milestoneAchievements.Add(msItem);
                }
            }

            List<AchievementItem> featuredMilestones = new List<AchievementItem>();
            foreach (int fId in FeaturedMilestoneIds)
            {
                AchievementItem fItem;
                if (byId.TryGetValue(fId, out fItem))
                {
                    featuredMilestones.Add(fItem);
                }
            }

            IcMilestones.ItemsSource = featuredMilestones;
        }

        private string ResolveAssociatedCharacter(int id, string name, string unlockByEn)
        {
            string directChar;
            if (DirectCharacterUnlockIds.TryGetValue(id, out directChar))
            {
                return directChar;
            }

            if (string.IsNullOrWhiteSpace(unlockByEn)) return null;

            // Check Tainted characters first (indices 17..33) so "as Tainted Isaac" matches Tainted Isaac
            for (int i = 17; i < PlayableCharacters.Length; i++)
            {
                string tChar = PlayableCharacters[i];
                if (unlockByEn.IndexOf("as " + tChar, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return tChar;
                }
            }

            // Check Normal characters (indices 0..16)
            if (unlockByEn.IndexOf("as Tainted ", StringComparison.OrdinalIgnoreCase) < 0)
            {
                for (int i = 0; i < 17; i++)
                {
                    string nChar = PlayableCharacters[i];
                    string shortName = nChar.StartsWith("The ", StringComparison.OrdinalIgnoreCase) ? nChar.Substring(4) : nChar;

                    if (ContainsCharacterPhrase(unlockByEn, "as " + nChar) ||
                        ContainsCharacterPhrase(unlockByEn, "as " + shortName))
                    {
                        return nChar;
                    }
                }
            }

            return null;
        }

        private static bool ContainsCharacterPhrase(string text, string phrase)
        {
            int idx = text.IndexOf(phrase, StringComparison.OrdinalIgnoreCase);
            while (idx >= 0)
            {
                int afterIdx = idx + phrase.Length;
                if (afterIdx >= text.Length) return true;
                char next = text[afterIdx];
                if (!char.IsLetterOrDigit(next)) return true;
                idx = text.IndexOf(phrase, idx + 1, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        private ImageSource LoadIconBitmapSafe(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    BitmapImage bi = new BitmapImage();
                    bi.BeginInit();
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.UriSource = new Uri(path, UriKind.Absolute);
                    bi.EndInit();
                    bi.Freeze();
                    return bi;
                }
            }
            catch
            {
            }
            return null;
        }

        private void LoadAndApplySaveFiles()
        {
            _saveSlots = ReadOnlySaveReader.AutoDetectAndReadAllSlots();

            // Check for newly unlocked achievements across any slot since the last read
            List<AchievementItem> newlyUnlockedItems = new List<AchievementItem>();
            for (int sIdx = 0; sIdx < _saveSlots.Count; sIdx++)
            {
                SaveSlotData slot = _saveSlots[sIdx];
                if (!slot.Exists) continue;

                HashSet<int> prevSet;
                if (_previousSlotUnlockedIds.TryGetValue(sIdx, out prevSet))
                {
                    foreach (int achId in slot.UnlockedAchievementIds)
                    {
                        if (!prevSet.Contains(achId))
                        {
                            _sessionUnlockedIds.Add(achId);
                            AchievementItem item = _allAchievements.FirstOrDefault(a => a.Id == achId);
                            if (item != null)
                            {
                                item.IsRecentlyUnlocked = true;
                                newlyUnlockedItems.Add(item);
                            }
                        }
                    }
                }

                _previousSlotUnlockedIds[sIdx] = new HashSet<int>(slot.UnlockedAchievementIds);
            }

            if (_saveSlots.Count >= 3)
            {
                UpdateSlotButtonSummary(0, TxtSlot1Percent, TxtSlot1Count, TxtSlot1Rank);
                UpdateSlotButtonSummary(1, TxtSlot2Percent, TxtSlot2Count, TxtSlot2Rank);
                UpdateSlotButtonSummary(2, TxtSlot3Percent, TxtSlot3Count, TxtSlot3Rank);
            }

            if (_activeSlotIndex < 0 || _activeSlotIndex >= _saveSlots.Count)
            {
                _activeSlotIndex = 0;
            }

            SelectSaveSlot(_activeSlotIndex);
            SetupReadOnlyFileWatcher();

            // Show bottom-right toast notifications for newly unlocked achievements
            foreach (AchievementItem newAch in newlyUnlockedItems)
            {
                UnlockToastWindow.ShowToast(newAch, _isTurkish, OnToastAchievementClicked);
            }
        }

        private void UpdateSlotButtonSummary(int idx, TextBlock txtPercent, TextBlock txtCount, TextBlock txtRank)
        {
            SaveSlotData slot = _saveSlots[idx];
            if (slot.Exists)
            {
                if (_isTurkish)
                {
                    txtPercent.Text = string.Format("%{0:0.0}", slot.CompletionPercent);
                    txtCount.Text = string.Format("{0} / {1} Sır", slot.UnlockedCount, slot.TotalAchievements);
                }
                else
                {
                    txtPercent.Text = string.Format(CultureInfo.InvariantCulture, "{0:0.0}%", slot.CompletionPercent);
                    txtCount.Text = string.Format("{0} / {1} Secrets", slot.UnlockedCount, slot.TotalAchievements);
                }
                txtRank.Text = slot.GetRankTitle(_isTurkish);
            }
            else
            {
                txtPercent.Text = "-";
                txtCount.Text = _isTurkish ? "Kayıt Yok" : "No Save";
                txtRank.Text = _isTurkish ? "Dosya Bulunamadı" : "File Not Found";
            }
        }

        private void SelectSaveSlot(int slotIndex)
        {
            if (_saveSlots == null || slotIndex < 0 || slotIndex >= _saveSlots.Count) return;

            _activeSlotIndex = slotIndex;
            SaveSlotData active = _saveSlots[slotIndex];

            HighlightSlotButton(BtnSlot1, slotIndex == 0);
            HighlightSlotButton(BtnSlot2, slotIndex == 1);
            HighlightSlotButton(BtnSlot3, slotIndex == 2);

            foreach (AchievementItem ach in _allAchievements)
            {
                ach.IsUnlocked = active.Exists && active.UnlockedAchievementIds.Contains(ach.Id);
                ach.IsRecentlyUnlocked = _sessionUnlockedIds.Contains(ach.Id);
            }

            // Update 34-Character Filter ComboBox with live completion counts
            UpdateCharacterFilterItems();

            // Update Milestone unlocked counter
            int unlockedMilestones = _milestoneAchievements.Count(m => m.IsUnlocked);
            int recentCount = _sessionUnlockedIds.Count;
            TxtMilestonesProgress.Text = string.Format("{0} / {1}", unlockedMilestones, _milestoneAchievements.Count);

            if (_isTurkish)
            {
                TxtActiveSlotHeader.Text = string.Format("{0}. KAYIT DOSYASI", active.SlotNumber);
                TxtProgressSummary.Text = string.Format("{0} Açık / {1} Kilitli (%{2:0.0})", active.UnlockedCount, active.LockedCount, active.CompletionPercent);
            }
            else
            {
                TxtActiveSlotHeader.Text = string.Format("SAVE FILE {0}", active.SlotNumber);
                TxtProgressSummary.Text = string.Format(CultureInfo.InvariantCulture, "{0} Unlocked / {1} Locked ({2:0.0}%)", active.UnlockedCount, active.LockedCount, active.CompletionPercent);
            }

            double maxBarWidth = 360.0;
            FrameworkElement parentGrid = ProgressFillBorder.Parent as FrameworkElement;
            if (parentGrid != null && parentGrid.ActualWidth > 20)
            {
                maxBarWidth = parentGrid.ActualWidth - 4;
            }
            ProgressFillBorder.Width = Math.Max(4.0, (active.CompletionPercent / 100.0) * maxBarWidth);

            if (active.Exists)
            {
                if (_isTurkish)
                {
                    TxtSaveFilePath.Text = string.Format("Salt Okunur Kaynak [{0}]: {1} (Son Kayıt: {2:dd.MM.yyyy HH:mm:ss})",
                        active.SourceName, active.FilePath, active.LastModified);
                    TxtFooterStatus.Text = string.Format("Save Dosyası {0} salt okunur (Read-Only) modda yüklendi • {1}/641 başarım açık.",
                        active.SlotNumber, active.UnlockedCount);
                }
                else
                {
                    TxtSaveFilePath.Text = string.Format("Read-Only Source [{0}]: {1} (Last Saved: {2:yyyy-MM-dd HH:mm:ss})",
                        active.SourceName, active.FilePath, active.LastModified);
                    TxtFooterStatus.Text = string.Format("Save File {0} loaded in strictly Read-Only mode • {1}/641 achievements unlocked.",
                        active.SlotNumber, active.UnlockedCount);
                }
            }
            else
            {
                TxtSaveFilePath.Text = _isTurkish
                    ? "Kayıt dosyası otomatik bulunamadı. Üst bardaki 'Save Seç' butonuyla manuel seçebilirsiniz."
                    : "Save file not found automatically. Use 'Open Save' in the top bar to select manually.";
                TxtFooterStatus.Text = _isTurkish ? "Kayıt dosyası bulunamadı." : "Save file not found.";
            }

            // Live Counters
            TxtStatMomKills.Text = active.MomKills.ToString("N0");
            TxtStatDeaths.Text = active.Deaths.ToString("N0");
            TxtStatStreak.Text = active.WinStreak.ToString("N0");
            TxtStatEden.Text = active.EdenTokens.ToString("N0");

            // Filter button counts
            if (_isTurkish)
            {
                BtnFilterAll.Content = string.Format("Tümü ({0})", _allAchievements.Count);
                BtnFilterUnlocked.Content = string.Format("✓ Açık ({0})", active.UnlockedCount);
                BtnFilterLocked.Content = string.Format("🔒 Kilitli ({0})", active.LockedCount);
                BtnFilterMilestones.Content = string.Format("🏆 Kilometre ({0}/{1})", unlockedMilestones, _milestoneAchievements.Count);
                BtnFilterRecent.Content = string.Format("🔥 Son Açılan ({0})", recentCount);
            }
            else
            {
                BtnFilterAll.Content = string.Format("All ({0})", _allAchievements.Count);
                BtnFilterUnlocked.Content = string.Format("✓ Unlocked ({0})", active.UnlockedCount);
                BtnFilterLocked.Content = string.Format("🔒 Locked ({0})", active.LockedCount);
                BtnFilterMilestones.Content = string.Format("🏆 Milestones ({0}/{1})", unlockedMilestones, _milestoneAchievements.Count);
                BtnFilterRecent.Content = string.Format("🔥 Recent ({0})", recentCount);
            }
            UpdateStatusFilterButtonStyles();

            if (_isInitialized)
            {
                ApplyFilters();
                AchievementItem sel = LstAchievements.SelectedItem as AchievementItem;
                if (sel != null)
                {
                    UpdateDetailPanel(sel);
                }
            }
        }

        private void HighlightSlotButton(Button btn, bool isSelected)
        {
            if (isSelected)
            {
                btn.Background = new SolidColorBrush(Color.FromRgb(34, 39, 54));
                btn.BorderBrush = new SolidColorBrush(Color.FromRgb(229, 184, 92));
            }
            else
            {
                btn.Background = new SolidColorBrush(Color.FromRgb(24, 27, 36));
                btn.BorderBrush = new SolidColorBrush(Color.FromRgb(42, 47, 64));
            }
        }

        private void UpdateCharacterFilterItems()
        {
            if (CmbCharacterFilter == null) return;

            _isUpdatingCharacterCombo = true;
            try
            {
                int prevSelected = CmbCharacterFilter.SelectedIndex;
                if (prevSelected < 0) prevSelected = 0;

                CmbCharacterFilter.Items.Clear();
                CmbCharacterFilter.Items.Add(new ComboBoxItem
                {
                    Content = _isTurkish ? "Tüm Karakterler (34)" : "All Characters (34)",
                    Tag = null
                });

                foreach (string charName in PlayableCharacters)
                {
                    int totalForChar = _allAchievements.Count(a => a.AssociatedCharacter == charName);
                    int unlockedForChar = _allAchievements.Count(a => a.AssociatedCharacter == charName && a.IsUnlocked);
                    string checkMark = (totalForChar > 0 && unlockedForChar == totalForChar) ? " ✓" : "";
                    string label = string.Format("{0} ({1}/{2}{3})", charName, unlockedForChar, totalForChar, checkMark);

                    CmbCharacterFilter.Items.Add(new ComboBoxItem
                    {
                        Content = label,
                        Tag = charName
                    });
                }

                if (prevSelected >= 0 && prevSelected < CmbCharacterFilter.Items.Count)
                {
                    CmbCharacterFilter.SelectedIndex = prevSelected;
                }
                else
                {
                    CmbCharacterFilter.SelectedIndex = 0;
                }
            }
            finally
            {
                _isUpdatingCharacterCombo = false;
            }
        }

        private void UpdateStatusFilterButtonStyles()
        {
            SetFilterBtnHighlight(BtnFilterAll, _statusFilter == "ALL");
            SetFilterBtnHighlight(BtnFilterUnlocked, _statusFilter == "UNLOCKED");
            SetFilterBtnHighlight(BtnFilterLocked, _statusFilter == "LOCKED");
            SetFilterBtnHighlight(BtnFilterMilestones, _statusFilter == "MILESTONES");
            SetFilterBtnHighlight(BtnFilterRecent, _statusFilter == "RECENT");
        }

        private void SetFilterBtnHighlight(Button btn, bool active)
        {
            if (active)
            {
                btn.Background = new SolidColorBrush(Color.FromRgb(40, 46, 64));
                btn.BorderBrush = new SolidColorBrush(Color.FromRgb(229, 184, 92));
                btn.Foreground = new SolidColorBrush(Color.FromRgb(242, 244, 248));
            }
            else
            {
                btn.Background = new SolidColorBrush(Color.FromRgb(24, 27, 36));
                btn.BorderBrush = new SolidColorBrush(Color.FromRgb(43, 48, 64));
                btn.Foreground = new SolidColorBrush(Color.FromRgb(180, 188, 208));
            }
        }

        private void ApplyFilters()
        {
            if (!_isInitialized) return;

            string query = (TxtSearch.Text ?? "").Trim().ToLowerInvariant();
            int dlcIndex = CmbDlcFilter.SelectedIndex;
            int catIndex = CmbCategoryFilter.SelectedIndex;
            string selectedCategoryKey = (catIndex > 0 && catIndex < CategoryKeysByIndex.Length)
                ? CategoryKeysByIndex[catIndex]
                : null;

            IEnumerable<AchievementItem> filtered = _allAchievements;

            // 1. Status filter
            if (_statusFilter == "UNLOCKED")
            {
                filtered = filtered.Where(a => a.IsUnlocked);
            }
            else if (_statusFilter == "LOCKED")
            {
                filtered = filtered.Where(a => !a.IsUnlocked);
            }
            else if (_statusFilter == "MILESTONES")
            {
                filtered = _milestoneAchievements;
            }
            else if (_statusFilter == "RECENT")
            {
                filtered = filtered.Where(a => _sessionUnlockedIds.Contains(a.Id));
            }

            // 1b. Character filter (34 Playable Characters)
            ComboBoxItem selectedCharItem = CmbCharacterFilter != null ? CmbCharacterFilter.SelectedItem as ComboBoxItem : null;
            string selectedCharName = selectedCharItem != null ? selectedCharItem.Tag as string : null;
            if (!string.IsNullOrEmpty(selectedCharName))
            {
                filtered = filtered.Where(a => string.Equals(a.AssociatedCharacter, selectedCharName, StringComparison.OrdinalIgnoreCase));
            }

            // 2. DLC filter
            if (dlcIndex == 1) filtered = filtered.Where(a => a.Dlc == "Rebirth");
            else if (dlcIndex == 2) filtered = filtered.Where(a => a.Dlc == "Afterbirth");
            else if (dlcIndex == 3) filtered = filtered.Where(a => a.Dlc == "Afterbirth+");
            else if (dlcIndex == 4) filtered = filtered.Where(a => a.Dlc == "Repentance");
            else if (dlcIndex == 5) filtered = filtered.Where(a => a.Dlc == "Repentance+");

            // 3. Category filter
            if (!string.IsNullOrEmpty(selectedCategoryKey))
            {
                filtered = filtered.Where(a => string.Equals(a.Category, selectedCategoryKey, StringComparison.OrdinalIgnoreCase));
            }

            // 4. Search query filter
            if (!string.IsNullOrEmpty(query))
            {
                filtered = filtered.Where(a =>
                    a.Id.ToString().Contains(query) ||
                    a.DisplayId.ToLowerInvariant().Contains(query) ||
                    (a.Name != null && a.Name.ToLowerInvariant().Contains(query)) ||
                    (a.InGameDescEn != null && a.InGameDescEn.ToLowerInvariant().Contains(query)) ||
                    (a.InGameDescTr != null && a.InGameDescTr.ToLowerInvariant().Contains(query)) ||
                    (a.UnlockByEn != null && a.UnlockByEn.ToLowerInvariant().Contains(query)) ||
                    (a.UnlockByTr != null && a.UnlockByTr.ToLowerInvariant().Contains(query))
                );
            }

            List<AchievementItem> resultList = filtered.ToList();
            AchievementItem previousSelection = LstAchievements.SelectedItem as AchievementItem;

            LstAchievements.ItemsSource = resultList;
            TxtFilterResultCount.Text = _isTurkish
                ? string.Format("{0} başarım listeleniyor", resultList.Count)
                : string.Format("Showing {0} achievements", resultList.Count);

            if (previousSelection != null && resultList.Contains(previousSelection))
            {
                LstAchievements.SelectedItem = previousSelection;
            }
            else if (resultList.Count > 0)
            {
                LstAchievements.SelectedIndex = 0;
            }
        }

        private void MilestoneCard_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            if (btn == null) return;

            AchievementItem item = btn.Tag as AchievementItem;
            if (item == null) return;

            UpdateDetailPanel(item);

            // Also select and scroll to the milestone in the main list if visible
            List<AchievementItem> currentItems = LstAchievements.ItemsSource as List<AchievementItem>;
            if (currentItems != null && currentItems.Contains(item))
            {
                LstAchievements.SelectedItem = item;
                LstAchievements.ScrollIntoView(item);
            }
        }

        private void LstAchievements_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            AchievementItem selected = LstAchievements.SelectedItem as AchievementItem;
            if (selected != null)
            {
                UpdateDetailPanel(selected);
            }
        }

        private void UpdateDetailPanel(AchievementItem item)
        {
            if (item == null) return;

            TxtDetailBanner.Text = item.SecretBannerTitle;
            ImgDetailIcon.Source = item.IconSource;
            ImgDetailIcon.Opacity = item.IsUnlocked ? 1.0 : 0.45;
            DetailLockOverlay.Visibility = item.IsUnlocked ? Visibility.Collapsed : Visibility.Visible;

            TxtDetailIdAndName.Text = string.Format("{0} — {1}", item.DisplayId, item.Name);
            TxtDetailStatus.Text = item.StatusDetailText;
            DetailStatusBorder.Background = item.StatusBadgeBrush;

            TxtDetailDlc.Text = item.Dlc;
            TxtDetailCategory.Text = item.DisplayCategory;

            if (_isTurkish)
            {
                TxtDetailDescPrimary.Text = item.InGameDescTr;
                TxtDetailDescSecondary.Text = "Orijinal Not: \"" + item.InGameDescEn + "\"";
                TxtDetailUnlockPrimary.Text = item.UnlockByTr;
                TxtDetailUnlockSecondary.Text = item.UnlockByEn;
            }
            else
            {
                TxtDetailDescPrimary.Text = item.DisplayInGameDesc;
                TxtDetailDescSecondary.Text = "TR: \"" + item.InGameDescTr + "\"";
                TxtDetailUnlockPrimary.Text = item.UnlockByEn;
                TxtDetailUnlockSecondary.Text = item.UnlockByTr;
            }
        }

        private void SetupReadOnlyFileWatcher()
        {
            try
            {
                if (_saveWatcher != null)
                {
                    _saveWatcher.EnableRaisingEvents = false;
                    _saveWatcher.Dispose();
                    _saveWatcher = null;
                }

                SaveSlotData slot = (_saveSlots != null && _saveSlots.Count > _activeSlotIndex) ? _saveSlots[_activeSlotIndex] : null;
                if (slot == null || !slot.Exists || string.IsNullOrEmpty(slot.FilePath)) return;

                string dir = Path.GetDirectoryName(slot.FilePath);
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;

                if (_watcherDebounceTimer == null)
                {
                    _watcherDebounceTimer = new DispatcherTimer();
                    _watcherDebounceTimer.Interval = TimeSpan.FromMilliseconds(800);
                    _watcherDebounceTimer.Tick += (s, args) =>
                    {
                        _watcherDebounceTimer.Stop();
                        LoadAndApplySaveFiles();
                    };
                }

                _saveWatcher = new FileSystemWatcher(dir, "*.dat");
                _saveWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName;
                _saveWatcher.Changed += OnSaveDirectoryChanged;
                _saveWatcher.Created += OnSaveDirectoryChanged;
                _saveWatcher.EnableRaisingEvents = true;
            }
            catch
            {
            }
        }

        private void OnSaveDirectoryChanged(object sender, FileSystemEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_watcherDebounceTimer != null)
                {
                    _watcherDebounceTimer.Stop();
                    _watcherDebounceTimer.Start();
                }
            }));
        }

        // ==================== CUSTOM TITLE BAR & BUTTON HANDLERS ====================

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ToggleMaximizeRestore();
                return;
            }
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                try
                {
                    DragMove();
                }
                catch { }
            }
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            ToggleMaximizeRestore();
        }

        private void ToggleMaximizeRestore()
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
                TxtMaximizeIcon.Text = "▢";
                OuterWindowBorder.CornerRadius = new CornerRadius(6);
            }
            else
            {
                WindowState = WindowState.Maximized;
                TxtMaximizeIcon.Text = "❐";
                OuterWindowBorder.CornerRadius = new CornerRadius(0);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BtnRefreshSave_Click(object sender, RoutedEventArgs e)
        {
            LoadAndApplySaveFiles();
            TxtFooterStatus.Text = _isTurkish
                ? string.Format("Kayıt dosyası salt okunur modda yeniden okundu ({0:HH:mm:ss}).", DateTime.Now)
                : string.Format("Save file reloaded in Read-Only mode ({0:HH:mm:ss}).", DateTime.Now);
        }

        private void BtnSelectSaveFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Title = _isTurkish
                ? "The Binding of Isaac: Repentance+ Save Dosyası Seç (Salt Okunur)"
                : "Select The Binding of Isaac: Repentance+ Save File (Read-Only)";
            dlg.Filter = "Isaac Save Files (*.dat)|*persistentgamedata*.dat;*.dat|All Files (*.*)|*.*";
            dlg.ReadOnlyChecked = true;
            dlg.ShowReadOnly = true;

            if (_saveSlots.Count > _activeSlotIndex && _saveSlots[_activeSlotIndex].Exists)
            {
                string currentDir = Path.GetDirectoryName(_saveSlots[_activeSlotIndex].FilePath);
                if (Directory.Exists(currentDir))
                {
                    dlg.InitialDirectory = currentDir;
                }
            }

            if (dlg.ShowDialog(this) == true)
            {
                SaveSlotData customSlot = ReadOnlySaveReader.ReadSaveFileReadOnly(
                    dlg.FileName,
                    _activeSlotIndex + 1,
                    _isTurkish ? "Manuel Seçilen (Salt Okunur)" : "Manual Selection (Read-Only)");

                _saveSlots[_activeSlotIndex] = customSlot;
                if (_activeSlotIndex == 0) UpdateSlotButtonSummary(0, TxtSlot1Percent, TxtSlot1Count, TxtSlot1Rank);
                if (_activeSlotIndex == 1) UpdateSlotButtonSummary(1, TxtSlot2Percent, TxtSlot2Count, TxtSlot2Rank);
                if (_activeSlotIndex == 2) UpdateSlotButtonSummary(2, TxtSlot3Percent, TxtSlot3Count, TxtSlot3Rank);

                SelectSaveSlot(_activeSlotIndex);
            }
        }

        private void BtnSlot1_Click(object sender, RoutedEventArgs e)
        {
            SelectSaveSlot(0);
        }

        private void BtnSlot2_Click(object sender, RoutedEventArgs e)
        {
            SelectSaveSlot(1);
        }

        private void BtnSlot3_Click(object sender, RoutedEventArgs e)
        {
            SelectSaveSlot(2);
        }

        private void BtnFilterAll_Click(object sender, RoutedEventArgs e)
        {
            _statusFilter = "ALL";
            UpdateStatusFilterButtonStyles();
            ApplyFilters();
        }

        private void BtnFilterUnlocked_Click(object sender, RoutedEventArgs e)
        {
            _statusFilter = "UNLOCKED";
            UpdateStatusFilterButtonStyles();
            ApplyFilters();
        }

        private void BtnFilterLocked_Click(object sender, RoutedEventArgs e)
        {
            _statusFilter = "LOCKED";
            UpdateStatusFilterButtonStyles();
            ApplyFilters();
        }

        private void BtnFilterMilestones_Click(object sender, RoutedEventArgs e)
        {
            _statusFilter = "MILESTONES";
            UpdateStatusFilterButtonStyles();
            ApplyFilters();
        }

        private void BtnFilterRecent_Click(object sender, RoutedEventArgs e)
        {
            _statusFilter = "RECENT";
            UpdateStatusFilterButtonStyles();
            ApplyFilters();
        }

        private void BtnTestToast_Click(object sender, RoutedEventArgs e)
        {
            AchievementItem target = LstAchievements.SelectedItem as AchievementItem;
            if (target == null && _allAchievements.Count > 0)
            {
                target = _allAchievements.FirstOrDefault(a => a.Id == 637) ?? _allAchievements[0];
            }
            if (target == null) return;

            _sessionUnlockedIds.Add(target.Id);
            target.IsRecentlyUnlocked = true;

            int recentCount = _sessionUnlockedIds.Count;
            BtnFilterRecent.Content = _isTurkish
                ? string.Format("🔥 Son Açılan ({0})", recentCount)
                : string.Format("🔥 Recent ({0})", recentCount);

            UnlockToastWindow.ShowToast(target, _isTurkish, OnToastAchievementClicked);
        }

        private void OnToastAchievementClicked(AchievementItem item)
        {
            if (item == null) return;
            try
            {
                if (WindowState == WindowState.Minimized)
                {
                    WindowState = WindowState.Normal;
                }
                Activate();
                UpdateDetailPanel(item);

                List<AchievementItem> currentItems = LstAchievements.ItemsSource as List<AchievementItem>;
                if (currentItems != null && currentItems.Contains(item))
                {
                    LstAchievements.SelectedItem = item;
                    LstAchievements.ScrollIntoView(item);
                }
            }
            catch { }
        }

        private void CmbCharacterFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingCharacterCombo) return;
            ApplyFilters();
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TxtSearchPlaceholder != null)
            {
                TxtSearchPlaceholder.Visibility = string.IsNullOrEmpty(TxtSearch.Text) ? Visibility.Visible : Visibility.Collapsed;
            }
            ApplyFilters();
        }

        private void CmbDlcFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void CmbCategoryFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void SaveWindowRenderPng(string filePath)
        {
            try
            {
                int w = (int)Math.Max(1, ActualWidth);
                int h = (int)Math.Max(1, ActualHeight);
                RenderTargetBitmap rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(OuterWindowBorder);
                PngBitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));
                using (FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                {
                    encoder.Save(fs);
                }
            }
            catch { }
        }
    }
}
