using KargoSim.Economy;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KargoSim.EditorTools
{
    /// <summary>
    /// EconomyConfig asset'ini uretir ve Composition Root'a baglar.
    /// Ayarlar burada TEK yerde durur; Inspector'dan elle degistirilebilir.
    /// </summary>
    public static class ProgressionSetupTool
    {
        private const string ConfigPath = "Assets/ScriptableObjects/Database/EconomyConfig.asset";
        private const string ScenePath = "Assets/Scenes/Game.unity";

        [MenuItem("Kargo/5. Ilerleme Ayarlarini Kur", priority = 4)]
        public static void BuildEconomyConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<EconomyConfig>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<EconomyConfig>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }

            // Varsayilanlar: oyuncunun gun 1'de bir kac teslimat yapip
            // SEVIYE ATLAMASI icin ayarlandi. XP taban 12, agir kargo
            // kg basina 1.5; 2. seviye 100 XP.
            config.startingMoney = 250;
            config.earlyBonusRatio = 0.25f;
            config.earlyBonusThreshold = 0.2f;
            config.failedDeliveryPenalty = 0;   // ceza yok: zaman kaybi yeterli
            config.xpPerDelivery = 12;
            config.xpPerKg = 1.5f;
            config.levelUpBaseCost = 100;
            config.levelUpCostGrowth = 1.35f;
            config.maxLevel = 20;

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();

            WireToCompositionRoot(config);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Verify(config);
            Debug.Log("[Kargo] Ilerleme ayarlari kuruldu: " + ConfigPath);
        }

        private static void WireToCompositionRoot(EconomyConfig config)
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var root = Object.FindFirstObjectByType<Core.GameCompositionRoot>();

            if (root == null)
            {
                Debug.LogError("[Kargo] Sahnede GameCompositionRoot bulunamadi. " +
                               "Once sahneyi kurun: Kargo/1. Tum Kurulumu Calistir");
                return;
            }

            root.SetEconomyForSetup(config);
            EditorUtility.SetDirty(root);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[Kargo] Composition Root'a EconomyConfig baglandi.");
        }

        private static void Verify(EconomyConfig config)
        {
            var problems = new System.Text.StringBuilder();

            if (config.xpPerDelivery <= 0 && config.xpPerKg <= 0f)
                problems.Append(" XP kazanci sifir: oyuncu hic seviye atlayamaz.");

            if (config.levelUpBaseCost <= 0)
                problems.Append(" Seviye maliyeti gecersiz.");

            // XP kazanci, 2. seviye icin gerekenin cok uzerindeyse
            // seviye sistemi anlamini yitirir; cok dusukse oyuncu takilir.
            int firstDayXp = config.xpPerDelivery * 12;   // 12 teslim/gun
            int levelTwo = config.XpCostForLevel(2);
            if (firstDayXp > levelTwo * 3)
            {
                problems.Append(" Bir gunde " + firstDayXp + " XP kazanilabiliyor ama " +
                                "2. seviye " + levelTwo + " XP: seviye cok hizli gelir.");
            }

            // En pahali kargo ile 1 teslimat en az 1 seviye ediyor mu?
            if (config.xpPerDelivery >= levelTwo)
            {
                problems.Append(" Tek teslimat (" + config.xpPerDelivery + " XP) bir seviye ediyor (" +
                                levelTwo + " XP): seviye anlamsiz.");
            }

            if (problems.Length == 0)
                Debug.Log("[Kargo] Dogrulama OK: XP kazanci ile seviye maliyeti dengeli.");
            else
                Debug.LogError("[Kargo] Dogrulama BASARISIZ:" + problems);
        }
    }
}
