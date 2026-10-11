# Kargo Simulator

Unity 6 ile geliştirilen,  kargo/kurye simülasyon oyunu.

**Durum:** A şama 1-6 tamamlandı. **Oyun oynanabilir** � �� depo → kargo yükle → adrese git → t eslim et → para kazan. Kalan: görevler, ka yıt, harita içeriği.

---

## Kurulum

| B ileşen | Sürüm |
|---|---|
| Unity | **600 0.3.25f1** (LTS) |
| Render Pipeline | URP 17 .3.0 (Forward) |
| Script | C# / .NET Standar d 2.1 |
| Harici paket | **Yok** — Timeline , Cinemachine, Input System, DOT dahil hiçbi r şey kullanılmıyor |

### Adımlar

1. Un ity Hub'da **+ → Open → add project from  disk** ile `D:\KargoSimulator` seç.
2. `6000 .3.25f1` sürümüyle aç.
3. Sahneyi aç: `A ssets/Scenes/Game.unity`
4. **Play**'e bas.

 İlk açılışta `Library` klasörü yeniden  oluşacağı için birkaç dakika sürebilir .

---

## Projeyi Kurma Aracı

Elle yapılm ası gereken onlarca ayar (URP pipeline'i olu şturmak, katmanları tanımlamak, fizik matr isini kurmak, sahneyi kurmak) tek bir editor  aracıyla yapılır:

**Unity menüsü → `K argo`**

| Menü | Ne yapar |
|---|---|
| `1.  Tum Kurulumu Calistir` | Her şeyi sırayla  yapar (idempotent, tekrar çalıştırılabil ir) |
| `2. Klasorleri Hazirla` | Klasör isk eletini oluşturur |
| `3. Render Pipeline (U RP) Kur` | URP asset + renderer oluşturur, * *global olarak atar** |
| `4. Katmanlari Kur`  | Layer 8-13 + fizik çarpışma matrisi |
|  `5. Fizik Ayarlari` | Yerçekimi, solver, au toSyncTransforms |
| `6. Kalite Ayarlari` | 6  kalite seviyesinin tümü, varsayılan Mediu m |
| `7. Player Ayarlari` | Ürün bilgisi,  renk uzayı, splash, Play modu |
| `8. GameCo nfig Olustur` | Oyun ayarları asset'i |
| `9 . Game Sahnesini Kur` | Sahneyi **sıfırdan* * kurar (var olanı siler, onay ister) |
| `1 0. Fixed Timestep 0.02` | 50 Hz fizik adımı  |
| `Diagnoz: GameConfig Yukleme` | Asset y� �klenemiyorsa nedenini raporlar |

> `9. Game  Sahnesini Kur` mevcut sahneyi siler. Toplu k urulum
> (`1.`) sahneye dokunmaz, sadece yoks a oluşturur.

Kurulum bittikten sonra araç  şunu doğrular:
`GameCompositionRoot.config`  referansının kaydedilmiş sahnede gerçekt en
dolu olduğunu okuyup kontrol eder. Refera ns boşsa hata verir — "oyun
neden açılm� �yor" belirsizliği kalmaz.

### Testler

Edi tMode testleri formülleri ve sahne bütünl� �ğünü, PlayMode testleri
gerçek fizik mot orunda sürüşü VE oyunun tüm akışını  doğrular (**179 + 8 test**).

```powershell
 # EditMode (179 test)
& "C:\Program Files\Uni ty\Hub\Editor\6000.3.25f1\Editor\Unity.exe" ` 
  -runTests -batchmode -nographics -projectP ath "D:\KargoSimulator" `
  -testPlatform Edi tMode -testResults "D:\KargoSimulator\editmod e.xml"

# PlayMode (8 test)
& "C:\Program Fil es\Unity\Hub\Editor\6000.3.25f1\Editor\Unity. exe" `
  -runTests -batchmode -nographics -pr ojectPath "D:\KargoSimulator" `
  -testPlatfo rm PlayMode -testResults "D:\KargoSimulator\p laymode.xml"
```

> **Not:** Editor normal ka panışta `Temp/UnityLockfile` silinmezse
> b atchmode "*another Unity instance is running* " hatası verir ve
> hiçbir şey çalıştı rmaz. Unity kapalıyken bu dosyayı silmek ye terlidir.
>
> ```powershell
> Remove-Item "D: \KargoSimulator\Temp\UnityLockfile" -Force -E rrorAction SilentlyContinue
> ```

Unity içi nden: **Window → General → Test Runner**
 
Komut satırından da çalıştırılabilir: 

```powershell
& "C:\Program Files\Unity\Hub \Editor\6000.3.25f1\Editor\Unity.exe" `
  -ba tchmode -quit -nographics `
  -projectPath "D :\KargoSimulator" `
  -executeMethod KargoSim .EditorTools.ProjectSetupTool.RunAllSetupIncl udingTimestep
```

---

## Mimari

```
         ┌─────────────� ��────┐
        │  GameCompositio n │  ← TEK referans noktası
        │        Root       │
        └────� ��───┬─────────┘
    ┌─────┬─────── ┼───────┬────── ──┬──────────┬─ ────────┐
   ▼     ▼        ▼       ▼        ▼          ▼          ▼
 Vehicle  Cargo  Delivery Economy  M ission  Save     UIManager
   └──── ───┴───────┴─── ────┴────────┴─ ───────┴─────── ──┘
                 │  okur, bağım lılık kurmaz
                 ▼
           ┌─────────────� �┐
          │  GameEvents  │  static e vent bus
          └───────� �──────┘
```

**Kural:** Sistem ler birbirini referanslamaz. Hepsi `GameEvent s` üzerinden konuşur.
Bir sistem silinse bi le diğerleri bozulmaz.

### Performans karar ları (baştan)

| Karar | Değer |
|---|---| 
| AA / MSAA | **kapalı** |
| HDR | **kapal� �** |
| Depth / Opaque texture | **kapalı**  (pre-pass yok) |
| Post-processing | **yok**  |
| Ek ışıklar | **Per Vertex, en fazla 1* * |
| Gölge | tek ışık, **1 cascade**, 60  m |
| Yerçekimi | **-25** (arcade his) |
|  Fixed Timestep | **0.02** (50 Hz) |
| `autoSy ncTransforms` | **kapalı** |
| Renk uzayı |  Linear |
| `Update()` sayısı | **1** (yaln ızca `GameCompositionRoot`) |

Bunlar `Kargo /1. Tum Kurulumu Calistir` ile uygulanır.

- --

## Klasör yapısı

```
Assets/
├─� � Art/
│   ├── Materials/        URP/ Lit düz renkli materyaller
│   ├── K enney/CarKit/    Hazır CC0 model paketi (bkz . "Görseller")
│   └── Prefabs/
├� ��─ Audio/                (boş)
├──  Prefabs/
├── Scenes/Game.unity     TEK  sahne
├── Scripts/
│   ├── Core /             GameConfig, GameEvents, GameClo ck, GameCompositionRoot
│   ├── Playe r/           PlayerController, CameraFollow,  PlayerProgress, CourierController, ParcelVisu al
│   ├── Vehicle/          VehicleD efinition, IVehicleInput, ArcadeVehicleContro ller
│   ├── Cargo/            Parcel , ParcelType, ParcelGenerator, CargoManifest,  CargoSystem
│   ├── Delivery/          MapLayout, DeliveryStop, RoutePlanner, Deli verySystem, StopMarker
│   ├── Econom y/          EconomyConfig, PayoutCalculator,  Payout, EconomyService
│   ├── Missio n/          (Aşama 6)
│   ├── UI/                UIManager, HUD, Depot, Stop, DayS ummary, Minimap
│   ├── Save/              (Aşama 7)
│   └── Editor/            8 kurulum aracı (Project / Vehicle / Ke nney / Content / Delivery / Progression / UI  / Courier)
├── ScriptableObjects/
│    ├── Definitions/      VehicleDefinitio n, Parcels/ (6 ParcelType)
│   └── Da tabase/GameConfig.asset, MapLayout.asset, Eco nomyConfig.asset
├── Settings/              URP asset + renderer
├── Tests/Edit Mode/       GameClock, araç fiziği, kargo,  sahne bütünlüğü
├── Tests/PlayMode /       Gerçek fizikte sürüş
└── Re sources/            boş — MVP'de gerek yok 
```

### Assembly tanımları (asmdef)

| Do sya | Kapsam |
|---|---|
| `Scripts/KargoSim. Runtime.asmdef` | Tüm oyun kodu (Core, Playe r, Vehicle, ...) |
| `Scripts/Editor/KargoSim .Editor.asmdef` | Editör araçları, sadece  Editor platformunda |
| `Tests/EditMode/Kargo Sim.Tests.EditMode.asmdef` | Testler |

---

 ## Görseller (hazır model paketleri)

Model  için Blender kullanılmıyor. Bunun yerine  lisansı ticari kullanıma
açık, düşük p oligonlu **hazır paketler** kullanılıyor.
 
| Paket | Kaynak | Lisans | Kullanım |
|--- |---|---|---|
| Car Kit 3.1 | [kenney.nl/asse ts/car-kit](https://kenney.nl/assets/car-kit)  | **CC0 1.0** | Kurye aracı (`delivery.fbx` ) + 50 model |
| Mini Characters 1.0 | [kenne y.nl/assets/mini-characters](https://kenney.n l/assets/mini-characters) | **CC0 1.0** | Kur ye karakteri (`character-male-a.fbx`, rigged,  32 klip) |

**CC0 ne demek:** Telif hakkı t amamen kamu malıdır. Ticari kullanım
serbe st, izin istemek zorunlu değil, atıf zorunl u değil. Yine de
`Assets/Art/Kenney/CarKit/L icense.txt` projede tutulur.

**Kurulum:** Pa keti indirip `Assets/Art/Kenney/CarKit/` alt� �na açın
(`.fbx` + `Textures/` aynı klasö r yapısında kalmalı), sonra menüden:

``` 
Kargo → Asset → Kenney Kurye Aracini Kur 
```

Ya da `Kargo → 0. HER SEYI KUR` tüm  zinciri sırayla çalıştırır.

**Ölçekl eme:** Model ölçüsü `delivery.fbx` içind e 3.25 m; kurulum aracı
hedef boyu **4.20 m* * olacak şekilde `x1.292` ile ölçekler, di ngil
ortasını ve teker izini eksene hizalar , en alta indirir (lastikler
zeminde). Sonuç : **4.20 × 1.94 × 2.13 m** bir panelvan, te ker yarıçapı
**0.30 m**. `VehicleDefinitio n.wheelRadius` buna göre güncellenir; fizik 
(şasi collider'ı, süspansiyon) görselle  birebir örtüşür.

**Tekerler:** Kenney mo delinde tekerler ayrı mesh gruplarıdır
(`w heel-front-left` …). Bunlar `WheelCollider`  nesnelerinin altına
taşınır ve `WheelVis ual` ile fizik pozuna kilitlenir — böylece  hem
**dönüyor** hem de ön tekerler **dire ksiyona uyuyor**. (Önceden
silindir tekerler  takılıydı ve hiç dönmüyordu.)

**Güve nli geri dönüş:** Hazır model kurulu değ ilse `VehiclePrefabBuilder`
primitive yola d� �şer. Yani paket silinse bile oyun yine sür ülebilir
bir araçla açılır.

---

## Kur ye ve fiziksel kargo

Kargo artık **listedek i satıra tıklamakla değil, taşınarak** y üklenir.
Karakter aracı kullanmaz; sürüş  yine araçla yapılır.

| Durum | Ne olur | 
|---|---|
| Depoda `E` | Depo listesi açıl ır |
| `1-9` + `E` | Seçilen kargo için ka rakter aracından inip depoya yürür, kargoy u alır (`pick-up`), araca döner ve yükler  |
| Adreste `E` | Karakter aracından inip ad rese yürür, kargoyu bırakır (`interact-ri ght`) ve aracına döner |

**Anlık yükleme  yok.** Karakter van'a varıp animasyonu biti rmeden kargo
"yüklü" sayılmaz; teslim sür esi de tam o andan başlar. Bu yüzden
oyuncu  acele edip süreyi kendi kaybetmez.

**Karak ter yürürken araç kilitlenir** (`ArcadeVeh icleController.ControlsLocked`).
Oyuncu aynı  anda hem yürüyüp hem araç kullanamaz —  "ben mi yürüyorum,
araç mı?" belirsizli� �i oluşmaz.

**HUD ipucu** o anki durumu yaz ar (`Kurye: Depodan kargo alınıyor` gibi),
 böylece karakter neden hareket ediyor anlaş ılır.

### Neden ayrı bir katman

`CargoSy stem` ve `DeliverySystem` **saf durum mantı� �ıdır**: kargo
yüklendiğinde "yüklü" de rler. Onlar test edilebilir ve deterministik
 kalmalıdır. `CourierController` ise o anın  **görselini ve zamanını** yönetir:
yür� �r, animasyon oynatır, *sonra* mantık siste mine "şimdi yükle" der.

Böylece fiziksel  tasıma eklenirken deterministik çekirdek te stleri
hiç değişmedi. 7 yeni `CourierContr ollerTests` bu ayrımı korur — en
önemlis i `YurumeBitmedenKargoYuklenmez`.

### Kullan ılan animasyonlar

Kenney Mini Characters 32  klip içerir; yalnızca beşi kullanılır
( kullanılmayan klip runtime'da bellek yeridir ):

| Klip | Süre | Kullanım |
|---|---|--- |
| `idle` | 1.33 s | bekleme |
| `walk` | 0. 67 s | yürüme |
| `pick-up` | 0.33 s | karg o alma |
| `holding-both` | 0.17 s | kargo ta şıma |
| `interact-right` | 0.67 s | teslim  |

Menü: `Kargo → 7. Kurye Karakterini Ku r`

---

## Oynanış akışı

Kurye karakte ri oyunun **kontrolünde**: ayaktayken WASD +  fare ile
FPS modunda yürür, araçta aracı  sürer.

| Aşama | Ne yaparsın |
|---|---| 
| **1. Dükkân** | Mavi alandaki dükkâna  gir. Raftaki kutulara yaklaş, `E` ile al (ay nı anda 3 kutu). |
| **2. Aracı yükle** |  Aracın arkasındaki mavi noktaya git, `E`ye  bas. Kutular araca yüklenir. |
| **3. Araca  bin** | Elin boşken aracın yanında `E`ye b as. Kamera araca geçer, araç kilidi açıl� �r. |
| **4. Dağıt** | Adrese git, `E` ile  aracı bırak, eve yürü, kapının önünde  `E`ye bas. |

`E`'nin ne yapacağını **hed ef kendisi söyler** ve ipucu `E`'nin yanınd a
yazar — "E: Kargo al (12 bekliyor)", "E:  Araca koy (2 kutu)",
"E: Araca bin", "E: Karg uyu bırak (3 bekliyor)".

### Neden tıklana bilir hedef sistemi

`IInteractable` sözleş mesi: her hedef (`ShopParcelPile`,
`VanCargoP oint`, `AddressDoor`) kendi ipucunu ve kendi  davranışını
bildirir. Önce `E = yükle`  kuralı `UIManager` içine dağılmıştı; d ört
farklı etkileşim noktası olunca oyunc u ne yapacağını tahmin etmek
zorunda kalı yordu. Artık `E`'ye basmadan önce ne olaca� �ını görüyor.

### Fare ve imleç

Ayakta yken imleç oyuna kilitlenir (standart FPS).  `Esc` imleci serbest
bırakır ve panelleri k apatır; **sol tık** imleci geri kilitler.

 ### Tuşlar

| Tuş | İşlev |
|---|---|
| * *W/A/S/D** | Yürü (bakış yönüne göre)  |
| **Fare** | Bak (imleç kilitliyken) |
| * *Shift** | Koş (kargo taşırken koşamaz) | 
| **Space** | Zıpla |
| **E** | Bağlamsal  eylem: raftan al / araca koy / araktan al / k apıda bırak |
| **F** | Araca bin / araçta n in (araç yavaş olmalı) |
| **1 – 9** |  Depo panelinde kargo seç |
| **Q** | Aracı  boşalt (kargolar depoya **döner**) |
| **E sc** | Panelleri kapat, imleci serbest bırak  |
| **R** | Aracı başlangıç noktasına i ade et |

> **Neden binme `F`:** `E` kargoyla  ilgilenir. Tek tuşta birleştirilseydi
> ar aç doluyken "E: Araca bin" hiç görünmez v e oyuncu hiç binemezdi.
> Ayrıca hızlı ar açtan inmek karakteri aracın altında bıra kıyordu;
> bu yüzden inme yalnızca araç y avaşken çalışır.

### Süre ne zaman ba� �lar

Kargo araca **gerçekten konduğu** and a başlar, raftan alındığında
değil. `Tr yLoadParcel(id, systemTime)` gerçek yükleme  anını alır;
önce `systemTime` hiç geçi rilmiyordu ve sayaç **gün başından** işl iyordu
(öğlen yüklenen kargo doğuştan "g eç" sayılıyordu).

### Araç neden hareket  etmiyor

Karakter ayaktayken araç bilerek k ilitlidir (`ControlsLocked`).
Oyuncu aynı an da hem yürüyüp hem araç kullanamaz; HUD'd a
"Arac kilitli" yazar. Karakter araca bininc e kilit açılır.

---

## Kontroller

| Tu� � | İşlev |
|---|---|
| **W / ↑** | Gaz ( ileri) |
| **S / ↓** | Fren / geri vites |
 | **A / ← · D / →** | Direksiyon |
| **S pace** | El freni (yalnızca arka tekerlere)  |
| **R** | Aracı başlangıç noktasına ia de et |
| **E** | **Ana eylem tuşu** (bağla ma göre aşağıya bak) |
| **1 – 9** | De poda kargo seç |
| **Q** | Aracı boşalt |
 | **Tab** | Depo listesini aç / kapat — *y alnızca alias* |
| **Esc** | Panelleri kapat  |

**`E` bağlama göre çalışır** — te k bir "ana eylem" tuşu, üç farklı durum:
 
| Durum | `E` ne yapar |
|---|---|
| Depoda,  panel kapalı | Depo listesini açar |
| Dep oda, panel açık | Seçili kargoyu yükler ( panel açık kalır, art arda yüklenebilir)  |
| Duraktayken | Sıradaki kargoyu teslim ed er |

> **Neden `Tab` birincil değil:** Unit y Editor `Tab`'ı odak
> değiştirme için k ullandığı için oyun içinde Editor'da ça lıştırırken
> `Tab` tuşu oyuncuya ulaşm ayabiliyordu. Bu yüzden birincil tuş `E`
>  yapıldı, `Tab` build'de çalışan alternat if olarak kaldı. Gerçek
> Build'de ikisi de  tanımlıdır.

Ayrıca HUD'da o anki bağla m yazılı (`[E] Depoyu ac`, `[E] Yukle`,
`[E ] Teslim et` …); oyuncu tuşları tahmin et mek zorunda kalmaz.

Detaylı oynanış: bak� �nız *Aşama 6 — Arayüz*.

---

---

---
 
## Aşama 2 — Araç

| Script | Görevi |
 |---|---|
| `Vehicle/VehicleDefinition` | Ara ç özellikleri (SO): hız, tork, fren, direk siyon, tutunma, süspansiyon, **kargo kapasit esi** |
| `Vehicle/IVehicleInput` | Sürücü  sözleşmesi: `Throttle`, `Steer`, `Handbrak e`, `RespawnRequested` |
| `Vehicle/ArcadeVeh icleController` | Fizik: motor, fren, direksi yon, yanal tutunma, downforce, takla korumas� � |
| `Player/PlayerController` | Klavye →  girdi |
| `Player/CameraFollow` | Takip kamer ası, hıza göre FOV |
| `Player/CourierCont roller` | Kargoyu depodan araca ve adresten a raca taşıyan karakter |
| `Editor/VehiclePr efabBuilder` | Prefab'ı üretir, sahneye koy ar, doğrular |
| `Editor/KenneyAssetSetup` |  Hazır CC0 modeli (Kenney Car Kit) içe alı r, aracı bu modelle üretir |
| `Vehicle/Whe elVisual` | Teker mesh'ini `WheelCollider` po zuna kilitler (dönme + direksiyon) |

**Tasa rım kararı:** Fizik matematiği `static` sa f fonksiyonlarda
(`ComputeSteerAngle`, `Compu teMotorTorque`, `ComputeBrakeTorque`).
EditMo de testleri formülleri sahne açmadan doğru lar; PlayMode testleri
gerçek fizik motorund a sürüşü VE oyunun tüm akışını doğr ular.

Menü: `Kargo/Vehicle/Arac Prefabini K ur`

**Tasarım kararı:** Gövdede `BoxColli der` **zorunludur**
(`[RequireComponent]`). S adece tekerlerden oluşan bir araç dururken  sorunsuz
durur ama ilk gazda takla atar — P layMode testinde ölçüldü.

---

## Aşama  3 — Kargo sistemi

| Script | Görevi |
|- --|---|
| `Cargo/ParcelType` (SO) | Kargo tü rü tanımı: ağırlık/hacim aralığı, kg  başına ücret, kırılabilirlik |
| `Cargo /ParcelPriority` | Öncelik enum'u + ödeme/s üre/başarı çarpanları |
| `Cargo/ParcelS tate` | Beklemede / aracta / teslim edildi |
 | `Cargo/Parcel` | Tek kargonun verisi (saf C #) |
| `Cargo/ParcelGenerator` | Günlük kar go üretimi — **deterministik** (`System.Ra ndom` + gün tohumu) |
| `Cargo/CargoManifest ` | Depo listesi + araç yükü, **ağırlık  VE hacim** kontrolü |
| `Cargo/CargoSystem`  | Orkestrasyon: gün başında üretim, yük leme isteği, olay yayını |
| `Editor/Conte ntSetupTool` | 6 kargo türünü üretir, Com position Root'a bağlar |

Menü: `Kargo/3. I cerik Assetlerini Kur`

### Kapasite iki ayr� � sınırdır

| Durum | Sonuç |
|---|---|
|  ağır ama küçük | ağırlık dolar, haci m boş kalır |
| hafif ama devasa | hacim do lar, ağırlık boş kalır |

Tek sınır ol saydı oyuncu devasa ama boş paketlerle sın ırsız yük taşırdı.

### Determinizm ned en önemli

`ParcelGenerator` `UnityEngine.Ra ndom` değil `System.Random` kullanır ve
toh umu gün numarasından türetir (`day * 7919` ). Aynı gün → aynı kargolar.
Böylece "5 . günde hangi kargolar vardı" sorusu save d osyasından yeniden
kurulabilir; testler de k ararlı olur.

### Bu aşamada olmayanlar (bi linçli)

Teslimat ve ödeme **Aşama 4/5**.  `Parcel.DestinationId` şimdilik opak bir
tam sayı; gerçek koordinatlara Aşama 4'te bağ lanacak. `Parcel.BaseValue`
kargonun kendi de ğerini tutar, ödeme hesabını ekonomi sist emi yapar — iki
yer hesap yapmaz.

---

##  Aşama 4 — Teslimat sistemi

| Script | Gö revi |
|---|---|
| `Delivery/MapLayout` (SO)  | Teslimat noktaları (id + konum) + depo kon umu |
| `Delivery/DeliveryOutcome` | Başarı lı / geç kaldı / başarısız + ödeme ça rpanı |
| `Delivery/DeliveryStop` | Bir adre se gidecek kargolar (aynı adres = tek durak)  |
| `Delivery/RoutePlanner` | En-yakın-kom� �u rota sıralaması (saf, statik) |
| `Deliv ery/DeliverySystem` | Süre takibi, varış a lgısı, teslim, gün sonu |
| `Editor/Delive rySetupTool` | 14 teslimat noktası üretir ( iki halka), sahneye bağlar |

Menü: `Kargo/ 4. Harita ve Teslimat Noktalarini Kur`

### S üre yüklenme anından işler

Teslim süres i gün başında değil, kargo **araca yükle ndiği anda** başlar.

Nedeni: Gün başınd a başlasaydı 12 kargonun hepsi aynı anda d olar ve süre
kavramı anlamını yitirdi. Y� �klenmeden başlayınca "çok kargo yükleme"  ile
"az kargo yükleme" arasında gerçek bi r tercih ortaya çıkıyor.

### Rota neden b asit

En-yakın-komşu (greedy) kullanılıyo r. Optimal çözüm (TSP) 14 noktada bile
pah alı ve oyuncuya kazancı görünmez; önemli  olan oyuncunun sırayı
**görebilmesi** (A� �ama 6'da minimapta çizilecek). Rota, giriş  listesini
değiştirmeden sıralı kopya dö ndürür — yan etkisiz ve test edilebilir.
 
### Teslimat sonucu

| Sonuç | Koşul | Etk i |
|---|---|---|
| `Success` | süre dolmada n | tam ödeme + XP |
| `Late` | süre geçti kten sonra | kısmi ödeme, az XP |
| `Failed ` | gün sonunda teslim edilmemiş | ödeme y ok, itibar kaybı |

Para ve XP **Aşama 5**.  `DeliverySystem` yalnızca sonucu bilir;
hes aplamayı ekonomi sistemi yapar.

### Rota ke ndini iyileştirir

Rota, event'e değil **y� �klü kargo sayısına** bakarak yenilenir. K argo
başka bir yoldan yüklenirse (test, ile ride araç değişimi) rota bayat
kalmaz. Tes t bunu doğruluyor.

### Bu aşamada olmayanl ar

Binalar, yollar, görsel teslimat noktala rı **Aşama 8**. Şu an
`MapLayout` iki halk adan oluşan geçici bir ızgaradır; Aşama  8'de aynı
kimlikli gerçek binalar bu konuml ara bindirilecek — asset değişmeyecek.

- --

---

## Aşama 5 — Para + XP/level

| S cript | Görevi |
|---|---|
| `EconomyConfig`  (SO) | Para + XP + seviye eğrisi ayarları  |
| `Payout` | Hesap sonucu: ödeme, bahşiş , ceza, XP |
| `PayoutCalculator` | **Saf sta tik formül** — kargo + sonuç → ödeme | 
| `PlayerProgress` | Bakiye, XP, seviye, gü n istatistikleri |
| `DaySummary` | Gün sonu  özeti |
| `EconomyService` | Teslimat olayl arını dinler, ödemeyi uygular |

Menü: `K argo/5. Ilerleme Ayarlarini Kur`

### Ödeme  formülü

```
ödeme  = kargo değeri × son uç çarpanı               (1.0 / 0.4 / 0.0) 
bahşiş = kargo değeri × bahsisOranı ×  kalanSüreOranı   ← sadece Success'te
XP      = (xpPerDelivery + xpPerKg × ağırlık)  × sonuç çarpanı
```

Bahşiş kalan süre yle ölçeklenir: süre tam kaldıysa tam bah şiş, eşiğin
altındaysa hiç. Böylece "h ızlı teslim = daha çok kazanç" ayrı bir  kurala
gerek kalmadan doğal çıkar.

### Ba şarısızlık cezası yok

Varsayılan `fail edDeliveryPenalty = 0`. Başarısız teslim * *para
kazandırmaz**, ama para da götürmez.  Gerekçe: negatif bakiye sinir
bozucudur ve  oyuncuyu kargoyu hiç yüklememeye iter. Bede li zaten zaman
kaybıdır — teslimatın öd enmemiş olması yeterince ağır.

### XP e� �risi (kritik)

Her seviye **atlama maliyeti* * artar; üs toplama uygulanmaz:

| Seviye |  Atlama maliyeti | Kümülatif |
|---|---|---| 
| 1 → 2 | 100 | 100 |
| 2 → 3 | 135 | 23 5 |
| 3 → 4 | 182 | 417 |

Üs toplama uygu lanırsa ilk atlama (100 XP) ikincisinden (35  XP) pahalı
çıkar; oyuncu ilk seviyede tak ılıp sonra hızla yükselir. Test bu hatay� �
yakaladı ve eğri düzeltildi.

### Gün d öngüsü

Saat 24:00 olduğunda sırayla: te slim edilmemişler başarısız sayılır → 
gün özeti üretilir → istatistikler sıf ırlanır → yeni günün kargoları
üretil ir.

Dinleme `DayStarted` / `DayEnded` üzeri nden yapılır; sistemler birbirini
tanımaz,  hepsi olay kanalından haber alır.

> **Bul unan hata:** Kargolar yalnızca `Start()`'ta  bir kez üretiliyordu —
> 2. gün depoda ** hiç kargo kalmıyordu**. Regresyon testi ekl endi.

### Bu aşamada olmayanlar

UI (Aşama  6). Para hareketleri şu an sadece sayı; ek randa görünmüyor.
Araç yükseltme mağaza sı, itibar ve şirket büyümesi bu aşaman� �n
kapsamında değil.

---

---

## Aşama 6  — Arayüz

| Script | Görevi |
|---|---|
 | `UI/UITheme` | Tüm renkler ve ölçüler � �� tek kaynak |
| `UI/UIManager` | Tek giriş  noktası: olay bağları, girdi, minimap |
|  `UI/HudView` | Para, seviye/XP, saat, kapasi te, sonraki durak |
| `UI/DepotView` + `Parce lRowView` | Depo listesi, yükleme, satır ha vuzu |
| `UI/StopView` | Adrese varınca aç� �lan teslimat paneli |
| `UI/DaySummaryView`  | Gün sonu özeti |
| `UI/MinimapView` | Dep o, duraklar, rota çizgisi, araç |
| `Delive ry/StopMarker` | Dünyada renkli zemin işare ti + etiket |
| `Editor/UiSetupTool` | Tüm a rayüzü sıfırdan kurar |

Menü: `Kargo/6.  Arayuzu Kur`

### Oynanış akışı

```
De poya git (mavi alan)  →  Tab            →  kargo listesini aç
                             tıkla / 1-9  → kargo seç
                             E             → yükle    ( Q: hepsini boşalt)
                             Tab           → kapat

Renkli kareye git         →  panel otomatik açılır
                             E             → teslim  et (her basışta bir kargo)
```

Minimap (s ağ üst) ve dünyadaki renkli işaretler hed efi gösterir.

### Performans: iki kural

** 1. Tek `Update` kuralı bozulmadı.** `UIMana ger.Update()` yok —
`CompositionRoot` her k arede `uiManager.Tick(dt)` çağırır. `Stop Marker`'ın
da `Update`'i yok: 14 marker içi n 14 geri çağrı gereksiz maliyettir.
Etike t yere yatay durur, kamera zaten üstten bakt ığı için okunur ve kare
maliyeti sıfırd ır.

**2. Metinler dirty-check ile yazılır .** `HudView.Refresh` her karede
çağrılır  ama metni yalnızca **değer değiştiğinde ** günceller. Aksi halde
saniyede 60 string  ayrıştırma olurdu.

Satırlar **havuzlanı r** — depo her açılışta GameObject üre tmez.

### Bulunan hata (editör aracı)

`De liverySetupTool`, 14 işareti oluşturduktan  **sonra** sahneyi
`OpenScene(Single)` ile yen iden açıyordu; kaydedilmemiş işaretler yo k
oluyordu. Sahne artık **başta** açılıy or, işaretler sonda yazılıyor.

> **Kural: ** Editör aracı sahneye nesne ekliyorsa sah neyi **tam başta bir
> kez** açmalı. Ortad a yeniden açmak kaydedilmemiş her şeyi sil er.

### Panel görünmüyordu (manuel testte  bulundu)

İlk manuel oyunda depo ve teslima t panelleri hiç açılmıyordu; oyuncu
"Tabl o/E çalışmıyor" sanıyordu. Gerçek neden  tuşlar değil, kurulum
aracıydı:

`UiSetu pTool`, panelin **kök** `GameObject`'ini kap atıyordu
(`go.SetActive(false)`), sonra "pan eli gizle"mek için içteki `panel`
çocuğun u kapatıyordu. `DepotView.Open()` ise yalnı zca **çocuğu**
açıyordu. Unity'de inactiv e bir nesnenin çocuğu da render edilmez — 
komut çalışıyor, hiçbir şey görünmü yordu.

Çözüm: kökler her zaman açık, g izleme yalnızca iç çocukta yapılır.
`Pan elKokleriAcikKalmali` testi bu kuralı koruyo r.

Ayrıca **`Tab` Unity Editor tarafından  yutuluyor** (odak değiştirme).
Bu yüzden b irincil eylem tuşu `E` yapıldı, `Tab` alia s kaldı; HUD
her an ne yapılabileceğini ya zıyor.

### HUD yerleşimi

Para/seviye pane li ve minimap **sol alta** alındı (istek ü zerine):
bilgi paneli sol alt köşede, minim ap hemen sağında, hedef/ipucu
metinleri alt  orta. Eskiden HUD sol üstte, minimap sağ � �stteydi.

### Bu aşamada olmayanlar

Yakıt , görevler (Mission) ve kayıt (Save) hâlâ  yok. Yakıt Aşama 2'de
bilinçli olarak ert elenmişti: depoya yakıt alma mekaniği karg o sistemiyle
birlikte çalışması daha doğ ru olur.

---

## Aşama 7 — Kayıt / Yükl eme

Oyuncu ilerlemesi `persistentDataPath/ka rgosave.json` dosyasına yazılır.
Kayıt ** gun sonunda otomatik** alınır (`GameComposi tionRoot.autosaveOnDayEnd`).

| Kaydedilen |  Geri yüklenen |
|---|---|
| Gün, saat | Gü n, saat |
| Para, XP, seviye | `PlayerProgres s.RestoreFromSave` |
| Kargolar (tür, alıc� �, adres, ağırlık, hacim, öncelik, süre,  durum) | Yeniden üretilip depoya konur, ara çtakiler tekrar araca yüklenir |
| Son karg o kimliği | Kimlik çakışması olmaması i çin zorlanır |

**Neden kargolar JSON'dan o kunmuyor, yeniden üretiliyor:** `Parcel`
iç  durum taşıyan düz bir sınıf; onu katman  katman geri kurmak yerine
kayıttaki girdile rden (`ParcelGenerator` ile birebir aynı for mül) yeniden
hesaplanıyor. Böylece kurguda ki bir değişiklik eski kayıtları bozmaz.
 
**Neden `nextParcelId` dört katmanda zorlan ıyor:** `Capture` → `TrySave`
→ `Load` � �� `Restore`. Kimlik çakışması olursa oyu ncu aynı kargoyu iki
kez üretir, iki kere t eslim eder ve iki kere para kazanır. Bozuk b ir
sayaç sessizce düzeltilir.

**Teslim edi lmiş kargolar geri yüklenmez.** Depoya konu rlarsa oyuncu
aynı gün onu tekrar yükleyip  teslim edebilir ve ödeme iki kez yazılır. 

Kayıt **opsiyoneldir**: dosya bozuksa veya  yazılamazsa oyun durmaz,
konsola uyarı ver ir ve yeni oyunla devam eder.

## Aşama 8 � � Ses (prosedürel)

Projede **hiç ses dosya sı yok ve gerekmiyor**: tüm efektler kod il e
sentezleniyor (`AudioClip.Create` + `Mathf. Sin` + kendi LCG tabanlı
gürültü üretici si). Toplam bellek ~220 KB, lisans sorunu yok .

| Kuyruk | Tetikleyici |
|---|---|
| Kutu  al | `CargoChanged` |
| Araca koy | `ParcelLo aded` |
| Teslim başarılı / geç | `Parcel Delivered` (sonuç başarılıysa) |
| Teslim  başarısız | `ParcelDelivered` (sonuç ba� �arısızdaysa) |
| Para kazanıldı | `Payou tApplied` (ödeme > 0 ise) |
| Seviye yüksel di | `LevelUp` |
| Ayak sesi | Karakter yür� �rken `Tick` ile aralıklı |
| Motor | Araç  hızına göre pitch; döngüsel, dikişsiz  |

`UnityEngine.Random` **kullanılmaz**: ayn ı parametre her zaman aynı
örneği üretir , testler tekrarlanabilir olur.

Susturma ses i bastırmakla değil **karar zincirini keser ek** yapar
(`TotalCuesPlayed` artmaz) — bu  sayede testler sesi duymadan da
doğrulayabil ir. `Awake`'de abone olur, `OnDestroy`'da **k esinlikle**
çıkar (Domain Reload kapalıyke n sızıntı olur).

## Aşama 9 — Şehir d ekoru

`MapLayout` yalnızca nokta listesi ve rir; dekor aracı o bilgiyi
sahnenin üstüne  kurar (asset değişmez).

| Öğe | Miktar  | Kural |
|---|---|---|
| Yol | 28 segment +  depo alanı | 5 m genişlik, zemine 0.06 m (p ed görünsün) |
| Kaldırım | 14 | Kapı � �nü şeridi |
| Bina | 14 adres × 2 | Adres  başına 2, renk kimliğe göre |
| Sokak la mbası | 8 | Yol kenarında |
| Ağaç | 36 ( tavan) | Yol boyunca seyrek |

Toplam ~155 Ga meObject (bütçe 300).

**İki kural dosyay� � belirler:**

1. **Çakışma yok.** Her nes ne ayak izinde 9 örnek nokta alır ve
   ped  / kapı / dükkân / araç / kurye / yol pay ına göre sınanır.
   Sığmayan aday **at lanır** — zorla yerleştirilen bina oyuncu yu kapatırdı.
2. **Belirlenimcilik.** Konum lar `Mathf.Sin` tabanlı seudo-rastgele
   il e üretilir, tohum sabit: menüye iki kez bas mak aynı şehri verir.

Kurulum sonrası `Ve rify` **sayısal** rapor verir: en yakın dek or-ped
mesafesi, zemin altında kalan bina sa yısı, nesne bütçesi.

---

| # | Aşama |  Durum |
|---|---|---|
| 1 | Proje + ayarlar  + çekirdek | ✅ |
| 2 | Player + araç kont rolü | ✅ |
| 3 | Kargo sistemi | ✅ |
| 4  | Teslimat sistemi | ✅ |
| 5 | Para + XP s istemi | ✅ |
| 6 | UI | ✅ |
| 7 | Save/Lo ad | ⬜ |
| 8 | Harita ve içerik | ⬜ |
|  9 | Optimizasyon | ⬜ |
| 10 | Polish ve yay ın hazırlığı | ⬜ |

**Kural:** Her aş ama Play'de test edilmeden sonrakine geçilme z.

---

## Bilinen teknik notlar

- **Domain  Reload kapalı** (Editör > Project Settings  > Editor > Enter Play Mode).
  Bu yüzden st atik event'ler `GameEvents.ResetStatics()` il e temizlenir.
  Statik değişken eklerken bu nu unutmayın.
- **URP 17'de** `supportsMainL ightShadows`, `supportsSoftShadows`,
  `addit ionalLightsRenderingMode` setter'ları `inter nal`'dır.
  `ProjectSetupTool.SetPrivateFiel d()` ile `SerializedProperty` üzerinden yaz� �lır.
- **`Time.fixedDeltaTime` kalıcı de� �iştirilemez.** Unity 6 bunu
  `TimeManager. asset` içinde rasyonel olarak saklar (`m_Cou nt / m_Numerator`).
  Ayar aracı YAML'ı bu  formata göre günceller.
- **Blender kurulu  değil.** Görseller hazır **CC0** model
  p aketlerinden geliyor (bkz. *Görseller*), Ble nder'a gerek yok.

### Bilinen Unity tuzaklar ı (bu projede yaşandı)

**0. Editor kurulu m aracı, nesne silmeden "temizlik" yaparsa y ığılmaya
yol açar.**
`DeliverySetupTool.B uildMarkers` her çağrıda yeni bir `Deliver yStops`
kökü açıyor, eski işaretleri sil meyi de o **yeni boş** kökte
arıyordu. Ara ma hiçbir şey bulamadığı için işaretle r üst üste
birikiyordu: harita iki kez kuru lunca 14 noktaya karşılık sahnede
**28** p ed vardı. Oyuncu aynı hedefi iki kez görü yordu.

İki düzeltme yapıldı: (a) temizli k artık tüm sahne köklerini tarayarak
`Del iveryStops` kopyalarını siliyor, (b) `Durak IsaretleriSahnede`
testi artık sadece "var m ı" değil, **sayının harita nokta sayısı na
birebir eşit** olduğunu doğruluyor. (Es ki test `> 0` idi ve 28 ped'de
sessizce geçi yordu.)

**0b. Mesh'lerin yerel sınırların ı birleştirirken transform'un
kendisi hesab a katılmalı.**
Araç modelini ölçerken `M eshFilter.sharedMesh.bounds` değerleri
doğr udan `Encapsulate` edildi. Ancak Kenney model inde tekerler modelin
içinde kaydırılmış  alt nesneler; kendi transform'ları yok say� �lınca
tekerler modelin merkezinde varsayıl ıyor ve **Y boyutu 3.23 m** gibi
yanlış ç ıktı (doğrusu 2.13 m). Çözüm: her mesh' in 8 köşesi
`Transform.TransformPoint` ile  istenen uzaya taşınarak birleştirilir.
Bu  yöntem `Renderer.bounds`'un kare gecikmesind en de etkilenmez.

**1. Script assembly taş� �nırsa eski asset'ler bozulur.**
Unity, Scri ptableObject ve MonoBehaviour asset'lerine
`m _EditorClassIdentifier` (örn. `KargoSim.Runt ime::KargoSim.Core.GameConfig`)
yazar. Script  `Assembly-CSharp`'ten `KargoSim.Runtime`'a t aşınırsa bu
kimlik bayat kalır ve `LoadAs setAtPath<T>()` **NULL** döner — üstelik
 `LoadAllAssetsAtPath()` doğru tipi gösterdi ği için hata yanıltıcıdır.
Belirti: sah nede referans GUID'i doğru ama runtime'da bo ş.

Çözüm: `Library` klasörünü silip p rojeyi yeniden aç.
`ProjectSetupTool.LoadGam eConfig()` önce `ImportAsset(ForceUpdate)` d ener.

**2. `Plane` (MeshCollider) araç taş ıyamaz.**
`PrimitiveType.Plane` tek parça b ir quad'dır ve `WheelCollider` ile
güvenili r çarpışmaz. PlayMode testinde ölçüldü : araç zemine değip
hemen içinden geçiyor du (y = -26 m). `Cube` (BoxCollider) ile ayn� �
sahne kusursuz çalışıyor (y = 0.15). Bu  yüzden zemin `Cube` ile üretilir.

**3. Ar acın gövdesinde collider olmazsa ilk gazda  takla atar.**
Sadece 4 `WheelCollider`'dan ol uşan bir araç, fizik motoru için
kararsız dır: dururken sorunsuz durur, ama ilk motor  torkunda takla atar.
Ölçülen sonuçlar (12 00 kg, 2000 N):

| Gövde collider | İtki |  Sonuç |
|---|---|---|
| yok | `AddForce` | a zami eğilme 164°, y = -76 m |
| yok | `moto rTorque` | azami eğilme 173°, y = -94 m |
|  var | `AddForce` | kararlı ama **hareket et miyor** (0.00 m) |
| var | `motorTorque` | ** kararlı, 2.4 sn'de 16.7 m** ✓ |

Gövde co llider'ı kök nesnedeki `BoxCollider`'dır
( `[RequireComponent]` ile otomatik gelir). Gö rsel mesh'ler (`Body`, `Cabin`,
`Bumper_*`) c ollider taşımaz. **Buraya ikinci bir `Chass is` çocuğu
eklemek iki collider yaratır ve  aracı yerinde sıkıştırır.**

**4. Duru şta fren uygulanırsa motor kazanamaz.**
Yer leştirme kaynaklı 0.02 m/s'lik geriye kayma da "gaz basılı ama hız
ters yönde" kural� � devreye girip tekeri 3500 Nm ile kilitliyor du.
`ComputeBrakeTorque` artık `|hız| < 0.1 ` iken fren uygulamaz.

**5. Direksiyon eğri si `1/(1+hız·k)` ise yüksek hızda araç d önmez.**
Bu eğri 10 m/s'te açıyı %9'a (2 .9°) düşürüyordu. Yerine doğrusal
enter polasyon: dururken tam açı, azami hızda `m inSteerFactor` (varsayılan
%35). 10 m/s'te 2 5°, 30 m/s'te 11°.

**6. Test zemini yeteri nce büyük olmalı.**
Azami hız testi 12 sa niye tam gaz gidiyor; araç yüzlerce metre y ol
alıyor. 400×400 bir zemin yeterli değil  — araç kenardan "düşüp"
boşluğa kaç ıyor ve hız testi sahte şekilde başarıs� �z oluyor.
Test zemini 10000×10000.

**7. `T ime.fixedDeltaTime` kalıcı değiştirilemez .**
Unity 6 bunu `TimeManager.asset` içinde  rasyonel saklar
(`m_Count / m_Numerator`, var sayılan `2822399/141120000 = 0.02`).
Satır  sonu LF'dir ve değerden sonra virgül yoktur .

**8. URP 17'de bazı ayarlar `internal set `.**
`supportsMainLightShadows`, `supportsSof tShadows`,
`additionalLightsRenderingMode` d� �şarıdan atanamaz →
`SerializedProperty`  gerekir (`ProjectSetupTool.SetPrivateField`). 

**9. `Physics.autoSyncTransforms` Unity 6'd a deprecated.**
Her zaman kapalı; transform  değişikliklerini elle taşımak gerekirse
` Physics.SyncTransforms()` çağrılmalı.

** 10. `EditorUtility.DisplayDialog` batchmode'd a kullanılamaz.**
Sahne yeniden oluşturma y alnızca menüden çağrıldığında onay is ter.

**11. MonoBehaviour `new` ile oluşturu lamaz.**
`new FakeDriver()` Unity'de "sahte n ull" döner (`== null` **true**).
`AddCompone nt<T>()` kullanılmalı.

**12. PlayMode'da ` AddComponent` anında `Awake` çağırır.**
 Girdi henüz atanmamışsa `Awake` hata logla r ve test `SetUp`'ta düşer.
Nesneyi **pasif ** kurup (`SetActive(false)`), bağlantılar� � yapıp sonra
aktifleştirmek gerekir — bu , prefab akışının da gerçekte nasıl
ça lıştığını yansıtır.

---

## Lisans

MIT — [`LICENSE`](LICENSE) dosyasina bakiniz.

### Varliklar icin istisna

Bu depodaki **kod** MIT ile lisanslidir. Icerim olarak kullanilan
paketler kendi lisanslarini tasir:

| Paket | Lisans |
|---|---|
| Kenney Arc Vehicles / Car Kit | CC0 (her ticari kullanim serbest) |
| Unity Motor | Unity'in kendi lisans sartlari |
| Diğer paketler | `Packages/manifest.json` icindeki ilgili satir |

Ses olarak kullanilan Kenney paketleri **ticari lisansla satin alinmistir**
ve bu depoya dahil **edilmemistir**; orneklerdeki sesler procedurel olarak
Unity AudioClip ile uretilir.
