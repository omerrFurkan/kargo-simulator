namespace KargoSim.Cargo
{
    /// <summary>
    /// Kargonun aciliyeti. Odeme ve sure bu enum ile birlikte belirlenir.
    ///
    /// Bu bir veri modelidir: hangi degerin ne anlama geldigini tek yerde
    /// topluyoruz, boylece oyunun geri kalani sadece buraya bakar.
    /// </summary>
    public enum ParcelPriority
    {
        /// <summary>Standart teslimat. En yuksek kazanc / en uzun sure.</summary>
        Normal = 0,

        /// <summary>Hizli teslimat. Daha cok kazanc, daha az sure.</summary>
        Express = 1,

        /// <summary>Acil. En cok kazanc, en az sure. Risk en yuksek.</summary>
        Urgent = 2,
    }

    /// <summary>
    /// ParcelPriority icin davranislar. Enum'a ozellik eklenemedigi icin
    /// (C# enumlar metot tasimaz) uzantilar ile sunulur.
    /// </summary>
    public static class ParcelPriorityExtensions
    {
        /// <summary>Odeme carpani. Acil kargo daha cok kazandirir.</summary>
        public static float PaymentMultiplier(this ParcelPriority priority)
        {
            switch (priority)
            {
                case ParcelPriority.Express: return 1.5f;
                case ParcelPriority.Urgent: return 2.2f;
                default: return 1f;
            }
        }

        /// <summary>
        /// Sure carpani. Acil kargonun suresi daha kisadir.
        /// Kargo ne kadar acilse, o kadar kisa surede teslim edilmesi gerekir.
        /// </summary>
        public static float TimeMultiplier(this ParcelPriority priority)
        {
            switch (priority)
            {
                case ParcelPriority.Express: return 0.7f;
                case ParcelPriority.Urgent: return 0.5f;
                default: return 1f;
            }
        }

        /// <summary>Normal teslim edilebilir oran (0..1). Acil kargo daha zor.</summary>
        public static float BaseSuccessRate(this ParcelPriority priority)
        {
            switch (priority)
            {
                case ParcelPriority.Express: return 0.85f;
                case ParcelPriority.Urgent: return 0.7f;
                default: return 0.95f;
            }
        }

        public static string DisplayName(this ParcelPriority priority)
        {
            switch (priority)
            {
                case ParcelPriority.Express: return "Hizli";
                case ParcelPriority.Urgent: return "Acil";
                default: return "Normal";
            }
        }
    }
}
