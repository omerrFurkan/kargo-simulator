using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using KargoSim.Player;
using KargoSim.Vehicle;
using UnityEditor;
using UnityEngine;

namespace KargoSim.EditorTools
{
    /// <summary>
    /// Kenney "Car Kit" (CC0) hazir modelini projeye alir ve kurye
    /// aracini bu modelle yeniden uretir.
    ///
    /// Neden ayri bir arac: primitive kutu arac oyuncuya "prototip"
    /// hissi veriyordu. Model degistiginde fizik (Rigidbody, WheelCollider,
    /// sasi collider'i) ve baglantilar (definition, girdi) aynen
    /// korunur; yalnizca GORSEL cocuklar degisir.
    ///
    /// Neden "truck": once secilen model KAPALI kasa bir kurye van'iydi.
    /// Arkasina kutu konulabiliyordu ama oyuncu onu GOREMIYORDU: first
    /// person kamera kabindeydi ve kutu kasa icinde gizliydi. Kenney
    /// Car Kit'teki "truck" bir PICKUP'tir - arka kasi acik, kutular
    /// gorsunur. Simdi kutu yukleme somut bir islem.
    ///
    /// Lisans: Kenney Car Kit, Creative Commons CC0. Ticari kullanima
    /// acik, atif zorunlu degil. Lisans metni License.txt olarak
    /// projeye kopyalanir.
    /// </summary>
    public static class KenneyAssetSetup
    {
        private const string KitRoot = "Assets/Art/Kenney/CarKit";
        private const string TruckFbx = KitRoot + "/Models/truck.fbx";
        private const string ColormapPath = KitRoot + "/Models/Textures/colormap.png";
        private const string MaterialPath = "Assets/Art/Materials/KenneyCarKit.mat";

        private const string VehicleDefPath =
            "Assets/ScriptableObjects/Definitions/VehicleDefinition_CourierVan.asset";
        private const string PrefabPath = "Assets/Prefabs/PlayerVehicle.prefab";

        private const string DownloadPage = "https://kenney.nl/assets/car-kit";

        private const int LayerVehicle = 11;

        /// <summary>
        /// Modelin hedef boyu (metre). Gorsel model, dingil olcekleri ve
        /// teker yaricaplari bu tek degere gore oranlanir; boylece fizik
        /// ile model birbirine uyar.
        /// </summary>
        private const float TargetLengthMeters = 4.2f;

        private struct WheelSlot
        {
            public Transform visual;
            public WheelCollider collider;
        }

        // =================== GECICI OLcum araci (olcum sonrasi silinecek) =====

        [MenuItem("Kargo/Asset/GEcici Model Olcum")]
        public static void MeasureModelsForSetup()
        {
            MeasureModel(KitRoot + "/Models/truck.fbx");
            MeasureModel(KitRoot + "/Models/wheel-truck.fbx");
        }

        private static void MeasureModel(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            bool wasReadable = importer != null && importer.isReadable;

            try
            {
                if (importer != null && !wasReadable)
                {
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                }

                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    Debug.LogError("OLCUM: yuklenemedi " + path);
                    return;
                }

                var host = new GameObject("MeasureHost");
                GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                model.name = "M";
                model.transform.SetParent(host.transform, false);

                var names = new System.Text.StringBuilder();
                foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
                {
                    if (t == model.transform) continue;
                    names.Append(t.name).Append(" | ");
                }

                List<Transform> wheels = FindWheelTransforms(model);
                TryGetMeshBounds(model, out Bounds raw);
                string rawSize = raw.size.ToString("0.0000");

                FitModel(model, new StringBuilder());
                TryGetMeshBounds(model, out Bounds fitted);
                Bounds b = fitted;

                var report = new StringBuilder();
                report.AppendLine("### " + path);
                report.AppendLine("cocuklar : " + names);
                report.AppendLine("teker    : " + wheels.Count);
                report.AppendLine("ham      : " + rawSize + "  center " + raw.center.ToString("0.000"));
                report.AppendLine("olcek    : " + model.transform.localScale.x.ToString("0.0000"));
                report.AppendLine("nihai    : " + b.size.ToString("0.000") +
                                  "  min " + b.min.ToString("0.000") +
                                  "  max " + b.max.ToString("0.000"));
                ReportDeckSurfaces(model, b, report);

                Debug.Log(report.ToString());
                Object.DestroyImmediate(host);
            }
            finally
            {
                if (importer != null && !wasReadable)
                {
                    importer.isReadable = false;
                    importer.SaveAndReimport();
                }
            }
        }

        /// <summary>
        /// Govde mesh'indeki YUKARI bakan yatay yuzeyleri kume toplar:
        /// pickup kasasi, kabin tavani, kaput gibi yumusak alanlardir.
        /// </summary>
        private static void ReportDeckSurfaces(GameObject model, Bounds fitted, StringBuilder report)
        {
            var buckets = new Dictionary<float, float[]>();
            var keys = new List<float>();

            foreach (MeshFilter f in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (f.sharedMesh == null) continue;
                if (f.transform.name.ToLowerInvariant().Contains("wheel")) continue;

                Mesh mesh = f.sharedMesh;
                Vector3[] verts = mesh.vertices;
                int[] tris = mesh.triangles;
                Transform owner = f.transform;

                for (int i = 0; i + 2 < tris.Length; i += 3)
                {
                    Vector3 a = owner.TransformPoint(verts[tris[i]]);
                    Vector3 b = owner.TransformPoint(verts[tris[i + 1]]);
                    Vector3 c = owner.TransformPoint(verts[tris[i + 2]]);

                    Vector3 nrm = Vector3.Cross(b - a, c - a);
                    float area = nrm.magnitude * 0.5f;
                    if (area < 0.0005f) continue;
                    nrm /= area * 2f;
                    if (nrm.y < 0.85f) continue;   // yalniz yatay yukari yuzeyler

                    Vector3 mid = (a + b + c) / 3f;
                    float key = Mathf.Round(mid.y / 0.03f) * 0.03f;

                    if (!buckets.TryGetValue(key, out float[] acc))
                    {
                        acc = new[] { 0f, float.MaxValue, float.MinValue, float.MaxValue, float.MinValue };
                        buckets[key] = acc;
                        keys.Add(key);
                    }

                    acc[0] += area;
                    if (mid.x < acc[1]) acc[1] = mid.x;
                    if (mid.x > acc[2]) acc[2] = mid.x;
                    if (mid.z < acc[3]) acc[3] = mid.z;
                    if (mid.z > acc[4]) acc[4] = mid.z;
                }
            }

            keys.Sort();
            keys.Reverse();

            report.AppendLine("yatay yukari yuzeyler (y | alan m2 | x araligi | z araligi):");
            for (int i = 0; i < keys.Count && i < 14; i++)
            {
                float[] a = buckets[keys[i]];
                if (a[0] < 0.02f) continue;
                report.AppendLine("  y=" + keys[i].ToString("0.00") +
                                  "  alan=" + a[0].ToString("0.00") +
                                  "  x[" + a[1].ToString("0.00") + "," + a[2].ToString("0.00") + "]" +
                                  "  z[" + a[3].ToString("0.00") + "," + a[4].ToString("0.00") + "]");
            }
        }

        // =================== GECICI OLcum araci sonu =========================

        [MenuItem("Kargo/Asset/Kenney Kurye Aracini Kur", priority = 2)]
        public static void BuildVehicleModel()
        {
            if (!IsKitAvailable())
            {
                Debug.LogError(
                    "[Kargo] Kenney Car Kit bulunamadi.\n" +
                    "Beklenen: " + TruckFbx + "\n" +
                    "Paketi indirip " + KitRoot + " altina acin. Kaynak: " + DownloadPage);
                return;
            }

            // 1) FBX'in importu. Ayar degistirilince yeniden import
            //    gerekir, bu yuzden importu ONCE tamamla.
            AssetDatabase.ImportAsset(TruckFbx,
                ImportAssetOptions.ImportRecursive |
                ImportAssetOptions.ForceUpdate |
                ImportAssetOptions.ForceSynchronousImport);

            ConfigureModelImporter();
            ConfigureTextureImporter();

            // 2) Reimport sonrasi referanslar gecersizlesir: yeniden yukle.
            GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TruckFbx);
            if (modelPrefab == null)
            {
                Debug.LogError("[Kargo] FBX yuklenemedi: " + TruckFbx);
                return;
            }

            Material material = CreateMaterial();
            VehicleDefinition definition = LoadOrCreateDefinition();

            var report = new StringBuilder();
            GameObject root = new GameObject("PlayerVehicle");

            try
            {
                root.layer = LayerVehicle;

                var rb = root.AddComponent<Rigidbody>();
                rb.mass = definition.mass;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

                var controller = root.AddComponent<ArcadeVehicleController>();
                var player = root.AddComponent<PlayerController>();

                // 3) Modeli arac altina koy, olcekle ve hizala.
                GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab);
                model.name = "Body_Kenney";
                model.transform.SetParent(root.transform, false);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one;
                ApplyMaterial(model, material);

                FitModel(model, report);

                // 4) Modelin gomulu tekerlerini WheelCollider altina tasi:
                //    boylece hem doner hem direksiyonu takip eder.
                WheelSlot[] wheels = AttachWheels(model, root.transform, definition, report);

                // 5) Sasi collider'i gorsel govdeye hizalanir. Yoksa arac
                //    takla atar (bkz. ArcadeVehicleController).
                FitChassis(root.GetComponent<BoxCollider>(), model, wheels, report);

                definition.wheelRadius = wheels[0].collider.radius;
                EditorUtility.SetDirty(definition);

                controller.ConfigureForSetup(definition,
                    wheels.Select(w => w.collider).ToArray(), player);

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (prefab == null)
                    Debug.LogError("[Kargo] Prefab kaydedilemedi: " + PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Verify();
            Debug.Log("[Kargo] Kenney arac kuruldu.\n" + report);
        }

        // ------------------------------------------------------------------
        // Olcekleme / hizalama
        // ------------------------------------------------------------------

        /// <summary>
        /// Modelin mesh'lerini kapsayan sinirlari toplar.
        ///
        /// Iki tur mesh birlikte taranir:
        ///  - MeshFilter  -> statik mesh'ler (kenney arac govdesi)
        ///  - SkinnedMeshRenderer -> RIGGED mesh'ler (kurye karakteri).
        ///    Rigged karakterlerde Unity MeshFilter EKLEMEZ; mesh'i
        ///    SkinnedMeshRenderer'in uzerinde tasir. Sadece MeshFilter
        ///    aranirsa karakter "meshi yok" sanilir ve olceklenmez.
        ///
        /// Kritik: her mesh kendi yerel transform'unu tasir (tekerler
        /// modelin icinde kaydirilmis durumda). MeshFilter.sharedMesh.bounds
        /// YEREL oldugu icin, transform'lari hesaba katmadan
        /// Encapsulate edersek tekerleri modelin merkezinde varsayip
        /// yanlis bir boyut cikar (Y 3.23 m gibi). Bu yuzden her mesh'in
        /// 8 kösesi Transform.TransformPoint ile istenen uzaya
        /// tasinir; Renderer.bounds'un kare gecikmesi sorunu da olmaz.
        /// </summary>
        internal static bool TryGetMeshBounds(GameObject model, out Bounds bounds)
        {
            bool first = true;
            bounds = default;

            foreach (MeshFilter f in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (f.sharedMesh == null) continue;
                EncapsulateTransformed(model, f.sharedMesh, f.transform, ref first, ref bounds);
            }

            foreach (SkinnedMeshRenderer s in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (s.sharedMesh == null) continue;
                EncapsulateTransformed(model, s.sharedMesh, s.transform, ref first, ref bounds);
            }

            return !first;
        }

        /// <summary>Bir mesh'in 8 kösesini istenen uzaya tasip sinirlara ekler.</summary>
        private static void EncapsulateTransformed(GameObject model, Mesh mesh, Transform owner,
                                                   ref bool first, ref Bounds bounds)
        {
            Bounds meshBounds = mesh.bounds;

            for (int corner = 0; corner < 8; corner++)
            {
                var local = new Vector3(
                    (corner & 1) == 0 ? meshBounds.min.x : meshBounds.max.x,
                    (corner & 2) == 0 ? meshBounds.min.y : meshBounds.max.y,
                    (corner & 4) == 0 ? meshBounds.min.z : meshBounds.max.z);

                Vector3 p = owner.TransformPoint(local);
                if (first) { bounds = new Bounds(p, Vector3.zero); first = false; }
                else bounds.Encapsulate(p);
            }
        }

        /// <summary>
        /// Modeli hedef boya olcekle ve arac kokune gore hizalar.
        ///
        /// Hizalama uc noktada yapilir:
        ///  - X: teker izlerinin ortasi 0 (sag-sol denge).
        ///  - Z: dingilin ortasi 0 (on-arka denge).
        ///  - Y: modelin en alti 0 (lastikler zeminde).
        /// Boylece kamera ve fizik varsayimlari tutarli kalir.
        /// </summary>
        private static void FitModel(GameObject model, StringBuilder report)
        {
            // Model henuz olceklenmeden, kimlik transform ile olc.
            if (!TryGetMeshBounds(model, out Bounds raw))
            {
                Debug.LogError("[Kargo] Kenney modelinde mesh bulunamadi.");
                return;
            }

            float longAxis = Mathf.Max(raw.size.x, raw.size.z);
            float scale = longAxis > 0.001f ? TargetLengthMeters / longAxis : 1f;
            model.transform.localScale = Vector3.one * scale;

            // Olceklemeden sonra tekerlerin arac kokune gore konumunu oku.
            List<Transform> wheelTransforms = FindWheelTransforms(model);
            Vector3 mid = Vector3.zero;
            if (wheelTransforms.Count > 0)
            {
                foreach (Transform w in wheelTransforms) mid += w.position;
                mid /= wheelTransforms.Count;
                mid.y = 0f;
            }
            else
            {
                mid = new Vector3(raw.center.x, 0f, raw.center.z);
                report.AppendLine("  ! teker alt nesnesi yok, genel orta kullanildi");
            }

            TryGetMeshBounds(model, out Bounds scaled);
            model.transform.position += new Vector3(-mid.x, -scaled.min.y, -mid.z);

            TryGetMeshBounds(model, out Bounds fitted);
            report.AppendLine("  olcek        : x" + scale.ToString("0.000"));
            report.AppendLine("  ham boyut    : " + raw.size.ToString("0.00"));
            report.AppendLine("  son boyut    : " + fitted.size.ToString("0.00") +
                              " m (hedef " + TargetLengthMeters.ToString("0.0") + " m)");
            report.AppendLine("  teker grubu  : " + wheelTransforms.Count + " adet");
        }

        private static List<Transform> FindWheelTransforms(GameObject model)
        {
            var result = new List<Transform>();
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
                if (t.name.ToLowerInvariant().Contains("wheel")) result.Add(t);
            return result;
        }

        private static WheelSlot[] AttachWheels(GameObject model, Transform root,
                                                VehicleDefinition definition, StringBuilder report)
        {
            List<Transform> found = FindWheelTransforms(model);

            // Mevcut duzeni koruyoruz: 0=FL, 1=FR, 2=RL, 3=RR.
            // ArcadeVehicleController ilk iki tekeri yonlendirir.
            WheelSlot[] slots = new WheelSlot[4];
            bool usable = found.Count == 4;

            if (usable)
            {
                // Her tekerin mesh'i olmali; eksikse gorsel bos kalir.
                foreach (Transform t in found)
                {
                    var mf = t.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) { usable = false; break; }
                }

                if (usable)
                {
                    foreach (Transform t in found)
                    {
                        Vector3 local = root.InverseTransformPoint(t.position);
                        int index = (local.z > 0f ? 0 : 2) + (local.x < 0f ? 0 : 1);
                        slots[index] = new WheelSlot { visual = t };
                    }

                    // Tumyu eslemesi tamamlanmadiysa yedek yola dus.
                    usable = slots.All(s => s.visual != null);
                }
            }

            if (!usable)
            {
                report.AppendLine("  ! teker gruplari cozulemedi (" + found.Count +
                                  " adet); primitive konumlar kullanildi");
                return BuildFallbackWheels(root, definition);
            }

            // Teker yaricapini mesh'ten olcuyoruz; boylece fizik ile
            // gorsel ayni olcekte olur.
            float radius = 0f;
            foreach (WheelSlot s in slots)
            {
                radius += 0.5f * Mathf.Max(
                    s.visual.GetComponent<MeshFilter>().sharedMesh.bounds.size.y,
                    s.visual.GetComponent<MeshFilter>().sharedMesh.bounds.size.z);
            }
            radius /= slots.Length;

            for (int i = 0; i < 4; i++)
            {
                Vector3 p = root.InverseTransformPoint(slots[i].visual.position);

                string tag = i == 0 ? "FL" : i == 1 ? "FR" : i == 2 ? "RL" : "RR";
                var go = new GameObject("Wheel_" + tag);
                go.layer = LayerVehicle;
                go.transform.SetParent(root, false);
                go.transform.localPosition = p;

                WheelCollider wc = go.AddComponent<WheelCollider>();
                wc.radius = radius;
                wc.suspensionDistance = definition.suspensionDistance;
                JointSpring spring = wc.suspensionSpring;
                spring.spring = definition.suspensionSpring;
                spring.damper = definition.suspensionDamper;
                spring.targetPosition = 0.5f;
                wc.suspensionSpring = spring;

                // Modelin gomulu tekerini collider'in altina tasi; boylece
                // donme ve direksiyon fizikten gelir.
                slots[i].visual.SetParent(go.transform, false);
                slots[i].visual.localPosition = Vector3.zero;
                slots[i].visual.localRotation = Quaternion.identity;
                slots[i].collider = wc;

                slots[i].visual.gameObject.AddComponent<WheelVisual>().Bind(wc, root);
            }

            report.AppendLine("  teker yaricap: " + radius.ToString("0.000") + " m");
            return slots;
        }

        /// <summary>
        /// Modelden teker cozulemezse eski primitive konumlara duser.
        /// Fizik calisir, gorsel teker yoktur ama oyun bozulmaz.
        /// </summary>
        private static WheelSlot[] BuildFallbackWheels(Transform root, VehicleDefinition definition)
        {
            WheelSlot[] slots = new WheelSlot[4];
            float r = definition.wheelRadius;
            Vector3[] positions =
            {
                new Vector3(-0.85f, r,  1.35f),
                new Vector3( 0.85f, r,  1.35f),
                new Vector3(-0.85f, r, -1.35f),
                new Vector3( 0.85f, r, -1.35f),
            };

            for (int i = 0; i < 4; i++)
            {
                string tag = i == 0 ? "FL" : i == 1 ? "FR" : i == 2 ? "RL" : "RR";
                var go = new GameObject("Wheel_" + tag);
                go.layer = LayerVehicle;
                go.transform.SetParent(root, false);
                go.transform.localPosition = positions[i];

                WheelCollider wc = go.AddComponent<WheelCollider>();
                wc.radius = r;
                wc.suspensionDistance = definition.suspensionDistance;
                slots[i].collider = wc;
            }

            return slots;
        }

        /// <summary>
        /// Sasi BoxCollider'ini gorsel modele hizalar. Govde collider'i
        /// olmadan arac ilk itkide takla atar.
        /// </summary>
        private static void FitChassis(BoxCollider chassis, GameObject model,
                                        WheelSlot[] wheels, StringBuilder report)
        {
            if (chassis == null) return;

            // Model artik olceklenmis VE hizalanmis durumda; olcum
            // dunya uzayinda yapilir, ayrica bir olcek carpimi yok.
            if (!TryGetMeshBounds(model, out Bounds world)) return;

            Vector3 size = world.size;
            // Kok nesne kimlik donusunde ve model altinda oldugu icin
            // world degerleri arac local degerleridir.
            Vector3 center = model.transform.parent.InverseTransformPoint(world.center);
            float radius = wheels[0].collider != null ? wheels[0].collider.radius : 0.35f;

            // Govde gorselden biraz kucuk: genis collider kerede takilir.
            chassis.size = new Vector3(size.x * 0.88f, size.y * 0.55f, size.z * 0.94f);
            chassis.center = new Vector3(center.x, radius + size.y * 0.28f, center.z);

            report.AppendLine("  sasi         : center " + chassis.center.ToString("0.00") +
                              "  size " + chassis.size.ToString("0.00"));
        }

        // ------------------------------------------------------------------
        // Import ayarlari / materyal
        // ------------------------------------------------------------------

        private static void ConfigureModelImporter()
        {
            var importer = AssetImporter.GetAtPath(TruckFbx) as ModelImporter;
            if (importer == null) return;

            // Gömülü materyal kullanmayalim: URP/Lit tabanli kendi
            // materyalimizi atiyoruz (FBX'in standard shader'i URP'de
            // pembe kalir).
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importCameras = false;
            importer.importLights = false;
            // Kendi BoxCollider'imiz var; mesh collider'i aracin altina
            // girip surusmeyi bozardi.
            importer.addCollider = false;
            importer.isReadable = false;
            importer.generateSecondaryUV = false;
            importer.SaveAndReimport();
        }

        private static void ConfigureTextureImporter()
        {
            if (!File.Exists(ColormapPath)) return;

            var importer = AssetImporter.GetAtPath(ColormapPath) as TextureImporter;
            if (importer == null) return;

            // colormap bir palet dokusu: sikistirilmis, tekrar tekrar
            // (tile) edilmez, low-poly keskin kenarlar korunur.
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 512;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
        }

        private static Material CreateMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (existing != null) return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[Kargo] URP/Lit shader bulunamadi. Render pipeline URP degil mi?");
                return null;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));

            var material = new Material(shader) { name = "KenneyCarKit" };
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ColormapPath);
            if (texture != null) material.mainTexture = texture;

            material.SetFloat("_Smoothness", 0.15f);
            material.SetFloat("_Metallic", 0f);

            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        private static void ApplyMaterial(GameObject root, Material material)
        {
            if (material == null) return;
            foreach (MeshRenderer r in root.GetComponentsInChildren<MeshRenderer>(true))
                r.sharedMaterial = material;
        }

        private static VehicleDefinition LoadOrCreateDefinition()
        {
            var def = AssetDatabase.LoadAssetAtPath<VehicleDefinition>(VehicleDefPath);
            if (def != null) return def;

            Directory.CreateDirectory(Path.GetDirectoryName(VehicleDefPath));
            def = ScriptableObject.CreateInstance<VehicleDefinition>();
            AssetDatabase.CreateAsset(def, VehicleDefPath);
            return def;
        }

        // ------------------------------------------------------------------
        // Dogrulama
        // ------------------------------------------------------------------

        public static bool IsKitAvailable()
        {
            return File.Exists(TruckFbx);
        }

        /// <summary>
        /// Kaydedilmis prefabi okuyup baglantilari dogrular. Uretim
        /// sessizce yarim kalirsa oyuncu suruse basinda hicbir sey
        /// olmazdi.
        /// </summary>
        private static void Verify()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[Kargo] Dogrulama: prefab bulunamadi " + PrefabPath);
                return;
            }

            var problems = new StringBuilder();
            if (prefab.GetComponent<ArcadeVehicleController>() == null) problems.Append(" ArcadeVehicleController yok.");
            if (prefab.GetComponent<PlayerController>() == null) problems.Append(" PlayerController yok.");
            if (prefab.GetComponent<Rigidbody>() == null) problems.Append(" Rigidbody yok.");
            if (prefab.transform.Find("Body_Kenney") == null) problems.Append(" Kenney modeli (Body_Kenney) yok.");

            WheelCollider[] wheelColliders = prefab.GetComponentsInChildren<WheelCollider>(true);
            if (wheelColliders.Length != 4) problems.Append(" Teker sayisi " + wheelColliders.Length + ", 4 olmali.");

            WheelVisual[] wheelVisuals = prefab.GetComponentsInChildren<WheelVisual>(true);
            if (wheelVisuals.Length != 4) problems.Append(" Teker gorseli " + wheelVisuals.Length + ", 4 olmali.");

            bool hasChassis = false;
            foreach (Collider c in prefab.GetComponentsInChildren<Collider>(true))
                if (c is BoxCollider) { hasChassis = true; break; }
            if (!hasChassis) problems.Append(" Sasi collider'i yok (takla atar).");

            if (problems.Length == 0)
                Debug.Log("[Kargo] Dogrulama OK: Kenney modeli bagli, 4 tekerli, sasi collider'li.");
            else
                Debug.LogError("[Kargo] Dogrulama BASARISIZ:" + problems);
        }
    }
}
