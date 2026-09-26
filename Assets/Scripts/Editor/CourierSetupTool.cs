using System.Linq;
using System.Text;
using KargoSim.Player;
using KargoSim.Vehicle;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KargoSim.EditorTools
{
    /// <summary>
    /// Kurye karakterini kurar: hazir CC0 modeli (Kenney Mini
    /// Characters) icer alir, Animator olusturur, sahneye koyar ve
    /// Composition Root'a baglar.
    ///
    /// Lisans: Kenney Mini Characters, Creative Commons CC0.
    ///
    /// Model RIGGED gelir: 32 animasyon klibi vardir. Burada yalnizca
    /// oyunun ihtiyaci olanlar secilir (idle / walk / pick-up /
    /// holding-both / interact-right). Gereksiz klip eklemek hem
    /// dosyayi buyutur hem de Inspector'i okunmaz yapar.
    /// </summary>
    public static class CourierSetupTool
    {
        private const string KitRoot = "Assets/Art/Kenney/MiniCharacters";
        private const string CharacterFbx = KitRoot + "/Models/character-male-a.fbx";
        private const string ColormapPath = KitRoot + "/Models/Textures/colormap.png";
        private const string MaterialPath = "Assets/Art/Materials/KenneyCharacter.mat";
        private const string AnimatorPath = "Assets/Animations/Courier.controller";

        private const string ScenePath = "Assets/Scenes/Game.unity";
        private const string DownloadPage = "https://kenney.nl/assets/mini-characters";

        private const string RootName = "Courier";
        private const string ModelName = "Model";
        private const string CarryAnchorName = "CarryAnchor";
        private const string FpsCameraName = "FpsCamera";

        /// <summary>Karakterin hedef boyu (metre). Gercek insan ~1.75 m.</summary>
        private const float TargetHeightMeters = 1.75f;

        [MenuItem("Kargo/7. Kurye Karakterini Kur", priority = 6)]
        public static void Build()
        {
            if (!System.IO.File.Exists(CharacterFbx))
            {
                Debug.LogError(
                    "[Kargo] Kenney Mini Characters bulunamadi.\n" +
                    "Beklenen: " + CharacterFbx + "\n" +
                    "Paketi indirip " + KitRoot + " altina acin. Kaynak: " + DownloadPage);
                return;
            }

            var report = new StringBuilder();

            AssetDatabase.ImportAsset(CharacterFbx,
                ImportAssetOptions.ImportRecursive |
                ImportAssetOptions.ForceUpdate |
                ImportAssetOptions.ForceSynchronousImport);

            ConfigureModelImporter();
            Material material = CreateMaterial();
            RuntimeAnimatorController controller = BuildAnimatorController(report);

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // Eski kuryeyi temizle: menü tekrar calistirilabilir olmali.
            var stale = GameObject.Find(RootName);
            if (stale != null) Object.DestroyImmediate(stale);

            var vanObject = Object.FindFirstObjectByType<ArcadeVehicleController>();
            if (vanObject == null)
            {
                Debug.LogError("[Kargo] Sahnede araç yok. Önce arac prefabini kurun " +
                               "(Kargo/2).");
                return;
            }

            BuildCourier(vanObject.transform, material, controller, report);
            WireToCompositionRoot();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Verify(report);
            Debug.Log("[Kargo] Kurye kuruldu.\n" + report);
        }

        // ------------------------------------------------------------------
        // Sahne kurulumu
        // ------------------------------------------------------------------

        private static void BuildCourier(Transform van, Material material,
                                         RuntimeAnimatorController controller, StringBuilder report)
        {
            var root = new GameObject(RootName);
            root.transform.position = van.position;

            // Karakter fizigi: yerdegistirme + yer cekimi. Model gorsel
            // oldugu icin MeshCollider degil, CharacterController dogru
            // tercih (kayar, merdiven gibi seylerde ise yarar).
            var cc = root.AddComponent<CharacterController>();
            cc.height = TargetHeightMeters;
            cc.radius = 0.32f;
            cc.center = new Vector3(0f, TargetHeightMeters * 0.5f, 0f);
            cc.slopeLimit = 50f;
            cc.stepOffset = 0.35f;
            cc.skinWidth = 0.03f;

            // --- Model ---
            GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CharacterFbx);
            if (modelPrefab == null)
            {
                Debug.LogError("[Kargo] Karakter modeli yuklenemedi: " + CharacterFbx);
                Object.DestroyImmediate(root);
                return;
            }

            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab);
            model.name = ModelName;
            model.transform.SetParent(root.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;

            foreach (MeshRenderer r in model.GetComponentsInChildren<MeshRenderer>(true))
                r.sharedMaterial = material;

            ScaleToHeight(model, report);

            var animator = model.GetComponentInChildren<Animator>(true);
            if (animator == null) animator = model.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;   // hareketi KOD suruyor

            // --- El tasma noktasi (olcek 1'de, modelden bagimsiz) ---
            var carry = new GameObject(CarryAnchorName);
            carry.transform.SetParent(root.transform, false);
            carry.transform.localPosition = new Vector3(0f, TargetHeightMeters * 0.55f, 0.42f);

            // --- FPS kamerasi (kafa yuksekliginde, karakterin cocugu) ---
            // MainCamera etiketi ALMAZ: etiketli iki kamera olursa
            // Camera.main belirsizlesir ve arac kamerasi bozulur.
            // Ikincil kamera karaktere bagli ve sadece ayaktayken acik.
            var fpsGo = new GameObject(FpsCameraName);
            fpsGo.transform.SetParent(root.transform, false);
            fpsGo.transform.localPosition = new Vector3(0f, TargetHeightMeters * 0.86f, 0f);
            fpsGo.tag = "Untagged";

            var fpsCamera = fpsGo.AddComponent<Camera>();
            fpsCamera.fieldOfView = 70f;
            fpsCamera.nearClipPlane = 0.05f;
            fpsCamera.enabled = false;   // baslangicta araçta
            fpsGo.AddComponent<AudioListener>();

            // --- Bilesen baglantilari ---
            var courier = root.AddComponent<CourierController>();
            var so = new SerializedObject(courier);
            so.FindProperty("controller").objectReferenceValue = cc;
            so.FindProperty("fpsCamera").objectReferenceValue = fpsCamera;
            so.FindProperty("vehicleCamera").objectReferenceValue = Camera.main;
            so.FindProperty("carryAnchor").objectReferenceValue = carry.transform;
            so.FindProperty("animator").objectReferenceValue = animator;
            so.FindProperty("vehicleController").objectReferenceValue =
                van.GetComponent<ArcadeVehicleController>();
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(courier);

            // Başlangıçta gizli: oyuncu araç sürerken karakter görünmez.
            model.SetActive(false);
            carry.SetActive(false);

            report.AppendLine("  model        : " + model.name + " (olcek x" +
                              model.transform.localScale.x.ToString("0.000") + ")");
            report.AppendLine("  tasma noktasi: " + carry.transform.localPosition.ToString("0.00"));
            report.AppendLine("  fps kamera   : " + fpsGo.transform.localPosition.ToString("0.00"));
        }

        /// <summary>
        /// Karakteri gercek insan boyuna (1.75 m) olcekle.
        ///
        /// Kenney modelleri ~1.7 birim gelir; oyundaki arac 4.2 m.
        /// Olceklenmezse kurye devasa, ya da hic gorunmez.
        /// </summary>
        private static void ScaleToHeight(GameObject model, StringBuilder report)
        {
            if (!KenneyAssetSetup.TryGetMeshBounds(model, out Bounds local))
            {
                Debug.LogWarning("[Kargo] Karakter mesh'i olcumledi; olceklenmedi.");
                return;
            }

            float height = local.size.y;
            if (height <= 0.001f) return;

            float scale = TargetHeightMeters / height;
            model.transform.localScale = Vector3.one * scale;

            // Ayaklar zeminde olsun: mesh'in altini 0'a indir.
            KenneyAssetSetup.TryGetMeshBounds(model, out Bounds scaled);
            model.transform.position += new Vector3(-scaled.center.x, -scaled.min.y, -scaled.center.z);

            report.AppendLine("  ham boy      : " + local.size.ToString("0.00") +
                              "  ->  " + TargetHeightMeters.ToString("0.00") + " m");
        }

        private static void WireToCompositionRoot()
        {
            var root = Object.FindFirstObjectByType<Core.GameCompositionRoot>();
            if (root == null)
            {
                Debug.LogWarning("[Kargo] GameCompositionRoot bulunamadi; kurye baglanmadi.");
                return;
            }

            var courier = Object.FindFirstObjectByType<CourierController>();
            if (courier == null) return;

            root.SetCourierForSetup(courier);
            EditorUtility.SetDirty(root);
        }

        // ------------------------------------------------------------------
        // Animasyon
        // ------------------------------------------------------------------

        /// <summary>
        /// Yalnizca oyunun kullandigi klipleri iceren Animator olusturur.
        /// Tum 32 klip eklenmez: kullanilmayan klip runtime'da bellek
        /// yeridir.
        /// </summary>
        private static RuntimeAnimatorController BuildAnimatorController(StringBuilder report)
        {
            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(CharacterFbx)
                .OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__preview"))
                .ToArray();

            report.AppendLine("  FBX klibi    : " + clips.Length + " adet bulundu");

            string[] wanted = { "idle", "walk", "pick-up", "holding-both", "interact-right" };

            if (!AssetDatabase.IsValidFolder("Assets/Animations"))
                AssetDatabase.CreateFolder("Assets", "Animations");

            var controller = AnimatorController.CreateAnimatorControllerAtPath(AnimatorPath);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            AnimatorState first = null;
            foreach (string clipName in wanted)
            {
                AnimationClip clip = clips.FirstOrDefault(c =>
                    c.name.Equals(clipName, System.StringComparison.OrdinalIgnoreCase) ||
                    c.name.IndexOf(clipName, System.StringComparison.OrdinalIgnoreCase) >= 0);

                if (clip == null)
                {
                    report.AppendLine("  ! klip bulunamadi: " + clipName);
                    continue;
                }

                var state = machine.AddState(clipName);
                state.motion = clip;
                state.writeDefaultValues = false;
                if (first == null) first = state;
                report.AppendLine("  klip         : " + clipName + " (" + clip.length.ToString("0.00") + "s)");
            }

            if (first != null) machine.defaultState = first;
            EditorUtility.SetDirty(controller);

            if (first == null)
            {
                Debug.LogError("[Kargo] Hiçbir animasyon klibi eşleşmedi. " +
                               "FBX farklı adlarla gelmiş olabilir; konsoldaki " +
                               "klip listesine bakın.");
            }

            return controller;
        }

        // ------------------------------------------------------------------
        // Import / materyal
        // ------------------------------------------------------------------

        private static void ConfigureModelImporter()
        {
            var importer = AssetImporter.GetAtPath(CharacterFbx) as ModelImporter;
            if (importer == null) return;

            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.addCollider = false;      // CharacterController var
            importer.isReadable = false;
            // Animasyonlar varsayilan olarak import edilir; Unity 6'da
            // ayri bir "importAnimations" anahtari kaldirilmis. Klip
            // gercekten geldigini BuildAnimatorController dogrular.
            importer.SaveAndReimport();
        }

        private static Material CreateMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (existing != null) return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[Kargo] URP/Lit shader bulunamadi.");
                return null;
            }

            if (!AssetDatabase.IsValidFolder("Assets/Art/Materials"))
                AssetDatabase.CreateFolder("Assets/Art", "Materials");

            var material = new Material(shader) { name = "KenneyCharacter" };
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(ColormapPath);
            if (texture != null) material.mainTexture = texture;

            material.SetFloat("_Smoothness", 0.1f);
            material.SetFloat("_Metallic", 0f);

            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        // ------------------------------------------------------------------
        // Dogrulama
        // ------------------------------------------------------------------

        private static void Verify(StringBuilder report)
        {
            var problems = new StringBuilder();

            var courier = Object.FindFirstObjectByType<CourierController>();
            if (courier == null)
            {
                Debug.LogError("[Kargo] Dogrulama: CourierController sahnede yok.");
                return;
            }

            var so = new SerializedObject(courier);
            Check(so, "fpsCamera", problems);
            Check(so, "vehicleCamera", problems);
            Check(so, "carryAnchor", problems);
            Check(so, "animator", problems);
            Check(so, "controller", problems);
            Check(so, "vehicleController", problems);

            if (courier.GetComponent<CharacterController>() == null)
                problems.Append(" CharacterController yok; karakter zeminde durmaz.");

            if (courier.GetComponentInChildren<Animator>(true) == null)
                problems.Append(" Animator yok; karakter hareketsiz kalir.");

            // Iki kamera olmali: karakterin FPS kamerasi + arac takip
            // kamerasi. Tek kamera olursa ya yururken goremezsin ya
            // araci goremezsin.
            Camera[] cameras = courier.GetComponentsInChildren<Camera>(true);
            if (cameras.Length < 1)
                problems.Append(" FPS kamerasi yok; karakter bakinca gormez.");

            // Ana kamera arac takip kamerasi olmali.
            Camera main = Camera.main;
            if (main == null)
                problems.Append(" MainCamera yok; arac kamera kaybolur.");
            else if (main.GetComponentInChildren<Camera>(true) == main &&
                     main.transform.IsChildOf(courier.transform))
                problems.Append(" MainCamera karakterin altinda; arac kamerasi kaybolur.");

            // Animator'in varligi yetmez: modelin gercekten mesh'i olmali.
            // Referanslar atanmis ama model bos birakilmissa oyuncu
            // HAREKETSIZ bir siluet gorur.
            int meshes = courier.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length
                       + courier.GetComponentsInChildren<MeshFilter>(true).Length;
            if (meshes == 0)
                problems.Append(" Karakter modelinde mesh yok (gorsel bos).");

            report.AppendLine("  model mesh   : " + meshes + " adet");

            var root = Object.FindFirstObjectByType<Core.GameCompositionRoot>();
            if (root == null) problems.Append(" GameCompositionRoot yok.");
            else
            {
                var rootSo = new SerializedObject(root);
                if (rootSo.FindProperty("courier").objectReferenceValue == null)
                    problems.Append(" Composition Root'a kurye baglanmadi.");
            }

            if (problems.Length == 0)
                Debug.Log("[Kargo] Dogrulama OK: kurye bagli, animator ve fizik yerinde.");
            else
                Debug.LogError("[Kargo] Dogrulama BASARISIZ:" + problems);
        }

        private static void Check(SerializedObject so, string property, StringBuilder problems)
        {
            var prop = so.FindProperty(property);
            if (prop == null)
            {
                problems.Append(" '" + property + "' alani bulunamadi.");
                return;
            }
            if (prop.objectReferenceValue == null)
                problems.Append(" '" + property + "' atanmamis.");
        }
    }
}
