using System.Collections;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace KargoSim.Tests.PlayMode
{
    /// <summary>
    /// ASAMA 22 - BISIKLET DENGESi: "hafiften donuyor, yamuluyor".
    ///
    /// Oyuncu bu duzgun iki kez soylemisti. `ApplyStability` anti-roll'u
    /// SADECE tork olarak uyguluyor (hiz terimi yok) ve dik/durustur:
    ///
    ///     float lean = Vector3.Dot(right, Vector3.up);
    ///     float antiRoll = -lean * antiRollStrength;
    ///     body.AddTorque(transform.up * antiRoll, ForceMode.Acceleration);
    ///
    /// Bu, hiz bagimi olmayan bir YAY: gidince bir yone, geri bir yone
    /// iter. "Yamulma" dedigin sey buyuk ihtimalle bu salinim.
    ///
    /// Bu dosya yatma ACISINI ZAMANLA OLcer. Yalnizca "ne kadar yatti" degil,
    /// "SALINIM YAPIYOR MU" sorusuna cevap verir.
    /// </summary>
    public class BicycleStabilityTests
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";

        private Core.GameCompositionRoot _root;
        private Player.CourierController _courier;
        private Vehicle.BicycleController _bike;
        private Player.VanCargoPoint _bikePoint;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Scene fresh = SceneManager.CreateScene("StabCleanup");
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
            for (int i = 0; i < 60; i++) yield return new WaitForFixedUpdate();

            _root = Object.FindFirstObjectByType<Core.GameCompositionRoot>();
            _courier = Object.FindFirstObjectByType<Player.CourierController>();
            _bike = Object.FindFirstObjectByType<Vehicle.BicycleController>();
            Assert.IsNotNull(_bike, "Bisiklet yok.");

            var points = Object.FindObjectsByType<Player.VanCargoPoint>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < points.Length; i++)
                if ((points[i].transform.position - _bike.transform.position).sqrMagnitude < 9f)
                    _bikePoint = points[i];
        }

        /// <summary>Yalma acisi (derece): 0 = dik, pozitif = sag egimi.</summary>
        private static float Lean(Vehicle.BicycleController bike)
        {
            return Mathf.Asin(Mathf.Clamp(Vector3.Dot(bike.transform.right, Vector3.up), -1f, 1f))
                   * Mathf.Rad2Deg;
        }

        private IEnumerator Board()
        {
            _courier.ExitVehicle(Vector3.zero);
            yield return null;
            _bikePoint.Board(_courier);
            yield return null; yield return null;
            Assert.IsTrue(_courier.IsInVehicle, "Binilemedi.");
        }

        [UnityTest]
        public IEnumerator DuzYoldeBikeSallanmaz()
        {
            yield return LoadGame();
            yield return Board();

            _bike.SetTestInput(1f, 0f, 0f, 0f);

            float minLean = 0f, maxLean = 0f;
            var sb = new StringBuilder();

            for (int i = 0; i < 180; i++)
            {
                yield return new WaitForFixedUpdate();
                float l = Lean(_bike);
                if (i > 20) { minLean = Mathf.Min(minLean, l); maxLean = Mathf.Max(maxLean, l); }
                if (i % 30 == 0) sb.AppendLine("  t=" + i + " yatma=" + l.ToString("F1"));
            }
            _bike.SetTestInput(0f, 0f, 0f, 0f);

            Debug.Log("[DENGE] duz yol:\n" + sb);

            float swing = maxLean - minLean;
            Assert.Less(Mathf.Abs(swing), 6f,
                "Duz yolda arac " + swing.ToString("F1") + " derece saliniyor " +
                "(min " + minLean.ToString("F1") + ", max " + maxLean.ToString("F1") +
                "). Duz yolda hic yatmamasi gerekir - oyuncu 'yamuluyor' diyor.");
        }

        [UnityTest]
        public IEnumerator DonusYaparkenSallanmaz()
        {
            yield return LoadGame();
            yield return Board();

            // Gaz + sabit direksiyon: gercek donus.
            _bike.SetTestInput(0.6f, 1f, 0f, 0f);

            var samples = new System.Collections.Generic.List<float>();
            var sb = new StringBuilder();
            for (int i = 0; i < 200; i++)
            {
                yield return new WaitForFixedUpdate();
                samples.Add(Lean(_bike));
                if (i % 20 == 0) sb.AppendLine("  t=" + i + " yatma=" + Lean(_bike).ToString("F1"));
            }
            _bike.SetTestInput(0f, 0f, 0f, 0f);

            Debug.Log("[DENGE] donus:\n" + sb);

            // Son yarida salinim var mi? Art arda yon degisimi sayilir.
            int flips = 0;
            for (int i = 51; i < samples.Count; i++)
            {
                if (Mathf.Sign(samples[i]) != Mathf.Sign(samples[i - 1])) flips++;
            }

            float peak = 0f;
            for (int i = 50; i < samples.Count; i++) peak = Mathf.Max(peak, Mathf.Abs(samples[i]));

            Debug.Log("[DENGE] donus sonrasi salinim: " + flips +
                      " yon degisimi, tepe yatma " + peak.ToString("F1") + " derece");

            // ONCE esik 35 idi ve test 34 derecede GECTI - oysa 34 derece
            // tam da sorunun kendisiydi. Kargo bisikleti sert donuste en
            // fazla ~20 derece yatmalidir; ustunde kasadaki kutular kayar.
            Assert.Less(peak, 20f,
                "Donuste arac " + peak.ToString("F1") + " derece yatiyor. " +
                "Cargo bisikleti 20 dereceyi gecmemeli; oyuncu 'yamuluyor' " +
                "ve kutu duser. (Eski antiRollStrength=60 ile tepe 34 dereceydi.)");
            Assert.LessOrEqual(flips, 2,
                "Donus bitince yatma " + flips + " kez yon degistiriyor: bisiklet " +
                "OSUYOR. Anti-roll hiz bagimi olmayan bir yay; salinim yapiyor. " +
                "Oyuncu 'hafiften donuyor, yamuluyor' diyor.");
        }

        [UnityTest]
        public IEnumerator DurustuktenSonraDikeyler()
        {
            yield return LoadGame();
            yield return Board();

            // Yandan itilmis gibi bir yatma ver, birak.
            _bike.transform.rotation = Quaternion.Euler(0f, 0f, 20f);
            _bike.SetTestInput(0f, 0f, 0f, 0f);

            for (int i = 0; i < 300; i++) yield return new WaitForFixedUpdate();

            float rest = Lean(_bike);
            Debug.Log("[DENGE] 5 sn sonra yatma: " + rest.ToString("F1") + " derece");

            Assert.Less(Mathf.Abs(rest), 8f,
                "Arac birakildiktan 5 sn sonra hala " + rest.ToString("F1") +
                " derece yatil duruyor. Denge sistemi araci diklestirmiyor " +
                "(ya cok yumusak ya da yetersiz).");
        }
    }
}
