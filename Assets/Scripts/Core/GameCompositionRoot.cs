using KargoSim.Cargo;
using UnityEngine;

namespace KargoSim.Core
{
    /// <summary>
    /// Composition Root: projenin TEK referans noktasi.
    ///
    /// Tum servisleri (Clock, Cargo, Economy, Delivery...) burada olusturur ve
    /// birbirine baglar. Sistemler birbirini tanimaz; hepsi buradan enjekte edilir.
    /// Yeni bir sistem eklemek icin: sinifi yaz, burada `new`le, sonra sahneye ekle.
    ///
    /// Tek Update cagrisi buradadir. Baska hicbir sistem Update kullanmaz -
    /// boylece Unity'nin her frame'de cagiracagi method sayisi sabit kalir.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameCompositionRoot : MonoBehaviour
    {
        [Header("Veri")]
        [SerializeField] private GameConfig config;

        [Header("Icerik")]
        [Tooltip("Gunluk kargo turlari. Bos birakilirsa kargo sistemi " +
                 "hicbir sey uretemez.")]
        [SerializeField] private ParcelType[] parcelTypes = new ParcelType[0];

        [Header("Arac")]
        [Tooltip("Kapasitesi kargo sistemine aktarilacak arac. Bos birakilirsa " +
                 "hicbir kargo araca sigmaz.")]
        [SerializeField] private Vehicle.VehicleDefinition playerVehicle;

        [Header("Harita")]
        [Tooltip("Teslimat noktalari. Bos birakilirsa hicbir adrese " +
                 "gidilemez.")]
        [SerializeField] private Delivery.MapLayout mapLayout;

        [Header("Teslimat")]
        [Tooltip("Arac bu yaricap icindeyse adrese varmiş sayilir (metre).")]
        [SerializeField] private float arrivalRadius = 7f;

        [Header("Ekonomi")]
        [Tooltip("Para ve XP ayarlari. Bos birakilirsa odeme yapilmaz.")]
        [SerializeField] private Economy.EconomyConfig economyConfig;

        [Header("Arayuz")]
        [Tooltip("Arayuz yoneticisi. Bos birakilirsa oyun HUD'suz oynanir.")]
        [SerializeField] private UI.UIManager uiManager;

        [Header("Kurye")]
        [Tooltip("Oyunun kontrolundeki karakter. Ayaktayken WASD + fare, " +
                 "arac icindeyken arac girdileri kullanilir.")]
        [SerializeField] private Player.CourierController courier;

        private GameClock _clock;
        private CargoSystem _cargo;
        private Delivery.DeliverySystem _delivery;
        private Economy.EconomyService _economy;
        private Player.PlayerProgress _progress;
        private Transform _playerVehicleTransform;

        public GameConfig Config => config;
        public GameClock Clock => _clock;
        public CargoSystem Cargo => _cargo;
        public Delivery.DeliverySystem Delivery => _delivery;
        public Player.PlayerProgress Progress => _progress;
        public Economy.EconomyService Economy => _economy;
        public Vehicle.VehicleDefinition PlayerVehicle => playerVehicle;
        public Delivery.MapLayout MapLayout => mapLayout;

        private void Awake()
        {
            if (config == null)
            {
                Debug.LogError(
                    "[GameCompositionRoot] GameConfig atanmadı. Bu component'i sahnede " +
                    "bulamazsak oyun ayarlari bilinmez. Assets/ScriptableObjects altindaki " +
                    "GameConfig asset'ini sürükleyip buraya bırak.", this);
                enabled = false;
                return;
            }

            _clock = new GameClock(config);
            _playerVehicleTransform = FindPlayerVehicle();

            CreateCargoSystem();
            CreateDeliverySystem();
            CreateCourier();
            CreateEconomy();

            // Gun dongusunu DIŞARIDAN dinlemek: saat degisimi once
            // tetiklenir, biz de ondan sonra kargo/teslim tazeliyoruz.
            // (Saat once bildirirse, gun 2 kargolari gun 1 kargosu
            //  temizlenmeden uretilirdi.)
            GameEvents.DayStarted += OnDayStarted;
            GameEvents.DayEnded += OnDayEnded;
        }

        /// <summary>
        /// Oyuncunun aracini bulur. Sarma (wrapper) bir MonoBehaviour olmadigi
        /// icin GetComponent ile degil, sahnedeki VehicleController aranir.
        /// </summary>
        private Transform FindPlayerVehicle()
        {
            var vehicle = FindAnyObjectByType<Vehicle.ArcadeVehicleController>();
            return vehicle != null ? vehicle.transform : null;
        }

        /// <summary>
        /// Kargo sistemini kurar. Eksik icerik (parca turu yok) OYUNU
        /// DUSURMEZ - sadece kargo uretilmez ve konsola bir kez uyari
        /// yazilir. Aksi halde yeni bir asama eklenirken sahne hazir
        /// degilse oyun hic acilmazdi.
        /// </summary>
        private void CreateCargoSystem()
        {
            if (parcelTypes == null || parcelTypes.Length == 0)
            {
                Debug.LogWarning(
                    "[GameCompositionRoot] ParcelType atanmadi; gunluk kargo " +
                    "uretilmeyecek. Menu: Kargo/3. Icerik Assetlerini Kur", this);
                return;
            }

            _cargo = new CargoSystem(config, parcelTypes, playerVehicle);
        }

        private void CreateDeliverySystem()
        {
            if (_cargo == null)
            {
                Debug.LogWarning(
                    "[GameCompositionRoot] Kargo sistemi yok, teslimat sistemi " +
                    "olusturulmadi.", this);
                return;
            }

            if (mapLayout == null)
            {
                Debug.LogWarning(
                    "[GameCompositionRoot] MapLayout atanmadi; adreslere gidilemeyecek. " +
                    "Menu: Kargo/4. Harita ve Teslimat Noktalarini Kur", this);
                return;
            }

            _delivery = new Delivery.DeliverySystem(mapLayout, _cargo, arrivalRadius);
            _delivery.BeginDay();
        }

        private void CreateEconomy()
        {
            if (economyConfig == null)
            {
                Debug.LogWarning(
                    "[GameCompositionRoot] EconomyConfig atanmadi; odeme yapilmayacak. " +
                    "Menu: Kargo/5. Ilerleme Ayarlarini Kur", this);
                return;
            }

            _progress = new Player.PlayerProgress(economyConfig);

            if (_delivery == null)
            {
                Debug.LogWarning("[GameCompositionRoot] Teslimat sistemi yok, ekonomi kurulamadi.", this);
                return;
            }

            _economy = new Economy.EconomyService(economyConfig, _progress, _delivery, 1);

            BindUI();
        }

        /// <summary>
        /// Arayuzu sistemlere baglar. UI yoksa oyun YINE calisir - sadece
        /// HUD olmaz. Bu, arayuzu olmayan bir build'i (sunucu, test)
        /// calistirabilmek icin onemli.
        /// </summary>
        private void BindUI()
        {
            if (uiManager == null) return;

            if (_progress == null || _clock == null)
            {
                Debug.LogWarning("[GameCompositionRoot] Arayuz baglanamadi: " +
                                 "ekonomi veya saat eksik. Oyun HUD'suz calisacak.", this);
                return;
            }

            uiManager.Bind(_cargo, _delivery, _progress, _clock, mapLayout, _playerVehicleTransform);
            uiManager.SetCourier(courier);
        }

        /// <summary>
        /// Kurye karakterini ve etkilesim noktalarini hazirlar.
        ///
        /// Sistemler kurulduktan SONRA cagrilir: hem hepsi kargo ve
        /// teslimat sistemlerine ihtiyac duyar.
        ///
        /// Karakter yoksa oyun yine kurulur ama yalnizca aracla
        /// surulur; kargo arayuzden aninda yuklenir.
        /// </summary>
        private void CreateCourier()
        {
            if (courier == null)
            {
                Debug.LogWarning(
                    "[GameCompositionRoot] Courier atanmadi; yalnizca arac kullanilabilir. " +
                    "Menu: Kargo/8. Dukkani Kur", this);
                return;
            }

            if (_cargo == null || _delivery == null)
            {
                Debug.LogWarning("[GameCompositionRoot] Kargo/teslimat sistemi yok, kurye kurulmadi.", this);
                return;
            }

            var vehicleController = _playerVehicleTransform != null
                ? _playerVehicleTransform.GetComponent<Vehicle.ArcadeVehicleController>()
                : null;

            Camera vehicleCamera = Camera.main;

            var fpsCamera = courier.GetComponentInChildren<Camera>(true);

            courier.Configure(null, fpsCamera, vehicleCamera, null, vehicleController);
            courier.EnterVehicle();

            // Etkilesim noktalarini bagla
            var pile = FindFirstObjectByType<Player.ShopParcelPile>();
            if (pile != null)
            {
                // Rafin kutu gorunumu ada gore bulunur. Once indeksle
                // (GetChild(1)) araniyordu; cocuk sirasi degisince
                // raf bos kaliyordu.
                Transform shelf = pile.transform.parent;
                Transform boxes = shelf != null ? shelf.Find("Boxes") : null;
                pile.Configure(_cargo, boxes);
            }

            var vanPoint = FindFirstObjectByType<Player.VanCargoPoint>();
            if (vanPoint != null) vanPoint.Configure(_cargo, _delivery, courier, vehicleController, vehicleCamera);

            var door = FindFirstObjectByType<Player.AddressDoor>();
            if (door != null) door.Configure(_delivery, courier);
        }

        /// <summary>
        /// Statik event kuyrugunu temizler.
        ///
        /// Neden: GameEvents statik bir kuyruk ve Domain Reload KAPALI.
        /// Bu kok kapanmazsa ikinci bir kurulumda (additive sahne, ya da
        /// Play'den cikip tekrar girip "Resume"a basmak) ayni olay IKI
        /// kez tetiklenir: oyuncu her teslimde iki kez para kazanir ve
        /// gun sonu istatistigi ikiye katlanir.
        /// </summary>
        private void OnDestroy()
        {
            GameEvents.DayStarted -= OnDayStarted;
            GameEvents.DayEnded -= OnDayEnded;

            // EconomyService de statik abone kuruyor; IDisposable
            // uyguladigi icin burada birakiliyor.
            _economy?.Dispose();
        }

        // ------------------------------------------------------------------
        // Gun dongusu
        // ------------------------------------------------------------------

        private void OnDayStarted(int day)
        {
            // Her gunun kargolarini yeniden uret. (Once yalnizca Start'ta
            // bir kez uretiliyordu, 2. gun depoda kargo kalmiyordu.)
            _cargo?.BeginDay(day);
            _delivery?.BeginDay();
        }

        private void OnDayEnded(int day)
        {
            // Once teslim edilmeyenleri basarisiz say (odemesi
            // ParcelDelivered uzerinden ekonomiye gider), sonra ozeti uret.
            _delivery?.FailRemainingParcels();
            _economy?.EndDay(day);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Yalnizca editor kurulum araclari icin. Inspector'dan elle atama
        /// yapmamiza gerek kalmasin diye ozel bir kanca; oyun calisirken
        /// (build) bu metot hic derlenmez.
        /// </summary>
        public void SetConfigForSetup(GameConfig value) => config = value;

        public void SetContentForSetup(ParcelType[] types, Vehicle.VehicleDefinition vehicle)
        {
            parcelTypes = types;
            playerVehicle = vehicle;
        }

        public void SetMapForSetup(Delivery.MapLayout layout, float radius)
        {
            mapLayout = layout;
            arrivalRadius = radius;
        }

        public void SetEconomyForSetup(Economy.EconomyConfig economy)
        {
            economyConfig = economy;
        }

        public void SetUiForSetup(UI.UIManager ui)
        {
            uiManager = ui;
        }

        public void SetCourierForSetup(Player.CourierController value)
        {
            courier = value;
        }
#endif

        private void Start()
        {
            // Gun 1'i baslat. GameEvents dinlenmeye ONCEDEN baslandi, bu
            // yuzden OnDayStarted tetiklenir ve kargolar uretilir.
            GameEvents.RaiseDayStarted(_clock.Day);
            GameEvents.RaiseDayProgressChanged(_clock.DayProgress, _clock.HourOfDay);
        }

        private void Update()
        {
            // Oyunun tek zamanlayici cagrisi.
            float dt = Time.deltaTime;

            _clock.Tick(dt);

            if (_delivery != null)
            {
                if (_playerVehicleTransform != null)
                    _delivery.VehiclePosition = _playerVehicleTransform.position;

                _delivery.Tick(dt, _clock.IsPaused);

                // Ekonomi zamani teslimat sisteminden alir: tek saat, tek
                // kaynak. Iki ayri sayaç zamanla sapar.
                if (_economy != null)
                    _economy.ElapsedProvider = _delivery.Elapsed;
            }

            // Kurye de ayni kareden surulur: yurume, fare bakisi ve
            // etkilesim ayni zamanda aksin.
            courier?.Tick(dt);

            // Arayuz de oyunun TEK Update cagrisindan surulur.
            if (uiManager != null) uiManager.Tick(dt);
        }
    }
}
