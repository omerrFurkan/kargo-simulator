using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;

namespace KargoSim.EditorTools
{
    /// <summary>
    /// OYUNUNU GORMEK ICIN.
    ///
    /// Bu projede gorsel degisiklikler hep tahminle yapildi ve
    /// cogu yanlis cikti (karakter 0.57 m zemine gomuluymus gibi).
    /// Bu arac sahneyi yukleyip kamera ile RESIM alir; boylece
    /// "daha guzel" tanimi olcumle tartilir.
    ///
    /// Kullanim:
    ///   Unity.exe -batchmode -projectPath . -executeMethod
    ///     KargoSim.EditorTools.ScreenshotTool.ShootAll
    ///
    /// -nographics BAYILIR: resim almak icin grafik gerekir.
    /// </summary>
    public static class ScreenshotTool
    {
        private const string OutDir = @"C:\Users\Furkan\AppData\Local\Temp\opencode\shots";
        private const int Width = 1600;
        private const int Height = 900;

        [MenuItem("Kargo/Ekran Goruntusu Al")]
        public static void ShootAll()
        {
            Directory.CreateDirectory(OutDir);

            Scene scene = EditorSceneManager.OpenScene(
                "Assets/Scenes/Game.unity", OpenSceneMode.Single);

            // Sahne acilinca oyun mantigi calismaz; ama Awake/Start
            // cagrilmiyorsa kurye vs. yerinde degil. Bu yuzden
            // PlayMode'a girmiyoruz; kamera ile geziyoruz.
            if (Application.isPlaying)
            {
                Debug.LogError("ScreenshotTool: PlayMode'da calisiyor, cik.");
                EditorApplication.Exit(2);
                return;
            }

            // Sahne acilinca runtime kodlari CALISMAZ. Gorsel katmani
            // elle kuruyoruz, yoksa eski (duz kutu) hali cikar.
            var visGo = new GameObject("__cityvis");
            var vis = visGo.AddComponent<KargoSim.World.CityVisualUpgrade>();
            vis.Apply();

            // NPC figuru: musteriler yalnizca OYUN MODUNDA doguyor,
            // editorde ortada hic sey yok. Onlar da burada uretilir.
            for (int i = 0; i < 3; i++)
            {
                GameObject n = vis.PreviewHuman(i, "__npc_" + i);
                n.transform.position = new Vector3(3.2f + i * 0.75f, 0f, 2.0f);
                n.transform.rotation = Quaternion.Euler(0f, 200f + i * 25f, 0f);
            }

            var shots = new[]
            {
                new Shot("01_depo_genel", new Vector3(14f, 9f, 20f), new Vector3(0f, 1.5f, -4f), 55f),
                new Shot("02_dukan_yakin", new Vector3(0f, 3.2f, 8f), new Vector3(0f, 1.6f, -7f), 50f),
                new Shot("03_sokak", new Vector3(0f, 4.5f, 30f), new Vector3(0f, 1.0f, 55f), 55f),
                new Shot("04_evler", new Vector3(40f, 8f, 30f), new Vector3(55f, 2f, 0f), 55f),
                new Shot("05_kurye_yakin", new Vector3(3.6f + 3f, 1.9f, 1.2f + 3f), new Vector3(3.6f, 1.0f, 1.2f), 45f),
                new Shot("06_npc_uzak", new Vector3(3.9f, 1.5f, 6.2f), new Vector3(3.9f, 1.0f, 2.0f), 40f),
                new Shot("07_npc_yakin", new Vector3(3.9f, 1.35f, 3.6f), new Vector3(3.9f, 1.05f, 2.0f), 45f),
            };

            foreach (Shot s in shots) Shoot(scene, s);

            Debug.Log("[ScreenshotTool] " + shots.Length + " gorsuntu alindi -> " + OutDir);
            EditorApplication.Exit(0);
        }

        private struct Shot
        {
            public string Name;
            public Vector3 From;
            public Vector3 At;
            public float Fov;
            public Shot(string n, Vector3 f, Vector3 a, float fov)
            { Name = n; From = f; At = a; Fov = fov; }
        }

        private static void Shoot(Scene scene, Shot shot)
        {
            var camGo = new GameObject("__shot_cam");
            Camera cam = camGo.AddComponent<Camera>();
            cam.transform.position = shot.From;
            cam.transform.LookAt(shot.At);
            cam.fieldOfView = shot.Fov;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 400f;
            cam.clearFlags = CameraClearFlags.Skybox;

            // URP ek kamera verisi olmadan post-process/tonemap
            // uygulanmaz; otomatik eklenmiyor, biz ekliyoruz.
            var urp = camGo.AddComponent<UniversalAdditionalCameraData>();
            urp.renderPostProcessing = true;
            urp.antialiasing = AntialiasingMode.FastApproximateAntialiasing;

            // Sahnedeki Directional Light'i bulmazsa ekle: yoksa
            // resim tamamen siyah cikar ve "karanlik oyun" sanilir.
            Light sun = UnityEngine.Object.FindFirstObjectByType<Light>();
            if (sun == null)
            {
                var l = new GameObject("__shot_sun");
                sun = l.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1.2f;
                sun.color = new Color(1f, 0.96f, 0.9f);
                l.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }

            RenderTexture rt = new RenderTexture(Width, Height, 24,
                                                RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;
            cam.targetTexture = rt;

            // Sahne "kirli" olabilir; Camera.Render() elle cagirinca
            // lighting/globals hazir olmayabilir.
            EditorSceneManager.MarkSceneDirty(scene);
            var prevAmbient = RenderSettings.ambientMode;

            cam.Render();

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            string path = Path.Combine(OutDir, shot.Name + ".png");
            File.WriteAllBytes(path, tex.EncodeToPNG());

            cam.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(tex);
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(camGo);

            Debug.Log("[ScreenshotTool] " + path);
        }
    }
}
