using System.Collections;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace KargoSim.Tests.PlayMode
{
    /// <summary>
    /// ASAMA 20 - KAPASITE: "3 kutu araca sigmadi (kapasite)".
    ///
    /// Oyuncu konsola sunu yazdi:
    ///   [VanCargoPoint] 3 kutu araca sigmadi (kapasite); elde kaldi.
    ///
    /// Bisiklet 60 kg / 150 L. Kutu tipleri en buyugu ESYA: 3-8 kg,
    /// 15-45 L. 3 adet en buyuk kutu = 24 kg / 135 L -> SIGMASI gerekir.
    /// Yine sigmiyor. Bu dosya gercek sayilari yazar.
    /// </summary>
    public class CargoCapacityTests
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
            Scene fresh = SceneManager.CreateScene("CapCleanup");
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

            var points = Object.FindObjectsByType<Player.VanCargoPoint>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < points.Length; i++)
                if ((points[i].transform.position - _bike.transform.position).sqrMagnitude < 9f)
                    _bikePoint = points[i];
            Assert.IsNotNull(_bikePoint, "Bisiklet kargo noktasi yok.");
        }

        /// <summary>Bir kutu alir ve paketler; agir/likti yazilir.</summary>
        private IEnumerator TakeAndPackage()
        {
            _pile.Interact(_courier);
            yield return null;
            if (_courier.CarriedCount == 0) yield break;

            int b = _courier.CarriedCount - 1;
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
        }

        /// <summary>
        /// Kuryenin elde tutma siniri. `maxCarry` bir SerializeField; test
        /// icin reflection ile okunur (sabit yazilirsa alan degisince test
        /// yaniltir).
        /// </summary>
        private int CarryLimit
        {
            get
            {
                var f = typeof(Player.CourierController).GetField("maxCarry",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);
                if (f == null) return 3;
                return System.Convert.ToInt32(f.GetValue(_courier));
            }
        }

        [UnityTest]
        public IEnumerator BosBisikleteUcKutuSigar()
        {
            yield return LoadGame();
            _courier.ExitVehicle(Vector3.zero);
            yield return null;

            // Kapasite ve kutu tiplerini yaz.
            var cap = _root.Cargo.Manifest.Capacity;
            Debug.Log("[KAPASITE] bisiklet: " + cap.MaxWeightKg + " kg / " +
                      cap.MaxVolumeLiters + " L. Elde tutma siniri: " +
                      CarryLimit + " kutu.");

            var sb = new StringBuilder();
            float totalKg = 0f, totalL = 0f;

            for (int n = 0; n < 3; n++)
            {
                yield return TakeAndPackage();
                if (_courier.CarriedCount == 0) { sb.AppendLine("  kutu alinamadi"); break; }

                int idx = _courier.CarriedCount - 1;
                var p = _courier.CarriedParcels[idx];
                sb.AppendLine(string.Format("  kutu {0}: {1:0.0} kg / {2:0.0} L  (paketli={3})",
                    n + 1, p.WeightKg, p.VolumeLiters, p.IsPackaged));
                totalKg += p.WeightKg;
                totalL += p.VolumeLiters;
            }

            sb.AppendLine(string.Format("  TOPLAM: {0:0.0} kg / {1:0.0} L", totalKg, totalL));
            Debug.Log("[KAPASITE] alinan kutular:\n" + sb);

            int inHand = _courier.CarriedCount;
            Assert.GreaterOrEqual(inHand, 3,
                "Ucak kutu alinamadi: " + sb);

            _bikePoint.Interact(_courier);
            yield return null;

            int loaded = _root.Cargo.Manifest.LoadedCount;
            Debug.Log("[KAPASITE] yuklenen: " + loaded + "/" + inHand +
                      "  -> aile: " + _root.Cargo.Manifest.LoadedWeightKg.ToString("0.0") +
                      " kg / " + _root.Cargo.Manifest.LoadedVolumeLiters.ToString("0.0") + " L");

            Assert.AreEqual(inHand, loaded,
                "Bos bisiklete elindeki " + inHand + " kutu sigmadi.\n" + sb +
                "\nKapasite: " + cap.MaxWeightKg + " kg / " + cap.MaxVolumeLiters +
                " L. Oyuncu elinde kutu tutuyor ve arac kabul etmiyor - " +
                "bu oyun dongusunu kiriyor.");
        }

        [UnityTest]
        public IEnumerator KapasiteElindekiTumKutulariKarsilayacakKadar()
        {
            yield return LoadGame();
            _courier.ExitVehicle(Vector3.zero);
            yield return null;

            var cap = _root.Cargo.Manifest.Capacity;
            int carry = CarryLimit;

            // Depodaki EN BUYUK kutuyu referans al: oyuncunun tasiyabilecegi
            // en kotu el dolu olur.
            var types = Object.FindObjectsByType<Cargo.ParcelType>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            float worstKg = 0f, worstL = 0f;
            for (int i = 0; i < types.Length; i++)
            {
                if (types[i] == null) continue;
                worstKg = Mathf.Max(worstKg, types[i].maxWeightKg);
                worstL = Mathf.Max(worstL, types[i].maxVolumeLiters);
            }

            float needKg = worstKg * carry;
            float needL = worstL * carry;

            Assert.LessOrEqual(needKg, cap.MaxWeightKg,
                "En kotu el (" + carry + " x " + worstKg + " kg = " + needKg +
                " kg) bisiklet kapasitesini (" + cap.MaxWeightKg + " kg) asiyor. " +
                "Oyuncu el dolu hicbir zaman arac kabul etmiyor.");
            Assert.LessOrEqual(needL, cap.MaxVolumeLiters,
                "En kotu el (" + carry + " x " + worstL + " L = " + needL +
                " L) bisiklet kapasitesini (" + cap.MaxVolumeLiters + " L) asiyor. " +
                "Oyuncu el dolu hicbir zaman arac kabul etmiyor.");
        }
    }
}
