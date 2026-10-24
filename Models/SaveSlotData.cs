using System;
using System.Collections.Generic;

namespace IsaacRepentancePlusAchievements.Models
{
    public class SaveSlotData
    {
        public int SlotNumber { get; set; }
        public string FilePath { get; set; }
        public string SourceName { get; set; }
        public DateTime LastModified { get; set; }
        public string HeaderSignature { get; set; }
        public int FileSizeBytes { get; set; }
        public bool Exists { get; set; }

        public HashSet<int> UnlockedAchievementIds { get; set; }
        public int TotalAchievements { get; set; }
        public int UnlockedCount
        {
            get { return UnlockedAchievementIds != null ? UnlockedAchievementIds.Count : 0; }
        }
        public int LockedCount
        {
            get { return Math.Max(0, TotalAchievements - UnlockedCount); }
        }
        public double CompletionPercent
        {
            get
            {
                if (TotalAchievements <= 0) return 0.0;
                return Math.Round((UnlockedCount * 100.0) / TotalAchievements, 1);
            }
        }

        // Event Counters (Read-Only from Chunk 1)
        public int MomKills { get; set; }
        public int Deaths { get; set; }
        public int WinStreak { get; set; }
        public int BestStreak { get; set; }
        public int EdenTokens { get; set; }
        public int DonationMachineCoins { get; set; }
        public int GreedMachineCoins { get; set; }
        public int RocksDestroyed { get; set; }
        public int TintedRocksDestroyed { get; set; }
        public int PoopDestroyed { get; set; }
        public int PillsEaten { get; set; }
        public int DevilDealsTaken { get; set; }
        public int CollectiblesSeen { get; set; }
        public int ChallengesCompleted { get; set; }

        public string GetRankTitle(bool isTurkish)
        {
            if (isTurkish)
            {
                if (!Exists) return "Kayıt Bulunamadı";
                if (UnlockedCount >= 637) return "DEAD GOD!";
                if (UnlockedCount >= 500) return "The Beast Avcısı";
                if (UnlockedCount >= 350) return "Mother Katili";
                if (UnlockedCount >= 200) return "Alt Path Gezgini";
                if (UnlockedCount >= 100) return "It Lives! Fatihi";
                if (UnlockedCount >= 25) return "Mahzen Maceracısı";
                if (UnlockedCount > 0) return "Yeni Başlangıç";
                return "Boş Kayıt (0 Sır)";
            }
            else
            {
                if (!Exists) return "Save Not Found";
                if (UnlockedCount >= 637) return "DEAD GOD!";
                if (UnlockedCount >= 500) return "The Beast Hunter";
                if (UnlockedCount >= 350) return "Mother Slayer";
                if (UnlockedCount >= 200) return "Alt Path Explorer";
                if (UnlockedCount >= 100) return "It Lives! Conqueror";
                if (UnlockedCount >= 25) return "Basement Adventurer";
                if (UnlockedCount > 0) return "Fresh Start";
                return "Empty Save (0 Secrets)";
            }
        }

        public SaveSlotData()
        {
            UnlockedAchievementIds = new HashSet<int>();
            TotalAchievements = 641;
        }
    }
}
