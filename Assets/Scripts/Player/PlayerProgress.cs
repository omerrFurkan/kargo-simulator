using System;
using KargoSim.Economy;
using KargoSim.Core;

namespace KargoSim.Player
{
    /// <summary>
    /// Gun sonu ozeti. Tek yerde toplanan sayilar - HUD'a, kayit ekranina
    /// ve ileride gorevlere ayni veri aktarilir.
    /// </summary>
    public readonly struct DaySummary
    {
        public int Day { get; }
        public int Delivered { get; }
        public int Successful { get; }
        public int Late { get; }
        public int Failed { get; }
        public int MoneyEarned { get; }
        public int XpEarned { get; }
        public int Level { get; }
        public int LevelUpsThisDay { get; }

        public DaySummary(int day, int delivered, int successful, int late, int failed,
                          int moneyEarned, int xpEarned, int level, int levelUps)
        {
            Day = day;
            Delivered = delivered;
            Successful = successful;
            Late = late;
            Failed = failed;
            MoneyEarned = moneyEarned;
            XpEarned = xpEarned;
            Level = level;
            LevelUpsThisDay = levelUps;
        }

        /// <summary>Hicbir teslimat yapilmadi mi?</summary>
        public bool IsEmpty => Delivered == 0;

        public override string ToString() =>
            $"Gun {Day}: {Delivered} teslimat ({Successful} zamaninda, {Late} gec, {Failed} basarisiz) " +
            $"+{MoneyEarned} TL, +{XpEarned} XP, seviye {Level}";
    }

    /// <summary>
    /// Oyuncunun para, XP ve seviyesi.
    ///
    /// Diger sistemlerden BILMEZ: sadece veri tutar ve uygular. Kimin ne
    /// kazandirdigini EconomyService bildirir. Bu ayrim sayesinde ilerleme
    /// mantigi test edilebilir ve UI'dan bagimsizdir.
    ///
    /// MonoBehaviour DEGIL - durum nesnesidir.
    /// </summary>
    public sealed class PlayerProgress
    {
        private readonly EconomyConfig _config;
        private int _money;
        private int _xp;
        private int _level = 1;

        // Gun istatistikleri (gun sonunda sifirlanir)
        private int _dayNumber;
        private int _dayDelivered;
        private int _daySuccessful;
        private int _dayLate;
        private int _dayFailed;
        private int _dayMoney;
        private int _dayXp;
        private int _dayLevelUps;

        public PlayerProgress(EconomyConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _money = config.startingMoney;
        }

        public int Money => _money;
        public int Xp => _xp;
        public int Level => _level;
        public bool IsMaxLevel => _level >= _config.maxLevel;

        /// <summary>Su seviyeye kadar gereken toplam XP.</summary>
        public int XpIntoLevel => _config.XpCostForLevel(_level);

        /// <summary>Bir sonraki seviyeye kalan XP. Azami seviyede 0.</summary>
        public int XpToNextLevel
        {
            get
            {
                if (IsMaxLevel) return 0;

                int currentCost = _config.XpCostForLevel(_level);
                int nextCost = _config.XpCostForLevel(_level + 1);
                return System.Math.Max(0, nextCost - _xp);
            }
        }

        /// <summary>0..1 seviye ilerleme cubugu (HUD icin).</summary>
        public float LevelProgress
        {
            get
            {
                if (IsMaxLevel) return 1f;

                int currentCost = _config.XpCostForLevel(_level);
                if (currentCost <= 0) return 0f;

                int nextCost = _config.XpCostForLevel(_level + 1);
                int span = nextCost - currentCost;
                if (span <= 0) return 0f;

                float progress = (float)(_xp - currentCost) / span;
                return progress < 0f ? 0f : (progress > 1f ? 1f : progress);
            }
        }

        // ------------------------------------------------------------------
        // Uygulama
        // ------------------------------------------------------------------

        /// <summary>
        /// Bir teslimin odemesini uygular. Seviye atlamayi da halledebilir
        /// (tek teslimde birden fazla seviye).
        /// </summary>
        /// <returns>Ulasilan azami seviye (seviye yukselmediyse mevcut).</returns>
        public int ApplyPayout(Payout payout, Delivery.DeliveryOutcome outcome, int day)
        {
            BeginDayStatsIfNewDay(day);

            int moneyDelta = payout.TotalMoney;
            if (moneyDelta != 0)
            {
                _money = System.Math.Max(0, _money + moneyDelta);
                _dayMoney += System.Math.Max(0, moneyDelta);
            }

            if (payout.Xp > 0)
            {
                _xp += payout.Xp;
                _dayXp += payout.Xp;
            }

            _dayDelivered++;
            switch (outcome)
            {
                case Delivery.DeliveryOutcome.Success: _daySuccessful++; break;
                case Delivery.DeliveryOutcome.Late: _dayLate++; break;
                case Delivery.DeliveryOutcome.Failed: _dayFailed++; break;
            }

            int newLevel = ApplyLevelUps();
            GameEvents.RaisePayoutApplied(payout, outcome);
            return newLevel;
        }

        /// <summary>
        /// XP arttikca seviye yukselir. Egrinin USTUNE cikan XP varsa (ornegin
        /// buyuk bir odemeden sonra) birden fazla seviye atlayabilir.
        /// </summary>
        private int ApplyLevelUps()
        {
            int newLevel = _level;
            while (!IsMaxLevel && _xp >= _config.XpCostForLevel(_level + 1))
            {
                _level++;
                newLevel = _level;
                _dayLevelUps++;
                GameEvents.RaiseLevelUp(_level);
            }

            return newLevel;
        }

        // ------------------------------------------------------------------
        // Gun dongusu
        // ------------------------------------------------------------------

        private void BeginDayStatsIfNewDay(int day)
        {
            if (day == _dayNumber && _dayNumber != 0) return;
            _dayNumber = day;
        }

        /// <summary>
        /// Gun sonu ozetini dondurur ve istatistikleri sifirlar.
        /// </summary>
        public DaySummary EndDay(int day)
        {
            var summary = new DaySummary(day, _dayDelivered, _daySuccessful,
                                         _dayLate, _dayFailed, _dayMoney,
                                         _dayXp, _level, _dayLevelUps);

            _dayDelivered = 0;
            _daySuccessful = 0;
            _dayLate = 0;
            _dayFailed = 0;
            _dayMoney = 0;
            _dayXp = 0;
            _dayLevelUps = 0;

            return summary;
        }

        /// <summary>Kayittan yukleme (Aşama 7). Dogrudan atar, seviye yukselmez.</summary>
        public void RestoreFromSave(int money, int xp, int level)
        {
            _money = System.Math.Max(0, money);
            _xp = System.Math.Max(0, xp);
            _level = System.Math.Clamp(level, 1, _config.maxLevel);
        }
    }
}
