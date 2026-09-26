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
        [Tooltip("Bağlamsal onay: depoda panel açar ve seçili kargoyu yükler, " +
                 "durakta teslim yapar.\n" +
                 "TAB bu tusun alternatifi olarak birakildi ama Unity EDITORU " +
                 "Tab'i odak degistirmek icin kullanir - Editor'de calismaz, " +
                 "build'de calisir. Bu yuzden E birincil.")]
        [SerializeField] private KeyCode confirmKey = KeyCode.E;
        [SerializeField] private KeyCode depotToggleKey = KeyCode.Tab;
        [SerializeField] private KeyCode unloadAllKey = KeyCode.Q;
        [SerializeField] private KeyCode closeKey = KeyCode.Escape;

        private CargoSystem _cargo;
        private DeliverySystem _delivery;
        private PlayerProgress _progress;
        private GameClock _clock;
        private MapLayout _layout;
        private Transform _vehicle;
        private Player.CourierController _courier;
        private bool _bound;

        /// <summary>
        /// Kontrolundeki kurye karakteri. Baglanmazsa oyun yalnizca
        /// aracla oynanir ve kargo arayuzden aninda yuklenir.
        /// </summary>
        public void SetCourier(Player.CourierController value)
        {
            _courier = value;
        }

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
            bool depotOpen = depotView != null && depotView.IsOpen;

            // --- Esc: her seyi kapat, imleci serbest birak ---
            if (Input.GetKeyDown(closeKey))
            {
                depotView?.Close();
                stopView?.Hide();
                summaryView?.Hide();
                _courier?.SetCursorLocked(false);
            }

            // --- Tab: alternatif depo ac/kapa (Editor'de yutulur) ---
            if (Input.GetKeyDown(depotToggleKey))
            {
                if (depotOpen) depotView.Close();
                else if (atDepot) OpenDepot();
            }

            // --- Depo paneli: rakam tuslari secim, Q bosaltir ---
            if (depotOpen)
            {
                for (int i = 0; i < 9; i++)
                {
                    if (Input.GetKeyDown(KeyCode.Alpha1 + i)) depotView.ToggleSelection(i);
                }

                if (Input.GetKeyDown(unloadAllKey))
                {
                    _cargo.UnloadVehicle();
                }
            }

            // --- E: baglamsal onay ---
            if (Input.GetKeyDown(confirmKey))
            {
                // Ayaktayken E, odaktaki hedefe gider: raftan kargo al,
                // araca koy, araca bin, kapiya birak. HEDEF ne olacagini
                // kendisi soyler.
                if (_courier != null && !_courier.IsInVehicle)
                {
                    if (!_courier.TryInteract()) OpenDepot();
                    return;
                }

                if (_courier != null && _courier.IsInVehicle)
                {
                    // Araçta: E -> aracdan in
                    ExitVehicle();
                    return;
                }

                if (depotOpen) RequestLoadSelected();
                else if (atDepot) OpenDepot();
                else if (_delivery != null && _delivery.CurrentStop != null) RequestDeliverNext();
            }

            UpdateHint(atDepot, depotOpen);
        }

        /// <summary>Aracdan in: kurye arac lastiginin yanina belirir.</summary>
        private void ExitVehicle()
        {
            if (_courier == null || _vehicle == null) return;

            Vector3 spot = _vehicle.position + _vehicle.right * 1.8f;
            _courier.ExitVehicle(spot);
        }

        /// <summary>
        /// Secili kargolari araca yukletir. Kurye ajani varsa karakter
        /// teker teker tasir; yoksa eski aninda yukleme yolu kullanilir.
        /// </summary>
        private void RequestLoadSelected()
        {
            if (_cargo == null || depotView == null) return;

            var parcels = depotView.SelectedParcels();
            if (parcels.Count == 0) return;

            // Bu yol sadece karakter YOKKEN kullanilir (yedek).
            // Karakter varken yukleme fizikseldir: raftan al, araca
            // gotur (bkz. ShopParcelPile + VanCargoPoint).
            foreach (Parcel parcel in parcels) _cargo.TryLoadParcel(parcel.Id);
            depotView.ClearSelection();
        }

        /// <summary>
        /// Oyuncuya ANLIK ne yapabilecegini yazar. Kimse 10 deneme
        /// yapip dogru tusu bulmamali.
        /// </summary>
        private void UpdateHint(bool atDepot, bool depotOpen)
        {
            if (hud == null) return;

            // Ayaktayken: odaktaki hedefin ipucu. Oyuncu ne yapabilecegini
            // E'ye basmadan gorur.
            if (_courier != null && !_courier.IsInVehicle)
            {
                var focus = _courier.Focus;
                string prompt = focus != null ? focus.Prompt : "";
                if (_courier.IsCarrying)
                    prompt = (prompt == "" ? "" : prompt + "    |    ") + "Elde: " + _courier.CarriedCount;

                hud.SetHint(prompt == "" ? "Dukkana gir, raftan kargo al" : prompt);
                return;
            }

            if (_courier != null && _courier.IsInVehicle)
            {
                hud.SetHint("[E] Aracdan in    |    Depo: mavi alan    |    Hedef: renkli kare");
                return;
            }

            if (depotOpen)
            {
                hud.SetHint("[1-9] Kargo sec    |    [E] Yukle    |    [Q] Bosalt    |    [Esc] Kapat");
                return;
            }

            if (atDepot)
            {
                hud.SetHint("[E] Depoyu ac");
                return;
            }

            hud.SetHint("Depo: mavi alan    |    Hedef: renkli kare");
        }

        /// <summary>
        /// <summary>
        /// YEDEK yol: kurye yoksa kargo durakta aninda teslim edilir.
        ///
        /// Kurye varsa bu yol KULLANILMAZ: teslimat adres kapisinda,
        /// karakterin eliyle yapilir (bkz. AddressDoor). Aninda
        /// teslim sadece "kurulum eksikse oyun yine oynanabilir" diye
        /// vardir.
        /// </summary>
        private void RequestDeliverNext()
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
