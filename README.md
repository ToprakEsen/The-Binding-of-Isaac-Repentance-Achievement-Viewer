<div align="center">

# 🩸 The Binding of Isaac: Repentance+ — Achievement & Secret Viewer

### 🌐 Choose Language / Dil Seçimi

[![English](https://img.shields.io/badge/🇬🇧_Language-English_(Active)-b83228?style=for-the-badge)](README.md)
[![Türkçe](https://img.shields.io/badge/🇹🇷_Dil-Türkçe_(Tıkla)-2b1d16?style=for-the-badge)](README.tr.md)

<p>
  <a href="README.md"><b>🇬🇧 English</b></a> •
  <a href="README.tr.md"><b>🇹🇷 Türkçe</b></a>
</p>

---

![Application Screenshot](Assets/screenshot.png)

**A strictly Read-Only Achievement & Save File Viewer for *The Binding of Isaac: Repentance+*, built with .NET (WPF) and styled after the game's dark cellar & pinned parchment aesthetic.**

</div>

---

## ✨ Key Features

- 🔒 **100% Strictly Read-Only Save Protection:**
  - Opens save files exclusively at the OS kernel level using `FileMode.Open`, `FileAccess.Read`, and `FileShare.ReadWrite`.
  - **Cannot modify, overwrite, or corrupt** your save files under any circumstances, and never locks the file while the game is running.
- 📂 **Automatic Save Detection & Real-Time Sync:**
  - Automatically scans and loads all 3 save slots (`Slot 1`, `Slot 2`, `Slot 3`) from Steam Cloud (`Steam\userdata\<id>\250900\remote\rep+persistentgamedata1..3.dat`) and `Documents\My Games\Binding of Isaac Repentance+`.
  - Uses a live `FileSystemWatcher` to automatically update your unlocked achievements in real time while you play.
- 🏆 **All 641 Official Achievements & Game Artwork:**
  - Covers every DLC era: *Rebirth (1–178)*, *Afterbirth (179–276)*, *Afterbirth+ (277–403)*, *Repentance (404–637)*, and *Repentance+ (638–641)* (including Online & *Item Descriptions* achievements).
  - Uses the official pixel-art achievement icons from the game.
  - Displays a custom **padlock & chain overlay** directly on top of locked achievement icons.
- 📜 **Detailed Unlock Guide ("SECRET FOUND!" Parchment Panel):**
  - Displays the in-game quote and clear **How to Unlock** conditions (in both English and Turkish) on a pinned parchment note inspired by the in-game secret screen.
- 📊 **Live In-Game Save Statistics:**
  - Displays real-time counters from your save file: *Mom Kills, Deaths, Win Streak, Best Streak, Donation Machine Coins, Greed Machine Coins, Eden Tokens, Collectibles Seen,* and *Challenges Completed*.
- 🎨 **Custom Isaac-Themed UI:**
  - Custom borderless title bar (drag, minimize, maximize, close), custom dark cellar & aged-bronze scrollbar, and instant search/filter by Unlock Status, DLC, Category, or `#ID`.

---

## 🚀 Getting Started

### Option 1: Direct Run (Pre-built Executable)
1. Clone or download this repository.
2. Run `IsaacRepentancePlusAchievements.exe` directly (make sure the `Data` and `Assets` folders remain in the same directory as the `.exe`).

### Option 2: Build from Source
You can compile the project on any Windows PC using the built-in `.NET Framework MSBuild` without installing external dependencies:

```powershell
& "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe" "IsaacRepentancePlusAchievements.csproj" /p:Configuration=Release
```

---

## 📁 Project Structure

| File / Folder | Description |
| :--- | :--- |
| `MainWindow.xaml` / `.cs` | Custom borderless title bar, cellar/parchment UI layout, search & filtering |
| `App.xaml` | Custom Isaac-themed `ScrollBar` and `ComboBox` control templates |
| `Services/ReadOnlySaveReader.cs` | Strictly read-only (`FileAccess.Read`) binary parser for `ISAACNGSAVE09R` `.dat` files |
| `Models/` | `AchievementItem` and `SaveSlotData` data models |
| `Data/achievements.json` | Complete database of all 641 achievements with EN & TR descriptions and unlock guides |
| `Assets/Achievements/` | Official pixel-art icons (`1.jpg` – `641.jpg`) for all 641 achievements |
