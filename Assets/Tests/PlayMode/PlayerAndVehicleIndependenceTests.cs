using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace KargoSim.Tests.PlayMode
{
    /// <summary>
    /// ASAMA 17 - OYUNCUNUN YENI RAPORU, OLÇÜLEBILIR KISIMLARI.
    ///
    /// Oyuncu dedi:
    ///  1) "araca binmedim ama benim hareketler ile hareket ediyor"
    ///  2) "sola yaptigimda karakterin gorusu de sag sola yatıyor"
    ///  3) "inincede karakter ekrani o bakis acisinda takilı kaliyor"
    ///  4) "bisiklete ve araca elimdeki kutuyu yerlestiremiyorum"
    ///
    /// Bu dosya 1-3'u olcer. 4 ayri bir dosyada.
    /// "Karakter haritanin altinda", "tekerler yanlis yerde",
    /// "HUD ortada" gibi GORSEL sorunlar otomasyonla olculemez.
    /// </summary>
    public class PlayerAndVehicleIndependenceTests
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";

        private Core.GameCompositionRoot _root;
        private Player.CourierController _courier;
        private Vehicle.BicycleController _bike;
        private Vehicle.ArcadeVehicleController _van;
        private Player.VanCargoPoint _bikePoint;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Scene fresh = SceneManager.CreateScene("IndepCleanup");
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
            _van = Object.FindFirstObjectByType<Vehicle.ArcadeVehicleController>();
            Assert.IsNotNull(_courier, "Kurye yok.");
            Assert.IsNotNull(_bike, "Bisiklet yok.");
            Assert.IsNotNull(_van, "Van yok.");

            var points = Object.FindObjectsByType<Player.VanCargoPoint>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < points.Length; i++)
                if ((points[i].transform.position - _bike.transform.position).sqrMagnitude < 9f)
                    _bikePoint = points[i];
            Assert.IsNotNull(_bikePoint, "Bisiklet kargo noktasi yok.");
        }

        // ==================================================================
        // 1) "araca binmedim ama benim hareketler ile hareket ediyor"
        // ==================================================================

        [UnityTest]
        public IEnumerator AyaktaykenIkiAracSabitKalir()
        {
            yield return LoadGame();
            _courier.ExitVehicle(Vector3.zero);
            yield return null;
            yield return null;

            // Yuruyerek araclara CALIP olmamak icin uzaklas.
            _courier.transform.position = new Vector3(0f, 40f, 0f);
            yield return null;

            // ASAMA 17: once araclar YERINE OTURSUN. Spawn'da havada
            // birakilmis olabiliyorlar; ilk 60 adimda 0.5 m dusmeleri
            // normal, "kendi kendine gidiyor" demek degil. Olculmesi
            // gereken oturduktan SONRAKI kayma.
            for (int i = 0; i < 60; i++) yield return new WaitForFixedUpdate();

            Vector3 bikeBefore = _bike.transform.position;
            Vector3 vanBefore = _van.transform.position;

            // 2 saniye fizik: araclar HAREKET ETMEMELI.
            for (int i = 0; i < 120; i++) yield return new WaitForFixedUpdate();

            float bikeMoved = Vector3.Distance(bikeBefore, _bike.transform.position);
            float vanMoved = Vector3.Distance(vanBefore, _van.transform.position);

            Assert.IsTrue(_bike.ControlsLocked,
                "Bisiklet ControlsLocked degil. Oyuncu aracinda degilken bile " +
                "klavyeyi okuyup gaz veriyor -> 'benim hareketler ile arac " +
                "hareket ediyor'.");
            Assert.IsTrue(_van.ControlsLocked,
                "Van ControlsLocked degil. Oyuncu vanin icinde degilken bile " +
                "klavyeyi okuyor; WASD oyuncunun girdisiyle vani surer.");

            Assert.Less(bikeMoved, 0.05f,
                "Oyuncu aracinda degilken bisiklet " + bikeMoved.ToString("F2") +
                " m yer degistirdi.");
            Assert.Less(vanMoved, 0.05f,
                "Oyuncu aracinda degilken van " + vanMoved.ToString("F2") +
                " m yer degistirdi.");
        }

        [UnityTest]
        public IEnumerator OyuncuAraciIterekKaydirmaz()
        {
            yield return LoadGame();
            _courier.ExitVehicle(Vector3.zero);
            yield return null;

            // Once arac yerine otursun (spawn'da havada kalabiliyor).
            for (int i = 0; i < 60; i++) yield return new WaitForFixedUpdate();

            // Oyuncuyu tam aracin uzerine koy ve birkac fizik adimi birak:
            // karakter araci itmemeli.
            _courier.transform.position = _bike.transform.position + Vector3.up * 0.5f;
            yield return null;

            Vector3 before = _bike.transform.position;
            for (int i = 0; i < 40; i++) yield return new WaitForFixedUpdate();

            float moved = Vector3.Distance(before, _bike.transform.position);
            Assert.Less(moved, 0.10f,
                "Oyuncu bisikletin USTUNE konunca bisiklet " +
                moved.ToString("F2") + " m kaydi. Karakter araci itmemeli " +
                "(Rigidbody fiziği karsi-skilled degil).");
        }

        [UnityTest]
        public IEnumerator KarakterAyaklariZemindeDegilHaritaninAltinda()
        {
            yield return LoadGame();
            _courier.ExitVehicle(Vector3.zero);
            yield return null;
            yield return null;

            var cc = _courier.GetComponent<CharacterController>();
            Assert.IsNotNull(cc, "CharacterController yok.");

            // Karakterin merkezi yerden CharacterController yaricapindan
            // asagi olmamali, yoksa yarisi haritanin altinda kalir.
            float bottom = _courier.transform.position.y - cc.height * 0.5f;
            float ground = GroundHeight(_courier.transform.position +
                                        new Vector3(0f, 5f, 0f));

            Assert.Less(bottom, ground + 0.35f,
                "Karakterin alti zeminin " + (ground - bottom).ToString("F2") +
                " m ALTINDA. Karakter haritanin altina gomulmus.");
        }

        private static float GroundHeight(Vector3 from)
        {
            if (Physics.Raycast(from, Vector3.down, out RaycastHit hit, 60f,
                                ~0, QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return float.NaN;
        }

        // ==================================================================
        // 2) "sola yaptigimda karakterin gorusu de sag sola yatiyor"
        // ==================================================================

        [UnityTest]
        public IEnumerator DireksiyonYonundeKameraYatmez()
        {
            yield return LoadGame();
            _courier.ExitVehicle(Vector3.zero);
            yield return null;
            _bikePoint.Board(_courier);
            yield return null;

            Camera cam = _bike.GetComponentInChildren<Camera>(true);
            Assert.IsNotNull(cam, "Bisiklet kamerasi yok.");

            // Bisiklete yatma (roll) ver.
            _bike.transform.rotation = Quaternion.Euler(0f, 0f, 35f);
            for (int i = 0; i < 40; i++) yield return null;

            Vector3 up = cam.transform.up;
            Vector3 worldUp = Vector3.up;
            float tilt = Vector3.Angle(up, worldUp);

            Assert.Less(tilt, 12f,
                "Bisiklet " + 35f + " derece yatmis ama kamera da " +
                tilt.ToString("F0") + " derece YATMIS. Kamera aracin " +
                "yatmasini (roll) miras almamali; oyuncu 'gorus de sag sola " +
                "yatiyor' diyor ve bu mideler.");
        }

        [UnityTest]
        public IEnumerator DuzDonusKamerayiYatirmaz()
        {
            yield return LoadGame();
            _courier.ExitVehicle(Vector3.zero);
            yield return null;
            _bikePoint.Board(_courier);
            yield return null;

            Camera cam = _bike.GetComponentInChildren<Camera>(true);

            // Sadece YATAY donus (yaw). Kamera yatmemali.
            _bike.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            for (int i = 0; i < 40; i++) yield return null;

            float tilt = Vector3.Angle(cam.transform.up, Vector3.up);
            Assert.Less(tilt, 12f,
                "Duz donuste kamera " + tilt.ToString("F0") +
                " derece yatti. Kamera roll almamali.");
        }

        // ==================================================================
        // 3) "inincede karakter ekrani o bakis acisinda takilı kaliyor"
        // ==================================================================

        [UnityTest]
        public IEnumerator IninceKameraSifirlanir()
        {
            yield return LoadGame();
            _courier.ExitVehicle(Vector3.zero);
            yield return null;

            _bikePoint.Board(_courier);
            yield return null;

            // Bisikleti egik birak: kamera egik olsun.
            _bike.transform.rotation = Quaternion.Euler(0f, 0f, 30f);
            for (int i = 0; i < 30; i++) yield return null;

            Camera fps = _courier.GetComponentInChildren<Camera>(true);
            Assert.IsNotNull(fps, "FPS kamera yok.");

            _courier.ExitVehicle(_bikePoint.ExitSpot());
            yield return null;
            yield return null;
            yield return null;

            Vector3 localEuler = fps.transform.localEulerAngles;
            Assert.IsTrue(Mathf.Abs(Mathf.DeltaAngle(localEuler.z, 0f)) < 10f,
                "Inince FPS kameranin Z egimi " + localEuler.z.ToString("F0") +
                " derece. Bisikletin yatma acisi FPS kameraya birakildi; " +
                "oyuncu 'ekran o bakis acisinda takilı kaliyor' diyor.");

            // ASIL KONTROL: DUNYA egimi. Kamera karakterin cocugu ve
            // karakter aracin yaw'una donduruluyor; bu yuzden YEREL egim
            // sifir gorunur ama dunyada kamera yatig kalir. Ilk yazimda
            // sadece yerel egime bakildi ve hata KAÇTI.
            float worldTilt = Vector3.Angle(fps.transform.up, Vector3.up);
            Assert.Less(worldTilt, 12f,
                "Inince FPS kamera dunyada " + worldTilt.ToString("F0") +
                " derece YATIK. Karakter yaw'a donduruldugu icin yerel " +
                "egim sifir gorunuyor ama kamera eski yatik acida kaliyor " +
                "- oyuncunun 'ekran takili kaliyor' raporu.");

            Assert.IsTrue(Mathf.Abs(Mathf.DeltaAngle(localEuler.x, 0f)) < 15f,
                "Inince FPS kameranin X egimi " + localEuler.x.ToString("F0") +
                " derece; kamera eski acida takili kaldi.");
        }
    }
}
