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
        /// </summary>
        public bool TryLoadParcel(int parcelId)
        {
            Parcel parcel = _manifest.FindById(parcelId);
            if (parcel == null) return false;

            if (!_manifest.TryLoad(parcel))
                return false;

            GameEvents.RaiseParcelLoaded(parcel);
            GameEvents.RaiseCargoChanged();
            return true;
        }

        /// <summary>Araci bosaltir (gun sonu).</summary>
        public void UnloadVehicle()
        {
            _manifest.UnloadAll();
            GameEvents.RaiseCargoChanged();
        }
    }
}
