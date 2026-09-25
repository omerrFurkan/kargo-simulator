using KargoSim.Delivery;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KargoSim.EditorTools
{
    /// <summary>
    /// MapLayout asset'ini uretir ve Composition Root'a baglar.
    ///
    /// Noktalar bir IZGARA olarak hesaplanir. Bu GECICI bir yerlesimdir:
    /// Aşama 8'de ayni kimlikli gercek binalar bu konumlara baglanacak.
    /// Asset'in kendisi degismeyecek, sadece uzerine gorsel kurulacak.
    /// </summary>
    public static class DeliverySetupTool
    {
        private const string LayoutPath = "Assets/ScriptableObjects/Database/MapLayout.asset";
        private const string ScenePath = "Assets/Scenes/Game.unity";

        private const int PointCount = 14;

        [MenuItem("Kargo/4. Harita ve Teslimat Noktalarini Kur", priority = 3)]
        public static void BuildLayout()
        {
            // Sahne BAŞTA acilir. Asamada uretilen isaretler sahneye
            // yazilir; arada sahne yeniden acilirsa hepsi kaybolur.
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var layout = AssetDatabase.LoadAssetAtPath<MapLayout>(LayoutPath);
            if (layout == null)
            {
                layout = ScriptableObject.CreateInstance<MapLayout>();
                AssetDatabase.CreateAsset(layout, LayoutPath);
            }

            // Depo merkezde. Noktalar cevresinde iki halka olarak dagilir:
            // yakin halka kisa teslimatlar, uzak halka "etrafi dolasmak" icin.
            layout.depotPosition = new Vector3(0f, 0f, 0f);
            layout.deliveryPoints = BuildPoints(PointCount);
            EditorUtility.SetDirty(layout);

            AssetDatabase.SaveAssets();

            BuildMarkers(layout);
            WireToCompositionRoot(layout);

            // Isaretler ve Composition Root degisiklikleri sahneye yazilir.
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Verify(layout);
            Debug.Log("[Kargo] Harita kuruldu: " + layout.deliveryPoints.Count + " teslimat noktasi.");
        }

        /// <summary>
        /// Her teslimat noktasi icin yerde renkli bir isaret olusturur.
        /// Aşama 8'de ayni yere bina kurulunca bu isaretler kapanabilir.
        /// </summary>
        private static void BuildMarkers(MapLayout layout)
        {
            // Onceki isaretleri ONCE tamamen temizle.
            //
            // Neden: eski surumde her calistirmada yeni bir "DeliveryStops"
            // koku olusturuluyor, temizlik de o yeni bos kokte
            // araniyordu. Hicbir sey bulunamayinca isaretler UST USTE
            // birikiyordu (harita iki kez kurulunca sahnede 28 ped).
            StopMarker[] old = Object.FindObjectsByType<StopMarker>(FindObjectsSortMode.None);
            for (int i = 0; i < old.Length; i++)
                Object.DestroyImmediate(old[i].gameObject);

            // Temizlikten sonra bos kalan kokleri de kaldir. GameObject.Find
            // yalnizca ILK eslesmeyi dondurur; harita birkac kez kurulduysa
            // birden fazla kop olabilir, bu yuzden tum kokler taranir.
            var roots = EditorSceneManager.GetActiveScene().GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == "DeliveryStops")
                    Object.DestroyImmediate(roots[i]);
            }

            var rootGo = new GameObject("DeliveryStops");
            rootGo.transform.position = Vector3.zero;
            Transform root = rootGo.transform;

            for (int i = 0; i < layout.deliveryPoints.Count; i++)
            {
                DeliveryPointData point = layout.deliveryPoints[i];

                // Eski isaret temizligi yukarida toplu olarak yapildi.
                var go = new GameObject("Stop_" + point.id);
                go.transform.SetParent(root, false);
                go.transform.position = point.position + new Vector3(0f, 0.06f, 0f);

                // Yerde yatan renkli kare
                GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pad.name = "Pad";
                Object.DestroyImmediate(pad.GetComponent<Collider>());
                pad.transform.SetParent(go.transform, false);
                pad.transform.localScale = new Vector3(6f, 0.1f, 6f);
                pad.transform.localPosition = Vector3.zero;

                var renderer = pad.GetComponent<Renderer>();
                renderer.sharedMaterial = GetMarkerMaterial();

                // Ad etiketi: yere yatay, kameraya bakinca okunur
                var labelGo = new GameObject("Label");
                labelGo.transform.SetParent(go.transform, false);
                labelGo.transform.localPosition = new Vector3(0f, 0.12f, 0f);
                // Yerden okunabilirlik icin yatay duruyor (billboard yok:
                // her marker'a Update yazmak "tek Update" kuralini bozardi)
                labelGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                labelGo.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);

                var marker = go.AddComponent<StopMarker>();
                var so = new SerializedObject(marker);
                so.FindProperty("padRenderer").objectReferenceValue = renderer;
                so.FindProperty("billboard").objectReferenceValue = labelGo.transform;
                so.ApplyModifiedPropertiesWithoutUndo();

                marker.Configure(point.id, point.displayName);
                EditorUtility.SetDirty(marker);
            }
        }

        private static Material GetMarkerMaterial()
        {
            const string path = "Assets/Art/Materials/DeliveryMarker.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                // Eski material yanlis emission keyword tasiyordu
                // ("_EMISSION_COLOR" degil "_EMISSION") ve 0.25 gri additive
                // emission tanimliydi. Additive emission taban rengini
                // boguyor; renkli isaretler ekranda BEYAZ gorunuyordu.
                // Var olan material de duzeltilir.
                ApplyMarkerMaterialSettings(existing);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[Kargo] URP/Lit shader bulunamadi.");
                return null;
            }

            var material = new Material(shader);
            ApplyMarkerMaterialSettings(material);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>
        /// Isaret materyalinin ayarlari.
        /// EMISSION KAPALI: isaretler zeminde yatar ve yonelmis isikla
        /// aydinlanir, ek isiye gerek yok; additive emission taban
        /// rengini boguyordu.
        /// </summary>
        private static void ApplyMarkerMaterialSettings(Material material)
        {
            material.SetColor("_BaseColor", Color.white);
            material.DisableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", Color.black);
            material.SetFloat("_Smoothness", 0.1f);
            material.SetFloat("_Metallic", 0f);
        }

        /// <summary>
        /// Iki halkali izgara. Duz bir cizgi olsaydi rota siralamasi
        /// anlamsizlasirdi (her zaman ayni yon).
        /// </summary>
        private static System.Collections.Generic.List<DeliveryPointData> BuildPoints(int count)
        {
            var points = new System.Collections.Generic.List<DeliveryPointData>(count);

            int inner = Mathf.CeilToInt(count * 0.55f);
            int outer = count - inner;

            for (int i = 0; i < inner; i++)
            {
                float angle = (2f * Mathf.PI * i) / inner;
                points.Add(new DeliveryPointData
                {
                    id = i,
                    displayName = AdresName(i),
                    position = new Vector3(Mathf.Cos(angle) * 55f, 0f, Mathf.Sin(angle) * 55f),
                });
            }

            for (int i = 0; i < outer; i++)
            {
                float angle = (2f * Mathf.PI * (i + 0.5f)) / outer;
                points.Add(new DeliveryPointData
                {
                    id = inner + i,
                    displayName = AdresName(inner + i),
                    position = new Vector3(Mathf.Cos(angle) * 95f, 0f, Mathf.Sin(angle) * 95f),
                });
            }

            return points;
        }

        private static string AdresName(int index)
        {
            string[] kinds = { "Ev", "Apartman", "Market", "Is Merkezi", "Kucuk Isletme" };
            return kinds[index % kinds.Length] + " #" + (index + 1);
        }

        private static void WireToCompositionRoot(MapLayout layout)
        {
            // DIKKAT: Sahneyi BURADA yeniden ACMA. BuildLayout basinda
            // acik olan sahne, bu asamada kaydedilmemis isaret degisiklikleri
            // tasiyor; OpenScene(Single) ile tekrar acmak onlari yok eder.
            // (Once bir teslim araci bu yuzden kayboldu.)
            var root = Object.FindFirstObjectByType<Core.GameCompositionRoot>();

            if (root == null)
            {
                Debug.LogError("[Kargo] Sahnede GameCompositionRoot bulunamadi. " +
                               "Once sahneyi kurun: Kargo/1. Tum Kurulumu Calistir");
                return;
            }

            root.SetMapForSetup(layout, 7f);
            EditorUtility.SetDirty(root);

            Debug.Log("[Kargo] Composition Root'a MapLayout baglandi.");
        }

        private static void Verify(MapLayout layout)
        {
            var problems = new System.Text.StringBuilder();

            if (layout.deliveryPoints.Count == 0)
                problems.Append(" Teslimat noktasi yok.");

            for (int i = 0; i < layout.deliveryPoints.Count; i++)
            {
                int id = layout.deliveryPoints[i].id;

                // Ayni iki kimlik birbirine karisirdi: kargolar yanlis
                // adrese giderdi.
                for (int j = i + 1; j < layout.deliveryPoints.Count; j++)
                {
                    if (layout.deliveryPoints[j].id != id) continue;
                    problems.Append(" Yinelenen adres kimligi: " + id + ".");
                }
            }

            var config = AssetDatabase.LoadAssetAtPath<Core.GameConfig>(
                "Assets/ScriptableObjects/Database/GameConfig.asset");
            if (config != null && config.destinationCount != layout.deliveryPoints.Count)
            {
                problems.Append(" GameConfig.destinationCount (" + config.destinationCount +
                                ") MapLayout nokta sayisiyla (" + layout.deliveryPoints.Count +
                                ") eslesmiyor. Kargolar bos adres alabilir.");
            }

            VerifyMarkerColors(problems);

            if (problems.Length == 0)
                Debug.Log("[Kargo] Dogrulama OK: adres kimligi benzersiz, isaret renkleri uygulandi.");
            else
                Debug.LogError("[Kargo] Dogrulama BASARISIZ:" + problems);
        }

        /// <summary>
        /// Isaret renklerinin gercekten uygulandigini dogrular.
        ///
        /// Once ekranda isaretler beyaz gorunuyordu. Iki olasi sebep vardi:
        /// (a) additive emission taban rengini boguyordu,
        /// (b) MaterialPropertyBlock yazilmamis.
        /// Ayirt etmek icin bloktan okunan rengi okuyoruz.
        /// </summary>
        private static void VerifyMarkerColors(System.Text.StringBuilder problems)
        {
            var markers = Object.FindObjectsByType<StopMarker>(FindObjectsSortMode.None);
            if (markers.Length == 0)
            {
                problems.Append(" Sahnede durak isareti yok.");
                return;
            }

            int painted = 0;   // rengi yazilmis isaret
            int distinct = 0;  // aktif durak disinda gercekten renkli isaret
            for (int i = 0; i < markers.Length; i++)
            {
                Renderer renderer = markers[i].GetComponentInChildren<Renderer>();
                if (renderer == null) continue;

                var block = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(block);

                if (!block.HasProperty("_BaseColor")) continue;
                painted++;

                // Aktif durak BILEREK beyazdir (hedef vurgusu yerine
                // gecici olarak). O yuzden "beyaz olmamali" degil,
                // "hicbiri renkli degil" diye kontrol ediyoruz.
                if (block.GetColor("_BaseColor") != Color.white) distinct++;
            }

            if (painted == 0)
            {
                problems.Append(" hicbir isaretin rengi yazilmamis (MaterialPropertyBlock bos). " +
                                "Isaretler ekranda beyaz gorunecek.");
            }
            else if (distinct == 0)
            {
                problems.Append(" isaretler beyaz: emission taban rengi boguyor olabilir.");
            }
            else
            {
                Debug.Log("[Kargo] Isaret kontrolu: " + painted + " isaretin rengi yazildi, " +
                          distinct + " / " + markers.Length + " tanesi renkli.");
            }
        }
    }
}
