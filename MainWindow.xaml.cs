using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
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
        private List<SaveSlotData> _saveSlots = new List<SaveSlotData>();
        private int _activeSlotIndex = 0; // 0 = Slot 1, 1 = Slot 2, 2 = Slot 3
        private string _statusFilter = "ALL"; // ALL, UNLOCKED, LOCKED
        private FileSystemWatcher _saveWatcher;
        private DispatcherTimer _watcherDebounceTimer;
        private bool _isInitialized = false;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                LoadAchievementsCatalog();
                LoadAndApplySaveFiles();
                _isInitialized = true;
                ApplyFilters();

                if (LstAchievements.Items.Count > 0)
                {
                    LstAchievements.SelectedIndex = 0;
                }

                string[] cmdArgs = Environment.GetCommandLineArgs();
                for (int i = 1; i < cmdArgs.Length; i++)
                {
                    if (cmdArgs[i] == "--filter-locked")
                    {
                        _statusFilter = "LOCKED";
                        UpdateStatusFilterButtonStyles();
                        ApplyFilters();
                        if (LstAchievements.Items.Count > 0) LstAchievements.SelectedIndex = 0;
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
                    "Başarım verileri yüklenirken bir hata oluştu:\n" + ex.Message,
                    "Hata",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private string GetAppBaseDir()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            if (Directory.Exists(Path.Combine(baseDir, "Data")))
            {
                return baseDir;
            }
            // Fallback if run from bin\Debug or bin\Release
            DirectoryInfo parent = Directory.GetParent(baseDir);
            while (parent != null)
            {
                if (Directory.Exists(Path.Combine(parent.FullName, "Data")))
                {
                    return parent.FullName;
                }
                parent = parent.Parent;
            }
            return baseDir;
        }

        private void LoadAchievementsCatalog()
        {
            _allAchievements.Clear();
            string baseDir = GetAppBaseDir();
            string jsonPath = Path.Combine(baseDir, "Data", "achievements.json");

            if (!File.Exists(jsonPath))
            {
                throw new FileNotFoundException("achievements.json dosyası bulunamadı: " + jsonPath);
            }

            string jsonText = File.ReadAllText(jsonPath, Encoding.UTF8);
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;
            List<AchievementJsonDto> dtos = serializer.Deserialize<List<AchievementJsonDto>>(jsonText);

            foreach (AchievementJsonDto dto in dtos.OrderBy(d => d.Id))
            {
                string iconFullPath = Path.Combine(baseDir, dto.IconFile.Replace('/', '\\'));
                ImageSource bmp = LoadIconBitmapSafe(iconFullPath);

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
                    IsUnlocked = false
                };

                _allAchievements.Add(item);
            }
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

            // Update Slot 1, 2, 3 UI Cards
            if (_saveSlots.Count >= 3)
            {
                UpdateSlotButtonSummary(0, TxtSlot1Percent, TxtSlot1Count, TxtSlot1Rank);
                UpdateSlotButtonSummary(1, TxtSlot2Percent, TxtSlot2Count, TxtSlot2Rank);
                UpdateSlotButtonSummary(2, TxtSlot3Percent, TxtSlot3Count, TxtSlot3Rank);
            }

            // Pick active slot (keep current slot index if valid)
            if (_activeSlotIndex < 0 || _activeSlotIndex >= _saveSlots.Count)
            {
                _activeSlotIndex = 0;
            }

            SelectSaveSlot(_activeSlotIndex);
            SetupReadOnlyFileWatcher();
        }

        private void UpdateSlotButtonSummary(int idx, TextBlock txtPercent, TextBlock txtCount, TextBlock txtRank)
        {
            SaveSlotData slot = _saveSlots[idx];
            if (slot.Exists)
            {
                txtPercent.Text = string.Format("%{0:0.0}", slot.CompletionPercent);
                txtCount.Text = string.Format("{0} / {1} Sır", slot.UnlockedCount, slot.TotalAchievements);
                txtRank.Text = slot.RankTitle;
            }
            else
            {
                txtPercent.Text = "-";
                txtCount.Text = "Kayıt Yok";
                txtRank.Text = "Dosya Bulunamadı";
            }
        }

        private void SelectSaveSlot(int slotIndex)
        {
            if (_saveSlots == null || slotIndex < 0 || slotIndex >= _saveSlots.Count) return;

            _activeSlotIndex = slotIndex;
            SaveSlotData active = _saveSlots[slotIndex];

            // Highlight selected slot button
            HighlightSlotButton(BtnSlot1, slotIndex == 0);
            HighlightSlotButton(BtnSlot2, slotIndex == 1);
            HighlightSlotButton(BtnSlot3, slotIndex == 2);

            // Apply unlocked states to all 641 achievements
            foreach (AchievementItem ach in _allAchievements)
            {
                ach.IsUnlocked = active.Exists && active.UnlockedAchievementIds.Contains(ach.Id);
            }

            // Update Top Banner Progress & Live Counters
            TxtActiveSlotHeader.Text = string.Format("AKTİF KAYIT: {0}. DOSYA ({1})", active.SlotNumber, active.RankTitle.ToUpperInvariant());
            TxtProgressSummary.Text = string.Format("{0} Açık / {1} Kilitli (%{2:0.0})", active.UnlockedCount, active.LockedCount, active.CompletionPercent);

            double maxBarWidth = 360.0;
            FrameworkElement parentGrid = ProgressFillBorder.Parent as FrameworkElement;
            if (parentGrid != null && parentGrid.ActualWidth > 20)
            {
                maxBarWidth = parentGrid.ActualWidth - 4;
            }
            ProgressFillBorder.Width = Math.Max(4.0, (active.CompletionPercent / 100.0) * maxBarWidth);

            if (active.Exists)
            {
                TxtSaveFilePath.Text = string.Format("Salt Okunur Kaynak [{0}]: {1} (Son Kayıt: {2:dd.MM.yyyy HH:mm:ss})",
                    active.SourceName, active.FilePath, active.LastModified);
                TxtFooterStatus.Text = string.Format("Save Dosyası {0} salt okunur (Read-Only) modda yüklendi • {1}/641 başarım açık.",
                    active.SlotNumber, active.UnlockedCount);
            }
            else
            {
                TxtSaveFilePath.Text = "Kayıt dosyası otomatik bulunamadı. Üst bardaki 'Save Seç' butonuyla manuel seçebilirsiniz.";
                TxtFooterStatus.Text = "Kayıt dosyası bulunamadı.";
            }

            // Live Counters
            TxtStatMomKills.Text = active.MomKills.ToString("N0");
            TxtStatDeaths.Text = active.Deaths.ToString("N0");
            TxtStatStreak.Text = string.Format("{0} (En İyi: {1})", active.WinStreak, active.BestStreak);
            TxtStatDonation.Text = string.Format("{0}¢ / {1}¢", active.DonationMachineCoins, active.GreedMachineCoins);
            TxtStatEden.Text = active.EdenTokens.ToString("N0");
            TxtStatItemsChallenges.Text = string.Format("{0} / {1}", active.CollectiblesSeen, active.ChallengesCompleted);

            // Filter button counts
            BtnFilterAll.Content = string.Format("Tümü ({0})", _allAchievements.Count);
            BtnFilterUnlocked.Content = string.Format("✓ Açılanlar ({0})", active.UnlockedCount);
            BtnFilterLocked.Content = string.Format("🔒 Kilitliler ({0})", active.LockedCount);
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
                btn.Background = new SolidColorBrush(Color.FromRgb(54, 36, 28));
                btn.BorderBrush = new SolidColorBrush(Color.FromRgb(224, 174, 82));
            }
            else
            {
                btn.Background = new SolidColorBrush(Color.FromRgb(28, 20, 16));
                btn.BorderBrush = new SolidColorBrush(Color.FromRgb(74, 52, 41));
            }
        }

        private void UpdateStatusFilterButtonStyles()
        {
            SetFilterBtnHighlight(BtnFilterAll, _statusFilter == "ALL");
            SetFilterBtnHighlight(BtnFilterUnlocked, _statusFilter == "UNLOCKED");
            SetFilterBtnHighlight(BtnFilterLocked, _statusFilter == "LOCKED");
        }

        private void SetFilterBtnHighlight(Button btn, bool active)
        {
            if (active)
            {
                btn.Background = new SolidColorBrush(Color.FromRgb(115, 34, 28));
                btn.BorderBrush = new SolidColorBrush(Color.FromRgb(224, 174, 82));
                btn.Foreground = new SolidColorBrush(Color.FromRgb(255, 244, 224));
            }
            else
            {
                btn.Background = new SolidColorBrush(Color.FromRgb(37, 27, 22));
                btn.BorderBrush = new SolidColorBrush(Color.FromRgb(78, 56, 44));
                btn.Foreground = new SolidColorBrush(Color.FromRgb(214, 195, 165));
            }
        }

        private void ApplyFilters()
        {
            if (!_isInitialized) return;

            string query = (TxtSearch.Text ?? "").Trim().ToLowerInvariant();
            int dlcIndex = CmbDlcFilter.SelectedIndex;
            string selectedCategory = null;
            ComboBoxItem catItem = CmbCategoryFilter.SelectedItem as ComboBoxItem;
            if (CmbCategoryFilter.SelectedIndex > 0 && catItem != null)
            {
                selectedCategory = catItem.Content.ToString();
            }

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

            // 2. DLC filter
            if (dlcIndex == 1) filtered = filtered.Where(a => a.Dlc == "Rebirth");
            else if (dlcIndex == 2) filtered = filtered.Where(a => a.Dlc == "Afterbirth");
            else if (dlcIndex == 3) filtered = filtered.Where(a => a.Dlc == "Afterbirth+");
            else if (dlcIndex == 4) filtered = filtered.Where(a => a.Dlc == "Repentance");
            else if (dlcIndex == 5) filtered = filtered.Where(a => a.Dlc == "Repentance+");

            // 3. Category filter
            if (!string.IsNullOrEmpty(selectedCategory))
            {
                filtered = filtered.Where(a => string.Equals(a.Category, selectedCategory, StringComparison.OrdinalIgnoreCase));
            }

            // 4. Search query filter
            if (!string.IsNullOrEmpty(query))
            {
                filtered = filtered.Where(a =>
                    a.Id.ToString().Contains(query) ||
                    a.DisplayId.ToLowerInvariant().Contains(query) ||
                    (a.Name != null && a.Name.ToLowerInvariant().Contains(query)) ||
                    (a.InGameDescTr != null && a.InGameDescTr.ToLowerInvariant().Contains(query)) ||
                    (a.UnlockByTr != null && a.UnlockByTr.ToLowerInvariant().Contains(query)) ||
                    (a.UnlockByEn != null && a.UnlockByEn.ToLowerInvariant().Contains(query))
                );
            }

            List<AchievementItem> resultList = filtered.ToList();
            AchievementItem previousSelection = LstAchievements.SelectedItem as AchievementItem;

            LstAchievements.ItemsSource = resultList;
            TxtFilterResultCount.Text = string.Format("{0} başarım listeleniyor", resultList.Count);

            if (previousSelection != null && resultList.Contains(previousSelection))
            {
                LstAchievements.SelectedItem = previousSelection;
            }
            else if (resultList.Count > 0)
            {
                LstAchievements.SelectedIndex = 0;
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
            TxtDetailCategory.Text = item.Category;

            TxtDetailDescTr.Text = item.InGameDescTr;
            TxtDetailDescEn.Text = "Orijinal Not: \"" + item.InGameDescEn + "\"";
            TxtDetailUnlockTr.Text = item.UnlockByTr;
            TxtDetailUnlockEn.Text = item.UnlockByEn;
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
                // Ignore watcher setup errors
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
            TxtFooterStatus.Text = string.Format("Kayıt dosyası salt okunur modda yeniden okundu ({0:HH:mm:ss}).", DateTime.Now);
        }

        private void BtnSelectSaveFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Title = "The Binding of Isaac: Repentance+ Save Dosyası Seç (Salt Okunur)";
            dlg.Filter = "Isaac Save Dosyaları (*.dat)|*persistentgamedata*.dat;*.dat|Tüm Dosyalar (*.*)|*.*";
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
                    "Manuel Seçilen (Salt Okunur)");

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
