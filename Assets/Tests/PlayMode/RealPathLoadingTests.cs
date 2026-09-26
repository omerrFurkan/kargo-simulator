using System.Collections;
using NUnit.Framework;
using KargoSim.Player;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace KargoSim.Tests.PlayMode
{
    /// <summary>
    /// ASAMA 18 - KUTU ARACA KONMUYOR: GERCEK OYUNCU YOLU.
    ///
    /// Oyuncu: "bisiklete ve araca elimdeki kutuyu yerlestiremiyorum".
    ///
    /// Onceki tum yukleme testleri `_bikePoint.Interact(courier)` cagiryordu
    /// - yani VanCargoPoint'in KENDI kodunu dogrudan, odak/mesafe sistemini
    /// atlayarak. Oyuncu ise E tusuna basiyor ve asil yol su:
    ///
    ///     Input.GetKeyDown(E) -> UIManager -> courier.TryInteract()
    ///        -> ScanForFocus()  <== BURASI HICBIR TESTTE KOSULMAMISTI
    ///        -> odaktaki nesnenin Interact()'i
    ///
    /// Tarama calismiyorsa yukleme calisir gorunur (dogrudan cagriyla)
    /// ama gercek oyunda E hicbir sey yapmaz. Bu dosya o bosluklari olcer.
    /// </summary>
    public class RealPathLoadingTests
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";

        private Core.GameCompositionRoot _root;
        private Player.CourierController _courier;
        private Vehicle.BicycleController _bike;
        private Player.VanCargoPoint _bikePoint;
        private Player.ShopParcelPile _pile;
        private Player.PackingTable _table;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Scene fresh = SceneManager.CreateScene("RealPathCleanup");
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

            _root = Object.FindFirstObjectByType<Core.GameCompositionRoot>();
            _courier = Object.FindFirstObjectByType<Player.CourierController>();
            _bike = Object.FindFirstObjectByType<Vehicle.BicycleController>();
            _pile = Object.FindFirstObjectByType<Player.ShopParcelPile>();
            _table = Object.FindFirstObjectByType<Player.PackingTable>();
            Assert.IsNotNull(_courier, "Kurye yok.");
            Assert.IsNotNull(_bike, "Bisiklet yok.");

            var points = Object.FindObjectsByType<Player.VanCargoPoint>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < points.Length; i++)
                if ((points[i].transform.position - _bike.transform.position).sqrMagnitude < 9f)
                    _bikePoint = points[i];
            Assert.IsNotNull(_bikePoint, "Bisiklet kargo noktasi yok.");
        }

        private IEnumerator PrepareOnePackagedBox()
        {
            _pile.Interact(_courier);
            yield return null;
            Assert.Greater(_courier.CarriedCount, 0, "Raftan kargo alinamadi.");

            for (int b = 0; b < _courier.CarriedCount; b++)
            {
                _courier.transform.position = _table.transform.position
                    + new Vector3(0f, 0.1f, 1.8f);
                yield return null; yield return null;
                int guard = 0;
                while (guard < 40 && !_courier.CarriedParcels[b].IsPackaged)
                {
                    _courier.TryInteract();
                    _root.Delivery.Tick(2.5f, false);
                    guard++;
                    yield return null;
                }
                Assert.IsTrue(_courier.CarriedParcels[b].IsPackaged, "Kutu paketlenemedi.");
            }
        }

        /// <summary>
        /// Kutuyu aracin YANINA kadar göturur ve bakis yonunu araca
        /// cevirir - yani oyuncunun yaptigi gibi.
        /// </summary>
        private IEnumerator StandNextToBike()
        {
            // VAnCargoPoint aracin uzerinde (y~1.64). Karakterin yanina
            // ve ona BAKARAK dur.
            Vector3 side = Vector3.Cross(_bike.transform.up, _bike.transform.forward).normalized;
            Vector3 stand = _bikePoint.transform.position + side * 1.6f;

            // Zemin: arac yere otursun diye biraz bekle, sonra ayak
            // hizasini zemine oturt.
            for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();

            if (Physics.Raycast(stand + Vector3.up * 8f, Vector3.down,
                                 out RaycastHit hit, 40f, ~0, QueryTriggerInteraction.Ignore))
                stand.y = hit.point.y;

            _courier.transform.position = stand;
            _courier.transform.rotation = Quaternion.LookRotation(
                (_bikePoint.transform.position - stand).normalized, Vector3.up);
            yield return null; yield return null;
        }

        // ==================================================================
        // 1. Odak araci yakalaniyor mu
        // ==================================================================

        [UnityTest]
        public IEnumerator KutuTasiyarkenAracOdagaGiriyor()
        {
            yield return LoadGame();
            _courier.ExitVehicle(Vector3.zero);
            yield return null;
            yield return PrepareOnePackagedBox();
            yield return StandNextToBike();

            Assert.Greater(_courier.CarriedCount, 0, "Kutu yok.");
            Assert.Less(Vector3.Distance(_courier.transform.position,
                                         _bikePoint.transform.position), 2.6f,
                "Test kurulumu: kurye aracin 2.6 m icinde olmali.");

            IInteractable focused = _courier.Focus;
            Assert.IsNotNull(focused,
                "Kurye aracin 2.6 m icinde, kutu elinde, ama odakta HICBIR " +
                "SEY yok. Oyuncu E'ye basinca hicbir sey olmuyor - " +
                "'elimdeki kutuyu araca koyamiyorum' raporunun sebebi bu.");

            Assert.AreSame(_bikePoint, focused,
                "Odakta arac kargo noktasi yerine baska bir sey var: " +
                focused.GetType().Name + ". E odakli hedefe gider, o yuzden " +
                "kutu araca konmaz.");
        }

        // ==================================================================
        // 2. GERCEK E yolu: sadece E tusu
        // ==================================================================

        [UnityTest]
        public IEnumerator ETusuyileKutuAracaKonur()
        {
            yield return LoadGame();
            _courier.ExitVehicle(Vector3.zero);
            yield return null;
            yield return PrepareOnePackagedBox();
            yield return StandNextToBike();

            int before = _root.Cargo.Manifest.LoadedCount;
            int inHand = _courier.CarriedCount;
            Assert.Greater(inHand, 0, "Yuklenecek kutu yok.");

            // Oyuncu E tusuna basar. Dogrudan Interact CAGRILMAZ.
            bool used = _courier.TryInteract();
            yield return null;

            Assert.IsTrue(used,
                "TryInteract() false dondu: odakta etkilesilebilir bir sey " +
                "bulunamadi. Oyuncu E'ye basiyor ve kutu araca girmiyor.");
            Assert.AreEqual(before + inHand, _root.Cargo.Manifest.LoadedCount,
                "E ile kutular araca yuklenmedi (yuklendi: " +
                (_root.Cargo.Manifest.LoadedCount - before) + ", elde " + inHand + ").");
            Assert.AreEqual(0, _courier.CarriedCount, "Yuklemeden sonra el bosalmali.");
        }

        [UnityTest]
        public IEnumerator AracaYakinlastikcaOdakKaybolmuyor()
        {
            yield return LoadGame();
            _courier.ExitVehicle(Vector3.zero);
            yield return null;
            yield return PrepareOnePackagedBox();

            // Farkli mesafelerde odak testi: oyuncu araca dogru yuruyerek
            // yakinlasir, odak her an yakalanabilir olmali.
            float[] distances = { 2.2f, 1.8f, 1.4f, 1.0f };
            Vector3 side = Vector3.Cross(_bike.transform.up, _bike.transform.forward).normalized;

            for (int i = 0; i < distances.Length; i++)
            {
                for (int f = 0; f < 10; f++) yield return new WaitForFixedUpdate();

                Vector3 stand = _bikePoint.transform.position + side * distances[i];
                if (Physics.Raycast(stand + Vector3.up * 8f, Vector3.down,
                                     out RaycastHit hit, 40f, ~0, QueryTriggerInteraction.Ignore))
                    stand.y = hit.point.y;

                _courier.transform.position = stand;
                _courier.transform.rotation = Quaternion.LookRotation(
                    (_bikePoint.transform.position - stand).normalized, Vector3.up);
                yield return null; yield return null;

                IInteractable focused = _courier.Focus;
                Assert.IsNotNull(focused,
                    "Mesafe " + distances[i].ToString("F1") +
                    " m: odak kayboldu. Oyuncu araca dogru yururken odak " +
                    "surekli kayboluyorsa yukleme mumkun degil.");
            }
        }

        // ==================================================================
        // 3. Inme yeri: ilk bindigi konum
        // ==================================================================

        [UnityTest]
        public IEnumerator IninceIlkBindigiKonumaDonuyor()
        {
            yield return LoadGame();
            _courier.ExitVehicle(Vector3.zero);
            yield return null;
            yield return StandNextToBike();

            Vector3 boardPosition = _courier.transform.position;
            _bikePoint.Board(_courier);
            yield return null;
            Assert.IsTrue(_courier.IsInVehicle, "Binmeli.");

            _courier.ExitVehicle(_bikePoint.ExitSpot());
            yield return null;
            yield return null;

            float d = Vector3.Distance(_courier.transform.position, boardPosition);
            Assert.Less(d, 1.0f,
                "Oyuncu " + boardPosition.ToString("F1") + " konumunda bindi, " +
                "inince " + _courier.transform.position.ToString("F1") +
                " konumunda olustu (fark " + d.ToString("F2") + " m). " +
                "Oyuncu 'inince ilk bindigim konumda olusuyorum' diyor.");
        }
    }
}
