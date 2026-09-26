# Kargo Simulator

Unity 6 ile geliştirilen, kargo/kurye simülasyon oyunu.

**Durum:** Aşama 1-6 tamamlandı. **Oyun oynanabilir** — depo → kargo yükle → adrese git → teslim et → para kazan. Kalan: görevler, kayıt, harita içeriği.

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

EditMode testleri formülleri ve sahne bütünlüğünü, PlayMode testleri
gerçek fizik motorunda sürüşü doğrular (**146 + 7 test**).

```powershell
# EditMode (146 test)
& "C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe" `
  -runTests -batchmode -nographics -projectPath "D:\KargoSimulator" `
  -testPlatform EditMode -testResults "D:\KargoSimulator\editmode.xml"

# PlayMode (7 test)
& "C:\Program Files\Unity\Hub\Editor\6000.3.25f1\Editor\Unity.exe" `
  -runTests -batchmode -nographics -projectPath "D:\KargoSimulator" `
  -testPlatform PlayMode -testResults "D:\KargoSimulator\playmode.xml"
```

> **Not:** Editor normal kapanışta `Temp/UnityLockfile` silinmezse
> batchmode "*another Unity instance is running*" hatası verir ve
> hiçbir şey çalıştırmaz. Unity kapalıyken bu dosyayı silmek yeterlidir.
>
> ```powershell
> Remove-Item "D:\KargoSimulator\Temp\UnityLockfile" -Force -ErrorAction SilentlyContinue
> ```

Unity içinden: **Window → General → Test Runner**

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
│   ├── Kenney/CarKit/    Hazır CC0 model paketi (bkz. "Görseller")
│   └── Prefabs/
├── Audio/                (boş)
├── Prefabs/
├── Scenes/Game.unity     TEK sahne
├── Scripts/
│   ├── Core/             GameConfig, GameEvents, GameClock, GameCompositionRoot
│   ├── Player/           PlayerController, CameraFollow, PlayerProgress, CourierController, ParcelVisual
│   ├── Vehicle/          VehicleDefinition, IVehicleInput, ArcadeVehicleController
│   ├── Cargo/            Parcel, ParcelType, ParcelGenerator, CargoManifest, CargoSystem
│   ├── Delivery/         MapLayout, DeliveryStop, RoutePlanner, DeliverySystem, StopMarker
│   ├── Economy/          EconomyConfig, PayoutCalculator, Payout, EconomyService
│   ├── Mission/          (Aşama 6)
│   ├── UI/               UIManager, HUD, Depot, Stop, DaySummary, Minimap
│   ├── Save/             (Aşama 7)
│   └── Editor/           8 kurulum aracı (Project / Vehicle / Kenney / Content / Delivery / Progression / UI / Courier)
├── ScriptableObjects/
│   ├── Definitions/      VehicleDefinition, Parcels/ (6 ParcelType)
│   └── Database/GameConfig.asset, MapLayout.asset, EconomyConfig.asset
├── Settings/             URP asset + renderer
├── Tests/EditMode/       GameClock, araç fiziği, kargo, sahne bütünlüğü
├── Tests/PlayMode/       Gerçek fizikte sürüş
└── Resources/            boş — MVP'de gerek yok
```

### Assembly tanımları (asmdef)

| Dosya | Kapsam |
|---|---|
| `Scripts/KargoSim.Runtime.asmdef` | Tüm oyun kodu (Core, Player, Vehicle, ...) |
| `Scripts/Editor/KargoSim.Editor.asmdef` | Editör araçları, sadece Editor platformunda |
| `Tests/EditMode/KargoSim.Tests.EditMode.asmdef` | Testler |

---

## Görseller (hazır model paketleri)

Model için Blender kullanılmıyor. Bunun yerine lisansı ticari kullanıma
açık, düşük poligonlu **hazır paketler** kullanılıyor.

| Paket | Kaynak | Lisans | Kullanım |
|---|---|---|---|
| Car Kit 3.1 | [kenney.nl/assets/car-kit](https://kenney.nl/assets/car-kit) | **CC0 1.0** | Kurye aracı (`delivery.fbx`) + 50 model |
| Mini Characters 1.0 | [kenney.nl/assets/mini-characters](https://kenney.nl/assets/mini-characters) | **CC0 1.0** | Kurye karakteri (`character-male-a.fbx`, rigged, 32 klip) |

**CC0 ne demek:** Telif hakkı tamamen kamu malıdır. Ticari kullanım
serbest, izin istemek zorunlu değil, atıf zorunlu değil. Yine de
`Assets/Art/Kenney/CarKit/License.txt` projede tutulur.

**Kurulum:** Paketi indirip `Assets/Art/Kenney/CarKit/` altına açın
(`.fbx` + `Textures/` aynı klasör yapısında kalmalı), sonra menüden:

```
Kargo → Asset → Kenney Kurye Aracini Kur
```

Ya da `Kargo → 0. HER SEYI KUR` tüm zinciri sırayla çalıştırır.

**Ölçekleme:** Model ölçüsü `delivery.fbx` içinde 3.25 m; kurulum aracı
hedef boyu **4.20 m** olacak şekilde `x1.292` ile ölçekler, dingil
ortasını ve teker izini eksene hizalar, en alta indirir (lastikler
zeminde). Sonuç: **4.20 × 1.94 × 2.13 m** bir panelvan, teker yarıçapı
**0.30 m**. `VehicleDefinition.wheelRadius` buna göre güncellenir; fizik
(şasi collider'ı, süspansiyon) görselle birebir örtüşür.

**Tekerler:** Kenney modelinde tekerler ayrı mesh gruplarıdır
(`wheel-front-left` …). Bunlar `WheelCollider` nesnelerinin altına
taşınır ve `WheelVisual` ile fizik pozuna kilitlenir — böylece hem
**dönüyor** hem de ön tekerler **direksiyona uyuyor**. (Önceden
silindir tekerler takılıydı ve hiç dönmüyordu.)

**Güvenli geri dönüş:** Hazır model kurulu değilse `VehiclePrefabBuilder`
primitive yola düşer. Yani paket silinse bile oyun yine sürülebilir
bir araçla açılır.

---

## Kurye ve fiziksel kargo

Kargo artık **listedeki satıra tıklamakla değil, taşınarak** yüklenir.
Karakter aracı kullanmaz; sürüş yine araçla yapılır.

| Durum | Ne olur |
|---|---|
| Depoda `E` | Depo listesi açılır |
| `1-9` + `E` | Seçilen kargo için karakter aracından inip depoya yürür, kargoyu alır (`pick-up`), araca döner ve yükler |
| Adreste `E` | Karakter aracından inip adrese yürür, kargoyu bırakır (`interact-right`) ve aracına döner |

**Anlık yükleme yok.** Karakter van'a varıp animasyonu bitirmeden kargo
"yüklü" sayılmaz; teslim süresi de tam o andan başlar. Bu yüzden
oyuncu acele edip süreyi kendi kaybetmez.

**Karakter yürürken araç kilitlenir** (`ArcadeVehicleController.ControlsLocked`).
Oyuncu aynı anda hem yürüyüp hem araç kullanamaz — "ben mi yürüyorum,
araç mı?" belirsizliği oluşmaz.

**HUD ipucu** o anki durumu yazar (`Kurye: Depodan kargo alınıyor` gibi),
böylece karakter neden hareket ediyor anlaşılır.

### Neden ayrı bir katman

`CargoSystem` ve `DeliverySystem` **saf durum mantığıdır**: kargo
yüklendiğinde "yüklü" derler. Onlar test edilebilir ve deterministik
kalmalıdır. `CourierController` ise o anın **görselini ve zamanını** yönetir:
yürür, animasyon oynatır, *sonra* mantık sistemine "şimdi yükle" der.

Böylece fiziksel tasıma eklenirken deterministik çekirdek testleri
hiç değişmedi. 7 yeni `CourierControllerTests` bu ayrımı korur — en
önemlisi `YurumeBitmedenKargoYuklenmez`.

### Kullanılan animasyonlar

Kenney Mini Characters 32 klip içerir; yalnızca beşi kullanılır
(kullanılmayan klip runtime'da bellek yeridir):

| Klip | Süre | Kullanım |
|---|---|---|
| `idle` | 1.33 s | bekleme |
| `walk` | 0.67 s | yürüme |
| `pick-up` | 0.33 s | kargo alma |
| `holding-both` | 0.17 s | kargo taşıma |
| `interact-right` | 0.67 s | teslim |

Menü: `Kargo → 7. Kurye Karakterini Kur`

---

## Oynanış akışı

Kurye karakteri oyunun **kontrolünde**: ayaktayken WASD + fare ile
FPS modunda yürür, araçta aracı sürer.

| Aşama | Ne yaparsın |
|---|---|
| **1. Dükkân** | Mavi alandaki dükkâna gir. Raftaki kutulara yaklaş, `E` ile al (aynı anda 3 kutu). |
| **2. Aracı yükle** | Aracın arkasındaki mavi noktaya git, `E`ye bas. Kutular araca yüklenir. |
| **3. Araca bin** | Elin boşken aracın yanında `E`ye bas. Kamera araca geçer, araç kilidi açılır. |
| **4. Dağıt** | Adrese git, `E` ile aracı bırak, eve yürü, kapının önünde `E`ye bas. |

`E`'nin ne yapacağını **hedef kendisi söyler** ve ipucu `E`'nin yanında
yazar — "E: Kargo al (12 bekliyor)", "E: Araca koy (2 kutu)",
"E: Araca bin", "E: Karguyu bırak (3 bekliyor)".

### Neden tıklanabilir hedef sistemi

`IInteractable` sözleşmesi: her hedef (`ShopParcelPile`,
`VanCargoPoint`, `AddressDoor`) kendi ipucunu ve kendi davranışını
bildirir. Önce `E = yükle` kuralı `UIManager` içine dağılmıştı; dört
farklı etkileşim noktası olunca oyuncu ne yapacağını tahmin etmek
zorunda kalıyordu. Artık `E`'ye basmadan önce ne olacağını görüyor.

### Fare ve imleç

Ayaktayken imleç oyuna kilitlenir (standart FPS). `Esc` imleci serbest
bırakır ve panelleri kapatır.

### Süre ne zaman başlar

Kargo araca **gerçekten konduğu** anda başlar, raftan alındığında
değil. `TryLoadParcel(id, systemTime)` gerçek yükleme anını alır;
önce `systemTime` hiç geçirilmiyordu ve sayaç **gün başından** işliyordu
(öğlen yüklenen kargo doğuştan "geç" sayılıyordu).

### Araç neden hareket etmiyor

Karakter ayaktayken araç bilerek kilitlidir (`ControlsLocked`).
Oyuncu aynı anda hem yürüyüp hem araç kullanamaz; HUD'da
"Arac kilitli" yazar. Karakter araca binince kilit açılır.

---

## Kontroller

| Tuş | İşlev |
|---|---|
| **W / ↑** | Gaz (ileri) |
| **S / ↓** | Fren / geri vites |
| **A / ← · D / →** | Direksiyon |
| **Space** | El freni (yalnızca arka tekerlere) |
| **R** | Aracı başlangıç noktasına iade et |
| **E** | **Ana eylem tuşu** (bağlama göre aşağıya bak) |
| **1 – 9** | Depoda kargo seç |
| **Q** | Aracı boşalt |
| **Tab** | Depo listesini aç / kapat — *yalnızca alias* |
| **Esc** | Panelleri kapat |

**`E` bağlama göre çalışır** — tek bir "ana eylem" tuşu, üç farklı durum:

| Durum | `E` ne yapar |
|---|---|
| Depoda, panel kapalı | Depo listesini açar |
| Depoda, panel açık | Seçili kargoyu yükler (panel açık kalır, art arda yüklenebilir) |
| Duraktayken | Sıradaki kargoyu teslim eder |

> **Neden `Tab` birincil değil:** Unity Editor `Tab`'ı odak
> değiştirme için kullandığı için oyun içinde Editor'da çalıştırırken
> `Tab` tuşu oyuncuya ulaşmayabiliyordu. Bu yüzden birincil tuş `E`
> yapıldı, `Tab` build'de çalışan alternatif olarak kaldı. Gerçek
> Build'de ikisi de tanımlıdır.

Ayrıca HUD'da o anki bağlam yazılı (`[E] Depoyu ac`, `[E] Yukle`,
`[E] Teslim et` …); oyuncu tuşları tahmin etmek zorunda kalmaz.

Detaylı oynanış: bakınız *Aşama 6 — Arayüz*.

---

---

---

## Aşama 2 — Araç

| Script | Görevi |
|---|---|
| `Vehicle/VehicleDefinition` | Araç özellikleri (SO): hız, tork, fren, direksiyon, tutunma, süspansiyon, **kargo kapasitesi** |
| `Vehicle/IVehicleInput` | Sürücü sözleşmesi: `Throttle`, `Steer`, `Handbrake`, `RespawnRequested` |
| `Vehicle/ArcadeVehicleController` | Fizik: motor, fren, direksiyon, yanal tutunma, downforce, takla koruması |
| `Player/PlayerController` | Klavye → girdi |
| `Player/CameraFollow` | Takip kamerası, hıza göre FOV |
| `Player/CourierController` | Kargoyu depodan araca ve adresten araca taşıyan karakter |
| `Editor/VehiclePrefabBuilder` | Prefab'ı üretir, sahneye koyar, doğrular |
| `Editor/KenneyAssetSetup` | Hazır CC0 modeli (Kenney Car Kit) içe alır, aracı bu modelle üretir |
| `Vehicle/WheelVisual` | Teker mesh'ini `WheelCollider` pozuna kilitler (dönme + direksiyon) |

**Tasarım kararı:** Fizik matematiği `static` saf fonksiyonlarda
(`ComputeSteerAngle`, `ComputeMotorTorque`, `ComputeBrakeTorque`).
EditMode testleri formülleri sahne açmadan doğrular; PlayMode testleri
gerçek fizik motorunda sürüşü doğrular.

Menü: `Kargo/Vehicle/Arac Prefabini Kur`

**Tasarım kararı:** Gövdede `BoxCollider` **zorunludur**
(`[RequireComponent]`). Sadece tekerlerden oluşan bir araç dururken sorunsuz
durur ama ilk gazda takla atar — PlayMode testinde ölçüldü.

---

## Aşama 3 — Kargo sistemi

| Script | Görevi |
|---|---|
| `Cargo/ParcelType` (SO) | Kargo türü tanımı: ağırlık/hacim aralığı, kg başına ücret, kırılabilirlik |
| `Cargo/ParcelPriority` | Öncelik enum'u + ödeme/süre/başarı çarpanları |
| `Cargo/ParcelState` | Beklemede / aracta / teslim edildi |
| `Cargo/Parcel` | Tek kargonun verisi (saf C#) |
| `Cargo/ParcelGenerator` | Günlük kargo üretimi — **deterministik** (`System.Random` + gün tohumu) |
| `Cargo/CargoManifest` | Depo listesi + araç yükü, **ağırlık VE hacim** kontrolü |
| `Cargo/CargoSystem` | Orkestrasyon: gün başında üretim, yükleme isteği, olay yayını |
| `Editor/ContentSetupTool` | 6 kargo türünü üretir, Composition Root'a bağlar |

Menü: `Kargo/3. Icerik Assetlerini Kur`

### Kapasite iki ayrı sınırdır

| Durum | Sonuç |
|---|---|
| ağır ama küçük | ağırlık dolar, hacim boş kalır |
| hafif ama devasa | hacim dolar, ağırlık boş kalır |

Tek sınır olsaydı oyuncu devasa ama boş paketlerle sınırsız yük taşırdı.

### Determinizm neden önemli

`ParcelGenerator` `UnityEngine.Random` değil `System.Random` kullanır ve
tohumu gün numarasından türetir (`day * 7919`). Aynı gün → aynı kargolar.
Böylece "5. günde hangi kargolar vardı" sorusu save dosyasından yeniden
kurulabilir; testler de kararlı olur.

### Bu aşamada olmayanlar (bilinçli)

Teslimat ve ödeme **Aşama 4/5**. `Parcel.DestinationId` şimdilik opak bir
tamsayı; gerçek koordinatlara Aşama 4'te bağlanacak. `Parcel.BaseValue`
kargonun kendi değerini tutar, ödeme hesabını ekonomi sistemi yapar — iki
yer hesap yapmaz.

---

## Aşama 4 — Teslimat sistemi

| Script | Görevi |
|---|---|
| `Delivery/MapLayout` (SO) | Teslimat noktaları (id + konum) + depo konumu |
| `Delivery/DeliveryOutcome` | Başarılı / geç kaldı / başarısız + ödeme çarpanı |
| `Delivery/DeliveryStop` | Bir adrese gidecek kargolar (aynı adres = tek durak) |
| `Delivery/RoutePlanner` | En-yakın-komşu rota sıralaması (saf, statik) |
| `Delivery/DeliverySystem` | Süre takibi, varış algısı, teslim, gün sonu |
| `Editor/DeliverySetupTool` | 14 teslimat noktası üretir (iki halka), sahneye bağlar |

Menü: `Kargo/4. Harita ve Teslimat Noktalarini Kur`

### Süre yüklenme anından işler

Teslim süresi gün başında değil, kargo **araca yüklendiği anda** başlar.

Nedeni: Gün başında başlasaydı 12 kargonun hepsi aynı anda dolar ve süre
kavramı anlamını yitirdi. Yüklenmeden başlayınca "çok kargo yükleme" ile
"az kargo yükleme" arasında gerçek bir tercih ortaya çıkıyor.

### Rota neden basit

En-yakın-komşu (greedy) kullanılıyor. Optimal çözüm (TSP) 14 noktada bile
pahalı ve oyuncuya kazancı görünmez; önemli olan oyuncunun sırayı
**görebilmesi** (Aşama 6'da minimapta çizilecek). Rota, giriş listesini
değiştirmeden sıralı kopya döndürür — yan etkisiz ve test edilebilir.

### Teslimat sonucu

| Sonuç | Koşul | Etki |
|---|---|---|
| `Success` | süre dolmadan | tam ödeme + XP |
| `Late` | süre geçtikten sonra | kısmi ödeme, az XP |
| `Failed` | gün sonunda teslim edilmemiş | ödeme yok, itibar kaybı |

Para ve XP **Aşama 5**. `DeliverySystem` yalnızca sonucu bilir;
hesaplamayı ekonomi sistemi yapar.

### Rota kendini iyileştirir

Rota, event'e değil **yüklü kargo sayısına** bakarak yenilenir. Kargo
başka bir yoldan yüklenirse (test, ileride araç değişimi) rota bayat
kalmaz. Test bunu doğruluyor.

### Bu aşamada olmayanlar

Binalar, yollar, görsel teslimat noktaları **Aşama 8**. Şu an
`MapLayout` iki halkadan oluşan geçici bir ızgaradır; Aşama 8'de aynı
kimlikli gerçek binalar bu konumlara bindirilecek — asset değişmeyecek.

---

---

## Aşama 5 — Para + XP/level

| Script | Görevi |
|---|---|
| `EconomyConfig` (SO) | Para + XP + seviye eğrisi ayarları |
| `Payout` | Hesap sonucu: ödeme, bahşiş, ceza, XP |
| `PayoutCalculator` | **Saf statik formül** — kargo + sonuç → ödeme |
| `PlayerProgress` | Bakiye, XP, seviye, gün istatistikleri |
| `DaySummary` | Gün sonu özeti |
| `EconomyService` | Teslimat olaylarını dinler, ödemeyi uygular |

Menü: `Kargo/5. Ilerleme Ayarlarini Kur`

### Ödeme formülü

```
ödeme  = kargo değeri × sonuç çarpanı               (1.0 / 0.4 / 0.0)
bahşiş = kargo değeri × bahsisOranı × kalanSüreOranı   ← sadece Success'te
XP     = (xpPerDelivery + xpPerKg × ağırlık) × sonuç çarpanı
```

Bahşiş kalan süreyle ölçeklenir: süre tam kaldıysa tam bahşiş, eşiğin
altındaysa hiç. Böylece "hızlı teslim = daha çok kazanç" ayrı bir kurala
gerek kalmadan doğal çıkar.

### Başarısızlık cezası yok

Varsayılan `failedDeliveryPenalty = 0`. Başarısız teslim **para
kazandırmaz**, ama para da götürmez. Gerekçe: negatif bakiye sinir
bozucudur ve oyuncuyu kargoyu hiç yüklememeye iter. Bedeli zaten zaman
kaybıdır — teslimatın ödenmemiş olması yeterince ağır.

### XP eğrisi (kritik)

Her seviye **atlama maliyeti** artar; üs toplama uygulanmaz:

| Seviye | Atlama maliyeti | Kümülatif |
|---|---|---|
| 1 → 2 | 100 | 100 |
| 2 → 3 | 135 | 235 |
| 3 → 4 | 182 | 417 |

Üs toplama uygulanırsa ilk atlama (100 XP) ikincisinden (35 XP) pahalı
çıkar; oyuncu ilk seviyede takılıp sonra hızla yükselir. Test bu hatayı
yakaladı ve eğri düzeltildi.

### Gün döngüsü

Saat 24:00 olduğunda sırayla: teslim edilmemişler başarısız sayılır →
gün özeti üretilir → istatistikler sıfırlanır → yeni günün kargoları
üretilir.

Dinleme `DayStarted` / `DayEnded` üzerinden yapılır; sistemler birbirini
tanımaz, hepsi olay kanalından haber alır.

> **Bulunan hata:** Kargolar yalnızca `Start()`'ta bir kez üretiliyordu —
> 2. gün depoda **hiç kargo kalmıyordu**. Regresyon testi eklendi.

### Bu aşamada olmayanlar

UI (Aşama 6). Para hareketleri şu an sadece sayı; ekranda görünmüyor.
Araç yükseltme mağazası, itibar ve şirket büyümesi bu aşamanın
kapsamında değil.

---

---

## Aşama 6 — Arayüz

| Script | Görevi |
|---|---|
| `UI/UITheme` | Tüm renkler ve ölçüler — tek kaynak |
| `UI/UIManager` | Tek giriş noktası: olay bağları, girdi, minimap |
| `UI/HudView` | Para, seviye/XP, saat, kapasite, sonraki durak |
| `UI/DepotView` + `ParcelRowView` | Depo listesi, yükleme, satır havuzu |
| `UI/StopView` | Adrese varınca açılan teslimat paneli |
| `UI/DaySummaryView` | Gün sonu özeti |
| `UI/MinimapView` | Depo, duraklar, rota çizgisi, araç |
| `Delivery/StopMarker` | Dünyada renkli zemin işareti + etiket |
| `Editor/UiSetupTool` | Tüm arayüzü sıfırdan kurar |

Menü: `Kargo/6. Arayuzu Kur`

### Oynanış akışı

```
Depoya git (mavi alan)  →  Tab            → kargo listesini aç
                            tıkla / 1-9  → kargo seç
                            E             → yükle    (Q: hepsini boşalt)
                            Tab           → kapat

Renkli kareye git        →  panel otomatik açılır
                            E             → teslim et (her basışta bir kargo)
```

Minimap (sağ üst) ve dünyadaki renkli işaretler hedefi gösterir.

### Performans: iki kural

**1. Tek `Update` kuralı bozulmadı.** `UIManager.Update()` yok —
`CompositionRoot` her karede `uiManager.Tick(dt)` çağırır. `StopMarker`'ın
da `Update`'i yok: 14 marker için 14 geri çağrı gereksiz maliyettir.
Etiket yere yatay durur, kamera zaten üstten baktığı için okunur ve kare
maliyeti sıfırdır.

**2. Metinler dirty-check ile yazılır.** `HudView.Refresh` her karede
çağrılır ama metni yalnızca **değer değiştiğinde** günceller. Aksi halde
saniyede 60 string ayrıştırma olurdu.

Satırlar **havuzlanır** — depo her açılışta GameObject üretmez.

### Bulunan hata (editör aracı)

`DeliverySetupTool`, 14 işareti oluşturduktan **sonra** sahneyi
`OpenScene(Single)` ile yeniden açıyordu; kaydedilmemiş işaretler yok
oluyordu. Sahne artık **başta** açılıyor, işaretler sonda yazılıyor.

> **Kural:** Editör aracı sahneye nesne ekliyorsa sahneyi **tam başta bir
> kez** açmalı. Ortada yeniden açmak kaydedilmemiş her şeyi siler.

### Panel görünmüyordu (manuel testte bulundu)

İlk manuel oyunda depo ve teslimat panelleri hiç açılmıyordu; oyuncu
"Tablo/E çalışmıyor" sanıyordu. Gerçek neden tuşlar değil, kurulum
aracıydı:

`UiSetupTool`, panelin **kök** `GameObject`'ini kapatıyordu
(`go.SetActive(false)`), sonra "paneli gizle"mek için içteki `panel`
çocuğunu kapatıyordu. `DepotView.Open()` ise yalnızca **çocuğu**
açıyordu. Unity'de inactive bir nesnenin çocuğu da render edilmez —
komut çalışıyor, hiçbir şey görünmüyordu.

Çözüm: kökler her zaman açık, gizleme yalnızca iç çocukta yapılır.
`PanelKokleriAcikKalmali` testi bu kuralı koruyor.

Ayrıca **`Tab` Unity Editor tarafından yutuluyor** (odak değiştirme).
Bu yüzden birincil eylem tuşu `E` yapıldı, `Tab` alias kaldı; HUD
her an ne yapılabileceğini yazıyor.

### HUD yerleşimi

Para/seviye paneli ve minimap **sol alta** alındı (istek üzerine):
bilgi paneli sol alt köşede, minimap hemen sağında, hedef/ipucu
metinleri alt orta. Eskiden HUD sol üstte, minimap sağ üstteydi.

### Bu aşamada olmayanlar

Yakıt, görevler (Mission) ve kayıt (Save) hâlâ yok. Yakıt Aşama 2'de
bilinçli olarak ertelenmişti: depoya yakıt alma mekaniği kargo sistemiyle
birlikte çalışması daha doğru olur.

---

## Geliştirme aşamaları

| # | Aşama | Durum |
|---|---|---|
| 1 | Proje + ayarlar + çekirdek | ✅ |
| 2 | Player + araç kontrolü | ✅ |
| 3 | Kargo sistemi | ✅ |
| 4 | Teslimat sistemi | ✅ |
| 5 | Para + XP sistemi | ✅ |
| 6 | UI | ✅ |
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
- **Blender kurulu değil.** Görseller hazır **CC0** model
  paketlerinden geliyor (bkz. *Görseller*), Blender'a gerek yok.

### Bilinen Unity tuzakları (bu projede yaşandı)

**0. Editor kurulum aracı, nesne silmeden "temizlik" yaparsa yığılmaya
yol açar.**
`DeliverySetupTool.BuildMarkers` her çağrıda yeni bir `DeliveryStops`
kökü açıyor, eski işaretleri silmeyi de o **yeni boş** kökte
arıyordu. Arama hiçbir şey bulamadığı için işaretler üst üste
birikiyordu: harita iki kez kurulunca 14 noktaya karşılık sahnede
**28** ped vardı. Oyuncu aynı hedefi iki kez görüyordu.

İki düzeltme yapıldı: (a) temizlik artık tüm sahne köklerini tarayarak
`DeliveryStops` kopyalarını siliyor, (b) `DurakIsaretleriSahnede`
testi artık sadece "var mı" değil, **sayının harita nokta sayısına
birebir eşit** olduğunu doğruluyor. (Eski test `> 0` idi ve 28 ped'de
sessizce geçiyordu.)

**0b. Mesh'lerin yerel sınırlarını birleştirirken transform'un
kendisi hesaba katılmalı.**
Araç modelini ölçerken `MeshFilter.sharedMesh.bounds` değerleri
doğrudan `Encapsulate` edildi. Ancak Kenney modelinde tekerler modelin
içinde kaydırılmış alt nesneler; kendi transform'ları yok sayılınca
tekerler modelin merkezinde varsayılıyor ve **Y boyutu 3.23 m** gibi
yanlış çıktı (doğrusu 2.13 m). Çözüm: her mesh'in 8 köşesi
`Transform.TransformPoint` ile istenen uzaya taşınarak birleştirilir.
Bu yöntem `Renderer.bounds`'un kare gecikmesinden de etkilenmez.

**1. Script assembly taşınırsa eski asset'ler bozulur.**
Unity, ScriptableObject ve MonoBehaviour asset'lerine
`m_EditorClassIdentifier` (örn. `KargoSim.Runtime::KargoSim.Core.GameConfig`)
yazar. Script `Assembly-CSharp`'ten `KargoSim.Runtime`'a taşınırsa bu
kimlik bayat kalır ve `LoadAssetAtPath<T>()` **NULL** döner — üstelik
`LoadAllAssetsAtPath()` doğru tipi gösterdiği için hata yanıltıcıdır.
Belirti: sahnede referans GUID'i doğru ama runtime'da boş.

Çözüm: `Library` klasörünü silip projeyi yeniden aç.
`ProjectSetupTool.LoadGameConfig()` önce `ImportAsset(ForceUpdate)` dener.

**2. `Plane` (MeshCollider) araç taşıyamaz.**
`PrimitiveType.Plane` tek parça bir quad'dır ve `WheelCollider` ile
güvenilir çarpışmaz. PlayMode testinde ölçüldü: araç zemine değip
hemen içinden geçiyordu (y = -26 m). `Cube` (BoxCollider) ile aynı
sahne kusursuz çalışıyor (y = 0.15). Bu yüzden zemin `Cube` ile üretilir.

**3. Aracın gövdesinde collider olmazsa ilk gazda takla atar.**
Sadece 4 `WheelCollider`'dan oluşan bir araç, fizik motoru için
kararsızdır: dururken sorunsuz durur, ama ilk motor torkunda takla atar.
Ölçülen sonuçlar (1200 kg, 2000 N):

| Gövde collider | İtki | Sonuç |
|---|---|---|
| yok | `AddForce` | azami eğilme 164°, y = -76 m |
| yok | `motorTorque` | azami eğilme 173°, y = -94 m |
| var | `AddForce` | kararlı ama **hareket etmiyor** (0.00 m) |
| var | `motorTorque` | **kararlı, 2.4 sn'de 16.7 m** ✓ |

Gövde collider'ı kök nesnedeki `BoxCollider`'dır
(`[RequireComponent]` ile otomatik gelir). Görsel mesh'ler (`Body`, `Cabin`,
`Bumper_*`) collider taşımaz. **Buraya ikinci bir `Chassis` çocuğu
eklemek iki collider yaratır ve aracı yerinde sıkıştırır.**

**4. Duruşta fren uygulanırsa motor kazanamaz.**
Yerleştirme kaynaklı 0.02 m/s'lik geriye kaymada "gaz basılı ama hız
ters yönde" kuralı devreye girip tekeri 3500 Nm ile kilitliyordu.
`ComputeBrakeTorque` artık `|hız| < 0.1` iken fren uygulamaz.

**5. Direksiyon eğrisi `1/(1+hız·k)` ise yüksek hızda araç dönmez.**
Bu eğri 10 m/s'te açıyı %9'a (2.9°) düşürüyordu. Yerine doğrusal
enterpolasyon: dururken tam açı, azami hızda `minSteerFactor` (varsayılan
%35). 10 m/s'te 25°, 30 m/s'te 11°.

**6. Test zemini yeterince büyük olmalı.**
Azami hız testi 12 saniye tam gaz gidiyor; araç yüzlerce metre yol
alıyor. 400×400 bir zemin yeterli değil — araç kenardan "düşüp"
boşluğa kaçıyor ve hız testi sahte şekilde başarısız oluyor.
Test zemini 10000×10000.

**7. `Time.fixedDeltaTime` kalıcı değiştirilemez.**
Unity 6 bunu `TimeManager.asset` içinde rasyonel saklar
(`m_Count / m_Numerator`, varsayılan `2822399/141120000 = 0.02`).
Satır sonu LF'dir ve değerden sonra virgül yoktur.

**8. URP 17'de bazı ayarlar `internal set`.**
`supportsMainLightShadows`, `supportsSoftShadows`,
`additionalLightsRenderingMode` dışarıdan atanamaz →
`SerializedProperty` gerekir (`ProjectSetupTool.SetPrivateField`).

**9. `Physics.autoSyncTransforms` Unity 6'da deprecated.**
Her zaman kapalı; transform değişikliklerini elle taşımak gerekirse
`Physics.SyncTransforms()` çağrılmalı.

**10. `EditorUtility.DisplayDialog` batchmode'da kullanılamaz.**
Sahne yeniden oluşturma yalnızca menüden çağrıldığında onay ister.

**11. MonoBehaviour `new` ile oluşturulamaz.**
`new FakeDriver()` Unity'de "sahte null" döner (`== null` **true**).
`AddComponent<T>()` kullanılmalı.

**12. PlayMode'da `AddComponent` anında `Awake` çağırır.**
Girdi henüz atanmamışsa `Awake` hata loglar ve test `SetUp`'ta düşer.
Nesneyi **pasif** kurup (`SetActive(false)`), bağlantıları yapıp sonra
aktifleştirmek gerekir — bu, prefab akışının da gerçekte nasıl
çalıştığını yansıtır.
