using System.Collections.Generic;
using KargoSim.Cargo;
using KargoSim.Delivery;
using UnityEngine;
using UnityEngine.UI;

namespace KargoSim.UI
{
    /// <summary>
    /// Adrese varildiginda acilan teslimat paneli.
    ///
    /// Oyuncuya "buradasin, su kargolari teslim edeceksin" der ve teslimi
    /// yapar. Panel otomatik acilir, teslim bitince kendini kapatir.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StopView : MonoBehaviour
    {
        [Header("Parcalar")]
        [SerializeField] private GameObject panel;
        [SerializeField] private Text headerText;
        [SerializeField] private Text listText;
        [SerializeField] private Text hintText;

        private readonly List<Parcel> _buffer = new List<Parcel>(8);
        private DeliveryStop _stop;
        private bool _warnedMissingPanel;

        public bool IsOpen => panel != null && panel.activeSelf;

        /// <summary>Panelin gosterdigi adres (test icin).</summary>
        public DeliveryStop CurrentStop => _stop;

        public void Show(DeliveryStop stop)
        {
            _stop = stop;
            if (!EnsurePanel()) return;

            panel.SetActive(true);
            Refresh();
        }

        /// <summary>
        /// Panel referansini garanti eder; yoksa kurulum adiyla ("Panel"
        /// cocugu) bulunur. DepotView ile ayni neden: kurulum menusi
        /// calistirilmamissa oyun kilitli kalmamali.
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
                        "[StopView] Panel bulunamadi. Menu: Kargo/6. Arayuzu Kur", this);
                }
                return false;
            }

            panel = child.gameObject;
            Debug.LogWarning(
                "[StopView] panel referansi BOS oldu; 'Panel' cocuguyla duzeltildi. " +
                "Kalici cozum: Kargo/6. Arayuzu Kur", this);
            return true;
        }

        public void Hide()
        {
            if (panel != null) panel.SetActive(false);
            _stop = null;
        }

        public void Refresh()
        {
            if (panel == null || !panel.activeSelf || _stop == null) return;

            if (headerText != null)
                headerText.text = "TESLIMAT: " + _stop.DisplayName;

            // Kargolari tek string'e yig: az sayida kargo (1-5), ayri satir
            // nesnesi olusturmak gereksiz.
            _buffer.Clear();
            for (int i = 0; i < _stop.Parcels.Count; i++)
            {
                Parcel p = _stop.Parcels[i];
                if (p != null && p.IsActive) _buffer.Add(p);
            }

            if (listText != null)
            {
                if (_buffer.Count == 0)
                {
                    listText.text = "Bu adresteki tum kargolar teslim edildi.";
                }
                else
                {
                    var sb = new System.Text.StringBuilder();
                    for (int i = 0; i < _buffer.Count; i++)
                    {
                        Parcel p = _buffer[i];
                        sb.AppendLine(string.Format(
                            "• {0}  ({1} kg, {2})  {3} TL   Sure {4:0} sn",
                            p.Label, p.WeightKg.ToString("0.0"),
                            p.Priority.DisplayName(), p.BaseValue, p.DeadlineSeconds));
                    }
                    listText.text = sb.ToString();
                }
            }

            if (hintText != null)
                hintText.text = _buffer.Count > 0
                    ? "E: TESLIM ET"
                    : "Tamamlandi. Tab: devam et";
        }
    }
}
