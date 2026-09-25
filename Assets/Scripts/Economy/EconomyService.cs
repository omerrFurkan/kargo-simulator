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
    public sealed class EconomyService
    {
        private readonly PlayerProgress _progress;
        private readonly EconomyConfig _config;
        private readonly DeliverySystem _delivery;
        private readonly int _currentDay;
        private float _elapsedProvider;

        public PlayerProgress Progress => _progress;
        public EconomyConfig Config => _config;

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
            _currentDay = currentDay;

            GameEvents.ParcelDelivered += OnParcelDelivered;
        }

        private void OnParcelDelivered(Parcel parcel, DeliveryOutcome outcome)
        {
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
    }
}
