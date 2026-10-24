# The Binding of Isaac: Repentance+ — Başarım & Sır Günlüğü (Achievement Viewer)

![Uygulama Ekran Görüntüsü](Assets/screenshot.png)

**The Binding of Isaac: Repentance+** için geliştirilmiş, `.NET (WPF)` tabanlı, oyunun karanlık mahzen ve parşömen temasına sadık, **%100 Salt Okunur (Read-Only)** başarım ve kayıt dosyası görüntüleme uygulaması.

---

## ✨ Özellikler (Features)

- **🔒 %100 Salt Okunur (Read-Only) Koruma:**
  - Kayıt dosyalarını işletim sistemi düzeyinde yalnızca `FileAccess.Read` ve `FileShare.ReadWrite` izinleriyle açar.
  - Kayıt dosyalarını **kesinlikle düzenlemez, üzerine yazmaz** ve oyun açıkken oyunun kayıt almasını engellemez.
- **📂 Otomatik Save Algılama & Canlı Senkronizasyon:**
  - Steam Cloud (`Steam\userdata\<id>\250900\remote\rep+persistentgamedata1..3.dat`) ve `Belgelerim\My Games\Binding of Isaac Repentance+` klasörlerindeki 3 kayıt yuvasını (`Slot 1`, `Slot 2`, `Slot 3`) otomatik bulur.
  - Siz oyunu oynarken yeni bir başarım açtığınızda `FileSystemWatcher` ile anında canlı olarak güncellenir.
- **🏆 641 Resmi Başarım & Oyun İçi Görseller:**
  - *Rebirth (1–178)*, *Afterbirth (179–276)*, *Afterbirth+ (277–403)*, *Repentance (404–637)* ve *Repentance+ (638–641)* dahil tüm **641 başarımın** orijinal piksel-art görsellerini içerir.
  - Kilitli olan başarımların görselleri üzerinde özel **kilit ve zincir simgesi katmanı** bulunur.
- **📜 Türkçe & İngilizce Rehber ("Nasıl Açılır?"):**
  - Her başarımın oyun içi açıklaması ve nasıl açılacağını anlatan detaylı Türkçe/İngilizce koşul rehberi sağdaki *"SECRET FOUND!"* parşömen kağıdında gösterilir.
- **🎨 Özel Isaac Temalı Arayüz & Başlık Çubuğu:**
  - Kendine özel açma/kapama/küçültme barı, özel mahzen/bronz kaydırma çubuğu (ScrollBar) ve kategori/DLC filtreleri.

---

## 🚀 Kurulum ve Çalıştırma (How to Run)

### 1. Doğrudan Çalıştırma (Hazır `.exe`)
1. Bu depoyu indirin (veya **Releases** kısmından `.zip` paketini indirin).
2. `IsaacRepentancePlusAchievements.exe` dosyasını çalıştırın (`Data` ve `Assets` klasörleri `.exe` ile aynı dizinde bulunmalıdır).

### 2. Kaynak Koddan Derleme (Build from Source)
Windows üzerinde harici bir kurulum gerektirmeden yerleşik `.NET Framework MSBuild` ile derleyebilirsiniz:

```powershell
& "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\MSBuild.exe" "IsaacRepentancePlusAchievements.csproj" /p:Configuration=Release
```

---

## 📁 Proje Yapısı

- `MainWindow.xaml` / `MainWindow.xaml.cs` — Özel başlık çubuğu, mahzen/parşömen arayüzü ve filtreleme mantığı
- `App.xaml` — Özel Isaac temalı `ScrollBar` ve `ComboBox` şablonları
- `Services/ReadOnlySaveReader.cs` — Salt okunur (`FileAccess.Read`) Steam Cloud & yerel kayıt dosyası okuyucu
- `Models/` — `AchievementItem` ve `SaveSlotData` veri modelleri
- `Data/achievements.json` — 641 başarımın Türkçe ve İngilizce açıklama/kilit açma veritabanı
- `Assets/Achievements/` — 641 başarımın orijinal oyun içi ikonları
