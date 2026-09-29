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
    /// ASAMA 11: sonucu artik kendisi URETMEZ, <see cref="DeliveryRisk"/>
    /// katmanina sorar (tur orani, kirilabilirlik, nadir kargo). Risk karari
    /// yine uc sonucu kullanir; ekonomi tarafi HICBIR degismedi.
    ///
    /// Update() YOK. Zamanlama disaridan gelir: CompositionRoot her karede
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

        /// <summary>
        /// ASAMA 11: risk tohumu (gun + kargo kimligi) buradan turer.
        /// CompositionRoot bu numarayi vermedigi icin kendimiz tutuyoruz.
        /// </summary>
        private int _day = 1;

        // DUZELTME (Hata 4): aracin konumu ve "bu konum gercek mi?" bilgisi
        // ayri tutulur. Vector3.zero hem "depodayim" hem "arac bilinmiyor"
        // anlamina gelir; ayirt edemeden rota kurmak yalnizca (0,0,0)da
        // dogru calisir, testlerde ve harita hizasinda yanlis sonuc verir.
        private Vector3 _vehiclePosition;
        private bool _hasVehiclePosition;

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

        /// <summary>
        /// Kargo sistemi. Kapidan "paketlenmis mi?" kontrolu yapmak icin
        /// gerekiyor: AddressDoor teslimattan once kargonun paketlenmis
        /// oldugunu dogruluyor ve bunu kargo sisteminden soruyor.
        /// </summary>
        public CargoSystem Cargo => _cargo;

        /// <summary>Rotadaki duraklar (sirali). UI bunu cizer.</summary>
        public IReadOnlyList<DeliveryStop> Route => _route;

        /// <summary>Aracin su an varis yaptigi durak (yoksa null).</summary>
        public DeliveryStop CurrentStop => _currentStop;

        /// <summary>Rotanin kalan toplam yolu (metre).</summary>
        public float RemainingDistance { get; private set; }

        /// <summary>Su an yuklenmis kargo sayisi.</summary>
        public int LoadedParcelCount => _cargo?.Manifest?.LoadedCount ?? 0;

        /// <summary>
        /// ASAMA 11: teslim sonucunu uretirken kullanilan gun numarasi.
        /// Ayni kargo, ayni gun, ayni karari verir.
        /// </summary>
        public int CurrentDay => _day;

        /// <summary>
        /// ASAMA 11: son teslimdeki kirilabilir hasar (0..100 yuzde).
        /// Kirilabilir olmayan kargo ve basarili teslimde hep 0'dir.
        ///
        /// NEDEN AYRI BIR OZELLIK: hasar yeni bir odeme ya da olay
        /// bicimi degil, mevcut <see cref="DeliveryOutcome"/> uclusune
        /// baglanir (hasarli teslim = BASARISIZ). Ekonomi tarafi ve
        /// GameEvents aynen kalir; HUD/istatistik ise bu sayaci okur.
        /// </summary>
        public int LastFragileDamage { get; private set; }

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
            // gidiyorsa 5 kez durmamak dogru davranistir.
            //
            // ASAMA 33: rota yalnizca ARAÇTAKI kargolardan kuruluyordu.
            // Kutu kuryenin eline gectiginde listeden cikinca durak
            // kayboluyordu ("Durak yok") ve teslimat yapilamiyordu.
            //
            // Dogru kaynak: ARAÇTA + ELDE. Kutu kuryenin elindeyken de
            // teslim edilecek bir adrese aittir; sadece fiziksel olarak
            // araçta degildir.
            IReadOnlyList<Parcel> loaded = _cargo.Manifest.Loaded;
            for (int i = 0; i < loaded.Count; i++)
                AddParcelToRoute(loaded[i]);

            IReadOnlyList<Parcel> carried = _cargo.Manifest.Carried;
            for (int i = 0; i < carried.Count; i++)
                AddParcelToRoute(carried[i]);

            _lastLoadedCount = RoutedParcelCount();
            RebuildRoute();
        }

        /// <summary>
        /// ROtaya dahil edilen kutu sayisi: aracTAKI + kuryeNDEKI.
        ///
        /// Rota bu toplama bakarak yeniden kurulur. Yalnizca aracdekiler
        /// sayilsaydi, kutuyu eline alan oyuncunun duragi kaybolurdu.
        /// </summary>
        private int RoutedParcelCount()
        {
            if (_cargo?.Manifest == null) return 0;
            return _cargo.Manifest.LoadedCount + _cargo.Manifest.Carried.Count;
        }

        /// <summary>Bir kutuyu rotaya ekler (yoksa durak acar).</summary>
        private void AddParcelToRoute(Parcel parcel)
        {
            if (parcel == null) return;

            DeliveryStop existing = FindStop(parcel.DestinationId);
            if (existing != null)
            {
                existing.AddParcel(parcel);
                return;
            }

            var point = _layout.Find(parcel.DestinationId);
            if (point == null)
            {
                // Haritada olmayan bir hedef: sessizce gecmek yerine bir
                // kez uyari ver, cunku oyuncu bu kargoyu asla teslim
                // edemez ve nedenini anlamaz.
                Debug.LogWarning("[DeliverySystem] Hedef adres haritada yok: " +
                                 parcel.DestinationId + ". Kargo teslim edilemeyecek.");
                return;
            }

            var stop = new DeliveryStop(parcel.DestinationId,
                                        point.Value.position,
                                        point.Value.displayName);
            stop.AddParcel(parcel);
            _stops.Add(stop);
        }


        /// <summary>Gun sifirlama (gun sonu / yeni gun).</summary>
        public void BeginDay()
        {
            // ASAMA 11: tohum "gun + kargo kimligi"den turetildigi icin
            // sistemin gun sayacini bilmesi gerekiyor. CompositionRoot bu
            // metodu kurulumda da ("gun 1") cagirir, o yuzden gercekten bir
            // gun gecmeden sayaci artirmiyoruz: simulasyon suresi dolmadikca
            // ayni gun sayilir. Boylece gun 1'in tohumu kaymaz.
            if (_elapsed > 0f) _day++;

            _elapsed = 0f;
            _stops.Clear();
            _route.Clear();
            _routeCursor = 0;
            _currentStop = null;
            RemainingDistance = 0f;
            _dirty = true;
            _lastLoadedCount = -1;
            LastFragileDamage = 0;
        }

        private void RebuildRouteIfNeeded()
        {
            // Rotayi YUKLENEN KARGO SAYISINA bakarak yeniler. Event'e
            // guvenmek yanlis olurdu: kargo baska bir yoldan yuklenirse
            // (test, ileride gelen arac degisimi) rota bayat kalirdi.
            int current = RoutedParcelCount();
            if (!_dirty && current == _lastLoadedCount) return;

            _dirty = false;
            _lastLoadedCount = current;
            RebuildStops();
        }

        private void RebuildRoute()
        {
            Vector3 start = RouteStartPosition();
            _route.Clear();
            _route.AddRange(RoutePlanner.OrderByNearestNeighbour(start, _stops));
            _routeCursor = 0;
            _currentStop = null;
            GameEvents.RaiseRouteChanged();
        }

        /// <summary>
        /// Rotanin baslangic noktasi.
        ///
        /// DUZELTME (Hata 4): burada daima _layout.depotPosition kullaniliyordu.
        /// Rota her teslimden sonra yeniden kuruldugu icin (blokaj durumu)
        /// arac 3. adresteyken teslim yapildiginda siralama BASTAN, yani
        /// depoya gore hesaplaniyordu. Oyuncu 4. adrese gitmek isterken
        /// "1. adrese don" emri aliyor, HUD'daki "Sonraki" ve minimap cizgisi
        /// zipliyor, kalan mesafe anlami kayboluyordu.
        ///
        /// Dogru baslangic aracin O ANKI konumudur: zaten oradayiz, dolayisiyla
        /// en yakin durak listede ilk olmali. Arac hic bilinmiyorsa (ilk Tick'ten
        /// once, test, sunucu) depoya dusulur - onceki davranis.
        /// </summary>
        private Vector3 RouteStartPosition()
        {
            if (_hasVehiclePosition) return _vehiclePosition;
            return _layout != null ? _layout.depotPosition : Vector3.zero;
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
        ///
        /// DUZELTME (Hata 4): setter ayrica "konum biliniyor" bayragini
        /// kaldirir. Boylece rota, arac taniniyorsa O ANKII konumdan,
        /// taninmiyorsa depodan baslatilir. Imza (get/set) ayni kaldi.
        /// </summary>
        public Vector3 VehiclePosition
        {
            get => _vehiclePosition;
            set
            {
                _vehiclePosition = value;
                _hasVehiclePosition = true;
            }
        }

        /// <summary>
        /// Su anki duraktaki bir kargoyu teslim eder.
        /// </summary>
        /// <returns>Sonuc; null ise kargo teslim edilemedi.</returns>
        public DeliveryOutcome? TryDeliver(int parcelId)
        {
            // Her denemede sifirlanir: teslim olmayan bir deneme, bir
            // onceki teslimin hasarini tasmamali.
            LastFragileDamage = 0;

            DeliveryStop stop = _currentStop;
            if (stop == null) return null;

            Parcel parcel = FindParcel(parcelId);
            if (parcel == null) return null;

            // ASAMA 11: karar burada degil, risk katmaninda. Onceki
            // kod "sure doldu mu?" diye sorup her zaman Success ya da Late
            // donuyordu; yani ParcelType.baseSuccessRate, fragile ve
            // isRare alanlari hic okunmuyordu ve oyun %100 basariliydi.
            DeliveryOutcome outcome = RollDeliveryOutcome(parcel);

            // Duragin kalan kargo sayaci once duser (rotada ilerleme icin).
            // Kargonun kendi durumuna dokunmaz.
            stop.MarkParcelDelivered(parcel);

            // DUZELTME (Hata 5): olay, kargonun durumu DEGISMEDEN once
            // yayilir. Once _cargo.Manifest.MarkDelivered(parcel) cagriliyordu,
            // o da parcel.State'i Delivered yapip listeden cikaruyordu;
            // EconomyService -> PayoutCalculator.RemainingRatio() parcayi
            // okudugunda IsLoaded zaten false olmus ve "1f dondur" dalina
            // dusuyordu. Sonuc: her teslim %100 kalan sure gibi gorunuyor,
            // EconomyConfig.earlyBonusThreshold ayari HIC kullanilmiyor ve
            // zamaninda teslim eden kurye erken teslim bahsini alamiyordu.
            // Simdi dinleyici kargoyu hala arac uzerinde, gercek kalan
            // suresiyle gorur.
            //
            // Bir dinleyici istisna atarsa kargonun durumu yine de
            // tutarli kalsin diye finally kullanilir.
            try
            {
                GameEvents.RaiseParcelDelivered(parcel, outcome);
            }
            finally
            {
                // Kargoyu listeden cikar: teslim edilen kargo arac uzerinde
                // kalmamali. Aksi halde yeni rota kuruldugunda tekrar gidilirdi.
                _cargo.Manifest.MarkDelivered(parcel);
            }

            // Bu duraktaki tum kargolar bittiyse rotada ilerle.
            if (stop.IsCompleted)
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
        /// ASAMA 11: tek bir teslimin kararini risk katmanindan alir.
        ///
        /// Tohum: gun + kargo kimligi. Ayni kargo, ayni gun, ayni karar;
        /// deneme sayisina baglanmaz, cunku bir kargo yalnizca bir kez
        /// teslim edilebilir.
        ///
        /// DEGER KAYBI NASIL AKTARILIYOR: kirilabilir kargo hasar gorurse
        /// teslim BASARISIZ sayilir (odeme 0 + ceza). Boylece yeni bir odeme
        /// bicimi, yeni bir olay ya da ekonomi dosyasinda degisiklik
        /// gerekmez: mevcut ucunlu (Success/Late/Failed) aynen yeterlidir.
        /// </summary>
        private DeliveryOutcome RollDeliveryOutcome(Parcel parcel)
        {
            float ratio = DeliveryRisk.RemainingRatio(parcel, _elapsed);

            DeliveryOutcome outcome = DeliveryRisk.RollOutcome(
                parcel, ratio, DeliveryRisk.OutcomeSeed(_day, parcel.Id));

            // Hasar ayri bir tohumdan atilir: ayni kargonun "sonucu" ve
            // "hasari" birbirine bagli olmaz.
            int damage = DeliveryRisk.FragileDamage(
                parcel, ratio, DeliveryRisk.DamageSeed(_day, parcel.Id));

            LastFragileDamage = damage;
            if (damage > 0) return DeliveryOutcome.Failed;

            return outcome;
        }

        /// <summary>
        /// Gun sonunda teslim edilmemis kargolari basarisiz sayar.
        /// (Teslimat teslimi de kargoyu listeden cikarir.)
        ///
        /// DUZELTME (Hata 3): burada _cargo.UnloadVehicle() cagriliyordu; o
        /// yol kargoyu DEPOYA GERI GONDURUR. Gun sonu basarisiz sayilan kargo
        /// oyuncuya geri donmustu: depoda tekrar gorunuyor, ertesi gun yeniden
        /// yuklenip ayni kargo ikinci kez basarisiz sayilabiliyor ve gun sonu
        /// ozetindeki "basarisiz" sayisi oyuncunun gercek kaybini yansitmiyordu.
        /// Dogru yol DiscardVehicle(): arac bosalir, kargo KALICI olarak yok olur
        /// (Q ile bosaltmanin tam zidi).
        /// </summary>
        public int FailRemainingParcels()
        {
            int failed = 0;
            IReadOnlyList<Parcel> loaded = _cargo?.Manifest?.Loaded;
            if (loaded == null) return 0;

            // Olay her kargo icin yayildigi ve dinleyici bu sirada araci
            // degistirebilir; canli listeyi indekslemek gun sonunda
            // IndexOutOfRange riski demektir. Tek seferlik kopya aliyoruz
            // (gunde bir kez, ihmal edilebilir maliyet).
            var snapshot = new List<Parcel>(loaded);
            for (int i = 0; i < snapshot.Count; i++)
            {
                Parcel parcel = snapshot[i];
                if (parcel == null) continue;

                // Kargo henuz aracda: dinleyici (ekonomi) gercek kalan
                // sureyi okuyabilir. Basarisiz odemede bahsis zaten olusmaz.
                GameEvents.RaiseParcelDelivered(parcel, DeliveryOutcome.Failed);
                failed++;
            }

            _cargo.DiscardVehicle();
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
