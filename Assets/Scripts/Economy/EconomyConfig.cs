using UnityEngine;

namespace KargoSim.Economy
{
    /// <summary>
    /// Para ve XP ayarlari. Sayilar burada toplanir; hicbir sistemde
    /// gomulu sabit bulunmaz. Inspector'dan anlik degistirilebilir.
    /// </summary>
    [CreateAssetMenu(
        fileName = "EconomyConfig",
        menuName = "KargoSim/Economy Config",
        order = 4)]
    public sealed class EconomyConfig : ScriptableObject
    {
        [Header("Baslangic")]
        [Tooltip("Oyunun basindaki bakiye (TL).")]
        [Min(0)] public int startingMoney = 250;

        // ------------------------------------------------------------------
        // Para
        // ------------------------------------------------------------------

        [Header("Odeme - Zamanli Teslim")]
        [Tooltip("Sure dolmadan teslimde, kargonun degeri kadar EK pay. " +
                 "Kalan sure oranina gore carpan olur, yani hizli teslim " +
                 "daha cok kazandirir. 0.25 = en fazla %25 bahsis.")]
        [Range(0f, 2f)] public float earlyBonusRatio = 0.25f;

        [Tooltip("Bu orandan az kalan sure kaldiginda bahsis verilmez.")]
        [Range(0f, 1f)] public float earlyBonusThreshold = 0.2f;

        [Header("Odeme - Basarisizlik")]
        [Tooltip("Basarisiz teslim icin para CEZASI. 0 onerilir: oyuncu " +
                 "zaten zaman kaybediyor, uzerine para kesmek sinir bozucu " +
                 "olur ve oyuncuyu kargoyu hic yuklememeye iter.")]
        [Range(0, 500)] public int failedDeliveryPenalty = 0;

        // ------------------------------------------------------------------
        // XP
        // ------------------------------------------------------------------

        [Header("XP - Kazanim")]
        [Tooltip("Teslimat basina taban XP.")]
        [Min(0)] public int xpPerDelivery = 12;

        [Tooltip("Kilogram basina ek XP. Agir kargo tasimak daha degerli.")]
        [Min(0f)] public float xpPerKg = 1.5f;

        // ------------------------------------------------------------------
        // Seviye
        // ------------------------------------------------------------------

        [Header("Seviye")]
        [Tooltip("2. seviyeye cikmak icin gereken XP.")]
        [Min(1)] public int levelUpBaseCost = 100;

        [Tooltip("Her seviyede maliyet kac kat artar? 1.3 = %30 artis. " +
                 "Cok yuksekse oyuncu takilir, cok dusukse anlamini yitirir.")]
        [Range(1.05f, 2f)] public float levelUpCostGrowth = 1.35f;

        [Tooltip("Azami seviye. Sinir koymazsak XP egrisi sonsuza gider.")]
        [Range(1, 60)] public int maxLevel = 20;

        /// <summary>
        /// <paramref name="level"/> seviyesine ulasmak icin gereken TOPLAM XP.
        ///
        /// Her seviye ATLAMININ maliyeti ayri hesaplanir ve artar:
        ///   1 -> 2 : taban            = 100
        ///   2 -> 3 : taban * buyume   = 135
        ///   3 -> 4 : taban * buyume^2 = 182
        /// ve kümülatif toplam: 2 = 100, 3 = 235, 4 = 417
        ///
        /// DİKKAT: Eksponansi "toplam"a degil "atlama maliyetine" uygulanir.
        /// Toplama uygulanirsa ilk atlama (100) ikincisinden (35) pahali
        /// cikar ve oyuncu ilk seviyede takilip sonra hizla yukselir -
        /// seviye sistemi anlamini yitirir. Test bu hatayi yakaladi.
        /// </summary>
        public int XpCostForLevel(int level)
        {
            if (level <= 1) return 0;

            int target = Mathf.Clamp(level, 1, maxLevel);

            float total = 0f;
            for (int current = 1; current < target; current++)
                total += levelUpBaseCost * Mathf.Pow(levelUpCostGrowth, current - 1);

            return Mathf.RoundToInt(total);
        }

        private void OnValidate()
        {
            if (startingMoney < 0) startingMoney = 0;
            if (xpPerDelivery < 0) xpPerDelivery = 0;
            if (xpPerKg < 0f) xpPerKg = 0f;
            if (levelUpBaseCost < 1) levelUpBaseCost = 1;
            if (levelUpCostGrowth < 1.05f) levelUpCostGrowth = 1.05f;
            if (maxLevel < 1) maxLevel = 1;
        }
    }
}
