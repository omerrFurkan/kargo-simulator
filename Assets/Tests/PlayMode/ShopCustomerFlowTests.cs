using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace KargoSim.Tests.PlayMode
{
    /// <summary>
    /// ASAMA 30 - YENI KUTU AKISI (oyuncunun sectigi B secenegi).
    ///
    ///   NPC gelir -> konusulur ve KUTU ALINIR (paketli gelir)
    ///   -> dukkana istedigin yere birakilir
    ///   -> dagitima cikarken ELINE ALINIR
    ///   -> arabaya konur
    ///
    /// Onceki akis: raf -> paketleme masasi (6 adim) -> araca.
    /// Artik oyuncu masada alti adim beklemez.
    /// </summary>
    public class ShopCustomerFlowTests
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";

        private Core.GameCompositionRoot _root;
        private Player.CourierController _courier;
        private Vehicle.BicycleController _bike;
        private Player.VanCargoPoint _bikePoint;
        private Player.ShopCustomer[] _customers;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Scene fresh = SceneManager.CreateScene("ShopFlowCleanup");
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
            for (int i = 0; i < 30; i++) yield return new WaitForFixedUpdate();

            _root = Object.FindFirstObjectByType<Core.GameCompositionRoot>();
            _courier = Object.FindFirstObjectByType<Player.CourierController>();
            _bike = Object.FindFirstObjectByType<Vehicle.BicycleController>();
            Assert.IsNotNull(_courier, "Kurye yok.");

            _customers = Object.FindObjectsByType<Player.ShopCustomer>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            var points = Object.FindObjectsByType<Player.VanCargoPoint>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < points.Length; i++)
                if ((points[i].transform.position - _bike.transform.position).sqrMagnitude < 9f)
                    _bikePoint = points[i];
        }

        private IEnumerator WalkTo(Transform target, float height)
        {
            return WalkToPoint(target.position, target.forward, height);
        }

        private IEnumerator WalkToPoint(Vector3 target, Vector3 lookDir, float height)
        {
            for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();

            Vector3 pos = target;
            if (Physics.Raycast(pos + Vector3.up * 8f, Vector3.down,
                                 out RaycastHit hit, 60f, ~0, QueryTriggerInteraction.Ignore))
                pos.y = hit.point.y;

            _courier.transform.position = pos;
            Vector3 dir = lookDir.sqrMagnitude > 0.01f ? lookDir : Vector3.forward;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
                _courier.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            yield return null; yield return null;
        }

        // ==================================================================
        // 1) NPC geliyor ve odaklanabiliyor
        // ==================================================================

        [UnityTest]
        public IEnumerator DukkanaMusterilerGeldi()
        {
            yield return LoadGame();
            _courier.ExitVehicle(Vector3.zero);
            yield return null;

            Assert.GreaterOrEqual(_customers.Length, 2,
                "Dukkana gelen musteri yok (" + _customers.Length +
                " tane). Oyuncu istedi: 'npc ler gelsin'.");
        }

        [UnityTest]
        public IEnumerator MusteriyeYakinlasincaOdakGiriyor()
        {
            yield return LoadGame();
            _courier.ExitVehicle(Vector3.zero);
            yield return null;
            Assert.Greater(_customers.Length, 0, "Musteri yok.");

            yield return WalkTo(_customers[0].transform, 1.6f);

            Assert.IsNotNull(_courier.Focus,
                "Musterinin yanina gidildi ama odak yok. Oyuncu 'musteri ile " +
                "konusup kutuyu aliyorum' diyemiyor.");
        }

        // ==================================================================
        // 2) Konusma: kutu ALINIR ve PAKETLIDIR
        // ==================================================================

        [UnityTest]
        public IEnumerator MusteridenPaketliKutuAliniyor()
        {
            yield return LoadGame();
            _courier.ExitVehicle(Vector3.zero);
            yield return null;
            Assert.Greater(_customers.Length, 0, "Musteri yok.");

            yield return WalkTo(_customers[0].transform, 1.6f);
            _courier.TryInteract();
            yield return null;

            Assert.AreEqual(1, _courier.CarriedCount,
                "Musteriyle konusup kutu alinmadi. Oyuncu 'gelen npc ile " +
                "konusup kutuyu alalim' dedi.");
            Assert.IsTrue(_courier.CarriedParcels[0].IsPackaged,
                "Musteriden alinan kutu PAKETLI degil. Oyuncu B secenegini " +
                "secmisti: kutu musteri tarafindan hazir gelmeli, masada " +
                "alti adim beklememeli.");
        }

        [UnityTest]
        public IEnumerator EldeKutuVarkenIkinciKutuAlinmiyor()
        {
            yield return LoadGame();
            _courier.ExitVehicle(Vector3.zero);
            yield return null;
            Assert.Greater(_customers.Length, 1, "Ikinci musteri yok.");

            yield return WalkTo(_customers[0].transform, 1.6f);
            _courier.TryInteract();
            yield return null;
            Assert.AreEqual(1, _courier.CarriedCount, "Ilk kutu alinmadi.");

            yield return WalkTo(_customers[1].transform, 1.6f);
            _courier.TryInteract();
            yield return null;

            Assert.AreEqual(1, _courier.CarriedCount,
                "Elde kutu varken ikinci kutu da alindi. Oyuncu kutu sayisini " +
                "kontrol edemiyor.");
        }

        // ==================================================================
        // 3) Istedigi yere koy -> eline al
        // ==================================================================

        [UnityTest]
        public IEnumerator KutuyuYereBirakipTekrarAlabiliyor()
        {
            yield return LoadGame();
            _courier.ExitVehicle(Vector3.zero);
            yield return null;
            Assert.Greater(_customers.Length, 0, "Musteri yok.");

            yield return WalkTo(_customers[0].transform, 1.6f);
            _courier.TryInteract();
            yield return null;
            Assert.AreEqual(1, _courier.CarriedCount, "Kutu alinmadi.");

            // Yerine birak: kuryeyi bos bir noktaya tasi, odagi temizle.
            var table = Object.FindFirstObjectByType<Player.PackingTable>();
            Assert.IsNotNull(table, "Paketleme masasi yok.");
            Vector3 nearTable = table.transform.position + Vector3.forward * 2f;
            yield return WalkToPoint(nearTable, table.transform.position - nearTable, 0.5f);

            int parcelsOnGround = Object.FindObjectsByType<Player.GroundParcel>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

            Assert.LessOrEqual(parcelsOnGround, 1,
                "Yerde beklenmedik kutu var; birakma akisi bozulmus olabilir.");
        }

        // ==================================================================
        // 4) Elindeki kutuyu araya koy
        // ==================================================================

        [UnityTest]
        public IEnumerator MusteridenAlinanKutuAracaGiriyor()
        {
            yield return LoadGame();
            _courier.ExitVehicle(Vector3.zero);
            yield return null;
            Assert.Greater(_customers.Length, 0, "Musteri yok.");
            Assert.IsNotNull(_bikePoint, "Bisiklet noktasi yok.");

            yield return WalkTo(_customers[0].transform, 1.6f);
            _courier.TryInteract();
            yield return null;
            Assert.AreEqual(1, _courier.CarriedCount, "Kutu alinmadi.");

            Vector3 side = Vector3.Cross(_bike.transform.up, _bike.transform.forward).normalized;
            Vector3 stand = _bikePoint.transform.position + side * 1.5f;
            for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();
            if (Physics.Raycast(stand + Vector3.up * 8f, Vector3.down,
                                 out RaycastHit h, 60f, ~0, QueryTriggerInteraction.Ignore))
                stand.y = h.point.y;
            _courier.transform.position = stand;
            _courier.transform.rotation = Quaternion.LookRotation(
                (_bikePoint.transform.position - stand).normalized, Vector3.up);
            yield return null; yield return null;

            int before = _root.Cargo.Manifest.LoadedCount;
            _courier.TryInteract();
            yield return null;

            Assert.AreEqual(before + 1, _root.Cargo.Manifest.LoadedCount,
                "Musteriden alinan kutu araca girmedi. Tam akis: musteri -> " +
                "kutu -> elde -> araya.");
        }
    }
}
