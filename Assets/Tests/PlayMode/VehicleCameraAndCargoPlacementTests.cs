using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace KargoSim.Tests.PlayMode
{
    /// <summary>
    /// ASAMA 16 - OYUNCUNUN RAPOR ETTIGI UC GERCEK HATA.
    ///
    /// Oyuncu sunlari yazdi:
    ///   1) "kamera ... binince sabit"      -> kamera araci takip etmiyor
    ///   2) "kargo merkezinde kaliyor"      -> kutular arac arkasinda degil
    ///   3) "kutular aracin arkasina yerlestirilmiyor"
    ///
    /// Onceki testler (CargoBikePlayModeTests) bu ucunu DEGILDI:
    /// kamera HANGI kameranin acik oldugunu hic olcmedi, sadece "bir
    /// kamera var mi" diye bakti. Bu dosya olcumu yapiyor.
    ///
    /// KAPSAM: kamera gecisi ve kutu KONUMU olculur. Gorsel kalite,
    /// kadro hizasi ve "yamuluyor" hissi otomasyonla olculemez.
    /// </summary>
    public class VehicleCameraAndCargoPlacementTests
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";

        private Core.GameCompositionRoot _root;
        private Player.CourierController _courier;
        private Vehicle.BicycleController _bike;
        private Player.VanCargoPoint _bikePoint;
        private Player.VanCargoPoint _vanPoint;
        private Player.ShopParcelPile _pile;
        private Player.PackingTable _table;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Scene fresh = SceneManager.CreateScene("CamCleanup");
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
            yield return null;
            yield return null;
            yield return null;

            _root = Object.FindFirstObjectByType<Core.GameCompositionRoot>();
            _courier = Object.FindFirstObjectByType<Player.CourierController>();
            _bike = Object.FindFirstObjectByType<Vehicle.BicycleController>();
            _pile = Object.FindFirstObjectByType<Player.ShopParcelPile>();
            _table = Object.FindFirstObjectByType<Player.PackingTable>();

            Assert.IsNotNull(_root, "CompositionRoot yok.");
            Assert.IsNotNull(_courier, "Kurye yok.");
            Assert.IsNotNull(_bike, "Kargo bisikleti yok.");
            Assert.IsNotNull(_pile, "Dukkan rafi yok.");
            Assert.IsNotNull(_table, "Paketleme masasi yok.");

            var points = Object.FindObjectsByType<Player.VanCargoPoint>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.GreaterOrEqual(points.Length, 2,
                "Iki arac = iki kargo noktasi olmali.");

            for (int i = 0; i < points.Length; i++)
            {
                if ((points[i].transform.position - _bike.transform.position).sqrMagnitude < 9f)
                    _bikePoint = points[i];
                else if (_vanPoint == null) _vanPoint = points[i];
            }

            Assert.IsNotNull(_bikePoint, "Bisikletin kargo noktasi yok.");
            Assert.IsNotNull(_vanPoint, "Vanin kargo noktasi yok.");
        }

        private Camera[] AllCameras()
        {
            return Object.FindObjectsByType<Camera>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        private static Camera BikeCameraOf(Vehicle.BicycleController bike)
        {
            return bike.GetComponentInChildren<Camera>(true);
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
                yield return null;
                yield return null;

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

        // ==================================================================
        // 1. "Kamera binince sabit" - ACIK KAMERA SAYISI
        // ==================================================================

        [UnityTest]
        public IEnumerator AyaktaykenSadeceOyunKamerasiAcik()
        {
            yield return LoadGame();
            _courier.ExitVehicle(Vector3.zero);
            yield return null;
            yield return null;

            int open = 0;
            Camera[] cams = AllCameras();
            for (int i = 0; i < cams.Length; i++)
                if (cams[i].enabled) open++;

            Assert.AreEqual(1, open,
                "Ayaktayken " + open + " kamera acik. Tek kamera olmali: " +
                "ikisi birden acilirsa hangi goruntulendigini Unity belirler " +
                "ve oyuncu kamerasiz kalir.\n" + Envanter(_courier, cams));
        }

        /// <summary>Hangi kameranin acik oldugunu insan okunur listeler.</summary>
        private static string Envanter(Player.CourierController courier, Camera[] cams)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("KAMERA ENVANTERI: toplam ").Append(cams.Length).Append('\n');
            for (int i = 0; i < cams.Length; i++)
            {
                Camera c = cams[i];
                sb.Append("  - ").Append(c.name)
                  .Append(" | tag=").Append(c.tag)
                  .Append(" | enabled=").Append(c.enabled)
                  .Append(" | follow=").Append(c.GetComponent<Player.CameraFollow>() != null);
                if (courier != null)
                {
                    var fps = courier.GetType()
                        .GetField("fpsCamera", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        ?.GetValue(courier) as Camera;
                    var veh = courier.GetType()
                        .GetField("vehicleCamera", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        ?.GetValue(courier) as Camera;
                    if (c == fps) sb.Append("  <== courier.fpsCamera");
                    if (c == veh) sb.Append("  <== courier.vehicleCamera");
                }
                sb.Append('\n');
            }
            return sb.ToString();
        }

        [UnityTest]
        public IEnumerator BisikleteBininceYalnizcaBisikletKamerasiAcik()
        {
            yield return LoadGame();
            _courier.ExitVehicle(Vector3.zero);
            yield return null;
            _bikePoint.Board(_courier);
            yield return null;
            yield return null;

            Camera bikeCam = BikeCameraOf(_bike);
            Assert.IsNotNull(bikeCam, "Bisiklet kamerasi yok.");

            Assert.IsTrue(bikeCam.enabled,
                "Bisiklete binince bisiklet kamerasi ACILMALI. Kapali kalirsa " +
                "oyuncu binince kamera SABIT kalir - oyuncunun raporu bu.");

            Camera[] cams = AllCameras();
            for (int i = 0; i < cams.Length; i++)
            {
                if (cams[i] == bikeCam) continue;
                Assert.IsFalse(cams[i].enabled,
                    "Bisiklete binince BISIKLET disi bir kamera da acik kaldi: " +
                    cams[i].name + ". Iki kamera ayni anda acilirsa oyuncu " +
                    "vanin arkasindan bakar ve 'kamera sabit' sanar.\n"
                    + Envanter(_courier, cams));
            }
        }

        [UnityTest]
        public IEnumerator VanabincaYalnizcaVanKamerasiAcik()
        {
            yield return LoadGame();
            _courier.ExitVehicle(Vector3.zero);
            yield return null;

            _bikePoint.Board(_courier);
            yield return null;
            _courier.ExitVehicle(_bikePoint.ExitSpot());
            yield return null;
            _vanPoint.Board(_courier);
            yield return null;
            yield return null;

            Camera bikeCam = BikeCameraOf(_bike);
            Assert.IsFalse(bikeCam.enabled,
                "Vana binince bisiklet kamerasi KAPANMALI. Acik kalirsa iki " +
                "kamera ayni anda render olur.");

            int open = 0;
            Camera[] cams = AllCameras();
            for (int i = 0; i < cams.Length; i++)
                if (cams[i].enabled) open++;

            Assert.AreEqual(1, open,
                "Vana binince " + open + " kamera acik; tam 1 olmali.");
        }

        // ==================================================================
        // 2. "Kamera sabit" - KAMERA ARACI TAKIP EDIYOR MU
        // ==================================================================

        [UnityTest]
        public IEnumerator BisikletHareketEttikceKameraOnunlaHareketEdiyor()
        {
            yield return LoadGame();
            _courier.ExitVehicle(Vector3.zero);
            yield return null;
            _bikePoint.Board(_courier);
            yield return null;
            yield return null;

            Camera bikeCam = BikeCameraOf(_bike);
            Vector3 camBefore = bikeCam.transform.position;
            Vector3 bikeBefore = _bike.transform.position;

            // Bisikleti elle 12 m ilerlet (fizik yerine dogrudan konum:
            // testin konusu kameranin araci takip ETMESI, fizik degil).
            _bike.transform.position = bikeBefore + new Vector3(0f, 0f, 12f);
            yield return null;
            yield return null;
            yield return null;

            float camMoved = Vector3.Distance(camBefore, bikeCam.transform.position);
            float bikeMoved = Vector3.Distance(bikeBefore, _bike.transform.position);

            Assert.Greater(bikeMoved, 10f, "Test kurulumu: bisiklet 12 m tasindi.");

            Assert.Greater(camMoved, bikeMoved * 0.8f,
                "Bisiklet " + bikeMoved.ToString("F1") + " m tasindi ama kamera " +
                "sadece " + camMoved.ToString("F1") + " m tasindi. Kamera araci " +
                "takip ETMIYOR - oyuncunun 'kamera sabit' raporu bu.");
        }

        [UnityTest]
        public IEnumerator BisikletDonunceKameraYonuDegisiyor()
        {
            yield return LoadGame();
            _courier.ExitVehicle(Vector3.zero);
            yield return null;
            _bikePoint.Board(_courier);
            yield return null;
            yield return null;

            Camera bikeCam = BikeCameraOf(_bike);
            Quaternion before = bikeCam.transform.rotation;

            _bike.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            // Kamera yonelmesi yumusatilir; birkac kare bekle.
            for (int i = 0; i < 30; i++) yield return null;

            float angle = Quaternion.Angle(before, bikeCam.transform.rotation);
            Assert.Greater(angle, 20f,
                "Bisiklet 90 derece dondu ama kamera yalnizca " +
                angle.ToString("F0") + " derece dondu. Kamera aracin yonunu " +
                "takip etmiyor.");
        }

        // ==================================================================
        // 3. "Kutular aracin arkasina yerlestirilmiyor"
        // ==================================================================

        [UnityTest]
        public IEnumerator YuklenenKutularArkaAksinArkasinda()
        {
            yield return LoadGame();
            _courier.ExitVehicle(Vector3.zero);
            yield return null;
            yield return PrepareOnePackagedBox();

            _bikePoint.Interact(_courier);
            yield return null;
            yield return null;

            var visual = _bike.GetComponent<Player.CargoBikeVisual>();
            Assert.IsNotNull(visual, "Bisiklette CargoBikeVisual yok.");
            Assert.Greater(visual.VisibleBoxCount, 0,
                "Yuklenen kargo kasada gorunmuyor.");

            Transform bed = visual.CargoBed;
            Assert.IsNotNull(bed,
                "cargoBed null. CargoBikeVisual.Awake() 'transform.Find(\"CargoBed\")' " +
                "ile arar; o YALNIZCA dogrudan cocuklara bakar. Prefab'da " +
                "CargoBed, CargoCage altindadir - bu arama hep basarisizdir.");

            // Kutu aracin yerel -Z yonunde mi? (Bisikletin onu +Z: on teker
            // z=+0.78, arka teker z=-0.72, kasa z=-0.95.)
            Vector3 local = _bike.transform.InverseTransformPoint(
                visual.VisibleBoxes[0].transform.position);

            Assert.Less(local.z, -0.30f,
                "Kutu aracin yerel z=" + local.z.ToString("F2") +
                " konumunda, yani ARACIN MERKEZINDE/ONUNDE. Kasa arkada " +
                "olmali (z ~ -0.95). Oyuncu 'kargo merkezinde kaliyor' dedi.");

            // Kutu arka tekerden de onde olmamali.
            Assert.Less(local.z, -0.72f,
                "Kutu arka tekerin (z=-0.72) ONUNDE. Kasa arka tekerin " +
                "arkasinda olmali.");
        }

        [UnityTest]
        public IEnumerator TumKutularKasaIcindeKalir()
        {
            yield return LoadGame();
            _courier.ExitVehicle(Vector3.zero);
            yield return null;
            yield return PrepareOnePackagedBox();

            _bikePoint.Interact(_courier);
            yield return null;
            yield return null;

            var visual = _bike.GetComponent<Player.CargoBikeVisual>();
            Assert.Greater(visual.VisibleBoxCount, 0, "Kutu yok.");

            for (int i = 0; i < visual.VisibleBoxCount; i++)
            {
                Vector3 local = _bike.transform.InverseTransformPoint(
                    visual.VisibleBoxes[i].transform.position);
                Assert.Less(local.z, 0f,
                    "Kutu " + i + " aracin onunda (z=" + local.z.ToString("F2") +
                    "). Tum kutular kasada, yani arkada olmali.");
            }
        }
    }
}
