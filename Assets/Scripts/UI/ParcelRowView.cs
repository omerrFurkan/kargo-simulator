using KargoSim.Cargo;
using UnityEngine;
using UnityEngine.UI;

namespace KargoSim.UI
{
    /// <summary>
    /// Depo listesindeki tek bir kargo satiri. Satirlar havuzlanir: her
    /// acilista yeniden yaratilirsa arayuz titrer ve gereksiz allocation
    /// olur.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ParcelRowView : MonoBehaviour
    {
        [Header("Parcalar")]
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private Image selectionMark;
        [SerializeField] private Text label;
        [SerializeField] private Text detail;

        private Parcel _parcel;
        private int _index = -1;
        private System.Action<ParcelRowView> _onClicked;

        public Parcel Parcel => _parcel;
        public int Index => _index;
        public bool IsSelected { get; private set; }
        public bool Fits => _fits;
        private bool _fits = true;

        private void Awake()
        {
            if (button != null) button.onClick.AddListener(OnClick);
        }

        private void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(OnClick);
        }

        /// <summary>Satiri bir kargoya baglar ve gosterir/gizler.</summary>
        public void Bind(Parcel parcel, int index, bool selected, bool fits,
                         System.Action<ParcelRowView> onClicked)
        {
            _parcel = parcel;
            _index = index;
            _onClicked = onClicked;
            IsSelected = selected;
            _fits = fits;

            gameObject.SetActive(parcel != null);
            if (parcel == null) return;

            if (label != null)
            {
                label.text = (index + 1) + ")  " + parcel.Label;
                label.color = fits ? UITheme.TextPrimary : UITheme.TextDisabled;
            }

            if (detail != null)
            {
                detail.text = string.Format(
                    "{0} kg / {1} L   |   {2}   |   {3:0} TL   |   Sure {4:0} sn{5}",
                    parcel.WeightKg.ToString("0.0"),
                    parcel.VolumeLiters.ToString("0"),
                    parcel.Priority.DisplayName(),
                    parcel.BaseValue,
                    parcel.DeadlineSeconds,
                    parcel.Fragile ? "   |   KIRILABILIR" : "");
                detail.color = parcel.Fragile ? UITheme.Warning : UITheme.TextMuted;
            }

            if (selectionMark != null)
                selectionMark.enabled = IsSelected;

            if (background != null)
                background.color = IsSelected
                    ? new Color(UITheme.Accent.r, UITheme.Accent.g, UITheme.Accent.b, 0.22f)
                    : new Color(1f, 1f, 1f, 0.05f);
        }

        /// <summary>Secim durumu degistirilir (klavye ile).</summary>
        public void SetSelected(bool selected)
        {
            IsSelected = selected;
            if (selectionMark != null) selectionMark.enabled = selected;

            if (background != null)
                background.color = selected
                    ? new Color(UITheme.Accent.r, UITheme.Accent.g, UITheme.Accent.b, 0.22f)
                    : new Color(1f, 1f, 1f, 0.05f);
        }

        private void OnClick()
        {
            if (_parcel == null) return;
            _onClicked?.Invoke(this);
        }
    }
}
