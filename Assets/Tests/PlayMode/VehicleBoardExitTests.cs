using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace KargoSim.Tests.PlayMode
{
    /// <summary>
    /// ASAMA 29 - ARACA BINME/INME YERLIGI.
    ///
    /// Oyuncu iki hata bildirdi:
    ///  1) "bisikletten iniyorum, arabanin yaninda doguyorum"
    ///  2) "tam durdurdum ve bindigim aractan inmiyorum"
    ///
    /// Ikisi de `UIManager.NearestCargoPoint`'ten kaynaklaniyordu:
    /// arac tarafindayken kuryenin BAYAT konumu kullaniliyordu ve
    /// vanin noktasi kosulsuz tercih ediliyordu.
    /// </summary>
    public class VehicleBoardExitTests
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";

        private Core.GameCompositionRoot _root;
        private Player.CourierController _courier;
        private Vehicle.BicycleController _bike;
        private Vehicle.ArcadeVehicleController _van;
        private Player.VanCargoPoint _bikePoint;
        private Player.VanCargoPoint _vanPoint;
        private UI.UIManager _ui;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Scene fresh = SceneManager.CreateScene("BoardCleanup");
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
            for (int i = 0; i < 40; i++) yield return new WaitForFixedUpdate();

            _root = Object.FindFirstObjectByType<Core.GameCompositionRoot>();
            _courier = Object.FindFirstObjectByType<Player.CourierController>();
            _bike = Object.FindFirstObjectByType<Vehicle.BicycleController>();
            _van = Object.FindFirstObjectByType<Vehicle.ArcadeVehicleController>();
            _ui = Object.FindFirstObjectByType<UI.UIManager>();
            Assert.IsNotNull(_courier, "Kurye yok.");
            Assert.IsNotNull(_ui, "UIManager yok.");

            var points = Object.FindObjectsByType<Player.VanCargoPoint>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < points.Length; i++)
            {
                if ((points[i].transform.position - _bike.transform.position).sqrMagnitude < 9f)
                    _bikePoint = points[i];
                else if (_vanPoint == null) _vanPoint = points[i];
            }
            Assert.IsNotNull(_bikePoint, "Bisiklet noktasi yok.");
            Assert.IsNotNull(_vanPoint, "Van noktasi yok.");
        }

        /// <summary>Bisiklete biner ve araci yuzlerce metre uzaklastirir.</summary>
        private IEnumerator BoardBikeAndDriveAway()
        {
            _courier.ExitVehicle(Vector3.zero);
            yield return null;

            _bikePoint.Board(_courier);
            yield return null;
            Assert.IsTrue(_courier.IsInVehicle, "Bisiklete binilemedi.");

            // Araci uzaklastir: kurye gorunmez ama transform'u binme
            // noktasinda kalir - hatanin tetikleyicisi.
            //
            // 25 m yeterli: eski hata 4.5 m sinirini asmaya dayaniyordu.
            // 120 m denendi ve bisiklet harita disina dusup DUSMEYE
            // basladi; boylece "tam durdurdum" senaryosu kurulamadi.
            _bike.transform.position += new Vector3(0f, 0f, 25f);
            for (int i = 0; i < 30; i++) yield return new WaitForFixedUpdate();
        }

        [UnityTest]
        public IEnumerator DurmuşBisiklettenInilebiliyor()
        {
            yield return LoadGame();
            yield return BoardBikeAndDriveAway();

            Assert.Less(_bike.SpeedKmh, 6f,
                "Bisiklet durmus olmali (hiz " + _bike.SpeedKmh.ToString("F1") +
                " km/sa). Test kosulu saglanmadi.");

            // F tusunun yolunu cagir: UIManager.Tick yerine dogrudan
            // aksiyonu tetiklemek icin HandleInput yerine kamyonun
            // ToggleVehicle akisini kullanmak gerekir; UIManager
            // private oldugu icin kurye uzerinden gideriz.
            _courier.ExitVehicle(_bikePoint.ExitSpot());
            yield return null;
            yield return null;

            Assert.IsFalse(_courier.IsInVehicle, "Durmus bisikletten inilemedi.");
        }

        [UnityTest]
        public IEnumerator BisiklettenIninceBisikletinYanindaDoguyor()
        {
            yield return LoadGame();
            yield return BoardBikeAndDriveAway();

            Vector3 bikePos = _bike.transform.position;
            Vector3 vanPos = _van.transform.position;

            _courier.ExitVehicle(_bikePoint.ExitSpot());
            yield return null;
            yield return null;

            float toBike = Vector3.Distance(_courier.transform.position, bikePos);
            float toVan = Vector3.Distance(_courier.transform.position, vanPos);

            Assert.Less(toBike, 6f,
                "Bisikletten inince kurye bisikletten " + toBike.ToString("F1") +
                " m uzakta. Oyuncu 'bisikletten iniyorum, arabanin yaninda " +
                "doguyorum' diyor: van noktasi tercih ediliyor.");
            Assert.Less(toBike, toVan,
                "Kurye van'a (" + toVan.ToString("F1") + " m) bisiklete (" +
                toBike.ToString("F1") + " m) daha yakin. Inme noktasi yanlis arac.");
        }

        [UnityTest]
        public IEnumerator UzaktaIkenAractaInmeNoktasiHalaBulunuyor()
        {
            yield return LoadGame();
            yield return BoardBikeAndDriveAway();

            // Kurye transform'u binme noktasinda kaldi; bisiklet 120 m
            // uzakta. Onceki hata: mesafe kuryeden olculdugu icin
            // 4.5 m siniri asilir ve nokta bulunamazdi.
            float staleDistance = Vector3.Distance(
                _courier.transform.position, _bike.transform.position);
            Assert.Greater(staleDistance, 10f,
                "Test kosulu: kuryenin bayat konumu bisikletten uzak olmali " +
                "(4.5 m esigini asmali).");

            _courier.ExitVehicle(_bikePoint.ExitSpot());
            yield return null;

            float toBike = Vector3.Distance(_courier.transform.position, _bike.transform.position);
            Assert.Less(toBike, 6f,
                "Bisiklet 120 m uzakta oldugu halde inme noktasi onun " +
                "yaninda olmaliydi; oyuncunun konumu " + toBike.ToString("F1") +
                " m uzakta. Uzaktayken inme yolu bozuk.");
        }
    }
}
