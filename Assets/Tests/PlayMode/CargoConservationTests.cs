using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using KargoSim.Cargo;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace KargoSim.Tests.PlayMode
{
    /// <summary>
    /// GOREV #26 - KARGO KAYBOLMA KURALI
    /// GOREV #60 - KARGO STATE MACHINE
    ///
    /// Gorev metni:
    ///   "Asagidaki islemlerden hicbirinde kargo sessizce yok olamaz:
    ///    aractan inme, aractan binme, scene reload, delivery basarisizligi,
    ///    wrong code, NPC not home, capacity fail."
    ///
    /// NEDEN SU AN ONEMLI: asama 33'te kutulara `Carried` durumu eklendi
    /// (kurye aractan kutu cekince). Bu YENI bir liste demek; ve bu listedeki
    /// kutularin gun sonu (`UnloadAll`) ve baska sureclerle ne oldugu daha
    /// once HICBIR TEST EDILMEMISTI. Yani benim ekledigim durum, kargo
    /// kaybettiren bir yol olabilirdi.
    ///
    /// Bu dosya HER gecisin kargoyu KORUDUGUNU olcer. Kutu sayisi her
    /// adimda tek tek sayilir.
    /// </summary>
    public class CargoConservationTests
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";

        private Core.GameCompositionRoot _root;
        private Player.CourierController _courier;
        private Player.ShopCustomer[] _customers;
        private Player.VanCargoPoint _bikePoint;
        private Vehicle.BicycleController _bike;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Scene fresh = SceneManager.CreateScene("CargoCons");
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
            Assert.IsNotNull(_root, "CompositionRoot yok.");
            Assert.IsNotNull(_courier, "Kurye yok.");

            var points = Object.FindObjectsByType<Player.VanCargoPoint>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < points.Length; i++)
                if ((points[i].transform.position - _bike.transform.position).sqrMagnitude < 9f)
                    _bikePoint = points[i];
        }

        /// <summary>
        /// SISTEMDEKI TOPLAM KUTU SAYISI.
        ///
        /// Kargo "bir sayi degil, fiziksel nesne" olmali (gorev #89):
        /// depoda + arakta + kuryede olan HER kutu sayilir.
        ///
        /// DIKKAT: kuryenin ELLERI ayrica bir depo DEGILDIR. Kurye eline
        /// aldiginda kutu sistemde ZATEN bir yerdedir:
        ///   - depodan aldiysa  -> `_available`da kalir
        ///   - aractan cektiyse -> `_carried`a gecer
        /// `CarriedParcels` bunlarin BIR REFERANSI; ayri sayilirsa kutu
        /// IKI KEZ sayilir ve test kendi hatasini "kayip" diye okur.
        ///
        /// (Ilk yazimda eller de sayiliyordu ve 12 -> 13 "kayip" uydurdu.)
        /// </summary>
        private int TotalParcels()
        {
            var m = _root.Cargo.Manifest;
            int n = 0;

            var available = m.Available;
            for (int i = 0; i < available.Count; i++) if (available[i] != null) n++;

            var loaded = m.Loaded;
            for (int i = 0; i < loaded.Count; i++) if (loaded[i] != null) n++;

            var carried = m.Carried;
            for (int i = 0; i < carried.Count; i++) if (carried[i] != null) n++;

            return n;
        }

        private void AssertConserved(int before, string step)
        {
            int after = TotalParcels();
            Assert.AreEqual(before, after,
                "KAYIP/KOPYA: '" + step + "' adiminda toplam kutu degisti " +
                before + " -> " + after + ". Gorev #26: kargo hicbir islemde " +
                "sessizce kaybolamaz.");
        }

        private IEnumerator TakeOneBox()
        {
            _courier.transform.position = _customers[0].transform.position + Vector3.forward;
            yield return null; yield return null;
            _courier.TryInteract();
            yield return null;
            Assert.AreEqual(1, _courier.CarriedCount, "Musteriden kutu alinamadi.");
        }

        // ==================================================================
        // 1) Temel gecisler
        // ==================================================================

        [UnityTest]
        public IEnumerator ElindeAlipAracaKoyuncaKutuKaybolmuyor()
        {
            yield return LoadGame();
            _courier.ExitVehicle(Vector3.zero);
            yield return null;

            int before = TotalParcels();
            yield return TakeOneBox();
            AssertConserved(before, "musteriden alma");

            _bikePoint.Interact(_courier);
            yield return null;
            AssertConserved(before, "araca yukleme");
        }

        [UnityTest]
        public IEnumerator AractanElineAlincaKutuKaybolmuyor()
        {
            yield return LoadGame();
            _courier.ExitVehicle(Vector3.zero);
            yield return null;

            yield return TakeOneBox();
            _bikePoint.Interact(_courier);
            yield return null;
            Assert.AreEqual(1, _root.Cargo.Manifest.LoadedCount, "Hazirlik: aracda kutu.");

            int before = TotalParcels();
            _courier.transform.position = _bikePoint.transform.position + Vector3.forward;
            yield return null; yield return null;
            _bikePoint.Interact(_courier);
            yield return null;

            Assert.AreEqual(1, _courier.CarriedCount, "Aractan kutu alinmadi.");
            AssertConserved(before, "araçtan alma");
        }

        [UnityTest]
        public IEnumerator GidisDonusTasimaKutuKaybettirmiyor()
        {
            yield return LoadGame();
            _courier.ExitVehicle(Vector3.zero);
            yield return null;

            yield return TakeOneBox();
            int before = TotalParcels();

            for (int i = 0; i < 4; i++)
            {
                _bikePoint.Interact(_courier); yield return null;   // yukle
                _bikePoint.Interact(_courier); yield return null;   // al
                AssertConserved(before, "gidis-donus " + i);
            }
        }

        // ==================================================================
        // 2) Basarisizlik yollari kargoyu KAYBETMEMELI
        // ==================================================================

        [UnityTest]
        public IEnumerator KapasiteDolarsaKutuEldeKalir()
        {
            yield return LoadGame();
            _courier.ExitVehicle(Vector3.zero);
            yield return null;

            yield return TakeOneBox();
            int before = TotalParcels();

            // Kapasiteyi bilerek kucultur.
            //
            // Once gunden gelen kargoyu araca doldurmayi denedim;
            // gunluk havuz (12 kutu) bisikletin 150 L'sini DOLDURMUYOR,
            // dolayisiyla hicbir red olusmuyor ve onkosul saglanmiyordu.
            // Test amaci kapasite REDDI olduguna gore kurali
            // "doldurulabilir havuz" olmaktan cikarmak dogru.
            var m = _root.Cargo.Manifest;
            var kutu = _courier.CarriedParcels[0];

            m.SetCapacity(new CargoCapacity(kutu.WeightKg * 0.5f, 1f));

            bool yuklendi = _root.Cargo.TryLoadParcel(kutu.Id, 0f);
            yield return null;

            Assert.IsFalse(yuklendi,
                "Kapasite kucultuldu ama kutu yine de yuklendi; kapasite " +
                "kontrolu calismiyor.");
            Assert.AreEqual(1, _courier.CarriedCount,
                "Kapasite reddedilen kutu ELDE KALMALI, kaybolmamali.");
            AssertConserved(before, "kapasite reddi");
        }

        [UnityTest]
        public IEnumerator GUNSONUNDAKargoKaybolmuyor()
        {
            yield return LoadGame();
            _courier.ExitVehicle(Vector3.zero);
            yield return null;

            yield return TakeOneBox();
            _bikePoint.Interact(_courier);
            yield return null;
            Assert.AreEqual(1, _root.Cargo.Manifest.LoadedCount, "Hazirlik: aracda kutu.");

            int before = TotalParcels();
            _root.Cargo.UnloadVehicle();
            yield return null;

            AssertConserved(before, "gun sonu araci bosaltma");
            Assert.AreEqual(0, _root.Cargo.Manifest.LoadedCount,
                "Arac bosalmali.");
        }

        [UnityTest]
        public IEnumerator GunSonuEldekiKutuVarsaKaybolmuyor()
        {
            yield return LoadGame();
            _courier.ExitVehicle(Vector3.zero);
            yield return null;

            // once araca koy, sonra cek (kutu `_carried`a gider),
            // sonra gun sonu bosalt.
            yield return TakeOneBox();
            _bikePoint.Interact(_courier);
            yield return null;

            _courier.transform.position = _bikePoint.transform.position + Vector3.forward;
            yield return null; yield return null;
            _bikePoint.Interact(_courier);
            yield return null;

            Assert.AreEqual(1, _root.Cargo.Manifest.Carried.Count,
                "Hazirlik: kutu kuryede olmali (elde).");

            int before = TotalParcels();
            _root.Cargo.UnloadVehicle();
            yield return null;

            AssertConserved(before, "gun sonu (elde kutu varken)");
        }

        // ==================================================================
        // 3) Kutu benzersizligi
        // ==================================================================

        [UnityTest]
        public IEnumerator SistemdeAyniKutuIkiYerdeSayilmaz()
        {
            yield return LoadGame();
            _courier.ExitVehicle(Vector3.zero);
            yield return null;

            yield return TakeOneBox();
            _bikePoint.Interact(_courier);
            yield return null;
            _bikePoint.Interact(_courier);
            yield return null;

            // Her kimlikte SADECE bir yerde olmali.
            var seen = new HashSet<int>();
            var m = _root.Cargo.Manifest;

            var a = m.Available;
            for (int i = 0; i < a.Count; i++)
                if (a[i] != null) Assert.IsTrue(seen.Add(a[i].Id),
                    "Depoda ayni kutu birden fazla kez var: #" + a[i].Id);

            var l = m.Loaded;
            for (int i = 0; i < l.Count; i++)
                if (l[i] != null) Assert.IsTrue(seen.Add(l[i].Id),
                    "Aracda ayni kutu birden fazla kez var: #" + l[i].Id);

            var c = m.Carried;
            for (int i = 0; i < c.Count; i++)
                if (c[i] != null) Assert.IsTrue(seen.Add(c[i].Id),
                    "Kuryede ayni kutu birden fazla kez var: #" + c[i].Id);
        }
    }
}
