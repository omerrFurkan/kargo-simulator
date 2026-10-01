using System;
using Object = UnityEngine.Object;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using KargoSim.Cargo;

namespace KargoSim.Tests.PlayMode
{
    /// <summary>
    /// YENI KARGO MEKANIĞI - otomatik test (kargoMekanigi.md #128) ve
    /// negatif testler (#126).
    ///
    /// Kapsam:
    ///   Parcel data    : benzersiz id, gecerli agirlik/hacim, gecerli state
    ///   Pickup         : dogru nesne secilir, TEK kargo, state
    ///   Drop           : gecerli yuzey, gecersiz yuzey, KAYIP YOK
    ///   Vehicle load   : agirlik kapasitesi, hacim kapasitesi, state, gorunurluk
    ///   Vehicle unload : TEK kargo, dogru kargo secilir, state korunur
    ///   Delivery       : dogru/yankargo/yanlis kod/evde yok/teslim/duplicate
    ///   Save           : state korunur
    ///
    /// ALTIN KURAL (#87): hicbir testte kargo kaybolmaz. Her FAIL
    /// durumunda kutu ESKI DURUMUNDA kalir.
    /// </summary>
    public class NewCargoMechanicsTests
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";

        private Core.GameCompositionRoot _root;
        private Player.CourierController _courier;
        private CargoCarryController _carry;
        private CargoWorldRegistry _world;
        private VehicleCargoArea _area;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (SceneManager.GetSceneByName("NewCargoCleanup").IsValid())
                yield break;
            Scene fresh = SceneManager.CreateScene("NewCargoCleanup");
            SceneManager.SetActiveScene(fresh);
            yield return null;
            Scene game = SceneManager.GetSceneByName("Game");
            if (game.IsValid() && game.isLoaded)
            {
                AsyncOperation u = SceneManager.UnloadSceneAsync(game);
                while (!u.isDone) yield return null;
            }
            yield return null;
            Core.GameEvents.ResetForTests();
        }

        private IEnumerator LoadGame()
        {
            SceneManager.LoadScene(ScenePath, LoadSceneMode.Single);
            yield return null; yield return null; yield return null;
            for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();

            _root = Object.FindFirstObjectByType<Core.GameCompositionRoot>();
            _courier = Object.FindFirstObjectByType<Player.CourierController>();
            Assert.IsNotNull(_courier, "Kurye yok.");

            _carry = _courier.Carry;
            Assert.IsNotNull(_carry, "TEK-KARGO tasiyici yok. kargoMekanigi #10.");
            _world = CargoWorldRegistry.Instance;

            var bike = Object.FindFirstObjectByType<Vehicle.BicycleController>();
            Assert.IsNotNull(bike, "Bisiklet yok.");
            _area = bike.GetComponentInChildren<VehicleCargoArea>(true);
            Assert.IsNotNull(_area, "Bisiklette CargoArea yok. kargoMekanigi #30.");

            // Oyuncuyu arac DIŞINDA baslat.
            _courier.transform.position = bike.transform.position + new Vector3(2.5f, 0.5f, 2.5f);
            yield return null;
        }

        /// <summary>Depoda fiziksel kutu uretir ve kuryenin onune koyar.</summary>
        private ParcelWorldObject SpawnBoxInFront(int index = 0)
        {
            var depot = _root.Cargo.Manifest.Available;
            if (depot.Count <= index) return null;
            Parcel p = depot[index];

            Vector3 at = _courier.transform.position
                       + _courier.transform.forward * 1.4f;
            // Zemine otur
            if (Physics.Raycast(at + Vector3.up * 3f, Vector3.down,
                    out RaycastHit hit, 10f, ~0, QueryTriggerInteraction.Ignore))
                at = hit.point + Vector3.up * 0.2f;

            ParcelWorldObject box = _world.GetOrCreate(p, at);
            if (box != null) box.ApplyPhysicsForState(p.State);
            return box;
        }

        // ==================================================================
        // PARCEL DATA (#128)
        // ==================================================================
        [UnityTest]
        public IEnumerator Parcel_IdBenzersizVeVerilerGecerli()
        {
            yield return LoadGame();
            _root.Cargo.BeginDay(1);

            var ids = new HashSet<int>();
            var depot = _root.Cargo.Manifest.Available;
            Assert.Greater(depot.Count, 0, "Gun 1 icin kargo uretilmedi.");

            for (int i = 0; i < depot.Count; i++)
            {
                Parcel p = depot[i];
                Assert.IsTrue(ids.Add(p.Id),
                    "Ayni ParcelId iki kargoda bulundu: #" + p.Id
                    + " (kargoMekanigi #79).");
                Assert.Greater(p.WeightKg, 0f, "Agirlik gecersiz (#128).");
                Assert.Greater(p.VolumeLiters, 0f, "Hacim gecersiz (#128).");
                Assert.IsTrue(Enum.IsDefined(typeof(ParcelState), p.State),
                    "State gecersiz: " + p.State);
                Assert.IsTrue(p.ShortLabel.StartsWith("P"),
                    "ParcelId bicimi P0001 olmali (#79): " + p.ShortLabel);
            }
        }

        [Test]
        public void State_MakinesiGecersizGecisleriReddeder()
        {
            // #63: Delivered -> Held / Loaded / Available YASAK
            Assert.IsFalse(ParcelStateRules.CanGoTo(ParcelState.Delivered, ParcelState.Carried),
                "Teslim edilmis kargo ELDE alinamaz (#61 duplicate).");
            Assert.IsFalse(ParcelStateRules.CanGoTo(ParcelState.Delivered, ParcelState.LoadedOnVehicle),
                "Teslim edilmis kargo yuklenemez (#61).");

            // #63: Loaded -> Packaging DOGRUDAN olmaz
            Assert.IsFalse(ParcelStateRules.CanGoTo(ParcelState.LoadedOnVehicle, ParcelState.Packaging),
                "Aracdan dogrudan paketlemeye gecilemez; once cikarilmalı.");

            // Gecerli yollar
            Assert.IsTrue(ParcelStateRules.CanGoTo(ParcelState.WaitingInDepot, ParcelState.Carried),
                "Depodan almak gecerli.");
            Assert.IsTrue(ParcelStateRules.CanGoTo(ParcelState.Carried, ParcelState.World),
                "Yere birakmak gecerli.");
            Assert.IsTrue(ParcelStateRules.CanGoTo(ParcelState.Carried, ParcelState.LoadedOnVehicle),
                "Araca koymak gecerli.");
        }

        // ==================================================================
        // PICKUP (#11, #21)
        // ==================================================================
        [UnityTest]
        public IEnumerator Pickup_KutuFizikselOlarakElegeGecer()
        {
            yield return LoadGame();
            _root.Cargo.BeginDay(1);

            ParcelWorldObject box = SpawnBoxInFront();
            Assert.IsNotNull(box, "Fiziksel kutu uretilemedi (#117).");
            yield return null;

            Vector3 worldBefore = box.transform.position;
            Assert.IsTrue(_carry.TryPickup(box, _courier.GetComponent<Collider>()),
                "Kutu alinamadi. " + _carry.LastMessage);
            yield return null;

            Assert.IsTrue(_carry.HasCargo, "Elde kargo gorunmuyor.");
            Assert.AreEqual(ParcelState.Carried, box.Parcel.State, "State = Carried olmali.");
            Assert.AreEqual(_carry.HoldAnchor, box.transform.parent,
                "Kutu CargoHoldAnchor'a baglanmali (#11, #12).");

            // Kinematik: tam serbest rigidbody DEGIL (#13)
            var rb = box.GetComponent<Rigidbody>();
            Assert.IsTrue(rb.isKinematic, "Eldeyken Rigidbody kinematic olmali (#13).");
            Assert.IsFalse(rb.useGravity, "Eldeyken yercekimi kapali olmali (#13).");

            // "Otomatik envanter" DEGIL: kutu hala fiziksel bir nesne
            Assert.IsTrue(box.gameObject.activeInHierarchy,
                "Kutu ortadan kalkmamali (#120 yasak).");
            TestContext.WriteLine("[Olcum] kutu dunya: " + worldBefore.ToString("F2")
                + " -> el: " + box.transform.position.ToString("F2"));
        }

        [UnityTest]
        public IEnumerator Pickup_EldeVarkenIkinciKutuAlinamaz_N2()
        {
            yield return LoadGame();
            _root.Cargo.BeginDay(1);

            ParcelWorldObject a = SpawnBoxInFront(0);
            yield return null;
            Assert.IsTrue(_carry.TryPickup(a, _courier.GetComponent<Collider>()), a.Parcel.ShortLabel);
            yield return null;

            // Ikinci kutu
            var depot = _root.Cargo.Manifest.Available;
            Parcel p2 = depot[1];
            Vector3 at = _courier.transform.position + _courier.transform.forward * 1.4f;
            ParcelWorldObject b = _world.GetOrCreate(p2, at);
            yield return null;

            bool second = _carry.TryPickup(b, _courier.GetComponent<Collider>());

            Assert.IsFalse(second, "Elde kargo varken 2. kutu ALINMAMALI (N2 / #10).");
            Assert.AreSame(a, _carry.HeldBox, "Eldeki kutu DEGISMEMELI.");
            // Alinamayan kutu ELDE DEGIL, depoda kaliyor: durumu DEGISMEMELI.

            Assert.AreEqual(ParcelState.WaitingInDepot, b.Parcel.State,
                "Alinamayan kutu KAYBOLMAMALI; eski durumunda kalmali (#57).");

            StringAssert.Contains("birak", _carry.LastMessage,
                "Oyuncuya sebep yazilmali (#21).");
        }

        [UnityTest]
        public IEnumerator Pickup_TeslimEdilmisKutuAlinamaz_N8()
        {
            yield return LoadGame();
            _root.Cargo.BeginDay(1);

            ParcelWorldObject box = SpawnBoxInFront();
            Assert.IsNotNull(box, "Kutu yok.");
            box.Parcel.MarkDelivered();
            yield return null;

            bool ok = _carry.TryPickup(box, _courier.GetComponent<Collider>());

            Assert.IsFalse(ok, "Teslim edilmis kargo tekrar ALINMAMALI (N8 / #61).");
            Assert.AreEqual(ParcelState.Delivered, box.Parcel.State,
                "State degismemeli.");
        }

        // ==================================================================
        // DROP (#15, #16, #17, #18)
        // ==================================================================
        [UnityTest]
        public IEnumerator Drop_KutuYereOturVeTekrarAlinabilir()
        {
            yield return LoadGame();
            _root.Cargo.BeginDay(1);

            ParcelWorldObject box = SpawnBoxInFront();
            yield return null;
            Assert.IsTrue(_carry.TryPickup(box, _courier.GetComponent<Collider>()));
            yield return null;
            int id = box.Parcel.Id;

            Assert.IsTrue(_carry.TryDrop(), "G ile birakma basarisiz. " + _carry.LastMessage);
            yield return null; yield return null;

            Assert.IsFalse(_carry.HasCargo, "Elde kargo kalmamali.");
            Assert.AreEqual(ParcelState.World, _world.Find(id).Parcel.State,
                "State = World olmali.");

            // TEKRAR AL (#119): ayni kutu tekrar alinabilmeli
            ParcelWorldObject again = _world.Find(id);
            Assert.IsNotNull(again, "Kutu kayboldu! ALTIN KURAL ihlali (#57).");

            bool ok = _carry.TryPickup(again, _courier.GetComponent<Collider>());
            Assert.IsTrue(ok, "Birakilan kutu TEKRAR ALINAMADI. " + _carry.LastMessage);
            Assert.AreEqual(id, _carry.HeldParcel.Id, "FARKLI kutu alindi.");
        }

        [UnityTest]
        public IEnumerator Drop_AtmaDegilKontrolluBirakma()
        {
            yield return LoadGame();
            _root.Cargo.BeginDay(1);

            ParcelWorldObject box = SpawnBoxInFront();
            yield return null;
            _carry.TryPickup(box, _courier.GetComponent<Collider>());
            yield return null;

            Vector3 player = _courier.transform.position;
            Assert.IsTrue(_carry.TryDrop(), "Birakma basarisiz.");
            yield return null; yield return null;

            Vector3 d = box.transform.position - player;
            // Kutuyu oyuncudan 5+ m uzaklastirmak "atmak" olurdu (#18)
            Assert.Less(d.magnitude, 4f,
                "Kutu oyuncudan " + d.magnitude.ToString("F1")
                + " m uzakta. G BIRLATMA TUSU DEGILDIR (#18).");
            Assert.LessOrEqual(d.y, 0.6f,
                "Kutu havada kalmamali, yere oturmali (#131).");
        }

        // ==================================================================
        // VEHICLE LOAD (#33, #37, #38, #39, #41, #42, #122)
        // ==================================================================
        [UnityTest]
        public IEnumerator Yukleme_KapasiteGecerliVeKasaIcindeGorunur()
        {
            yield return LoadGame();
            _root.Cargo.BeginDay(1);

            // Kuryeyi aracin yanina tasi
            var bike = _area.transform.root;
            _courier.transform.position = bike.position + new Vector3(1.2f, 0.4f, 1.2f);
            yield return null; yield return null;

            ParcelWorldObject box = SpawnBoxInFront();
            yield return null;
            Assert.IsTrue(_carry.TryPickup(box, _courier.GetComponent<Collider>()));
            yield return null;

            Assert.IsTrue(_carry.TryLoadToVehicle(_area),
                "Araca yuklenmedi. " + _carry.LastMessage);
            yield return null;

            Assert.IsFalse(_carry.HasCargo, "Elde kargo kalmamali.");
            Assert.AreEqual(ParcelState.LoadedOnVehicle, box.Parcel.State, "State = Loaded.");
            Assert.AreEqual(1, _area.Count, "Kasada 1 kargo olmali.");
            Assert.Greater(_area.UsedWeightKg, 0f, "Agirlik sayaci calismali (#40).");
            Assert.Greater(_area.UsedVolumeLiters, 0f, "Hacim sayaci calismali (#40).");

            // Kutu GERCEKTEN kasanin icinde (#105)
            Vector3 lp = _area.transform.InverseTransformPoint(box.transform.position);
            Assert.Less(Mathf.Abs(lp.x), 1.5f, "Kutu kasa disina tasmis (#105).");
            Assert.Less(Mathf.Abs(lp.y), 1.5f, "Kutu kasa disina tasmis (#105).");
        }

        [UnityTest]
        public IEnumerator Yukleme_KapasiteAsimiKutuyuEldenDusurmez()
        {
            yield return LoadGame();
            _root.Cargo.BeginDay(1);

            var bike = _area.transform.root;
            _courier.transform.position = bike.position + new Vector3(1.2f, 0.4f, 1.2f);
            yield return null;

            ParcelWorldObject box = SpawnBoxInFront();
            yield return null;
            _carry.TryPickup(box, _courier.GetComponent<Collider>());
            yield return null;

            // Kapasiteyi neredeyse sifirla
            _area.Configure(new CargoCapacity(0.2f, 0.2f));

            bool ok = _carry.TryLoadToVehicle(_area);

            Assert.IsFalse(ok, "Kapasite asimi basarili OLMAMALI (N4 / #39).");
            Assert.IsTrue(_carry.HasCargo,
                "Kapasite reddedilen kutu ELDE KALMALI - kaybolmamali (#41).");
            Assert.AreEqual(0, _area.Count, "Kasaya bir sey girmemeli.");
            StringAssert.Contains("kapasite", _carry.LastMessage.ToLower(),
                "Oyuncuya kapasite sebebi yazilmali (#42).");
        }

        [UnityTest]
        public IEnumerator Yukleme_TekTekTekKargo_YoksaTopluYok()
        {
            yield return LoadGame();
            _root.Cargo.BeginDay(1);

            var bike = _area.transform.root;
            _courier.transform.position = bike.position + new Vector3(1.2f, 0.4f, 1.2f);
            yield return null;

            ParcelWorldObject a = SpawnBoxInFront(0);
            yield return null;
            _carry.TryPickup(a, _courier.GetComponent<Collider>());
            yield return null;
            _carry.TryLoadToVehicle(_area);
            yield return null;

            // Ikinci kutu: once ele alinir, SONRA araca konur.
            // "E -> tum kargolar yuklendi" YASAK (#122)
            var depot = _root.Cargo.Manifest.Available;
            ParcelWorldObject b = _world.GetOrCreate(depot[1],
                _courier.transform.position + _courier.transform.forward * 1.3f);
            yield return null;

            _carry.TryPickup(b, _courier.GetComponent<Collider>());
            yield return null;
            _carry.TryLoadToVehicle(_area);
            yield return null;

            Assert.AreEqual(2, _area.Count, "Iki kargo ayri ayri yuklenmeli.");
        }

        // ==================================================================
        // VEHICLE UNLOAD (#44, #45, #46, #123, #104)
        // ==================================================================
        [UnityTest]
        public IEnumerator Cikarma_TekTekVeBakinKutuSecilir()
        {
            yield return LoadGame();
            _root.Cargo.BeginDay(1);

            var bike = _area.transform.root;
            _courier.transform.position = bike.position + new Vector3(1.2f, 0.4f, 1.2f);
            yield return null;

            ParcelWorldObject a = SpawnBoxInFront(0);
            yield return null;
            _carry.TryPickup(a, _courier.GetComponent<Collider>());
            yield return null;
            _carry.TryLoadToVehicle(_area);
            yield return null;

            var depot = _root.Cargo.Manifest.Available;
            ParcelWorldObject b = _world.GetOrCreate(depot[1],
                _courier.transform.position + _courier.transform.forward * 1.3f);
            yield return null;
            _carry.TryPickup(b, _courier.GetComponent<Collider>());
            yield return null;
            _carry.TryLoadToVehicle(_area);
            yield return null;

            Assert.AreEqual(2, _area.Count, "Hazirlik: kasada 2 kargo.");

            // Bir kutu sec: o BAKILAN kutu
            ParcelWorldObject target = _area.Occupants[0];
            int targetId = target.Parcel.Id;
            int otherId = _area.Occupants[1].Parcel.Id;

            bool ok = _carry.TryUnloadFromVehicle(_area, target);
            Assert.IsTrue(ok, "Kasadan kargo alinamadi. " + _carry.LastMessage);
            yield return null;

            Assert.AreEqual(1, _area.Count,
                "TEK kargo cikmali; tumu birden cikmamali (#44 / #123).");
            Assert.AreEqual(targetId, _carry.HeldParcel.Id,
                "Secilen kutu alinmali (#45).");
            Assert.AreNotEqual(otherId, _carry.HeldParcel.Id,
                "SISTEM BASKA KUTUYU SECTI - oyuncu bunu gormemisti!");
            Assert.AreEqual(ParcelState.Carried, target.Parcel.State, "State = Carried.");
        }

        [UnityTest]
        public IEnumerator Cikarma_EldeVarkenKasayaKoyulamaz()
        {
            yield return LoadGame();
            _root.Cargo.BeginDay(1);

            var bike = _area.transform.root;
            _courier.transform.position = bike.position + new Vector3(1.2f, 0.4f, 1.2f);
            yield return null;

            ParcelWorldObject a = SpawnBoxInFront(0);
            yield return null;
            _carry.TryPickup(a, _courier.GetComponent<Collider>());
            yield return null;
            _carry.TryLoadToVehicle(_area);
            yield return null;

            // Yeni kutu al
            var depot = _root.Cargo.Manifest.Available;
            ParcelWorldObject b = _world.GetOrCreate(depot[1],
                _courier.transform.position + _courier.transform.forward * 1.3f);
            yield return null;
            _carry.TryPickup(b, _courier.GetComponent<Collider>());
            yield return null;

            bool ok = _carry.TryUnloadFromVehicle(_area, a);

            Assert.IsFalse(ok,
                "Elde kargo varken kasadan 2. kutu ALINMAMALI (tek kargo kuralı).");
            Assert.AreEqual(1, _area.Count, "Kasada degisiklik olmamali.");
        }

        [UnityTest]
        public IEnumerator AracCikincaKargoKasadaKalir_N10()
        {
            yield return LoadGame();
            _root.Cargo.BeginDay(1);

            var bike = _area.transform.root;
            _courier.transform.position = bike.position + new Vector3(1.2f, 0.4f, 1.2f);
            yield return null;

            ParcelWorldObject a = SpawnBoxInFront(0);
            yield return null;
            _carry.TryPickup(a, _courier.GetComponent<Collider>());
            yield return null;
            _carry.TryLoadToVehicle(_area);
            yield return null;
            int id = a.Parcel.Id;

            var point = bike.GetComponentInChildren<Player.VanCargoPoint>(true);
            Assert.IsNotNull(point, "VanCargoPoint yok.");

            // Bin, sur, in
            point.Board(_courier);
            yield return null;
            Assert.IsTrue(_courier.IsInVehicle, "Araca binemedi.");

            for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();

            _courier.ExitVehicle(point.ExitSpot());
            yield return null; yield return null;

            Assert.IsFalse(_courier.IsInVehicle, "Aracdan inemedi.");
            Assert.AreEqual(1, _area.Count,
                "Arac cikinca kargo KAYBOLMAMALI (N10 / #48 / #104).");
            Assert.IsNotNull(_world.Find(id), "Fiziksel kutu hala durmali.");
            Assert.AreEqual(ParcelState.LoadedOnVehicle, _world.Find(id).Parcel.State,
                "State degismemeli.");
        }

        // ==================================================================
        // TESLIMAT (#53, #57, #58, #100)
        // ==================================================================
        [UnityTest]
        public IEnumerator YanlisAdresTeslimReddedilirVeKargoKalir_N5()
        {
            yield return LoadGame();
            _root.Cargo.BeginDay(1);

            var depot = _root.Cargo.Manifest.Available;
            Assert.Greater(depot.Count, 0, "Kargo yok.");
            Parcel p = depot[0];

            var doors = Object.FindObjectsByType<Player.AddressDoor>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.Greater(doors.Length, 0, "Adres kapisi yok.");

            // p'nin ADRESI OLMAYAN bir kapi sec
            Player.AddressDoor wrong = null;
            for (int i = 0; i < doors.Length; i++)
            {
                int id = IdOf(doors[i]);
                if (id >= 0 && id != p.DestinationId) { wrong = doors[i]; break; }
            }
            Assert.IsNotNull(wrong,
                "Yanlis adres kapi bulunamadi (p adresi " + p.DestinationId + ").");


            // Kargoyu ele al
            Vector3 at = _courier.transform.position + _courier.transform.forward * 1.4f;
            ParcelWorldObject box = _world.GetOrCreate(p, at);
            yield return null;
            _carry.TryPickup(box, _courier.GetComponent<Collider>());
            yield return null;
            Assert.IsTrue(_carry.HasCargo, "Kutu alinamadi.");

            string result = wrong.TryDeliverForTest(box.Parcel);
            yield return null;

            Assert.IsNotEmpty(result, "Yanlis adres bir SEBEP dondurmeli.");
            Assert.IsTrue(_carry.HasCargo,
                "Yanlis adreste kargo ELDEN DUSMEMELI (#100 / #57).");
            Assert.AreEqual(ParcelState.Carried, box.Parcel.State,
                "State degismemeli.");
        }

        [UnityTest]
        public IEnumerator YanlisKodTeslimYokKargoKalir_N6()
        {
            yield return LoadGame();
            _root.Cargo.BeginDay(1);

            var depot = _root.Cargo.Manifest.Available;
            Parcel p = depot[0];

            Player.AddressDoor door = DoorFor(p.DestinationId);
            Assert.IsNotNull(door, "Dogru kapi bulunamadi (adres " + p.DestinationId + ").");

            Vector3 at = _courier.transform.position + _courier.transform.forward * 1.4f;
            ParcelWorldObject box = _world.GetOrCreate(p, at);
            yield return null;
            _carry.TryPickup(box, _courier.GetComponent<Collider>());
            yield return null;

            // ONCE teslim oturumu kurulmalı: arac dogru durakta olmali
            // ve kurye kapiya yakin olmali.
            Assert.IsTrue(ArriveAtAddressForTest(p.DestinationId),
                "Arac " + p.DestinationId + " adresine varamadi. " + _lastArriveError);

            bool opened = door.KnockForTest();
            yield return null;

            if (!opened)
            {
                // --------------------------------------------------------
                // N7: NPC EVDE DEGIL.
                // Bu bir hata DEGIL, dogru davranis: teslim olmaz ve
                // kargo oyuncuda kalir; oyuncu ayni adrese tekrar
                // gelip tekrar deneyebilir (kargoMekanigi #59, #103).
                // --------------------------------------------------------
                Assert.IsTrue(_carry.HasCargo, "NPC yokken kargo ELDE KALMALI (#59).");
                Assert.IsFalse(p.IsDelivered, "NPC yokken teslim OLMAMALI (N7).");
                Assert.AreEqual(ParcelState.Carried, box.Parcel.State, "State degismemeli.");
                TestContext.WriteLine("[N7] NPC evde degil -> teslim yok, kargo elde. DOGRULANDI.");
                yield break;
            }

            bool wrong = door.SubmitCodeForTest(9999);

            yield return null;

            Assert.IsFalse(wrong, "YANLIS kod kabul EDILMEMELI (N6 / #58).");
            Assert.IsTrue(_carry.HasCargo, "Yanlis kodda kargo ELDE KALMALI (#58).");
            Assert.AreEqual(ParcelState.Carried, box.Parcel.State, "State degismemeli.");
            Assert.IsFalse(p.IsDelivered, "Teslim edilmemis olmali.");

            // Dogru kodla tekrar dene
            bool right = door.SubmitCodeForTest(p.DeliveryCode);
            yield return null;
            Assert.IsTrue(right, "Dogru kod kabul edilmeli.");
            Assert.IsTrue(p.IsDelivered, "Teslim olmali (#56).");
            Assert.AreEqual(ParcelState.Delivered, p.State, "State = Delivered.");

            // Duplicate: tekrar teslim
            Assert.IsFalse(door.SubmitCodeForTest(p.DeliveryCode),
                "Teslim edilmis kargo TEKRAR teslim EDILMEMELI (N8 / #61).");
        }

        // ==================================================================
        // YARDIMCILAR
        // ==================================================================
        private string _lastArriveError = "";

        /// <summary>
        /// Araci kargonun adresine goturur ve teslim oturumunu hazirlar.
        ///
        /// Oyuncu adimlarinin deterministik karsiligi: arac surulur,
        /// adrese varilir. Oyuncu yolu degistirilmez.
        /// </summary>
        private bool ArriveAtAddressForTest(int addressId)
        {
            _lastArriveError = "";

            var bike = Object.FindFirstObjectByType<Vehicle.BicycleController>();
            if (bike == null) { _lastArriveError = "Bisiklet yok."; return false; }

            var route = _root.Delivery.Route;
            if (route == null || route.Count == 0)
            {
                _lastArriveError = "Rota kurulmadi; kargo aracla tasinmiyor.";
                return false;
            }

            for (int i = 0; i < route.Count; i++)
            {
                if (route[i].DestinationId != addressId) continue;

                bike.transform.position = route[i].Position;
                _root.Delivery.VehiclePosition = route[i].Position;
                _root.Delivery.SetRouteCursorForTests(i);
                _root.Delivery.Tick(1f, false);

                if (_root.Delivery.CurrentStop == null)
                {
                    _lastArriveError = "Durak atanmadi.";
                    continue;
                }
                return true;
            }
            _lastArriveError = "Rotada " + addressId + " adresi yok.";
            return false;
        }

        private static int IdOf(Player.AddressDoor d)
        {
            if (d == null) return -1;
            Transform t = d.transform;
            for (int up = 0; t != null && up < 4; up++, t = t.parent)
            {
                string n = t.name;
                int k = n.LastIndexOf('_');
                if (k < 0) continue;
                if (int.TryParse(n.Substring(k + 1), out int v)) return v;
            }
            return -1;
        }


        private Player.AddressDoor DoorFor(int addressId)
        {
            var doors = Object.FindObjectsByType<Player.AddressDoor>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < doors.Length; i++)
            {
                Transform t = doors[i].transform;
                for (int up = 0; t != null && up < 3; up++, t = t.parent)
                {
                    string n = t.name;
                    int k = n.LastIndexOf('_');
                    if (k >= 0 && int.TryParse(n.Substring(k + 1), out int v) && v == addressId)
                        return doors[i];
                }
            }
            return null;
        }
    }
}
