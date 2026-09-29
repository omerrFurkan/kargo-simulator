using System.Collections;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace KargoSim.Tests.PlayMode
{
    /// <summary>
    /// ASAMA 26 - VAN TEKERLERI GOVDE DISINDA.
    ///
    /// Oyuncu fotograf: lastikler artik DIK ama aracin DISINDA duruyor;
    /// teker yuvalari (wheel arch) bos kalmis.
    ///
    /// "Teker dogru yerde mi" sorusu ancak GOVDE SINIRLARINA karsilastirilarak
    /// cevaplanir: lastigin merkezi, govdenin yatay ayak izi (footprint)
    /// ICINDE olmali ve koselere yakin durmali.
    /// </summary>
    public class VanWheelPlacementTests
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";

        private GameObject _van;
        private Vector3 _bodyMin, _bodyMax;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Scene fresh = SceneManager.CreateScene("PlaceCleanup");
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
            for (int i = 0; i < 40; i++) yield return new WaitForFixedUpdate();

            var van = Object.FindFirstObjectByType<Vehicle.ArcadeVehicleController>();
            Assert.IsNotNull(van, "Van yok.");
            _van = van.gameObject;

            // Govde: en buyuk MeshRenderer parcasi (pickup govdesi).
            var rends = _van.GetComponentsInChildren<MeshRenderer>(true);
            float bestVolume = -1f;
            Vector3 min = Vector3.zero, max = Vector3.zero;
            for (int i = 0; i < rends.Length; i++)
            {
                if (rends[i].GetComponent<KargoSim.Vehicle.WheelVisual>() != null) continue;
                if (rends[i].name.ToLower().Contains("wheel")) continue;

                Bounds b = rends[i].bounds;
                float v = b.size.x * b.size.y * b.size.z;
                if (v <= bestVolume) continue;

                bestVolume = v;
                min = b.min; max = b.max;
            }
            _bodyMin = min; _bodyMax = max;
        }

        [UnityTest]
        public IEnumerator GovdeSinirlariVeTekerKonumlari()
        {
            yield return LoadGame();

            var sb = new StringBuilder();
            sb.AppendLine("VAN GOVDE (dunya sinirlari):");
            sb.AppendLine("  min = " + _bodyMin.ToString("F2"));
            sb.AppendLine("  max = " + _bodyMax.ToString("F2"));
            sb.AppendLine("  olcu = " + (_bodyMax - _bodyMin).ToString("F2") +
                          "   (X=en, Y=boy, Z=uzunluk)");
            sb.AppendLine("  arac kok = " + _van.transform.position.ToString("F2") +
                          " olcek = " + _van.transform.lossyScale.ToString("F2"));

            var visuals = _van.GetComponentsInChildren<KargoSim.Vehicle.WheelVisual>(true);
            sb.AppendLine("TEKERLER:");
            for (int i = 0; i < visuals.Length; i++)
            {
                var wc = visuals[i].transform.GetComponentInParent<WheelCollider>();
                Vector3 p = visuals[i].transform.position;

                bool insideX = p.x >= _bodyMin.x && p.x <= _bodyMax.x;
                bool insideZ = p.z >= _bodyMin.z && p.z <= _bodyMax.z;
                float outX = p.x < _bodyMin.x ? _bodyMin.x - p.x
                          : (p.x > _bodyMax.x ? p.x - _bodyMax.x : 0f);
                float outZ = p.z < _bodyMin.z ? _bodyMin.z - p.z
                          : (p.z > _bodyMax.z ? p.z - _bodyMax.z : 0f);

                sb.AppendLine("  " + (wc != null ? wc.name : "?") +
                    " konum=" + p.ToString("F2") +
                    " govdeDisi=" + (Mathf.Max(outX, outZ)).ToString("F2") + " m" +
                    (insideX && insideZ ? "  [ICINDE]" : "  [DISINDA]") +
                    " tekerYaricap=" + (wc != null ? wc.radius.ToString("F2") : "?") +
                    " aski=" + (wc != null ? wc.suspensionDistance.ToString("F2") : "?"));
            }
            Debug.Log("[VAN YERLESIM]\n" + sb);
            Assert.Pass("Yerlesim raporu yazildi.");
        }

        [UnityTest]
        public IEnumerator TekerlerGovdeninIcinde()
        {
            yield return LoadGame();

            var visuals = _van.GetComponentsInChildren<KargoSim.Vehicle.WheelVisual>(true);
            int kontrol = 0;
            for (int i = 0; i < visuals.Length; i++)
            {
                kontrol++;
                Vector3 p = visuals[i].transform.position;
                float outX = p.x < _bodyMin.x ? _bodyMin.x - p.x
                          : (p.x > _bodyMax.x ? p.x - _bodyMax.x : 0f);
                float outZ = p.z < _bodyMin.z ? _bodyMin.z - p.z
                          : (p.z > _bodyMax.z ? p.z - _bodyMax.z : 0f);
                float dis = Mathf.Max(outX, outZ);

                Assert.Less(dis, 0.35f,
                    "Teker govdenin " + dis.ToString("F2") +
                    " m DISINDA (konum=" + p.ToString("F2") + ", govde " +
                    _bodyMin.ToString("F1") + " .. " + _bodyMax.ToString("F1") +
                    "). Teker yuvalari bos kalmis; lastikler yerde duruyor.");
            }
            Assert.Greater(kontrol, 0, "Vanda teker bulunamadi.");
        }
    }
}
