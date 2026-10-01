using System;
using System.Collections.Generic;
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

        [Header("Sunum")]
        [Tooltip("Ses yoneticisi. Bos birakilirsa oyun sessiz oynanir " +
                 "(prosedürel ses, harici dosya yok).")]
        [SerializeField] private Audio.AudioDirector audio;

        [Header("Kayit")]
        [Tooltip("Gun sonunda otomatik kayit. Kapaliysa oyun kayit tutmaz.")]
        [SerializeField] private bool autosaveOnDayEnd = true;

        private GameClock _clock;
        private CargoSystem _cargo;
        private Delivery.DeliverySystem _delivery;
        private Economy.EconomyService _economy;
        private Player.PlayerProgress _progress;
        private Transform _playerVehicleTransform;
        private bool _playerPaused;
        private Mission.MissionSystem _missions;
        private Cargo.PackagingSystem _packaging;
        private NPC.NPCSystem _npc;

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
            CreateMissions();
            CreateAudio();
            LoadFromDisk();

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
            // ASAMA 14: BASLANGIC ARACI BISIKLET.
            //
            // Oyuncu ilk oyununa bisikletle baslar (brief sira haritasi:
            // bisiklet -> motor -> arac). Van sahnede KALIR ve ileride
            // acilabilecek ust kademe arac olarak korunur; sadece baslangic
            // araci degisti.
            //
            // Neden "once" sirasi: ikisi de varsayilan olarak bulunabilir
            // ve yalnizca "bir arac bul" demek belirsiz olurdu.
            var bike = FindAnyObjectByType<Vehicle.BicycleController>();
            if (bike != null) return bike.transform;

            var vehicle = FindAnyObjectByType<Vehicle.ArcadeVehicleController>();
            if (vehicle != null) return vehicle.transform;

            return null;
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

            // Paketleme sistemi kargo sisteminden SONRA kurulur: manifest
            // `requiresPackaging` kuralini zaten okuyor, PackagingSystem
            // de ayni bayragi kendi `Required` alanina alir.
            _packaging = new Cargo.PackagingSystem(config.requiresPackaging);

            CreateNpcSystem();
        }

        /// <summary>
        /// NPC sistemi: her adres icin bir musteri.
        ///
        /// Neden ayri metot ve neden CreateCargoSystem SONUNDA: adres
        /// listesi MapLayout'ta (ScriptableObject), NPC verisi oradan
        /// turer. Kargo/teslimat sistemlerinden BAGIMSIZ olmasi gerekir:
        /// NPC sistemi teslimat akisini bilmez, sadece "bu adreste kimse
        /// var mi" sorusunu yanitlar.
        /// </summary>
        private void CreateNpcSystem()
        {
            _npc = new NPC.NPCSystem();

            if (mapLayout == null || mapLayout.deliveryPoints == null) return;

            var ids = new List<int>(mapLayout.deliveryPoints.Count);
            var names = new List<string>(mapLayout.deliveryPoints.Count);

            for (int i = 0; i < mapLayout.deliveryPoints.Count; i++)
            {
                var point = mapLayout.deliveryPoints[i];
                ids.Add(point.id);
                // Adres zaten bir musteri adi iceriyorsa onu kullan;
                // yoksa NPC sistemi kendi varsayilan adini uretir.
                names.Add(point.displayName);
            }

            // 0.8: 14 adresten gunde yaklasik 11'i evde. Yuzde 100
            // teslim edilebilir olsaydi kod dogrulamasi oyunun kalan
            // kismi gibi hissedilirdi; yuzde 0 olsaydi oyuncu ilk
            // kapida takilip vazgecerdi.
            _npc.Build(ids, names, 0.8f);
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

            // Aracin tipi soylesmeden gelir: hem bisiklet hem van bu
            // sozlesmeyi uyguluyor. Once GetComponent<ArcadeVehicleController>
            // yaziliyordu ve bisiklet secildiginde NULL donerdi -> kurye
            // arac baglantilari hic kurulmazdi.
            //
            // ONCE kameralar eslestirilir: FindVehicleCamera asagida
            // _vehicleCameras sozlusunu okur, o da WireVehicleCameras ile
            // dolar.
            WireVehicleCameras(FindAllVehicles());

            var vehicleController = _playerVehicleTransform != null
                ? _playerVehicleTransform.GetComponent<Vehicle.IVehicleController>()
                : null;

            // Kameranin araca ait olanini bul (iki arac = iki kamera).
            Camera vehicleCamera = FindVehicleCamera(vehicleController);

            var fpsCamera = courier.GetComponentInChildren<Camera>(true);

            courier.Configure(null, fpsCamera, vehicleCamera, null, vehicleController);

            // ASAMA 14: oyuncu ARTAKTA baslar.
            //
            // Once oyuncu arac icinde basliyordu ve hic gormeden
            // surmeye basliyordu. Brief'in akisi: "oyuncu bisiklete
            // yaklasir, F ile biner". Ayrica oyuncu ilk karede
            // bisikletini GORMEZSE arac seciminin hicbir anlami
            // kalmaz.
            //
            // ASAMA 17: kilit BASLANGIC aracina degil TUM araclara
            // konur.
            //
            // Oyuncu: "araca binmedim ama benim hareketler ile hareket
            // ediyor". Sebep: yalnizca baslangictaki arac kilitleniyordu.
            // Van kilitlenmedigi icin WASD oyuncunun yurumesinden gelen
            // girdiyle vani suruyordu. Oyuncu hic binmeden arac gidiyordu.
            var allVehiclesEarly = FindAllVehicles();
            foreach (var v in allVehiclesEarly)
                if (v != null) v.ControlsLocked = true;

            PlaceCourierNearVehicle(courier);
            AlignCourierModelToFeet(courier);
            // GORSEL KATMAN (dukkan/ev/sokak/NPC). Mevcut nesnelere
            // yalnizca COCUK EKLER; hicbir seyi silmez, tasirmaz.
            if (GetComponent<World.CityVisualUpgrade>() == null)
                gameObject.AddComponent<World.CityVisualUpgrade>();

            // ASAMA 17: karakter araclari ITER.
            //
            // Oyuncu bisiklete dogru yuruyunce bisiklet 0.52 m kayiyordu
            // (olculdu). CharacterController, Rigidbody'li araci itiyor.
            // Oyuncu "benim hareketler ile arac hareket ediyor" diyor ve
            // bu yarisi de dogru.
            //
            // Cozum: karakter ile araclar arasindaki carpismalar kapatilir.
            // Kargo oyununda araca yaslanmak onun icinden gecmekten
            // iyidir; oyuncu takilip kalmaz ve arac yerinden oynamaz.
            var courierCollider = courier.GetComponent<CharacterController>();
            if (courierCollider != null)
            {
                foreach (var v in allVehiclesEarly)
                {
                    if (v == null || v.VehicleTransform == null) continue;
                    var parts = v.VehicleTransform.GetComponentsInChildren<Collider>(true);
                    for (int i = 0; i < parts.Length; i++)
                        Physics.IgnoreCollision(courierCollider, parts[i], true);
                }
            }

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

            // ASAMA 30: DUKKANA GELEN MUSTERILER (yeni kutu akisi).
            //
            // Oyuncu istedi: "npc ler gelsin, gelen npc ile konusup
            // kutuyu alalim, dukkana istedigimiz bir yere yerlestirelim,
            // dagitima cikarken elimize alip arabaya yerlestirelim".
            //
            // Sahneye elle nesne eklenmez; burada kurulur. Sebep: bu
            // projede sahne YAML'ini elle duzenlemek defalarca bozdu
            // (kamera baglantilari, etiketler, RectTransform'lar) ve
            // uretim kodu her zaman daha guvenilir bir kaynak oldu.
            SpawnShopCustomers(courier);

            // ENVANTER PANELI (karakter + arac bagaji).
            // Sahneye elle eklenmez; uretim kodundan kurulur.
            _inventory = UI.CargoInventoryBuilder.Build(
                FindFirstObjectByType<Canvas>(), _cargo, courier, _delivery);

            if (_inventory != null && uiManager != null)
                uiManager.SetInventoryView(_inventory);

            // NESNE OZELLIK PENCERESI (gorev #31).
            //
            // OLCUM: Game.unity icinde `tooltipView` alanina 0 atif
            // vardi. Yani panel HIC kurulmadigi icin oyuncu odaktaki
            // kutunun ozelliklerini goremiyordu; gorev yazida
            // tamamlanmis sayiliyordu.
            _tooltip = UI.TooltipBuilder.Build(FindFirstObjectByType<Canvas>());
            if (_tooltip != null && uiManager != null)
                uiManager.SetTooltipView(_tooltip);

            // ASAMA 14: her aracin kendi kargo noktasi ve kendi
            // surucu kamerasi var.
            //
            // Once TEK bir nokta bulunuyordu (FindFirstObjectByType) ve
            // vanin kamerasi (Camera.main) her iki araca da veriliyordu.
            // Iki arac olunca bu ikisi de yanlis olurdu: bisikletin kargo
            // noktasi baglanmazdi (yukleme calismazdi) ve iki arac tek
            // kamerayi paylasirdi.
            //
            // Kargo noktasi <-> arac eslesmesi KONUMLA yapilir: nokta
            // hangi aracin sinirlari icindeyse odur. Cocuk nesne
            // (kasa, arka) oldugu icin GetComponentInParent kullanilir.
            var allCargoPoints = FindObjectsByType<Player.VanCargoPoint>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            var allVehicles = FindAllVehicles();

            Player.VanCargoPoint primaryCargoPoint = null;

            for (int i = 0; i < allCargoPoints.Length; i++)
            {
                var point = allCargoPoints[i];
                var owner = FindOwningVehicle(point, allVehicles);
                if (owner == null) continue;

                point.Configure(_cargo, _delivery, courier, owner, FindVehicleCamera(owner));

                // HUD'in kullandigi nokta: oyuncunun surdugu aracin
                // noktasi. Van ilk sira gelir (sahne kurulum sirasi).
                if (primaryCargoPoint == null) primaryCargoPoint = point;
            }

            if (primaryCargoPoint != null) uiManager?.SetVanPoint(primaryCargoPoint);

            // TUM noktalar HUD'a verilir: F tusu oyuncunun yanindaki araca
            // binmeli. Tek nokta verilseydi F daima van'a biner ve
            // bisiklete hiç binilemezdi.
            if (uiManager != null)
            {
                var pointList = new List<Player.VanCargoPoint>();
                for (int i = 0; i < allCargoPoints.Length; i++)
                {
                    var p = allCargoPoints[i];
                    if (p == null) continue;

                    var owner = FindOwningVehicle(p, allVehicles);
                    if (owner != null) pointList.Add(p);
                }
                uiManager.SetCargoPoints(pointList);
            }

            // Kurye baslangicta bisiklette mi van'da mi olacak? Simdilik
            // kurye oyuna van'da basliyor (playerVehicle = van). Bisiklet
            // ikinci arac olarak duruyor. Bu bir oyun tasarimi karari;
            // raporda PM'e soruldu.
            var bike = FindObjectOfType<Vehicle.BicycleController>();
            if (bike != null)
            {
                bike.ControlsLocked = true;
                var bikeVisual = bike.GetComponent<Player.CargoBikeVisual>();
                if (bikeVisual != null)
                {
                    Transform bed = bikeVisual.CargoBed != null
                        ? bikeVisual.CargoBed
                        : bike.transform.Find("CargoCage/CargoBed");
                    if (bed != null) bikeVisual.ConfigureForSetup(bed);
                }
            }

            var door = FindFirstObjectByType<Player.AddressDoor>();
            if (door != null)
            {
                door.Configure(_delivery, courier);
                door.SetNpcSystem(_npc);
            }

            // Teslimat kodu giris paneli. Panel kurulumda KAPALI
            // birakildigi icin pasif nesneler de aranmalidir; aksi halde
            // bulunamaz ve kod girisi hic acilmaz.
            var codeEntry = FindFirstObjectByType<UI.CodeEntryView>(
                FindObjectsInactive.Include);
            if (codeEntry != null)
            {
                codeEntry.Configure(door);
                uiManager?.SetCodeEntry(codeEntry);
            }

            // Paketleme masasi. Zaman kaynagi OLARAK SIMULASYON SAATI
            // veriliyor, Time.time degil: gun saati duraklatildiginda
            // (menu, odak kaybi) masadaki is de durmali. Aksi halde
            // oyuncu menuyu acinca kutu kendi kendine bitiyor.
            var packingTable = FindFirstObjectByType<Player.PackingTable>();
            if (packingTable != null && _packaging != null)
            {
                Transform work = packingTable.transform.Find("WorkVisual");
                packingTable.Configure(_packaging, courier, work,
                    () => _delivery != null ? _delivery.Elapsed : 0f);
            }
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

            // Domuz Donuk (Domain Reload) kapali calistirildiginda Awake
            // yeniden cagrilsa bile statik olaylarda ikinci bir kurye
            // birikmesin diye abonelik burada MUTLAKA kaldirilir.
            GameEvents.ParcelDelivered -= OnMissionParcelDelivered;
            GameEvents.PayoutApplied -= OnMissionPayoutApplied;
            GameEvents.LevelUp -= OnMissionLevelUp;

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
            _packaging?.BeginDay();
            _npc?.BeginDay(day);

            // Gunun gorevleri burada secilir: once kargo ve teslimat
            // tazelenir, sonra gorev havuzu gunun tohumuyla kurulur.
            _missions?.BeginDay(day);
        }

        /// <summary>
        /// Gunluk gorevleri kurar ve ilerlemeyi DOGRUDAN GameEvents'ten
        /// dinler.
        ///
        /// Neden ayri bir dinleyici degil: gorev ilerlemesi zaten
        /// "bir sey oldu" olaylarindan turer. Yeni bir olay turu
        /// eklersek (or. kargoda hasar) gorev sistemi degismez, sadece
        /// buraya bir satir eklenir.
        ///
        /// MissionSystem saf bir siniftir; statik event kullanmaz, bu
        /// yuzden abonelik burada kurulur ve OnDestroy'da kaldirilir.
        /// </summary>
        private void CreateMissions()
        {
            if (economyConfig == null)
            {
                // Ekonomi yoksa odul kredisi ne olacak? Bilinmiyor; gorev
                // sistemi kurulmaz. Oyun yine oynanir, sadece gorev olmaz.
                return;
            }

            _missions = new Mission.MissionSystem(BuildMissionPool(), economyConfig);

            GameEvents.ParcelDelivered += OnMissionParcelDelivered;
            GameEvents.PayoutApplied += OnMissionPayoutApplied;
            GameEvents.LevelUp += OnMissionLevelUp;
        }

        /// <summary>
        /// Gorev havuzunu kurar. Sayilar oyunun temposuna gore secildi:
        /// 12 kargoluk bir gunde 3 teslim rahatca yapilabilir, 8 zor.
        /// </summary>
        private static List<Mission.MissionDefinition> BuildMissionPool()
        {
            return new List<Mission.MissionDefinition>
            {
                Mission.MissionDefinition.Create(
                    Mission.MissionKind.DeliverCount, 3, "Bugun 3 kargo teslim et", 60, 25),

                Mission.MissionDefinition.Create(
                    Mission.MissionKind.DeliverCount, 5, "Bugun 5 kargo teslim et", 120, 45),

                Mission.MissionDefinition.Create(
                    Mission.MissionKind.EarnMoney, 400, "Bugun 400 TL kazanc yap", 100, 35),

                Mission.MissionDefinition.Create(
                    Mission.MissionKind.EarnMoney, 800, "Bugun 800 TL kazanc yap", 200, 70),

                Mission.MissionDefinition.Create(
                    Mission.MissionKind.NoFailures, 0, "Bugun hicbir teslimati bozma", 150, 60),

                Mission.MissionDefinition.Create(
                    Mission.MissionKind.OnTimeRate, 90, "Teslimatlarin %90'i zamaninda olsun", 180, 75),

                Mission.MissionDefinition.CreatePriority(
                    Mission.MissionKind.DeliverPriority, 2, "2 acil kargoyu zamaninda teslim et",
                    KargoSim.Cargo.ParcelPriority.Urgent, 160, 55),

                Mission.MissionDefinition.CreateBonus(
                    Mission.MissionKind.DeliverCount, 7, "Sezon bonusu: 7 kargo teslim et", 300, 120),
            };
        }

        private void OnMissionParcelDelivered(KargoSim.Cargo.Parcel parcel, KargoSim.Delivery.DeliveryOutcome outcome)
        {
            if (_missions == null || parcel == null) return;

            bool ok = outcome == KargoSim.Delivery.DeliveryOutcome.Success;

            // Basarisiz teslimat sayac turlerini ilerletmez ama
            // "hicbir teslimati bozma" gorevini kalici olarak bozar.
            _missions.Report(Mission.MissionKind.DeliverCount, 1, ok);
            _missions.Report(Mission.MissionKind.NoFailures, 0, ok);
            _missions.Report(Mission.MissionKind.OnTimeRate, 0, ok);
            _missions.Report(Mission.MissionKind.DeliverPriority, 1, ok, parcel.Priority);
        }

        private void OnMissionPayoutApplied(Economy.Payout payout, KargoSim.Delivery.DeliveryOutcome outcome)
        {
            if (_missions == null) return;
            _missions.Report(Mission.MissionKind.EarnMoney, payout.Payment,
                outcome == KargoSim.Delivery.DeliveryOutcome.Success);
        }

        private void OnMissionLevelUp(int level)
        {
            _missions?.Report(Mission.MissionKind.ReachLevel, level, true);
        }

        /// <summary>
        /// Sahnedeki TUM araclari toplar.
        ///
        /// Neden FindObjectsByType(IVehicleController) DEGIL: Unity'nin
        /// o overload'i `where T : Object` istiyor ve bir ARAYUZ
        /// karsilamaz (CS0311). Cozum, tum MonoBehaviour'leri taramak ve
        /// arayuzu eleyerek istemek - sahne kucuk, maliyet bir kez.
        ///
        /// Sadece bilesen tabanli araclar sayilir; arayuzu baska bir
        /// MonoBehaviour de (ornegin test kancasI) uyguluyorsa yine
        /// gecerli: bir aracin parcasi olmasi yeterli.
        /// </summary>
        private static List<Vehicle.IVehicleController> FindAllVehicles()
        {
            var result = new List<Vehicle.IVehicleController>();

            var behaviours = FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (int i = 0; i < behaviours.Length; i++)
            {
                var v = behaviours[i] as Vehicle.IVehicleController;
                if (v != null) result.Add(v);
            }

            return result;
        }

        /// <summary>
        /// Bir etkilesim noktasinin hangi araca ait oldugunu bulur.
        ///
        /// Kargo noktasi aracin kasa/arka cocuk nesnesidir; iki arac da
        /// depoya yakin durdugu icin "en yakin" secimi yeterlidir.
        /// </summary>
        private static Vehicle.IVehicleController FindOwningVehicle(
            Component point, List<Vehicle.IVehicleController> candidates)
        {
            Vehicle.IVehicleController best = null;
            float bestDistance = float.MaxValue;
            Vector3 origin = point.transform.position;

            for (int i = 0; i < candidates.Count; i++)
            {
                var v = candidates[i];
                if (v == null || v.VehicleTransform == null) continue;

                float d = (v.VehicleTransform.position - origin).sqrMagnitude;
                if (d >= bestDistance) continue;

                bestDistance = d;
                best = v;
            }

            return best;
        }

        /// <summary>
        /// Araca ait surucu kamerasini bulur.
        ///
        /// ASAMA 16 - "kamera binince sabit" hatasinin ikinci yarisi.
        ///
        /// Arama sirasi:
        ///   1) Aracin cocugu olan CameraFollow (bisiklet: gidon kamerasi).
        ///   2) Sahnedeki, HEDEFI bu arac olan CameraFollow (van: "Main
        ///      Camera" aracin cocugu DEGIL, ayri bir sahne nesnesi).
        ///   3) hicbiri bulunamazsa null.
        ///
        /// ONCEDEN `return Camera.main` vardi. Bu yanlisti: etiket
        /// degisikligiyle Camera.main oyuncunun FPS kamerasi oldu ve
        /// araca atanacak kamera oyuncunun kamerasi olarak geri donerdi.
        /// Oyuncu arac yerine kendi kamerasindan bakardi.
        /// </summary>
        private static Camera FindVehicleCamera(Vehicle.IVehicleController vehicle)
        {
            return _vehicleCameras.TryGetValue(vehicle, out var cam) ? cam : null;
        }

        /// <summary>
        /// Her araci KENDI kamerasiyla eslestirir ve kamera hedefini baglar.
        ///
        /// ASAMA 16 - "kamera binince sabit" hatasinin ASIL kok nedeni.
        ///
        /// Sahnede iki CameraFollow vardi:
        ///   - Bisikletinki prefabin icinde, hedefi dogru atanmis.
        ///   - Vaninki ayri bir SAHNE nesnesi ve `target: {fileID: 0}`,
        ///     yani HICBIR araca bagli degil.
        ///
        /// Baglanmadigi icin van kamerasi dunnya sabit bir noktada duruyordu
        /// ve MainCamera etiketi tasidigi icin her zaman ekranda oluyordu.
        /// Oyuncu aracinda donen degil, DEGISMEYEN bir goruntu goruyordu.
        ///
        /// Eslestirme sirasi:
        ///   1) Hedefi zaten bu arac olan kamera.
        ///   2) Aracin cocugu olan kamera.
        ///   3) HedeFI BOS olan kamera (van) -> araca baglanir.
        /// </summary>
        private static void WireVehicleCameras(List<Vehicle.IVehicleController> vehicles)
        {
            _vehicleCameras.Clear();

            var follows = FindObjectsByType<Player.CameraFollow>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            var claimed = new HashSet<Player.CameraFollow>();

            // 1) Hedefi belli olanlar.
            foreach (var v in vehicles)
            {
                if (v == null || v.VehicleTransform == null) continue;
                for (int i = 0; i < follows.Length; i++)
                {
                    if (claimed.Contains(follows[i]) || follows[i].Target == null) continue;
                    if (follows[i].Target != v.VehicleTransform) continue;

                    var c = follows[i].GetComponent<Camera>();
                    if (c != null) { _vehicleCameras[v] = c; claimed.Add(follows[i]); }
                    break;
                }
            }

            // 2) Aracin cocugu olanlar (bisiklet).
            foreach (var v in vehicles)
            {
                if (v == null || v.VehicleTransform == null) continue;
                if (_vehicleCameras.ContainsKey(v)) continue;

                var own = v.VehicleTransform.GetComponentsInChildren<Player.CameraFollow>(true);
                for (int i = 0; i < own.Length; i++)
                {
                    var c = own[i].GetComponent<Camera>();
                    if (c == null) continue;
                    _vehicleCameras[v] = c;
                    claimed.Add(own[i]);
                    break;
                }
            }

            // 3) HedeFI BOS olanlar -> araclara baglanir (van).
            var spare = new List<Player.CameraFollow>();
            for (int i = 0; i < follows.Length; i++)
                if (!claimed.Contains(follows[i]) && follows[i].Target == null)
                    spare.Add(follows[i]);

            int spareIndex = 0;
            foreach (var v in vehicles)
            {
                if (v == null || v.VehicleTransform == null) continue;
                if (_vehicleCameras.ContainsKey(v)) continue;
                if (spareIndex >= spare.Count) break;

                var follow = spare[spareIndex++];
                var cam = follow.GetComponent<Camera>();
                if (cam == null) continue;

                follow.SetTarget(v.VehicleTransform);
                _vehicleCameras[v] = cam;
                claimed.Add(follow);

                // Uyari oturumda BIR kez: her sahne yuklenisinde tekrarlamak
                // (testlerde 40+ kez) logu okunamaz hale getiriyor.
                if (!_warnedUnwiredCamera)
                {
                    _warnedUnwiredCamera = true;
                    Debug.LogWarning("[GameCompositionRoot] '" + cam.name +
                                     "' kamerasi hicbir araca bagli degildi; " +
                                     "simdi araca baglandi. Bu kamera daha " +
                                     "once SABIT duruyordu. Kalici cozum: " +
                                     "sahnede bu kameranin CameraFollow " +
                                     "Target alanina araci atayin.", cam);
                }
            }
        }

        private static bool _warnedUnwiredCamera;

        private static readonly Dictionary<Vehicle.IVehicleController, Camera> _vehicleCameras =
            new Dictionary<Vehicle.IVehicleController, Camera>();

        /// <summary>
        /// Kuryeyi aracin yanina, yuruyerek ulasabilecegi bir noktaya koyar.
        ///
        /// NEDEN: oyun artik arac icinde baslamiyor. Kurye aracin tam
        /// icinde dogsaydi ve bisikletin govdesi icinde kalirdi - hem
        /// gorunmez hem de fiziksel olarak takili kalirdi. Yanina
        /// birakirsak ilk is "yaklas ve F'e bas" olur.
        ///
        /// Aracin SAGINDAN 2.2 m: kargo noktasi genelde aracin saginda
        /// oldugu icin oyuncu zaten o tarafta dolasir.
        /// </summary>
        /// <summary>
        /// Dukkana gelen musterileri kurar.
        ///
        /// Oyuncu akisi (asama 30): musteri -> kutu (paketli) -> istedigin
        /// yere koy -> eline al -> araya koy.
        ///
        /// Sahneye elle nesne eklenmez; burada kurulur. Sebep: bu projede
        /// sahne YAML'ini elle duzenlemek defalarca bozdu (kamera
        /// baglantilari, etiketler, RectTransform'lar) ve uretim kodu her
        /// zaman daha guvenilir bir kaynak oldu.
        /// </summary>
        private UI.CargoInventoryView _inventory;
        private UI.ParcelTooltipView _tooltip;

        private void SpawnShopCustomers(Player.CourierController courier)
        {
            if (courier == null) return;

            // Depo alanini referans al; yoksa kuryenin cevresinde kur.
            var table = FindFirstObjectByType<Player.PackingTable>();
            Vector3 origin = table != null
                ? table.transform.position
                : courier.transform.position;

            const int musteriSayisi = 3;
            const float aralik = 2.4f;

            // Müsteriler masanin/rafin YANINA kurulur, ONUNE degil.
            //
            // Onceden depo onune (-2.6 m) konuluyordu ve odak rafta
            // kalip kutu MUSTERIDEN degil RAFTAN aliniyordu. Iki hata da
            // bundan geldi: kutu paketli degildi ve "elde kutu varken
            // ikinci kutu alinmiyor" kontrolu tutmadi.
            Vector3 side = origin + new Vector3(4.2f, 0f, 0f);

            for (int i = 0; i < musteriSayisi; i++)
            {
                var go = new GameObject("ShopCustomer_" + i);
                go.transform.SetParent(transform, false);

                // Masanin yaninda, birbirinden aralikli.
                float offset = (i - (musteriSayisi - 1) * 0.5f) * aralik;
                Vector3 pos = side + Vector3.right * offset;

                if (Physics.Raycast(pos + Vector3.up * 8f, Vector3.down,
                                     out RaycastHit hit, 60f, ~0,
                                     QueryTriggerInteraction.Ignore))
                    pos.y = hit.point.y;

                go.transform.position = pos;
                go.transform.rotation = Quaternion.LookRotation(
                    (origin - pos).normalized + Vector3.up, Vector3.up);

                // Gorunur figur: kapsul govde + ETKILESIM COLLIDER'I.
                //
                // ONEMLI: Collider SILINMEZ. Odak sistemi (ScanForFocus)
                // maske uzerinden collider arar; collider'i olmayan bir
                // musteri hicbir zaman oda��a girmez ve oyuncu kutu
                // alamazdi. (Once burada Destroy(getComponent<Collider>())
                // vardi ve musteri gorunur ama etkilesilemezdi.)
                var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.name = "Body";
                body.transform.SetParent(go.transform, false);
                body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
                body.transform.localScale = new Vector3(0.5f, 0.9f, 0.5f);

                var customer = go.AddComponent<Player.ShopCustomer>();
                customer.ConfigureForSetup("Musteri " + (i + 1));
                customer.Configure(_cargo, courier);
            }
        }

        /// <summary>
        /// Kuryeyi aracin yanina, yuruyerek ulasabilecegi bir noktaya koyar.
        /// </summary>
        /// <summary>
        /// GORSEL GOMULME DUZELTMESI.
        ///
        /// Oyuncu: "karakterin yarisi harita zemininin altinda".
        /// OLCULDU: fizik dogrudu (ayak-zemin farki 0.03 m) ama GORELEN
        /// model zemine 0.57 m gomuluyordu. Sebep: sahnede `Model`
        /// cocugunun `localPosition.y = -0.600` olmasi.
        ///
        /// NEDEN KODDA: bu projede sahne/prefab YAML'i elle duzenlendi
        /// ve uc kez bozuldu. Calisma zamaninda hizalanir.
        /// </summary>
        private void AlignCourierModelToFeet(Player.CourierController target)
        {
            if (target == null) return;

            Transform model = target.transform.Find("Model");
            if (model == null) return;

            Vector3 p = model.localPosition;
            if (Mathf.Abs(p.y) > 2f) return;   // zaten bilinmeyen deger
            if (Mathf.Approximately(p.y, 0f)) return;

            p.y = 0f;
            model.localPosition = p;

            Debug.Log("[GameCompositionRoot] Kurye modeli ayak hizasina "
                      + "getirildi (localY 0.000). 'Karakter yere gomulu' "
                      + "sikayeti olcumle 0.57 m gomulme olarak dogrulandi.");
        }

        private void PlaceCourierNearVehicle(Player.CourierController courier)
        {
            if (courier == null || _playerVehicleTransform == null) return;

            var cc = courier.GetComponent<CharacterController>();
            if (cc == null) return;

            Vector3 right = Vector3.ProjectOnPlane(
                _playerVehicleTransform.right, Vector3.up).normalized;
            if (right.sqrMagnitude < 0.01f) right = Vector3.right;

            Vector3 spot = _playerVehicleTransform.position + right * 2.2f;
            spot.y = _playerVehicleTransform.position.y;

            // Karakterin altinda zemin olsun; araç yokusunda birakılırsa
            // ilk karede havada durur ve düşer.
            if (Physics.Raycast(spot + Vector3.up * 6f, Vector3.down,
                                 out RaycastHit hit, 30f,
                                 ~0, QueryTriggerInteraction.Ignore))
            {
                spot.y = hit.point.y;
            }

            cc.enabled = false;
            courier.transform.position = spot;
            cc.enabled = true;
        }

        /// <summary>
        /// Ses yoneticisini hazirlar. Yoksa oyun sessiz oynanir; bu bir
        /// HATA degil (ses opsiyonel).
        /// </summary>
        private void CreateAudio()
        {
            if (audio == null)
            {
                audio = GetComponent<Audio.AudioDirector>();
                if (audio == null) return;
            }

            audio.Initialize();
        }

        /// <summary>
        /// Diskte kayit varsa yukler.
        ///
        /// Neden Awake'de: oyuncu Play'e basar basmaz eski parasini ve
        /// kargolarini gormeli. Kayit YOKSA hicbir sey yapilmaz
        /// (yeni oyun).
        /// </summary>
        private void LoadFromDisk()
        {
            if (_cargo == null || _progress == null) return;
            if (Save.SaveSystem.Exists() == false) return;

            Save.SaveData data = Save.SaveSystem.Load();
            if (data == null) return;

            // systemTime: aracdaki kargolarin geri sayimi nereden
            // baslayacak? Gunun gecen suresinden hesaplanir; sifir
            // verilirse kargolar gun basindan baslar.
            float systemTime = _clock != null
                ? Mathf.Clamp01(_clock.DayProgress) * config.dayLengthSeconds
                : 0f;

            Save.RestoreResult result = Save.SaveSystem.Restore(
                data, _progress, _cargo, parcelTypes, systemTime, mapLayout);

            if (result.Success == false) return;

            if (_clock != null) _clock.SetHour(data.hourOfDay);
            _delivery?.BeginDay();

            Debug.Log("[GameCompositionRoot] Kayit yuklendi: Gun " + data.day
                      + ", " + data.money + " TL, " + data.ParcelCount + " kargo.");
        }

        /// <summary>
        /// Gun sonunda kaydeder. Kayit yazilamazsa oyun DURMAZ; sadece
        /// uyari verir (kayit opsiyonel bir konfor).
        /// </summary>
        private void SaveToDisk()
        {
            if (_cargo == null || _progress == null) return;

            var parcels = new List<Cargo.Parcel>();
            parcels.AddRange(_cargo.Manifest.Available);
            parcels.AddRange(_cargo.Manifest.Loaded);

            Save.SaveData data = Save.SaveSystem.Capture(
                _clock != null ? _clock.Day : 1,
                _progress.Money,
                _progress.Xp,
                _progress.Level,
                _clock != null ? _clock.HourOfDay : config.startHour,
                parcels,
                parcelTypes,
                _cargo.GeneratedToday + 1);

            Save.SaveSystem.TrySave(data);
        }

        private void OnDayEnded(int day)
        {
            // Once teslim edilmeyenleri basarisiz say (odemesi
            // ParcelDelivered uzerinden ekonomiye gider), sonra ozeti uret.
            _delivery?.FailRemainingParcels();
            _economy?.EndDay(day);

            // Gorev kapanisi EKONOMI kapanisindAN SONRA cagrilir: "bugun
            // hicbir teslimati bozma" ve "zamaninda oran" gorevleri ancak
            // gunun tam istatistigi bittikten sonra degerlendirilebilir.
            if (_missions != null)
            {
                _missions.EndDay();
                GrantMissionRewards(day);
            }

            // Gun bittiginde arac nerede olursa olsun depoya don.
            // Ayar tanimliydi ama HICBIR YERDE okunmuyordu: oyuncu
            // 90 m uzaklakta gun bitince yeni kargo geliyor, arac orada
            // kaliyor ve "depoya don" yaziyordu.
            if (config != null && config.forceReturnToDepotOnDayEnd)
            {
                // Once yalnizca ArcadeVehicleController araniyordu; baslangic
                // araci bisiklet olunca GUN SONU ARACI GERI CAGIRILMIYORDU.
                // Oyuncu 90 m uzakta birakiliyor ve ertesi gun kargolari
                // kargida bekliyordu.
                var layout = mapLayout;

                var van = FindFirstObjectByType<Vehicle.ArcadeVehicleController>();
                if (van != null && layout != null)
                {
                    van.SetSpawnPoint(layout.depotPosition, Quaternion.identity);
                    van.Respawn();
                }

                if (_playerVehicleTransform != null && layout != null)
                {
                    var start = _playerVehicleTransform.GetComponent<Vehicle.IVehicleController>();
                    if (start != null && start.VehicleTransform != null)
                    {
                        start.VehicleTransform.position = layout.depotPosition;
                        start.VehicleTransform.rotation = Quaternion.identity;
                        start.Respawn();
                    }
                }
            }

            if (autosaveOnDayEnd) SaveToDisk();
        }

        /// <summary>
        /// Tamamlanan gorevlerin odulunu oyuncuya verir.
        ///
        /// Neden Payout uzerinden: para oyununun TEK girdisi
        /// PlayerProgress.ApplyPayout'tur. Buradan dogrudan para eklersek
        /// gun sonu ozeti bu parayi GORMEZ ve kayitta iki farkli gercek
        /// olusur. Ayni yoldan gecersek ozet, HUD ve kayit tutarli kalir.
        /// </summary>
        private void GrantMissionRewards(int day)
        {
            int money = _missions.TotalRewardMoney;
            int xp = _missions.TotalRewardXp;
            if ((money <= 0 && xp <= 0) || _progress == null) return;

            var reward = new Economy.Payout(money, 0, 0, xp);
            _progress.ApplyPayout(reward, KargoSim.Delivery.DeliveryOutcome.Success, day);

            Debug.Log("[GameCompositionRoot] Gun " + day + " gorev odulu: " +
                      money + " para, " + xp + " XP.", this);
        }

        /// <summary>
        /// Gunun gorevlerini okumak icin (HUD / panel). Gorev sistemi
        /// kurulmadiysa null doner; cagiran taraf null'a dayanik olmali.
        /// </summary>
        public Mission.MissionSystem Missions => _missions;

        /// <summary>
        /// NPC sistemi (Asama 13). HUD ve testler icin okunabilir.
        /// </summary>
        public NPC.NPCSystem Npc => _npc;

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

        /// <summary>
        /// BASLANGIC ARACINI atar (Asama 14). Kurulum araci cagirir.
        ///
        /// Neden gerekli: bu alan kargo KAPASITESINI belirliyor. Van
        /// tanimi (350 kg / 900 L) burada kalirsa oyuncu bisiklete
        /// bindiginde HUD "350 kg bos" der ve 60 kg'a sigmayacak kutulari
        /// kontrolsuz kabul ederdi.
        ///
        /// Van tanimi KORUNUR: van sahnede kalir, ileride acilabilecek ust
        /// kademe arac olarak kullanilir. Sadece baslangicta kullanilan
        /// tanim degisir.
        /// </summary>
        public void SetPlayerVehicleForSetup(Vehicle.VehicleDefinition value) =>
            playerVehicle = value;

        public void SetCourierForSetup(Player.CourierController value)        {
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

        /// <summary>
        /// Oyuncu baska bir pencereye gecince (Alt-Tab) oyun DURUR.
        /// Ayar vardi ama HICBIR YERDE okunmuyordu: oyuncu baska
        /// isine bakinca gun saati isiyordu.
        ///
        /// Neden OnApplicationFocus ve her karede Application.isFocused
        /// degil: batchmode'da isFocused HER ZAMAN false'dur. Her karede
        /// kontrol edilirse oyun teste kalici donuyor ve teslimat
        /// sistemi hic islemiyor. Unity odak degisimini YALNIZCA geri
        /// cagirir; headless ortamda hic cagrilmaz.
        /// </summary>
        private void OnApplicationFocus(bool hasFocus)
        {
            if (config == null || !config.pauseClockOnFocusLoss) return;
            _clock?.SetPaused(!hasFocus || _playerPaused);
        }

        /// <summary>Oyunun duraklatma durumu (odak kaybinda korunur).</summary>
        public void SetPlayerPaused(bool paused)
        {
            _playerPaused = paused;
            bool lostFocus = !Application.isFocused;
            _clock?.SetPaused(paused || (config != null && config.pauseClockOnFocusLoss && lostFocus));
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

            // Ses motoru hiz ile degisir, adim sesi arakla ilerler.
            // Update YOK: oyunun tek Update'inden surulur.
            if (audio != null)
            {
                bool walking = courier != null && !courier.IsInVehicle && courier.IsMoving;
                audio.SetWalking(walking);

                if (courier != null && !courier.IsInVehicle) audio.SetEngineActive(false);
                else
                {
                    audio.SetEngineActive(true);
                    float maxSpeed = playerVehicle != null ? playerVehicle.maxSpeed : 30f;
                    float ratio = maxSpeed > 0.01f
                        ? Mathf.Clamp01(Mathf.Abs(vehicleSpeedKmh) / (maxSpeed * 3.6f))
                        : 0f;
                    audio.SetEnginePitch01(ratio);
                }

                audio.Tick(dt);
            }

            // Arayuz de oyunun TEK Update cagrisindan surulur.
            if (uiManager != null) uiManager.Tick(dt);
        }

        private float vehicleSpeedKmh
        {
            get
            {
                var controller = _playerVehicleTransform != null
                    ? _playerVehicleTransform.GetComponent<Vehicle.IVehicleController>()
                    : null;
                return controller != null ? controller.SpeedKmh : 0f;
            }
        }
    }
}
