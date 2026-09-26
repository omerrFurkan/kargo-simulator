using System.IO;
using System.Text;
using KargoSim.Delivery;
using KargoSim.Player;
using KargoSim.Vehicle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KargoSim.EditorTools
{
    /// <summary>
    /// Dükkânı ve etkileşim noktalarını kurar.
    ///
    /// Dükkân, deponun üzerinde durduğu yerde bir bina: tezgah, raf ve
    /// üstünde kargo yığını. Oyuncu karakteri FPS olarak içeri girer,
    /// raftan kutu alır, aracın arkasına götürür.
    ///
    /// Ayrıca her teslimat noktasına bir kapı ve araca bir kargo noktası
    /// ekler. Böylece "E" tuşunun ne yapacağını hedef kendisi söyler.
    /// </summary>
    public static class ShopSetupTool
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";
        private const string ShopRootName = "Shop";
        private const string ShelfName = "ParcelShelf";
        private const string AddressRootName = "AddressDoors";
        private const string VanPointName = "VanCargoPoint";
        private const string WallMaterialPath = "Assets/Art/Materials/ShopWall.mat";
        private const string WoodMaterialPath = "Assets/Art/Materials/ShopWood.mat";

        private const float ShopWidth = 9f;
        private const float ShopDepth = 7f;
        private const float ShopHeight = 4.2f;

        [MenuItem("Kargo/8. Dukkani Kur", priority = 7)]
        public static void Build()
        {
            var report = new StringBuilder();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var root = Object.FindFirstObjectByType<Core.GameCompositionRoot>();
            if (root == null)
            {
                Debug.LogError("[Kargo] GameCompositionRoot yok.");
                return;
            }

            var layoutSo = new SerializedObject(root);
            var layout = layoutSo.FindProperty("mapLayout").objectReferenceValue as MapLayout;
            if (layout == null)
            {
                Debug.LogError("[Kargo] MapLayout atanmadi. Once Kargo/4. Harita kur.");
                return;
            }

            BuildShop(layout.depotPosition, report);
            BuildAddressDoors(layout, report);
            BuildVanCargoPoint(report);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            AssetDatabase.SaveAssets();
            Debug.Log("[Kargo] Dukkan kuruldu.\n" + report);
        }

        // ------------------------------------------------------------------
        // Dukkan
        // ------------------------------------------------------------------

        private static void BuildShop(Vector3 depot, StringBuilder report)
        {
            // Eskiyi temizle: menü tekrar calistirilabilir olmali.
            GameObject stale = GameObject.Find(ShopRootName);
            if (stale != null) Object.DestroyImmediate(stale);

            Material wall = CreateMaterial(WallMaterialPath, new Color(0.86f, 0.84f, 0.78f));
            Material wood = CreateMaterial(WoodMaterialPath, new Color(0.55f, 0.38f, 0.22f));

            var shop = new GameObject(ShopRootName);
            // Dükkân, deponun biraz gerisinde: araç önüne park eder,
            // raf arkada görünür.
            shop.transform.position = depot + new Vector3(0f, 0f, -7f);
            shop.transform.rotation = Quaternion.identity;

            // --- Gövde: çatısız dört duvar (önü açık) ---
            CreateBox("Wall_Back", shop.transform,
                new Vector3(0f, ShopHeight * 0.5f, -ShopDepth * 0.5f),
                new Vector3(ShopWidth, ShopHeight, 0.3f), wall);

            CreateBox("Wall_Left", shop.transform,
                new Vector3(-ShopWidth * 0.5f, ShopHeight * 0.5f, 0f),
                new Vector3(0.3f, ShopHeight, ShopDepth), wall);

            CreateBox("Wall_Right", shop.transform,
                new Vector3(ShopWidth * 0.5f, ShopHeight * 0.5f, 0f),
                new Vector3(0.3f, ShopHeight, ShopDepth), wall);

            // Çatı
            CreateBox("Roof", shop.transform,
                new Vector3(0f, ShopHeight + 0.15f, 0f),
                new Vector3(ShopWidth + 0.6f, 0.3f, ShopDepth + 0.6f), wood);

            // --- Tezgah (rafın altı) ---
            CreateBox("Counter", shop.transform,
                new Vector3(0f, 0.5f, -ShopDepth * 0.5f + 0.7f),
                new Vector3(ShopWidth - 1.2f, 1.0f, 0.7f), wood);

            // --- Kargo rafı + etkileşim noktası ---
            var shelf = new GameObject(ShelfName);
            shelf.transform.SetParent(shop.transform, false);
            shelf.transform.localPosition = new Vector3(0f, 1.0f, -ShopDepth * 0.5f + 1.1f);

            CreateBox("ShelfBoard", shelf.transform, Vector3.zero,
                new Vector3(ShopWidth - 2f, 0.12f, 0.8f), wood);

            // Kutuların oturacağı yüzey (görsel yığın buraya dizilir)
            var visualRoot = new GameObject("Boxes");
            visualRoot.transform.SetParent(shelf.transform, false);
            visualRoot.transform.localPosition = new Vector3(0f, 0.06f, 0f);

            // Etkileşim için görünmez tetikleyici
            var trigger = new GameObject("Interact");
            trigger.transform.SetParent(shelf.transform, false);
            trigger.transform.localPosition = new Vector3(0f, 0.6f, 0.7f);
            var box = trigger.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(ShopWidth - 2f, 1.6f, 1.8f);

            trigger.AddComponent<ShopParcelPile>();

            // --- Paketleme masasi (rafin yaninda) ---
            BuildPackingTable(shop.transform, wood, report);

            report.AppendLine("  dukkan     : " + shop.transform.position.ToString("0.0")
                              + "  (" + ShopWidth + "x" + ShopDepth + " m)");
        }

        // ------------------------------------------------------------------
        // Paketleme masasi
        // ------------------------------------------------------------------

        private const string PackingTableName = "PackingTable";
        private const string PackingWorkRootName = "WorkVisual";

        /// <summary>
        /// Paketleme masasini kurar: rafin sag on tarafinda, oyuncunun
        /// dogrudan yuruyerek erisebilecegi bir konum.
        ///
        /// TODO(agent): GameCompositionRoot baglantisi ayrica eklenecek
        /// PackingTable.Configure(packaging, courier, "WorkVisual", timeProvider)
        /// cagrisi CompositionRoot tarafinda yapilacak. Bu arac yalnizca
        /// NESNEYI kurar; sistemler calisma aninda dogar.
        /// </summary>
        private static void BuildPackingTable(Transform shop, Material wood, StringBuilder report)
        {
            // Eskiyi temizle: menu tekrar calistirilabilir olmali.
            Transform stale = shop.Find(PackingTableName);
            if (stale != null) Object.DestroyImmediate(stale.gameObject);

            var table = new GameObject(PackingTableName);
            table.transform.SetParent(shop, false);

            // Konum: raf arka duvarda (local z = -2.4) ve merkezde. Masa
            // onun sag-inde ve biraz onde; oyuncu rafla mesa arasinda
            // ikisini de tek noktada degil, "raf -> masa" yonunde
            // karsilasir.
            table.transform.localPosition = new Vector3(2.8f, 0f, -1.2f);
            table.transform.localRotation = Quaternion.identity;

            // --- Gorsel: tabla + dort bacak (hepsi collider'siz) ---
            CreateBox("TableTop", table.transform, new Vector3(0f, 0.9f, 0f),
                new Vector3(1.8f, 0.12f, 1.0f), wood);

            CreateBox("TableLeg_A", table.transform, new Vector3(-0.8f, 0.45f, 0.4f),
                new Vector3(0.12f, 0.9f, 0.12f), wood);
            CreateBox("TableLeg_B", table.transform, new Vector3(0.8f, 0.45f, 0.4f),
                new Vector3(0.12f, 0.9f, 0.12f), wood);
            CreateBox("TableLeg_C", table.transform, new Vector3(-0.8f, 0.45f, -0.4f),
                new Vector3(0.12f, 0.9f, 0.12f), wood);
            CreateBox("TableLeg_D", table.transform, new Vector3(0.8f, 0.45f, -0.4f),
                new Vector3(0.12f, 0.9f, 0.12f), wood);

            // Kargonun cizilecegi yuzey. CompositionRoot bu cocugu bulup
            // PackingTable.Configure'in ucuncu parametresine gecirir.
            var work = new GameObject(PackingWorkRootName);
            work.transform.SetParent(table.transform, false);
            work.transform.localPosition = new Vector3(0f, 0.97f, 0f);

            // Etkilesim hacmi: masa govdesi + onundeki bos alan. Raf ve
            // adres kapisiyla ayni kurulum: normal BoxCollider, isTrigger
            // ACILIR (ScanForFocus QueryTriggerInteraction.Collide ile
            // tetikleyicileri de yakalar).
            var box = table.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(2.2f, 1.8f, 2.2f);
            box.center = new Vector3(0f, 0.9f, 0.4f);

            table.AddComponent<PackingTable>();

            report.AppendLine("  paketleme  : " + PackingTableName
                              + " yerel (" + table.transform.localPosition + ")");
        }

        // ------------------------------------------------------------------
        // Adres kapilari
        // ------------------------------------------------------------------

        private static void BuildAddressDoors(MapLayout layout, StringBuilder report)
        {
            GameObject stale = GameObject.Find(AddressRootName);
            if (stale != null) Object.DestroyImmediate(stale);

            var rootGo = new GameObject(AddressRootName);
            int count = 0;

            for (int i = 0; i < layout.deliveryPoints.Count; i++)
            {
                DeliveryPointData point = layout.deliveryPoints[i];

                var door = new GameObject("Door_" + point.id);
                door.transform.SetParent(rootGo.transform, false);
                door.transform.position = point.position + new Vector3(0f, 0f, 2.2f);

                // Kapı levhası (görsel)
                CreateBox("Plate", door.transform, new Vector3(0f, 1.1f, 0f),
                    new Vector3(1.2f, 2.2f, 0.15f),
                    CreateMaterial("Assets/Art/Materials/DoorPlate.mat",
                                   new Color(0.30f, 0.45f, 0.65f)));

                // Etkileşim tetikleyicisi
                var trigger = new GameObject("Interact");
                trigger.transform.SetParent(door.transform, false);
                trigger.transform.localPosition = new Vector3(0f, 0.8f, 1.4f);
                var box = trigger.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(2.4f, 2f, 2.2f);

                trigger.AddComponent<AddressDoor>();
                count++;
            }

            report.AppendLine("  adres kapisi: " + count + " adet");
        }

        // ------------------------------------------------------------------
        // Arac kargo noktasi
        // ------------------------------------------------------------------

        private static void BuildVanCargoPoint(StringBuilder report)
        {
            var vehicle = Object.FindFirstObjectByType<ArcadeVehicleController>();
            if (vehicle == null)
            {
                Debug.LogWarning("[Kargo] Araç yok; kargo noktası kurulamadı.");
                return;
            }

            Transform existing = vehicle.transform.Find(VanPointName);
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var point = new GameObject(VanPointName);
            point.transform.SetParent(vehicle.transform, false);
            // Arac arka kapi: model -Z yuzu (on +Z).
            point.transform.localPosition = new Vector3(0f, 0.6f, -2.2f);

            var box = point.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(3.2f, 2.4f, 1.6f);

            point.AddComponent<VanCargoPoint>();
            report.AppendLine("  arac noktasi: " + VanPointName);
        }

        // ------------------------------------------------------------------
        // Yardimcilar
        // ------------------------------------------------------------------

        private static GameObject CreateBox(string name, Transform parent, Vector3 localPosition,
                                            Vector3 size, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());

            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = size;

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null && material != null) renderer.sharedMaterial = material;
            return go;
        }

        private static Material CreateMaterial(string path, Color color)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[Kargo] URP/Lit shader bulunamadi.");
                return null;
            }

            if (!AssetDatabase.IsValidFolder("Assets/Art/Materials"))
                AssetDatabase.CreateFolder("Assets/Art", "Materials");

            var material = new Material(shader) { color = color, name = Path.GetFileNameWithoutExtension(path) };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
