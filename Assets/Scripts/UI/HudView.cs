using KargoSim.Cargo;
using KargoSim.Core;
using KargoSim.Delivery;
using KargoSim.Player;
using UnityEngine;
using UnityEngine.UI;

namespace KargoSim.UI
{
    /// <summary>
    /// Sol ustteki sabit bilgi: para, seviye/XP, saat, kapasite, sonraki durak.
    ///
    /// PERFORMANS: Metinler her cagrida degil, deger degistiginde yazilir.
    /// <see cref="Refresh"/> saniyede 60 kez cagrilir ama string tahsisi
    /// yalnizca gercekten bir sey degistiginde olur.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HudView : MonoBehaviour
    {
        [Header("Metinler")]
        [SerializeField] private Text moneyText;
        [SerializeField] private Text levelText;
        [SerializeField] private Text timeText;
        [SerializeField] private Text capacityText;
        [SerializeField] private Text nextStopText;
        [SerializeField] private Text hintText;

        [Header("Cubuklar")]
        [SerializeField] private Image xpBarFill;
        [SerializeField] private Image capacityBarFill;

        [Header("Geri Bildirim")]
        [SerializeField] private Text payoutText;

        // --- Onbellek: ne yaziliydi ---
        private string _moneyCache = "";
        private string _levelCache = "";
        private string _timeCache = "";
        private string _capacityCache = "";
        private string _nextStopCache = "";
        private string _hintCache = "";
        private float _payoutHideAt = -1f;

        /// <summary>Son guncellenen icerik (test edilebilirlik icin).</summary>
        public string MoneyText => _moneyCache;
        public string LevelText => _levelCache;
        public string TimeText => _timeCache;
        public string CapacityText => _capacityCache;
        public string NextStopText => _nextStopCache;
        public string HintText => _hintCache;

        /// <summary>
        /// Girdi konumuna gore ipucu. Oyuncu ne yapabilecegini her an
        /// bilmeli; hud bunu taşımakla sorumludur.
        /// </summary>
        public void Refresh(PlayerProgress progress, GameClock clock,
                            CargoSystem cargo, DeliverySystem delivery)
        {
            if (progress == null || clock == null) return;

            // --- Para ---
            SetIfChanged(ref _moneyCache, progress.Money + " TL", moneyText);

            // --- Seviye + XP ---
            string level = progress.IsMaxLevel
                ? "Seviye " + progress.Level + " (MAX)"
                : "Seviye " + progress.Level;
            SetIfChanged(ref _levelCache, level, levelText);

            if (xpBarFill != null)
            {
                float target = progress.LevelProgress;
                if (!Mathf.Approximately(xpBarFill.fillAmount, target))
                    xpBarFill.fillAmount = target;
            }

            // --- Saat ---
            SetIfChanged(ref _timeCache, clock.FormatTime() + "  |  Gun " + clock.Day, timeText);

            // --- Kapasite (agirlik ve hacim ayri) ---
            if (cargo != null)
            {
                var m = cargo.Manifest;
                SetIfChanged(ref _capacityCache,
                    "Yuk: " + m.LoadedCount + " kargo\n" +
                    m.LoadedWeightKg.ToString("0") + " / " + m.Capacity.MaxWeightKg.ToString("0") + " kg\n" +
                    m.LoadedVolumeLiters.ToString("0") + " / " + m.Capacity.MaxVolumeLiters.ToString("0") + " L",
                    capacityText);

                if (capacityBarFill != null)
                {
                    float target = m.LoadRatio;
                    if (!Mathf.Approximately(capacityBarFill.fillAmount, target))
                        capacityBarFill.fillAmount = target;
                }
            }

            // --- Sonraki durak ---
            if (delivery != null)
            {
                string next;
                if (delivery.CurrentStop != null)
                    next = "Teslimat: " + delivery.CurrentStop.DisplayName +
                           "  (" + delivery.CurrentStop.PendingCount + " kargo)";
                else if (delivery.Route.Count > 0)
                    next = "Sonraki: " + delivery.Route[0].DisplayName +
                           "  -  " + delivery.RemainingDistance.ToString("0") + " m";
                else
                    next = cargo != null && cargo.Manifest.LoadedCount > 0
                        ? "Rota yok - kargo yuklemelisin"
                        : "Depoya git: " + cargo.Manifest.AvailableCount + " kargo bekliyor";

                SetIfChanged(ref _nextStopCache, next, nextStopText);
            }
        }

        /// <summary>Oyuncuya ne yapabilecegini söyler (baglama bagli).</summary>
        public void SetHint(string hint) => SetIfChanged(ref _hintCache, hint, hintText);

        /// <summary>Odeme bildirimi: ekranda kisa sure gosterir.</summary>
        public void FlashPayout(Economy.Payout payout, DeliveryOutcome outcome)
        {
            if (payoutText == null) return;

            string sign = payout.TotalMoney >= 0 ? "+" : "";
            payoutText.text = sign + payout.TotalMoney + " TL   +" + payout.Xp + " XP";

            Color color = outcome == DeliveryOutcome.Success ? UITheme.Success
                        : outcome == DeliveryOutcome.Late ? UITheme.Warning
                        : UITheme.Danger;
            payoutText.color = color;
            payoutText.gameObject.SetActive(true);

            _payoutHideAt = Time.unscaledTime + 2.5f;
        }

        /// <summary>Urun bildiriminin suresi dolduysa gizler.</summary>
        public void TickFeedback()
        {
            if (_payoutHideAt < 0f || payoutText == null) return;
            if (Time.unscaledTime < _payoutHideAt) return;

            payoutText.gameObject.SetActive(false);
            _payoutHideAt = -1f;
        }

        /// <summary>Seviye atlama bildirimi.</summary>
        public void ShowLevelUp(int level)
        {
            if (payoutText == null) return;

            payoutText.text = "SEVIYE " + level + " ATLADIN!";
            payoutText.color = UITheme.Experience;
            payoutText.gameObject.SetActive(true);
            _payoutHideAt = Time.unscaledTime + 3f;
        }

        /// <summary>
        /// Yalnizca deger degistiginde metni yazar. UIManager.Refresh her
        /// karede cagrildigi icin bu kontrol olmazsa saniyede 60 kez
        /// string ayristirma olur.
        /// </summary>
        private static void SetIfChanged(ref string cache, string value, Text target)
        {
            if (cache == value) return;

            cache = value;
            if (target != null) target.text = value;
        }
    }
}
