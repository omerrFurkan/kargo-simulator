using System.Collections.Generic;
using KargoSim.Cargo;
using KargoSim.Core;
using UnityEngine;

namespace KargoSim.Delivery
{
    /// <summary>
    /// Teslimat sistemi: yuklenen kargolardan durak noktalari olusturur,
    /// rotayi siralar, arac varisini algilar ve teslimi yonetir.
    ///
    /// Para ve XP HESAPLAMAZ (Aşama 5). Sadece sonucu (Success/Late/Failed)
    /// ve neden oldugunu bilir; odemeyi ekonomi sistemi yapar. Iki katman
    /// ayni isi yapmaz.
    ///
    /// Update() YOK. Zamanlama dışaridan gelir: CompositionRoot her karede
    /// <see cref="Tick"/> cagirir. Oyunun tek Update cagrisi kurali bozulmaz.
    /// </summary>
    public sealed class DeliverySystem
    {
        private const float DefaultArrivalRadius = 7f;

        private readonly MapLayout _layout;
        private readonly CargoSystem _cargo;
        private readonly float _arrivalRadius;
        private readonly List<DeliveryStop> _stops = new List<DeliveryStop>();
        private readonly List<DeliveryStop> _route = new List<DeliveryStop>();

        private float _elapsed;
        private int _routeCursor;
        private DeliveryStop _currentStop;

        /// <summary>Rota elle gecersersilestirilir (teslim sonrasi vs.).</summary>
        private bool _dirty = true;

        /// <summary>
        /// Rotanin hangi yukleme sayisina gore kuruldugu. Teslimat durak
        /// basina bakarak degil, BURAYA bakarak karar verir.
        /// </summary>
        private int _lastLoadedCount = -1;

        public DeliverySystem(MapLayout layout, CargoSystem cargo, float arrivalRadius = DefaultArrivalRadius)
        {
            _layout = layout;
            _cargo = cargo;
            _arrivalRadius = arrivalRadius > 0f ? arrivalRadius : DefaultArrivalRadius;
        }

        /// <summary>Gun basindan bu yana gecen simulasyon suresi (saniye).</summary>
        public float Elapsed => _elapsed;

        /// <summary>Rotadaki duraklar (sirali). UI bunu cizer.</summary>
        public IReadOnlyList<DeliveryStop> Route => _route;

        /// <summary>Aracin su an varis yaptigi durak (yoksa null).</summary>
        public DeliveryStop CurrentStop => _currentStop;

        /// <summary>Rotanin kalan toplam yolu (metre).</summary>
        public float RemainingDistance { get; private set; }

        /// <summary>Su an yuklenmis kargo sayisi.</summary>
        public int LoadedParcelCount => _cargo?.Manifest?.LoadedCount ?? 0;

        // ------------------------------------------------------------------
        // Zamanlama
        // ------------------------------------------------------------------

        /// <summary>
        /// Her kare cagrilir. Oyun duraklatildiginda <paramref name="paused"/>
        /// true gelir ve simülasyon ilerlemez.
        /// </summary>
        public void Tick(float deltaTime, bool paused)
        {
            if (paused || deltaTime <= 0f) return;

            _elapsed += deltaTime;
            RebuildRouteIfNeeded();
            UpdateArrival();
            UpdateRemainingDistance();
        }

        // ------------------------------------------------------------------
        // Durak ve rota yonetimi
        // ------------------------------------------------------------------

        private void RebuildStops()
        {
            _stops.Clear();
            if (_layout == null || _cargo == null) return;

            // Yuklenen kargolardan duraklari kur. Ayni adrese giden
            // kargolar TEK durakta toplanir - 5 kargo ayni apartmana
            // gidiyorsa 5 kere durmamak dogru davranistir.
            IReadOnlyList<Parcel> loaded = _cargo.Manifest.Loaded;
            for (int i = 0; i < loaded.Count; i++)
            {
                Parcel parcel = loaded[i];
                if (parcel == null) continue;

                DeliveryStop existing = FindStop(parcel.DestinationId);
                if (existing != null)
                {
                    existing.AddParcel(parcel);
                    continue;
                }

                var point = _layout.Find(parcel.DestinationId);
                if (point == null)
                {
                    // Haritada olmayan bir hedef: sessizce gecmek yerine
                    // bir kez uyari ver, cunku oyuncu bu kargoyu asla
                    // teslim edemez ve nedenini anlamaz.
                    Debug.LogWarning("[DeliverySystem] Hedef adres haritada yok: " +
                                     parcel.DestinationId + ". Kargo teslim edilemeyecek.");
                    continue;
                }

                var stop = new DeliveryStop(parcel.DestinationId,
                                            point.Value.position,
                                            point.Value.displayName);
                stop.AddParcel(parcel);
                _stops.Add(stop);
            }

            _lastLoadedCount = _cargo?.Manifest?.LoadedCount ?? 0;
            RebuildRoute();
        }

        /// <summary>Gun sifirlama (gun sonu / yeni gun).</summary>
        public void BeginDay()
        {
            _elapsed = 0f;
            _stops.Clear();
            _route.Clear();
            _routeCursor = 0;
            _currentStop = null;
            RemainingDistance = 0f;
            _dirty = true;
            _lastLoadedCount = -1;
        }

        private void RebuildRouteIfNeeded()
        {
            // Rotayi YUKLENEN KARGO SAYISINA bakarak yeniler. Event'e
            // guvenmek yanlis olurdu: kargo baska bir yoldan yuklenirse
            // (test, ileride gelen arac degisimi) rota bayat kalirdi.
            int current = _cargo?.Manifest?.LoadedCount ?? 0;
            if (!_dirty && current == _lastLoadedCount) return;

            _dirty = false;
            _lastLoadedCount = current;
            RebuildStops();
        }

        private void RebuildRoute()
        {
            Vector3 start = _layout != null ? _layout.depotPosition : Vector3.zero;
            _route.Clear();
            _route.AddRange(RoutePlanner.OrderByNearestNeighbour(start, _stops));
            _routeCursor = 0;
            _currentStop = null;
            GameEvents.RaiseRouteChanged();
        }

        private DeliveryStop FindStop(int destinationId)
        {
            for (int i = 0; i < _stops.Count; i++)
                if (_stops[i].DestinationId == destinationId) return _stops[i];

            return null;
        }

        // ------------------------------------------------------------------
        // Varis ve teslim
        // ------------------------------------------------------------------

        private void UpdateArrival()
        {
            if (_currentStop != null) return;   // zaten bir duraktayiz
            if (_route.Count == 0) return;
            if (_routeCursor >= _route.Count) return;

            DeliveryStop next = _route[_routeCursor];
            if (next == null) { _routeCursor++; return; }

            if (Vector3.Distance(next.Position, VehiclePosition) <= _arrivalRadius)
            {
                _currentStop = next;
                GameEvents.RaiseDeliveryStopArrived(next);
            }
        }

        private void UpdateRemainingDistance()
        {
            if (_route.Count == 0) { RemainingDistance = 0f; return; }

            float total = RoutePlanner.CalculateRouteLength(VehiclePosition, _route);
            RemainingDistance = total;
        }

        /// <summary>
        /// Aracin dunyada bulundugu konum. CompositionRoot her karede set eder;
        /// teslimat sistemi araci tanimaz, sadece konumu okur.
        /// </summary>
        public Vector3 VehiclePosition { get; set; }

        /// <summary>
        /// Su anki duraktaki bir kargoyu teslim eder.
        /// </summary>
        /// <returns>Sonuc; null ise kargo teslim edilemedi.</returns>
        public DeliveryOutcome? TryDeliver(int parcelId)
        {
            if (_currentStop == null) return null;

            Parcel parcel = FindParcel(parcelId);
            if (parcel == null) return null;

            DeliveryOutcome outcome = parcel.IsOverdue(_elapsed)
                ? DeliveryOutcome.Late
                : DeliveryOutcome.Success;

            // Kargoyu once listeden cikar: teslim edilen kargo arac uzerinde
            // kalmamali. Aksi halde yeni rota kuruldugunda tekrar gidilirdi.
            _cargo.Manifest.MarkDelivered(parcel);

            _currentStop.MarkParcelDelivered(parcel);
            GameEvents.RaiseParcelDelivered(parcel, outcome);

            // Bu duraktaki tum kargolar bittiyse rotada ilerle.
            if (_currentStop.IsCompleted)
            {
                _routeCursor++;
                _currentStop = null;
            }

            // Teslim edilen kargo listeden cikti ama sayi degisimi (baska
            // bir kargo yuklenmesi) rotayi bayatlatabilir: elle isaretle.
            _dirty = true;
            return outcome;
        }

        /// <summary>
        /// Gun sonunda teslim edilmemis kargolari basarisiz sayar.
        /// (Teslimat teslimi de kargoyu listeden cikarir.)
        /// </summary>
        public int FailRemainingParcels()
        {
            int failed = 0;
            IReadOnlyList<Parcel> loaded = _cargo?.Manifest?.Loaded;
            if (loaded == null) return 0;

            for (int i = 0; i < loaded.Count; i++)
            {
                Parcel parcel = loaded[i];
                if (parcel == null) continue;

                GameEvents.RaiseParcelDelivered(parcel, DeliveryOutcome.Failed);
                failed++;
            }

            _cargo.UnloadVehicle();
            BeginDay();
            return failed;
        }

        private Parcel FindParcel(int parcelId)
        {
            IReadOnlyList<Parcel> parcels = _currentStop?.Parcels;
            if (parcels == null) return null;

            for (int i = 0; i < parcels.Count; i++)
                if (parcels[i] != null && parcels[i].Id == parcelId) return parcels[i];

            return null;
        }

        /// <summary>Yuklenen kargonun kalan suresi (HUD icin).</summary>
        public float GetRemainingSeconds(Parcel parcel)
        {
            if (parcel == null) return 0f;
            return parcel.RemainingSeconds(_elapsed);
        }
    }
}
