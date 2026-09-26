using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace KargoSim.Tests.PlayMode
{
    /// <summary>
    /// OYUNUN OYNANABILIR OLDUGUNU KANITLAYAN KABUL TESTI.
    ///
    /// EditMode testleri sahneyi YUKLUYOR ama runtime'i gormuyor.
    /// Buradaki test gercek oyunu acar ve butun akisi bastan sona
    /// surer:
    ///
    ///   arac -> aracdan in -> raftan kargo al -> araca koy ->
    ///   araca bin -> adrese git -> in -> kapidan teslim et
    ///
    /// Bu turda bulunan en kotu hata buydu: araca binmek yerine
    /// aracdan inme cagriliyordu ve adres teslimi matematiksel
    /// olarak IMPKANSIZDI (arac->el yolu hic yoktu). Bu test o
    /// turleri kapatir: akis yarida kalsaydi kirmizi olurdu.
    /// </summary>
    public class GameFlowAcceptanceTests
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";

        /// <summary>
        /// Bu test GERCEK oyun sahnesini yukler ve orada birkac saniye
        /// simulasyon secer. Artik aktif sahne oyun sahnesidir; onu
        /// temizlemezsek SONRAKI testler (or. arac dondurme testi)
        /// gercek arac ve zeminin USTUNE kendi aracini kuruyor ve
        /// fizik engellere takiliyordu.
        /// </summary>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // Once bos bir sahne olustur ve aktif yap, SONRA oyun
            // sahnesini kaldir. (Tek sahne varken UnloadScene calismaz.)
            Scene fresh = SceneManager.CreateScene("AcceptanceCleanup");
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

        [UnityTest]
        public IEnumerator Oyun_ButunAkisi_UctanUcaCalisir()
        {
            // --- Sahneyi ac ---
            // LoadScene void doner; aktif sahneyi ayrica soruyoruz.
            // Sahne Build Settings'te olmali (GameSceneIntegrityTests
            // bunu ayrica dogrular).
            SceneManager.LoadScene(ScenePath, LoadSceneMode.Single);
            yield return null;
            yield return null;

            Scene scene = SceneManager.GetActiveScene();
            Assert.IsTrue(scene.IsValid() && scene.isLoaded,
                "Oyun sahnesi yuklenemedi: " + ScenePath + " (Build Settings'e ekli mi?)");
            Assert.AreEqual("Game", scene.name, "Beklenenden farkli sahne yuklendi: " + scene.name);

            // Birkare daha bekle: tum Awake/Start ve ilk olaylar calissin.
            yield return null;
            yield return null;

            // --- 1. Temel baglantilar yerinde mi? ---
            var root = Object.FindFirstObjectByType<Core.GameCompositionRoot>();
            Assert.IsNotNull(root, "GameCompositionRoot yok.");
            Assert.IsNotNull(root.Cargo, "Kargo sistemi kurulmadi.");
            Assert.IsNotNull(root.Delivery, "Teslimat sistemi kurulmadi.");
            Assert.IsNotNull(root.MapLayout, "MapLayout atanmadi.");

            var courier = Object.FindFirstObjectByType<Player.CourierController>();
            Assert.IsNotNull(courier, "Kurye karakteri yok. Menu: Kargo/7. Kurye Karakterini Kur");

            var pile = Object.FindFirstObjectByType<Player.ShopParcelPile>();
            Assert.IsNotNull(pile, "Dukkan rafi yok. Menu: Kargo/8. Dukkani Kur");

            var vanPoint = Object.FindFirstObjectByType<Player.VanCargoPoint>();
            Assert.IsNotNull(vanPoint, "Arac kargo noktasi yok.");

            var door = Object.FindFirstObjectByType<Player.AddressDoor>();
            Assert.IsNotNull(door, "Adres kapisi yok (14 adet olmali).");

            // --- 2. Baslangic durumu ---
            Assert.IsTrue(courier.IsInVehicle,
                "Oyun arac icinde baslamali; kurye ayaktaymis.");
            Assert.IsFalse(courier.IsCarrying, "Baslangicta elde kargo olmamali.");

            int moneyBefore = root.Progress != null ? (int)root.Progress.Money : 0;

            // --- 3. Aracdan in ---
            courier.ExitVehicle(vanPoint.ExitSpot());
            yield return null;
            Assert.IsFalse(courier.IsInVehicle, "Arac birakildiktan sonra ayakta olmali.");

            // --- 4. Dukkan raftan kargo al ---
            Assert.IsTrue(pile.CanInteract(courier), "Rafta kargo varken etkilesim kurulamadi.");
            pile.Interact(courier);
            yield return null;

            Assert.Greater(courier.CarriedCount, 0,
                "Raftan kargo alinmadi. Oyuncu oynanamaz.");
            Assert.AreEqual(courier.CarriedCount, courier.CarriedParcels.Count,
                "Gorsel ile gercek kargo listesi ayristi.");

            int availableAfterPickup = root.Cargo.Manifest.AvailableCount;
            Assert.Greater(availableAfterPickup, 0,
                "Kargo raftan alinca listeden DUSMEMELI; kargo kaybolmamali.");

            // --- 5. Araca koy ---
            Assert.IsTrue(vanPoint.CanInteract(courier), "Arac noktasi etkilesime kapali.");
            vanPoint.Interact(courier);
            yield return null;

            Assert.AreEqual(0, courier.CarriedCount,
                "Arac noktasina E deyince el bosalmali.");
            Assert.Greater(root.Cargo.Manifest.LoadedCount, 0,
                "Hicbir kargo araca yuklenmedi. Oyuncu oynanamaz.");

            // --- 6. Araca bin (KRITIK ADIM) ---
            // Once burada ExitVehicle cagriliyordu ve oyuncu bir daha
            // binemiyordu. Bu adim kirmizi olmamali.
            // Not: E kargoyla ilgilenir, binme F tusudur. Test de
            // UIManager ile AYNI metodu (Board) cagirir.
            vanPoint.Board(courier);
            yield return null;
            Assert.IsTrue(courier.IsInVehicle,
                "Araca binilebilmeli. (Bu adim daha once arac birakmayi "
                + "cagriyordu ve oyun 2. adimda bitiyordu.)");

            // --- 7. Adrese git ---
            var delivery = root.Delivery;
            delivery.VehiclePosition = delivery.Route[0].Position;
            delivery.Tick(0.1f, false);
            yield return null;

            Assert.IsNotNull(delivery.CurrentStop,
                "Arac adrese vardiginda durak bildirilmedi. Rota: " + delivery.Route.Count);

            var stop = delivery.CurrentStop;
            int pendingAtStop = stop.PendingCount;
            Assert.Greater(pendingAtStop, 0, "Durakta bekleyen kargo yok.");

            // --- 8. Adreste arac birak, kargoyu al, kapidan teslim et ---
            courier.ExitVehicle(vanPoint.ExitSpot());
            yield return null;
            Assert.IsFalse(courier.IsInVehicle, "Adreste arac birakilmali.");

            // Arac -> kurye (KRITIK ADIM: bu yol hic yoktu)
            vanPoint.Interact(courier);
            yield return null;
            Assert.Greater(courier.CarriedCount, 0,
                "Araktan kargo alinamadi. Adres teslimi bu yoldan yapiliyor; "
                + "yol yoksa oyuncu HIC para kazanamaz.");

            // Teslim sayacini OLAYDAN tut. Kargo teslim edilince
            // listelerden CIKARILIR; listeleri tarayan sayac 0
            // gosterirdi ve gercek teslimi "basarisiz" sanardik.
            int deliveredCount = 0;
            System.Action<Cargo.Parcel, Delivery.DeliveryOutcome> onDelivered =
                (parcel, outcome) => deliveredCount++;
            Core.GameEvents.ParcelDelivered += onDelivered;

            try
            {
                // Kapiya yuruyor gibi yap: karakteri kapinin onune
                // tasiyip birkare bekle.
                courier.transform.position = door.InteractWorldPosition();
                yield return null;

                Assert.IsTrue(door.CanInteract(courier),
                    "Kapiya varildi ama etkilesim kurulamadi. Ipuclu: " + door.Prompt);

                door.Interact(courier);
                yield return null;

                Assert.AreEqual(1, deliveredCount,
                    "Kapida E'ye basinca kargo teslim OLAYI yayilmadi. "
                    + "Oyuncu para kazanamaz.");
            }
            finally
            {
                Core.GameEvents.ParcelDelivered -= onDelivered;
            }

            // --- 9. Sonuc: oyuncu para kazandi mi? ---
            int moneyAfter = root.Progress != null ? (int)root.Progress.Money : 0;
            Assert.Greater(moneyAfter, moneyBefore,
                "Kargo teslim edildi ama PARA ARTMADI. Oyuncu hic kazanamaz.");

            // El bosalmali: kurye kargoyu biraktigini GORSIN.
            Assert.AreEqual(0, courier.CarriedCount,
                "Teslimden sonra kuryenin eli bosalmali.");

            // Durak ilerlemis olmali.
            Assert.Less(stop.PendingCount, pendingAtStop,
                "Teslimden sonra durakta bekleyen kargo sayisi azalmali.");

            Debug.Log("[Kabul] Akis tamam: in -> al -> koy -> bin -> adres -> al -> teslim. "
                      + "Para: " + moneyBefore + " -> " + moneyAfter
                      + ", elde kalan: " + courier.CarriedCount);
        }

    }
}
