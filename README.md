# Kargo Simulator

Unity 6 ile geliştirilen, kargo/kurye simülasyon oyunu.

**Durum:** Aşama 1 (proje + çekirdek) tamamlandı. Henüz oynanabilir değil.

---

## Kurulum

| Bileşen | Sürüm |
|---|---|
| Unity | **6000.3.25f1** (LTS) |
| Render Pipeline | URP 17.3.0 (Forward) |
| Script | C# / .NET Standard 2.1 |
| Harici paket | **Yok** — Timeline, Cinemachine, Input System, DOT dahil hiçbir şey kullanılmıyor |

### Adımlar

1. Unity Hub'da **+ → Open → add project from disk** ile `D:\KargoSimulator` seç.
2. `6000.3.25f1` sürümüyle aç.
3. Sahneyi aç: `Assets/Scenes/Game.unity`
4. **Play**'e bas.

İlk açılışta `Library` klasörü yeniden oluşacağı için birkaç dakika sürebilir.

---

## Projeyi Kurma Aracı

Elle yapılması gereken onlarca ayar (URP pipeline'i oluşturmak, katmanları tanımlamak, fizik matrisini kurmak, sahneyi kurmak) tek bir editor aracıyla yapılır:

**Unity menüsü → `Kargo`**

| Menü | Ne yapar |
|---|---|
| `1. Tum Kurulumu Calistir` | Her şeyi sırayla yapar (idempotent, tekrar çalıştırılabilir) |
| `2. Klasorleri Hazirla` | Klasör iskeletini oluşturur |
| `3. Render Pipeline (URP) Kur` | URP asset + renderer oluşturur, **global olarak atar** |
| `4. Katmanlari Kur` | Layer 8-13 + fizik çarpışma matrisi |
| `5. Fizik Ayarlari` | Yerçekimi, solver, autoSyncTransforms |
| `6. Kalite Ayarlari` | 6 kalite seviyesinin tümü, varsayılan Medium |
| `7. Player Ayarlari` | Ürün bilgisi, renk uzayı, splash, Play modu |
| `8. GameConfig Olustur` | Oyun ayarları asset'i |
| `9. Game Sahnesini Kur` | Sahneyi **sıfırdan** kurar (var olanı siler, onay ister) |
| `10. Fixed Timestep 0.02` | 50 Hz fizik adımı |
| `Diagnoz: GameConfig Yukleme` | Asset yüklenemiyorsa nedenini raporlar |

> `9. Game Sahnesini Kur` mevcut sahneyi siler. Toplu kurulum
> (`1.`) sahneye dokunmaz, sadece yoksa oluşturur.

Kurulum bittikten sonra araç şunu doğrular:
`GameCompositionRoot.config` referansının kaydedilmiş sahnede gerçekten
dolu olduğunu okuyup kontrol eder. Referans boşsa hata verir — "oyun
neden açılmıyor" belirsizliği kalmaz.

### Testler

EditMode testleri `GameClock` mantığını ve sahne bütünlüğünü doğrular (14 test).

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe" `
  -runTests -batchmode -nographics `
  -projectPath "D:\KargoSimulator" `
  -testPlatform EditMode -testResults "D:\KargoSimulator\testresults.xml"
```

Unity içinden: **Window → General → Test Runner → EditMode → Run All**

Komut satırından da çalıştırılabilir:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe" `
  -batchmode -quit -nographics `
  -projectPath "D:\KargoSimulator" `
  -executeMethod KargoSim.EditorTools.ProjectSetupTool.RunAllSetupIncludingTimestep
```

---

## Mimari

```
        ┌──────────────────┐
        │  GameComposition │  ← TEK referans noktası
        │       Root       │
        └────────┬─────────┘
   ┌─────┬───────┼───────┬────────┬──────────┬─────────┐
   ▼     ▼       ▼       ▼        ▼          ▼         ▼
 Vehicle  Cargo  Delivery Economy  Mission  Save     UIManager
   └───────┴───────┴───────┴────────┴────────┴─────────┘
                 │  okur, bağımlılık kurmaz
                 ▼
          ┌──────────────┐
          │  GameEvents  │  static event bus
          └──────────────┘
```

**Kural:** Sistemler birbirini referanslamaz. Hepsi `GameEvents` üzerinden konuşur.
Bir sistem silinse bile diğerleri bozulmaz.

### Performans kararları (baştan)

| Karar | Değer |
|---|---|
| AA / MSAA | **kapalı** |
| HDR | **kapalı** |
| Depth / Opaque texture | **kapalı** (pre-pass yok) |
| Post-processing | **yok** |
| Ek ışıklar | **Per Vertex, en fazla 1** |
| Gölge | tek ışık, **1 cascade**, 60 m |
| Yerçekimi | **-25** (arcade his) |
| Fixed Timestep | **0.02** (50 Hz) |
| `autoSyncTransforms` | **kapalı** |
| Renk uzayı | Linear |
| `Update()` sayısı | **1** (yalnızca `GameCompositionRoot`) |

Bunlar `Kargo/1. Tum Kurulumu Calistir` ile uygulanır.

---

## Klasör yapısı

```
Assets/
├── Art/
│   ├── Materials/        URP/Lit düz renkli materyaller
│   ├── Models/           Blender çıktısı (boş)
│   └── Prefabs/
├── Audio/                (boş)
├── Prefabs/
├── Scenes/Game.unity     TEK sahne
├── Scripts/
│   ├── Core/             GameConfig, GameEvents, GameClock, GameCompositionRoot
│   ├── Player/           (Aşama 2)
│   ├── Vehicle/          (Aşama 2)
│   ├── Cargo/            (Aşama 3)
│   ├── Delivery/         (Aşama 4)
│   ├── Economy/          (Aşama 5)
│   ├── Mission/          (Aşama 6)
│   ├── UI/               (Aşama 6)
│   ├── Save/             (Aşama 7)
│   └── Editor/           ProjectSetupTool
├── ScriptableObjects/
│   ├── Definitions/      (Aşama 3+)
│   └── Database/GameConfig.asset
├── Settings/             URP asset + renderer
├── Tests/EditMode/       GameClock + sahne bütünlüğü testleri
└── Resources/            boş — MVP'de gerek yok
```

### Assembly tanımları (asmdef)

| Dosya | Kapsam |
|---|---|
| `Scripts/KargoSim.Runtime.asmdef` | Tüm oyun kodu (Core, Player, Vehicle, ...) |
| `Scripts/Editor/KargoSim.Editor.asmdef` | Editör araçları, sadece Editor platformunda |
| `Tests/EditMode/KargoSim.Tests.EditMode.asmdef` | Testler |

---

## Kontroller

Henüz yok. Aşama 2'de (araç) eklenir.

---

## Geliştirme aşamaları

| # | Aşama | Durum |
|---|---|---|
| 1 | Proje + ayarlar + çekirdek | ✅ |
| 2 | Player + araç kontrolü | ⬜ |
| 3 | Kargo sistemi | ⬜ |
| 4 | Teslimat sistemi | ⬜ |
| 5 | Para + XP sistemi | ⬜ |
| 6 | UI | ⬜ |
| 7 | Save/Load | ⬜ |
| 8 | Harita ve içerik | ⬜ |
| 9 | Optimizasyon | ⬜ |
| 10 | Polish ve yayın hazırlığı | ⬜ |

**Kural:** Her aşama Play'de test edilmeden sonrakine geçilmez.

---

## Bilinen teknik notlar

- **Domain Reload kapalı** (Editör > Project Settings > Editor > Enter Play Mode).
  Bu yüzden statik event'ler `GameEvents.ResetStatics()` ile temizlenir.
  Statik değişken eklerken bunu unutmayın.
- **URP 17'de** `supportsMainLightShadows`, `supportsSoftShadows`,
  `additionalLightsRenderingMode` setter'ları `internal`'dır.
  `ProjectSetupTool.SetPrivateField()` ile `SerializedProperty` üzerinden yazılır.
- **`Time.fixedDeltaTime` kalıcı değiştirilemez.** Unity 6 bunu
  `TimeManager.asset` içinde rasyonel olarak saklar (`m_Count / m_Numerator`).
  Ayar aracı YAML'ı bu formata göre günceller.
- **Blender kurulu değil.** MVP görselleri Unity primitive'leriyle yapılacak.

### Bilinen Unity tuzakları (bu projede yaşandı)

**1. Script assembly taşınırsa eski asset'ler bozulur.**
Unity, ScriptableObject ve MonoBehaviour asset'lerine
`m_EditorClassIdentifier` (örn. `KargoSim.Runtime::KargoSim.Core.GameConfig`)
yazar. Script `Assembly-CSharp`'ten `KargoSim.Runtime`'a taşınırsa bu
kimlik bayat kalır ve `LoadAssetAtPath<T>()` **NULL** döner — üstelik
`LoadAllAssetsAtPath()` doğru tipi gösterdiği için hata yanıltıcıdır.
Belirti: sahnede referans GUID'i doğru ama runtime'da boş.

Çözüm: `Library` klasörünü silip projeyi yeniden aç.
`ProjectSetupTool.LoadGameConfig()` önce `ImportAsset(ForceUpdate)` dener.

**2. `Time.fixedDeltaTime` kalıcı değiştirilemez.**
Unity 6 bunu `TimeManager.asset` içinde rasyonel saklar
(`m_Count / m_Numerator`, varsayılan `2822399/141120000 = 0.02`).
Satır sonu LF'dir ve değerden sonra virgül yoktur.

**3. URP 17'de bazı ayarlar `internal set`.**
`supportsMainLightShadows`, `supportsSoftShadows`,
`additionalLightsRenderingMode` dışarıdan atanamaz →
`SerializedProperty` gerekir (`ProjectSetupTool.SetPrivateField`).

**4. `EditorUtility.DisplayDialog` batchmode'da kullanılamaz.**
`CreateGameScene` bu yüzden yalnızca menüden çağrıldığında onay ister.
