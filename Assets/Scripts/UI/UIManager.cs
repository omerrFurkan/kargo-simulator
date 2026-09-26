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

        [Tooltip("Teslimat kodu giris paneli (Asama 13).")]
        [SerializeField] private CodeEntryView codeEntry;

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
        [Tooltip("Araca bin / aractan in. E kargoyla ilgilenir; binme " +
                 "ayri tusa ayrildi, yoksa arac doluyken oyuncu hic binemez.")]
        [SerializeField] private KeyCode boardKey = KeyCode.F;
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
        private Player.VanCargoPoint _vanPoint;
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

            // Kod paneli ONCE islenir: oyuncunun kapiyi calan E basisi
            // HandleInput icinde tetiklenir ve ayni karede paneli acar.
            // Sirayi ters cevirirsek o karede E, henuz acilmamis panelin
            // tuslarini tuketirdi.
            if (codeEntry != null) codeEntry.Tick(deltaTime);

            // Panel acikken oyunun diger girdileri (binme, bosaltma,
            // depo paneli) sizmasin: kod girisi bitmeden araca binilirse
            // teslimat oturumu havada kalir.
            if (codeEntry != null && codeEntry.IsOpen) return;

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

            // --- Gün sonu özeti: oyun DURUR, oyuncu okuyup devam eder ---
            // Önce panel "Tab: yeni güne başla" yazıyordu ama Tab hiçbir
            // şey yapmıyordu ve oyun panelin arkasında kesintisiz akıyordu.
            if (summaryView != null && summaryView.IsOpen)
            {
                if (Input.GetKeyDown(confirmKey) || Input.GetKeyDown(depotToggleKey))
                {
                    summaryView.Hide();
                }
                UpdateHint(atDepot, depotOpen);
                return;
            }

            // --- Esc: her seyi kapat, imleci serbest birak ---
            if (Input.GetKeyDown(closeKey))
            {
                depotView?.Close();
                stopView?.Hide();
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

            // --- E: baglamsal onay (yalnizca odakta hedef varken) ---
            if (Input.GetKeyDown(confirmKey) && !depotOpen)
            {
                // Ayaktayken E, odaktaki hedefe gider: raftan kargo al,
                // araca koy, araktan al, kapida birak.
                if (_courier != null && !_courier.IsInVehicle)
                {
                    if (!_courier.TryInteract() && atDepot) OpenDepot();
                }
                else if (_courier != null && _courier.IsInVehicle)
                {
                    if (_delivery != null && _delivery.CurrentStop != null) RequestDeliverNext();
                }
                else if (atDepot) OpenDepot();
            }

            // --- Depo paneli acikken: rakam sec, Q bosalt, E yukle ---
            if (depotOpen)
            {
                for (int i = 0; i < 9; i++)
                {
                    if (Input.GetKeyDown(KeyCode.Alpha1 + i)) depotView.ToggleSelection(i);
                }

                if (Input.GetKeyDown(unloadAllKey)) _cargo.UnloadVehicle();

                if (Input.GetKeyDown(confirmKey)) RequestLoadSelected();
            }

            // --- F: araca bin / aractan in ---
            // E kargoyla ilgilenir; binme AYRI tus. Tek tusa
            // sigdirilsaydi arac doluyken oyuncu hic binemezdi.
            if (Input.GetKeyDown(boardKey)) ToggleVehicle();

            UpdateHint(atDepot, depotOpen);
        }

        /// <summary>
        /// Araca biner veya aractan iner. HIZLI arac birakilmaz:
        /// hareketli bir aracin yanina 1sinda inmak karakteri
        /// aracin altinda birakir.
        /// </summary>
        private void ToggleVehicle()
        {
            if (_courier == null || _vanPoint == null) return;

            if (_courier.IsInVehicle)
            {
                if (!_vanPoint.IsVehicleSlow())
                {
                    if (hud != null) hud.SetHint("Arac yavaslayinca inebilirsin");
                    return;
                }

                _courier.ExitVehicle(_vanPoint.ExitSpot());
                return;
            }

            _vanPoint.Board(_courier);
        }

        public void SetVanPoint(Player.VanCargoPoint point)        {
            _vanPoint = point;
        }

        /// <summary>
        /// Kod giris panelini baglar (CompositionRoot cagirir).
        ///
        /// DIKKAT: panel kurulumda KAPALI birakildigi icin
        /// FindFirstObjectByType'in FindObjectsInactive.Include overload'u
        /// ile bulunmasi gerekir.
        /// </summary>
        public void SetCodeEntry(CodeEntryView view) => codeEntry = view;

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

                // Odak yokken SABIT metin yazmak yanilticiydi: oyuncu
                // aracin arkasina yururken de "dukkana gir" yaziyordu.
                // Artik gercek duruma gore susar.
                hud.SetHint(prompt);
                return;
            }

            if (_courier != null && _courier.IsInVehicle)
            {
                hud.SetHint("[F] Aracdan in    |    Depo: mavi alan    |    Hedef: renkli kare");
                return;
            }

            if (depotOpen)
            {
                hud.SetHint("[1-9] Kargo sec    |    [E] Yukle    |    [Q] Bosalt    |    [Esc] Kapat");
                return;
            }

            if (atDepot)
            {
                hud.SetHint("[E] Depoyu ac    |    [F] Araca bin");
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
