using KargoSim.Cargo;
using UnityEngine;
using UnityEngine.UI;

namespace KargoSim.UI
{
    /// <summary>
    /// ESYA OZELLIK PENCERESI (tooltip / inspector).
    ///
    /// ASAMA 31 - Oyuncu istedi:
    ///   "kullanicinin esyalarin ozelliklerinde uzerlerine tikladiginda
    ///    ya da mouse ile beklerse kucuk bir pencerede de yazsin"
    ///
    /// Fare bir esya (kutu, musteri, arac) UZERINDE bekince ya da
    /// tiklandiginda o nesnenin ayrintisi imlecin yaninda belirir.
    ///
    /// ONEMLI: Metin URETIMI GORUNEN KISIMDAN AYRILDI. `Describe`
    /// saf bir fonksiyondur ve test edilebilir; pencere sadece onu
    /// gosterir. Boylece "dogru yazi cikiyor mu" sorusu ekran
    /// goruntulenmeden cevaplanabilir.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ParcelTooltipView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform panel;
        [SerializeField] private Text titleText;
        [SerializeField] private Text bodyText;

        [Tooltip("Imlecten kac piksel uzakta gosterilir.")]
        [SerializeField] private Vector2 cursorOffset = new Vector2(18f, -14f);

        [SerializeField] private float maxWidth = 320f;

        private bool _visible;

        public bool IsVisible => _visible;
        public string BodyText => bodyText != null ? bodyText.text : "";

        /// <summary>
        /// Kutunun ozellik metnini uretir.
        ///
        /// Oyuncu "gittiğimiz yere göre kargolarımızı görebilelim"
        /// dedi; kime gideceği ilk satirda olsun ki oyuncu kutuyu
        /// hedefe göre secebilsin.
        /// </summary>
        public static string Describe(Parcel parcel)
        {
            if (parcel == null) return "";

            string tip = parcel.Type != null ? parcel.Type.displayName : "Kutu";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("#" + parcel.Id + "  " + tip);
            sb.AppendLine("Alıcı: " + parcel.RecipientName + "  (adres " + parcel.DestinationId + ")");
            sb.AppendLine("Ağırlık: " + parcel.WeightKg.ToString("0.0") + " kg"
                        + "    Hacim: " + parcel.VolumeLiters.ToString("0.0") + " L");

            if (parcel.IsPackaged)
            {
                sb.AppendLine("Durum: paketli (araça konulabilir)");
            }
            else
            {
                sb.AppendLine("Durum: PAKETLENMEDİ  (%"
                            + (parcel.PackagingProgress01 * 100f).ToString("0") + ")");
            }

            if (parcel.IsLoaded)
            {
                sb.AppendLine("Teslim kodu: " + parcel.DeliveryCodeText);
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>Pencereyi kutu icin acar (imlecin yaninda).</summary>
        public void Show(Parcel parcel)
        {
            if (parcel == null) { Hide(); return; }

            string body = Describe(parcel);
            if (string.IsNullOrEmpty(body)) { Hide(); return; }

            if (titleText != null)
                titleText.text = parcel.RecipientName;

            if (bodyText != null) bodyText.text = body;

            if (group != null) group.alpha = 1f;
            if (panel != null) panel.sizeDelta = new Vector2(maxWidth, 0f);
            gameObject.SetActive(true);

            // IKI CIZIMI ARASI GEC: LayoutGroup bir sonraki karede
            // olcer; iki kare beklenmezse yazi eski yerde kalir.
            MoveNextFrame();

            _visible = true;
        }

        public void Hide()
        {
            _visible = false;
            if (group != null) group.alpha = 0f;
        }

        private void MoveNextFrame()
        {
            Vector3 mouse = Input.mousePosition;
            if (panel != null && panel.parent != null)
            {
                var canvasRect = panel.parent as RectTransform;
                if (canvasRect != null)
                {
                    Vector2 local;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        canvasRect, mouse, null, out local);
                    panel.anchoredPosition = local + cursorOffset;
                }
            }
        }

        private void Update()
        {
            if (!_visible) return;

            // Fare basili degilse ve hedef degistiyse kapatmayi
            // ustteki yoneticinin (UIManager) sorumlulugu; burada sadece
            // konum tazelenir.
            MoveNextFrame();
        }
    }
}
