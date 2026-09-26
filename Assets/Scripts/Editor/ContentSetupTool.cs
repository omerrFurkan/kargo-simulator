using KargoSim.Cargo;
using KargoSim.Vehicle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KargoSim.EditorTools
{
    /// <summary>
    /// Kargo turlerini (ParcelType) uretir ve Composition Root'a baglar.
    ///
    /// Elle Inspector'dan 6 asset olusturmak ve sahneye baglamak yerine
    /// buradan tek komutla kurulur. Icerik kod degil VERI'dir; uretim
    /// yine de tekrarlanabilir olmali.
    /// </summary>
    public static class ContentSetupTool
    {
        private const string ParcelFolder = "Assets/ScriptableObjects/Definitions/Parcels";
        private const string VehicleDefPath = "Assets/ScriptableObjects/Definitions/VehicleDefinition_CourierVan.asset";

        /// <summary>
        /// Kargo turu tanimlari.
        ///
        /// Bunlar oynun "zorluk kalibrasyonu"dur:
        ///  - En ucuz/en hafif tur her gun olusabilir (ogrenme kolayligi)
        ///  - En pahali/en agir tur nadir olmalidir, yoksa oyunucu
        ///    ilk gunden her seyi yapabilir ve gecis olmaz
        ///  - Kirilabilir tur risk-getiri degisimi yaratir
        /// </summary>
        private struct ParcelTypeSpec
        {
            public string FileName;
            public string DisplayName;
            public float MinWeight;
            public float MaxWeight;
            public float MinVolume;
            public float MaxVolume;
            public float PaymentPerKg;
            public bool Fragile;
            public float BaseSuccessRate;
            public bool IsRare;
        }

        // baseSuccessRate: zamaninda teslimde basari sansi. Kirilabilir
        // turler daha dusuk tutulur (delme, cevirme, yagmurda islanma),
        // belge neredeyse her zaman gider. isRare: ozel mudahale isteyen
        // kargo; kucuk bir basari bonusu ve daha yuksek odeme getirir.
        private static readonly ParcelTypeSpec[] Specs =
        {
            new ParcelTypeSpec { FileName = "ParcelType_Belge",    DisplayName = "Belge",
                MinWeight = 0.4f, MaxWeight = 1.2f, MinVolume = 1f,   MaxVolume = 3f,
                PaymentPerKg = 35f, Fragile = false, BaseSuccessRate = 0.99f, IsRare = false },

            new ParcelTypeSpec { FileName = "ParcelType_Kirtasiye", DisplayName = "Kirtasiye",
                MinWeight = 1.0f, MaxWeight = 3.0f, MinVolume = 4f,   MaxVolume = 12f,
                PaymentPerKg = 30f, Fragile = false, BaseSuccessRate = 0.97f, IsRare = false },

            new ParcelTypeSpec { FileName = "ParcelType_Esya",     DisplayName = "Esya",
                MinWeight = 3.0f, MaxWeight = 8.0f, MinVolume = 15f,  MaxVolume = 45f,
                PaymentPerKg = 42f, Fragile = false, BaseSuccessRate = 0.95f, IsRare = false },

            new ParcelTypeSpec { FileName = "ParcelType_Gida",     DisplayName = "Gida",
                MinWeight = 2.0f, MaxWeight = 6.0f, MinVolume = 10f,  MaxVolume = 30f,
                PaymentPerKg = 38f, Fragile = true, BaseSuccessRate = 0.92f, IsRare = false },

            new ParcelTypeSpec { FileName = "ParcelType_Elektronik", DisplayName = "Elektronik",
                MinWeight = 1.5f, MaxWeight = 5.0f, MinVolume = 8f,   MaxVolume = 25f,
                PaymentPerKg = 85f, Fragile = true, BaseSuccessRate = 0.88f, IsRare = true },

            new ParcelTypeSpec { FileName = "ParcelType_Ilac",     DisplayName = "Ilac",
                MinWeight = 0.5f, MaxWeight = 2.0f, MinVolume = 2f,   MaxVolume = 8f,
                PaymentPerKg = 65f, Fragile = true, BaseSuccessRate = 0.90f, IsRare = true },
        };

        [MenuItem("Kargo/3. Icerik Assetlerini Kur", priority = 2)]
        public static void BuildContent()
        {
            if (!AssetDatabase.IsValidFolder(ParcelFolder))
            {
                string[] parts = ParcelFolder.Split('/');
                string current = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                        AssetDatabase.CreateFolder(current, parts[i]);
                    current = next;
                }
            }

            var created = new ParcelType[Specs.Length];
            for (int i = 0; i < Specs.Length; i++)
                created[i] = CreateOrUpdateParcelType(Specs[i]);

            var vehicleDef = AssetDatabase.LoadAssetAtPath<VehicleDefinition>(VehicleDefPath);
            if (vehicleDef == null)
            {
                // Arac tanimi yoksa kapasite aktarilamaz ve hicbir kargo
                // araca sigmaz. Oyun yine de acilir (kargo uretilir, sadece
                // yuklenemez) - bu yuzden sadece uyari.
                Debug.LogWarning("[Kargo] Arac tanimi bulunamadi, kapasite aktarilmadi: " +
                                 VehicleDefPath + ". Once Kargo/Vehicle/Arac Prefabini Kur calistirin.");
            }

            WireToCompositionRoot(created, vehicleDef);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Verify(created);
            Debug.Log("[Kargo] Icerik kuruldu: " + created.Length + " kargo turu.");
        }

        private static ParcelType CreateOrUpdateParcelType(ParcelTypeSpec spec)
        {
            string path = ParcelFolder + "/" + spec.FileName + ".asset";
            var type = AssetDatabase.LoadAssetAtPath<ParcelType>(path);

            if (type == null)
            {
                type = ScriptableObject.CreateInstance<ParcelType>();
                AssetDatabase.CreateAsset(type, path);
            }

            type.displayName = spec.DisplayName;
            type.minWeightKg = spec.MinWeight;
            type.maxWeightKg = spec.MaxWeight;
            type.minVolumeLiters = spec.MinVolume;
            type.maxVolumeLiters = spec.MaxVolume;
            type.paymentPerKg = spec.PaymentPerKg;
            type.fragile = spec.Fragile;
            type.baseSuccessRate = spec.BaseSuccessRate;
            type.isRare = spec.IsRare;
            type.Sanitize();

            EditorUtility.SetDirty(type);
            return type;
        }

        /// <summary>
        /// Sahnedeki Composition Root'a kargo turlarini ve araci baglar.
        /// Batchmode'da calisirken sahne acik olmayabilir; bu yuzden
        /// sahneyi kendisi acar ve kaydeder.
        /// </summary>
        private static void WireToCompositionRoot(ParcelType[] types, VehicleDefinition vehicle)
        {
            const string scenePath = "Assets/Scenes/Game.unity";

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            var root = UnityEngine.Object.FindFirstObjectByType<Core.GameCompositionRoot>();
            if (root == null)
            {
                Debug.LogError("[Kargo] Sahnede GameCompositionRoot bulunamadi. " +
                               "Once sahneyi kurun: Kargo/1. Tum Kurulumu Calistir");
                return;
            }

            root.SetContentForSetup(types, vehicle);
            EditorUtility.SetDirty(root);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[Kargo] Composition Root baglandi: " + types.Length + " kargo turu.");
        }

        private static void Verify(ParcelType[] created)
        {
            var problems = new System.Text.StringBuilder();

            foreach (var type in created)
            {
                if (type == null) { problems.Append(" NULL kargo turu."); continue; }

                if (type.maxWeightKg < type.minWeightKg)
                    problems.Append(" " + type.name + ": agirlik araligi gecersiz.");
                if (type.maxVolumeLiters < type.minVolumeLiters)
                    problems.Append(" " + type.name + ": hacim araligi gecersiz.");
                if (type.paymentPerKg <= 0f)
                    problems.Append(" " + type.name + ": odeme sifir.");
            }

            if (problems.Length == 0)
                Debug.Log("[Kargo] Dogrulama OK: tum kargo turleri gecerli.");
            else
                Debug.LogError("[Kargo] Dogrulama BASARISIZ:" + problems);
        }
    }
}
