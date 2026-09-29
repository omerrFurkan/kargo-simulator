using System.Collections;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace KargoSim.Tests.PlayMode
{
    /// <summary>
    /// ASAMA 25 - TEKER AKSINI MODELDEN BAGIMSIZ OLMEK.
    ///
    /// Onceki olcümler tekerin "ince ekseni"ne bakiyordu. Bu yalnizca
    /// Unity SILINDIR primitifinde calisir; vanin Kenney modelinde
    /// calismaz. Sonuc: vanin yatmis tekeri icin dogru ofset bulunamadi.
    ///
    /// BURADA: bir teker, AKSESI (en ince ekseni) etrafinda donerli
    /// simetrik bir gosedir. Mesh'in YEREL sinirlarindan en kucuk eksen
    /// bulunur; dunyada karsi karsigi aracin DIKEY eksenine dik olmalidir.
    ///
    /// Bu yontem modelden bagimsizdir: silindir, torus, Kenney tekeri -
    /// hepsinde calisir.
    /// </summary>
    public class WheelAxleOrientationTests
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";

        private GameObject _bikeGo;
        private GameObject _vanGo;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Scene fresh = SceneManager.CreateScene("AxleCleanup");
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

            var bike = Object.FindFirstObjectByType<Vehicle.BicycleController>();
            var van = Object.FindFirstObjectByType<Vehicle.ArcadeVehicleController>();
            Assert.IsNotNull(bike, "Bisiklet yok.");
            Assert.IsNotNull(van, "Van yok.");
            _bikeGo = bike.gameObject;
            _vanGo = van.gameObject;
        }

        /// <summary>
        /// Mesh'in yerel sinirlarina bakip en KUCUK ekseni bulur: bu
        /// tekerin aksidir. Donmus yon de donmus bir vektor dondurur.
        ///
        /// ONEMLI: sinirlar mesh'in KENDI olculeridir, obje olcegi
        /// UYGULANMAMISTIR. Bisikletin lastigi silindir primitivi
        /// (sinir 1,2,1) ama objenin olcegi (0.64, 0.06, 0.64) ile
        /// gercek kalinligi 0.12 m olur. Olcek hesaba katilmazsa en ince
        /// eksen yanlis (Y) secilir ve aks yanlis cikar.
        /// </summary>
        private static Vector3 AxleWorldDirection(GameObject mesh)
        {
            var filter = mesh.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return Vector3.zero;

            Vector3 raw = filter.sharedMesh.bounds.size;
            Vector3 scale = mesh.transform.lossyScale;

            Vector3 size = new Vector3(raw.x * scale.x, raw.y * scale.y, raw.z * scale.z);

            Vector3 local = Vector3.right;
            if (size.y <= size.x && size.y <= size.z) local = Vector3.up;
            else if (size.x <= size.z) local = Vector3.right;
            else local = Vector3.forward;

            return mesh.transform.TransformDirection(local).normalized;
        }

        /// <summary>1 = aks dikey (teker yatmis), 0 = dik (dogru).</summary>
        private static float Verticality(GameObject mesh, Transform vehicle)
        {
            Vector3 axle = AxleWorldDirection(mesh);
            if (axle.sqrMagnitude < 0.5f) return 0f;   // mesh yok: olcum yapamaz
            return Mathf.Abs(Vector3.Dot(axle, vehicle.up));
        }

        private void Rapor(string etiket, GameObject vehicleGo)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== " + etiket + " arac up=" + vehicleGo.transform.up.ToString("F2"));

            // WheelVisual, WheelCollider'in kendisinde degil MESH COCUGUNDA
            // durur (uretimde `visual.AddComponent<WheelVisual>()`). Bu yuzden
            // collider'in uzerinde aramak hicbir sey bulmaz; gorseller
            // gezilir, teker kendi ebeveyninden bulunur.
            var visuals = vehicleGo.GetComponentsInChildren<KargoSim.Vehicle.WheelVisual>(true);
            sb.AppendLine("   WheelVisual sayisi: " + visuals.Length);

            for (int c = 0; c < visuals.Length; c++)
            {
                var mesh = visuals[c].transform;
                var wc = mesh.GetComponentInParent<WheelCollider>();
                string wcName = wc != null ? wc.name : "?";

                Vector3 size = Vector3.one;
                var f = mesh.GetComponent<MeshFilter>();
                if (f != null && f.sharedMesh != null) size = f.sharedMesh.bounds.size;

                float v = Verticality(mesh.gameObject, vehicleGo.transform);
                sb.AppendLine("   " + wcName +
                    " aks=" + AxleWorldDirection(mesh.gameObject).ToString("F2") +
                    " dikeydenSapma=" + (v * 90f).ToString("F0") + " derece" +
                    " meshSinir=" + size.ToString("F2") +
                    " meshOlcek=" + mesh.lossyScale.ToString("F2") +
                    " yerelRot=" + mesh.localRotation.eulerAngles.ToString("F0"));
            }
            Debug.Log("[AKS OLCUM]\n" + sb);
        }

        [UnityTest]
        public IEnumerator IkiAracinTekerAksiOlculur()
        {
            yield return LoadGame();

            Rapor("BISIKLET", _bikeGo);
            Rapor("VAN", _vanGo);
            Assert.Pass("Aks raporu yazildi.");
        }

        [UnityTest]
        public IEnumerator VanTekerAksiYatay()
        {
            yield return LoadGame();
            Rapor("VAN", _vanGo);

            var visuals = _vanGo.GetComponentsInChildren<KargoSim.Vehicle.WheelVisual>(true);
            int kontrol = 0;
            for (int c = 0; c < visuals.Length; c++)
            {
                kontrol++;
                var wc = visuals[c].transform.GetComponentInParent<WheelCollider>();
                string ad = wc != null ? wc.name : visuals[c].name;

                float v = Verticality(visuals[c].transform.gameObject, _vanGo.transform);
                Assert.Less(v, 0.30f,
                    ad + " aksi arac dikeyine " + (v * 90f).ToString("F0") +
                    " derece paralel -> teker YATIK. Vanin lastikleri yerde " +
                    "serbest duruyor; aracin uzerinde lastik yok.");
            }
            Assert.Greater(kontrol, 0, "Vanda WheelVisual bulunamadi.");
        }
    }
}
