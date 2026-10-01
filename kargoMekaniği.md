# KARGO SİMÜLATÖRÜ

# KARGO MEKANİKLERİ — SIFIRDAN TASARIM VE UYGULAMA

## 0. GÖREVİN ÖZÜ

Mevcut kargo mekanikleri oyuncu deneyimi açısından başarısızdır.

Mevcut sistemi küçük düzeltmelerle kurtarmaya çalışma.

Bu görevde:

> **Mevcut fiziksel kargo alma / taşıma / bırakma / yükleme / çıkarma / paketleme bağlantısını sök ve kargo gameplay'ini sıfırdan, temiz ve oyuncu odaklı şekilde oluştur.**

Amaç:

```text
Kargoyu görüyorum
↓
Yanına gidiyorum
↓
E ile TEK bir kargoyu alıyorum
↓
Kargo gerçekten elimde
↓
Yürüyebiliyorum
↓
İstediğim yere götürüyorum
↓
G ile yere bırakabiliyorum
↓
Tekrar alabiliyorum
↓
Aracıma götürüyorum
↓
E ile araca koyuyorum
↓
Kargonun araçta gerçekten durduğunu görüyorum
↓
Daha sonra araçtan tekrar TEK TEK alabiliyorum
↓
Adresin kapısına taşıyorum
↓
Teslim ediyorum
```

Bu zincir doğal ve sorunsuz çalışmadan:

**KARGO SİSTEMİ TAMAMLANMIŞ SAYILMAZ.**

---

# 1. ÖNCE MEVCUT SİSTEMİ ANALİZ ET

Kod yazmaya başlamadan önce mevcut projeyi incele.

Özellikle şu alanları bul:

* `Parcel`
* `ParcelState`
* `CargoSystem`
* `CargoManifest`
* `VanCargoPoint`
* `PackagingSystem`
* `PackingTable`
* cargo pickup / interaction kodları
* vehicle cargo loading
* vehicle cargo unloading
* delivery ile cargo arasındaki bağlantılar
* save/load içindeki parcel state
* UI içindeki cargo bilgileri
* testlerde cargo ile ilgili bütün sınıflar

Önce bir dependency map çıkar.

Şunu tespit et:

```text
Hangi sistem hangi eski kargo koduna bağlı?
```

---

# 2. ESKİ KARGO MEKANİĞİ KORUNMAYACAK

Aşağıdaki eski davranışları olduğu gibi taşıma:

* kargoyu otomatik ele verme
* fiziksel olmayan pickup
* yanlış interaction seçimi
* toplu kargo taşıma
* kargonun gizlice inventory'ye dönüşmesi
* kargonun otomatik araç slotuna ışınlanması
* aracın bütün kargoyu tek seferde yüklemesi
* aracın bütün kargoyu tek seferde çıkarması
* teslimatta kargonun otomatik yok olması
* fizik ile rastgele savrulan kutular
* test amacıyla hazırlanmış cargo shortcut'ları
* eski kötü state geçişleri

Bunlar yeni sisteme taşınmayacak.

---

# 3. ÖNEMLİ: DİĞER SİSTEMLERİ BOZMA

Kargo sistemi sıfırdan yapılacak ama aşağıdakiler korunmalı:

* Player
* Interaction altyapısının kullanılabilir kısmı
* Vehicle
* NPC
* Delivery
* Economy
* XP
* Day
* Save
* HUD
* Minimap

Gerekli yerlerde bunların cargo entegrasyonları yeniden yazılabilir.

Ancak:

> Kötü cargo mimarisine sırf başka sistem bağlı diye bağlı kalma.

Gerekirse temiz adapter/interface oluştur.

---

# 4. KARGO TASARIM FELSEFESİ

Kargo:

**bir sayı değildir.**

Kargo:

**dünyada bulunan fiziksel bir gameplay nesnesidir.**

Oyuncu:

```text
Kargoyu GÖRÜR
Kargoya BAKAR
Kargoyu SEÇER
Kargoyu ALIR
Kargoyu TAŞIR
Kargoyu BIRAKIR
Kargoyu YÜKLER
Kargoyu ÇIKARIR
Kargoyu TESLİM EDER
```

---

# 5. İKİ AYRI KATMAN

Kargo sistemi iki parçadan oluşmalı:

## A. Cargo Data

Kargonun bilgileri.

Örneğin:

```text
ParcelId
Receiver
AddressId
Type
Weight
Volume
Value
Priority
Fragile
Deadline
DeliveryCode
PackagingState
DeliveryState
```

## B. Physical Cargo Object

Dünyadaki gerçek kutu.

Örneğin:

```text
ParcelWorldObject
Collider
Rigidbody
Mesh
Interaction
CargoVisual
```

Data ile fiziksel obje birbirine bağlı olacak.

---

# 6. KARGO TÜRLERİ

Demo 1:

### Belge

```text
0.4 – 1.2 kg
1 – 3 L
```

### Kırtasiye

```text
1 – 3 kg
4 – 12 L
```

### Elektronik

```text
1.5 – 5 kg
8 – 25 L
```

### Gıda

```text
2 – 6 kg
10 – 30 L
```

### İlaç

```text
0.5 – 2 kg
2 – 8 L
```

### Eşya

```text
3 – 8 kg
15 – 45 L
```

---

# 7. KUTU BOYUTU

Ağırlık ve hacim yalnız UI'da sayı olmayacak.

Kargonun fiziksel boyutunu da etkilemeli.

Örneğin:

### Küçük

Belge / ilaç

```text
small box
```

### Orta

Kırtasiye / elektronik / gıda

```text
medium box
```

### Büyük

Eşya

```text
large box
```

Bu boyutlar:

* elde taşıma
* araç yerleşimi
* cargo capacity

ile uyumlu olacak.

---

# 8. KUTU GÖRSELİ

Her kargo kutusu:

* karton kutu görünümünde
* okunabilir
* sade
* düşük poly
* performans dostu

olacak.

Kutunun üzerinde küçük bir kargo etiketi bulunabilir.

Etiket:

```text
Parcel #1042
Derya Akın
14
```

gibi bilgileri gösterebilir.

Teslim kodunu kutunun üzerine açıkça yazma.

---

# 9. KARGO ETİKETİ

Oyuncu kutuyu eline aldığında veya üzerine baktığında küçük tooltip:

```text
Kargo #1042

Alıcı:
Derya Akın

Adres:
14

Tür:
Elektronik

Ağırlık:
3.2 kg

Hacim:
14 L

Kırılgan:
Evet

Durum:
Hazır
```

gösterebilir.

Teslim kodunu burada gösterme veya gameplay'i anlamsızlaştıracak şekilde her zaman açık bırakma.

Teslim kodu NPC'den öğrenilecek.

---

# 10. PLAYER ELDEN KARGO SİSTEMİ

Oyuncunun aynı anda:

**SADECE 1 fiziksel kargo**

taşımasına izin ver.

Bu kritik.

Şu olmamalı:

```text
3 kutu elimde
```

Bunun yerine:

```text
1 kutu elde
```

olacak.

Oyuncu üç kutu taşımak istiyorsa:

```text
1. kutu → taşı
2. kutu → bırak
3. kutu → taşı
```

veya:

```text
araçta 1
araçta 2
araçta 3
```

şeklinde çalışacak.

Bu fiziksel hissi güçlendirecek.

---

# 11. KARGO ALMA

Oyuncu kutuya bakıyor.

Kutunun mesafesi uygun.

Prompt:

```text
[E] Kargoyu al
```

Oyuncu E:

```text
World
↓
Held
```

geçişini başlatır.

Kutu oyuncunun elindeki:

```text
CargoHoldAnchor
```

noktasına gelir.

---

# 12. KARGO ELDEYKEN

Kutu:

* kameraya yapışmamalı
* oyuncunun kafasının içine girmemeli
* omuzdan geçmemeli
* göğsün içine girmemeli
* görüşü tamamen kapatmamalı

Kutu için belirli bir elde taşıma noktası:

```text
Player
└── Camera
    └── CargoHoldAnchor
```

olabilir.

Ancak fizik:

**tam serbest Rigidbody**

olarak bırakılmamalı.

Yoksa:

* jitter
* duvar çarpışması
* kutunun savrulması
* oyuncunun takılması

oluşabilir.

---

# 13. ELDE TAŞIMA MODELİ

En sağlam yaklaşım:

### Held

Kutu:

* Rigidbody = kinematic
* collider = kontrollü
* player collision = uygun şekilde ignore
* transform = CargoHoldAnchor'ı takip

### World

Kutu:

* Rigidbody dynamic
* normal collider
* dünyayla fizik etkileşimi açık

Bu iki state arasında açık geçiş olacak.

---

# 14. PLAYER HAREKETİ

Kutu taşırken:

```text
WASD
Mouse
Shift
```

çalışmalı.

Ancak:

```text
HoldingCargo
```

durumunda oyuncu normalden biraz daha yavaş yürüyebilir.

Örneğin:

```text
Normal = 1.0
Holding = 0.8
Running = 0.65
```

gibi çarpan.

Exact değerleri gameplay test ederek ayarla.

Ama:

**oyuncuyu kaplumbağa gibi yavaşlatma.**

---

# 15. KARGO BIRAKMA

Elde kutu varsa:

```text
[G] Bırak
```

promptu göster.

G:

```text
Held
↓
World
```

yapar.

Kutu oyuncunun önündeki uygun yüzeye yerleştirilir.

---

# 16. BIRAKMA NOKTASI

Kutu oyuncunun göğsünden direkt yere düşürülmemeli.

Önce:

```text
Forward raycast
```

ve zemini hesapla.

Uygun bir yer bulunursa:

```text
DropPosition
```

hesaplanır.

Kutu oraya yerleştirilir.

---

# 17. GEÇERSİZ BIRAKMA

Uygun yüzey yoksa:

```text
Buraya kargo bırakılamaz.
```

göster.

Kutu oyuncunun elinde kalmalı.

Örneğin:

* havada
* duvarın içinde
* NPC'nin içinde
* aracın içine rastgele
* masa içine

bırakılmamalı.

---

# 18. KUTUYU ATMA

G tuşu:

**fırlatma tuşu değildir.**

Kutu:

* ileri doğru atılmayacak
* fizik kuvveti uygulanmayacak
* oyuncunun elinden fırlamayacak

Sadece kontrollü şekilde bırakılacak.

Bu bir kargo simülasyonu.

Kutu top değil.

---

# 19. KUTU ÇARPIŞMALARI

World state'teki kutu:

* zeminle çarpışır
* duvarla çarpışır
* araçla uygun şekilde etkileşir

Ama:

* 500 metre uzağa uçmamalı
* yere gömülmemeli
* sonsuza kadar sallanmamalı
* diğer kutuları roket gibi savurmamalı

Fizik ağırlıkları gameplay için kontrollü olmalı.

---

# 20. KUTULARI ÜST ÜSTE KOYMA

Dünyada oyuncu:

```text
kutu 1
↓
kutu 2
↓
kutu 3
```

şeklinde kutuları üst üste bırakabilir.

Ancak çarpışmaları kararlı olmalı.

Kutu spawn olurken başka kutunun merkezinin içine oluşmamalı.

---

# 21. KUTU ALMA ÖNCELİĞİ

Oyuncu bir kutuya bakıyorsa:

```text
Box
```

öncelikli interaction olacak.

Ancak:

```text
AlreadyHoldingCargo
```

ise başka kutu alma promptu gösterilmeyecek.

Prompt:

```text
Önce elindeki kargoyu bırak.
```

olabilir.

---

# 22. TEK KARGO KURALININ AVANTAJI

Bu özellikle korunmalı.

Oyuncu:

```text
kargoyu al
taşı
bırak
geri dön
başka kargoyu al
```

yapar.

Böylece oyuncunun iş yaptığı hissi oluşur.

Kargo yönetimi bir inventory menüsüne dönüşmez.

---

# 23. KARGO ENVANTERİ

TAB paneli kargoların **verisini** gösterebilir.

Ancak TAB paneli:

**fiziksel kargoyu teleportun kısa yolu olmamalı.**

Örneğin:

```text
Kargo #1
[Ekle]
```

ile kargoyu otomatik elde yaratmak yasak.

Fiziksel kargo dünyada nerede ise orada kalacak.

---

# 24. DEPODAN KARGO ALMA

Müşteri ile konuşulduğunda müşteri:

**paketlenmiş fiziksel kutuyu** teslim eder.

Akış:

```text
Customer
↓
E
↓
Customer hands parcel
↓
Parcel appears at hand / customer transfer point
↓
Player obtains physical box
```

Oyuncu bunu dünyada görür.

---

# 25. KARGONUN MÜŞTERİDEN ALINMASI

Kargonun havadan spawn olup oyuncunun içine girmesine izin verme.

Kutu:

* müşteri önünden
* tezgâh
* transfer noktası

gibi mantıklı bir konumdan alınır.

---

# 26. KARGO PAKETLEME

Burada iki kargo sınıfı olabilir:

### Hazır gelen teslimat

Brief'teki depo müşterisinin verdiği paket:

```text
PackagingState = Ready
```

### Oyuncunun hazırladığı paket

İlerleyen sistem için:

```text
Raw
→ Packaging
→ Ready
```

olabilir.

Demo 1 ana teslimat akışında:

**Müşterinin verdiği kargo hazır olarak gelir.**

Paketleme sistemi varsa ayrı gameplay olarak kullanılabilir.

Bu iki akışı birbirine karıştırma.

---

# 27. PAKETLEME VE FİZİKSEL KARGO İLİŞKİSİ

Oyuncu paketleme yapıyorsa:

```text
Physical Parcel
↓
Packing Station
↓
Temporary Work State
↓
Physical Parcel updated
↓
Ready
```

olur.

Kargo gizlice yok edilip başka bir kutu yaratılmamalı.

Mümkün olduğunca:

**aynı ParcelId korunmalı.**

---

# 28. PAKETLEME MASASINA KOYMA

Kutu eldeyken:

```text
[E] Masaya koy
```

çıksın.

E:

```text
Held
↓
OnPackingTable
```

yapsın.

Oyuncu kutuyu masanın üzerinde görsün.

---

# 29. MASADAN ALMA

Paketleme tamamlandığında:

```text
[E] Kargoyu al
```

ile yeniden alınabilsin.

Akış:

```text
Table
↓
Held
```

olacak.

---

# 30. ARAÇ KARGO BÖLGESİ

Araçta:

```text
CargoArea
```

adında gerçek bir gameplay alanı bulunmalı.

Bu alan:

* görünür
* collider sınırı belli
* oyuncunun erişebileceği
* kapasite hesaplayan

bir alan olacak.

---

# 31. BİSİKLET KARGO ALANI

Başlangıç bisikleti:

```text
60 kg
150 L
```

kapasite.

Kargo kasası görsel olarak gerçek bir alan olacak.

---

# 32. ARAÇ KARGO YERLEŞİMİ

Kargo:

**araçta sadece sayısal olarak bulunmayacak.**

Kasa içinde gerçekten görünecek.

Örneğin:

```text
Cargo Slot 01
Cargo Slot 02
Cargo Slot 03
...
```

slotları olabilir.

Ancak slot sistemi oyuncunun fiziksel yerleştirme özgürlüğünü tamamen ortadan kaldırmamalı.

Hedef:

> Oyuncu kargoyu kasaya koyuyor gibi hissetsin.

---

# 33. ARAÇ YÜKLEME

Oyuncu:

1. Kargoyu eline alır.
2. Bisikletin kasasına gider.
3. Kargoyu geçerli yükleme bölgesine getirir.
4. Prompt çıkar:

```text
[E] Kargoyu araca koy
```

5. E'ye basar.
6. Kargo:

```text
Held
↓
Loaded
```

olur.

---

# 34. KARGO YÜKLEME TELEPORTU

Oyuncu kargoyu aracın 5 metre uzağından yüklememeli.

Kargo:

**gerçekten cargo area'ya yakın olmalı.**

Ancak E ile koyduğunda fiziksel olarak düzgün yerleştirilebilmesi için slot/snap kullanılabilir.

---

# 35. SNAP SİSTEMİ

Kargoyu aracın içine tamamen rastgele physics ile bırakmak yerine:

```text
Player manually brings box near cargo area
↓
E
↓
nearest available cargo placement point
↓
box snaps there
```

olabilir.

Bu:

**oyuncunun yerine oynamak değildir.**

Çünkü oyuncu hâlâ:

* kutuyu almış
* taşımış
* araca götürmüş
* yükleme noktasına getirmiş

oluyor.

---

# 36. KARGO SLOT DÜZENİ

Her araç için önceden tanımlı kargo yerleştirme noktaları olabilir.

Örneğin bisiklet:

```text
01
02
03
04
05
06
```

Ancak slot sayısı kesin kapasite değildir.

Gerçek kapasite:

```text
Weight
+
Volume
```

ile hesaplanmalı.

---

# 37. AĞIRLIK

Örneğin:

```text
Bike:
60 kg
```

3 kutu:

```text
20 kg
20 kg
15 kg
```

toplam:

```text
55 kg
```

olur.

Bir sonraki 10 kg kutu:

```text
65 kg
```

olacağı için yüklenmemeli.

---

# 38. HACİM

Aynı şekilde:

```text
150 L
```

üzerinden hesaplanmalı.

Örneğin:

```text
110 L
+
50 L
=
160 L
```

ise ikinci kutu reddedilir.

---

# 39. İKİLİ KAPASİTE KURALI

Yükleme:

```text
CurrentWeight + ParcelWeight <= MaxWeight
AND
CurrentVolume + ParcelVolume <= MaxVolume
```

ise başarılı.

İkisinden biri bile aşılıyorsa:

**LOAD FAIL**

---

# 40. KAPASİTE UI

Oyuncuya:

```text
Kargo:
3

Ağırlık:
47 / 60 kg

Hacim:
118 / 150 L
```

gösterilebilir.

Aşım durumunda:

```text
Bu kargo bisikletin kapasitesini aşıyor.
```

mesajı.

---

# 41. YÜKLEME BAŞARISIZSA

En önemli kural:

**Kargo kaybolmayacak.**

Kargo:

```text
Held
```

olarak kalacak.

Böylece oyuncu yere bırakabilir veya başka araca götürebilir.

---

# 42. ARAÇ DOLU

Araç doluysa:

```text
[E] Kargoyu araca koy
```

yerine:

```text
Kapasite yetersiz
47 / 60 kg
118 / 150 L
```

gibi açıklayıcı prompt çıkabilir.

---

# 43. ARAÇTAN KARGO ALMA

Oyuncu araç kasasına yaklaşır.

Üzerine baktığında:

```text
[E] Kargoyu al
```

çıkar.

E:

```text
Loaded
↓
Held
```

yapar.

---

# 44. TEK TEK ARAÇTAN ALMA

Kesinlikle:

```text
"E → tüm kargolar arabadan çıktı"
```

olmayacak.

Bir E:

**bir kargoyu** çıkaracak.

---

# 45. HANGİ KARGO ALINACAK?

Oyuncunun baktığı kargo seçilmeli.

Örneğin araçta:

```text
Kargo A
Kargo B
Kargo C
```

var.

Oyuncu Kargo B'ye bakıyorsa:

```text
[E] Kargo B'yi al
```

olmalı.

Sistem başka kutuyu seçmemeli.

---

# 46. KARGO ÇIKARMA SONRASI

Kargo:

```text
Loaded
↓
Held
```

olur.

Oyuncu:

* taşıyabilir
* bırakabilir
* kapıya götürebilir
* yanlışsa geri araca koyabilir.

---

# 47. ARACA GERİ KOYMA

Oyuncu teslim etmekten vazgeçerse:

```text
Held
↓
Vehicle Cargo Area
↓
E
↓
Loaded
```

tekrar yapılabilmeli.

---

# 48. ARAÇTAN İNME

Oyuncu araçtan indiğinde:

**kargo kaybolmayacak.**

Araçtaki kargolar araçta kalacak.

Araç:

```text
Player
X
Cargo
```

ile bağımsız yaşayacak.

---

# 49. ARAÇ HAREKET EDERKEN KARGO

Araç hareket ettiğinde kargolar:

* kasada kalmalı
* yer değiştirmemeli
* uçmamalı
* aracın içine gömülmemeli
* yere düşmemeli

Normal arcade sürüşte:

**kontrollü şekilde araçla birlikte hareket etmeli.**

---

# 50. KIRILGAN KARGO

`Fragile == true` ise:

Demo 1'de fiziksel hasar sistemi şart değildir.

Ancak veri tutulmalı.

Gelecekte:

```text
hard impact
→ damage
→ payout penalty
```

eklenebilmesine uygun olmalı.

Şimdilik yeni damage system yaratma.

---

# 51. DEADLINE

Kargonun deadline bilgisi olabilir.

Kargo HUD'da:

```text
Deadline:
09:30
```

gibi gösterilebilir.

Ancak kargo pickup sistemi deadline yüzünden fiziksel kutuyu yok etmemeli.

Teslimat sistemi karar vermeli.

---

# 52. KARGONUN TESLİM NOKTASINA TAŞINMASI

Doğru adrese gelindiğinde:

```text
Vehicle
↓
F
↓
Player OnFoot
↓
Vehicle Cargo
↓
E
↓
Select correct parcel
↓
Held
↓
Door
```

---

# 53. DOĞRU KARGO KONTROLÜ

NPC teslimatı öncesinde:

```text
Parcel.AddressId
==
Door.AddressId
```

kontrol edilmeli.

Yanlışsa:

```text
Bu kargo bu adrese ait değil.
```

ve teslim olmamalı.

---

# 54. KAPIYA KARGO İLE GİTME

Kargo elindeyken oyuncu kapıya gider.

Kapı:

```text
[E] Kapıyı çal
```

der.

E:

NPC sürecini başlatır.

Kargo oyuncunun elinde kalır.

---

# 55. TESLİM KODU

NPC:

```text
4 haneli kod
```

söyler.

Oyuncu kodu girer.

---

# 56. TESLİM BAŞARILI

Doğru kod:

```text
Held Parcel
↓
Delivered
↓
Parcel disappears from world ONLY NOW
```

Burada "disappear":

**kargonun teslim edildiği ve artık gameplay nesnesi olmadığı anlamına gelir.**

Para/XP:

```text
+
```

---

# 57. TESLİMDEN ÖNCE KARGO YOK OLAMAZ

Şunların hiçbirinde kargo kaybolmayacak:

```text
pickup
drop
loading
unloading
vehicle exit
wrong code
wrong door
NPC not home
scene change
save/load
UI close
```

---

# 58. YANLIŞ KOD

Yanlış kod:

```text
No delivery
No payment
No XP
Parcel stays held
Retry
```

---

# 59. NPC EVDE DEĞİL

```text
Knock
↓
No answer
```

Kargo oyuncuda kalabilir.

Oyuncu daha sonra:

```text
aynı parcel
↓
same address
↓
retry
```

yapabilir.

---

# 60. BAŞARILI TESLİMDEN SONRA

Parcel state:

```text
Delivered
```

olur.

World object:

```text
Destroyed / despawned
```

olabilir.

Ama Parcel Data save sistemi içinde kalır.

Böylece:

```text
Delivered = true
```

korunur.

---

# 61. DUPLICATE DELIVERY

Bir Parcel:

```text
Delivered
```

ise:

* tekrar pickup edilemez
* tekrar yüklenemez
* tekrar teslim edilemez
* tekrar para verilemez
* tekrar XP verilemez

---

# 62. KARGO STATE MACHINE

Yeni sistemin merkezi açık bir state machine olmalı.

Önerilen:

```text
Created
↓
Available
↓
Held
↕
World
↓
OnPackingTable
↓
Ready
↓
Held
↓
Loaded
↓
Held
↓
AtDelivery
↓
Delivered
```

Gerekli durumlarda:

```text
ReturnedToDepot
Failed
Cancelled
```

eklenebilir.

Ancak kullanılmayan state ekleme.

---

# 63. GEÇERSİZ STATE GEÇİŞLERİ

Örneğin:

```text
Delivered → Held
Delivered → Loaded
Delivered → Available
```

yasak.

Aynı şekilde:

```text
Loaded → Packaging
```

doğrudan yapılamaz.

Önce araçtan çıkarılmalı.

---

# 64. STATE DEĞİŞİMİNİN TEK SAHİBİ

UI:

**Parcel state değiştirmez.**

Vehicle:

**Parcel state kurallarını tek başına belirlemez.**

Delivery:

**kargoyu fiziksel olarak spawn etmemeli.**

Tek bir Cargo domain/state sahibi olmalı.

---

# 65. ÖNERİLEN SINIFLAR

Gerektiği kadar.

Örneğin:

```text
Parcel
ParcelState
ParcelData
ParcelWorldObject
CargoSystem
CargoInteraction
CargoCarryController
CargoPlacement
CargoArea
CargoSlot
CargoCapacity
```

Ama:

**gereksiz 20 sınıflık mimari oluşturma.**

Basit tut.

---

# 66. PLAYER CARRY CONTROLLER

Bir `CargoCarryController`:

Oyuncunun elinde:

```text
HeldParcel
```

olup olmadığını yönetebilir.

Temel API:

```text
TryPickup(parcel)
TryDrop()
TryPlaceOnSurface()
TryLoadToVehicle()
TryUnloadFromVehicle()
HasCargo
HeldParcel
```

gibi olabilir.

---

# 67. FİZİK KONTROLÜ

Cargo state'e göre fizik değişsin.

### World

```text
Rigidbody dynamic
Collider active
```

### Held

```text
Rigidbody kinematic
Controlled transform
```

### Loaded

```text
Rigidbody kinematic
Attached to cargo placement
Vehicle-relative
```

### Delivered

```text
World object removed
```

---

# 68. PARENTING

Kargo:

**oyuncunun fiziksel dünyadaki bütün hareketini doğrudan parent ederek bozmasın.**

Ancak:

```text
Held
```

ve:

```text
Loaded
```

durumunda controlled attachment kullanılabilir.

Önemli olan:

* player velocity cargo'ya patlamasın
* vehicle Rigidbody ile cargo birbirini fiziksel olarak itmesin

---

# 69. CARGO COLLISION LAYERS

Ayrı layer kullan:

```text
Cargo
```

Gereken yerlerde:

```text
Player ↔ Cargo
Vehicle ↔ Cargo
Cargo ↔ Cargo
Ground ↔ Cargo
Building ↔ Cargo
```

matrisini kontrollü ayarla.

Amaç:

**oyuncu kutuyu alırken fizik motorunun saçmalamaması.**

---

# 70. CARGO PICKUP RAYCAST

Oyuncunun baktığı kutuyu seç.

Öncelik:

1. Crosshair'a en yakın
2. Raycast doğrudan hit
3. Interaction distance
4. CanInteract
5. doğru state

---

# 71. HOLD POSITION KAMERA KURALI

Kutu:

* ekranın ortasını tamamen kapatmamalı
* görüş alanını öldürmemeli
* oyuncunun önünde doğal durmalı

Kamera clipping kontrolü yapılmalı.

---

# 72. KARGO GÖRSELLERİ

Kutu elde:

```text
Label visible
Parcel visual visible
```

Araçta:

```text
Box visual visible
```

Yerde:

```text
Box visual visible
```

Teslim sonrası:

```text
removed
```

---

# 73. KARGO YERLEŞİMİ

Oyuncu kargoyu yere bırakırken dünya pozisyonuna göre:

* zemin
* masa
* araç
* uygun platform

hesaplanmalı.

Geometri içine yerleştirme yapılmamalı.

---

# 74. KUTUNUN ÇEVREYE GÖMÜLMESİ

Drop sırasında:

```text
OverlapBox
```

veya uygun collision testleri ile:

```text
blocked
```

olup olmadığını kontrol et.

Blocked ise kutuyu en yakın geçerli noktaya yerleştir veya elde tut.

---

# 75. ARAÇ KASASINA YERLEŞTİRME

CargoArea içerisinde kargo için:

```text
PlacementPoint
```

oluştur.

Yerleşim noktaları:

* kasa dışına taşmamalı
* ground altına girmemeli
* diğer kutularla çakışmamalı

---

# 76. KARGO STACKING

Kasa için mümkünse katmanlı yerleşim:

```text
row 1
row 2
row 3
```

gibi olabilir.

Ancak ilk demo için maksimum fiziksel kutu sayısını gereksiz artırma.

Performans + okunabilirlik korunmalı.

---

# 77. KARGO SAYISI

Bir gün:

**10–14 civarı kargo**

üretebilir.

Ancak tüm 10–14 kargonun aynı anda fiziksel dünyaya saçılması gerekiyorsa performansı kontrol et.

Depodaki kargolar:

**düzenli ve okunabilir bir alanda** bulunmalı.

---

# 78. DEPODA KARGO YERLEŞİMİ

Kargolar:

```text
Shelf
Floor
Counter
Cargo Area
```

gibi noktalarda düzenli spawn edilmeli.

Hepsi üst üste spawn edilmemeli.

---

# 79. KARGO ID

Her kargonun:

```text
ParcelId
```

benzersiz olmalı.

Örnek:

```text
P0001
P0002
P0003
```

Aynı ID:

**aynı gün içinde iki kargoda bulunamaz.**

---

# 80. GÜN DEĞİŞİMİ

Gün sonunda:

```text
Delivered
```

kargolar tamamlanmış sayılır.

Bekleyen kargolar için mevcut oyun tasarımına uygun state korunmalı.

Yeni güne geçerken:

* eski kargo
* yeni kargo

birbirine karışmamalı.

---

# 81. SAVE / LOAD

Save:

```text
ParcelId
State
AddressId
Receiver
DeliveryState
PackagingState
Weight
Volume
DeliveryCode
```

gibi kritik bilgileri korumalı.

Physical object save'den sonra tekrar spawn edilecekse:

```text
ParcelId
```

üzerinden doğru obje yeniden kurulmalı.

---

# 82. SAVE SONRASI FİZİKSEL DURUM

Örneğin oyuncu:

```text
Kargo P005
↓
bisiklette
↓
oyunu kapattı
```

ise save sisteminin tasarımına göre:

```text
P005 = Loaded
```

olarak korunmalı.

Oyun yeniden açıldığında:

```text
P005
↓
bisiklet cargo area
```

yeniden kurulmalı.

---

# 83. SCENE RELOAD

Scene reload:

**kargoları çoğaltmamalı.**

Örneğin:

```text
P001
```

scene reload sonrası:

```text
P001
P001 copy
```

olmamalı.

---

# 84. DUPLICATE SPAWN KONTROLÜ

Her `ParcelId` için:

```text
one active world representation
```

kuralı olmalı.

Aynı fiziksel kargodan iki instance oluşturulması yasak.

---

# 85. KARGO SPAWN YÖNETİMİ

Kargo spawn noktası:

* collider kontrolü
* ground kontrolü
* overlap kontrolü

yapmalı.

Başka objenin içinde kargo spawn etme.

---

# 86. PLAYER RESPAWN

Oyuncu yeniden doğarsa:

Held cargo:

**havaya saçılmamalı.**

Mevcut state'e göre:

* elde kalmalı
* güvenli noktaya bırakılmalı
* vehicle'daysa vehicle'da kalmalı

---

# 87. ALTIN KURAL — KARGO KAYBOLMAYACAK

Debug dışında:

```text
Instantiate
```

sonrasında kargo:

```text
Destroy
```

yalnızca geçerli sebeplerle yapılabilir:

### Geçerli

```text
Delivered
Cancelled
```

### Geçersiz

```text
pickup transition
load
unload
wrong code
wrong address
player death/reset
UI close
```

---

# 88. DEBUG KODU

Kargo sisteminde debug araçları olabilir.

Ama:

**release gameplay'de kullanılmayacak.**

Test araçları:

```text
Editor
Tests
Development Build
```

alanında tutulmalı.

---

# 89. UI DURUMU

Oyuncu elinde kargo varsa:

```text
Kargo:
P0042

[E] Yere bırak
```

gibi prompt.

Araç yanında:

```text
[E] Araca yükle
```

Kasa bakışı:

```text
[E] Kargoyu al
```

Promptlar state'e göre değişmeli.

---

# 90. PROMPT YALAN SÖYLEMEYECEK

Örneğin oyuncunun elinde zaten kargo varsa:

```text
[E] Kargoyu al
```

göstermek yasaktır.

Benzer şekilde:

Kapasite doluysa:

```text
[E] Araca yükle
```

yerine:

```text
Kapasite yetersiz
```

göster.

---

# 91. HOVER TOOLTIP

Mouse ile kutuya bakıldığında kısa bilgi:

```text
P0042
Derya Akın
Adres 14
Elektronik
3.2 kg
14 L
Hazır
```

görülebilir.

Bu tooltip:

* gameplay'i durdurmamalı
* gereksiz büyük olmamalı
* sürekli ekranı kapatmamalı

---

# 92. KARGO RENK KODU

İstersen görsel ayırt edilebilirlik için:

* belge
* elektronik
* gıda
* ilaç

gibi türlerde küçük renk/etiket farkları kullanılabilir.

Ancak bütün kutuları gökkuşağı gibi farklı renklere dönüştürme.

Gerçek kargo hissini koru.

---

# 93. KARGO YÖNETİM PANELİ

TAB paneli açıldığında:

```text
Kargolar
------------------
P001
Derya Akın
Adres 14
Ready

P002
Mehmet Kaya
Adres 08
Loaded
```

gibi bilgileri gösterebilir.

Panel:

**fiziksel durumun yerine geçmez.**

---

# 94. PANELDEN KARGO SEÇME

Panelden bir kargo seçmek:

**oyuncunun onu eline teleport etmemeli.**

Panel yalnız:

* bilgi
* takip
* seçim
* detay

amaçlı.

---

# 95. KARGO TESLİM SIRASI

Route system aktif kargoyu belirleyebilir.

Ancak oyuncu yanlış sıradaki kargoyu alıp farklı adrese gitmek isterse oyun bunu engellemek zorunda değil.

Oyuncunun özgürlüğü korunabilir.

Teslimat kontrolü yalnız:

```text
AddressId
```

ile yapılır.

---

# 96. OYUNCUNUN KARGO SEÇİMİ

Örneğin 6 kargo yüklü.

Oyuncu:

```text
önce P003
sonra P001
sonra P005
```

teslim edebilir.

Sistem:

**her zaman fiziksel olarak seçilen kargoyu** baz almalı.

---

# 97. AKTİF ROTA VE KARGO

HUD:

```text
Sıradaki:
Derya Akın
Adres 14
```

diyebilir.

Ancak bu:

**oyuncuyu başka kargo almaya kilitlemez.**

---

# 98. KARGO TESLİMİ TAM OLARAK

Oyuncu:

```text
Kargoyu aldı
↓
kapıya götürdü
↓
kapıyı çaldı
↓
NPC kod verdi
↓
kod doğru
```

sonrasında teslim gerçekleşir.

İstersen teslim anında:

```text
Kargo elden müşteri transferi
```

gibi kısa bir görsel hareket yapılabilir.

Ama uzun cutscene yapma.

---

# 99. TESLİM SONRASI

HUD:

```text
TESLİMAT TAMAMLANDI
+₺XXX
+XX XP
```

göstersin.

Parcel:

```text
Delivered
```

olsun.

Route:

```text
Next
```

olsun.

---

# 100. YANLIŞ ADRES

Yanlış adrese kapı çalındıysa:

```text
Bu kargo bu adrese ait değil.
```

NPC varsa bile teslim başlamamalı.

Kargo:

**elde kalmalı.**

---

# 101. WRONG CODE

Yanlış kodda:

```text
Kargo elde kalır.
```

UI açık kalabilir.

Oyuncu:

```text
retry
```

yapabilir.

---

# 102. NOT HOME

Müşteri yoksa:

```text
No delivery.
```

Kargo:

**elde kalır.**

---

# 103. OYUNCUNUN KARGOYU GERİ ARACA KOYMASI

Oyuncu NPC yoksa:

```text
Kargoyu al
↓
geri dön
↓
bisiklete koy
```

yapabilmeli.

Bu kritik bir gameplay loop.

---

# 104. KARGONUN ARAÇTA KAYBOLMASI YASAK

Kargoyu yükledikten sonra:

```text
camera move
vehicle drive
player exit
player re-enter
```

yapıldığında kargo hâlâ görünmeli.

---

# 105. ARAÇTA KARGONUN YERİ

Kargolar:

* kasa içinde
* oyuncunun görüşünde
* araç sınırları içinde

olmalı.

Kutu araç gövdesinden geçmemeli.

---

# 106. BİSİKLET İÇİN ÖZEL KURAL

Bisiklette cargo area küçük olduğu için:

**kutular bisikletin dışına taşmamalı.**

Gerekirse:

* 3–6 fiziksel yerleşim noktası
* kontrollü stack

kullan.

Ama oyuncuya:

```text
Kargoyu araca yükledim
```

hissi ver.

---

# 107. FİZİKSEL KASA

Bisiklet kasası:

* kutunun içine gömülmesini engellemeli
* üstten yüklemeye izin vermeli
* kenarlarda kutunun düşmesini azaltmalı

Tamamen kapalı bir kutu yapma.

Oyuncu kargoyu görebilmeli.

---

# 108. KARGO YÜKLEME ANİMASYONU

Demo 1 için zorunlu değil.

Kutu:

```text
Held
↓
E
↓
Cargo placement
```

ile kısa ve temiz bir hareket yapabilir.

Uzun animasyon gerekmiyor.

---

# 109. KARGO ALMA ANİMASYONU

Demo 1 için:

* gerçek el animasyonu şart değil.

Ancak fiziksel kutunun:

```text
world
→
hand
```

geçişi gözle anlaşılır olmalı.

---

# 110. KARGO FİZİĞİNDE HEDEF

Gerçekçi fizik:

**değil.**

Kontrollü fizik:

**evet.**

Oyuncunun amacı:

```text
kargo taşımak
```

olduğu için fizik:

```text
stable
predictable
forgiving
```

olmalı.

---

# 111. PERFORMANS

10–14 kargo aktif olabilir.

Ancak gereksiz:

* Update
* Rigidbody simulation
* collision
* allocation

yapma.

Elde/araçta bulunan kargolar için physics mümkün olduğunca optimize edilmeli.

---

# 112. KARGO SİSTEMİ EVENTLERİ

Gerekirse:

```text
ParcelCreated
ParcelPickedUp
ParcelDropped
ParcelPlacedOnTable
ParcelPackaged
ParcelLoaded
ParcelUnloaded
ParcelAtDoor
ParcelDelivered
ParcelFailed
```

kullan.

Ancak event spam oluşturma.

Mevcut `GameEvents` ile uyumlu çalış.

---

# 113. ECONOMY ENTEGRASYONU

Cargo:

**para hesabı yapmaz.**

Cargo sadece kargo datasını tutar.

Delivery:

```text
ParcelDelivered
```

olayını üretir.

Economy:

```text
Payment
XP
```

hesaplar.

---

# 114. SAVE ENTEGRASYONU

Cargo:

**save dosyası oluşturmaz.**

SaveSystem:

Cargo state'i serialize eder.

Böylece sorumluluklar ayrılır.

---

# 115. GAME DAY ENTEGRASYONU

Cargo:

günün saatini yönetmez.

DaySystem:

zamanı yönetir.

Cargo yalnız:

```text
CreatedAt
Deadline
```

gibi bilgileri taşır.

---

# 116. CARGO MANIFEST

Manifest yalnız:

```text
hangi kargolar mevcut?
```

sorusunu cevaplamalı.

Şunları yapmamalı:

* fizik spawn
* player movement
* vehicle movement
* economy

---

# 117. CARGO WORLD OBJECT

Physical world representation ayrı tutulmalı.

Bu sayede:

```text
Parcel Data
```

ile:

```text
GameObject
```

birbirine gereksiz şekilde bağlanmaz.

---

# 118. KARGO SİSTEMİNİN ANA AKIŞI

Tek satırlık nihai model:

```text
AVAILABLE
→
PICKED UP
→
CARRIED
→
PLACED
→
PICKED UP
→
PACKED
→
CARRIED
→
LOADED
→
TRANSPORTED
→
UNLOADED
→
CARRIED
→
AT DOOR
→
VERIFIED
→
DELIVERED
```

---

# 119. GERÇEK OYUNCU DENEYİMİ

Ben:

```text
E
```

basınca ne olduğunu anında görmeliyim.

Kargo:

**eline geçmeli.**

Ben:

```text
G
```

basınca:

**yere koymalıyım.**

Ben başka yere yürüyünce:

**kutu orada kalmalı.**

Ben tekrar bakınca:

**aynı kutuyu tekrar alabilmeliyim.**

---

# 120. "OTOMATİK INVENTORY" YASAK

Bu sistem:

```text
E
→
+1 Cargo
```

gibi çalışmayacak.

Oyuncu:

**fiziksel kargo objesini** alacak.

---

# 121. "OTOMATİK YÜKLEME" YASAK

Şu davranış yasak:

```text
Kargo elimde
↓
aracın yanına geldim
↓
sistem kendiliğinden yükledi
```

Yükleme ancak:

```text
E
```

ile.

---

# 122. "TOPLU YÜKLEME" YASAK

Şu ilk demo için yasak:

```text
E
→ tüm kargolar yüklendi
```

Her kargo:

**tek tek.**

---

# 123. "TOPLU ÇIKARMA" YASAK

Aynı şekilde:

```text
E
→ tüm kargolar yere çıktı
```

yok.

---

# 124. OYUNCUYA SAYGI

Oyuncunun yaptığı fiziksel işler sistem tarafından gereksiz yere atlanmamalı.

Kargo:

```text
gör
→ al
→ taşı
→ koy
```

hissini koru.

---

# 125. DEMO 1 KARGO SENARYOSU

Tam test:

```text
GAME START
↓
Depot
↓
Customer
↓
E
↓
Paketli kutu
↓
Kargoyu gör
↓
E
↓
Kargoyu al
↓
Yürü
↓
G
↓
Kargoyu yere bırak
↓
Tekrar bak
↓
E
↓
Tekrar al
↓
Bisiklete git
↓
Kargoyu taşı
↓
E
↓
Bisiklete yükle
↓
Kasanın içinde gör
↓
F
↓
Bisiklete bin
↓
Sür
↓
Adres
↓
F
↓
İn
↓
Kargoya bak
↓
E
↓
Kargoyu çıkar
↓
Kapıya taşı
↓
E
↓
NPC
↓
Kod
↓
Kod doğru
↓
Teslim
↓
+Para
↓
+XP
```

Bu senaryoda:

**kargo hiçbir yerde kaybolmayacak veya otomatikleşmeyecek.**

---

# 126. NEGATİF TESTLER

## N1

Kutuya çok uzaktan E:

```text
FAIL
```

## N2

Elinde kutu varken ikinci kutuya E:

```text
FAIL
```

## N3

Geçersiz yüzeye G:

```text
FAIL
```

## N4

Kapasite aşımı:

```text
FAIL
```

## N5

Yanlış kargo + yanlış adres:

```text
FAIL
```

## N6

Yanlış kod:

```text
FAIL
```

## N7

NPC yok:

```text
FAIL
```

## N8

Teslim edilmiş kargoyu tekrar teslim:

```text
FAIL
```

## N9

Save/load:

```text
STATE PRESERVED
```

## N10

Araçtan in:

```text
CARGO PRESERVED
```

---

# 127. REGRESSION TEST

Şunlar mutlaka test edilmeli:

```text
Pickup
↓
Drop
↓
Pickup again

Pickup
↓
Load
↓
Unload

Load
↓
Drive
↓
Exit vehicle
↓
Re-enter vehicle

Load
↓
Save
↓
Reload

Wrong code
↓
Retry
↓
Correct code
```

---

# 128. OTOMATİK TESTLER

Minimum test grubu:

### Parcel Data

* unique ID
* valid weight
* valid volume
* valid state

### Pickup

* correct object selected
* one parcel only
* pickup state

### Drop

* valid surface
* invalid surface
* no loss

### Vehicle Load

* capacity weight
* capacity volume
* correct state
* visible cargo

### Vehicle Unload

* one parcel only
* correct parcel selected
* state preserved

### Delivery

* correct parcel
* wrong parcel
* wrong code
* not home
* successful delivery
* duplicate delivery

### Save

* parcel state persists

---

# 129. MANUEL TEST ZORUNLU

Bu sistemin tamamlanmasında otomatik test tek başına kabul değildir.

Gerçek klavye + mouse ile oynanacak.

Minimum:

```text
01 Pickup
02 Carry
03 Walk with cargo
04 Drop
05 Pickup again
06 Carry to vehicle
07 Load
08 Inspect cargo in vehicle
09 Drive
10 Exit vehicle
11 Unload one cargo
12 Carry to door
13 Delivery
14 Wrong code
15 Retry
16 Save/load
```

Her biri:

```text
PASS
FAIL
BLOCKED
NOT EXECUTED
```

---

# 130. EN ÖNEMLİ MANUEL TEST

Özellikle bu senaryoyu:

```text
Kargo al
→
20 metre taşı
→
yere bırak
→
başka yere git
→
geri dön
→
aynı kargoyu al
→
bisiklete götür
→
yükle
→
sür
→
in
→
araçtan tekrar al
→
kapıya götür
→
teslim et
```

hiç reset atmadan tamamla.

Bu test geçmeden sistem kabul edilmez.

---

# 131. GÖRSEL KABUL KRİTERLERİ

Oyun sırasında hiçbir noktada:

* kutu havada
* kutu yerde yarım gömülü
* kutu araç dışına taşmış
* kutu duvar içinde
* kutu oyuncunun yüzünde
* kutu başka kutunun içine gömülmüş
* kargo havaya uçmuş

olmamalı.

---

# 132. ETKİLEŞİM KABUL KRİTERLERİ

Oyuncu:

```text
E
```

bastığında sistem:

**tek bir mantıklı işlem** yapmalı.

Bir tuş:

```text
pickup + load + delivery
```

gibi birden fazla farklı sonucu tetiklememeli.

---

# 133. RESPONSIVE GAMEPLAY

Oyuncu tuşa bastığında:

* gecikme hissedilmemeli
* input kaçırılmamalı
* iki kez tetiklenmemeli

özellikle:

```text
E
G
```

için duplicate input koruması yapılmalı.

---

# 134. STATE KİLİTLENMESİ

Örneğin:

```text
Pickup işlemi devam ediyor
```

iken tekrar E:

**aynı işlemi ikinci kez başlatmamalı.**

---

# 135. FAIL-SAFE

Her beklenmedik durumda kargo kaybolmak yerine:

```text
safe fallback state
```

ile korunmalı.

Örneğin fizik yerleşimi başarısız olduysa:

```text
Held
```

veya:

```text
PreviousValidState
```

korunabilir.

---

# 136. DEBUG / DEVELOPMENT

Development sırasında:

```text
ParcelId
CurrentState
Owner
Parent
RigidBodyState
```

gibi debug bilgileri loglanabilir.

Release gameplay'de gereksiz log bırakma.

---

# 137. ESKİ TESTLER

Eski cargo testlerini körü körüne koruma.

Eğer testler eski kötü davranışı test ediyorsa:

**sil veya yeniden yaz.**

Özellikle:

```text
"TryLoad automatically loads parcel"
```

gibi oyuncunun gerçek davranışını temsil etmeyen testler kabul edilmeyecek.

---

# 138. TEST BAŞARISI

Örneğin:

```text
300/300
```

olması tek başına yeterli değildir.

Ayrıca:

```text
FULL MANUAL CARGO FLOW = PASS
```

gereklidir.

---

# 139. TEMİZLİK

Eski sistem tamamen kaldırıldıktan sonra:

* unused component
* dead methods
* eski cargo event
* eski cargo references
* eski test helper
* eski inspector field

temizlenmeli.

Build'de kullanılmayan kötü sistem artık runtime'da çalışmamalı.

---

# 140. MIGRATION

Eski save dosyaları yeni cargo state'iyle uyumsuzsa:

* schema version kontrolü yap
* gerektiğinde migration yap

Ama kullanıcı verisini sessizce bozma.

---

# 141. ASSET

İlk aşamada mevcut kutu assetleri kullanılabilir.

Ancak asset bozuksa:

* collision
* pivot
* scale

düzelt.

Yeni büyük asset paketi indirme.

---

# 142. PIVOT

Kutu prefabının pivotu:

**mantıklı merkezde** veya placement sistemine uygun konumda olmalı.

Araçta kutu merkezleri buna göre hizalanmalı.

---

# 143. SCALE

Unity:

```text
1 unit ≈ 1 meter
```

mantığı korunmalı.

Kutu boyutları oyuncu açısından gerçekçi görünmeli.

---

# 144. RIGIDBODY

World cargo için:

* mass uygun
* drag uygun
* angular drag uygun
* interpolation uygun

olarak ayarlanmalı.

Ancak fizik saçmalamasın diye değerler ölçülerek tune edilmeli.

---

# 145. PHYSICS MATERIAL

Gerekirse kutular için:

* düşük/orta friction
* kontrollü bounciness

kullan.

Ama:

**top gibi zıplayan koli istemiyoruz.**

---

# 146. CARGO COLLISION TEST

Şunları test et:

```text
box → floor
box → wall
box → table
box → vehicle
box → box
box → player
```

---

# 147. ARAÇ KASASI COLLISION TEST

Kargo araç içindeyken:

```text
vehicle acceleration
vehicle braking
vehicle turning
vehicle incline
```

test et.

Kutu kasadan çıkmamalı.

---

# 148. BİSİKLET TESTİ

Özellikle:

```text
cargo empty
cargo 1
cargo 2
cargo near capacity
cargo full
```

durumlarında sür.

Bisiklet fizik davranışı kontrol edilmeli.

---

# 149. KARGO VE OYUNCU HIZI

Elde kutu taşırken:

* koşu mümkün
* hız biraz düşebilir
* ama oyun işkenceye dönüşmemeli

---

# 150. YÖNETİM FELSEFESİ

Oyuncu kargoyu:

**yönetiyor**

gibi hissetmeli.

Sistem oyuncunun yerine:

**oynamamalı.**

---

# 151. DEMO 1 İÇİN SON KARAR

Demo 1'de kargo mekaniğinin ana hedefi:

> "Kargo taşıyorum."

hissi.

Şimdilik:

* otomatik sorting
* forklift
* çalışanlar
* conveyor
* warehouse automation
* advanced inventory
* realistic damage

YOK.

---

# 152. TAM KARGO KABULÜ

Sistem ancak aşağıdaki maddeler tamamlanırsa kabul:

```text
[ ] Tek kargo pickup
[ ] Gerçek elde taşıma
[ ] Yürüme
[ ] Koşma
[ ] Controlled drop
[ ] Yeniden pickup
[ ] Packing interaction
[ ] Vehicle loading
[ ] Vehicle cargo visual
[ ] Vehicle unloading one-by-one
[ ] Correct parcel selection
[ ] Weight capacity
[ ] Volume capacity
[ ] Cargo persistence during driving
[ ] Cargo persistence when exiting vehicle
[ ] Correct address
[ ] Wrong address rejection
[ ] Wrong code rejection
[ ] Not home
[ ] Correct delivery
[ ] Money
[ ] XP
[ ] Save/load
[ ] Duplicate delivery prevention
```

---

# 153. TAM OYUNCU TESTİ

Şu cümleyi gerçekten kanıtla:

> "Oyuncu bir kargoyu fiziksel olarak alıp taşıyabiliyor, yere bırakıp tekrar alabiliyor, araca tek tek yükleyip araçtan tek tek çıkarabiliyor ve aynı kargoyu hiçbir debug müdahalesi olmadan doğru adrese taşıyıp teslim edebiliyor."

---

# 154. DEVELOPMENT ORDER

Bu görevi kendi içinde şu sırayla geliştir:

```text
1. Eski cargo sisteminin dependency analizi
2. Eski cargo gameplay'inin sökülmesi
3. Parcel data model
4. Parcel state machine
5. Physical parcel prefab
6. Pickup
7. Carry
8. Drop
9. Pickup tekrar
10. Packing table integration
11. Cargo area
12. Vehicle load
13. Vehicle unload
14. Weight/volume capacity
15. Delivery integration
16. Wrong cargo
17. Wrong code
18. Not home
19. Delivered
20. Economy integration
21. Save/load
22. UI/HUD
23. Automated tests
24. Manual gameplay test
25. Regression
26. Cleanup
```

Bir aşama bozuksa sonraki aşamada bug'ın üstünü kapatma.

---

# 155. IMPLEMENTATION KURALI

Her aşamadan sonra:

```text
Compile
↓
Automated test
↓
Manual check
```

yap.

---

# 156. MANUEL TEST YAPAMIYORSAN

Eğer çalışma ortamı gerçek mouse/keyboard girişi ve Unity görünümünü test etmene izin vermiyorsa:

```text
MANUAL TEST = NOT EXECUTED
```

yaz.

Bunu:

```text
PASS
```

olarak gösterme.

---

# 157. BAŞARISIZLIK DURUMU

Eğer fiziksel pickup düzgün değilse:

```text
STOP
```

ve problemi çöz.

Önce:

**pickup**

sonra:

**carry**

sonra:

**drop**

sonra:

**load**

şeklinde ilerle.

---

# 158. BU GÖREVDE YENİ FEATURE YOK

Şunları bu görevde yapma:

* yeni araç
* trafik
* hikâye
* çalışan
* yeni bölge
* online satış
* warehouse
* multiplayer
* araç upgrade
* gelişmiş ekonomi
* hava durumu

---

# 159. SONUÇ

Bu görevin sonunda eski cargo mekanikleriyle yeni cargo mekanikleri birbirine karışmamalı.

Olması gereken:

```text
OLD CARGO
     X
     X
     X

NEW CARGO
     ↓
Physical Parcel
     ↓
Player Carry
     ↓
Drop
     ↓
Pick Up
     ↓
Vehicle Cargo
     ↓
Unload
     ↓
Delivery
```

---

# 160. DEVELOPMENT REPORT

Görev sonunda:

## 1. TASK SUMMARY

Eski sistem neden kaldırıldı ve yeni sistem ne amaçlıyor?

## 2. OLD SYSTEM REMOVED

Hangi eski cargo sistemleri kaldırıldı?

## 3. NEW SYSTEM CREATED

Yeni mekanikler.

## 4. CREATED FILES

## 5. MODIFIED FILES

## 6. ARCHITECTURE

Data / Physical Object / Carry / Vehicle Cargo / Delivery ayrımı.

## 7. STATE MACHINE

Tüm state geçişleri.

## 8. AUTOMATED TEST RESULTS

```text
Compile:
EditMode:
PlayMode:
```

## 9. MANUAL TEST RESULTS

Şu maddeleri tek tek:

```text
Pickup
Carry
Walk
Run
Drop
Pickup again
Packing
Load
Vehicle drive
Vehicle exit
Unload
Carry to door
Wrong cargo
Wrong code
Not home
Correct delivery
Save/load
```

için:

```text
PASS
FAIL
BLOCKED
NOT EXECUTED
```

## 10. BUGS FOUND

## 11. BUGS FIXED

## 12. KNOWN ISSUES

## 13. PERFORMANCE

## 14. FINAL VERDICT

Mutlaka:

```text
FULL CARGO GAMEPLAY PLAYABLE: YES / NO
```

ve:

```text
PHYSICAL PICKUP: YES / NO
PHYSICAL CARRY: YES / NO
PHYSICAL DROP: YES / NO
VEHICLE LOAD/UNLOAD: YES / NO
DELIVERY INTEGRATION: YES / NO
SAVE/LOAD: YES / NO
MANUAL PLAYTHROUGH: PASS / FAIL / NOT EXECUTED
```

yaz.

---

# 161. PM KARARI

Bu görev tamamlanmadan:

**hikâye, dükkân, yeni araç, yeni içerik geliştirmeye geçme.**

Çünkü Kargo Simülatörü'nün temel gameplay'i:

> **KARGOYU AL → TAŞI → ARAÇA KOY → SÜR → ÇIKAR → TESLİM ET**

olduğundan bu mekanik sağlam değilse geri kalan bütün sistemler anlamsızdır.

Bu nedenle:

**KARGO SİSTEMİ = P0 TEMEL OYNANIŞ SİSTEMİ.**
