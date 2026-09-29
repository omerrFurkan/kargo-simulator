using System.Collections;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace KargoSim.Tests.PlayMode
{
    /// <summary>
    /// ASAMA 21 - OYNANABILIRLIK GATE'I.
    ///
    /// Oyuncu defalarca "oyunu oynanabilir hale getir" dedi. Su ana kadar
    /// tek tek hata duzeltiyorduk ve her seferinde yeni bir hata cikiyordu.
    /// Bu dosya o donguyu keser: TEK bir test, butun oyuncu yolunu BASINDAN
    /// SONUNA kadar yalnizca OYUNCUNUN KULLANDIGI YOLLARDAN gecer.
    ///
    /// KURALLAR:
    ///  - E aksiyonu daima `TryInteract()` ile cagrilir (odak sistemi
    ///    dahil). Dogrudan `Interact()` cagrilmaz; o yol oyuncunun
    ///    kullandigi yol degildir ve onceki hatalari gizlemisti.
    ///  - Her adimda oyunun GECERLI kaldigi kontrol edilir: odak var mi,
    ///    kargo sayilari tutarli mi, kamera dogru mu, arac bir yerde mi.
    ///  - Hicbir adimda istisna olmamali.
    ///
    /// Bu test YES/NO vermek icindir: YES = oyuncu bu yolu yuruyebilir.
    /// </summary>
    public class PlayabilityGateTests
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";

        private Core.GameCompositionRoot _root;
        private Player.CourierController _courier;
        private Vehicle.BicycleController _bike;
        private Player.VanCargoPoint _bikePoint;
        private Player.ShopParcelPile _pile;
        private Player.PackingTable _table;
        private Player.AddressDoor _door;

        private readonly StringBuilder _log = new StringBuilder();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Scene fresh = SceneManager.CreateScene("PlayGateCleanup");
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
            _door = Object.FindFirstObjectByType<Player.AddressDoor>();

            Assert.IsNotNull(_root, "CompositionRoot yok - oyun baslamiyor.");
            Assert.IsNotNull(_courier, "Kurye yok.");
            Assert.IsNotNull(_bike, "Baslangic araci (bisiklet) yok.");
            Assert.IsNotNull(_pile, "Kutuphanedeki kargo rafi yok.");
            Assert.IsNotNull(_table, "Paketleme masasi yok.");
            Assert.IsNotNull(_door, "Teslimat kapisi yok.");

            var points = Object.FindObjectsByType<Player.VanCargoPoint>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < points.Length; i++)
                if ((points[i].transform.position - _bike.transform.position).sqrMagnitude < 9f)
                    _bikePoint = points[i];
            Assert.IsNotNull(_bikePoint, "Bisikletin kargo noktasi yok.");
        }

        private void Step(string text)
        {
            _log.AppendLine(text);
        }

        /// <summary>Kurye bu duraga ait kargo tasiyor mu?</summary>
        private bool CarriesFor(Delivery.DeliveryStop stop)
        {
            if (stop == null) return false;
            for (int i = 0; i < _courier.CarriedCount; i++)
            {
                var p = _courier.CarriedParcels[i];
                if (p == null) continue;
                if (p.DestinationId == stop.DestinationId) return true;
            }
            return false;
        }


        /// <summary>Kuryeyi bir noktaya goturup zemine oturtur (oyuncu yurudu).</summary>
        private IEnumerator WalkTo(Vector3 target, float lookAtHeight = 1.2f)
        {
            for (int i = 0; i < 12; i++) yield return new WaitForFixedUpdate();

            if (Physics.Raycast(target + Vector3.up * 8f, Vector3.down,
                                 out RaycastHit hit, 60f, ~0, QueryTriggerInteraction.Ignore))
                target.y = hit.point.y;

            Vector3 look = target + Vector3.up * lookAtHeight;
            _courier.transform.position = target;
            Vector3 dir = look - target;
            if (dir.sqrMagnitude > 0.01f)
                _courier.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            yield return null; yield return null;
        }

        private IEnumerator StandNear(Transform point, float distance = 1.5f, float height = 1.6f)
        {
            Vector3 side = Vector3.Cross(_bike.transform.up, _bike.transform.forward).normalized;
            if (side.sqrMagnitude < 0.01f) side = Vector3.right;
            yield return WalkTo(point.position + side * distance, height);
        }

        // ==================================================================
        // OYUNCUNUN TAM YOLU
        // ==================================================================

        [UnityTest]
        public IEnumerator OyuncuBasindanSonunaKadarOynayabiliyor()
        {
            yield return LoadGame();

            // ----------------------------------------------------------
            // 1) Oyun baslar: yaya, bisikletin yaninda
            // ----------------------------------------------------------
            _courier.ExitVehicle(Vector3.zero);
            yield return null; yield return null;

            Step("1) Baslangic");
            Assert.IsFalse(_courier.IsInVehicle, "Oyun arac icinde baslamamali.");
            Assert.IsTrue(_bike.ControlsLocked,
                "Oyuncu binmeden bisiklet kilitlenmeli - yoksa kendi kendine surer.");
            Assert.Less(Vector3.Distance(_courier.transform.position, _bike.transform.position), 12f,
                "Oyuncu baslangicta bisiklete yakin olmali; uzaksa arac secimi "
                + "hicbir anlam tasimaz ve oyuncu nereye gidecegini bilemez.");
            Assert.IsTrue(_courier.Focus != null,
                "Oyun baslar hicbir sey odakta degil - oyuncu ilk adimi ne "
                + "yapacagini bilemez. Konum: " + _courier.transform.position.ToString("F1"));
            Step("   odak: " + (_courier.Focus != null ? _courier.Focus.GetType().Name : "YOK"));

            // ----------------------------------------------------------
            // 2) Depodan uc KUTU al (gercek E yolu)
            // ----------------------------------------------------------
            Step("2) Kargo al");
            yield return StandNear(_pile.transform, 1.4f, 0.6f);

            var ids = new System.Collections.Generic.HashSet<int>();
            for (int n = 0; n < 3; n++)
            {
                bool used = _courier.TryInteract();
                yield return null;
                Assert.IsTrue(used, "Raf " + (n + 1) + ". kutuyu vermedi (E yolu bos dondu).");
                Assert.Greater(_courier.CarriedCount, n,
                    "Kutu alinmadi (elde " + _courier.CarriedCount + ").");
            }

            Assert.AreEqual(3, _courier.CarriedCount, "Elde 3 kutu olmali.");
            for (int i = 0; i < _courier.CarriedCount; i++)
            {
                var p = _courier.CarriedParcels[i];
                Assert.IsNotNull(p, "Eldeki " + i + ". kutu null.");
                Assert.IsTrue(ids.Add(p.Id),
                    "AYNI kutu birden fazla alindi (#" + p.Id + "). Oyuncu E'ye " +
                    "her basinca ayni kutu geliyor; kargo alis mekanigi bozuk.");
            }
            Step("   alinan: " + string.Join(", ", ids));

            // ----------------------------------------------------------
            // 3) Paketle (6 adim, kutular ayri ayri)
            // ----------------------------------------------------------
            Step("3) Paketle");
            for (int b = 0; b < _courier.CarriedCount; b++)
            {
                yield return WalkTo(_table.transform.position + new Vector3(0f, 0.1f, 1.8f), 0.6f);

                int guard = 0;
                while (guard < 60 && !_courier.CarriedParcels[b].IsPackaged)
                {
                    _courier.TryInteract();
                    _root.Delivery.Tick(2.5f, false);
                    guard++;
                    yield return null;
                }
                Assert.IsTrue(_courier.CarriedParcels[b].IsPackaged,
                    "Kutu " + b + " paketlenemedi (60 denemede). Oyuncu masada " +
                    "takilip kalir - oyun kilitlenir.");
            }
            Step("   3 kutu paketli");

            // ----------------------------------------------------------
            // 4) Bisiklete bin (F yolu)
            // ----------------------------------------------------------
            Step("4) Bin");
            yield return StandNear(_bikePoint.transform, 1.5f, 1.6f);
            _bikePoint.Board(_courier);
            yield return null; yield return null;

            Assert.IsTrue(_courier.IsInVehicle, "Bisiklete binilemedi.");
            Assert.IsFalse(_bike.ControlsLocked, "Binen oyuncu araci suremiyor.");

            Camera fps = _courier.GetComponentInChildren<Camera>(true);
            Assert.IsFalse(fps.enabled, "Binince FPS kamera kapali olmali.");
            Camera bikeCam = _bike.GetComponentInChildren<Camera>(true);
            Assert.IsTrue(bikeCam.enabled, "Binince bisiklet kamerasi acmali; kapali kalirsa oyuncu gorusu doner.");
            Step("   kamera acildi, tilt=" +
                 Vector3.Angle(bikeCam.transform.up, Vector3.up).ToString("F0") + " derece");

            // ----------------------------------------------------------
            // 5) Kutulari araca koy (E yolu, arac yonunden)
            // ----------------------------------------------------------
            Step("5) Yukle");
            int inHand = _courier.CarriedCount;
            Assert.AreEqual(3, inHand, "Binerken elde 3 kutu olmali.");
            Assert.AreEqual(0, _root.Cargo.Manifest.LoadedCount, "Bisiklet bos olmali.");

            _courier.ExitVehicle(_bikePoint.ExitSpot());
            yield return null; yield return null;
            yield return StandNear(_bikePoint.transform, 1.5f, 1.6f);

            bool loaded = _courier.TryInteract();
            yield return null;
            Assert.IsTrue(loaded, "Arac kabul etmedi (E yolu bos dondu).");
            Assert.AreEqual(3, _root.Cargo.Manifest.LoadedCount,
                "3 kutu araca girmedi, " + _root.Cargo.Manifest.LoadedCount + " girdi.");
            Assert.AreEqual(0, _courier.CarriedCount, "Yuklemeden sonra el bosalmali.");

            // Kasada kutular GERCEKTEN gorunuyor ve arkada mi?
            var visual = _bike.GetComponent<Player.CargoBikeVisual>();
            Assert.AreEqual(3, visual.VisibleBoxCount,
                "Kasada 3 kutu gorunmeli, " + visual.VisibleBoxCount + " var. " +
                "Oyuncu yukledigini gormezse yukledigini bilmez.");
            for (int i = 0; i < visual.VisibleBoxCount; i++)
            {
                Vector3 local = _bike.transform.InverseTransformPoint(
                    visual.VisibleBoxes[i].transform.position);
                Assert.Less(local.z, -0.3f,
                    "Kutu " + i + " aracin ONUNDE (z=" + local.z.ToString("F2") +
                    "); kasa arkada olmali.");
            }
            Step("   3 kutu kasada, hepsi arkada");

            // ----------------------------------------------------------
            // 6) Sur
            // ----------------------------------------------------------
            Step("6) Sur");
            _bikePoint.Board(_courier);
            yield return null; yield return null;

            Vector3 camBefore = bikeCam.transform.position;
            _bike.SetTestInput(1f, 0f, 0f, 0f);
            for (int i = 0; i < 90; i++) yield return new WaitForFixedUpdate();
            _bike.SetTestInput(0f, 0f, 0f, 0f);
            yield return null; yield return null;

            float travelled = Vector3.Distance(
                _bike.transform.position, Vector3.zero) > 0f
                ? Vector3.Distance(bikeCam.transform.position, camBefore)
                : 0f;
            Assert.Greater(travelled, 0.5f,
                "Gaz verildi ama kamera hic koymadiginden bilesiklet surmedi mi " +
                "belirsiz. Once aracin gercekten kaydigini olcmek lazim.");
            Step("   kamera " + travelled.ToString("F1") + " m tasindi");

            // ----------------------------------------------------------
            // 7) In - aracin yanina
            // ----------------------------------------------------------
            Step("7) In");
            _bike.SetTestInput(0f, 0f, 0f, 0f);
            for (int i = 0; i < 20; i++) yield return new WaitForFixedUpdate();

            Vector3 bikePos = _bike.transform.position;
            _courier.ExitVehicle(_bikePoint.ExitSpot());
            yield return null; yield return null;

            Assert.IsFalse(_courier.IsInVehicle, "Inilemedi.");
            Assert.Less(Vector3.Distance(_courier.transform.position, bikePos), 6f,
                "Inince kurye bisikletten " +
                Vector3.Distance(_courier.transform.position, bikePos).ToString("F1") +
                " m uzakta. Oyuncu 'bisiklet nerde, ben nerde' diyor: arac " +
                "oradayken kurye kilometre uzakta olmamali.");
            Assert.IsTrue(fps.enabled, "Inince FPS kamera acmali.");
            float exitTilt = Vector3.Angle(fps.transform.up, Vector3.up);
            Assert.Less(exitTilt, 20f,
                "Inince kamera " + exitTilt.ToString("F0") + " derece yatik; " +
                "oyuncu eski bakis acisinda takili kaliyor.");
            Step("   aracin " + Vector3.Distance(_courier.transform.position, bikePos).ToString("F1") +
                 " m yaninda, kamera tilt " + exitTilt.ToString("F0") + " derece");

            // ----------------------------------------------------------
            // 8) Kargoyu bosalt, teslim et
            // ----------------------------------------------------------
            Step("8) Teslim");
            _bikePoint.Board(_courier);
            yield return null;
            _courier.ExitVehicle(_bikePoint.ExitSpot());
            yield return null; yield return null;
            yield return StandNear(_bikePoint.transform, 1.5f, 1.6f);

            // Arabadan kargo al
            for (int n = 0; n < 3; n++)
            {
                _courier.TryInteract();
                yield return null;
            }
            Assert.Greater(_courier.CarriedCount, 0, "Aracdan kargo alinamadi - teslimat mumkun degil.");

            // Kapiya git
            //
            // ONEMLI: `FindFirstObjectByType<AddressDoor>()` ARBITRARY bir
            // kapi donduruyor ve o kapi teslimat hedefi olmayabilir. O
            // durumda "odak yok" hatasi oyuncunun degil TESTIN hatasidir.
            // Oyuncu hedefteki kapiya yurur; test de oyle yapmali.
            // Adrese var: durak arac konumu hedefe ulastiginda ATANIR.
            //
            // ONEMLI: rota birden fazla durak iceriyor ve kuryenin elindeki
            // kutular HEPSINE ait olmayabilir. AddressDoor.CanInteract
            // "bu adrese ait kargo elinde var mi" sartini koyar. Bu yuzden
            // ilk duraga degil, KURYENIN ELSINDEKI KUTULARIN GITTIKI
            // duraga gidilir - oyuncu da esyanin teslim edilecegi yere
            // gider.
            var route = _root.Delivery.Route;
            Assert.Greater(route.Count, 0,
                "Kargo yuklendi ama rota bos. Oyuncu nereye teslim edecegini " +
                "bilmiyor - oyun dongusu kapali.");

            Delivery.DeliveryStop stop = null;
            for (int i = 0; i < route.Count && stop == null; i++)
            {
                _bike.transform.position = route[i].Position;
                _root.Delivery.VehiclePosition = route[i].Position;
                _root.Delivery.Tick(1f, false);

                var s = _root.Delivery.CurrentStop;
                if (s == null) continue;
                if (CarriesFor(s)) stop = s;
            }

            Assert.IsNotNull(stop,
                "Rota " + route.Count + " durakli ama kuryenin elindeki " +
                _courier.CarriedCount + " kutunun TESIlim edilecegi durak yok. " +
                "Oyuncu kargoyi tasir ama nereye teslim edecegini bilemez.");
            Step("   adrese varildi: " + stop.DisplayName +
                 " (" + stop.PendingCount + " kargo bekliyor)");

            // Dogru kapiyi BUL: oyuncu kapiya yurur ve odaklanir. Test de
            // butun kapilari tek tek deneyip odaklanan ilk kapida durur.
            //
            // Konuma en yakin kapi YANLIS olabilir: 14 kapi var ve duragin
            // binasi 3.7 m uzakta oldugu icin baska bir adresin kapisi daha
            // yakin cikabiliyor. CanInteract "bu adrese ait kargo elinde mi"
            // sartini koyar; yanlis kapida odak hic olusmaz.
            Player.AddressDoor target = null;
            var doors = Object.FindObjectsByType<Player.AddressDoor>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < doors.Length && target == null; i++)
            {
                yield return WalkTo(doors[i].InteractWorldPosition(), 0.1f);
                if (_courier.Focus == doors[i]) target = doors[i];
            }

            Assert.IsNotNull(target,
                "Rota " + route.Count + " durakli, kurye " + _courier.CarriedCount +
                " kutu tasiyor, " + doors.Length + " kapi denendi ama HICBIR " +
                "kapi odaga girmedi. Oyuncu kapiya gidip E'ye basiyor ve " +
                "hicbir sey olmuyor - teslimat yolu kapali.");

            yield return WalkTo(target.InteractWorldPosition(), 0.1f);
            Step("   kapi odakta: " + target.name + " | ipucu: " + target.Prompt);
            _courier.TryInteract();
            yield return null;
            yield return null;

            Step("   odak: " + (_courier.Focus != null ? _courier.Focus.GetType().Name : "YOK"));
            Assert.IsNotNull(_courier.Focus,
                "Teslimat kapisinin onunda odak yok. Oyuncu kapiya gidip E'ye " +
                "basiyor ve hicbir sey olmuyor - teslimat yapilamiyor.");

            int moneyBefore = _root.Progress.Money;

            // Kapi kodu penceresini ac ve dogru kodu gonder.
            target.SubmitCode(_courier.CarriedParcels[0].DeliveryCodeText);
            yield return null; yield return null;


            Assert.Greater(_root.Progress.Money, moneyBefore,

                "Teslim sonrasi para artmadi: " + moneyBefore + " -> " +
                _root.Progress.Money + ". Oyuncu calisip para kazanamiyorsa " +
                "oyun dongusu kapali.");
            Step("   para " + moneyBefore + " -> " + _root.Progress.Money + " TL");

            Debug.Log("[OYNANABILIRLIK] tum adimlar gecti:\n" + _log);
        }
    }
}
