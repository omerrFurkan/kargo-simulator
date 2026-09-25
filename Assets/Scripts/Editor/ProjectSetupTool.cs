using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using KargoSim.Core;

namespace KargoSim.EditorTools
{
    /// <summary>
    /// Projeyi kurumsal hale getiren TEK SEFERLIK editor araci.
    ///
    /// Elle yapilacak onlarca ayari (URP pipeline'i olusturmak, katmanlari
    /// tanimlamak, fizik matrisini kurmak, sahneyi kurmak) tek komutla ve
    /// TEKRARLANABILIR sekilde yapar. Projeyi yeniden kurarsaniz ya da
    /// bir baska bilgisayara tasiyorsaniz bu araci tekrar calistirin.
    ///
    /// Kullanim: Unity ust menusunden  Kargo/1. Tum Kurulumu Calistir
    /// veya komut satirindan:
    ///   Unity.exe -batchmode -quit -projectPath . -executeMethod KargoSim.EditorTools.ProjectSetupTool.RunAllSetup
    /// </summary>
    public static class ProjectSetupTool
    {
        private const string SettingsFolder = "Assets/Settings";
        private const string UrpAssetPath = SettingsFolder + "/UniversalRenderPipelineAsset.asset";
        private const string RendererDataPath = SettingsFolder + "/UniversalRendererData.asset";
        private const string GameConfigPath = "Assets/ScriptableObjects/Database/GameConfig.asset";
        private const string ScenePath = "Assets/Scenes/Game.unity";

        private const int LayerGround = 8;
        private const int LayerRoad = 9;
        private const int LayerBuilding = 10;
        private const int LayerVehicle = 11;
        private const int LayerDepoZone = 12;
        private const int LayerDeliveryStop = 13;

        [MenuItem("Kargo/1. Tum Kurulumu Calistir", priority = 0)]
        public static void RunAllSetup()
        {
            EnsureFolders();
            SetupRenderPipeline();
            SetupLayers();
            SetupPhysics();
            SetupQuality();
            SetupPlayerSettings();
            CreateGameConfig();
            CreateGameSceneIfMissing();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Kargo] Kurulum tamamlandi. Sirada: Editor acilsin ve Game.unity sahnesini calistir.");
        }

        // ------------------------------------------------------------------
        // Klasorler
        // ------------------------------------------------------------------

        [MenuItem("Kargo/2. Klasorleri Hazirla", priority = 20)]
        public static void EnsureFolders()
        {
            string[] folders =
            {
                SettingsFolder,
                "Assets/Art/Materials",
                "Assets/Art/Models",
                "Assets/Art/Prefabs",
                "Assets/Audio",
                "Assets/Prefabs",
                "Assets/Scenes",
                "Assets/Scripts/Core",
                "Assets/Scripts/Player",
                "Assets/Scripts/Vehicle",
                "Assets/Scripts/Cargo",
                "Assets/Scripts/Delivery",
                "Assets/Scripts/Economy",
                "Assets/Scripts/Mission",
                "Assets/Scripts/UI",
                "Assets/Scripts/Save",
                "Assets/Scripts/Editor",
                "Assets/ScriptableObjects/Definitions",
                "Assets/ScriptableObjects/Database",
                "Assets/Resources",
            };

            foreach (string f in folders)
            {
                if (!AssetDatabase.IsValidFolder(f))
                {
                    CreateNestedFolder(f);
                    Debug.Log("[Kargo] Klasor olusturuldu: " + f);
                }
            }
        }

        private static void CreateNestedFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0]; // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        // ------------------------------------------------------------------
        // URP pipeline
        // ------------------------------------------------------------------

        [MenuItem("Kargo/3. Render Pipeline (URP) Kur", priority = 21)]
        public static void SetupRenderPipeline()
        {
            // URP ayarlari icin ayri klasor
            if (!AssetDatabase.IsValidFolder(SettingsFolder))
                CreateNestedFolder(SettingsFolder);

            // --- Renderer Data ---
            UniversalRendererData rendererData =
                AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererDataPath);

            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, RendererDataPath);
                Debug.Log("[Kargo] Renderer olusturuldu: " + RendererDataPath);
            }

            // --- Pipeline Asset ---
            UniversalRenderPipelineAsset urp =
                AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(UrpAssetPath);

            if (urp == null)
            {
                urp = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(urp, UrpAssetPath);
                Debug.Log("[Kargo] URP asset olusturuldu: " + UrpAssetPath);
            }

            // --- PERFORMANS: gereksiz ozellikleri kapat ---
            urp.msaaSampleCount = 1;                    // MSAA kapali
            urp.supportsHDR = false;                    // HDR kamerasi = gereksiz bant genisligi
            urp.supportsCameraDepthTexture = false;      // depth pre-pass yok
            urp.supportsCameraOpaqueTexture = false;     // copy pass yok
            urp.renderScale = 1f;
            urp.useSRPBatcher = true;                    // SRP Batcher: cizim maliyetini dusurur

            // --- Golge: tek isik, tek cascade ---
            // NOT: supportsMainLightShadows / supportsSoftShadows /
            // additionalLightsRenderingMode alanlarinin setter'i URP 17'de
            // 'internal' - yani disaridan atanamaz. Bu yuzden SerializedProperty
            // ile serilestirilmis alanlara dogrudan yaziyoruz.
            SetPrivateField(urp, "m_MainLightShadowsSupported", true);
            SetPrivateField(urp, "m_SoftShadowsSupported", true);
            SetPrivateField(urp, "m_AdditionalLightsRenderingMode", (int)LightRenderingMode.PerVertex);
            SetPrivateField(urp, "m_AdditionalLightShadowsSupported", false);
            urp.shadowDistance = 60f;
            urp.shadowCascadeCount = 1;                  // 4 degil 1: daha ucuz
            urp.maxAdditionalLightsCount = 1;

            EditorUtility.SetDirty(urp);
            EditorUtility.SetDirty(rendererData);

            // --- Global olarak ata ---
            GraphicsSettings.defaultRenderPipeline = urp;

            // Bu deger kalici olarak SADECE varsayilan render pipeline kanalina
            // yazilir. QualitySettings.renderPipeline her zaman varsayilani yazar,
            // bu yuzden tum kalite seviyelerine ayri ayri atamak kalici olmaz.
            // (Inspector'da kalite seviyesi basina "Render Pipeline" secilirse
            //  oraya ayri bir asset atanmalidir.)

            // Artik gereksiz olan varsayilan volume profile'i tasimak yerine
            // Settings klasorune toplayalim.
            MoveIfExists("Assets/DefaultVolumeProfile.asset",
                         SettingsFolder + "/DefaultVolumeProfile.asset");
            MoveIfExists("Assets/UniversalRenderPipelineGlobalSettings.asset",
                         SettingsFolder + "/UniversalRenderPipelineGlobalSettings.asset");

            Debug.Log("[Kargo] Render Pipeline kuruldu: Forward, 1 cascade, HDR/Depth/SSAA kapali.");
        }

        /// <summary>
        /// URP bazi ayarlari 'internal set' ile koruyor. Editor araci olarak
        /// SerializedProperty uzerinden serilestirilmis alana yazabiliyoruz.
        /// Alan bulunamazsa hata verir (yanlis isim yazildiginda sessizce
        /// gecmemesi icin).
        /// </summary>
        private static void SetPrivateField(UnityEngine.Object target, string fieldName, object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogError("[Kargo] URP alani bulunamadi: " + fieldName +
                               " (URP surumu degismis olabilir)");
                return;
            }

            if (value is bool b) prop.boolValue = b;
            else if (value is int i) prop.intValue = i;
            else if (value is float f) prop.floatValue = f;

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void MoveIfExists(string from, string to)
        {
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(from) == null) return;
            string error = AssetDatabase.MoveAsset(from, to);
            if (!string.IsNullOrEmpty(error))
                Debug.LogWarning("[Kargo] Tasinamadi: " + from + " -> " + to + " (" + error + ")");
        }

        // ------------------------------------------------------------------
        // Katmanlar
        // ------------------------------------------------------------------

        [MenuItem("Kargo/4. Katmanlari Kur", priority = 22)]
        public static void SetupLayers()
        {
            var tagManagerAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (tagManagerAssets == null || tagManagerAssets.Length == 0)
            {
                Debug.LogError("[Kargo] TagManager.asset bulunamadi.");
                return;
            }

            var tagManager = new SerializedObject(tagManagerAssets[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");

            SetLayer(layers, LayerGround, "Ground");
            SetLayer(layers, LayerRoad, "Road");
            SetLayer(layers, LayerBuilding, "Building");
            SetLayer(layers, LayerVehicle, "Vehicle");
            SetLayer(layers, LayerDepoZone, "DepoZone");
            SetLayer(layers, LayerDeliveryStop, "DeliveryStop");

            tagManager.ApplyModifiedPropertiesWithoutUndo();

            // --- Fizik matrisini daralt ---
            // Trigger bölgeleri fiziksel engel olmamali: arac onlarin icinden gecebilmeli.
            Physics.IgnoreLayerCollision(LayerVehicle, LayerDeliveryStop, true);
            Physics.IgnoreLayerCollision(LayerVehicle, LayerDepoZone, true);
            Physics.IgnoreLayerCollision(LayerDeliveryStop, LayerDepoZone, true);
            Physics.IgnoreLayerCollision(LayerDeliveryStop, LayerDeliveryStop, true);
            Physics.IgnoreLayerCollision(LayerDepoZone, LayerDepoZone, true);

            // Arac binalara ve ground'a carpabilmeli - o ikisi acik kalsin.
            Debug.Log("[Kargo] Katmanlar kuruldu (8-13) ve fizik matrisi daraltildi.");
        }

        private static void SetLayer(SerializedProperty layers, int index, string name)
        {
            SerializedProperty element = layers.GetArrayElementAtIndex(index);
            if (element.stringValue != name)
            {
                element.stringValue = name;
                Debug.Log("[Kargo] Katman " + index + " = " + name);
            }
        }

        // ------------------------------------------------------------------
        // Fizik
        // ------------------------------------------------------------------

        [MenuItem("Kargo/5. Fizik Ayarlari", priority = 23)]
        public static void SetupPhysics()
        {
            // Arcade his: gercek yercekimi -9.81 cok "yavas" kureler hissettirir.
            Physics.gravity = new Vector3(0f, -25f, 0f);

            // Transform'lar her karede otomatik senkronlanmasin. Rigidbody ile
            // hareket ettirdigimiz her sey bu sayede fizik icin bedava olur.
            Physics.autoSyncTransforms = false;

            // Carpisma geri bildirimi havuzunu yeniden kullanim icin etkinlestir.
            Physics.reuseCollisionCallbacks = true;

            // Cozum: 8/3 varsayilan, yumuşak temas icin yeterli.
            Physics.defaultSolverIterations = 8;
            Physics.defaultSolverVelocityIterations = 3;

            Debug.Log("[Kargo] Fizik: gravity -25, autoSyncTransforms KAPALI.");
        }

        // ------------------------------------------------------------------
        // Kalite
        // ------------------------------------------------------------------

        [MenuItem("Kargo/6. Kalite Ayarlari", priority = 24)]
        public static void SetupQuality()
        {
            // Varsayilan olarak 6 kalite seviyesi gelir (Very Low ... Ultra).
            // Bunlar birbirinden farkli oldugu icen ayarlari TEK seviyeye
            // yazmak yaniltici olur: oyun seviye degistirilince ayarlar degisir.
            // Bu yuzden hepsine ayni performans odakli degerleri yaziyoruz.
            string[] levels = QualitySettings.names;
            int previous = QualitySettings.GetQualityLevel();

            for (int i = 0; i < levels.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);

                QualitySettings.antiAliasing = 0;                  // AA yok
                QualitySettings.pixelLightCount = 1;               // 4 isik -> 1
                QualitySettings.shadows = UnityEngine.ShadowQuality.All;
                QualitySettings.shadowResolution = UnityEngine.ShadowResolution.Medium;
                QualitySettings.vSyncCount = 1;                    // 60 FPS kilidi
                QualitySettings.realtimeReflectionProbes = false;
                QualitySettings.lodBias = 1f;
            }

            // Varsayilan seviye: "Medium". Dusuk sistem gereksinimi hedefi icin
            // "Ultra" gereksiz pahali, "Very Low" ise goruntuyu bozar.
            int medium = System.Array.IndexOf(levels, "Medium");
            if (medium < 0) medium = Mathf.Clamp(levels.Length / 2, 0, levels.Length - 1);

            QualitySettings.SetQualityLevel(medium, true);

            Debug.Log("[Kargo] Kalite: " + levels.Length + " seviyenin tamamina uygulandi, " +
                      "varsayilan = " + levels[medium] +
                      " (AA kapali, 1 piksel isigi, vsync acik, reflection probe yok).");
        }

        // ------------------------------------------------------------------
        // Player / Editor ayarlari
        // ------------------------------------------------------------------

        [MenuItem("Kargo/7. Player Ayarlari", priority = 25)]
        public static void SetupPlayerSettings()
        {
            PlayerSettings.companyName = "thevoidwalkerdev";
            PlayerSettings.productName = "Kargo Simulator";

            // Lineer renk uzayi - sadece zaten Linear degilse degistir,
            // aksi halde tum projeyi yeniden import eder (dakikalar surer).
            if (PlayerSettings.colorSpace != ColorSpace.Linear)
            {
                Debug.LogWarning("[Kargo] Renk uzayi Gamma -> Linear yapiliyor. Proje yeniden import edilecek, biraz surebilir.");
                PlayerSettings.colorSpace = ColorSpace.Linear;
            }

            PlayerSettings.runInBackground = false;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;

            // Hedef: genis PC kitlesi. Splash ekrani ve tanitim videolari gereksiz.
            PlayerSettings.SplashScreen.show = false;

            PlayerSettings.SetApplicationIdentifier(
                NamedBuildTarget.Standalone, "com.thevoidwalkerdev.kargosimulator");

            // Play moduna girmeden derlemeyi hizlandir.
            EditorSettings.serializationMode = SerializationMode.ForceText;
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;

            Debug.Log("[Kargo] Player ayarlari uygulandi. Domain Reload kapali -> Play cok daha hizli.");
        }

        // ------------------------------------------------------------------
        // GameConfig asset'i
        // ------------------------------------------------------------------

        [MenuItem("Kargo/8. GameConfig Olustur", priority = 26)]
        public static void CreateGameConfig()
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);
            if (existing != null)
            {
                Debug.Log("[Kargo] GameConfig zaten var, dokunulmadi.");
                return;
            }

            var config = ScriptableObject.CreateInstance<GameConfig>();
            AssetDatabase.CreateAsset(config, GameConfigPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Kargo] GameConfig olusturuldu: " + GameConfigPath);
        }

        // ------------------------------------------------------------------
        // Sahne
        // ------------------------------------------------------------------

        /// <summary>
        /// Toplu kurulumda sahneyi YALNIZCA yoksa olusturur.
        /// "Tum Kurulumu Calistir" aracini gelistirme ortaminda tekrar tekrar
        /// calistirmak normaldir; o an yapilan sahne degisiklikleri
        /// kazara silinmemelidir. Silmek icin menuunden acikca
        /// "9. Game Sahnesini Kur" calistirilir.
        /// </summary>
        private static void CreateGameSceneIfMissing()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                Debug.Log("[Kargo] Game.unity zaten var, dokunulmadi. " +
                          "Yeniden kurmak icin: Kargo/9. Game Sahnesini Kur");
                return;
            }

            CreateGameScene();
        }

        [MenuItem("Kargo/9. Game Sahnesini Kur", priority = 27)]
        public static void CreateGameScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null &&
                !EditorUtility.DisplayDialog(
                    "Sahne zaten var",
                    "Game.unity sahnesi zaten mevcut.\n\n" +
                    "Bu islem sahnedeki TUM nesneleri silip bastan olusturur. " +
                    "Yaptigin degisiklikler kaybolur.\n\nDevam edilsin mi?",
                    "Yine de olustur", "Iptal"))
            {
                return;
            }

            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);
            if (config == null)
            {
                CreateGameConfig();
                config = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- Zemin ---
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.layer = LayerGround;
            ground.transform.localScale = new Vector3(20f, 1f, 20f); // 200x200 birim
            GameObjectUtility.SetStaticEditorFlags(ground,
                StaticEditorFlags.BatchingStatic |
                StaticEditorFlags.OccluderStatic |
                StaticEditorFlags.OccludeeStatic);

            // --- Ana isik ---
            GameObject lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            light.intensity = 1.2f;
            light.color = new Color(1f, 0.96f, 0.9f);
            lightGo.AddComponent<UniversalAdditionalLightData>();
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // --- Kamera ---
            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.55f, 0.68f, 0.82f);
            cam.fieldOfView = 60f;
            camGo.AddComponent<UniversalAdditionalCameraData>();
            camGo.transform.position = new Vector3(0f, 18f, -22f);
            camGo.transform.rotation = Quaternion.Euler(35f, 0f, 0f);

            // --- Composition Root ---
            var rootGo = new GameObject("GameCompositionRoot");
            var root = rootGo.AddComponent<GameCompositionRoot>();
            AssignConfig(root, config);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            // --- Build settings ---
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
            };

            Debug.Log("[Kargo] Sahne olusturuldu: " + ScenePath);
        }

        private static void AssignConfig(GameCompositionRoot root, GameConfig config)
        {
            var so = new SerializedObject(root);
            so.FindProperty("config").objectReferenceValue = config;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------
        // Fixed Timestep: public API yok, YAML duzenlenir
        // ------------------------------------------------------------------

        /// <summary>
        /// Time.fixedDeltaTime kod ile kalici olarak degistirilemez (sadece
        /// calisma aninda gecerlidir). Kalici deger TimeManager.asset icindedir.
        ///
        /// Unity 6 bu degeri artik ondalik degil RASYONEL olarak sakliyor:
        ///   m_Count / m_Numerator  =  saniye cinsinden deger
        /// Varsayilan deger 2822399/141120000 = 0.02 (50 Hz) olarak gelir,
        /// yani cogu durumda bu adim zaten yapilmis durumdadir.
        /// </summary>
        [MenuItem("Kargo/10. Fixed Timestep 0.02", priority = 28)]
        public static void SetupFixedTimestep()
        {
            const string path = "ProjectSettings/TimeManager.asset";
            const double target = 0.02;

            if (!File.Exists(path))
            {
                Debug.LogError("[Kargo] TimeManager.asset bulunamadi.");
                return;
            }

            string yaml = File.ReadAllText(path);

            long count = ReadYamlLong(yaml, "m_Count:");
            long numerator = ReadYamlLong(yaml, "m_Numerator:");

            if (count < 0 || numerator <= 0)
            {
                Debug.LogError("[Kargo] TimeManager.asset beklenen formatta degil " +
                               "(m_Count=" + count + ", m_Numerator=" + numerator + ").");
                return;
            }

            double current = (double)count / numerator;
            if (Math.Abs(current - target) < 0.0005)
            {
                Debug.Log("[Kargo] Fixed Timestep zaten " +
                          current.ToString("0.0000") + " sn (50 Hz). Dokunulmadi.");
                return;
            }

            long newCount = (long)Math.Round(target * numerator);

            // Sadece m_Count degerini degistir, m_Numerator'a dokunma.
            int keyIndex = yaml.IndexOf("m_Count:", StringComparison.Ordinal);
            int valueStart = keyIndex + "m_Count:".Length;
            int lineEnd = yaml.IndexOf('\n', valueStart);
            if (lineEnd < 0) lineEnd = yaml.Length;

            string updated = yaml.Substring(0, valueStart) + " " + newCount + yaml.Substring(lineEnd);
            File.WriteAllText(path, updated);

            Debug.Log("[Kargo] Fixed Timestep " + current.ToString("0.0000") +
                      " -> 0.0200 sn (50 Hz). Editor yeniden baslatilmalidir.");
        }

        /// <summary>
        /// YAML'da "anahtar: deger" satirindan sayiyi okur.
        /// Unity 6'nin TimeManager.asset'i satir sonu olarak LF kullanir ve
        /// degerden sonra virgul YOKTUR - bu yuzden ilk satir ve bosluk
        /// temizlenerek alinir (virgule bolmek tum dosyayi dondurur).
        /// </summary>
        private static long ReadYamlLong(string yaml, string key)
        {
            int index = yaml.IndexOf(key, StringComparison.Ordinal);
            if (index < 0) return -1;

            int lineEnd = yaml.IndexOf('\n', index);
            if (lineEnd < 0) lineEnd = yaml.Length;

            string value = yaml.Substring(index + key.Length, lineEnd - index - key.Length).Trim();
            return long.TryParse(value, out long result) ? result : -1;
        }

        /// <summary>Tum adimlari sirayla calistirir.</summary>
        public static void RunAllSetupIncludingTimestep()
        {
            RunAllSetup();
            SetupFixedTimestep();
        }
    }
}
