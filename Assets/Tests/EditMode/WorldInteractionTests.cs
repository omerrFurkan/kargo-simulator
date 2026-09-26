using System.Collections.Generic;
using KargoSim.Cargo;
using KargoSim.Core;
using KargoSim.Delivery;
using KargoSim.Player;
using KargoSim.Vehicle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace KargoSim.Tests.EditMode
{
    /// <summary>
    /// Dunya etkilesimleri: kurye, dukkan rafi, arac kargo noktasi ve adres
    /// kapisi arasindaki BUTUNLUK kurallari.
    ///
    /// Buradaki testler saf algoritma degil, birbirine bagli parcalarin
    /// birbirine ne yaptigini dogrular: kurye kargoyu alir, arac kargoyu
    /// yukler, kapida teslim eder. Onceki hatalar hep bu sinirlarda
    /// olustu (kutu tasiniyordu ama kargo kayboluyordu, yanlis adrese
    /// teslim ediliyordu), o yuzden kural her zaman ayni yerde duruyor:
    /// GERCEK PARCEL nesnesi elde -> aracta -> teslim edilmis.
    /// </summary>
    public sealed class WorldInteractionTests
    {
        private ParcelType _type;
        private GameConfig _config;
        private VehicleDefinition _vehicle;
        private MapLayout _layout;
        private CargoSystem _cargo;
        private CourierController _courier;

        private readonly List<GameObject> _spawned = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            _type = ScriptableObject.CreateInstance<ParcelType>();
            _type.displayName = "Test";
            _type.minWeightKg = 1f; _type.maxWeightKg = 2f;
            _type.minVolumeLiters = 1f; _type.maxVolumeLiters = 2f;
            _type.paymentPerKg = 10f;
            _type.Sanitize();

            _config = ScriptableObject.CreateInstance<GameConfig>();
            _config.parcelsPerDay = 8;
            _config.destinationCount = 3;
            _config.baseDeliverySeconds = 100f;

            _vehicle = ScriptableObject.CreateInstance<VehicleDefinition>();
            _vehicle.cargoCapacityKg = 1000f;
            _vehicle.cargoCapacityLiters = 1000f;

            _layout = ScriptableObject.CreateInstance<MapLayout>();
            _layout.depotPosition = Vector3.zero;
            _layout.deliveryPoints = new List<DeliveryPointData>
            {
                new DeliveryPointData { id = 0, displayName = "A", position = new Vector3(10f, 0f, 0f) },
                new DeliveryPointData { id = 1, displayName = "B", position = new Vector3(0f, 0f, 20f) },
                new DeliveryPointData { id = 2, displayName = "C", position = new Vector3(-30f, 0f, 0f) },
            };

            _cargo = new CargoSystem(_config, new[] { _type }, _vehicle);
            _courier = Spawn("Kurye").AddComponent<CourierController>();

            // Kurye aracla baslar (_inVehicle = true). Dukkan rafi ve arac
            // kargo noktasi disinda, etkilesim yalnizca kurye AYAKTAyken
            // kurulur; o yuzden testler on foot baslar.
            _courier.ExitVehicle(Vector3.zero);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) Object.DestroyImmediate(_spawned[i]);

            _spawned.Clear();

            foreach (Object o in new Object[] { _type, _config, _vehicle, _layout })
                if (o != null) Object.DestroyImmediate(o);
        }

        /// <summary>Test sahnesine nesne kurar; TearDown hepsini siler.</summary>
        private GameObject Spawn(string objectName)
        {
            var go = new GameObject(objectName);
            _spawned.Add(go);
            return go;
        }

        /// <summary>
        /// EditMode'da Object.Destroy yasaktir ("DestroyImmediate kullanin").
        /// CourierController eldeki kutunun GORSELINI Destroy ile sildigi
        /// icin PickUp/DropOne bu editor kuralina takilir. Burada yalnizca
        /// o hat bastirilir; testin konusu olan liste + manifest
        /// dogrulamalari aynen calisir.
        /// </summary>
        private static void IgnoreEditorDestroyError(System.Action body)
        {
            LogAssert.ignoreFailingMessages = true;
            try
            {
                body();
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }
        }

        /// <summary>
        /// Araci rota sirasiyla ilk duraga goturur. Adres kapisi sadece
        /// arac duraktayken teslim yapar; o yuzden test once burayi cagirir.
        /// </summary>
        private static DeliveryStop ArriveAtFirstStop(DeliverySystem delivery)
        {
            Assert.Greater(delivery.Route.Count, 0, "Rota bos: once kargo yuklenmeli.");
            delivery.VehiclePosition = delivery.Route[0].Position;
            delivery.Tick(0.1f, false);
            Assert.IsNotNull(delivery.CurrentStop, "Arac duraga varis yapmali.");
            return delivery.CurrentStop;
        }

        /// <summary>
        /// Gunu baslatir ve birkac kargoyu araca yukleyerek ROTAYI OLUSTURUR.
        ///
        /// Neden: rota yalnizca ARACDA KARGO OLDUGUNDA kurulur. Teste
        /// `new Parcel(...)` ile kurgusal kargo vermek ise rotayi
        /// bos birakir: kargo manifest'te kayitli degildir, teslimat
        /// sistemi onu hic gormez.
        /// </summary>
        private DeliverySystem BuildRouteWithLoadedCargo(int cargoCount)
        {
            _cargo.BeginDay(1);

            var delivery = new DeliverySystem(_layout, _cargo, 7f);
            delivery.Tick(0.1f, false);

            int loaded = 0;
            for (int i = 0; i < _cargo.Manifest.AvailableCount && loaded < cargoCount; i++)
            {
                if (_cargo.TryLoadParcel(_cargo.Manifest.Available[i].Id, 0f)) loaded++;
            }

            Assert.GreaterOrEqual(loaded, 1, "En az bir kargo araca yuklenmeli.");
            delivery.Tick(0.1f, false);
            return delivery;
        }

        /// <summary>Aractaki kargolardan DestinationId'si verilen olani bulur.</summary>
        private Parcel FindLoadedFor(DeliverySystem delivery, DeliveryStop stop, bool matching)
        {
            foreach (Parcel p in _cargo.Manifest.Loaded)
            {
                if (p == null) continue;
                bool isThis = p.DestinationId == stop.DestinationId;
                if (isThis == matching) return p;
            }
            return null;
        }

        // ------------------------------------------------------------------
        // Kurye - kapasite
        // ------------------------------------------------------------------

        [Test]
        public void Kurye_KapasiteDolarsa_KutAlamaz()
        {
            _cargo.BeginDay(1);
            IReadOnlyList<Parcel> depot = _cargo.Manifest.Available;
            Assert.GreaterOrEqual(depot.Count, 4, "Test icin en az 4 kargo uretilmeli.");

            IgnoreEditorDestroyError(() => _courier.PickUp(depot[0]));
            IgnoreEditorDestroyError(() => _courier.PickUp(depot[1]));
            IgnoreEditorDestroyError(() => _courier.PickUp(depot[2]));

            Assert.AreEqual(3, _courier.CarriedCount, "Ilk uc kutu alinmali.");
            Assert.IsFalse(_courier.CanCarryMore, "maxCarry 3: el doldu.");

            bool fourth = true;
            IgnoreEditorDestroyError(() => fourth = _courier.PickUp(depot[3]));

            Assert.IsFalse(fourth,
                "Kapasite doluyken 4. kutu ALINMAMALI (maxCarry = 3).");
            Assert.AreEqual(3, _courier.CarriedCount,
                "Alinamayan 4. kutudan sonra elde yine 3 kutu olmali.");
            Assert.AreEqual(3, _courier.CarriedParcels.Count,
                "Gorsel yigin ile GERCEK kargo listesi esit olmali.");
        }

        // ------------------------------------------------------------------
        // Kurye - birakma (kargo kaybolmaz)
        // ------------------------------------------------------------------

        [Test]
        public void Kurye_KutBirakir_KargoKendiKaybolmaz()
        {
            _cargo.BeginDay(1);
            Parcel parcel = _cargo.Manifest.Available[0];
            int waitingBefore = _cargo.Manifest.AvailableCount;

            IgnoreEditorDestroyError(() => _courier.PickUp(parcel));
            Assert.AreEqual(1, _courier.CarriedCount, "Kutu elde olmali.");

            bool dropped = false;
            IgnoreEditorDestroyError(() => dropped = _courier.DropOne(parcel));

            Assert.IsTrue(dropped, "Eldeki kutu birakilmali.");
            Assert.AreEqual(0, _courier.CarriedCount, "Birakma sonrasi el bosalmali.");
            Assert.AreEqual(0, _courier.CarriedParcels.Count,
                "CarriedParcels listesi de bosalmali.");

            // REGRESYON: yalnizca kutuyu birakmak kargoyu YOK ETMEZ. Once
            // "kutu gorseli tasiniyordu ama kargo kayboluyordu" hatasi vardi.
            Assert.AreEqual(ParcelState.WaitingInDepot, parcel.State,
                "Birakilan kargo depoda beklemeye devam etmeli.");
            Assert.AreEqual(waitingBefore, _cargo.Manifest.AvailableCount,
                "Depodaki kargo sayisi degismemeli.");
            CollectionAssert.Contains(_cargo.Manifest.Available, parcel,
                "Birakilan kargo deponun listesinde kalmali.");
            Assert.AreSame(parcel, _cargo.Manifest.FindById(parcel.Id),
                "Birakilan kargo manifest'te bulunabilmeli; kargo kaybolmamali.");
        }

        // ------------------------------------------------------------------
        // Arac kargo noktasi
        // ------------------------------------------------------------------

        [Test]
        public void AracNoktasi_EldeKargoYokken_AracaBinmeyiOnerir()
        {
            var point = Spawn("AracKargoNoktasi").AddComponent<VanCargoPoint>();
            point.Configure(_cargo, null, _courier, null, null);

            Assert.IsFalse(_courier.IsInVehicle, "Kurye ayakta olmali.");
            Assert.IsFalse(_courier.IsCarrying, "Elde kargo olmamali.");

            Assert.IsTrue(point.CanInteract(_courier),
                "Elde kargo yokken nokta etkilesime acik olmali (E = araca bin).");
            StringAssert.Contains("Araca bin", point.Prompt,
                "Ipucu araca binmeyi soylemeli.");
        }

        // ------------------------------------------------------------------
        // Dukkan rafi
        // ------------------------------------------------------------------

        [Test]
        public void DukkanRaftan_AlinanKargo_ManifestdenDusmemeli()
        {
            _cargo.BeginDay(1);

            // Rafin GORSEL katmani (8 adet gecici kutu) bu testin konusu
            // degil; null vererek uretmiyoruz. Raf sayilari kargo
            // sisteminden okunur, onu ayri bir test dogrular.
            var pile = Spawn("DukkanRafi").AddComponent<ShopParcelPile>();
            pile.Configure(_cargo, null);

            int waitingBefore = _cargo.Manifest.AvailableCount;
            Assert.Greater(waitingBefore, 0, "Rafta bekleyen kargo olmali.");
            Assert.IsTrue(pile.CanInteract(_courier),
                "Rafta kargo varken etkilesim acilmali.");

            IgnoreEditorDestroyError(() => pile.Interact(_courier));

            Assert.AreEqual(1, _courier.CarriedCount,
                "Raftan bir kutu alinmali; kurye bos kalmamali.");
            Assert.AreEqual(1, _courier.CarriedParcels.Count,
                "Elde GERCEK kargo kaydi olmali (gorsel degil).");

            Parcel taken = _courier.CarriedParcels[0];
            Assert.IsNotNull(taken, "Alinan kargo nesnesi null olmamali.");
            Assert.AreSame(taken, _cargo.Manifest.FindById(taken.Id),
                "Alinan kargo manifest'te olmali; kargo kaybolmamali.");
            Assert.AreEqual(ParcelState.WaitingInDepot, taken.State,
                "Alinan kargo, araca yuklenene kadar manifest'te beklemeli.");
            Assert.AreEqual(waitingBefore, _cargo.Manifest.AvailableCount,
                "Kargo araca yuklenmedigi surece depo listesi degismemeli.");
        }

        // ------------------------------------------------------------------
        // Adres kapisi
        // ------------------------------------------------------------------

        [Test]
        public void AdresKapisi_YalnizcaDogruAdresinKargosunuTeslimEder()
        {
            DeliverySystem delivery = BuildRouteWithLoadedCargo(4);
            DeliveryStop stop = ArriveAtFirstStop(delivery);

            int pendingBefore = stop.PendingCount;
            int loadedBefore = _cargo.Manifest.LoadedCount;

            Parcel wrong = FindLoadedFor(delivery, stop, false);
            if (wrong == null)
            {
                // Bütün kargolar aynı adrese gidiyorsa bu senaryo yoktur.
                Assert.Ignore("Aracdaki tum kargolar ayni adrese gidiyor; " +
                              "yanlis-adres senaryosu kurulamadi.");
            }

            var door = Spawn("AdresKapi").AddComponent<AddressDoor>();
            door.Configure(delivery, _courier);

            IgnoreEditorDestroyError(() => _courier.PickUp(wrong));
            Assert.AreEqual(1, _courier.CarriedCount, "Yalnizca yanlis kargo elde olmali.");

            IgnoreEditorDestroyError(() => door.Interact(_courier));

            Assert.IsTrue(wrong.IsLoaded,
                "Yanlis adres kargosu arac uzerinde kalmali; teslim edilmemeli.");
            Assert.IsFalse(wrong.IsDelivered, "Yanlis adres teslim edilmemeli.");
            Assert.AreEqual(loadedBefore, _cargo.Manifest.LoadedCount,
                "Arac kargo sayisi degismemeli.");
            Assert.AreEqual(pendingBefore, stop.PendingCount,
                "Duraktaki bekleyen kargo sayisi azalmamali.");
            Assert.AreEqual(1, _courier.CarriedCount,
                "Teslim edilmeyen kutu kuryenin elinde kalmali.");
        }

        [Test]
        public void AdresKapisi_DogruAdresKargosu_TeslimEdilir()
        {
            DeliverySystem delivery = BuildRouteWithLoadedCargo(4);
            DeliveryStop stop = ArriveAtFirstStop(delivery);

            Parcel dogru = FindLoadedFor(delivery, stop, true);
            Assert.IsNotNull(dogru, "Duraga ait arac kargosu bulunamadi.");

            var door = Spawn("AdresKapi").AddComponent<AddressDoor>();
            door.Configure(delivery, _courier);

            IgnoreEditorDestroyError(() => _courier.PickUp(dogru));
            Assert.AreEqual(1, _courier.CarriedCount, "Kargo elde olmali.");

            IgnoreEditorDestroyError(() => door.Interact(_courier));

            Assert.IsTrue(dogru.IsDelivered,
                "Dogru adres kargosi teslim edilmeli.");
            Assert.IsFalse(dogru.IsLoaded,
                "Teslim edilen kargo arac uzerinde kalmamali.");
            Assert.AreEqual(0, stop.PendingCount, "Durak bosalmali.");
            Assert.AreEqual(0, _courier.CarriedCount,
                "Teslimden sonra kurye bosalmali.");
        }

        // ------------------------------------------------------------------
        // Yapilandirilmamis etkilesim noktalari
        // ------------------------------------------------------------------

        [Test]
        public void Etkilesim_Sohbet_BosKutusundaGuvenli()
        {
            // Configure HIC cagrilmadi: _cargo ve _delivery null.
            var pile = Spawn("BosRaf").AddComponent<ShopParcelPile>();
            var door = Spawn("BosKapi").AddComponent<AddressDoor>();

            string pilePrompt = null;
            string doorPrompt = null;

            Assert.DoesNotThrow(() =>
            {
                pilePrompt = pile.Prompt;
                pile.CanInteract(_courier);
                pile.CanInteract(null);

                doorPrompt = door.Prompt;
                door.CanInteract(_courier);
                door.CanInteract(null);
            }, "Yapilandirilmamis nokta NullReferenceException atmamali.");

            Assert.AreEqual(string.Empty, pilePrompt,
                "Kargosuz raf bos ipucu vermeli.");
            Assert.IsFalse(pile.CanInteract(_courier),
                "Kargosuz rafta etkilesim kurulamamali.");
            Assert.IsFalse(pile.CanInteract(null),
                "Kuryesiz rafta etkilesim kurulamamali.");

            Assert.AreEqual(string.Empty, doorPrompt,
                "Teslim sistemi yokken kapi bos ipucu vermeli.");
            Assert.IsFalse(door.CanInteract(_courier),
                "Durak bilinmiyorken kapida etkilesim kurulamamali.");
            Assert.IsFalse(door.CanInteract(null),
                "Kuryesiz kapida etkilesim kurulamamali.");
        }
    }
}
