using System.Collections;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace KargoSim.Tests.PlayMode
{
    /// <summary>
    /// ASAMA 24 - VAN LASTIKLERI YERINDE DEGIL.
    ///
    /// Oyuncu fotograf gonderdi: yesil pickup'in (van) UZERINDE lastik
    /// yok; yerde iki lastik serbest duruyor, biri yatay. Bisiklet de
    /// kadrajda gorunmuyor.
    ///
    /// Bu dosya TEK SEY yapmaz, olcer: her iki aracin tum MeshRenderer
    /// cocuklarini ad, dunya konumu, araca gore uzaklik ve olcek olarak
    /// yazar. "Teker nerede" sorusu ancak veriyle cevaplanir.
    /// </summary>
    public class VehicleWheelDiagnosticTests
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Scene fresh = SceneManager.CreateScene("DiagCleanup");
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

        [UnityTest]
        public IEnumerator IkiAracinTumGorselParcalariNerede()
        {
            SceneManager.LoadScene(ScenePath, LoadSceneMode.Single);
            yield return null; yield return null; yield return null;
            for (int i = 0; i < 40; i++) yield return new WaitForFixedUpdate();

            var sb = new StringBuilder();

            // IVehicleController arayuzdur, Object degil: ture gore
            // bulunur.
            var roots = new System.Collections.Generic.List<Vehicle.IVehicleController>();
            roots.Add(Object.FindFirstObjectByType<Vehicle.BicycleController>());
            roots.Add(Object.FindFirstObjectByType<Vehicle.ArcadeVehicleController>());
            var vehicles = roots;

            sb.AppendLine("ARAC SAYISI: " + vehicles.Count);

            for (int v = 0; v < vehicles.Count; v++)
            {
                var root = vehicles[v].VehicleTransform;
                if (root == null) continue;
                sb.AppendLine("");
                sb.AppendLine("=== " + vehicles[v].GetType().Name + " (" + root.name + ") " +
                              "konum=" + root.position.ToString("F1"));
                sb.AppendLine("   kok olcek=" + root.lossyScale.ToString("F2") +
                              " kok rotasyon=" + root.eulerAngles.ToString("F0"));

                // MeshRenderer'li tum cocuklari yaz.
                var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
                sb.AppendLine("   MeshRenderer'li parca: " + renderers.Length);

                // Teker gibi duranlari (kureye yakin olcek) ayir.
                int wheelLike = 0;
                for (int i = 0; i < renderers.Length; i++)
                {
                    Transform t = renderers[i].transform;
                    Vector3 s = t.lossyScale;
                    bool round = (s.x > 0.5f && Mathf.Abs(s.x - s.y) < 0.3f) ||
                                 (s.x > 0.4f && Mathf.Abs(s.x - s.z) < 0.3f);
                    if (!round) continue;

                    wheelLike++;
                    float dist = Vector3.Distance(t.position, root.position);
                    sb.AppendLine("   * TEKER gibi: " + t.name +
                        " kok=" + (t.parent != null ? t.parent.name : "-") +
                        " konum=" + t.position.ToString("F1") +
                        " aracaUzaklik=" + dist.ToString("F2") +
                        " olcek=" + s.ToString("F2") +
                        " rot=" + t.eulerAngles.ToString("F0"));
                }
                sb.AppendLine("   teker benzeri parca sayisi: " + wheelLike);

                // WheelCollider var mi?
                var wcs = root.GetComponentsInChildren<WheelCollider>(true);
                sb.AppendLine("   WheelCollider: " + wcs.Length);
            }

            Debug.Log("[TASIMA RAPORU]\n" + sb);
            Assert.Pass("Taslama raporu yazildi.");
        }
    }
}
