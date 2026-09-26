using System;
using KargoSim.Cargo;
using KargoSim.Core;
using KargoSim.Delivery;
using KargoSim.Player;

namespace KargoSim.Economy
{
    /// <summary>
    /// Teslimat olaylarini dinler, odemeyi hesaplar ve oyuncunun ilerlemesine
    /// uygular.
    ///
    /// BURASI orkestrasyon katmanidir: Cargo, Delivery ve Player birbirini
    /// tanimaz, hepsi burada bulusur. "Hangi kargo ne kadar kazandirdi"
    /// sorusunun TEK bir cevabi vardir.
    ///
    /// Update() YOK. Dayanma tek teslimde biten olaydir, surmek icin
    /// kare gerekmez.
    /// </summary>
    public sealed class EconomyService : IDisposable
    {
        private readonly PlayerProgress _progress;
        private readonly EconomyConfig _config;
        private readonly DeliverySystem _delivery;
        private float _elapsedProvider;

        // DUZELTME (Hata 2): bu alan readonly idi ve hicbir yerde
        // guncellenmiyordu. EconomyService "..., 1)" ile kuruldugu icin
        // 2. gunden itibaren HER odeme kaydi 1. gune yaziliyordu.
        // PlayerProgress.BeginDayStatsIfNewDay() gelen gun numarasini
        // bekler; hep 1 gordugu icin 2. gunun istatistikleri 1. gunla
        // ayni kovada birisiyor, gun sonu ozeti yanlis gunu gosteriyor
        // ve oyuncunun 2. gun kazanci kayboluyordu.
        private int _currentDay;
        private bool _disposed;

        public PlayerProgress Progress => _progress;
        public EconomyConfig Config => _config;
        public DeliverySystem Delivery => _delivery;

        /// <summary>Odeme kayitlarinin yazildigi aktif gun (1'den baslar).</summary>
        public int CurrentDay => _currentDay;

        /// <summary>
        /// Simülasyon saatani. Teslimat sisteminden okunur; ekonomi saati
        /// KENDI tutmaz (tek saat, tek kaynak).
        /// </summary>
        public float ElapsedProvider
        {
            get => _elapsedProvider;
            set => _elapsedProvider = value;
        }

        public EconomyService(EconomyConfig config, PlayerProgress progress,
                              DeliverySystem delivery, int currentDay)
        {
            _config = config;
            _progress = progress;
            _delivery = delivery;

            // 0 veya negatif gun "bilinmiyor" demektir; istatistik kovasini
            // bozmamak icin en az 1'e sabitlenir.
            _currentDay = currentDay < 1 ? 1 : currentDay;

            GameEvents.ParcelDelivered += OnParcelDelivered;

            // DUZELTME (Hata 2): gunu kendimiz de dinleriz. CompositionRoot
            // SetDay() cagirmasa bile (veya cagirmayi unutsa bile) aktif
            // gun odemeden once guncellenir. Abonelik kapatilabilir oldugu
            // icin sizinti olusmaz.
            GameEvents.DayStarted += OnDayStarted;

            // Teslimat kodu (Asama 13). Sadece CEZA uygular; basarili
            // teslim parası ParcelDelivered uzerinden gelmeye devam eder,
            // yani teslim odemesi bu eklemeden hic degismedi.
            GameEvents.DeliveryCodeWrong += OnDeliveryCodeWrong;
        }

        /// <summary>
        /// Aktif gunu gunceller. Disaridan (kayit yukleme, test, elle kurulum)
        /// cagrilabilir; ayrica DayStarted olayindan da otomatik gelir.
        ///
        /// DUZELTME (Hata 2): _currentDay readonly idi, bu yuzden 2. gunden
        /// itibaren ApplyPayout(..., day: 1) cagriliyordu.
        /// </summary>
        public void SetDay(int day)
        {
            if (_disposed) return;

            // 0 gecersizdir: PlayerProgress 0'i "gun baslamadi" sayar ve
            // her odemede istatistik kovasini yeniden acar.
            if (day < 1) return;

            _currentDay = day;
        }

        private void OnDayStarted(int day) => SetDay(day);

        private void OnParcelDelivered(Parcel parcel, DeliveryOutcome outcome)
        {
            // DUZELTME (Hata 1): Dispose() cagrildiktan sonra bu abonelik
            // GameEvents busunda kalmasin; olen bir ekonomi nesnesi
            // odemeyi yazmaya devam etmesin.
            if (_disposed || parcel == null) return;

            // Zaman bilgisi teslimat sisteminden gelir; ekonomi kendi
            // sayacini tutmaz, yoksa iki saat birbirinden sapar.
            float elapsed = _elapsedProvider;

            float ratio = PayoutCalculator.RemainingRatio(parcel, elapsed);
            Payout payout = PayoutCalculator.Calculate(parcel.BaseValue, parcel.WeightKg,
                                                        outcome, ratio, _config);

            _progress.ApplyPayout(payout, outcome, _currentDay);
        }

        /// <summary>Gun bitti: ozeti uret, istatistikleri temizle.</summary>
        public DaySummary EndDay(int day)
        {
            DaySummary summary = _progress.EndDay(day);
            GameEvents.RaiseDaySummaryReady(summary);
            return summary;
        }

        /// <summary>
        /// Kayittan yukleme (Aşama 7). Sistem olaylari icin kayit degil.
        /// </summary>
        public void Restore(int money, int xp, int level)
        {
            _progress.RestoreFromSave(money, xp, level);
        }

        /// <summary>
        /// Abonelikleri kaldirir. Sahnede birden fazla ekonomi kuruldugunda
        /// (ya da "Domain Reload: Disabled" ile EditMode'den Play'e gecildiginde)
        /// statik event busunda OLU BIRIKEN dinleyiciler silinmez; her
        /// kurulum bir Onceki olay zincirini daha ekler.
        ///
        /// DUZELTME (Hata 1): kurucuda GameEvents.ParcelDelivered += vardi,
        /// -= HIC YOKTU. Bu yuzden:
        ///   - ikinci bir EconomyService kurulunca bir teslim ODEMESI IKI KEZ
        ///     yaziliyor (para ve XP katlaniyor, gun sonu sayaclari bozuluyor),
        ///   - sahne yeniden yuklendiginde eski (olu) servis hala dinliyor ve
        ///     oyuncunun parasini yazmaya devam ediyor.
        /// Cagrilmasa bile etkisi olmaz: sadece dinleyiciyi bus'tan siler.
        /// </summary>
        /// <summary>
        /// Yanlis teslimat kodu: ceza kesilir, odeme VERILMEZ.
        ///
        /// Neden ayri: para kazanma yolu <c>ParcelDelivered</c>'dır. Yanlis
        /// kodda bu olay hiç yayinlanmadigi icin zaten odeme olmaz; burada
        /// sadece CEZA var.
        ///
        /// Kurgu: gercek bir teslimat sirketinde musteriye gidip kodu
        /// okutamamak bir teslimat DEGILDIR - kurye yine de yolda zaman
        /// kaybeder. Ceza her hatali giris icindir; kural degistirilmek
        /// istenirse tek yer burasi.
        /// </summary>
        private void OnDeliveryCodeWrong(Cargo.Parcel parcel)
        {
            if (_disposed) return;
            if (_config == null) return;
            if (_config.wrongCodePenalty <= 0) return;

            // DIKKAT: `Delivery` bu sinifta bir PROPERTI'dir
            // (DeliverySystem Delivery => _delivery), yani
            // `Delivery.DeliveryOutcome` yazmak property'ye gider ve
            // derlenmez (CS1061). Tam nitelendirme sart.
            var penalty = new Payout(0, 0, _config.wrongCodePenalty, 0);
            _progress.ApplyPayout(penalty,
                KargoSim.Delivery.DeliveryOutcome.Failed, _currentDay);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            GameEvents.ParcelDelivered -= OnParcelDelivered;
            GameEvents.DayStarted -= OnDayStarted;
            GameEvents.DeliveryCodeWrong -= OnDeliveryCodeWrong;

            GC.SuppressFinalize(this);
        }
    }
}
