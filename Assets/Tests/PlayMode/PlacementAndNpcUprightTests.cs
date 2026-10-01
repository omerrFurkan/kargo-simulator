using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using KargoSim.Cargo;

namespace KargoSim.Tests.PlayMode
{
    /// <summary>
    /// OYUNCUNUN IKI SIKAYETI + EKRAN GORUNTUSU:
    ///   1) "KARGOYU KOYDUÃ„ÂUMDA Ãƒâ€“N KALDIRIP KENDÃ„Â° ETRAFINDA DÃƒâ€“NÃƒÅ“YOR GÃ„Â°DÃ„Â°YOR"
    ///   2) "NPC YAMUK DURANLAR VAR - DÃƒÅ“KKANIN YANINDAKÃ„Â°LER"
    ///
    /// Bu test ikisini de OLCEER, gozle degil.
    /// </summary>
    public class PlacementAndNpcUprightTests
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";
        private Core.GameCompositionRoot _root;
        private Player.CourierController _courier;
        private CargoCarryController _carry;
        private CargoWorldRegistry _world;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (SceneManager.GetSceneByName("FixCleanup").IsValid()) yield break;
            Scene f = SceneManager.CreateScene("FixCleanup");
            SceneManager.SetActiveScene(f);
            yield return null;
            Scene g = SceneManager.GetSceneByName("Game");
            if (g.IsValid() && g.isLoaded)
            {
                AsyncOperation u = SceneManager.UnloadSceneAsync(g);
                while (!u.isDone) yield return null;
            }
            yield return null;
            Core.GameEvents.ResetForTests();
        }

        private IEnumerator LoadGame()
        {
            SceneManager.LoadScene(ScenePath, LoadSceneMode.Single);
            yield return null; yield return null; yield return null;
            for (int i = 0; i < 25; i++) yield return new WaitForFixedUpdate();

            _root = UnityEngine.Object.FindFirstObjectByType<Core.GameCompositionRoot>();
            _courier = UnityEngine.Object.FindFirstObjectByType<Player.CourierController>();
            _carry = _courier.Carry;
            _world = CargoWorldRegistry.Instance;
            _root.Cargo.BeginDay(1);
        }

        // ==================================================================
        // 1) NPC'LER DIK DURMALI
        // ==================================================================
        [UnityTest]
        public IEnumerator DukkanMusterileriDikDuruyor()
        {
            yield return LoadGame();
            yield return null; yield return null;

            var customers = UnityEngine.Object.FindObjectsByType<Player.ShopCustomer>(
                FindObjectsSortMode.None);
            Assert.Greater(customers.Length, 0, "Dukkan musterisi yok.");

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < customers.Length; i++)
            {
                float tilt = Vector3.Angle(customers[i].transform.up, Vector3.up);
                sb.AppendLine("[Tilt] " + customers[i].name + " = "
                    + tilt.ToString("F1") + " derece");

                Assert.Less(tilt, 8f,
                    customers[i].name + " " + tilt.ToString("F0")
                    + " derece YATIK. Oyuncu 'dukkanin yaninda yamuk duran "
                    + "NPC'ler var' diyor. Sebep: musteri bakisi "
                    + "`(origin - pos) + Vector3.up` ile kuruluyordu; "
                    + "yatay vektore yukari eklenince 45 derece egim oluyor.");
            }
            TestContext.WriteLine(sb.ToString());
        }

        // ==================================================================
        // 2) BIRAKILAN KUTU KAYMAMALI / DÃƒâ€“NMEMELÃ„Â°
        // ==================================================================
        [UnityTest]
        public IEnumerator BirakilanKutuKaymiyorDonmuyor()
        {
            yield return LoadGame();

            var depot = _root.Cargo.Manifest.Available;
            Assert.Greater(depot.Count, 0, "Kargo yok.");

            Vector3 at = _courier.transform.position
                       + _courier.transform.forward * 1.4f;
            ParcelWorldObject box = _world.GetOrCreate(depot[0], at);
            yield return null;

            Assert.IsTrue(_carry.TryPickup(box, _courier.GetComponent<Collider>()),
                "Kutu alinamadi.");
            yield return null;
            Assert.IsTrue(_carry.TryDrop(), "Birakma basarisiz. " + _carry.LastMessage);

            // 1 saniye fizik
            Vector3 p0 = box.transform.position;
            Quaternion r0 = box.transform.rotation;
            yield return new WaitForSeconds(1f);
            for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();

            Vector3 p1 = box.transform.position;
            float drift = Vector3.Distance(p0, p1);
            float spin = Quaternion.Angle(r0, box.transform.rotation);

            TestContext.WriteLine("[Olcum] 1 sn icinde kayma = "
                + drift.ToString("F3") + " m, donme = " + spin.ToString("F1")
                + " derece");

            Assert.Less(drift, 0.35f,
                "BÃ„Â±rakÃ„Â±lan kutu " + drift.ToString("F2")
                + " m KAYDI. Oyuncu 'kargoyu koyduÃ„Å¸umda ÃƒÂ¶n kaydÃ„Â±p kendi "
                + "etrafÃ„Â±nda dÃƒÂ¶nÃƒÂ¼yor gidiyor' diyor. Sebep: kutu merkezi "
                + "yÃƒÂ¼zeye +0.02 m ile konuluyordu, yani dibi zemine "
                + "GÃƒâ€“MÃƒÅ“LÃƒÅ“ kalÃ„Â±yor; fizik dÃ„Â±Ã…Å¸arÃ„Â± fÃ„Â±rlatÃ„Â±yor.");

            Assert.Less(spin, 25f,
                "BÃ„Â±rakÃ„Â±lan kutu " + spin.ToString("F0")
                + " derece dÃƒÂ¶ndÃƒÂ¼. 'Kendi etrafÃ„Â±nda dÃƒÂ¶nÃƒÂ¼yor'.");

            // Zemine gomulmemeli (#131)
            float bottom = box.transform.position.y - box.BoxSize.y * 0.5f;
            TestContext.WriteLine("[Olcum] kutu alt y = " + bottom.ToString("F2"));
        }

        [UnityTest]
        public IEnumerator BirakilanKutuZemineGomulmuyor()
        {
            yield return LoadGame();

            var depot = _root.Cargo.Manifest.Available;
            Vector3 at = _courier.transform.position
                       + _courier.transform.forward * 1.4f;
            ParcelWorldObject box = _world.GetOrCreate(depot[0], at);
            yield return null;
            _carry.TryPickup(box, _courier.GetComponent<Collider>());
            yield return null;
            Assert.IsTrue(_carry.TryDrop(), "Birakma basarisiz.");
            yield return new WaitForSeconds(1f);

// Kutu altinin altindaki zemin
            // DIKKAT: isin kutunun ALTINDAN baslamali. Ustunden baslayinca
            // kutunun kendi tepesine isabet ediyor ve "gomulme" olcumum
            // yanlislikla TAM KUTU YUKSEKLIGI cikiyordu (0.32 m).
            float groundY = float.NaN;
            Vector3 under = box.transform.position
                           + Vector3.down * (box.BoxSize.y * 0.5f + 0.25f);
            if (Physics.Raycast(under + Vector3.up * 1.0f, Vector3.down,
                    out RaycastHit h, 3f, ~0, QueryTriggerInteraction.Ignore))
                groundY = h.point.y;
            else
                Assert.Fail("Kutunun altinda zemin bulunamadi.");

            float bottom = box.transform.position.y - box.BoxSize.y * 0.5f;
            float sink = float.IsNaN(groundY) ? 0f : groundY - bottom;
            TestContext.WriteLine("[Olcum] gomulme = " + sink.ToString("F3") + " m");

            Assert.Less(sink, 0.12f,
                "Kutu zemine " + sink.ToString("F2")
                + " m gÃƒÂ¶mÃƒÂ¼lÃƒÂ¼. Oyun kabul kriteri: 'kutu yerde yarÃ„Â±m "
                + "gÃƒÂ¶mÃƒÂ¼lÃƒÂ¼ olmamalÃ„Â±'.");
        }
    }
}
