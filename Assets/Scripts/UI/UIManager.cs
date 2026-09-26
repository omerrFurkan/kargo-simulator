using System.Collections.Generic;
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

        /// <summary>
        /// TUM arac kargo noktalari (van + bisiklet).
        ///
        /// ASAMA 15: tek nokta tutuluyordu ve F daima van'a biniyordu.
        /// Iki arac olduktan sonra oyuncu BISIKLETE binemiyordu.
        /// </summary>
        private readonly List<Player.VanCargoPoint> _cargoPoints =
            new List<Player.VanCargoPoint>();
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
                    bool used = _courier.TryInteract();

                    // Odakta hedef yoksa ve oyuncu depoda degilse
                    // depo panelini ac (eski davranis).
                    if (!used && atDepot) OpenDepot();
                }
                else if (_courier != null && _courier.IsInVehicle)
                {
                    if (_delivery != null && _delivery.CurrentStop != null) RequestDeliverNext();
                }
                else if (atDepot) OpenDepot();
            }

            // --- G: elindeki kutuyu BIRAK ---
            //
            // ASAMA 15: BIRAKMA E'YE baglanmisti ve calismiyordu.
            // Iki sebep:
            //  1) Kutu alindigi yer depo; depoda E depo panelini ACHIYOR,
            //     birakma hic denenmiyordu.
            //  2) Depoda oyuncunun odagi rafta oldugu icin E yeni kutu
            //     ALIYORDU, birakma diye hic ulasilamiyordu.
            //
            // Cozum: ayri tus. E baglamsal kalir (her zaman hedefi alir),
            // G her zaman birakir. Karistirma yok.
            if (Input.GetKeyDown(KeyCode.G) && !depotOpen)
            {
                if (_courier != null && !_courier.IsInVehicle) TryDropCarried();
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
            if (_courier == null) return;

            // ASAMA 15: IKI ARAÇ var (van + bisiklet) ve F oyuncunun
            // YANINDAKİ araca binmeli.
            //
            // Once TEK bir kargo noktasi tutuluyordu ve o her zaman VAN'in
            // noktasiydi. Sonuc: bisiklete HICBIR ZAMAN binilemiyordu,
            // F her zaman van'a biniyordu. Oyuncu "bisiklet binemedim"
            // dedi - dogruydu.
            var near = NearestCargoPoint();
            if (near == null)
            {
                if (hud != null)
                    hud.SetHint("Arac yok: bisiklet veya vanin yanina git");
                return;
            }

            if (_courier.IsInVehicle)
            {
                if (!near.IsVehicleSlow())
                {
                    if (hud != null) hud.SetHint("Arac yavaslayinca inebilirsin");
                    return;
                }

                near.SettleAfterExit();
                _courier.ExitVehicle(near.ExitSpot());
                return;
            }

            near.Board(_courier);
        }

        /// <summary>
        /// Oyuncunun EN YAKININDAKI arac kargo noktasi.
        ///
        /// Neden "en yakin" ve "odak" degil: oyuncu aracin yaninda ama
        /// kamerasi onune bakmiyor olabilir. yakinlik kurali "yaninda
        /// dur" demiyi daha guvenilir kiliyor.
        ///
        /// Eski aracla birlikte "binilmis" arac varsa odur: iki arac da
        /// yakinlikte ayni olabilir.
        /// </summary>
        private Player.VanCargoPoint NearestCargoPoint()
        {
            Player.VanCargoPoint best = null;
            float bestDistance = 4.5f;   // binecek kadar yakin

            if (_vanPoint != null)
            {
                // Su an kullanilan arac: her zaman oncelikli.
                if (_courier.ActiveVehicle != null &&
                    _vanPoint != null &&
                    _courier.ActiveVehicle.VehicleTransform != null)
                {
                    float d = Vector3.Distance(
                        _courier.transform.position,
                        _vanPoint.transform.position);
                    best = _vanPoint;
                    bestDistance = d;
                }
            }

            for (int i = 0; i < _cargoPoints.Count; i++)
            {
                var point = _cargoPoints[i];
                if (point == null) continue;

                float d = Vector3.Distance(
                    _courier.transform.position, point.transform.position);
                if (d >= bestDistance) continue;

                bestDistance = d;
                best = point;
            }

            return best;
        }

        public void SetVanPoint(Player.VanCargoPoint point)
        {
            _vanPoint = point;
            RegisterCargoPoint(point);
        }

        /// <summary>Tum arac kargo noktalarini kaydeder (iki arac icin).</summary>
        public void SetCargoPoints(System.Collections.Generic.List<Player.VanCargoPoint> points)
        {
            _cargoPoints.Clear();
            if (points == null) return;
            for (int i = 0; i < points.Count; i++) _cargoPoints.Add(points[i]);
        }

        private void RegisterCargoPoint(Player.VanCargoPoint point)
        {
            if (point == null) return;
            if (_cargoPoints.Contains(point)) return;
            _cargoPoints.Add(point);
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

        /// <summary>
        /// Eldeki kutuyu yere birakir (E, hedef yokken).
        ///
        /// ASAMA 15: dikey dilim "masaya gotur -> BIRAK -> paketle ->
        /// tekrar al" adimini gerektiriyordu ve bu adim YOKTU.
        /// </summary>
        private void TryDropCarried()
        {
            if (_courier == null || _courier.IsInVehicle) return;
            if (_courier.CarriedCount <= 0) return;

            Cargo.Parcel dropped = _courier.DropTopToWorld(_courier.transform.position);
            if (dropped == null) return;

            _courier.PlayAction("interact-right", 0.3f);
            if (hud != null)
                hud.SetHint("Kutu birakildi: " + dropped.Label
                            + "  |  El: " + _courier.CarriedCount);
        }

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
