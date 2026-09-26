using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace KargoSim.Tests.PlayMode
{
    /// <summary>
    /// ASAMA 15 - Dikey dilimin EN ONEMLI ADIMI: kutuyu masaya BIRAKIP
    /// yerinde paketlemek, sonra tekrar almak.
    ///
    /// Neden ayri test: bu adim once HIC YOKTU.
    /// <c>CourierController.DropOne</c> kutuyu yere koymaz, gorseli YOK
    /// EDERDI. Oyuncu "kutuyu masaya gotur ve buraya birak" dediginde
    /// kutu kayboluyor ve paketleme baslamiyordu. Yani oyun basindan
    /// sonuna kadar oynanamazdi.
    ///
    /// Bu test akisin TAMAMINI oyuncu yoluyla surer:
    ///   raftan tek kutu al -> masaya git -> BIRAK -> yerinde paketle
    ///   -> tekrar al -> bisiklete yukle
    /// </summary>
    public class GroundParcelDropTests
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";

        private Core.GameCompositionRoot _root;
        private Player.CourierController _courier;
        private Player.PackingTable _table;
        private Player.ShopParcelPile _pile;
        private Player.VanCargoPoint _bikePoint;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Scene fresh = SceneManager.CreateScene("DropCleanup");
            SceneManager.SetActiveScene(fresh);
            yield return null;

            Scene game = SceneManager.GetSceneByName("Game");
            if (game.IsValid() && game.isLoaded)
            {
                AsyncOperation unload = SceneManager.UnloadSceneAsync(game);
                while (!unload.isDone) yield return null;
            }

            Core.GameEvents.ResetForTests();
            yield return null;
        }

        private IEnumerator LoadGame()
        {
            SceneManager.LoadScene(ScenePath, LoadSceneMode.Single);
            yield return null;
            yield return null;
            yield return null;

            _root = Object.FindFirstObjectByType<Core.GameCompositionRoot>();
            _courier = Object.FindFirstObjectByType<Player.CourierController>();
            _table = Object.FindFirstObjectByType<Player.PackingTable>();
            _pile = Object.FindFirstObjectByType<Player.ShopParcelPile>();

            Assert.IsNotNull(_root, "CompositionRoot yok.");
            Assert.IsNotNull(_courier, "Kurye yok.");
            Assert.IsNotNull(_table, "Paketleme masasi yok.");
            Assert.IsNotNull(_pile, "Rafta yok.");

            var bike = Object.FindFirstObjectByType<Vehicle.BicycleController>();
            Assert.IsNotNull(bike, "Kargo bisikleti yok.");

            // Kargo noktasi bisikletin COCUGU olabilir; GetComponent
            // yalnizca ayni nesne uzerinde arar.
            _bikePoint = bike.GetComponentInChildren<Player.VanCargoPoint>(true);
            Assert.IsNotNull(_bikePoint,
                "Bisikletin kargo noktasi yok; kargo yuklenemez.");
        }

        // ------------------------------------------------------------------
        // 1. Raf TEK kutu veriyor (iki eli doldurmuyor)
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator RafTekEBasindaTekKutuVeriyor()
        {
            yield return LoadGame();

            _pile.Interact(_courier);
            yield return null;

            Assert.AreEqual(1, _courier.CarriedCount,
                "Raf bir E'de TEK kutu vermeli. Iki eli birden dolduruyordu "
                + "ve oyuncu kutuyu istedigi yere goturemiyordu.");

            // Ikinci E ikinci kutuyu verir.
            _pile.Interact(_courier);
            yield return null;
            Assert.AreEqual(2, _courier.CarriedCount,
                "Ikinci E ikinci kutuyu vermeli.");
        }

        // ------------------------------------------------------------------
        // 2. BIRAK: kutu yerde durur, kaybolmaz
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator BirakilanKutuDunyadaDuruyor()
        {
            yield return LoadGame();

            _pile.Interact(_courier);
            yield return null;
            Assert.AreEqual(1, _courier.CarriedCount, "Once kutu alinmali.");

            var parcel = _courier.CarriedParcels[0];

            Cargo.Parcel dropped =
                _courier.DropTopToWorld(_courier.transform.position);

            Assert.IsNotNull(dropped, "Birakma null donmemeli.");
            Assert.AreEqual(0, _courier.CarriedCount, "Elde kargo kalmamali.");
            Assert.IsNotNull(parcel, "Birakilan kargo kaybolmamali.");

            yield return null;

            var inWorld = Object.FindObjectsByType<Player.GroundParcel>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            Assert.AreEqual(1, inWorld.Length,
                "Birakilan kutu dunyada bir nesne olarak durmali. Önce "
                + "gOrseli yok ediliyordu ve kutu KAYBOLUYORDU.");
            Assert.AreEqual(parcel.Id, inWorld[0].ParcelId,
                "Yerdeki kutu, birakilan kargo olmali.");
        }

        // ------------------------------------------------------------------
        // 3. Yerinde paketleme: masadaki kutu paketlenir
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator MasadakiKutuYerindePaketleniyor()
        {
            yield return LoadGame();

            _pile.Interact(_courier);
            yield return null;
            var parcel = _courier.CarriedParcels[0];

            // Kutuyu MASANIN UZERINE birak.
            //
            // Kuryenin YONU belirlenmeli: birakma noktasi kuryenin ONUNDE
            // hesaplanir, yonu donuk birakirsak kutu masanin disina duser
            // ve test rastgele basar/basaramaz.
            _courier.transform.position = _table.transform.position
                + new Vector3(0f, 0.1f, 1.2f);
            _courier.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            yield return null;

            _courier.DropTopToWorld(_courier.transform.position, 1.0f);
            yield return null;
            yield return null;

            Assert.AreEqual(0, _courier.CarriedCount, "Kutu birakildi, el bos.");

            // Sira degistirmeden masanin basina don.
            _courier.transform.position = _table.transform.position
                + new Vector3(0f, 0.1f, 1.8f);
            _courier.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            yield return null;
            yield return null;

            int guard = 0;
            while (guard < 40 && parcel != null && !parcel.IsPackaged)
            {
                _table.Interact(_courier);
                _root.Delivery.Tick(2.5f, false);
                guard++;
                yield return null;
            }

            Assert.IsTrue(parcel != null && parcel.IsPackaged,
                "Masanin uzerindeki kutu YERINDE paketlenmeli. Önce masa "
                + "sadece elde tutulan kutuyu kabul ediyordu ve oyuncu kutuyu "
                + "geri almak zorunda kaliyordu.");
        }

        // ------------------------------------------------------------------
        // 4. Dikey dilim: al -> birak -> paketle -> tekrar al -> yukle
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TamDikeyDilim_BirakPaketleTekrarAlYukle()
        {
            yield return LoadGame();

            // --- 1) raftan kutu al
            _pile.Interact(_courier);
            yield return null;
            var parcel = _courier.CarriedParcels[0];
            Assert.IsNotNull(parcel, "Kutu alinamadi.");

            // --- 2) masaya git ve BIRAK (yon onemli: kutu masanin
            //         uzerine dussun)
            _courier.transform.position = _table.transform.position
                + new Vector3(0f, 0.1f, 1.2f);
            _courier.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            yield return null;
            _courier.DropTopToWorld(_courier.transform.position, 1.0f);
            yield return null;

            // --- 3) yerinde paketle
            _courier.transform.position = _table.transform.position
                + new Vector3(0f, 0.1f, 1.8f);
            _courier.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            yield return null;
            yield return null;

            int guard = 0;
            while (guard < 40 && !parcel.IsPackaged)
            {
                _table.Interact(_courier);
                _root.Delivery.Tick(2.5f, false);
                guard++;
                yield return null;
            }
            Assert.IsTrue(parcel.IsPackaged, "Paketleme tamamlanmadi.");

            // --- 4) paketi TEKRAR AL (yerden)
            var ground = FindGroundParcelFor(parcel);
            Assert.IsNotNull(ground,
                "Paketlenmis kutu yerde durmali; oyuncu onu almali.");
            Assert.IsTrue(ground.CanInteract(_courier),
                "Paketlenmis kutu masanin ustundeyse alinamaz "
                + "(masa aittir).");

            // Kutuyu masadan uzaklastir, sonra al.
            ground.transform.position += new Vector3(0f, 0f, 2.5f);
            yield return null;
            Assert.IsTrue(ground.CanInteract(_courier),
                "Masanin disindaki paket alinabilmeli.");

            _courier.transform.position =
                ground.transform.position + new Vector3(0f, 0.2f, 1.2f);
            yield return null;
            yield return null;

            ground.Interact(_courier);
            yield return null;

            Assert.AreEqual(1, _courier.CarriedCount,
                "Paket yerden tekrar alinabilmeli.");
            Assert.AreSame(parcel, _courier.CarriedParcels[0],
                "Alinan paket ayni kargo olmali.");

            // --- 5) bisiklete yukle
            int before = _root.Cargo.Manifest.LoadedCount;
            _bikePoint.Interact(_courier);
            yield return null;

            Assert.AreEqual(before + 1, _root.Cargo.Manifest.LoadedCount,
                "Paket bisiklete yuklenmeli.");
        }

        private Player.GroundParcel FindGroundParcelFor(Cargo.Parcel parcel)
        {
            var all = Object.FindObjectsByType<Player.GroundParcel>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].ParcelId == parcel.Id) return all[i];
            return null;
        }
    }
}
