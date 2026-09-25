using KargoSim.Cargo;
using KargoSim.Delivery;
using KargoSim.Economy;
using KargoSim.Player;
using NUnit.Framework;
using UnityEngine;

namespace KargoSim.Tests
{
    /// <summary>
    /// Aşama 5: para, XP, seviye. Hepsi saf mantık — sahne veya motor yok.
    /// </summary>
    public sealed class EconomyTests
    {
        private EconomyConfig _config;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<EconomyConfig>();
            _config.startingMoney = 250;
            _config.earlyBonusRatio = 0.25f;
            _config.earlyBonusThreshold = 0.2f;
            _config.failedDeliveryPenalty = 0;
            _config.xpPerDelivery = 12;
            _config.xpPerKg = 1.5f;
            _config.levelUpBaseCost = 100;
            _config.levelUpCostGrowth = 1.35f;
            _config.maxLevel = 20;
        }

        [TearDown]
        public void TearDown()
        {
            if (_config != null) Object.DestroyImmediate(_config);
        }

        // ------------------------------------------------------------------
        // PayoutCalculator - odeme
        // ------------------------------------------------------------------

        [Test]
        public void Odeme_ZamanindaTamKargoDegeriniVerir()
        {
            Payout p = PayoutCalculator.Calculate(100, 5f, DeliveryOutcome.Success, 1f, _config);
            Assert.AreEqual(100, p.Payment, "Zamaninda teslim tam deger almali.");
        }

        [Test]
        public void Odeme_BahsisKalanSureyleOlceklenir()
        {
            // %100 sure kaldi: tam bahsis (100 * 0.25 = 25)
            Payout full = PayoutCalculator.Calculate(100, 5f, DeliveryOutcome.Success, 1f, _config);
            Assert.AreEqual(25, full.Bonus, 0, "Tam kalan sürede tam bahşiş.");

            // %50 kaldi: 100 * 0.25 * 0.5 = 12.5 -> yuvarlanir 13
            Payout half = PayoutCalculator.Calculate(100, 5f, DeliveryOutcome.Success, 0.5f, _config);
            Assert.AreEqual(13, half.Bonus, 0, "Bahşiş kalan süreyle ölçeklenmeli.");

            Assert.Less(half.TotalMoney, full.TotalMoney,
                "Hızlı teslim daha çok kazandırmalı.");
        }

        [Test]
        public void Odeme_EskiKargoyaBahsisVerilmez()
        {
            Payout p = PayoutCalculator.Calculate(100, 5f, DeliveryOutcome.Success, 0.05f, _config);
            Assert.AreEqual(0, p.Bonus, 0,
                "Esik altinda kalan surede bahsis verilmemeli (esik " +
                _config.earlyBonusThreshold + ").");
        }

        [Test]
        public void Odeme_GecKaldigindaKismiODenir()
        {
            Payout p = PayoutCalculator.Calculate(100, 5f, DeliveryOutcome.Late, 0f, _config);
            Assert.AreEqual(40, p.Payment, 0, "Geç teslimtam deger almamali (%40).");
            Assert.AreEqual(0, p.Bonus, 0, "Geç teslimde bahsis olmaz.");
        }

        [Test]
        public void Odeme_BasarisizdaOdemeYoktur()
        {
            Payout p = PayoutCalculator.Calculate(100, 5f, DeliveryOutcome.Failed, 0f, _config);
            Assert.AreEqual(0, p.Payment, "Başarısız teslimde ödeme olmamalı.");
            Assert.AreEqual(0, p.Bonus, 0);
            Assert.AreEqual(0, p.Xp, "Başarısız teslimde XP olmamalı.");
        }

        [Test]
        public void Odeme_BasarisizCezasiVarsaUygulanir()
        {
            _config.failedDeliveryPenalty = 50;
            Payout p = PayoutCalculator.Calculate(100, 5f, DeliveryOutcome.Failed, 0f, _config);
            Assert.AreEqual(50, p.Penalty, "Cezasi yapilandirildiysa uygulanmali.");
            Assert.AreEqual(-50, p.TotalMoney, 0, "Toplam para cezayi yansitmali.");
        }

        [Test]
        public void Odeme_AgirKargoDahaCokXPVerir()
        {
            Payout light = PayoutCalculator.Calculate(100, 1f, DeliveryOutcome.Success, 0f, _config);
            Payout heavy = PayoutCalculator.Calculate(100, 20f, DeliveryOutcome.Success, 0f, _config);

            Assert.AreEqual(14, light.Xp, 0, "12 taban + 1*1.5 = 13.5 -> 14");
            Assert.AreEqual(42, heavy.Xp, 0, "12 taban + 20*1.5 = 42");
            Assert.Greater(heavy.Xp, light.Xp, "Ağır kargo daha fazla XP vermeli.");
        }

        [Test]
        public void Odeme_SonucCarpaniXPyeUygulanir()
        {
            Payout success = PayoutCalculator.Calculate(100, 5f, DeliveryOutcome.Success, 0f, _config);
            Payout late = PayoutCalculator.Calculate(100, 5f, DeliveryOutcome.Late, 0f, _config);

            Assert.Less(late.Xp, success.Xp, "Geç teslimde XP daha az olmalı.");
        }

        [Test]
        public void Odeme_AsiriDegerlerGuvenliIslenir()
        {
            // Negatif deger ve asimi oran bozmasin.
            Payout neg = PayoutCalculator.Calculate(-50, -5f, DeliveryOutcome.Success, -1f, _config);
            Assert.GreaterOrEqual(neg.TotalMoney, 0, "Negatif girdi sifir vermeli.");

            Payout over = PayoutCalculator.Calculate(100, 5f, DeliveryOutcome.Success, 5f, _config);
            Payout at1 = PayoutCalculator.Calculate(100, 5f, DeliveryOutcome.Success, 1f, _config);
            Assert.AreEqual(at1.Bonus, over.Bonus, 0, "1'i aşan oran 1 gibi ele alınmalı.");
        }

        [Test]
        public void Odeme_YuvarlamaTutarli()
        {
            // 12 + 1*1.5 = 13.5 -> bankacı yuvarlamasi 14
            Payout p = PayoutCalculator.Calculate(0, 1f, DeliveryOutcome.Success, 0f, _config);
            Assert.AreEqual(14, p.Xp, "Yuvarlama tek kural olmali (.5 yukarilir).");
        }

        [Test]
        public void Odeme_AyarsizEkonomiHataVerir()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => PayoutCalculator.Calculate(100, 1f, DeliveryOutcome.Success, 1f, null));
        }

        // ------------------------------------------------------------------
        // KalanSureOrani
        // ------------------------------------------------------------------

        [Test]
        public void SureOrani_YuklenmedenOnceTam()
        {
            var type = ScriptableObject.CreateInstance<ParcelType>();
            var parcel = new Parcel(1, type, "A", 0, 1f, 1f, ParcelPriority.Normal, 100f, 50);
            try
            {
                Assert.AreEqual(1f, PayoutCalculator.RemainingRatio(parcel, 500f), 0.001f,
                    "Yüklenmeden önce süre başlamadı, oran tam olmalı.");
            }
            finally
            {
                Object.DestroyImmediate(type);
            }
        }

        [Test]
        public void SureOrani_ZamanlaAzalir()
        {
            var type = ScriptableObject.CreateInstance<ParcelType>();
            var parcel = new Parcel(1, type, "A", 0, 1f, 1f, ParcelPriority.Normal, 100f, 50);
            try
            {
                parcel.MarkLoaded(0f);
                Assert.AreEqual(1f, PayoutCalculator.RemainingRatio(parcel, 0f), 0.001f);
                Assert.AreEqual(0.5f, PayoutCalculator.RemainingRatio(parcel, 50f), 0.001f);
                Assert.AreEqual(0f, PayoutCalculator.RemainingRatio(parcel, 100f), 0.001f);
            }
            finally
            {
                Object.DestroyImmediate(type);
            }
        }

        // ------------------------------------------------------------------
        // Seviye egri
        // ------------------------------------------------------------------

        [Test]
        public void SeviyeMaliyeti_IlkSeviyedeTabanDeger()
        {
            Assert.AreEqual(100, _config.XpCostForLevel(2), 0,
                "2. seviyeye geçiş taban maliyetle olmalı.");
        }

        [Test]
        public void SeviyeMaliyeti_AtlamaMaliyetleriHerSeviyedeArtar()
        {
            // Regresyon testi: egri "toplam"a egilseydi ilk atlama (100 XP)
            // ikincisinden (35 XP) pahali olurdu. Oyuncu ilk seviyede
            // takilir, sonra hizla yukselirdi.
            int prevStep = _config.XpCostForLevel(2);   // 1 -> 2 atlama maliyeti
            for (int level = 3; level <= 8; level++)
            {
                int total = _config.XpCostForLevel(level);
                int step = total - _config.XpCostForLevel(level - 1);

                Assert.Greater(step, prevStep,
                    level + ". seviyeye atlama maliyeti oncekinden buyuk olmali " +
                    "(onceki " + prevStep + ", simdi " + step + ").");

                prevStep = step;
            }
        }

        [Test]
        public void SeviyeMaliyeti_ArtarakKarmasik()
        {
            int l2 = _config.XpCostForLevel(2);
            int l3 = _config.XpCostForLevel(3);
            int l5 = _config.XpCostForLevel(5);

            Assert.Greater(l3, l2, "3. seviye 2'den pahalı olmalı.");
            Assert.Greater(l5, l3, "Eğri artmalı.");
        }

        [Test]
        public void SeviyeMaliyeti_AzamiSeviyedeSinirli()
        {
            int cost = _config.XpCostForLevel(999);
            int maxCost = _config.XpCostForLevel(_config.maxLevel);
            Assert.AreEqual(maxCost, cost, 0, "Azami seviye dışına taşmamalı.");
        }

        // ------------------------------------------------------------------
        // PlayerProgress
        // ------------------------------------------------------------------

        [Test]
        public void Baslangic_BakiyeAyarDegeridir()
        {
            var progress = new PlayerProgress(_config);
            Assert.AreEqual(250, progress.Money);
            Assert.AreEqual(0, progress.Xp);
            Assert.AreEqual(1, progress.Level);
        }

        [Test]
        public void Odeme_ParayiVeXPYiUygular()
        {
            var progress = new PlayerProgress(_config);
            int before = progress.Money;

            progress.ApplyPayout(new Payout(100, 25, 0, 14), DeliveryOutcome.Success, 1);

            Assert.AreEqual(before + 125, progress.Money, "Ödeme + bahşiş eklenmeli.");
            Assert.AreEqual(14, progress.Xp);
        }

        [Test]
        public void Odeme_SeviyeAtlar()
        {
            var progress = new PlayerProgress(_config);
            int levelUps = 0;
            System.Action<int> handler = l => levelUps++;
            Core.GameEvents.LevelUp += handler;

            try
            {
                progress.ApplyPayout(new Payout(0, 0, 0, 150), DeliveryOutcome.Success, 1);
                Assert.AreEqual(2, progress.Level, "150 XP ile 2. seviyeye atlanmalı.");
                Assert.AreEqual(1, levelUps, "Seviye atlama olayı bir kez yayılmalı.");
            }
            finally
            {
                Core.GameEvents.LevelUp -= handler;
            }
        }

        [Test]
        public void Odeme_BirdenFazlaSeviyeAtlayabilir()
        {
            var progress = new PlayerProgress(_config);
            // 100 -> 2 (135 -> 3 -> 182 -> 4 ...)
            progress.ApplyPayout(new Payout(0, 0, 0, 1000), DeliveryOutcome.Success, 1);
            Assert.Greater(progress.Level, 2, "Çok bükük XP birden fazla seviye atlatmalı.");
        }

        [Test]
        public void Odeme_AzamiSeviyedeDurur()
        {
            _config.maxLevel = 3;
            var progress = new PlayerProgress(_config);
            progress.ApplyPayout(new Payout(0, 0, 0, 999999), DeliveryOutcome.Success, 1);

            Assert.AreEqual(3, progress.Level, "Azami seviye aşılmamalı.");
            Assert.IsTrue(progress.IsMaxLevel);
            Assert.AreEqual(0, progress.XpToNextLevel, "Azami seviyede kalan XP yok.");
        }

        [Test]
        public void Odeme_BakiyeNegatifeDusmez()
        {
            _config.failedDeliveryPenalty = 9999;
            var progress = new PlayerProgress(_config);
            progress.ApplyPayout(new Payout(0, 0, 9999, 0), DeliveryOutcome.Failed, 1);

            Assert.AreEqual(0, progress.Money, "Bakiye negatife düşmemeli.");
        }

        [Test]
        public void SeviyeCubugu_AralikDisindaDegil()
        {
            var progress = new PlayerProgress(_config);
            Assert.GreaterOrEqual(progress.LevelProgress, 0f);
            Assert.LessOrEqual(progress.LevelProgress, 1f);

            progress.ApplyPayout(new Payout(0, 0, 0, 150), DeliveryOutcome.Success, 1);
            Assert.GreaterOrEqual(progress.LevelProgress, 0f);
            Assert.LessOrEqual(progress.LevelProgress, 1f);
        }

        [Test]
        public void GunOzeti_IstatistikleriToplar()
        {
            var progress = new PlayerProgress(_config);
            progress.ApplyPayout(new Payout(100, 0, 0, 10), DeliveryOutcome.Success, 1);
            progress.ApplyPayout(new Payout(40, 0, 0, 4), DeliveryOutcome.Late, 1);
            progress.ApplyPayout(new Payout(0, 0, 0, 0), DeliveryOutcome.Failed, 1);

            DaySummary summary = progress.EndDay(1);

            Assert.AreEqual(3, summary.Delivered);
            Assert.AreEqual(1, summary.Successful);
            Assert.AreEqual(1, summary.Late);
            Assert.AreEqual(1, summary.Failed);
            Assert.AreEqual(140, summary.MoneyEarned);
            Assert.AreEqual(14, summary.XpEarned);
            Assert.IsFalse(summary.IsEmpty);
        }

        [Test]
        public void GunOzeti_SonrasiIstatistiklerSifirlanir()
        {
            var progress = new PlayerProgress(_config);
            progress.ApplyPayout(new Payout(100, 0, 0, 10), DeliveryOutcome.Success, 1);
            progress.EndDay(1);

            DaySummary next = progress.EndDay(1);
            Assert.AreEqual(0, next.Delivered, "Gün özeti istatistikleri sıfırlamalı.");
            Assert.IsTrue(next.IsEmpty);
        }

        [Test]
        public void GunOzeti_BakiyeVeSeviyeKorunur()
        {
            var progress = new PlayerProgress(_config);
            progress.ApplyPayout(new Payout(100, 0, 0, 150), DeliveryOutcome.Success, 1);
            int money = progress.Money;
            int level = progress.Level;

            progress.EndDay(1);

            Assert.AreEqual(money, progress.Money, "Gün sonu bakiyeyi silmemeli.");
            Assert.AreEqual(level, progress.Level, "Gün sonu seviyeyi silmemeli.");
        }

        [Test]
        public void KayitYukleme_ParayiVeSeviyeyiGeriGetirir()
        {
            var progress = new PlayerProgress(_config);
            progress.RestoreFromSave(1000, 500, 5);

            Assert.AreEqual(1000, progress.Money);
            Assert.AreEqual(500, progress.Xp);
            Assert.AreEqual(5, progress.Level);
        }

        [Test]
        public void KayitYukleme_GeçersizDegerleriOnarir()
        {
            var progress = new PlayerProgress(_config);
            progress.RestoreFromSave(-50, -10, 999);

            Assert.AreEqual(0, progress.Money, "Negatif bakiye onarılmalı.");
            Assert.AreEqual(0, progress.Xp, "Negatif XP onarılmalı.");
            Assert.AreEqual(_config.maxLevel, progress.Level, "Seviye sınıra kırpılmalı.");
        }
    }
}
