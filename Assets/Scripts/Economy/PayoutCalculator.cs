using System;
using KargoSim.Cargo;
using KargoSim.Delivery;

namespace KargoSim.Economy
{
    /// <summary>
    /// Odeme ve XP formulu. STATIK ve SAF: sahne, motor, zaman veya
    /// rastgelelik kullanmaz. Ayni girdi -> ayni cikti.
    ///
    /// Boylece ekonomi "dogru mu?" sorusu saniyeler icinde yuzlerce
    /// senaryo ile dogrulanabilir.
    /// </summary>
    public static class PayoutCalculator
    {
        /// <summary>
        /// Bir teslimin odemesini hesaplar.
        /// </summary>
        /// <param name="baseValue">Kargonun degeri (TL), Parcel.BaseValue.</param>
        /// <param name="weightKg">Kargonun agirligi (XP agir kargoyu degerlendirir).</param>
        /// <param name="outcome">Teslim sonucu (odeme ve XP carpanini belirler).</param>
        /// <param name="remainingRatio">
        /// Kalan surenin baslangic suresine orani (0..1). Bahsis bununla
        /// olceklenir.
        /// </param>
        /// <param name="config">Ayarlar.</param>
        public static Payout Calculate(int baseValue, float weightKg,
                                       DeliveryOutcome outcome, float remainingRatio,
                                       EconomyConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            if (baseValue < 0) baseValue = 0;
            if (weightKg < 0f) weightKg = 0f;

            float ratio = remainingRatio < 0f ? 0f : (remainingRatio > 1f ? 1f : remainingRatio);
            float factor = outcome.RewardFactor();

            // --- Para ---
            int payment = Round(baseValue * factor);

            // Bahsis SADECE zamaninda tam teslimde verilir ve kalan sure
            // oranina olceklenir: %100 sure = tam bahsis, %0 = bonus yok.
            int bonus = 0;
            if (outcome == DeliveryOutcome.Success && ratio >= config.earlyBonusThreshold)
                bonus = Round(baseValue * config.earlyBonusRatio * ratio);

            int penalty = 0;
            if (outcome == DeliveryOutcome.Failed)
                penalty = config.failedDeliveryPenalty;

            // --- XP ---
            // Taban + agirliga gore ek; sonuc carpani ikisine de uygulanir.
            float rawXp = (config.xpPerDelivery + config.xpPerKg * weightKg) * factor;
            int xp = Round(rawXp);

            return new Payout(payment, bonus, penalty, xp);
        }

        /// <summary>
        /// Kargonun kalan sure oranini dondurur (0..1). Daha once yuklenmediyse
        /// 1 doner (henuz baslamadi, tam sure mevcut).
        /// </summary>
        public static float RemainingRatio(Parcel parcel, float elapsed)
        {
            if (parcel == null) return 0f;
            if (parcel.DeadlineSeconds <= 0f) return 0f;
            if (!parcel.IsLoaded) return 1f;

            return parcel.RemainingSeconds(elapsed) / parcel.DeadlineSeconds;
        }

        /// <summary>
        /// Yuvarlama. Para ve XP icin tek kural olmali, yoksa "1.5 -> 1 mi
        /// 2 mi?" belirsizligi testlerde tutarsizlik yaratir.
        /// Bankaci yuvarlamasi (.5 yukarilir), tutarli.
        /// </summary>
        private static int Round(float value) =>
            (int)Math.Round(value, MidpointRounding.AwayFromZero);
    }
}
