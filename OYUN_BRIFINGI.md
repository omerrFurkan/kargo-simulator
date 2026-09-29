# KARGO SİMÜLATÖRÜ — Oyun Brifingi

> Bu belge, oyunu **ilk kez duyan** biri içindir. Oyun ne, kim oynuyor, nasıl
> oynanıyor ve ilk demo ne içerecek — hepsi burada.
>
> Sürüm: İlk oynanabilir dikey dilim (vertical slice)

---

## 1. Oyun ne?

**Kargo Simülatörü**, bir kuryenin günlük işini baştan sona yaptığı, üç boyutlu bir
benzetim oyunudur. Şehirde dolaşırsınız, depolar ve dükkanlardan paket alırsınız,
kendi aracınıza yüklersiniz, adreslere götürürsünüz ve teslim edersiniz. Karşılığında
para kazanır, seviye atlar, gün sonunda günün hesabını görürsünüz.

Kısaca: **"Ne kadar çok kargo, o kadar çok para"** temelli, zamana karşı yarışan bir
kargo oyunu.

Bir yarış oyunu **değildir**. Hız için yarışmak yerine düzenli ve okunabilir bir akış
hedeflenir: oyuncu nereye gideceğini, elinde ne olduğunu ve aracında ne kaldığını
her an bilmelidir.

---

## 2. Oyuncu kim?

Oyuncu bir **kargo kuryesidir**.

- Adı yoktur, sen yaratmazsın.
- Tek bir günde birden çok teslimat yapar.
- Başlangıçta elinde boş bir kargo bisikleti vardır.
- Para kazandıkça seviye atlar; seviye arttıkça daha çok ve daha ağır kargo taşır.

Oyuncu rolü oyunda üçüncü şahıs bir gözle değil, **birinci şahıs** (FPS) bir gözle
oynanır. Yani ekranda kendi karakterini değil, etrafı görürsün.

---

## 3. Oyun nasıl oynanır?

### 3.1 Kontroller

| Tuş | İşlev |
|---|---|
| **W A S D** | Yürü (arac içindeyken sür) |
| **Fare** | Etrafa bak |
| **Sol tık** | Fare kilidini yeniden yakala |
| **E** | Bağlamsal etkileşim: konuş, kutu al, kutu bırak, kapı çal, araca yükle |
| **F** | Araca bin / araçtan in |
| **G** | Elindeki kutuyu yere bırak |
| **Space** | Fren (bisiklet) |
| **Shift** | Koş (yük taşırken yavaşlar) |
| **TAB** | Depo panelini aç / kapat, gün sonu özetini aç / kapat |
| **1 – 9** | Depo panelinde kargo seç |
| **Fare ile bekleme** | Bir eşyanın üstünde bek: küçük pencerede özellikleri çıkar |

> **E tuşu her zaman bağlamsaldır.** Neyin önünde durduğuna göre farklı iş yapar.
> Bu yüzden "E'ye bas" yerine "önünde ne varsa E'ye bas" mantığı geçerlidir.

### 3.2 Bir gün şöyle geçer

```
1.  DEPOYA GİT
    Şehrin merkezindeki depoda müşteriler sıraya girer.

2.  MÜŞTERİYLE KONUŞ  (E)
    Müşterinin yanına yürü, E'ye bas.
    Sana PAKETLİ bir kutu verir. Kutu hazırdır, paketlemeye gerek yok.

3.  KUTUYU İSTEDİĞİN YERE KOY  (G)
    Kutuyu dükkânın içinde herhangi bir yere bırakabilirsin.
    Alışveriş yapmaya devam edebilir, sonra geri alırsın.

4.  ARACA BİN  (F)
    Yanında durduğun aracın yanına git ve F'e bas.
    Elindeki kutuları araca koyabilmen için önce aracın yanında olman gerekir.

5.  ARACIN DOLDUR  (E)
    E'ye basınca elindeki paketli kutular araca yüklenir.

6.  ADRESE GİT
    Rotanın işaret ettiği adrese sür. Ekranda hangi adrese gideceğin yazar.

7.  KAPIYI ÇAL  (E)
    Kapının önünde E'ye bas. Zil çalar, kapı çalınır.

8.  KODU GİR
    Müşteri kapıyı açar ve sana 4 haneli bir teslim kodu söyler.
    Kodu girersin.

9.  TESLİM TAMAMLANDI
    Para kazanırsın, XP kazanırsın, rota bir sonraki adrese geçer.

10. TEKRARLA
    Gün bitene kadar teslimat yap. Gün sonunda özet çıkar.
```

### 3.3 Eğer bir şey yanlış giderse

| Durum | Ne olur | Ne yapmalı |
|---|---|---|
| Kutuyu aracaya koyamıyorsun | Kutu paketlenmemiş | Zaten paketli geliyor; sorun değilse HUD ipucuna bak |
| "Kapasite dolu" diyorsun | Araç limitini aştın | Elindeki kutuyu yere bırak (G) veya başka araca geç |
| Kapıda kod çıkmıyor | Müşteri evde değil | Bir sonraki adrese geç, sonra geri dön |
| Kod yanlış çıkıyor | Tespit edilmedi | Tekrar dene, kodu dikkatli gir |
| Araçtan inemiyorsun | Araç çok hızlı | Yavaşla, sonra F'e bas |
| Adres görünmüyor | Rotan yok | Depoya dön, yeni kargo al |

---

## 4. Temel sistemler

### 4.1 Kargo

Oyunda altı tür kargo vardır. Hepsi farklı ağırlık ve hacimdedir — bu yüzden
bir araca kaç kutu sığacağı değişir.

| Tür | Ağırlık | Hacim | Not |
|---|---|---|---|
| Belge | 0,4 – 1,2 kg | 1 – 3 L | En hafif |
| Kırtasiye | 1 – 3 kg | 4 – 12 L | |
| Elektronik | 1,5 – 5 kg | 8 – 25 L | Kırılgan olabilir |
| Gıda | 2 – 6 kg | 10 – 30 L | |
| İlaç | 0,5 – 2 kg | 2 – 8 L | Acele etmek önemli |
| Eşya | 3 – 8 kg | 15 – 45 L | En ağır, en büyük |

Her kutu taşır:
- **alıcı adı** (örn. "Derya Akın")
- **adres numarası**
- **teslim kodu** (4 hane, kutuya özel)
- **kırılganlık** (varsa dikkatli sür)

### 4.2 Araçlar

| Araç | Kapasite | Karakteristik |
|---|---|---|
| **Kargo Bisikleti** (başlangıç) | 60 kg / 150 L | Üç tekerli, ağır ama çevik, sürücü kamerası |
| **Kargo Vapuru** | 350 kg / 900 L | Dört tekerli, çok yer, arka kamerayla takip |

Oyun bisikletle başlar. Vapur ileride açılabilir.

### 4.3 Teslimat

- Her adresin bir **kapısı** ve bir **sakin NPC** vardır.
- Teslimat ancak **sakin evdeyse** yapılır.
- Kapı çalınca zil çalar, kapı açılır ve NPC kodu söyler.
- Doğru kod → teslim tamamlanır, para ödenir.
- Yanlış kod → teslim olmaz, para ödenmez, tekrar deneyebilirsin.
- Bazı kargolarda **süre** vardır; süre geçerse risk artar.

### 4.4 Ekonomi ve İlerleme

- Günde belirli bir miktar parayla başlarsın.
- Teslimat başına para kazanırsın.
- XP birikir, seviye atarsın.
- Seviye yükseldikçe daha büyük kargo taşıyabilirsin.

### 4.5 Gün Döngüsü

- Saat ilerler (her gün sabah 08:00'de başlar).
- Gün bitince **gün özeti** çıkar: kaç teslimat, ne kadar kazanç, ne kadar zaman.
- Ertesi gün yeni kargo listesiyle başlar.

### 4.6 Arayüz (HUD)

Bilgiler ekranın **köşelerinde** durur — oyuncu baktığı alanın ortası boş kalır.

| Konum | Ne gösterir |
|---|---|
| Sol üst | Para, seviye, XP çubuğu, saat |
| Sağ üst | Küçük harita (sen, araç, depo, duraklar) |
| Sol alt | Yük kapasitesi (kaç kutu / kg / L) |
| Sağ alt | Sıradaki hedef ve mesafe |
| Alt orta | Bağlamsal ipucu ("E ile konuş" gibi) |
| Merkez | Sadece zorunlu pencereler (kod girişi, gün özeti) |

Fareyle bir kutu veya aracın üzerine gelersen **küçük bir bilgi penceresi** açılır:
kutu numarası, alıcı, adres, ağırlık, hacim, paket durumu ve teslim kodu.

---

## 5. İlk demo prototipi (demo 1) ne içerecek?

Demo 1'in amacı **"bu oyun oynanabilir bir şey"** demektir. Üstünlük olarak çalışan
bir akış önerilir; eksik kalan buraya girmez.

### 5.1 Demo 1'de olacaklar

- [x] Depoda müşteriler gelir, konuşunca **paketli kutu verir**
- [x] Kutuyu dükkânda istediğin yere bırakabilir ve geri alabilirsin
- [x] Kargo bisikleti ve kargo vapuru
- [x] Araca bin / in (bindiğin yere yakın in)
- [x] Elindeki kutuyu araca koyma, araçtan kutu alma
- [x] Kapasite sınırı (bisiklet 60 kg / 150 L, vapur 350 kg / 900 L)
- [x] Haritada adres kapıları ve adres göstergeleri
- [x] Kapı çalma + **zil sesi**
- [x] Teslim kodu girişi (doğru / yanlış)
- [x] Para, XP, seviye
- [x] Küçük harita (araç konumu, duraklar)
- [x] Köşelere yerleşik HUD
- [x] Eşya özellik penceresi (fare ile bekleme)
- [x] Gün döngüsü ve gün sonu özeti
- [x] Kayıt (ilerleme kaybolmasın)

### 5.2 Demo 1'de olmayacaklar

Bunlar bilinçli olarak ertelenmiştir. Demo 1'in amacı akışı doğrulamaktır.

- **Karakter envanteri paneli ve araç bagaj paneli** (şu an yapılıyor)
- Yakıt, bakım, arıza, para ekonomisi derinliği
- Birden fazla depo veya harita
- NPC ile derin sohbet seçenekleri
- Görev zinciri, sözleşmeler
- Kötü yol, kaza, ceza sistemi
- Çok oyunculu
- Dış ses kaynakları (şu an sesler kod içinde üretiliyor)

### 5.3 Demo 1 "bitti" sayılır mı?

Aşağıdaki kontrol listesi tamamlanırsa demo 1 tamamlanmış sayılır:

**Temel akış**
1. [ ] Oyuna gir → yaya başlıyorum, yanımda bisiklet var
2. [ ] Depoya yürü → müşteriyi görüyorum
3. [ ] Müşteriye E → paketli kutu elimde
4. [ ] G ile kutuya bir yere bırak → geri al
5. [ ] Bisiklete F ile bin
6. [ ] E ile kutuyu araca koy, kasada görüyorum
7. [ ] Sür → hedef adrese ulaş
8. [ ] E ile kapıyı çal → zil çalar
9. [ ] Kodu gir → teslim tamamlanır
10. [ ] Para artar
11. [ ] Araçtan in → aracın yanında beliririm
12. [ ] Aynı şekilde vapurla dene

**Kabul ölçütleri**
- [ ] Yukarıdaki 12 adım **kesintisiz** yapılabiliyor (hata mesajı olmadan)
- [ ] Araçta hareket ederken kamera ve araç birlikte dönüyor
- [ ] Kapasite dolunca doğru ve anlaşılır uyarı çıkıyor
- [ ] Yanlış kod para vermiyor
- [ ] Oyun kapatılıp açılınca ilerleme korunuyor
- [ ] Ekranda sürekli takılmaya neden olan bir hata yok

---

## 6. Teknik kimlik kartı

| Konu | Değer |
|---|---|
| Motor | Unity 6 (6000.3.25f1) |
| Grafik | URP (Universal Render Pipeline) |
| Dil | C# |
| Girdi | Klasik Input Manager (Input.GetAxis / GetKeyDown) |
| Çözünürlük hedefi | 1920 × 1080, ölçeklenebilir Canvas |
| Otomatik test | 307 EditMode + 84 PlayMode |
| Kayıt | JSON, kalıcı veri klasöründe, sürümlü |

**Kod düzeni**

```
Assets/Scripts/
├── Core/        CompositionRoot, olaylar (GameEvents), saat, ayarlar
├── Cargo/       Kargo, manifest, kapasite, türler, teslim kodu
├── Player/      Kurye, kamera, kargo noktaları, müşteri, zemin eşyaları
├── Vehicle/     Bisiklet ve vapur fizikleri, teker görselleri
├── Delivery/    Rota, duraklar, adresler
├── Economy/     Para, XP, seviye
├── UI/          HUD, mini harita, paneller, özellik penceresi
└── Editor/      Sahne ve varlık kurulum araçları (menüler)
```

**Kod kuralları**

- Her sistem tek bir "Composition Root" tarafından kurulur; sahneye elle bağlama yapılmaz.
- Olaylar `GameEvents` üzerinden yayılır.
- Kullanıcıya görünen her yeni hata mesajı, oyuncuya bir sonraki adımı söyler.

---

## 7. Şu anki durum

**Çalışan ve test edilenler:** temel akışın tamamı, araçlar, kargo, teslimat kodu,
ekonomi, gün döngüsü, HUD, mini harita, zil, kayıt, müşteri teslimi.

**Üzerinde çalışılan:** karakter envanteri paneli ve araç bagaj paneli.

**Bilinmeyen / netleştirilmesi gerekenler**

- Karakterin harita zeminine yarı gömülü görünmesi (çözüm aranıyor)
- Bisiklet kadro/teker görsel hizasının gözle düzeltilmesi
- "Akıl yütütme işleyişi" talebi: hangi kararın değişmesi istendiği net değil

---

## 8. Bu belgeyi okuyan kişi için tek cümle

> Depoda müşteriden paketli kutu al, aracına koy, adrese sür, kapıyı çal, kodu
> gir, parayı al. Gün bitene kadar tekrarla.
