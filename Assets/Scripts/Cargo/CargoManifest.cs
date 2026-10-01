using System;
using System.Collections.Generic;

namespace KargoSim.Cargo
{
    /// <summary>
    /// Aracın kargo kapasitesi. Iki ayri sinir vardir ve ikisi de dolmali:
    ///
    ///   agir ama kucuk   -> agirligi dolarsin, hacim bos kalir
    ///   hafif ama devasa -> hacmi dolarsin, agirligi bos kalir
    ///
    /// Tek bir sinir kullanmak oyunu bozardi: sadece agirlik olsaydi oyuncu
    /// devasa ama bos paketlerle sinirsiz yuk tasirdi.
    /// </summary>
    public readonly struct CargoCapacity
    {
        public float MaxWeightKg { get; }
        public float MaxVolumeLiters { get; }

        public CargoCapacity(float maxWeightKg, float maxVolumeLiters)
        {
            MaxWeightKg = maxWeightKg;
            MaxVolumeLiters = maxVolumeLiters;
        }

        public bool IsValid => MaxWeightKg > 0f && MaxVolumeLiters > 0f;
    }

    /// <summary>
    /// Iki kargo yuku: depoda bekleyenler ve aracaki teslimatlar.
    ///
    /// Bu sinif SADECE listeleri ve kurallari yonetir; para, sure, teslimat
    /// hesabi YAPMAZ (Aşama 4/5). Tek sorumlulugu: hangi kargo aracda ve
    /// kapasite kurallari ne.
    /// </summary>
    public sealed class CargoManifest
    {
        private readonly List<Parcel> _available = new List<Parcel>();
        private readonly List<Parcel> _loaded = new List<Parcel>();

        /// <summary>
        /// KURYENIN ELINDEKI kutular (asama 33).
        ///
        /// Once bu yer yoktu: aractan alinan kutu _loaded listesinde
        /// kaliyordu. Teslimat icin dogruydu ama kutu hem kasada hem
        /// elde sayiliyordu ve envanter paneli yapilamiyordu.
        ///
        /// ONEMLI: kutu "elde" iken arac ROTASINDAN cikmaz.
        /// DeliverySystem duraklari _loaded VE _carried listesinden
        /// kurar; rota yalnizca ikisinden kuruldugu icin teslimat
        /// zinciri bozulmaz.
        /// </summary>
        private readonly List<Parcel> _carried = new List<Parcel>();

        /// <summary>Depoda bekleyen kargolar (teslim edilmemis).</summary>
        public IReadOnlyList<Parcel> Available => _available;

        /// <summary>Kuryenin elinde tutulan kutular (rota dahil).</summary>
        public IReadOnlyList<Parcel> Carried => _carried;

        /// <summary>
        /// Kutu aractan kuryenin eline gecti (asama 33).
        ///
        /// Kapasite arac kargosu icindir; kutu eldeyken araci mesgul
        /// etmez, boylece oyuncu kutuyu cekerek yer acabilir.
        /// </summary>
        /// <summary>
        /// KUTU KURYENIN ELINE GECTI (yeni tasiyici cagirir).
        ///
        /// ONCEKI SISTEMDE kuru kargoyu ALINCA manifest sadece
        /// "tasiniyor" diye bir bayrak tutuyordu; kargonun KENDI listesi
        /// degismiyordu. Bu yuzden yeni fiziksel tasima sisteminde
        /// kutu kuryeye gecse bile ROTA onu gormuyordu ve teslim
        /// yapilamiyordu.
        ///
        /// Kargo kaybolmaz; sadece listeler arasinda yer degistirir.
        /// </summary>
        public void NoteCarriedByPlayer(Parcel parcel)
        {
            if (parcel == null) return;
            if (!_carried.Contains(parcel)) _carried.Add(parcel);
        }

        /// <summary>Kutu kuryeden CIKTI (yere birakildi / araca kondu).</summary>
        public void NoteReleasedByPlayer(Parcel parcel)
        {
            if (parcel == null) return;
            _carried.Remove(parcel);
        }

        /// <summary>
        /// Kutu araca kondu: tasima listesinden arac listesine.
        /// </summary>
        public void NoteLoadedOnVehicle(Parcel parcel)
        {
            if (parcel == null) return;
            _carried.Remove(parcel);
            if (!_loaded.Contains(parcel)) _loaded.Add(parcel);
        }

        public bool UnloadParcel(Parcel parcel)
        {
            if (parcel == null) return false;
            if (!_loaded.Remove(parcel) && !_carried.Remove(parcel)) return false;

            LoadedWeightKg -= parcel.WeightKg;
            LoadedVolumeLiters -= parcel.VolumeLiters;
            if (LoadedWeightKg < 0f) LoadedWeightKg = 0f;
            if (LoadedVolumeLiters < 0f) LoadedVolumeLiters = 0f;

            if (!_carried.Contains(parcel)) _carried.Add(parcel);
            parcel.MarkCarried();
            return true;
        }

        /// <summary>Aracda olan kargolar.</summary>
        public IReadOnlyList<Parcel> Loaded => _loaded;

        public float LoadedWeightKg { get; private set; }
        public float LoadedVolumeLiters { get; private set; }

        /// <summary>Gun basinda degisir.</summary>
        public CargoCapacity Capacity { get; private set; }

        public CargoManifest()
        {
            Capacity = new CargoCapacity(0f, 0f);
        }

        /// <summary>Aracin kapasitesini ayarlar (arac degistiginde cagrilir).</summary>
        public void SetCapacity(CargoCapacity capacity) => Capacity = capacity;

        /// <summary>
        /// Paketleme zorunlu mu? Aciksa yalnizca <see cref="ParcelState.Ready"/>
        /// kargolar araca yuklenebilir.
        /// </summary>
        /// <remarks>
        /// Neden manifest'te: kapasite de manifest'in isidir, "bu kargo
        /// araca girebilir mi" sorusu da ayni yere ait. Kural UI'da
        /// degil, TEK yerde durmali; yoksa depo paneli bir yere, arac
        /// noktasi baska yere bakip biri unutur.
        /// </remarks>
        public bool RequiresPackaging { get; set; }

        /// <summary>
        /// Kargo araca yuklenebilir mi? Sadece sorar.
        ///
        /// Zamanlama ve kapsite bakmadan SADECE uygunluk doner; UI ipucu
        /// vermek icin kullanilir.
        /// </summary>
        public bool CanLoad(Parcel parcel)
        {
            if (parcel == null) return false;
            if (!parcel.IsActive) return false;

            if (RequiresPackaging && !parcel.IsPackaged) return false;

            return Fits(parcel);
        }

        /// <summary>Depodaki listeyi degistirir (gun basinda cagrilir).</summary>
        /// <remarks>
        /// DUZELTME (Hata 2): Once _available.Clear() SONRA UnloadAll()
        /// sirasi vardi. UnloadAll artik kargoyu depoya geri ekledigi icin
        /// dunku arac kargolari yeni gunun deposuna karisip ikinci kez
        /// yuklenebilir hale geliyordu. Dogru sira: araci kalici olarak
        /// bosalt, sonra depoyu temizle, sonra yeni kargolari yaz.
        /// </remarks>
        public void BeginDay(IReadOnlyList<Parcel> parcels)
        {
            // Dunun kargolari oyun disi: yenisine dokunmadan birak.
            DiscardLoaded();
            _available.Clear();

            if (parcels == null) return;

            // Diger bir dosya bize Available listesinin KENDISINI verebilir.
            // _available'i temizlemek o listeyi de bosaltir, yani ayni
            // listeyi geri yazmak anlamsizdir. Bu durumda depoyu bos birak.
            if (ReferenceEquals(parcels, _available)) return;

            for (int i = 0; i < parcels.Count; i++)
                if (parcels[i] != null) _available.Add(parcels[i]);
        }

        /// <summary>
        /// Teslim edilen kargoyu listeden cikarir.
        ///
        /// Onemli: teslim edilen kargo depoya GERI DONDURULMEZ, tamamen
        /// cikarilir. Aksi halde teslim ettikten sonra rota yeniden
        /// kuruldugunda ayni adrese tekrar gidilirdi.
        /// </summary>
        public bool MarkDelivered(Parcel parcel)
        {
            if (parcel == null) return false;
            if (!_loaded.Remove(parcel) && !_carried.Remove(parcel)) return false;

            LoadedWeightKg -= parcel.WeightKg;
            LoadedVolumeLiters -= parcel.VolumeLiters;

            // Yuvarlama birikmesi sonrasi kapasiteyi asmasin diye.
            if (LoadedWeightKg < 0f) LoadedWeightKg = 0f;
            if (LoadedVolumeLiters < 0f) LoadedVolumeLiters = 0f;

            parcel.MarkDelivered();
            return true;
        }

        /// <summary>
        /// Arsaci bosaltir ve kargoyu DEPOYA GERI DONDURUR (Q tusu).
        ///
        /// DUZELTME (Hata 2): eski surumde yalnizca MarkReturnedToDepot()
        /// cagrilip _loaded.Clear() yapiliyordu, _available listesine geri
        /// EKLENMIYORDU. Sonuc: oyuncu Q'ya basinca kargo o gunku listeden
        /// TAMAMEN kayboluyordu. FindById bulamiyor, depo paneli kargoyu
        /// gostermiyor ve oyuncu ayni gun o kargoyu ikinci kez yukleyip
        /// teslim edemiyordu - oyuncunun parasal kazanci sessizce yok oluyordu.
        /// Artik kargo aracdan cikar, depoya yazar ve yeniden yuklenebilir.
        /// </summary>
        public void UnloadAll()
        {
            for (int i = 0; i < _loaded.Count; i++)
            {
                Parcel parcel = _loaded[i];
                if (parcel == null) continue;

                parcel.MarkReturnedToDepot();

                // Yukleme sirasinda depodan cikarildigi icin burada asla
                // var olmamasi gerekir; yine de guvenlik icin tekrar eklemeyiz.
                if (!_available.Contains(parcel)) _available.Add(parcel);
            }

            _loaded.Clear();
            ResetLoadCounters();
        }

        /// <summary>
        /// Arsaci bosaltir ve kargoyu KALICI olarak yok eder (gun sonu basarisiz
        /// sayimi icin).
        ///
        /// DUZELTME (Hata 2): UnloadAll() ile ayni isi yapmaz. Orada kargo
        /// depoya geri doner; burada depoya hic girmez. Boylece basarisiz sayilan
        /// kargo ayni gun yeniden yuklenip yeniden basarisiz sayilamaz.
        ///
        /// DIKKAT: DeliverySystem.FailRemainingParcels() bu davranisi
        /// kullanmalidir; "Q ile bosalt" yolunun karsi istidir.
        /// </summary>
        public void DiscardLoaded()
        {
            for (int i = 0; i < _loaded.Count; i++)
                _loaded[i]?.MarkDiscarded();

            // _available listesine EKLENMEZ: kalici olarak kaybolur.
            _loaded.Clear();
            ResetLoadCounters();
        }

        /// <summary>
        /// Yukleme agirlik/hacim sayaclarini sifirlar.
        /// Iki bosaltma yolu da ayni sayaci paylasir, bu yuzden tek yerde.
        /// </summary>
        private void ResetLoadCounters()
        {
            LoadedWeightKg = 0f;
            LoadedVolumeLiters = 0f;
        }

        /// <summary>
        /// Kargoyu araca yukler. Iki kapasinin da sigmasi gerekir.
        /// </summary>
        /// <param name="parcel">Yuklenecek kargo.</param>
        /// <param name="systemTime">
        /// Simulasyon saati (saniye). Kargonun teslim suresi bu andan itibaren
        /// islemeye baslar. Varsayilan deger 0 - testler ve gun ilk kargolari
        /// icin uygundur.
        ///
        /// DUZELTME (Hata 1) notu: 0f "gun basindan say" demektir. Gunun
        /// gec yarisinda yuklenen kargo 0f ile yuklenirse geri sayim zaten
        /// yarisi dolmus baslar ve kargo dogustan "gec" sayilir; odeme ve XP
        /// dusuk kesilir. Oyun ici her yuklemede gercek simulasyon saati
        /// gecirilmelidir.
        /// </param>
        /// <returns>Basariliysa true, sigmiyorsa false (liste degismez).</returns>
        public bool TryLoad(Parcel parcel, float systemTime = 0f)
        {
            if (parcel == null) return false;
            if (!Capacity.IsValid) return false;
            if (parcel.State != ParcelState.Ready
                && parcel.State != ParcelState.WaitingInDepot
                && parcel.State != ParcelState.Carried)
                return false;

            // Paketleme zorunluysa ham kargo araca giremez.
            if (RequiresPackaging && !parcel.IsPackaged) return false;

            if (!Fits(parcel)) return false;

            if (parcel.State == ParcelState.Carried) _carried.Remove(parcel);
            _available.Remove(parcel);
            _loaded.Add(parcel);
            parcel.MarkLoaded(systemTime);

            LoadedWeightKg += parcel.WeightKg;
            LoadedVolumeLiters += parcel.VolumeLiters;
            return true;
        }

        /// <summary>
        /// Kargo bu araca sigar mi? Sadece sorar, hicbir sey degistirmez.
        /// UI'da "bu kargo sigmaz" ipucu vermek icin kullanilir.
        /// </summary>
        public bool Fits(Parcel parcel)
        {
            if (parcel == null) return false;

            return LoadedWeightKg + parcel.WeightKg <= Capacity.MaxWeightKg + Epsilon
                && LoadedVolumeLiters + parcel.VolumeLiters <= Capacity.MaxVolumeLiters + Epsilon;
        }

        /// <summary>Arac bos mu?</summary>
        public bool IsEmpty => _loaded.Count == 0;

        /// <summary>Yuklenen kargo sayisi.</summary>
        public int LoadedCount => _loaded.Count;

        /// <summary>Depodaki kargo sayisi.</summary>
        public int AvailableCount => _available.Count;

        /// <summary>Id ile arama (depo + arac).</summary>
        public Parcel FindById(int id)
        {
            for (int i = 0; i < _loaded.Count; i++)
                if (_loaded[i].Id == id) return _loaded[i];

            for (int i = 0; i < _available.Count; i++)
                if (_available[i].Id == id) return _available[i];

            // KURYENIN ELINDEKI kutular da aranir.
            //
            // EKLENMEDIGINDE NE OLUYORDU: kutu aractan cekilince
            // `_carried`a giriyordu. `FindById` sadece arac + depo
            // listelerini aradigi icin kutu BULUNAMIYOR ve yeniden araca
            // konulamiyordu. Oyuncu kutuyu cekip geri koyunca kutu
            // kaybolmus gibi gorunuyordu ("iki yonlu transfer calismiyor").
            for (int i = 0; i < _carried.Count; i++)
                if (_carried[i].Id == id) return _carried[i];
            return null;
        }

        /// <summary>
        /// Yuzde olarak arac dolulugu. HUD'da gosterilir.
        /// Iki kapsanin DOLUSU degerlendirilir; dolu olan kapasite belirler.
        /// </summary>
        public float LoadRatio
        {
            get
            {
                if (!Capacity.IsValid) return 0f;

                float byWeight = LoadedWeightKg / Capacity.MaxWeightKg;
                float byVolume = LoadedVolumeLiters / Capacity.MaxVolumeLiters;

                // System.Math: bu sinif bilerek UnityEngine'e bagli degil,
                // boylece motoru baslatmadan test edilebilir.
                return (float)System.Math.Max(0f, System.Math.Min(1f,
                    System.Math.Max(byWeight, byVolume)));
            }
        }

        // Yuvarlama hatalarina karsi tolerans. 0.0001 olmadan
        // 0.1 + 0.2 + 0.7 gibi toplamlar kapsayi "asip" reddedebilir.
        private const float Epsilon = 0.0001f;
    }
}
