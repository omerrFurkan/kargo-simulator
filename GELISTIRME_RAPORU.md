# GELİŞTİRME RAPORU — KARGO SİMÜLATÖRÜ DEMO 1

> Rapor biçimi görev metni §100'e göre hazırlanmıştır.
> Tarih itibarıyla oturum: görev metni `görevlerin.txt` uygulanıyor.

---

## 1. TASK SUMMARY

Görev: **Demo 1'i baştan sona gerçek bir oyuncunun debug müdahalesi olmadan
oynayabileceği hâle getirmek.**

Bu oturumda yapılan iş, yeni sistem eklemek değil, **mevcut oynanışın
dayanıklılığını doğrulamak ve bozuklukları kapatmak** oldu.

Oyun zaten büyük ölçüde çalışıyordu; asıl sorun **bireysel hataların
birbirini nasıl etkilediği** idi. Bu oturumda kargo hayatta kalma
(muhafaza) kuralının boşluğu kapatıldı.

**Sonuç özeti:** Otomatik doğrulama tam ve yeşil. Manuel doğrulama
henüz yapılmadı. Bu nedenle nihai hüküm **NO**'dur — ama bu "oyun
bozuk" demek değildir, "insan eliyle doğrulanmadı" demektir.

---

## 2. COMPLETED SYSTEMS

Bu oturumda **doğrulanan** (yeni yazılmayan) sistemler:

| Sistem | Durum | Doğrulayan test |
|---|---|---|
| Kargo yaşam döngüsü (al → taşı → bırak → araca → adrese) | ✅ | `PlayabilityGateTests` |
| Kargo muhafazası (hiçbir geçişte kaybolmaz) | ✅ **yeni** | `CargoConservationTests` |
| Depo müşterisi → paketli kutu | ✅ | `ShopCustomerFlowTests` |
| Araç kargo alanı (yükle / çıkar) | ✅ | `RealPathLoadingTests` |
| Bisiklet: bin / in / kamera / fizik / fren / dönüş | ✅ | `CargoBikePlayModeTests`, `BicycleTurningTests` |
| Teker–gövde hizası (her iki araç) | ✅ | `WheelAxleOrientationTests`, `VanWheelPlacementTests` |
| Kapı çalma + zil | ✅ | `NpcDeliveryPlayModeTests` |
| Teslim kodu (doğru / yanlış / tekrar teslim) | ✅ | `NpcCodeDeliveryTests` |
| Ekonomi + XP + seviye | ✅ | `EconomyTests` |
| Gün döngüsü (08:00, 300 sn) | ✅ | `GameClockTests` + config doğrulaması |
| Kayıt / yükleme | ✅ | `SaveSystemTests` |
| HUD köşe yerleşimi | ✅ | `HudLayoutTests` |
| Eşya özellik penceresi | ✅ | `ParcelTooltipTextTests` |

**Doğrulanan tasarım kuralları:**
- §46 — Gün 08:00'de başlıyor, 1 gün = 300 saniye (5 dakika). ✔
- §7 — 6 kargo türü, brief'teki ağırlık/hacim değerleriyle birebir. ✔
- §15 — Bisiklet 60 kg / 150 L, 3 teker; vapur 350 kg / 900 L, 4 teker. ✔
- §12/13 — Paketleme sistemi **korundu**; müşteri kutuları hazır geliyor (§27 ile uyumlu). ✔

---

## 3. CREATED FILES

**Bu oturumda:**

| Dosya | Ne işe yarar |
|---|---|
| `Assets/Tests/PlayMode/CargoConservationTests.cs` | Görev §26 + §60 — kargo muhafazası, 7 test |
| `OYUN_BRIFINGI.md` | Oyunu hiç bilmeyenler için brifing + ilk demo kapsamı |
| `görevlerin.txt` | (Kullanıcıdan geldi, dokümana alındı) |

**Önceki oturumlarda oluşturulan ve hâlâ duran:**

| Dosya | Ne işe yarar |
|---|---|
| `Assets/Scripts/Player/ShopCustomer.cs` | Depoya gelen müşteri, paketli kutu verir |
| `Assets/Scripts/UI/ParcelTooltipView.cs` | Fare ile eşya özellik penceresi |
| `Assets/Tests/PlayMode/PlayabilityGateTests.cs` | Uçtan uca oynanabilirlik kapısı |
| `Assets/Tests/PlayMode/ShopCustomerFlowTests.cs` | Müşteri → kutu → araç zinciri |
| `Assets/Tests/PlayMode/RealPathLoadingTests.cs` | Gerçek `E` yoluyla yükleme |
| `Assets/Tests/PlayMode/VehicleBoardExitTests.cs` | Doğru araçtan inme |
| `Assets/Tests/PlayMode/WheelAxleOrientationTests.cs` | Modelden bağımsız teker aksı ölçümü |
| `Assets/Tests/PlayMode/VanWheelPlacementTests.cs` | Tekerler gövde içinde mi |
| `Assets/Tests/PlayMode/BicycleTurningTests.cs` | Sol/sağ dönüş simetrisi |
| `Assets/Tests/EditMode/ParcelTooltipTextTests.cs` | Özellik penceresi metni |

---

## 4. MODIFIED FILES

**Bu oturumda değişen (kod değişikliği YOK, yalnızca test):**
- Test dosyaları dışında hiçbir oyun kodu değiştirilmedi.

**Önceki oturumlarda değişen ve teslim edilen:**

| Dosya | Değişiklik |
|---|---|
| `Assets/Scripts/Vehicle/BicycleController.cs` | Anti-roll torku yanlış eksende (`up`) uygulanıyordu → `forward`. Dönüş 12° → 92°. Azami yatma sınırı (4°). |
| `Assets/Scripts/Vehicle/WheelVisual.cs` | `Awake` ile fizik bağlantısı kurulur (runtime'da hiç kurulmuyordu → tekerler dönmezdi). Duruş ofseti eklendi. Referans çerçeve tekerin ebeveyni oldu (konum iki kez ekleniyordu). |
| `Assets/Scripts/Cargo/Parcel.cs` | `Carried = 6` durumu eklendi (saya sona). |
| `Assets/Scripts/Cargo/CargoManifest.cs` | `_carried` listesi, `UnloadParcel`, `TryLoad` eldeki kutuyu kabul eder, `MarkDelivered` iki listeden de çıkarır, `FindById` `_carried`'ı arar. |
| `Assets/Scripts/Delivery/DeliverySystem.cs` | Rota **araçta + elde** kargolardan kurulur. |
| `Assets/Scripts/Player/VanCargoPoint.cs` | Araçtan alma `UnloadParcel` kullanır. |
| `Assets/Scripts/UI/UIManager.cs` | Araçtayken en yakın araç yerine **aktif araç** kullanılır. Zamanlı ipucu mesajı. Tooltip. |
| `Assets/Scripts/UI/HudLayout.cs` (yeni) | HUD köşelere sabitlendi. |
| `Assets/Scripts/Core/GameCompositionRoot.cs` | Kamera eşleştirme (`WireVehicleCameras`), müşteri üretimi, envanter panel iskeleti. |
| `Assets/Scripts/Player/CourierController.cs` | `ApplyCameraState` (tek kamera kuralı), `IsCarryingParcel`, çıkışta kamera sıfırlama, `LastActionHint`. |
| `Assets/Scripts/Player/ShopParcelPile.cs` | Eldeki kutuyu atlama (aynı kutu 3 kez alınıyordu). |
| `Assets/Scenes/Game.unity` | Kamera etiketleri, kamera başlangıç durumu, teker rotasyonları, HUD kökü. |

---

## 5. UNITY SCENE CHANGES

- `Main Camera` etiketi `FpsCamera`'ya taşındı (`Camera.main` artık oyuncunun kamerası; araç kameraları ikincil).
- FPS kamera açık, van kamerası kapalı başlıyor (oyuncu yaya başlıyor).
- `CargoBike.prefab` — 3 `WheelVisual` için duruş ofseti; `antiRollStrength` 60 → 130.
- `PlayerVehicle.prefab` — 4 `WheelVisual` için duruş ofseti (−90°).

**Sahne elle düzenleme dersi:** Bu görevde sahnenin `YAML` dosyası elle
düzenlendi ve **üç ayrı seferde bozuldu** (kamera bağlantısı, etiketler,
`RectTransform`). Sonuç olarak müşteri, envanter ve HUD gibi nesneler
**üretim kodundan** kurulur. `CARGO.md` notu: sahne, kodun türevi; kod
doğrusu.

---

## 6. ARCHITECTURE CHANGES

1. **Kutu modelinde "elde" gerçek bir durum oldu.** Daha önce kurye elindeki
   kutu manifestin araç listesinde kalıyordu; hem kasada hem elde sayılıyordu.
   Bu çözülmeden **envanter paneli** ve **iki yönlü transfer** mümkün değildi.
   Rota "araçta + elde" listesinden kurulacak şekilde değiştirildi.

2. **Kamera sahipliği merkezileştirildi.** `CourierController.ApplyCameraState()`
   sahnedeki tüm kameraları tarar ve yalnızca gerekeni açar. Daha önce
   hangi kameranın açık kalacağı dağınık ve yanlıştı.

3. **Üretim kodu tek doğruluk kaynağı.** Sahnedeki nesneler elle değil,
   `GameCompositionRoot` tarafından kuruluyor.

---

## 7. AUTOMATED TEST RESULTS

```
Compile:   0 hata
EditMode:  307 / 307   başarısız 0
PlayMode:   91 / 91   başarısız 0
```

- **İstisna (exception): 0**
- **Oyun hata logu: 0** (tek log satırı Unity lisans/token mesajıdır, oyun hatası değildir)
- Kabul akışı: `in → al → paketle (6 adım) → koy → bin → adres → al → kapı → NPC → kod → teslim`, para `250 → 558 TL`

**Test sayıları dürüstçe:** 398 otomatik test yeşildir. Bu, görev §99'un
uyardığı gibi **"oyun çalışıyor" demek değildir.**

---

## 8. MANUAL TEST RESULTS

Görev §67 gereği: *"Gerçek manuel test yapamıyorsan `NOT EXECUTED` yaz.
Kesinlikle `PASS` yazma."*

Ajanın gerçek klavye/fare/ekran erişimi **yok**. Bu yüzden 46 maddelik
listenin **hiçbir maddesi için `PASS` yazılmamıştır.**

Aşağıdaki tablo, her maddenin **otomatik karşılığını** gösterir. Bu
otomatik testler oyuncuyla aynı kod yolunu çalıştırır, ancak gözle
görülebilir olanı (kamera, ekran, "hissi") doğrulayamaz.

| # | Madde | Otomatik karşılık | Manuel |
|---|---|---|---|
| 01 | Game start | `PlayabilityGateTests` | NOT EXECUTED |
| 02 | Player spawn | — | **NOT EXECUTED** (bilinen görsel sorun) |
| 03 | Player movement | `WorldInteractionTests` | NOT EXECUTED |
| 04 | Mouse look | — | NOT EXECUTED |
| 05 | Customer interaction | `ShopCustomerFlowTests` | NOT EXECUTED |
| 06 | Parcel pickup | `ShopCustomerFlowTests` | NOT EXECUTED |
| 07 | Parcel carry | `ShopCustomerFlowTests` | NOT EXECUTED |
| 08 | Parcel drop | `GroundParcelDropTests` | NOT EXECUTED |
| 09 | Parcel pickup again | `GroundParcelDropTests` | NOT EXECUTED |
| 10 | Packing table | `PackagingPlayFlowTests` | NOT EXECUTED |
| 11 | Packaging | `PackagingSystemTests` | NOT EXECUTED |
| 12 | Packed parcel pickup | `ShopCustomerFlowTests` | NOT EXECUTED |
| 13 | Bike interaction | `CargoBikePlayModeTests` | NOT EXECUTED |
| 14 | Bike enter | `VehicleBoardExitTests` | NOT EXECUTED |
| 15 | Bike camera | `VehicleCameraAndCargoPlacementTests` | NOT EXECUTED |
| 16 | Bike acceleration | `BicyclePhysicsMeasurements` | NOT EXECUTED |
| 17 | Bike steering | `BicycleTurningTests` | NOT EXECUTED |
| 18 | Bike brake | `BicyclePhysicsMeasurements` | NOT EXECUTED |
| 19 | Bike reverse | `BicyclePhysicsMeasurements` | NOT EXECUTED |
| 20 | Bike exit | `VehicleBoardExitTests` | NOT EXECUTED |
| 21 | Vehicle stays independent | `PlayerAndVehicleIndependenceTests` | NOT EXECUTED |
| 22 | Cargo loading | `RealPathLoadingTests` | NOT EXECUTED |
| 23 | Cargo visible in vehicle | `VehicleCameraAndCargoPlacementTests` | NOT EXECUTED |
| 24 | Cargo unload | `CargoConservationTests` | NOT EXECUTED |
| 25 | Correct address | `PlayabilityGateTests` | NOT EXECUTED |
| 26 | Wrong address | `NpcCodeDeliveryTests` | NOT EXECUTED |
| 27 | Door knock | `NpcDeliveryPlayModeTests` | NOT EXECUTED |
| 28 | NPC home | `NpcCodeDeliveryTests` | NOT EXECUTED |
| 29 | NPC not home | `NpcCodeDeliveryTests` | NOT EXECUTED |
| 30 | NPC code | `NpcCodeDeliveryTests` | NOT EXECUTED |
| 31 | Correct code | `NpcCodeDeliveryTests` | NOT EXECUTED |
| 32 | Wrong code | `NpcCodeDeliveryTests` | NOT EXECUTED |
| 33 | Delivery complete | `PlayabilityGateTests` | NOT EXECUTED |
| 34 | Money reward | `EconomyTests` + kabul logu | NOT EXECUTED |
| 35 | XP reward | `EconomyTests` | NOT EXECUTED |
| 36 | Duplicate delivery rejection | `NpcCodeDeliveryTests` | NOT EXECUTED |
| 37 | Capacity weight | `CargoCapacityTests` | NOT EXECUTED |
| 38 | Capacity volume | `CargoCapacityTests` | NOT EXECUTED |
| 39 | Minimap | Konum: `HudLayoutTests` / İçerik: yok | NOT EXECUTED |
| 40 | Target distance | — | NOT EXECUTED |
| 41 | HUD accuracy | `HudLayoutTests` (konum) | NOT EXECUTED |
| 42 | Day clock | `GameClockTests` + config | NOT EXECUTED |
| 43 | Day summary | — | NOT EXECUTED |
| 44 | Autosave | `SaveSystemTests` | NOT EXECUTED |
| 45 | Reload save | `SaveSystemTests` | NOT EXECUTED |
| 46 | Full scenario | `PlayabilityGateTests` | NOT EXECUTED |

**Özet:** 46 / 46 → manuel doğrulama bekliyor. 0 adet doğrulanmış manuel PASS.

---

## 9. FULL DEMO PLAYTHROUGH

```
Start → Cargo → Packaging → Loading → Driving → Delivery → Reward → Day End → Save
```

**Otomatik karşılık:** `PlayabilityGateTests` bu zinciri tek testte,
hiçbir reset yapmadan, yalnızca oyuncunun kullandığı yollardan geçiyor.
Kabul logu: `250 → 558 TL`, `elde kalan: 0`.

**Manuel:** **NOT EXECUTED.**

---

## 10. BUGS FOUND

1. **Aynı kutu üç kez alınıyordu.** Oyuncu E'ye 3 kez basıyor, elinde 3
   farklı kutu sanıyor; gerçekte `#1` kimlikli aynı kutunun 3 kopyası
   oluyor. Araca yalnızca 1'i sığıyor ve oyuncu "kapasite dolu" sanıyor.
2. **Araç kendi kendini sürüyordu.** Van `ControlsLocked` almadığı için
   oyuncunun yürüyüş girdisiyle gaz alıyordu.
3. **Karakter aracı itiyordu.** `CharacterController`, Rigidbody'li bisikleti
   0.52 m itiyordu.
4. **Kamera araçtan inince donuyordu.** Vanın `CameraFollow.target`
   `fileID: 0` — hiçbir araca bağlı değildi; `MainCamera` etiketi
   taşıdığı için sürekli açık kalıyordu.
5. **Tekerler yere yatıyordu ve hiç dönmüyordu.** `WheelVisual.wheelCollider`
   serialize edilmemişti → runtime'da `Sync()` hiç çalışmıyordu.
6. **Tekerler araç dışında duruyordu.** `WheelVisual` mesh'i araç köküne
   göre konumluyordu, oysa teker nesnesinin çocuğu → konum iki kez ekleniyordu.
7. **Bisiklet zayıf dönüyordu ve yatıyordu.** Anti-roll torku `up` (yaw)
   ekseninde uygulanıyordu → dönüşü frenliyor, yatmayı düzeltmiyordu.
8. **Yanlış araçtan iniliyordu.** `NearestCargoPoint` vanın noktasını
   koşulsuz seçiyordu ve mesafeyi kuryenin bayat konumundan ölçüyordu.
9. **Kutular hem elde hem araçta sayılıyordu.** "Elde" gerçek bir durum
   değildi → envanter ve iki yönlü transfer imkânsızdı.
10. **HUD ekranın merkezindeydi.** HUD kökü `RectTransform` değildi.

## 11. BUGS FIXED

| Bug | Root cause | Fix | Regresyon testi |
|---|---|---|---|
| Aynı kutu çok kez | Raf eldeki kutuyu atlamıyordu | `IsCarryingParcel` + rafta atlama | `CargoCapacityTests` |
| Araç kendi kendine sürüyor | Kilit yalnız başlangıç aracına konuyordu | Tüm araçlara kilit | `PlayerAndVehicleIndependenceTests` |
| Karakter aracı itiyor | `CharacterController` → Rigidbody itişi | `Physics.IgnoreCollision` | `PlayerAndVehicleIndependenceTests` |
| Kamera donuyor | Van `CameraFollow.target` null | `WireVehicleCameras` | `WheelAxleOrientationTests` |
| İki kamera aynı anda | `MainCamera` etiketi araç kamerasında | Etiket FPS'ye taşındı + `ApplyCameraState` | `VehicleCameraAndCargoPlacementTests` |
| Teker yatık / dönmüyor | `wheelCollider` serialize edilmemiş | `Awake` ile bağlantı + duruş ofseti | `WheelAxleOrientationTests` |
| Teker araç dışında | Yanlış referans çerçeve | `transform.parent` çerçevesi | `VanWheelPlacementTests` |
| Bisiklet zayıf/yatık | Tork yanlış eksende | `up` → `forward` | `BicycleTurningTests` |
| Yanlış araçtan inme | Van koşulsuz tercih + bayat konum | Aktif araç referansı | `VehicleBoardExitTests` |
| Kutu iki yerde | "Elde" durumu yoktu | `Carried` durumu + rota güncellemesi | `CargoConservationTests` |
| HUD merkezde | Kök `RectTransform` değildi | `HudLayout` + kök düzeltmesi | `HudLayoutTests` |
| Sessiz kargo kaybı riski | Yeni liste test edilmemişti | 7 muhafaza testi | `CargoConservationTests` |

**Üç hata, testin kendi hatasıydı (kod değil) ve düzeltildi:**
- `TotalParcels` kuryenin ellerini ayrı sayıyordu → kutu iki kez sayılıyordu
- Kapasite testi günlük havuzla aracı dolduramıyordu → önkoşul sağlanmıyordu
- "Elde ikinci kutu alınmaz" testi odağa bağlıydı, odak raftaydı (raf 3 kutuyu
  doldurmaya izin verir — bu bir tasarım kararı, hata değil)

---

## 12. KNOWN ISSUES

| # | Sorun | Sınıf | Durum |
|---|---|---|---|
| 1 | Karakter harita zeminine yarı gömülü görünüyor | **P1** | Çözülmedi — gözle doğrulama gerekiyor |
| 2 | Bisiklet kadro/teker görsel hizası tam oturmuyor | **P2** | Aks ölçümü düzeldi; geri kalan görsel |
| 3 | HUD dışında araç bagajı / karakter envanteri paneli yok | Kapsam dışı (bkz. not) | Bu oturumda yapılmadı |
| 4 | Minimap rota çizgisi çizmiyor | P2 | Konumu doğrulandı, içeriği doğrulanmadı |
| 5 | Manuel doğrulama hiç yapılmadı | **P1 (süreç)** | Kullanıcı yapmalı |

**Kapsam notu (görev §76/§98):** Karakter envanteri ve araç bagajı
panelleri, görev metnindeki zorunlu Demo 1 listesinde **yok** (§29 depo
panelini ister, envanter panelini değil). Bu oturumda yazılan envanter
paneli, teslimat zincirini kırdığı için **geri alındı**. Yeni özellik
eklemek yerine mevcut akışın doğrulanmasına öncelik verildi.

---

## 13. PERFORMANCE

**Ölçüm:** Gerçek Play Mode FPS ölçümü ajan tarafından yapılamaz
(klavye/fare/ekran erişimi yok).

**Otomatik gözlem:** PlayMode koşularında istisna yok, bellek/GC
spike belirtisi yok, kare başına log spam yok.

**Kod incelemesi (görev §65):**
- HUD metinleri yalnızca **değer değişince** yazılıyor (`SetIfChanged`)
- Kargo kutusu görselleri olay güdümlü, `Update`'te değil
- LINQ yok, kare başına allocation yok
- `GameEvents` statik olay bus — arama yok

**NOT EXECUTED:** gerçek 60 FPS ölçümü.

---

## 14. FINAL VERDICT

Görev §93'teki cümle:

> *"Ben şimdi Unity'de Play'e basıp bütün Demo 1'i debug müdahalesi olmadan
> oynayabiliyorum."*

Bu cümleye **`YES` diyemiyorum**, çünkü gerçek klavye/fare/ekran
erişimim yok ve bu görevi hiçbir yerde öyle doğrulamadım. Görev §67
bunu açıkça yasaklıyor: gerçek manuel test yapılmadıysa `PASS` yazma.

```text
DEMO 1 FULLY PLAYABLE: NO
```

**Sebep: doğrulama eksikliği — bilinen bir oyun hatası olduğu için değil.**

```text
P0 BLOCKERS:
  (bilinen kod hatası yok)

P1 BLOCKERS:
  1. Manuel doğrulama yapılmadı (46/46 madde NOT EXECUTED)
  2. Karakter harita zeminine gömülü görünüyor — oyuncu çıplak gözle
     doğrulamadan kapanamaz

P2:
  1. Bisiklet kadro/teker görsel hizası
  2. Minimap rota çizgisi
```

**Bu NO'yu ne kapatır?**

1. Kullanıcının Unity'de `Play`'e basıp Demo 1'i baştan sona
   oynaması, ve
2. Yukarıdaki P1'lerin (zemin gömülmesi dahil) giderilmesi.

Otomatik doğrulama tarafı hazır: **398 test yeşil, kabul akışı uçtan uca
çalışıyor, kargo muhafazası kanıtlandı.** Eksik olan tek şey insan
gözü ve elidir.
