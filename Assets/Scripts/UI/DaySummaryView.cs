using KargoSim.Player;
using UnityEngine;
using UnityEngine.UI;

namespace KargoSim.UI
{
    /// <summary>
    /// Gun sonu ozeti. Gun bittiginde acilir, oyuncuya donumu anlatir.
    /// "Oyun ilerliyor" duygusunun en ucuz yolu: her gun somut bir
    /// sayi gostermek.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DaySummaryView : MonoBehaviour
    {
        [Header("Parcalar")]
        [SerializeField] private GameObject panel;
        [SerializeField] private Text titleText;
        [SerializeField] private Text bodyText;
        [SerializeField] private Text hintText;

        private bool _warnedMissingPanel;

        public bool IsOpen => panel != null && panel.activeSelf;

        public void Show(DaySummary summary)
        {
            if (!EnsurePanel()) return;

            panel.SetActive(true);

            if (titleText != null)
                titleText.text = summary.IsEmpty
                    ? "GUN " + summary.Day + " - TESLIMAT YOK"
                    : "GUN " + summary.Day + " TAMAMLANDI";

            if (bodyText != null)
            {
                bodyText.text = string.Format(
                    "Teslimat:     {0}   (zamaninda {1} / gec {2} / basarisiz {3})\n" +
                    "Kazanc:     {4} TL\n" +
                    "XP:            {5}\n" +
                    "Seviye:      {6}{7}",
                    summary.Delivered, summary.Successful, summary.Late, summary.Failed,
                    summary.MoneyEarned, summary.XpEarned, summary.Level,
                    summary.LevelUpsThisDay > 0 ? "  (+" + summary.LevelUpsThisDay + " seviye!)" : "");
            }

            if (hintText != null)
                hintText.text = "E: yeni gune basla";
        }

        public void Hide()
        {
            if (panel != null) panel.SetActive(false);
        }

        /// <summary>
        /// Panel referansini garanti eder; yoksa kurulum adiyla ("Panel"
        /// cocugu) bulunur. Diğer view'lar ile ayni neden.
        /// </summary>
        private bool EnsurePanel()
        {
            if (panel != null) return true;

            Transform child = transform.Find("Panel");
            if (child == null)
            {
                if (!_warnedMissingPanel)
                {
                    _warnedMissingPanel = true;
                    Debug.LogError(
                        "[DaySummaryView] Panel bulunamadi. Menu: Kargo/6. Arayuzu Kur", this);
                }
                return false;
            }

            panel = child.gameObject;
            Debug.LogWarning(
                "[DaySummaryView] panel referansi BOS oldu; 'Panel' cocuguyla duzeltildi. " +
                "Kalici cozum: Kargo/6. Arayuzu Kur", this);
            return true;
        }
    }
}
