using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Win32;
using IsaacRepentancePlusAchievements.Models;

namespace IsaacRepentancePlusAchievements.Services
{
    /// <summary>
    /// Strictly Read-Only Save Reader for The Binding of Isaac: Repentance+.
    /// Uses FileAccess.Read and FileShare.ReadWrite exclusively.
    /// Contains zero write or modification methods.
    /// </summary>
    public static class ReadOnlySaveReader
    {
        public static List<SaveSlotData> AutoDetectAndReadAllSlots()
        {
            List<SaveSlotData> slots = new List<SaveSlotData>();
            for (int slot = 1; slot <= 3; slot++)
            {
                string sourceLabel;
                string bestPath = FindBestSaveFileForSlot(slot, out sourceLabel);
                if (!string.IsNullOrEmpty(bestPath) && File.Exists(bestPath))
                {
                    SaveSlotData data = ReadSaveFileReadOnly(bestPath, slot, sourceLabel);
                    slots.Add(data);
                }
                else
                {
                    slots.Add(new SaveSlotData
                    {
                        SlotNumber = slot,
                        FilePath = "Kayıt dosyası bulunamadı",
                        SourceName = "Yok",
                        Exists = false
                    });
                }
            }
            return slots;
        }

        public static string FindBestSaveFileForSlot(int slotNumber, out string sourceLabel)
        {
            sourceLabel = "Bulunamadı";
            List<CandidateFile> candidates = new List<CandidateFile>();

            // 1. Check Steam Cloud userdata folders (AppID 250900)
            foreach (string steamRoot in GetSteamRootDirectories())
            {
                string userDataDir = Path.Combine(steamRoot, "userdata");
                if (Directory.Exists(userDataDir))
                {
                    try
                    {
                        string[] userDirs = Directory.GetDirectories(userDataDir);
                        foreach (string uDir in userDirs)
                        {
                            string remoteDir = Path.Combine(uDir, "250900", "remote");
                            if (Directory.Exists(remoteDir))
                            {
                                string repPlusFile = Path.Combine(remoteDir, string.Format("rep+persistentgamedata{0}.dat", slotNumber));
                                if (File.Exists(repPlusFile))
                                {
                                    candidates.Add(new CandidateFile(repPlusFile, "Steam Cloud (Repentance+)", 100));
                                }

                                string repFile = Path.Combine(remoteDir, string.Format("rep_persistentgamedata{0}.dat", slotNumber));
                                if (File.Exists(repFile))
                                {
                                    candidates.Add(new CandidateFile(repFile, "Steam Cloud (Repentance)", 80));
                                }

                                string baseFile = Path.Combine(remoteDir, string.Format("persistentgamedata{0}.dat", slotNumber));
                                if (File.Exists(baseFile))
                                {
                                    candidates.Add(new CandidateFile(baseFile, "Steam Cloud (Rebirth)", 60));
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Ignore permission issues on unrelated directories
                    }
                }
            }

            // 2. Check Documents\My Games\Binding of Isaac Repentance+
            string docsDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (!string.IsNullOrEmpty(docsDir))
            {
                string repPlusDocs = Path.Combine(docsDir, "My Games", "Binding of Isaac Repentance+");
                if (Directory.Exists(repPlusDocs))
                {
                    string directFile = Path.Combine(repPlusDocs, string.Format("rep+persistentgamedata{0}.dat", slotNumber));
                    if (File.Exists(directFile))
                    {
                        candidates.Add(new CandidateFile(directFile, "Belgelerim (Repentance+)", 95));
                    }

                    string backupsDir = Path.Combine(repPlusDocs, "save_backups");
                    if (Directory.Exists(backupsDir))
                    {
                        try
                        {
                            string pattern = string.Format("*.rep+persistentgamedata{0}.dat", slotNumber);
                            string[] backups = Directory.GetFiles(backupsDir, pattern);
                            foreach (string bFile in backups)
                            {
                                candidates.Add(new CandidateFile(bFile, "Belgelerim (Yedek / Backup)", 70));
                            }
                        }
                        catch
                        {
                        }
                    }
                }

                string repDocs = Path.Combine(docsDir, "My Games", "Binding of Isaac Repentance");
                if (Directory.Exists(repDocs))
                {
                    string directRep = Path.Combine(repDocs, string.Format("persistentgamedata{0}.dat", slotNumber));
                    if (File.Exists(directRep))
                    {
                        candidates.Add(new CandidateFile(directRep, "Belgelerim (Repentance)", 75));
                    }
                }
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            // Sort by Priority group first (prefer active Repentance+ over old backups), then by LastWriteTime descending
            CandidateFile best = candidates
                .OrderByDescending(c => c.Priority >= 90 ? 1 : 0)
                .ThenByDescending(c => c.LastWriteUtc)
                .ThenByDescending(c => c.Priority)
                .First();

            sourceLabel = best.SourceLabel;
            return best.FilePath;
        }

        /// <summary>
        /// Opens the save file strictly in Read-Only mode (FileAccess.Read, FileShare.ReadWrite)
        /// and parses achievements + game statistics in memory.
        /// </summary>
        public static SaveSlotData ReadSaveFileReadOnly(string filePath, int slotNumber, string sourceLabel)
        {
            SaveSlotData result = new SaveSlotData
            {
                SlotNumber = slotNumber,
                FilePath = filePath,
                SourceName = sourceLabel,
                Exists = false
            };

            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                return result;
            }

            byte[] bytes;
            FileInfo fi = new FileInfo(filePath);
            result.LastModified = fi.LastWriteTime;
            result.FileSizeBytes = (int)fi.Length;

            // STRICT READ-ONLY ACCESS: FileMode.Open, FileAccess.Read, FileShare.ReadWrite
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                bytes = new byte[fs.Length];
                int totalRead = 0;
                while (totalRead < bytes.Length)
                {
                    int read = fs.Read(bytes, totalRead, bytes.Length - totalRead);
                    if (read <= 0) break;
                    totalRead += read;
                }
            }

            if (bytes.Length < 32)
            {
                return result;
            }

            result.Exists = true;
            result.HeaderSignature = Encoding.ASCII.GetString(bytes, 0, 16).Trim('\0', ' ');

            // Chunk parsing starts at offset 0x14 (20 bytes: 16-byte header + 4-byte checksum)
            int pos = 0x14;
            int[] elemSizes = new int[] { 0, 1, 4, 4, 1, 1, 1, 1, 4, 4, 1 };

            for (int chunkIdx = 0; chunkIdx < 10; chunkIdx++)
            {
                if (pos + 12 > bytes.Length) break;

                int chunkType = BitConverter.ToInt32(bytes, pos);
                int chunkLen = BitConverter.ToInt32(bytes, pos + 4);
                int chunkCount = BitConverter.ToInt32(bytes, pos + 8);
                pos += 12;

                if (chunkType < 1 || chunkType > 10 || chunkCount < 0)
                {
                    break;
                }

                int dataByteLen = chunkCount * elemSizes[chunkType];
                if (pos + dataByteLen > bytes.Length)
                {
                    break;
                }

                if (chunkType == 1)
                {
                    // Achievements chunk: index 0 is unused padding; 1..641 are achievement IDs
                    int maxAchId = Math.Min(641, chunkCount - 1);
                    for (int achId = 1; achId <= maxAchId; achId++)
                    {
                        if (bytes[pos + achId] != 0)
                        {
                            result.UnlockedAchievementIds.Add(achId);
                        }
                    }
                }
                else if (chunkType == 2)
                {
                    // Event Counters (int32 array)
                    result.MomKills = GetCounterSafe(bytes, pos, chunkCount, 1);
                    result.RocksDestroyed = GetCounterSafe(bytes, pos, chunkCount, 2);
                    result.TintedRocksDestroyed = GetCounterSafe(bytes, pos, chunkCount, 3);
                    result.PoopDestroyed = GetCounterSafe(bytes, pos, chunkCount, 5);
                    result.PillsEaten = GetCounterSafe(bytes, pos, chunkCount, 6);
                    result.Deaths = GetCounterSafe(bytes, pos, chunkCount, 9);
                    result.DevilDealsTaken = GetCounterSafe(bytes, pos, chunkCount, 10);
                    result.DonationMachineCoins = GetCounterSafe(bytes, pos, chunkCount, 20);
                    result.EdenTokens = GetCounterSafe(bytes, pos, chunkCount, 21);
                    result.WinStreak = GetCounterSafe(bytes, pos, chunkCount, 23);
                    result.BestStreak = GetCounterSafe(bytes, pos, chunkCount, 24);
                    result.GreedMachineCoins = GetCounterSafe(bytes, pos, chunkCount, 111);
                }
                else if (chunkType == 4)
                {
                    // Collectibles seen
                    int seen = 0;
                    for (int i = 1; i < chunkCount; i++)
                    {
                        if (bytes[pos + i] != 0) seen++;
                    }
                    result.CollectiblesSeen = seen;
                }
                else if (chunkType == 7)
                {
                    // Challenges completed
                    int done = 0;
                    for (int i = 1; i < chunkCount; i++)
                    {
                        if (bytes[pos + i] != 0) done++;
                    }
                    result.ChallengesCompleted = done;
                }

                pos += dataByteLen;
            }

            return result;
        }

        private static int GetCounterSafe(byte[] bytes, int basePos, int count, int index)
        {
            if (index < 0 || index >= count) return 0;
            int offset = basePos + (index * 4);
            if (offset + 4 > bytes.Length) return 0;
            return BitConverter.ToInt32(bytes, offset);
        }

        private static List<string> GetSteamRootDirectories()
        {
            HashSet<string> dirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam", false))
                {
                    if (key != null)
                    {
                        object val = key.GetValue("SteamPath");
                        if (val != null)
                        {
                            string p = val.ToString().Replace('/', '\\');
                            if (Directory.Exists(p)) dirs.Add(p);
                        }
                    }
                }
            }
            catch { }

            try
            {
                using (RegistryKey key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Valve\Steam", false))
                {
                    if (key != null)
                    {
                        object val = key.GetValue("InstallPath");
                        if (val != null)
                        {
                            string p = val.ToString().Replace('/', '\\');
                            if (Directory.Exists(p)) dirs.Add(p);
                        }
                    }
                }
            }
            catch { }

            string[] commonPaths = new string[]
            {
                @"C:\Program Files (x86)\Steam",
                @"C:\Program Files\Steam",
                @"D:\Steam",
                @"E:\Steam"
            };
            foreach (string cp in commonPaths)
            {
                if (Directory.Exists(cp)) dirs.Add(cp);
            }

            return dirs.ToList();
        }

        private class CandidateFile
        {
            public string FilePath { get; set; }
            public string SourceLabel { get; set; }
            public int Priority { get; set; }
            public DateTime LastWriteUtc { get; set; }

            public CandidateFile(string path, string label, int priority)
            {
                FilePath = path;
                SourceLabel = label;
                Priority = priority;
                try
                {
                    LastWriteUtc = File.GetLastWriteTimeUtc(path);
                }
                catch
                {
                    LastWriteUtc = DateTime.MinValue;
                }
            }
        }
    }
}
