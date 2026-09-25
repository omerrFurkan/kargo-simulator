using System.IO;
using KargoSim.Core;
using KargoSim.Delivery;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KargoSim.Tests
{
    /// <summary>
    /// Asama 1'in cikti dogrulugunu kontrol eder: sahne acilabiliyor mu,
    /// CompositionRoot yapilandirilmis mi, temel nesneler yerinde mi.
    ///
    /// Editor'de elle bakmak yerine otomatik test: sahne kaydedilip
    /// yanlislikla bozuldugunda (ornegin GameConfig referansi koptugunda)
    /// aninda yakalar.
    /// </summary>
    public sealed class GameSceneIntegrityTests
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";
        private const string LayerGround = "Ground";

        private Scene _scene;

        [SetUp]
        public void SetUp()
        {
            Assert.IsTrue(File.Exists(ScenePath), "Game.unity bulunamadi: " + ScenePath);
            _scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        [TearDown]
        public void TearDown()
        {
            if (_scene.IsValid() && !_scene.isLoaded) return;
        }

        [Test]
        public void SahneYuklenir()
        {
            Assert.IsTrue(_scene.IsValid());
            Assert.IsTrue(_scene.isLoaded);
        }

        [Test]
        public void CompositionRootSahnedeVeYapilandirilmis()
        {
            var roots = _scene.GetRootGameObjects();
            GameCompositionRoot root = null;
            foreach (var go in roots)
            {
                root = go.GetComponent<GameCompositionRoot>();
                if (root != null) break;
            }

            Assert.IsNotNull(root, "Sahnede GameCompositionRoot bulunamadi.");
            Assert.IsNotNull(root.Config,
                "GameCompositionRoot.gameConfig ATANMAMIS. " +
                "Kargo/1. Tum Kurulumu Calistir menusunu calistir.");
        }

        [Test]
        public void ZeminDogruKatmandaVeStatikIsaretli()
        {
            var ground = GameObject.Find("Ground");
            Assert.IsNotNull(ground, "Sahnede 'Ground' zemini yok.");

            Assert.AreEqual(LayerGround, LayerMask.LayerToName(ground.layer),
                "Zemin 'Ground' katmaninda olmali (fizik matrisine gore).");

            Assert.IsTrue(GameObjectUtility.AreStaticEditorFlagsSet(ground, StaticEditorFlags.BatchingStatic),
                "Zemin static isaretli olmali: static batching + occlusion culling saglar.");
        }

        [Test]
        public void YonelmisIsikVeKameraVar()
        {
            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            bool hasDirectional = false;
            foreach (var l in lights)
            {
                if (l.type == LightType.Directional) { hasDirectional = true; break; }
            }
            Assert.IsTrue(hasDirectional,
                "Yonelmis isik yok. Aydinlatma tek isiga bagli, bake edilebilir olmali.");

            Assert.IsNotNull(Camera.main, "Main Camera yok ya da MainCamera etiketi atanmemis.");
        }

        [Test]
        public void SahneBuildSettingsListesindeVeEtkin()
        {
            var scenes = EditorBuildSettings.scenes;
            bool found = false;
            foreach (var s in scenes)
            {
                if (s.path == ScenePath && s.enabled) { found = true; break; }
            }
            Assert.IsTrue(found, "Game.unity Build Settings listesinde etkin degil.");
        }

        [Test]
        public void CompositionRootKargoIcerigiBagli()
        {
            var roots = _scene.GetRootGameObjects();
            GameCompositionRoot root = null;
            foreach (var go in roots)
            {
                root = go.GetComponent<GameCompositionRoot>();
                if (root != null) break;
            }
            Assert.IsNotNull(root, "GameCompositionRoot bulunamadi.");

            // Bu baglar yoksa oyun sessizce acilir ama HICBIR kargo
            // uretilmez - oyuncunun karsilasmasi en kotu senaryo.
            var so = new SerializedObject(root);
            SerializedProperty types = so.FindProperty("parcelTypes");
            SerializedProperty vehicle = so.FindProperty("playerVehicle");

            Assert.IsNotNull(types, "parcelTypes alani bulunamadi.");
            Assert.IsNotNull(vehicle, "playerVehicle alani bulunamadi.");

            Assert.Greater(types.arraySize, 0,
                "Composition Root'a hic ParcelType baglanmadi - kargo uretilmez. " +
                "Menu: Kargo/3. Icerik Assetlerini Kur");

            Assert.IsNotNull(vehicle.objectReferenceValue,
                "Composition Root'a arac tanimi baglanmadi - hicbir kargo araca sigmaz. " +
                "Menu: Kargo/Vehicle/Arac Prefabini Kur");

            SerializedProperty layout = so.FindProperty("mapLayout");
            Assert.IsNotNull(layout, "mapLayout alani bulunamadi.");
            Assert.IsNotNull(layout.objectReferenceValue,
                "Composition Root'a MapLayout baglanmadi - hicbir adrese gidilemez. " +
                "Menu: Kargo/4. Harita ve Teslimat Noktalarini Kur");
        }

        [Test]
        public void HaritaNoktaSayisiOyunAyariylaUyumlu()
        {
            // destinationCount ile MapLayout nokta sayisi eslesmezse
            // kargolarin bir kismi var olmayan adres alir ve oyuncu
            // neden teslim edemedigini anlamaz.
            var layout = AssetDatabase.LoadAssetAtPath<Delivery.MapLayout>(
                "Assets/ScriptableObjects/Database/MapLayout.asset");
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(
                "Assets/ScriptableObjects/Database/GameConfig.asset");

            Assert.IsNotNull(layout, "MapLayout.asset bulunamadi.");
            Assert.IsNotNull(config, "GameConfig.asset bulunamadi.");
            Assert.AreEqual(config.destinationCount, layout.deliveryPoints.Count,
                "GameConfig.destinationCount ile MapLayout nokta sayisi eslesmiyor.");
        }
    }
}
