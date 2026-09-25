namespace KargoSim.Delivery
{
    /// <summary>
    /// Bir kargonun teslim sonucu.
    ///
    /// Para ve XP Aşama 5'te hesaplanir; burada sadece SONUC vardir.
    /// Iki katman ayni isi yapmasin diye ayridir.
    /// </summary>
    public enum DeliveryOutcome
    {
        /// <summary>Suresi dolmadan teslim edildi. Tam odeme + XP.</summary>
        Success = 0,

        /// <summary>
        /// Suresi gectikten sonra teslim edildi. Müşteri kargoyu alir ama
        /// tam ödemeyi yapmaz; XP kazanci azalir.
        /// </summary>
        Late = 1,

        /// <summary>
        /// Gun sonunda hala teslim edilmemis. Müşteri kargoyu almaz,
        /// oyuncu para ve itibar kaybeder.
        /// </summary>
        Failed = 2,
    }

    public static class DeliveryOutcomeExtensions
    {
        public static string DisplayName(this DeliveryOutcome outcome)
        {
            switch (outcome)
            {
                case DeliveryOutcome.Late: return "GEC KALDI";
                case DeliveryOutcome.Failed: return "BASARISIZ";
                default: return "TESLIM EDILDI";
            }
        }

        /// <summary>XP kazanimi carpani (Aşama 5 ekonomi bunu kullanir).</summary>
        public static float RewardFactor(this DeliveryOutcome outcome)
        {
            switch (outcome)
            {
                case DeliveryOutcome.Late: return 0.4f;
                case DeliveryOutcome.Failed: return 0f;
                default: return 1f;
            }
        }
    }
}
