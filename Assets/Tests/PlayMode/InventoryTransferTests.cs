using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace KargoSim.Tests.PlayMode
{
    /// <summary>
    /// ENVANTER: karakter eli <-> arac bagaji (gorev #26, #89, #90).
    ///
    /// Oyuncu: "kendi envanterimizden aracin bagajindan kutu alip
    /// yerlestirebilelim, gittigimiz yere gore kargolarimizi gorelim".
    ///
    /// ONCEKI BASARISIZ DENEMENIN DERSI: o zaman once kutulara
    /// `Carried` durumu eklendiydi ama TESLIMAT SISTEMI degistirilmemisti;
    /// rota sadece `Manifest.Loaded`dan kuruldugu icin kutu eline
    /// gecince durak kayboluyordu ve 7 test kirildi. Simdi temel
    /// (asama 33) teslim edildi ve rota "arac + elde"den kuruluyor.
    /// </summary>
    public class InventoryTransferTests
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";

        private Core.GameCompositionRoot _root;
        private Player.CourierController _courier;
        private UI.CargoInventoryView _inventory;
        private Player.ShopCustomer[] _customers;
        private Player.VanCargoPoint _bikePoint;
        private Vehicle.BicycleController _bike;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Scene fresh = SceneManager.CreateScene("InvCleanup");
            SceneManager.SetActiveScene(fresh);
            yield return null;
            Scene game = SceneManager.GetSceneByName("Game");
            if (game.IsValid() && game.isLoaded)
            {
                AsyncOperation unload = SceneManager.UnloadSceneAsync(game);
                while (!unload.isDone) yield return null;
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
            _bike = Object.FindFirstObjectByType<Vehicle.BicycleController>();
            _customers = Object.FindObjectsByType<Player.ShopCustomer>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.IsNotNull(_courier, "Kurye yok.");

            // Panel I tusu ile acildigi icin baslangicta GIZLI; arama
            // pasif nesneleri de kapsamali.
            var views = Object.FindObjectsByType<UI.CargoInventoryView>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (views.Length > 0) _inventory = views[0];
            Assert.IsNotNull(_inventory, "Envanter paneli kurulmamis.");

            var points = Object.FindObjectsByType<Player.VanCargoPoint>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < points.Length; i++)
                if ((points[i].transform.position - _bike.transform.position).sqrMagnitude < 9f)
                    _bikePoint = points[i];
        }

        private IEnumerator TakeBoxFromCustomer()
        {
            _courier.transform.position = _customers[0].transform.position + Vector3.forward;
            yield return null; yield return null;
            _courier.TryInteract();
            yield return null;
            Assert.AreEqual(1, _courier.CarriedCount, "Musteriden kutu alinamadi.");
        }

        // ==================================================================
        // 1) Panel acilir
        // ==================================================================

        [UnityTest]
        public IEnumerator EnvanterITusuylaAcilipKapaniyor()
        {
            yield return LoadGame();
            _courier.ExitVehicle(Vector3.zero);
            yield return null;

            Assert.IsFalse(_inventory.IsOpen, "Envanter baslangicta kapali olmali.");
            _inventory.Toggle();
            Assert.IsTrue(_inventory.IsOpen, "I tusu ile acilmadi.");
            _inventory.Toggle();
            Assert.IsFalse(_inventory.IsOpen, "I tusu ile kapanmadi.");
        }

        // ==================================================================
        // 2) Iki yonlu transfer
        // ==================================================================

        [UnityTest]
        public IEnumerator EldenAracaKutuKonuluyor()
        {
            yield return LoadGame();
            _courier.ExitVehicle(Vector3.zero);
            yield return null;
            _inventory.Toggle();

            yield return TakeBoxFromCustomer();
            Assert.AreEqual(0, _root.Cargo.Manifest.LoadedCount, "Arac bos olmali.");

            Assert.IsTrue(_inventory.PutIntoVehicle(1), "[1] kutu araca koymadi.");
            yield return null;

            Assert.AreEqual(1, _root.Cargo.Manifest.LoadedCount, "Kutu araca girmedi.");
            Assert.AreEqual(0, _courier.CarriedCount, "El bosalmali.");
        }

        [UnityTest]
        public IEnumerator AractanEldenKutuAliniyor()
        {
            yield return LoadGame();
            _courier.ExitVehicle(Vector3.zero);
            yield return null;
            _inventory.Toggle();

            yield return TakeBoxFromCustomer();
            _inventory.PutIntoVehicle(1);
            yield return null;
            Assert.AreEqual(1, _root.Cargo.Manifest.LoadedCount, "Hazirlik: aracda kutu.");

            Assert.IsTrue(_inventory.TakeFromVehicle(1), "[4] kutu eline almadi.");
            yield return null;

            Assert.AreEqual(1, _courier.CarriedCount, "Kutu ele gecmedi.");
            Assert.AreEqual(0, _root.Cargo.Manifest.LoadedCount,
                "Kutu hem elde hem arakta sayiliyor.");
        }

        [UnityTest]
        public IEnumerator KutuGidisDonusTasinabiliyor()
        {
            yield return LoadGame();
            _courier.ExitVehicle(Vector3.zero);
            yield return null;
            _inventory.Toggle();

            yield return TakeBoxFromCustomer();

            for (int i = 0; i < 4; i++)
            {
                Assert.IsTrue(_inventory.PutIntoVehicle(1), "gidis " + i + " basarisiz");
                yield return null;
                Assert.AreEqual(1, _root.Cargo.Manifest.LoadedCount, "Aracda kutu olmali.");

                Assert.IsTrue(_inventory.TakeFromVehicle(1), "donus " + i + " basarisiz");
                yield return null;
                Assert.AreEqual(1, _courier.CarriedCount, "Elde kutu olmali.");
            }
        }

        [UnityTest]
        public IEnumerator SlotTuslariIkiYonluCalisiyor()
        {
            yield return LoadGame();
            _courier.ExitVehicle(Vector3.zero);
            yield return null;
            _inventory.Toggle();

            yield return TakeBoxFromCustomer();

            // 1-3 elden araca
            Assert.IsTrue(_inventory.UseSlot(1), "[1] elden araca calismadi.");
            yield return null;
            Assert.AreEqual(1, _root.Cargo.Manifest.LoadedCount, "Kutu aracda olmali.");

            // 4-6 aractan ele
            Assert.IsTrue(_inventory.UseSlot(4), "[4] aractan ele calismadi.");
            yield return null;
            Assert.AreEqual(1, _courier.CarriedCount, "Kutu elde olmali.");
        }

        // ==================================================================
        // 3) Bos slot ve hata durumlari
        // ==================================================================

        [UnityTest]
        public IEnumerator BosSlotTasimaYapmaz()
        {
            yield return LoadGame();
            _courier.ExitVehicle(Vector3.zero);
            yield return null;
            _inventory.Toggle();

            Assert.IsFalse(_inventory.PutIntoVehicle(1), "Bos elde [1] basarili olmamali.");
            Assert.IsFalse(_inventory.TakeFromVehicle(1), "Bos arac [4] basarili olmamali.");
        }

        [UnityTest]
        public IEnumerator KapasiteDoluncaKutuEldeKalir()
        {
            yield return LoadGame();
            _courier.ExitVehicle(Vector3.zero);
            yield return null;
            _inventory.Toggle();

            yield return TakeBoxFromCustomer();
            var kutu = _courier.CarriedParcels[0];

            _root.Cargo.Manifest.SetCapacity(new Cargo.CargoCapacity(kutu.WeightKg * 0.5f, 1f));

            Assert.IsFalse(_inventory.PutIntoVehicle(1),
                "Kapasite doluyken yukleme basarili olmamali.");
            yield return null;
            Assert.AreEqual(1, _courier.CarriedCount,
                "Kapasite reddedilen kutu ELDE KALMALI - kaybolmamali.");
            StringAssert.Contains("Kapasite", _inventory.LastMessage,
                "Oyuncuya kapasite sebebi yazilmalidir (gorev #51).");
        }

        [UnityTest]
        public IEnumerator PanelKapaliykenSlotCalismaz()
        {
            yield return LoadGame();
            _courier.ExitVehicle(Vector3.zero);
            yield return null;

            yield return TakeBoxFromCustomer();
            // Panel KAPALI: tuslar oyunu degistirmemeli.
            Assert.IsFalse(_inventory.UseSlot(1),
                "Panel kapaliyken [1] kutu araca koydu. Panel acikken "
                + "calismalidir (gorev #29).");
            Assert.AreEqual(1, _courier.CarriedCount, "Kutu elde kalmali.");
        }

        // ==================================================================
        // 4) Teslimat zinciri bozulmadi
        // ==================================================================

        [UnityTest]
        public IEnumerator EnvanterTeslimatZinciriniBozmuyor()
        {
            yield return LoadGame();
            _courier.ExitVehicle(Vector3.zero);
            yield return null;
            _inventory.Toggle();

            yield return TakeBoxFromCustomer();
            _inventory.PutIntoVehicle(1);
            yield return null;
            _inventory.TakeFromVehicle(1);
            yield return null;

            // Kutu ELDE ama rota duragi olmali degil (araca konmadi),
            // ancak teslimat zinciri cokmemeli. Kutu tekrar aracayla
            // yuklenip adrese gidilebiliyor mu diye bakiliyor.
            _inventory.PutIntoVehicle(1);
            yield return null;

            Assert.AreEqual(1, _root.Cargo.Manifest.LoadedCount,
                "Kutu yeniden araca konmadi; iki yonlu transfer teslimat zincirini "
                + "bozdu.");

            _root.Delivery.Tick(0.5f, false);
            yield return null;

            // Durak arac ADRESE VARDIGINDA atanir; sadece Tick yeterli
            // degil. Onceki yazimda konum verilmedigi icin CurrentStop
            // null kaldi ve test "zincir bozuldu" sanildi.
            var route = _root.Delivery.Route;
            Assert.Greater(route.Count, 0,
                "Rota hic kurulmadi; kutu aracda olmaliydi.");

            _bike.transform.position = route[0].Position;
            _root.Delivery.VehiclePosition = route[0].Position;
            _root.Delivery.Tick(0.5f, false);
            yield return null;

            Assert.IsNotNull(_root.Delivery.CurrentStop,
                "Adrese varildi ama durak atanmadi. Envanter degisikligi "
                + "teslimat zincirini bozdu.");
        }
    }
}
