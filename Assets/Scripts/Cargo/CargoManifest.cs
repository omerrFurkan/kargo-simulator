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

        /// <summary>Depoda bekleyen kargolar (teslim edilmemis).</summary>
        public IReadOnlyList<Parcel> Available => _available;

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
            if (!_loaded.Remove(parcel)) return false;

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
            if (parcel.State != ParcelState.WaitingInDepot) return false;

            if (!Fits(parcel)) return false;

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
