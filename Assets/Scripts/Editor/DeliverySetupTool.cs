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

            WireToCompositionRoot(layout);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Verify(layout);
            Debug.Log("[Kargo] Harita kuruldu: " + layout.deliveryPoints.Count + " teslimat noktasi.");
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
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var root = Object.FindFirstObjectByType<Core.GameCompositionRoot>();

            if (root == null)
            {
                Debug.LogError("[Kargo] Sahnede GameCompositionRoot bulunamadi. " +
                               "Once sahneyi kurun: Kargo/1. Tum Kurulumu Calistir");
                return;
            }

            root.SetMapForSetup(layout, 7f);
            EditorUtility.SetDirty(root);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

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

            if (problems.Length == 0)
                Debug.Log("[Kargo] Dogrulama OK: tum adres kimligi benzersiz.");
            else
                Debug.LogError("[Kargo] Dogrulama BASARISIZ:" + problems);
        }
    }
}
