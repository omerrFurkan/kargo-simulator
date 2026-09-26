using System.IO;
using System.Linq;
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

            // ASAMA 16: MainCamera etiketi oyuncunun FPS kamerasindadir.
            //
            // Once etiket vanin takip kamerasinda ("Main Camera") idi. O
            // kamera hicbir zaman kapatilmadigi icin iki kamera ayni anda
            // acik kaliyordu ve oyuncu aracinda SABIT bir goruntu
            // goruyordu.
            Assert.IsNotNull(Camera.main,
                "Main Camera yok ya da MainCamera etiketi atanmemis.");

            // Etiketli kamera oyuncunun kamerasi olmali. Arac kamerasi
            // etiketliyse Camera.main araci gosterir ve oyuncu kendi
            // kamerasini gormez.
            var courier = UnityEngine.Object.FindFirstObjectByType<KargoSim.Player.CourierController>();
            Assert.IsNotNull(courier, "Kurye yok.");
            var fps = courier.GetComponentInChildren<Camera>(true);
            Assert.IsNotNull(fps, "Kuryenin FPS kamerasi yok.");
            Assert.AreSame(fps, Camera.main,
                "MainCamera etiketi FPS kamerasinda olmali. Etiket arac " +
                "kamerasinda ise Camera.main araci takip eder ve oyuncu " +
                "kendi bakis acisini gormez.");
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

            SerializedProperty economy = so.FindProperty("economyConfig");
            Assert.IsNotNull(economy, "economyConfig alani bulunamadi.");
            Assert.IsNotNull(economy.objectReferenceValue,
                "Composition Root'a EconomyConfig baglanmadi - odeme yapilmayacak, " +
                "oyuncu para kazanamaz. Menu: Kargo/5. Ilerleme Ayarlarini Kur");
        }

        [Test]
        public void ArayuzSahnedeVeBagli()
        {
            // UI yoksa oyun "calisir" ama oyuncu ne yapacagini bilmez:
            // hangi kargoyu nereye goturecegini goremez. Bu yuzden
            // varligi ayri bir kosul olarak dogrulanir.
            var ui = UnityEngine.Object.FindFirstObjectByType<UI.UIManager>();
            Assert.IsNotNull(ui, "Sahnede UIManager yok. Menu: Kargo/6. Arayuzu Kur");

            var so = new SerializedObject(ui);
            SerializedProperty hud = so.FindProperty("hud");
            SerializedProperty depot = so.FindProperty("depotView");
            SerializedProperty minimap = so.FindProperty("minimap");

            Assert.IsNotNull(hud.objectReferenceValue, "HUD görünümü bağlı değil.");
            Assert.IsNotNull(depot.objectReferenceValue, "Depo paneli bağlı değil.");
            Assert.IsNotNull(minimap.objectReferenceValue, "Minimap bağlı değil.");

            // Canvas olmadan UI calisir mi?
            Canvas canvas = ui.GetComponent<Canvas>();
            Assert.IsNotNull(canvas, "UIManager'ın üzerinde Canvas olmalı.");
        }

        [Test]
        public void PanelReferanslariAtanmisOlmali()
        {
            // Regresyon (gercek bir hatayi yakaladi): UiSetupTool'da
            // CreatePanel() bir RectTransform donuyor, ama DepotView/StopView/
            // DaySummaryView'in "panel" alani GameObject tipinde. SerializedObject
            // tip uyusmazligini SESSIZCE reddedip alani null birakiyordu.
            // Sonuc: paneller hicbir zaman acilmadi ve oyuncu "E/Tab
            // calismiyor" sanip gercekten calismayan tuslari sucladi.
            //
            // Panel KOKUNUN aktif olmasi (PanelKokleriAcikKalmali) bu hatayi
            // yakalayamaz: kok aktif olabilir ama ic panelin referansi null
            // olabilir. Bu yuzden alanlarin degerini ayrica kontrol ediyoruz.
            AssertAssigned<UI.DepotView>("DepotPanel", "panel");
            AssertAssigned<UI.StopView>("StopPanel", "panel");
            AssertAssigned<UI.DaySummaryView>("DaySummary", "panel");
            AssertAssigned<UI.HudView>("HUD", "moneyText");
            AssertAssigned<UI.HudView>("HUD", "hintText");
            AssertAssigned<UI.MinimapView>("Minimap", "area");
        }

        private void AssertAssigned<T>(string gameObjectName, string fieldName)
            where T : Component
        {
            T view = _scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<T>(true))
                .FirstOrDefault();

            Assert.IsNotNull(view, "Sahnede '" + gameObjectName + "' üzerinde " +
                                    typeof(T).Name + " bulunamadı.");

            var so = new SerializedObject(view);
            SerializedProperty prop = so.FindProperty(fieldName);
            Assert.IsNotNull(prop, typeof(T).Name + "." + fieldName + " alanı bulunamadı.");

            Assert.IsNotNull(prop.objectReferenceValue,
                gameObjectName + "." + fieldName + " BOŞ — bu panel açılmaz. " +
                "Menu: Kargo/6. Arayuzu Kur");
        }

        [Test]
        public void PanelKokleriAcikKalmali()
        {
            // Regresyon: UiSetupTool panel nesnelerinin GameObject'ini
            // inactive birakiyordu. Cocuk bir panel acsa bile (SetActive(true))
            // gorunmez cunku GameObject inactive iken cocugu da render
            // edilmez. Oyuncu "Tab/E calismiyor" saniyordu.
            var depot = UnityEngine.Object.FindFirstObjectByType<UI.DepotView>();
            var stop = UnityEngine.Object.FindFirstObjectByType<UI.StopView>();
            var summary = UnityEngine.Object.FindFirstObjectByType<UI.DaySummaryView>();

            Assert.IsNotNull(depot, "DepotView yok.");
            Assert.IsNotNull(stop, "StopView yok.");
            Assert.IsNotNull(summary, "DaySummaryView yok.");

            Assert.IsTrue(depot.gameObject.activeInHierarchy,
                "Depot panelinin GameObject'i kapali: acilma komutu etkisiz kalir.");
            Assert.IsTrue(stop.gameObject.activeInHierarchy,
                "Teslimat panelinin GameObject'i kapali.");
            Assert.IsTrue(summary.gameObject.activeInHierarchy,
                "Gun sonu panelinin GameObject'i kapali.");
        }

        [Test]
        public void DurakIsaretleriSahnedeVeMukerrerDegil()
        {
            // Noktalar haritada görünmezse oyuncu hedefi arayamaz.
            //
            // Regresyon: eski BuildMarkers her çalıştırmada yeni bir
            // "DeliveryStops" kökü açıp temizliği o boş kökte arıyordu.
            // Sonuç: harita iki kez kurulunca sahnede 28 ped birikiyordu
            // (14 noktaya karşılık 28 isaret) ve oyuncu aynı hedefi iki
            // kez görüyordu. Varlık kontrolü ("> 0") bunu kaçırıyordu;
            // bu yüzden sayı artık harita verisiyle birebir eşleşmeli.
            int markers = 0;
            var all = _scene.GetRootGameObjects();
            for (int i = 0; i < all.Length; i++)
            {
                markers += all[i].GetComponentsInChildren<Delivery.StopMarker>(true).Length;
            }

            Assert.Greater(markers, 0,
                "Sahnede durak işareti yok - hedefler dünyada görünmüyor. " +
                "Menu: Kargo/4. Harita ve Teslimat Noktalarini Kur");

            var layout = AssetDatabase.LoadAssetAtPath<Delivery.MapLayout>(
                "Assets/ScriptableObjects/Database/MapLayout.asset");
            Assert.IsNotNull(layout, "MapLayout.asset bulunamadi.");

            Assert.AreEqual(layout.deliveryPoints.Count, markers,
                "Sahnedeki durak işareti sayısı harita nokta sayısıyla eşleşmiyor. " +
                "Muhtemelen harita iki kez kuruldu ve eski işaretler silinmedi " +
                "(her noktaya birden fazla renkli ped bindirmiş olabilir). " +
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
