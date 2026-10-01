using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using KargoSim.Cargo;

namespace KargoSim.Tests.PlayMode
{
    /// <summary>
    /// OYUNCUNUN RAPORU: "Elimdeki kutuyu teslim edemiyorum, kapı
    /// çalma, bu kapıyı açan NPC..." + ekran görüntüsü.
    ///
    /// Ekranda gorunenler:
    ///   - HUD: "TESLIMAT: Market #8 (1 kargo)"
    ///   - Kutu etiketi: "adres: 7"
    ///   - Alt satir: "E: Araca koy (1 kutu) | [F] Araca bin | Elde: 1"
    ///   - Ust ipucu: "F: araci birak ve kargoyu eve gotur"
    ///
    /// Yani: oyuncu ARACTA, ELDE ADRES 7 VAR, durak ADRES 8.
    /// Bu test bu durumu olusturup NEDEN teslim olmadigini olcer.
    /// </summary>
    public class DeliveryDiagnosisTests
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";
        private Core.GameCompositionRoot _root;
        private Player.CourierController _courier;
        private CargoCarryController _carry;
        private CargoWorldRegistry _world;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (SceneManager.GetSceneByName("DiagCleanup").IsValid()) yield break;
            Scene fresh = SceneManager.CreateScene("DiagCleanup");
            SceneManager.SetActiveScene(fresh);
            yield return null;
            Scene game = SceneManager.GetSceneByName("Game");
            if (game.IsValid() && game.isLoaded)
            {
                AsyncOperation u = SceneManager.UnloadSceneAsync(game);
                while (!u.isDone) yield return null;
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
            _carry = _courier.Carry;
            _world = CargoWorldRegistry.Instance;
            _root.Cargo.BeginDay(1);
        }

        private static string Describe(Core.GameCompositionRoot root)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[DIAG] Durak: "
                + (root.Delivery.CurrentStop == null
                    ? "YOK" : "#" + root.Delivery.CurrentStop.DestinationId));
            sb.AppendLine("[DIAG] Rota: " + root.Delivery.Route.Count + " durak ->");
            for (int i = 0; i < root.Delivery.Route.Count; i++)
                sb.Append(" " + root.Delivery.Route[i].DestinationId);
            sb.AppendLine();
            var man = root.Cargo.Manifest;
            sb.AppendLine("[DIAG] Manifest arac=" + man.LoadedCount
                + " elde=" + man.Carried.Count + " depo=" + man.AvailableCount);
            sb.AppendLine("[DIAG] Rota KAYNAK (hangi kargolar):");
            for (int i = 0; i < root.Delivery.Route.Count; i++)
            {
                int d = root.Delivery.Route[i].DestinationId;
                var ps = root.Delivery.Route[i].Parcels;
                sb.Append("   adres " + d + ": ");
                if (ps == null) { sb.AppendLine("(parcel yok)"); continue; }
                for (int k = 0; k < ps.Count; k++)
                    sb.Append("#" + ps[k].Id + "(" + ps[k].State + ") ");
                sb.AppendLine();
            }
            return sb.ToString();
        }

        [UnityTest]
        public IEnumerator EldeKutuylaKapida_NedenTeslimOlmuyor()
        {
            yield return LoadGame();

            var depot = _root.Cargo.Manifest.Available;
            Assert.Greater(depot.Count, 0, "Kargo yok.");
            Parcel p = depot[0];

            // Kuryeyi yurut ve kutuyu AL
            Vector3 at = _courier.transform.position + _courier.transform.forward * 1.4f;
            ParcelWorldObject box = _world.GetOrCreate(p, at);
            yield return null;
            Assert.IsTrue(_carry.TryPickup(box, _courier.GetComponent<Collider>()),
                "Kutu alinamadi. " + _carry.LastMessage);
            yield return null;

            // Arac rota: kuryedeki kutuyu yukle
            var bike = Object.FindFirstObjectByType<Vehicle.BicycleController>();
            var area = bike.GetComponentInChildren<VehicleCargoArea>(true);
            _courier.transform.position = bike.transform.position + new Vector3(1.2f, 0.4f, 1.2f);
            yield return null;
            Assert.IsTrue(_carry.TryLoadToVehicle(area), "Yuklenemedi. " + _carry.LastMessage);
            yield return null;

            // Kutuyu TEKRADAN ele al (oyuncunun yaptigi gibi)
            Assert.IsTrue(_carry.TryUnloadFromVehicle(area, box), "Alinamadi.");
            yield return null;

            TestContext.WriteLine(Describe(_root));

            // Rotada bu adres var mi?
            var route = _root.Delivery.Route;
            int pIdx = -1;
            for (int i = 0; i < route.Count; i++)
                if (route[i].DestinationId == p.DestinationId) pIdx = i;

            TestContext.WriteLine("[DIAG] Elde kutu adresi = " + p.DestinationId
                + "; rota indexi = " + pIdx + " / " + route.Count);

            Assert.Greater(route.Count, 0,
                "ROTA BOS. Eldeki kargo rota kurulmasini saglamiyor - teslim "
                + "hic yapilamaz. Bu, oyuncunun 'teslim edemiyorum' "
                + "sikayetinin KOK NEDENI olabilir.");
        }

        [UnityTest]
        public IEnumerator Kapida_NpcEvdeMiVeKapıAciliyorMu()
        {
            yield return LoadGame();

            var depot = _root.Cargo.Manifest.Available;
            Parcel p = depot[0];

            // Kuryeyi adrese gotur
            var route = _root.Delivery.Route;
            int idx = -1;
            for (int i = 0; i < route.Count; i++)
                if (route[i].DestinationId == p.DestinationId) idx = i;

            if (idx < 0)
            {
                TestContext.WriteLine("[DIAG] Kutu adresi " + p.DestinationId
                    + " rotada YOK. Rota: ");
                foreach (var s in route) TestContext.WriteLine("   " + s.DestinationId);
                yield break;
            }

            var bike = Object.FindFirstObjectByType<Vehicle.BicycleController>();
            bike.transform.position = route[idx].Position;
            _root.Delivery.VehiclePosition = route[idx].Position;
            _root.Delivery.SetRouteCursorForTests(idx);
            _root.Delivery.Tick(1f, false);
            yield return null;

            Assert.IsNotNull(_root.Delivery.CurrentStop, "Durak atanmadi.");
            TestContext.WriteLine("[DIAG] Durak #" + _root.Delivery.CurrentStop.DestinationId);

            // Kutuyu ele al ve kapinin onune git
            Vector3 at = _courier.transform.position + _courier.transform.forward * 1.4f;
            ParcelWorldObject box = _world.GetOrCreate(p, at);
            yield return null;
            _carry.TryPickup(box, _courier.GetComponent<Collider>());
            yield return null;

            var doors = Object.FindObjectsByType<Player.AddressDoor>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            Player.AddressDoor right = null;
            for (int i = 0; i < doors.Length; i++)
            {
                Transform t = doors[i].transform;
                for (int up = 0; t != null && up < 4; up++, t = t.parent)
                {
                    string n = t.name;
                    int k = n.LastIndexOf('_');
                    if (k >= 0 && int.TryParse(n.Substring(k + 1), out int v)
                        && v == p.DestinationId) { right = doors[i]; break; }
                }
                if (right != null) break;
            }

            Assert.IsNotNull(right, "Dogru kapi bulunamadi (adres " + p.DestinationId + ").");

            // Kuryeyi kapiya yurut
            _courier.transform.position = right.InteractWorldPosition()
                                       + Vector3.forward * 1.2f;
            yield return null;

            string reject = right.TryDeliverForTest(p);
            TestContext.WriteLine("[DIAG] TryDeliver sonucu: "
                + (string.IsNullOrEmpty(reject) ? "(bos = sorun yok)" : reject));

            bool knocked = right.KnockForTest();
            TestContext.WriteLine("[DIAG] KnockForTest = " + knocked
                + "  (false = musteri EVIDE DEGIL)");

            Assert.IsTrue(knocked,
                "Musteri evde degil. Bu, oyuncunun 'kapi acan npc yok' "
                + "sikayetinin KOK NEDENI: NPC rastgele disariida birakiliyor.");
        }
    [UnityTest]
        public IEnumerator KapiyiAcanNpcGorunurVeKargoyuAlir()
        {
            yield return LoadGame();

            var depot = _root.Cargo.Manifest.Available;
            Parcel p = depot[0];

            // Rotayi kurmak icin kutuyu araca koy, adrese git
            Vector3 at = _courier.transform.position + _courier.transform.forward * 1.4f;
            ParcelWorldObject box = _world.GetOrCreate(p, at);
            yield return null;
            _carry.TryPickup(box, _courier.GetComponent<Collider>());
            yield return null;

            // Teslim icin kargo PAKETLENMIS olmali (dogru kural).
            // DIKKAT: durumu degistirmek yanlis - `Carried` kalmali,
            // sadece paketleme ilerlemesi tamamlanmali.
            // (Once `RestoreState(Ready)` denendi; bu "Kargo elde
            // degil" diye teslimi engelledi.)
            p.MarkPackagingStarted(0f);
            for (int g = 0; g < 12 && !p.IsPackaged; g++) p.AdvancePackaging();
            TestContext.WriteLine("[DIAG] paketlendi=" + p.IsPackaged
                + " state=" + p.State);
            Assert.IsTrue(p.IsPackaged, "Kargo paketlenemedi.");
            yield return null;

            var bike = Object.FindFirstObjectByType<Vehicle.BicycleController>();
            var area = bike.GetComponentInChildren<VehicleCargoArea>(true);
            _courier.transform.position = bike.transform.position + new Vector3(1.2f, 0.4f, 1.2f);
            yield return null;
            _carry.TryLoadToVehicle(area);
            yield return null;
            _carry.TryUnloadFromVehicle(area, box);
            yield return null;

            var route = _root.Delivery.Route;
            int idx = -1;
            for (int i = 0; i < route.Count; i++)
                if (route[i].DestinationId == p.DestinationId) idx = i;
            Assert.GreaterOrEqual(idx, 0,
                "Rotada kargonun adresi yok. Adres " + p.DestinationId
                + " rota: " + route.Count + " durak.");

            bike.transform.position = route[idx].Position;
            _root.Delivery.VehiclePosition = route[idx].Position;
            _root.Delivery.SetRouteCursorForTests(idx);
            _root.Delivery.Tick(1f, false);
            yield return null;
            Assert.IsNotNull(_root.Delivery.CurrentStop, "Durak atanmadi.");

            Player.AddressDoor door = DoorFor(p.DestinationId);
            Assert.IsNotNull(door, "Kapi bulunamadi.");

            _courier.transform.position = door.InteractWorldPosition()
                                       + Vector3.forward * 1.2f;
            yield return null;

            // --- 1) Kapi calininca NPC BELIRMEli
            var npc = door.GetComponent<Player.DoorNpc>();
            Assert.IsNotNull(npc,
                "Kapida NPC bileseni yok. Oyuncu 'kapiyi acan npc yok' "
                + "diyordu.");
            Assert.IsFalse(npc.gameObject.activeInHierarchy
                           && npc.IsOutside,
                "Kapi hic calinmadan NPC disariida olmamali.");

            // Kapiyi sisteme bagla (testte elle; oyunda root yapiyor)
            door.Configure(_root.Delivery, _courier);

            TestContext.WriteLine("[DIAG] kapi=" + door.name
                + " CanInteract=" + door.CanInteract(_courier)
                + " tasiyor=" + _carry.HasCargo
                + " durak=" + (_root.Delivery.CurrentStop == null
                    ? "YOK" : _root.Delivery.CurrentStop.DestinationId.ToString())
                + " bekliyor=" + door.IsAwaitingCode
                + " teslimSonucu=" + door.TryDeliverForTest(p));

            // DIKKAT: `KnockForTest` VE `Interact` ikisi de
            // `AnswerDoor` cagirir. Ikisini arka arkaya cagirinca
            // musteri durumu "cevap veriyor"a gecer ve ikinci cagri
            // basarisiz doner. Gercek oyuncuda yalnizca Interact var.
            door.Interact(_courier);
            yield return null;

            TestContext.WriteLine("[DIAG] Interact sonrasi: awaitCode="
                + door.IsAwaitingCode + " npcDisarida=" + npc.IsOutside);

            Assert.IsTrue(npc.IsOutside,
                "Kapi calindiktan sonra NPC KAPIDA GORUNMELI. Oyuncu ortada "
                + "kimse gormedigi icin teslim edilmis saniyordu.");

            // --- 2) Dogru kod: NPC kargoyu alir ve kutu YOK OLUR
            bool ok = door.SubmitCode(p.DeliveryCodeText);
            yield return null;

            Assert.IsTrue(ok, "Dogru kod kabul edilmedi. " + (ok ? "" : ""));
            Assert.IsFalse(_carry.HasCargo, "Teslimden sonra el BOSalmali.");
            Assert.IsTrue(p.IsDelivered, "Parcel Delivered olmali.");

            // Kutu birkac kare sonra yok olur
            for (int i = 0; i < 90; i++) yield return null;

            Assert.IsNull(_world.Find(p.Id),
                "Kutu NPC'ye teslim edildikten sonra YOK OLMALI. Kargo kaybolmadigi "
                + "icin bu teslim aninda olmali (#56, #87).");
            TestContext.WriteLine("[DIAG] NPC kapida belirdi, kargoyu aldi, kutu yok oldu.");
        }

        private Player.AddressDoor DoorFor(int addressId)
        {
            var doors = Object.FindObjectsByType<Player.AddressDoor>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < doors.Length; i++)
            {
                Transform t = doors[i].transform;
                for (int up = 0; t != null && up < 4; up++, t = t.parent)
                {
                    string n = t.name;
                    int k = n.LastIndexOf('_');
                    if (k >= 0 && int.TryParse(n.Substring(k + 1), out int v)
                        && v == addressId) return doors[i];
                }
            }
            return null;
        }
    }
}