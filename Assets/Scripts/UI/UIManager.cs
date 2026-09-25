using KargoSim.Cargo;
using KargoSim.Core;
using KargoSim.Delivery;
using KargoSim.Economy;
using KargoSim.Player;
using UnityEngine;

namespace KargoSim.UI
{
    /// <summary>
    /// Arayuzun tek giris noktasi.
    ///
    /// SORUMLULUKLAR:
    ///  - Sistemleri dinlemek ve gorunumu tazelermek (olay g driven)
    ///  - Oyuncu girdisini okumak (E / Tab / Q / rakamlar)
    ///  - Baglama bagli gorunumler (minimap) her kare tazelemek
    ///
    /// Update() YOK. CompositionRoot her karede <see cref="Tick"/> cagirir.
    /// Boylece "oyunda tek Update" kurali bozulmaz ve UI simülasyonla
    /// ayni karede guncellenir.
    ///
    /// PERFORMANS: Metinler her karede degil, DEGER DEGISTIGINDE
    /// guncellenir (dirty check). Boylece saniyede 60 string ayristirma
    /// yapilmaz, gerektiginde bir tane yapilir.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIManager : MonoBehaviour
    {
        [Header("Gorunumler")]
        [SerializeField] private HudView hud;
        [SerializeField] private DepotView depotView;
        [SerializeField] private StopView stopView;
        [SerializeField] private DaySummaryView summaryView;
        [SerializeField] private MinimapView minimap;

        [Header("Ayar")]
        [Tooltip("Aracin deponun bu yaricapinda sayilmasi (metre).")]
        [SerializeField] private float depotRadius = 12f;

        [Header("Klavye")]
        [SerializeField] private KeyCode depotToggleKey = KeyCode.Tab;
        [SerializeField] private KeyCode confirmKey = KeyCode.E;
        [SerializeField] private KeyCode unloadAllKey = KeyCode.Q;
        [SerializeField] private KeyCode closeKey = KeyCode.Escape;

        private CargoSystem _cargo;
        private DeliverySystem _delivery;
        private PlayerProgress _progress;
        private GameClock _clock;
        private MapLayout _layout;
        private Transform _vehicle;
        private bool _bound;

        // ------------------------------------------------------------------
        // Baglama
        // ------------------------------------------------------------------

        /// <summary>
        /// CompositionRoot tarafindan cagrilir. Buradan sonra UI kendi
        /// bagimliliklarini bilir ve sistemlerden haberdar olur.
        /// </summary>
        public void Bind(CargoSystem cargo, DeliverySystem delivery, PlayerProgress progress,
                        GameClock clock, MapLayout layout, Transform vehicle)
        {
            _cargo = cargo;
            _delivery = delivery;
            _progress = progress;
            _clock = clock;
            _layout = layout;
            _vehicle = vehicle;

            if (_cargo == null || _progress == null || _clock == null)
            {
                Debug.LogError("[UIManager] Baglama eksik (kargo/ilerleme/saat). " +
                               "Arayuz calismayacak.", this);
                enabled = false;
                return;
            }

            Subscribe();
            RefreshAll();
            _bound = true;
        }

        private void OnDestroy()
        {
            if (!_bound) return;
            Unsubscribe();
        }

        private void Subscribe()
        {
            GameEvents.CargoChanged += OnCargoChanged;
            GameEvents.RouteChanged += OnRouteChanged;
            GameEvents.PayoutApplied += OnPayoutApplied;
            GameEvents.LevelUp += OnLevelUp;
            GameEvents.DaySummaryReady += OnDaySummary;
            GameEvents.DeliveryStopArrived += OnStopArrived;
        }

        private void Unsubscribe()
        {
            GameEvents.CargoChanged -= OnCargoChanged;
            GameEvents.RouteChanged -= OnRouteChanged;
            GameEvents.PayoutApplied -= OnPayoutApplied;
            GameEvents.LevelUp -= OnLevelUp;
            GameEvents.DaySummaryReady -= OnDaySummary;
            GameEvents.DeliveryStopArrived -= OnStopArrived;
        }

        // ------------------------------------------------------------------
        // Her kare
        // ------------------------------------------------------------------

        /// <summary>CompositionRoot tarafindan cagrilir (Update degil).</summary>
        public void Tick(float deltaTime)
        {
            if (!_bound) return;

            HandleInput();

            // Odeme/seviye bildiriminin suresi dolduysa gizle
            if (hud != null) hud.TickFeedback();

            // Yalnizca baglama bagli olan minimap her kare tazelenir.
            if (minimap != null && _vehicle != null)
                minimap.SetVehiclePosition(_vehicle.position);
        }

        private void HandleInput()
        {
            bool atDepot = IsAtDepot();

            // --- Panel ac/kapa ---
            if (Input.GetKeyDown(depotToggleKey))
            {
                if (depotView != null && depotView.IsOpen) depotView.Close();
                else if (atDepot) OpenDepot();
            }

            if (Input.GetKeyDown(closeKey))
            {
                depotView?.Close();
                summaryView?.Hide();
            }

            // --- Depo ---
            if (depotView != null && depotView.IsOpen)
            {
                // 1-9 rakamlariyla secim degistir
                for (int i = 0; i < 9; i++)
                {
                    if (!Input.GetKeyDown(KeyCode.Alpha1 + i)) continue;
                    depotView.ToggleSelection(i);
                }

                if (Input.GetKeyDown(confirmKey)) depotView.LoadSelected();
                if (Input.GetKeyDown(unloadAllKey)) _cargo.UnloadVehicle();
            }

            // --- Teslim (durak basinda) ---
            if (Input.GetKeyDown(confirmKey) &&
                (depotView == null || !depotView.IsOpen) &&
                _delivery != null && _delivery.CurrentStop != null)
            {
                DeliverNext();
            }
        }

        /// <summary>Duraktaki siradaki kargoyu teslim eder.</summary>
        private void DeliverNext()
        {
            DeliveryStop stop = _delivery.CurrentStop;
            if (stop == null || stop.PendingCount == 0) return;

            Parcel parcel = null;
            for (int i = 0; i < stop.Parcels.Count; i++)
            {
                if (stop.Parcels[i] != null && stop.Parcels[i].IsActive) { parcel = stop.Parcels[i]; break; }
            }
            if (parcel == null) return;

            _delivery.TryDeliver(parcel.Id);
        }

        /// <summary>Arac deponun yaricapinda mi?</summary>
        public bool IsAtDepot()
        {
            if (_layout == null || _vehicle == null) return false;
            return Vector3.Distance(_vehicle.position, _layout.depotPosition) <= depotRadius;
        }

        // ------------------------------------------------------------------
        // Olay tepkileri
        // ------------------------------------------------------------------

        private void OnCargoChanged() => RefreshAll();

        private void OnRouteChanged()
        {
            if (minimap != null) minimap.Rebuild(_layout, _delivery);
        }

        private void OnPayoutApplied(Payout payout, DeliveryOutcome outcome)
        {
            if (hud != null) hud.FlashPayout(payout, outcome);
        }

        private void OnLevelUp(int level)
        {
            if (hud != null) hud.ShowLevelUp(level);
        }

        private void OnDaySummary(DaySummary summary)
        {
            if (summaryView != null) summaryView.Show(summary);
        }

        private void OnStopArrived(DeliveryStop stop)
        {
            if (stopView != null) stopView.Show(stop);
        }

        // ------------------------------------------------------------------
        // Genel
        // ------------------------------------------------------------------

        private void OpenDepot()
        {
            if (depotView == null) return;
            depotView.Open(_cargo);
            stopView?.Hide();
        }

        public void RefreshAll()
        {
            if (!_bound) return;

            hud?.Refresh(_progress, _clock, _cargo, _delivery);
            depotView?.Refresh();
            stopView?.Refresh();

            if (minimap != null) minimap.Rebuild(_layout, _delivery);
        }
    }
}
