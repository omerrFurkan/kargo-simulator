using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace KargoSim.Tests.PlayMode
{
    /// <summary>
    /// ASAMA 23 - TEKER GORSELI: "tekerlerin hali ne".
    ///
    /// Oyuncu fotograf gonderdi: tekerler yerden yuksekte degil, YERE
    /// YATIK duruyor ve aracdan kopmus gorunuyordu.
    ///
    /// Sebep `WheelVisual`'da: mesh'in rotasyonu her kare yalnizca fizik
    /// pozundan yaziliyordu. Unity silindirinin ekseni local Y'dir ve
    /// teker ancak 90 derece dondurulunce dik durur; uretimde
    /// `Euler(0,0,90)` ile kuruluyordu ama bu ofset her kare SILINIYORDU.
    ///
    /// Bu test tekerin AKSINI olcer: lastik silindirinin uzun ekseni
    /// (mesh'in local up'i) arac dikey eksenine ne kadar paralel? Dik
    /// duran bir tekende bu ~0 (yatay), yatan tekende ~1 (dikey).
    /// </summary>
    public class WheelVisualOrientationTests
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";

        private Vehicle.BicycleController _bike;
        private Player.CourierController _courier;
        private Player.VanCargoPoint _bikePoint;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Scene fresh = SceneManager.CreateScene("WheelCleanup");
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
            _bike = Object.FindFirstObjectByType<Vehicle.BicycleController>();
            _courier = Object.FindFirstObjectByType<Player.CourierController>();
            Assert.IsNotNull(_bike, "Bisiklet yok.");

            var points = Object.FindObjectsByType<Player.VanCargoPoint>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < points.Length; i++)
                if ((points[i].transform.position - _bike.transform.position).sqrMagnitude < 9f)
                    _bikePoint = points[i];
        }

        /// <summary>Teker mesh'inin dunya ekseni; 0 = yatay (dik teker), 1 = dikey (yatan teker).</summary>
        private static float AxleVerticality(Transform mesh, Vector3 vehicleUp)
        {
            return Mathf.Abs(Vector3.Dot(mesh.up, vehicleUp));
        }

        [UnityTest]
        public IEnumerator BisikletTekerleriDikDuruyor()
        {
            yield return LoadGame();

            var visuals = _bike.GetComponentsInChildren<Vehicle.WheelVisual>(true);
            Assert.AreEqual(3, visuals.Length,
                "Bisiklette 3 teker gorseli olmali, " + visuals.Length + " var.");

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("arac up    = " + _bike.transform.up);
            for (int i = 0; i < visuals.Length; i++)
            {
                var mesh = visuals[i].transform;
                var wc = mesh.parent != null
                    ? mesh.parent.GetComponent<WheelCollider>()
                    : null;

                float v = AxleVerticality(mesh, _bike.transform.up);
                sb.AppendLine(mesh.name +
                    " | mesh.up=" + mesh.up.ToString("F2") +
                    " mesh.right=" + mesh.right.ToString("F2") +
                    " | ince eksen (mesh.up) dikeyden " + (v * 90f).ToString("F0") + " derece" +
                    (wc != null
                        ? " | collider.right=" + wc.transform.right.ToString("F2") +
                          " collider.up=" + wc.transform.up.ToString("F2")
                        : " | collider YOK"));
            }
            Debug.Log("[TEKER OLCUM]\n" + sb);

            for (int i = 0; i < visuals.Length; i++)
            {
                var mesh = visuals[i].transform;
                float v = AxleVerticality(mesh, _bike.transform.up);
                Assert.Less(v, 0.30f,
                    mesh.name + " yatay duruyor (eksen dikeyden " +
                    (v * 90f).ToString("F0") + " derece sapmis). Teker yere " +
                    "yatmis gorunuyor - oyuncunun fotografindaki sorun bu.");
            }
        }

        [UnityTest]
        public IEnumerator TekerlerAracinYanindaKalir()
        {
            yield return LoadGame();

            var visuals = _bike.GetComponentsInChildren<Vehicle.WheelVisual>(true);
            // Yaricap: bisiklet tekeri 0.32 m. Mesh, fizik noktasindan
            // yaricap kadar uzakta olabilir (suspension) ama asla aracin
            // onunun 1 m otesinde degil.
            for (int i = 0; i < visuals.Length; i++)
            {
                float d = Vector3.Distance(visuals[i].transform.position,
                                          _bike.transform.position);
                Assert.Less(d, 3f,
                    visuals[i].name + " arac gorevdesinden " + d.ToString("F1") +
                    " m uzakta. Tekerler aracdan kopmus - oyuncunun " +
                    "fotografinda sol tarafta havada duruyorlardi.");
            }
        }

        [UnityTest]
        public IEnumerator TekerlerYerdenDogruDegilAyakta()
        {
            yield return LoadGame();
            for (int i = 0; i < 60; i++) yield return new WaitForFixedUpdate();

            var visuals = _bike.GetComponentsInChildren<Vehicle.WheelVisual>(true);
            for (int i = 0; i < visuals.Length; i++)
            {
                Vector3 p = visuals[i].transform.position;
                float above = p.y - _bike.transform.position.y;
                // Teker merkezi arac tabaninin biraz ustunde olmali
                // (yaricap kadar). Cok yuksek = havada, cok asagi = gomulmus.
                Assert.Less(above, 2.5f,
                    visuals[i].name + " yerden " + above.ToString("F1") +
                    " m yuksekte; teker havada duruyor.");
                Assert.Greater(above, -0.6f,
                    visuals[i].name + " arac tabaninin " + above.ToString("F1") +
                    " m ALTINDA; teker yere gomulmus.");
            }
        }
    }
}
