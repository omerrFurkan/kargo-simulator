using System;
using System.Collections.Generic;
using KargoSim.Core;
using KargoSim.Vehicle;

namespace KargoSim.Cargo
{
    /// <summary>
    /// Kargo sistemini birlikte calistirir: gun basinda kargo uretir,
    /// aracin kapasitesini ayarlar, yukleme istegini yonlendirir.
    ///
    /// Bu katman SADECE yonlendirir: parcalar uretir, manifest'i doldurur ve
    /// olaylari yayar. Karar (hangi kargo secilecek) oyuncunundur.
    ///
    /// MonoBehaviour DEGIL: gun olaylarini GameEvents uzerinden dinler,
    /// oyuncu komutlari da ayni yoldan gelir. Bu sayede test, oyun saatini
    /// elle ilerleterek yapabilir.
    /// </summary>
    public sealed class CargoSystem
    {
        private readonly CargoManifest _manifest = new CargoManifest();
        private readonly ParcelGenerator _generator;
        private readonly ParcelType[] _types;
        private readonly GameConfig _config;

        public CargoManifest Manifest => _manifest;

        /// <summary>Uretilen toplam kargo sayisi (istatistik / HUD).</summary>
        public int GeneratedToday { get; private set; }

        public CargoSystem(GameConfig config, ParcelType[] types, VehicleDefinition vehicle)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _types = types ?? throw new ArgumentNullException(nameof(types));

            if (types.Length == 0)
                throw new ArgumentException("En az bir ParcelType gerekli.", nameof(types));

            _generator = new ParcelGenerator(types, config.destinationCount, config.baseDeliverySeconds);

            ApplyVehicleCapacity(vehicle);
        }

        /// <summary>Arac degistiginde kapasiteyi guncelle (Aşama 3'te tek arac).</summary>
        public void ApplyVehicleCapacity(VehicleDefinition vehicle)
        {
            if (vehicle == null)
            {
                _manifest.SetCapacity(new CargoCapacity(0f, 0f));
                return;
            }

            _manifest.SetCapacity(new CargoCapacity(
                vehicle.cargoCapacityKg,
                vehicle.cargoCapacityLiters));
        }

        /// <summary>
        /// Yeni gun: kargolari uretir ve depoyu doldurur.
        /// Tohum gun numarasindan turetilir -> ayni gun ayni kargolar.
        /// </summary>
        public void BeginDay(int day)
        {
            _generator.ResetForNewDay(day * 7919);
            List<Parcel> parcels = _generator.Generate(_config.parcelsPerDay);

            _manifest.BeginDay(parcels);
            GeneratedToday = parcels.Count;

            GameEvents.RaiseParcelsGenerated(parcels.Count);
            GameEvents.RaiseCargoChanged();
        }

        /// <summary>
        /// Kargoyu araca yuklemeyi dener. Kapasiteye sigmazsa false doner.
        ///
        /// DIKKAT: bu eski yol sistem saatini 0f gecirir, yani teslim geri
        /// sayimi GUN BASINDAN isler. Oyun icinde kullanilacaksa
        /// <see cref="TryLoadParcel(int, float)"/> overload'u tercih
        /// edilmelidir. Imza korunur; disaridan (UI/Player) cagrilan yerler
        /// derlenmeye devam etsin diye bu surum duruyor.
        /// </summary>
        public bool TryLoadParcel(int parcelId)
        {
            return TryLoadParcel(parcelId, 0f);
        }

        /// <summary>
        /// Kargoyu araca yukler ve teslim suresini <paramref name="systemTime"/>
        /// anindan baslatir.
        ///
        /// DUZELTME (Hata 1): Once tek yol vardi ve o yol manifest'e
        /// systemTime GECIRMIYORDU; her zaman varsayilan 0f gidiyordu.
        /// Parcel.TimeLoaded = 0 oldugu icin
        /// Parcel.RemainingSeconds() = Deadline - (simdi - 0) hesabini
        /// yapiyor, yani gunun gec yarisinda yuklenen her kargo dogustan
        /// "gec" sayiliyordu. Oyuncu kargoyi zamaninda teslim etse bile
        /// odemesi ve XP'si kisiya gore azaliyordu; HUD'daki sure de
        /// yanlis gorunuyordu. Artik cagiran (UI/Player) gercek simulasyon
        /// saatini gecebilir.
        ///
        /// OLAYLAR: basarili yuklemede ParcelLoaded ve CargoChanged yayilir
        /// (eski davranisla ayni).
        /// </summary>
        /// <param name="parcelId">Yuklenecek kargonun kimligi.</param>
        /// <param name="systemTime">
        /// Similasyon saati (saniye, gun basindan itibaren). Teslim geri
        /// sayiminin baslayacagi an.
        /// </param>
        /// <returns>Basariliysa true, yoksa false.</returns>
        public bool TryLoadParcel(int parcelId, float systemTime)
        {
            Parcel parcel = _manifest.FindById(parcelId);
            if (parcel == null) return false;

            if (!_manifest.TryLoad(parcel, systemTime))
                return false;

            GameEvents.RaiseParcelLoaded(parcel);
            GameEvents.RaiseCargoChanged();
            return true;
        }

        /// <summary>
        /// Arsaci bosaltir ve kargoyu DEPOYA GERI DONDURUR (Q tusu).
        ///
        /// DUZELTME (Hata 2): Once UnloadAll() yalnizca araci bosaltiyor,
        /// kargoyu depoya yazmiyordu; boylece oyuncu Q'ya basinca kargo
        /// kalici olarak listeden kayboluyor, tekrar yuklenemiyor ve o
        /// kargodan beklenen odeme hic gelmiyordu. Artik kargo depoda
        /// bekliyor ve oyuncu istedigi zaman tekrar yukleyebiliyor.
        /// </summary>
        public void UnloadVehicle()
        {
            _manifest.UnloadAll();
            GameEvents.RaiseCargoChanged();
        }

        /// <summary>
        /// Arsaci bosaltir ve kargoyu KALICI olarak yok eder.
        ///
        /// DUZELTME (Hata 2): Gun sonu basarisiz sayimi icin ayri yol.
        /// UnloadVehicle() ile tamamen zit davranis: kargo depoya donmez.
        /// Boylece "Q ile bosalt" ile "gun sonunda basarisiz say" ayni
        /// metodu kullanmaz.
        ///
        /// DIKKAT: DeliverySystem.FailRemainingParcels() su an
        /// UnloadVehicle() cagiriyor; oyunun gun sonu davranisinin dogru
        /// olmasi icin o taraf bu metodu kullanmali.
        /// </summary>
        public void DiscardVehicle()
        {
            _manifest.DiscardLoaded();
            GameEvents.RaiseCargoChanged();
        }
    }
}
