using KargoSim.Player;
using KargoSim.Vehicle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KargoSim.EditorTools
{
    /// <summary>
    /// Kurye aracini Unity primitive'lerinden uretir: gorsel model
    /// (Blender) gelene kadar oynanabilir bir arac sahip olmak icin.
    ///
    /// Neden elle Inspector'dan kurmak yerine arac: prefab her zaman
    /// yeniden uretilebilir olur. Ileride Blender'dan gercek model
    /// geldiginde yalnizca gorsel cocuklar degisir; fizik ve baglantilar
    /// aynen korunur.
    /// </summary>
    public static class VehiclePrefabBuilder
    {
        private const string VehicleDefPath = "Assets/ScriptableObjects/Definitions/VehicleDefinition_CourierVan.asset";
        private const string PrefabFolder = "Assets/Prefabs";
        private const string PrefabPath = PrefabFolder + "/PlayerVehicle.prefab";
        private const string ScenePath = "Assets/Scenes/Game.unity";
        private const string BodyMaterialPath = "Assets/Art/Materials/VehicleBody.mat";
        private const string WheelMaterialPath = "Assets/Art/Materials/VehicleWheel.mat";

        private const int LayerVehicle = 11;

        [MenuItem("Kargo/Vehicle/Arac Prefabini Kur", priority = 30)]
        public static void Build()
        {
            EnsureFolders();

            VehicleDefinition definition = CreateVehicleDefinition();
            Material bodyMat = CreateMaterial(BodyMaterialPath, new Color(0.90f, 0.55f, 0.13f));
            Material wheelMat = CreateMaterial(WheelMaterialPath, new Color(0.12f, 0.12f, 0.14f));

            GameObject root = new GameObject("PlayerVehicle");
            try
            {
                root.layer = LayerVehicle;

                var rb = root.AddComponent<Rigidbody>();
                rb.mass = definition.mass;
                // Interpolate: 50 Hz fizik, 60+ Hz gorsel. Bu olmadan arac titrer.
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                // Sürekli çarpışma tespiti (CCD): hizli araç duvardan "tünelleyip"
                // geçmesin diye. Bedava maliyeti yok, kazanci büyük.
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

                var controller = root.AddComponent<ArcadeVehicleController>();
                var player = root.AddComponent<PlayerController>();

                // [RequireComponent(BoxCollider)] sayesinde kokte ZATEN bir
                // BoxCollider var. Onu sasi olarak kullaniyoruz - boyut ve
                // merkez tekerlerin hemen ustunde kalacak sekilde.
                // (Ayrı bir "Chassis" cocugu EKLEMEK ikinci bir collider
                // yaratir ve arac yerinde sıkışıp hareket edemez.)
                var chassis = root.GetComponent<BoxCollider>();
                chassis.center = new Vector3(0f, 0.75f, 0f);
                chassis.size = new Vector3(1.8f, 0.8f, 4.2f);

                BuildBody(root.transform, definition, bodyMat);
                var wheelColliders = BuildWheels(root.transform, definition, wheelMat);

                // Referanslar uretim bittikten sonra atanir: once agac kurulur,
                // sonra baglanir (yarim kurulmus bir nesne asla olusmaz).
                controller.ConfigureForSetup(definition, wheelColliders, player);

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (prefab == null)
                    Debug.LogError("[Kargo] Prefab kaydedilemedi: " + PrefabPath);
            }
            finally
            {
                // Prefab zaten kaydedildiyse gecici nesneyi her durumda temizle.
                Object.DestroyImmediate(root);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            VerifyPrefab();
            PlaceInScene();
            Debug.Log("[Kargo] Arac prefabi olusturuldu: " + PrefabPath);
        }

        // ------------------------------------------------------------------
        // Sahneye yerlestirme
        // ------------------------------------------------------------------

        /// <summary>
        /// Prefabi Game.unity sahnesine koyar ve takip kamerasini baglar.
        /// Tekrar calistirildiginda mevcut araca dokunmaz (gelistirme
        /// sirasinda aracin sahnede yaptigimiz yerlesimleri korumak icin).
        /// </summary>
        [MenuItem("Kargo/Vehicle/Araci Sahneye Koy", priority = 31)]
        public static void PlaceInScene()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[Kargo] Once arac prefabini kurun: Kargo/Vehicle/Arac Prefabini Kur");
                return;
            }

            UnityEngine.SceneManagement.Scene scene =
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var existing = Object.FindFirstObjectByType<ArcadeVehicleController>();
            if (existing != null)
            {
                Debug.Log("[Kargo] Sahnede zaten bir arac var, dokunulmadi.");
                return;
            }

            // Arac yerden biraz yukarda dogar; suspension yerlestirir.
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.position = new Vector3(0f, 1f, 0f);
            Undo.RegisterCreatedObjectUndo(instance, "PlayerVehicle");

            AttachFollowCamera(instance.transform);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[Kargo] Arac sahneye kondu ve kamera baglandi.");
        }

        private static void AttachFollowCamera(Transform vehicle)
        {
            var cameraGo = Camera.main != null
                ? Camera.main.gameObject
                : GameObject.FindGameObjectWithTag("MainCamera");

            if (cameraGo == null)
            {
                Debug.LogError("[Kargo] MainCamera bulunamadi; kamera takipi baglanmadi. " +
                               "Sahnede MainCamera etiketli bir kamera olmalidir.");
                return;
            }

            var follow = cameraGo.GetComponent<CameraFollow>();
            if (follow == null) follow = cameraGo.AddComponent<CameraFollow>();

            follow.SetTarget(vehicle);

            // Kamerayi aracin pehine hemen oturt: Acikista kamera eski
            // konumundan yavas yavas gelir, oyuncu havada süzülüyormus gibi gorunur.
            cameraGo.transform.position = vehicle.position + new Vector3(0f, 6.5f, -9.5f);
            cameraGo.transform.rotation = vehicle.rotation;
        }

        // ------------------------------------------------------------------
        // Gorsel govde
        // ------------------------------------------------------------------

        private static void BuildBody(Transform parent, VehicleDefinition def, Material material)
        {
            // Govde mesh'i SADECE gorseldir; collider'i yoktur.
            // Sert govde collider'i kok nesnedeki BoxCollider'dir (Build
            // icinde ayarlanir). Bkz. ArcadeVehicleController.ApplyDrive:
            // sasi collider'i olmadan arac takla atar.
            GameObject body = CreateBox("Body",
                new Vector3(0f, 0.75f, 0f),
                new Vector3(1.8f, 1.6f, 4.2f),
                parent, material, def.wheelRadius);

            // Kabin: yoncu kutusu
            CreateBox("Cabin",
                new Vector3(0f, 1.55f, 1.25f),
                new Vector3(1.7f, 0.9f, 1.2f),
                body.transform, material, def.wheelRadius);

            // On/arka tampon: yonu gorsel olarak anlasilir kilar
            CreateBox("Bumper_Front",
                new Vector3(0f, 0.55f, 2.15f),
                new Vector3(1.85f, 0.35f, 0.2f),
                parent, material, def.wheelRadius);

            CreateBox("Bumper_Back",
                new Vector3(0f, 0.55f, -2.15f),
                new Vector3(1.85f, 0.35f, 0.2f),
                parent, material, def.wheelRadius);
        }

        // ------------------------------------------------------------------
        // Tekerler
        // ------------------------------------------------------------------

        private static WheelCollider[] BuildWheels(Transform parent,
                                                   VehicleDefinition def,
                                                   Material wheelMaterial)
        {
            // Sira ONDAN SONRA: ArcadeVehicleController ilk iki tekeri yonlendirir.
            float r = def.wheelRadius;
            float x = 0.85f;     // tekerler arası yari genislik
            float zFront = 1.35f;
            float zRear = -1.35f;
            float y = r;         // teker merkezi yerden yaricap kadar yukarida

            var colliders = new WheelCollider[4];

            colliders[0] = CreateWheel("Wheel_FL", new Vector3(-x, y, zFront), parent, def, wheelMaterial);
            colliders[1] = CreateWheel("Wheel_FR", new Vector3(x, y, zFront), parent, def, wheelMaterial);
            colliders[2] = CreateWheel("Wheel_RL", new Vector3(-x, y, zRear), parent, def, wheelMaterial);
            colliders[3] = CreateWheel("Wheel_RR", new Vector3(x, y, zRear), parent, def, wheelMaterial);

            return colliders;
        }

        private static WheelCollider CreateWheel(string name, Vector3 localPosition,
                                                 Transform parent, VehicleDefinition def,
                                                 Material material)
        {
            GameObject wheelGo = new GameObject(name);
            wheelGo.layer = LayerVehicle;
            wheelGo.transform.SetParent(parent, false);
            wheelGo.transform.localPosition = localPosition;

            // WheelCollider yalnizca fizik icindir; gorsel icerik TEMAAS EDILMEZ.
            WheelCollider collider = wheelGo.AddComponent<WheelCollider>();
            collider.radius = def.wheelRadius;
            collider.suspensionDistance = def.suspensionDistance;

            JointSpring spring = collider.suspensionSpring;
            spring.spring = def.suspensionSpring;
            spring.damper = def.suspensionDamper;
            spring.targetPosition = 0.5f;
            collider.suspensionSpring = spring;

            // Gorsel silindir cocuk nesne olarak: dondurulurken ekseni degismez.
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            visual.name = "Mesh";
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(wheelGo.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = new Vector3(0.7f, 0.28f, 0.7f);
            // Unity silindiri Y ekseninde acar; tekerin ekseni X olmali.
            visual.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            ApplyMaterial(visual, material);

            return collider;
        }

        // ------------------------------------------------------------------
        // Yardimcilar
        // ------------------------------------------------------------------

        private static GameObject CreateBox(string name, Vector3 localPosition, Vector3 size,
                                            Transform parent, Material material, float radius)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;

            // Bu nesneler YALNIZCA gorseldir. Sert govde collider'i kok
            // nesnedeki BoxCollider'dir; burada fazladan collider her yanda
            // hem fizik maliyeti hem de aracin altina girip surus
            // bozmasina yol acar.
            Object.DestroyImmediate(box.GetComponent<Collider>());

            box.transform.SetParent(parent, false);
            box.transform.localPosition = localPosition;
            box.transform.localScale = size;
            ApplyMaterial(box, material);
            return box;
        }

        private static void ApplyMaterial(GameObject go, Material material)
        {
            if (material == null) return;
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer == null) return;
            renderer.sharedMaterial = material;
        }

        private static Material CreateMaterial(string path, Color color)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            // URP'de standart shader "Universal Render Pipeline/Lit".
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[Kargo] URP/Lit shader bulunamadi. Render pipeline URP degil mi?");
                return null;
            }

            var material = new Material(shader) { color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static VehicleDefinition CreateVehicleDefinition()
        {
            var existing = AssetDatabase.LoadAssetAtPath<VehicleDefinition>(VehicleDefPath);
            if (existing != null) return existing;

            var def = ScriptableObject.CreateInstance<VehicleDefinition>();
            AssetDatabase.CreateAsset(def, VehicleDefPath);
            return def;
        }

        private static void EnsureFolders()
        {
            string[] folders =
            {
                PrefabFolder,
                "Assets/Art/Materials",
                "Assets/ScriptableObjects/Definitions",
            };
            foreach (string f in folders)
            {
                if (AssetDatabase.IsValidFolder(f)) continue;

                string[] parts = f.Split('/');
                string current = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                        AssetDatabase.CreateFolder(current, parts[i]);
                    current = next;
                }
            }
        }

        /// <summary>
        /// Kaydedilmis prefabi diskten okuyup baglantilari dogrular.
        /// Uretim sessizce yarim kalmis olabilir (ornegin teker referansi
        /// null) ve oyuncu suruse basunca hicbir sey olmazdi.
        /// </summary>
        private static void VerifyPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
            {
                Debug.LogError("[Kargo] Dogrulama: prefab bulunamadi " + PrefabPath);
                return;
            }

            var controller = prefab.GetComponent<ArcadeVehicleController>();
            var player = prefab.GetComponent<PlayerController>();
            var rb = prefab.GetComponent<Rigidbody>();

            var problems = new System.Text.StringBuilder();

            if (controller == null) problems.Append(" ArcadeVehicleController yok.");
            if (player == null) problems.Append(" PlayerController yok.");
            if (rb == null) problems.Append(" Rigidbody yok.");
            if (controller != null && controller.Definition == null) problems.Append(" VehicleDefinition atanmamis.");

            // GOVDE COLLIDER'I ZORUNLU: sadece tekerlerden olusan arac
            // ilk itki kuvvetiyle takla atar. Burada olmazsa oyun acilir
            // ama surulemez.
            var colliders = prefab.GetComponentsInChildren<Collider>(true);
            bool hasChassis = false;
            foreach (var c in colliders)
            {
                if (c is BoxCollider) { hasChassis = true; break; }
            }
            if (!hasChassis) problems.Append(" GOVDE COLLIDER'I YOK (takla atar).");

            var wheels = prefab.GetComponentsInChildren<WheelCollider>(true);
            if (wheels.Length != 4) problems.Append(" Teker sayisi " + wheels.Length + ", 4 olmali.");

            if (problems.Length == 0)
                Debug.Log("[Kargo] Dogrulama OK: prefab sasi collider'li, 4 tekerli, " +
                          "tanim ve girdi bagli.");
            else
                Debug.LogError("[Kargo] Dogrulama BASARISIZ:" + problems);
        }
    }
}
