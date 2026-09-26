using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace KargoSim.Tests.PlayMode
{
    /// <summary>
    /// Asama 13 - NPC + TESLIMAT KODU akisinin GERCEK SAHNEDE dogrulanmasi.
    ///
    /// Kabul kriteri:
    ///   Paket hazir -> aracda -> adrese git -> in -> kargoyu al
    ///   -> kapida E -> NPC -> kod -> dogru kod -> Delivered -> odul
    ///   Yanlis kod       -> Delivered OLMAZ
    ///   NPC evde degilse -> Delivered OLMAZ
    ///
    /// NPC KONTROLU: NPC'nin evde olmasi gun tohumuna baglidir. Test
    /// rastgele bir gune denk gelip "evde degil" yüzünden gecersiz
    /// kalmasin diye <c>GameCompositionRoot.Npc.BeginDay(d)</c> ile
    /// ARANAN gun secilir. Bu yalnizca NPC durumunu degistirir; kargonun
    /// teslimat kodu kargonun kendi gununden turer ve etkilenmez.
    ///
    /// KAPSAM SINIRI: oyuncu girdisi (klavye/fare) ve gorsel cikti
    /// otomasyonla olculemez. Buradaki testler LOGIK ve ENTEGRASYON
    /// zincirini dogrular; panelin gorunumu ve tus basma hissi gozle
    /// incelenmelidir.
    /// </summary>
    public class NpcDeliveryPlayModeTests
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";

        private Core.GameCompositionRoot _root;
        private Player.CourierController _courier;
        private Player.AddressDoor _door;
        private Player.VanCargoPoint _vanPoint;
        private Player.PackingTable _table;
        private Player.ShopParcelPile _pile;
        private UI.CodeEntryView _codeEntry;

        /// <summary>RunUpToDoor'in buldugu teslimat adresine ait kargo.</summary>
        private Cargo.Parcel _target;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Scene fresh = SceneManager.CreateScene("NpcCleanup");
            SceneManager.SetActiveScene(fresh);
            yield return null;

            Scene game = SceneManager.GetSceneByName("Game");
            if (game.IsValid() && game.isLoaded)
            {
                AsyncOperation unload = SceneManager.UnloadSceneAsync(game);
                while (!unload.isDone) yield return null;
            }
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
            _door = Object.FindFirstObjectByType<Player.AddressDoor>();
            _vanPoint = Object.FindFirstObjectByType<Player.VanCargoPoint>();
            _table = Object.FindFirstObjectByType<Player.PackingTable>();
            _pile = Object.FindFirstObjectByType<Player.ShopParcelPile>();
            _codeEntry = Object.FindFirstObjectByType<UI.CodeEntryView>(
                FindObjectsInactive.Include);

            Assert.IsNotNull(_root, "GameCompositionRoot yok.");
            Assert.IsNotNull(_courier, "Kurye yok.");
            Assert.IsNotNull(_door, "Adres kapisi yok.");
            Assert.IsNotNull(_root.Npc, "NPC sistemi kurulmadi.");
            Assert.IsNotNull(_codeEntry, "Kod giris paneli YOK. Menu: Kargo/0. HER SEYI KUR");
        }

        /// <summary>
        /// NPC durumunu istenen hale getirir. Npc.BeginDay deterministik
        /// oldugu icin dogru gunu arayip onu uygulamak yeterli.
        /// </summary>
        private void ForceNpcHome(int destinationId, bool home)
        {
            for (int day = 1; day <= 60; day++)
            {
                _root.Npc.BeginDay(day);
                if (_root.Npc.IsHome(destinationId) == home) return;
            }

            Assert.Fail("60 gun icinde " + destinationId + " adresi icin "
                + (home ? "EVDE" : "EVDE DEGIL") + " durumu bulunamadi. "
                + "homeChance kurali bozulmus olabilir.");
        }

        /// <summary>
        /// Akisin teslim oncesi kismini surer ve <see cref="_target"/> alanina
        /// teslim edilecek kargoyu yazar.
        /// </summary>
        private IEnumerator RunUpToDoor()
        {
            _courier.ExitVehicle(_vanPoint.ExitSpot());
            yield return null;

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

            _vanPoint.Interact(_courier);
            yield return null;
            _vanPoint.Board(_courier);
            yield return null;

            var delivery = _root.Delivery;
            delivery.VehiclePosition = delivery.Route[0].Position;
            delivery.Tick(0.1f, false);
            yield return null;

            _courier.ExitVehicle(_vanPoint.ExitSpot());
            yield return null;

            // KRITIK: duraktaki kargo ARAÇTADIR, kuryede degil. Once
            // arac noktasindan alinir (VanCargoPoint.Interact). Bu adim
            // atlanirsa elde hicbir kargo olmaz ve kapida teslim yapilamaz.
            _vanPoint.Interact(_courier);
            yield return null;
            Assert.Greater(_courier.CarriedCount, 0, "Araktan kargo alinamadi.");

            var stop = delivery.CurrentStop;
            Assert.IsNotNull(stop, "Durak yok.");

            _target = null;
            var carried = _courier.CarriedParcels;
            for (int i = 0; i < carried.Count; i++)
            {
                if (carried[i].DestinationId == stop.DestinationId) { _target = carried[i]; break; }
            }
            Assert.IsNotNull(_target, "Duraga ait kargo aractan alinmadi.");

            _courier.transform.position =
                _door.transform.TransformPoint(new Vector3(0f, 0.1f, 1.6f));
            yield return null;
            yield return null;
        }

        // ------------------------------------------------------------------
        // 1. Dogru kod -> Delivered + odul
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator DogruKod_TeslimatTamamlanirVeOduluVerilir()
        {
            yield return LoadGame();
            yield return RunUpToDoor();

            ForceNpcHome(_root.Delivery.CurrentStop.DestinationId, true);

            int moneyBefore = _root.Progress.Money;
            var parcel = _target;

            _door.Interact(_courier);
            yield return null;

            Assert.IsTrue(_door.IsAwaitingCode, "Kapi calindiginda kod oturumu acilmali.");
            Assert.IsTrue(_codeEntry.IsOpen, "Kod paneli acilmali.");
            Assert.IsFalse(parcel.IsDelivered, "Kod girilmeden teslim olmamali.");
            Assert.AreEqual(0, _door.WrongAttempts, "Hata sayaci 0 olmali.");

            _door.SubmitCode(parcel.DeliveryCodeText);
            yield return null;

            Assert.IsTrue(parcel.IsDelivered, "Dogru kodla teslim edilmis olmali.");
            Assert.IsFalse(parcel.IsLoaded, "Teslim edilen kargo aracda kalmamali.");
            Assert.IsFalse(_door.IsAwaitingCode, "Oturum kapanmali.");
            Assert.Greater(_root.Progress.Money, moneyBefore,
                "Teslimat odulu islenmeli (para artmali).");
        }

        // ------------------------------------------------------------------
        // 2. Yanlis kod -> Delivered OLMAZ, para verilmez
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator YanlisKod_TeslimatOlusmazParaVerilmez()
        {
            yield return LoadGame();
            yield return RunUpToDoor();

            ForceNpcHome(_root.Delivery.CurrentStop.DestinationId, true);

            var parcel = _target;
            int moneyBefore = _root.Progress.Money;

            _door.Interact(_courier);
            yield return null;
            Assert.IsTrue(_door.IsAwaitingCode, "Kod oturumu acilmali.");

            int wrong = parcel.DeliveryCode == 1111 ? 2222 : 1111;
            bool ok = _door.SubmitCode(wrong.ToString());
            yield return null;

            Assert.IsFalse(ok, "Yanlis kod kabul edilmemeli.");
            Assert.IsFalse(parcel.IsDelivered,
                "KRITIK: yanlis kodla kargo TESLIM EDILMIS olmamali.");
            Assert.IsTrue(parcel.IsActive, "Kargo oyunda kalmali (iade edilebilir).");
            Assert.AreEqual(1, _door.WrongAttempts, "Hata sayaci 1 olmali.");
            Assert.AreEqual(moneyBefore, _root.Progress.Money,
                "Yanlis kodla para KAZANILMAMALI (ceza 0 ise degismemeli).");

            // Oyuncunun elinde kaldi: kargo KAYBOLMADI.
            bool stillCarried = false;
            for (int i = 0; i < _courier.CarriedParcels.Count; i++)
                if (_courier.CarriedParcels[i] == parcel) { stillCarried = true; break; }
            Assert.IsTrue(stillCarried, "Yanlis koddan sonra kargo ELDE kalmali.");
        }

        // ------------------------------------------------------------------
        // 3. Ayni kargo ikinci kez teslim EDILENEMEZ
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator TeslimEdilenKargo_IkinciKezTeslimEdilemez()
        {
            yield return LoadGame();
            yield return RunUpToDoor();

            ForceNpcHome(_root.Delivery.CurrentStop.DestinationId, true);

            var parcel = _target;

            _door.Interact(_courier);
            yield return null;
            _door.SubmitCode(parcel.DeliveryCodeText);
            yield return null;
            Assert.IsTrue(parcel.IsDelivered, "Once teslim etmeli.");

            // Ikinci deneme: oturum kapali.
            bool second = _door.SubmitCode(parcel.DeliveryCodeText);
            yield return null;

            Assert.IsFalse(second, "Teslim edilmis kargo ikinci kez teslim edilmemeli.");
            Assert.IsTrue(parcel.IsDelivered, "Durum Delivered olarak kalmali.");
        }

        // ------------------------------------------------------------------
        // 4. NPC evde degilse teslim olusmaz
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator MusteriEvdeDegilse_TeslimatOlusmaz()
        {
            yield return LoadGame();
            yield return RunUpToDoor();

            ForceNpcHome(_root.Delivery.CurrentStop.DestinationId, false);

            var parcel = _target;
            int moneyBefore = _root.Progress.Money;
            int carriedBefore = _courier.CarriedCount;

            _door.Interact(_courier);
            yield return null;

            Assert.IsFalse(_door.IsAwaitingCode,
                "Musteri evde degilse kod oturumu ACILMAMALI.");
            Assert.IsFalse(parcel.IsDelivered,
                "KRITIK: musteri evde degilse teslim OLMAMALI.");
            Assert.AreEqual(moneyBefore, _root.Progress.Money, "Para kazanilmamali.");
            Assert.AreEqual(carriedBefore, _courier.CarriedCount,
                "Kargo elde kalmali; teslim edilmemis olmamali.");
        }

        // ------------------------------------------------------------------
        // 5. Yanlis koddan sonra DOGRU kod ile teslimat yapilabilir
        // ------------------------------------------------------------------

        [UnityTest]
        public IEnumerator YanlisKoddanSonraDogruKodTeslimatTamamlar()
        {
            yield return LoadGame();
            yield return RunUpToDoor();

            ForceNpcHome(_root.Delivery.CurrentStop.DestinationId, true);

            var parcel = _target;

            _door.Interact(_courier);
            yield return null;

            int wrong = parcel.DeliveryCode == 1111 ? 2222 : 1111;
            Assert.IsFalse(_door.SubmitCode(wrong.ToString()),
                "Once yanlis kod reddedilmeli.");
            yield return null;

            // Oturum ACIK kalmali: oyuncu ayni kapida tekrar deneyebilmeli.
            Assert.IsTrue(_door.IsAwaitingCode,
                "Yanlis koddan sonra oturum kapanmamali; oyuncu kapiya "
                + "tekrar gitmek zorunda kalmamali.");

            Assert.IsTrue(_door.SubmitCode(parcel.DeliveryCodeText),
                "Ardindan dogru kod kabul edilmeli.");
            yield return null;
            Assert.IsTrue(parcel.IsDelivered, "Teslimat tamamlanmali.");
        }
    }
}
