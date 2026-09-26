using System.Collections.Generic;
using KargoSim.Cargo;
using UnityEngine;
using UnityEngine.UI;

namespace KargoSim.UI
{
    /// <summary>
    /// Depo paneli: bekleyen kargolar ve yukleme.
    ///
    /// Satirlar HAVUZLANIR. Kargo sayisi 12 civarinda olsa bile her
    /// acilista yeni GameObject yaratmak arayuz titremesi yaratir ve
    /// gereksiz allocation'dir; havuz bunu onler.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DepotView : MonoBehaviour
    {
        [Header("Parcalar")]
        [SerializeField] private GameObject panel;
        [SerializeField] private RectTransform rowContainer;
        [SerializeField] private ParcelRowView rowPrefab;
        [SerializeField] private Text headerText;
        [SerializeField] private Text footerText;

        private readonly List<ParcelRowView> _rows = new List<ParcelRowView>();
        private readonly HashSet<int> _selected = new HashSet<int>();
        private CargoSystem _cargo;
        private bool _warnedMissingPanel;

        public bool IsOpen => panel != null && panel.activeSelf;

        /// <summary>Depodaki kargo sayisi (test/hud icin).</summary>
        public int RowCount => _rows.Count;

        /// <summary>
        /// Secili satirlarin kargo referanslari - YUKLEME YAPMADAN.
        ///
        /// Kargo artik fiziksel: once karakter kargoyu tasir, ancak o
        /// ANDA mantik sistemine bildirilir. Bu yuzden UI once secimi
        /// sorup kurye ajanina devreder.
        /// </summary>
        public List<Parcel> SelectedParcels()
        {
            var result = new List<Parcel>();
            if (_cargo == null) return result;

            // Sirali: HashSet sirasi garanti degil, oyuncu "1, 2, 3"
            // dediginde sirayla yuklemesini bekler.
            var indices = new List<int>(_selected);
            indices.Sort();

            for (int i = 0; i < indices.Count; i++)
            {
                int index = indices[i];
                if (index < 0 || index >= _cargo.Manifest.AvailableCount) continue;
                result.Add(_cargo.Manifest.Available[index]);
            }

            return result;
        }

        /// <summary>Secimi temizler ve listeyi yeniler.</summary>
        public void ClearSelection()
        {
            if (_selected.Count == 0) return;
            _selected.Clear();
            Refresh();
        }

        public void Open(CargoSystem cargo)
        {
            _cargo = cargo;
            if (!EnsurePanel()) return;

            panel.SetActive(true);
            _selected.Clear();
            Refresh();
        }

        /// <summary>
        /// Panel referansini garanti eder.
        ///
        /// Neden: panel alani kurulum araci tarafindan doldurulur ve
        /// birSure arac atamayi sessizce reddedip alani null birakabilir.
        /// Boyle bir durumda oyuncu "E/Tab calismiyor" sanip gercekten
        /// calisan tuslari suclar; oyun da hicbir sey gostermeden kilitli
        /// kalir. Bu yuzden referans yoksa KURULUM ADIYLA (cocuk "Panel")
        /// bulunur: kurulum menusu calistirilmamis olsa bile oyun
        /// oynanabilir kalir.
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
                        "[DepotView] Panel bulunamadi (ne referans ne de 'Panel' cocugu). " +
                        "Menu: Kargo/6. Arayuzu Kur", this);
                }
                return false;
            }

            panel = child.gameObject;
            Debug.LogWarning(
                "[DepotView] panel referansi BOS oldu; 'Panel' cocuguyla duzeltildi. " +
                "Kalici cozum: Kargo/6. Arayuzu Kur", this);
            return true;
        }

        public void Close()
        {
            if (panel != null) panel.SetActive(false);
            _selected.Clear();
        }

        /// <summary>Rakam tusuyla secim degistirir (0 tabanli).</summary>
        public void ToggleSelection(int index)
        {
            if (_cargo == null) return;
            if (index < 0 || index >= _cargo.Manifest.AvailableCount) return;

            if (!_selected.Remove(index)) _selected.Add(index);
            Refresh();
        }

        /// <summary>Secili kargolari yukler. Sigmayanlar atlanir.</summary>
        public int LoadSelected()
        {
            if (_cargo == null || _selected.Count == 0) return 0;

            int loaded = 0;

            // Kopyala: yukleme sirasinda secim seti degisebilir.
            var indices = new List<int>(_selected);
            foreach (int index in indices)
            {
                if (index < 0 || index >= _cargo.Manifest.AvailableCount) continue;
                if (_cargo.TryLoadParcel(_cargo.Manifest.Available[index].Id)) loaded++;
            }

            _selected.Clear();
            Refresh();
            return loaded;
        }

        public void Refresh()
        {
            if (_cargo == null || panel == null) return;
            if (!panel.activeSelf) return;

            IReadOnlyList<Parcel> available = _cargo.Manifest.Available;
            int needed = available.Count;

            EnsureRowCount(needed);

            for (int i = 0; i < _rows.Count; i++)
            {
                if (i < needed)
                {
                    bool fits = _cargo.Manifest.Fits(available[i]);
                    _rows[i].Bind(available[i], i, _selected.Contains(i), fits, OnRowClicked);
                }
                else
                {
                    _rows[i].Bind(null, -1, false, true, OnRowClicked);
                }
            }

            if (headerText != null)
            {
                headerText.text = "DEPO - Gunluk Kargolar\n" +
                    "Yuklenecek: " + available.Count + "   Yuklu: " + _cargo.Manifest.LoadedCount;
            }

            if (footerText != null)
            {
                footerText.text =
                    "TIKLA veya 1-9: sec   |   E: secili yukle   |   Q: bosalt   |   Tab: kapat\n" +
                    "Beyaz = sigar, soluk = kapasite dolu";
            }
        }

        private void OnRowClicked(ParcelRowView row)
        {
            if (row == null || row.Index < 0) return;

            if (!_selected.Remove(row.Index)) _selected.Add(row.Index);
            Refresh();
        }

        private void EnsureRowCount(int needed)
        {
            // Fazlalari gizle, eksigi havuzdan al.
            while (_rows.Count < needed)
            {
                ParcelRowView row = Instantiate(rowPrefab, rowContainer);
                row.gameObject.SetActive(false);
                _rows.Add(row);
            }

            LayoutRows();
        }

        /// <summary>
        /// Satirlari alta dogru siralar.
        ///
        /// KRITIK: Satirlar havuzdan Instantiate edildiginde hepsi konteynerin
        /// TEPESINE yapisir (satir sablonunun anchor'i (0,1)-(1,1)). Yer
        /// verilmedigi icin 12 kargo 12 satirlik TEK yigin olarak ust uste
        /// biniyor ve okunmuyordu. Her satira index * RowHeight verilir.
        /// </summary>
        private void LayoutRows()
        {
            if (rowContainer == null) return;

            for (int i = 0; i < _rows.Count; i++)
            {
                var rt = (RectTransform)_rows[i].transform;
                rt.anchoredPosition = new Vector2(0f, -i * UITheme.RowHeight);
            }
        }
    }
}
