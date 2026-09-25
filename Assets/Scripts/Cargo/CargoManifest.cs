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
        public void BeginDay(IReadOnlyList<Parcel> parcels)
        {
            _available.Clear();
            UnloadAll();

            if (parcels == null) return;
            for (int i = 0; i < parcels.Count; i++)
                if (parcels[i] != null) _available.Add(parcels[i]);
        }

        /// <summary>Araci bosaltir (gun sonu veya teslim hatalari).</summary>
        public void UnloadAll()
        {
            for (int i = 0; i < _loaded.Count; i++)
                _loaded[i]?.MarkReturnedToDepot();

            _loaded.Clear();
            LoadedWeightKg = 0f;
            LoadedVolumeLiters = 0f;
        }

        /// <summary>
        /// Kargoyu araca yukler. Iki kapasinin da sigmasi gerekir.
        /// </summary>
        /// <returns>Basariliysa true, sigmiyorsa false (liste degismez).</returns>
        public bool TryLoad(Parcel parcel)
        {
            if (parcel == null) return false;
            if (!Capacity.IsValid) return false;
            if (parcel.State != ParcelState.WaitingInDepot) return false;

            if (!Fits(parcel)) return false;

            _available.Remove(parcel);
            _loaded.Add(parcel);
            parcel.MarkLoaded();

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
