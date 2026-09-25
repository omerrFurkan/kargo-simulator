using UnityEngine;

namespace KargoSim.Cargo
{
    /// <summary>
    /// Bir kargo turunun tanimi. ScriptableObject cunku icerik (cesit, agirligi,
    /// degeri) kod degil VERI'dir: yeni bir kargo turu eklemek icin script
    /// degil sadece yeni bir asset yeterlidir.
    ///
    /// Sayilar araliktir: ParcelGenerator her kargo icin bu araliktan rastgele
    /// bir deger secer, boylece "Kagit kargo" her seferinde ayni olmaz.
    /// </summary>
    [CreateAssetMenu(
        fileName = "ParcelType",
        menuName = "KargoSim/Parcel Type",
        order = 2)]
    public sealed class ParcelType : ScriptableObject
    {
        [Header("Gosterim")]
        [Tooltip("Depo listesinde gorunecek ad.")]
        public string displayName = "Kargo";

        [Header("Agirlik")]
        [Tooltip("Bu turden bir kargonun agirligi araligi (kg). " +
                 "Alt ve ust sinir arasinda deger uretilir.")]
        [Min(0.1f)] public float minWeightKg = 1f;
        public float maxWeightKg = 3f;

        [Header("Hacim")]
        [Tooltip("Bu turden bir kargonun hacmi araligi (litre). " +
                 "Agirlik ve hacim AYRI sinirlardir: oyuncu ikisini de " +
                 "doldurmak zorundadir, yoksa kocaman ama bos arac " +
                 "avantajli olur.")]
        [Min(0.1f)] public float minVolumeLiters = 2f;
        public float maxVolumeLiters = 8f;

        [Header("Deger")]
        [Tooltip("Kilogram basina taban ucret. Oncelik carpani bunu " +
                 "(acil kargo daha cok kazandirir) sonra uygulanir.")]
        [Min(0f)] public float paymentPerKg = 45f;

        [Header("Ozellik")]
        [Tooltip("Kirilabilir kargo. Hasar gorurse deger kaybi olur " +
                 "(Aşama 4'te teslimat oncesi hasar kontrolu).")]
        public bool fragile;

        [Tooltip("Normal teslim edilebilirlik orani (0..1). Oncelik bunu " +
                 "dusurur. Vanilmayan bir deger varsayilan kullanilir.")]
        [Range(0f, 1f)] public float baseSuccessRate = 0.95f;

        /// <summary>Depo listesinde renk olarak gosterilebilir (0 = normal).</summary>
        public bool isRare;

        private void OnValidate()
        {
            if (minWeightKg <= 0f) minWeightKg = 0.1f;
            if (maxWeightKg < minWeightKg) maxWeightKg = minWeightKg;

            if (minVolumeLiters <= 0f) minVolumeLiters = 0.1f;
            if (maxVolumeLiters < minVolumeLiters) maxVolumeLiters = minVolumeLiters;

            if (paymentPerKg < 0f) paymentPerKg = 0f;

            // OnValidate yalnizca Inspector'da calisir; AssetDatabase uzerinden
            // yeniden yuklenen bir asset'te de calissin diye burada da bir
            // guvenlik agi var (clamp).
            if (baseSuccessRate < 0f) baseSuccessRate = 0f;
            if (baseSuccessRate > 1f) baseSuccessRate = 1f;
        }

        /// <summary>
        /// Asset'i guvenli araliga getirir. Unity asset'i diskte duzenlerse
        /// degerler bozulmus olabilir; uretim sirasinda cagrilir.
        /// </summary>
        public void Sanitize()
        {
            if (minWeightKg <= 0f) minWeightKg = 0.1f;
            if (maxWeightKg < minWeightKg) maxWeightKg = minWeightKg;
            if (minVolumeLiters <= 0f) minVolumeLiters = 0.1f;
            if (maxVolumeLiters < minVolumeLiters) maxVolumeLiters = minVolumeLiters;
            if (paymentPerKg < 0f) paymentPerKg = 0f;
            if (baseSuccessRate < 0f) baseSuccessRate = 0f;
            if (baseSuccessRate > 1f) baseSuccessRate = 1f;
        }
    }
}
