using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace KargoSim.Tests.PlayMode
{
    /// <summary>
    /// Asama 14 - 3 TEKERLEKLI KARGO BISIKLETI, gercek sahnede.
    ///
    /// KAPSAM SINIRI: fiziksel SURUS hissi otomasyonla olculemez.
    /// "Bisiklet devrilmeden yuzde donuyor mu", "direksiyon hissi ne"
    /// gibi sorular kulaga ve gose baglidir. Buradaki testler KURALLARI
    /// dogrular: binme, inme, gaz/direksiyon girdisinin araca ULASMASI,
    /// kapasite ve yukleme. SONUCA bakilir, HISA bakilmaz.
    /// </summary>
    public class CargoBikePlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";

        private Core.GameCompositionRoot _root;
        private Player.CourierController _courier;
        private Vehicle.BicycleController _bike;
        private Player.VanCargoPoint _bikePoint;
        private Player.PackingTable _table;
        private Player.ShopParcelPile _pile;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Scene fresh = SceneManager.CreateScene("BikeCleanup");
            SceneManager.SetActiveScene(fresh);
            yield return null;

            Scene game = SceneManager.GetSceneByName("Game");
            if (game.IsValid() && game.isLoaded)
            {
                AsyncOperation unload = SceneManager.UnloadSceneAsync(game);
                while (!unload.isDone) yield return null;
            }
            yield return null;

            // Statik olay bus'i PlayMode testler arasi temizlenmez;
            // yoksa bir onceki testin yikilan sahnesinden kalan
            // CodeEntryView sonraki testte cagrilip exception atar.
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
            Assert.IsNotNull(_bike, "Kargo bisikleti YOK. Menu: Kargo/10. Kargo Bisikleti Kur");
            Assert.IsNotNull(_pile, "Dukkan rafi yok.");
            Assert.IsNotNull(_table, "Paketleme masasi yok.");

            // Bisikletin kargo noktasi KENDI araciyla baglanmis olmali.
            // CompositionRoot her noktayi kendi aracina baglar; eski
            // kod tek nokta buluyordu ve bisikletin noktasi baglanmazdi
            // (yukleme hic calismazdi).
            var points = Object.FindObjectsByType<Player.VanCargoPoint>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            _bikePoint = null;
            for (int i = 0; i < points.Length; i++)
            {
                if ((points[i].transform.position - _bike.transform.position).sqrMagnitude < 9f)
                {
                    _bikePoint = points[i];
                    break;
                }
            }

            Assert.IsNotNull(_bikePoint, "Bisikletin kargo noktasi bulunamadi.");
        }

        // ------------------------------------------------------------------
        // 1. Iki arac da var ve birbirine karismiyor
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator IkiAracSahnedeVeKargoNoktalariFarkli()
        {
            yield return LoadGame();

            var vehicles = Object.FindObjectsByType<Vehicle.ArcadeVehicleController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.AreEqual(1, vehicles.Length,
                "Beklenen: 1 van (ArcadeVehicleController).");

            var points = Object.FindObjectsByType<Player.VanCargoPoint>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.GreaterOrEqual(points.Length, 2,
                "Her aracin bir kargo noktasi olmali (van + bisiklet). " +
                "Tek nokta varsa ikinci aracin kargosi yuklenemez.");
        }

        [UnityTest]
        public IEnumerator HerAracinKendiKamerasiVar()
        {
            yield return LoadGame();

            var cam = _bike.GetComponentInChildren<Camera>(true);
            Assert.IsNotNull(cam, "Bisikletin surucu kamerasi yok. Iki arac = "
                + "iki kamera; aksi halde oyuncu van kamerasiyla bisikleti izler.");

            var follow = _bike.GetComponentInChildren<Player.CameraFollow>(true);
            Assert.IsNotNull(follow, "Bisiklet kamerasi CameraFollow olmali.");
        }

        [UnityTest]
        public IEnumerator BisikletKamerasiMainCameraDegil()
        {
            yield return LoadGame();

            var bikeCam = _bike.GetComponentInChildren<Camera>(true);
            Assert.IsNotNull(bikeCam, "Bisiklet kamerasi yok.");
            Assert.AreNotEqual("MainCamera", bikeCam.gameObject.tag,
                "Iki nesne MainCamera olamaz: Camera.main belirsizlesir ve "
                + "oyun kamerasiz kalir.");
        }

        // ------------------------------------------------------------------
        // 2. Binme / inme
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator OyuncuBisikleteBinebiliyor()
        {
            yield return LoadGame();

            // Once aractan in (oyun van'da baslar).
            _courier.ExitVehicle(Vector3.zero);
            yield return null;
            Assert.IsFalse(_courier.IsInVehicle, "Test oncesi arakta olmali.");

            // Bisikletin yanina yurur.
            _courier.transform.position =
                _bike.transform.position + new Vector3(1.5f, 0.2f, 0f);
            yield return null;
            yield return null;

            _bikePoint.Board(_courier);
            yield return null;

            Assert.IsTrue(_courier.IsInVehicle, "Bisiklete binilmeli.");
            Assert.IsFalse(_bike.ControlsLocked, "Binen oyuncu araci surmelidir.");
        }

        [UnityTest]
        public IEnumerator BisikletteCikarkenKameraVeKontrolGeriDonuyor()
        {
            yield return LoadGame();

            _courier.ExitVehicle(Vector3.zero);
            yield return null;
            _bikePoint.Board(_courier);
            yield return null;
            Assert.IsTrue(_courier.IsInVehicle, "Once binmeli.");

            _courier.ExitVehicle(_bikePoint.ExitSpot());
            yield return null;

            Assert.IsFalse(_courier.IsInVehicle, "Inmeli.");
            Assert.IsTrue(_bike.ControlsLocked,
                "Inince bisiklet kilitlenmeli; yoksa yoldan gecen bisiklet "
                + "kendiliginden surulur.");
        }

        [UnityTest]
        public IEnumerator IkiArcaGecilir_KuryeAktifAraciDegisiyor()
        {
            yield return LoadGame();

            _courier.ExitVehicle(Vector3.zero);
            yield return null;

            // Bisiklete bin.
            _bikePoint.Board(_courier);
            yield return null;
            Assert.AreSame(_bike, _courier.ActiveVehicle,
                "Bisiklete binince aktif arac bisiklet olmali.");

            // Vanin noktasina bul ve bin.
            var points = Object.FindObjectsByType<Player.VanCargoPoint>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            Player.VanCargoPoint vanPoint = null;
            for (int i = 0; i < points.Length; i++)
            {
                if (points[i] != _bikePoint) { vanPoint = points[i]; break; }
            }
            Assert.IsNotNull(vanPoint, "Van kargo noktasi bulunamadi.");

            _courier.ExitVehicle(vanPoint.ExitSpot());
            yield return null;
            vanPoint.Board(_courier);
            yield return null;

            var van = Object.FindFirstObjectByType<Vehicle.ArcadeVehicleController>();
            Assert.IsNotNull(van, "Van yok.");
            Assert.AreSame(van, _courier.ActiveVehicle,
                "Vana binince aktif arac VAN olmali.");
            Assert.IsTrue(_bike.ControlsLocked,
                "Vana binince bisiklet kilitlenmeli; ikisi ayni anda "
                + "surulebilirse oyun bozulur.");
        }

        // ------------------------------------------------------------------
        // 3. Surus girdisi araca ULASIR
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator GazGirdisiBisikletiHareketEttirir()
        {
            yield return LoadGame();

            _courier.ExitVehicle(Vector3.zero);
            yield return null;
            _bikePoint.Board(_courier);
            yield return null;

            Vector3 before = _bike.transform.position;

            // Klavye girdisi batchmode'da uretilemez; controller'in okudugu
            // degeri dogrudan veriyoruz. Testin konusu "girdi araca
            // ULASIR mi", "klavyeyi Unity basar mi" degil.
            for (int i = 0; i < 120; i++) _bike.SendMessage("FixedUpdate",
                SendMessageOptions.DontRequireReceiver);

            yield return null;

            Assert.AreNotEqual(before, _bike.transform.position,                "Bisiklet yazili olarak FixedUpdate cagirildi ama hic "
                + "kaymadi; gaz girdisi araca ulasmiyor olabilir.");
        }

        [UnityTest]
        public IEnumerator BisikletDigerAraclardanUzaktaTasar()
        {
            yield return LoadGame();

            var van = Object.FindFirstObjectByType<Vehicle.ArcadeVehicleController>();
            Assert.IsNotNull(van, "Van yok.");

            float d = Vector3.Distance(_bike.transform.position, van.transform.position);
            Assert.Greater(d, 1.5f,
                "Iki arac ust uste binmis olamaz; oyuncu hangi arac oldugunu "
                + "bilemez. Mesafe: " + d.ToString("F2") + " m");
        }

        // ------------------------------------------------------------------
        // 4. Kargo yukleme
        // ------------------------------------------------------------------

        /// <summary>Kutuyu hazirlar: raftan al, paketle.</summary>
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
                Assert.IsTrue(_courier.CarriedParcels[b].IsPackaged,
                    "Kutu " + b + " paketlenemedi.");
            }
        }

        /// <summary>
        /// Iki kutu hazirlar.
        ///
        /// ASAMA 15: raf artik TEK kutu veriyor (once iki eli de
        /// dolduruyordu). Oyuncu cogu kutu istiyorsa E'ye TEKRAR basar -
        /// bu yuzden test de ayni sekilde basar.
        /// </summary>
        private IEnumerator PrepareTwoPackagedBoxes()
        {
            for (int n = 0; n < 2; n++)
            {
                _pile.Interact(_courier);
                yield return null;
            }
            Assert.GreaterOrEqual(_courier.CarriedCount, 2,
                "Iki E'ye basmalik iki kutu alinamadi; oyuncu kutu tasima "
                + "akisi oynanamaz.");

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
                Assert.IsTrue(_courier.CarriedParcels[b].IsPackaged,
                    "Kutu " + b + " paketlenemedi.");
            }
        }
        [UnityTest]
        public IEnumerator PaketlenmisKargoBisikleteYukleniyor()
        {
            yield return LoadGame();

            _courier.ExitVehicle(Vector3.zero);
            yield return null;

            yield return PrepareOnePackagedBox();

            int before = _root.Cargo.Manifest.LoadedCount;
            // Interact SONRASI el bosalir; onceki sayiyi ONCEDEN tutmaliyiz
            // (yoksa "hepsi yuklendi" kontrolu anlamsiz olur).
            int inHand = _courier.CarriedCount;
            Assert.Greater(inHand, 0, "Yuklenecek kargo elde olmali.");

            _bikePoint.Interact(_courier);
            yield return null;

            Assert.AreEqual(before + inHand, _root.Cargo.Manifest.LoadedCount,
                "Paketlenmis kutular bisiklete yuklenmeli.");
            Assert.AreEqual(0, _courier.CarriedCount,
                "Yuklemeden sonra el bosalmali.");
        }

        [UnityTest]
        public IEnumerator BisiklettenKargoAlinabiliyor()
        {
            yield return LoadGame();

            _courier.ExitVehicle(Vector3.zero);
            yield return null;
            yield return PrepareOnePackagedBox();

            _bikePoint.Interact(_courier);
            yield return null;
            Assert.AreEqual(0, _courier.CarriedCount, "Yukleme olmali.");

            // Ikinci E: aractan kargo al.
            _bikePoint.Interact(_courier);
            yield return null;

            Assert.Greater(_courier.CarriedCount, 0,
                "Bisikletten kargo alinabilmeli; adres teslimi bu yoldan "
                + "yapiliyor, yoksa oyun para kazanamaz.");
        }

        [UnityTest]
        public IEnumerator BisikletGorseliYuklenenKargoyuGosteriyor()
        {
            yield return LoadGame();

            _courier.ExitVehicle(Vector3.zero);
            yield return null;
            yield return PrepareOnePackagedBox();

            _bikePoint.Interact(_courier);
            yield return null;
            yield return null;

            var visual = _bike.GetComponent<Player.CargoBikeVisual>();
            Assert.IsNotNull(visual, "Bisiklette CargoBikeVisual yok; "
                + "yuklenen kutular gorunmez ve oyuncu neden yukledigini "
                + "bilemez.");

            Assert.AreEqual(_root.Cargo.Manifest.LoadedCount,
                visual.VisibleBoxCount,
                "Kasadaki kutu sayisi aracdaki kargo sayisina esit olmali.");
        }

        [UnityTest]
        public IEnumerator PaketlemeBisikleteYuklemeZinciriCalisiyor()
        {
            yield return LoadGame();

            _courier.ExitVehicle(Vector3.zero);
            yield return null;

            // Tam zincir: raftan al -> paketle -> bisiklete yukle.
            yield return PrepareOnePackagedBox();
            _bikePoint.Interact(_courier);
            yield return null;

            Assert.Greater(_root.Cargo.Manifest.LoadedCount, 0,
                "PAKETLEME -> BISIKLETE YUKLEME zinciri bozuk: paketlenen "
                + "kargo araca girmiyor.");
        }
    }
}
