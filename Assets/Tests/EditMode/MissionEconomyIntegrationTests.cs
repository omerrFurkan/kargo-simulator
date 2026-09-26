using KargoSim.Cargo;
using KargoSim.Delivery;
using KargoSim.Economy;
using KargoSim.Mission;
using KargoSim.Player;
using NUnit.Framework;
using UnityEngine;

namespace KargoSim.Tests
{
    /// <summary>
    /// Gorev odulunun oyuncuya gercekten ulasip ulasmadigini korur.
    ///
    /// Neden ayri dosya: MissionSystem testleri saf gorev mantigini,
    /// EconomyTests odeme hesabini dogrular. Aradaki DIKIS - "gorev bitti"
    /// olayindan sonra para ve XP oyuncunun defterine yaziliyor mu - hicbir
    /// testle korunmuyordu. Oyun orada sessizce odu veya hicbir sey olmadi.
    ///
    /// CompositionRoot.GrantMissionRewards'in kopyasi: dogrudan
    /// PlayerProgress.ApplyPayout uzerinden gider, cunku paranin oyunda
    /// tek girdisi odur.
    /// </summary>
    public sealed class MissionEconomyIntegrationTests
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

        private static MissionSystem SingleDeliveryMission(int money, int xp)
        {
            var defs = new[]
            {
                MissionDefinition.Create(MissionKind.DeliverCount, 1, "1 kargo", money, xp)
            };
            var system = new MissionSystem(defs, null);
            system.BeginDay(1);
            return system;
        }

        /// <summary>
        /// CompositionRoot'taki yolun birebir karsiligi.
        /// </summary>
        private static void ApplyMissionReward(
            MissionSystem missions, PlayerProgress progress, int day)
        {
            int money = missions.TotalRewardMoney;
            int xp = missions.TotalRewardXp;
            if (money <= 0 && xp <= 0) return;

            progress.ApplyPayout(
                new Payout(money, 0, 0, xp), DeliveryOutcome.Success, day);
        }

        [Test]
        public void TamamlananGorev_OduluOyuncuParasinaEklendir()
        {
            var progress = new PlayerProgress(_config);
            int basta = progress.Money;
            int basXp = progress.Xp;

            var missions = SingleDeliveryMission(60, 25);
            missions.Report(MissionKind.DeliverCount, 1, true);

            Assert.IsTrue(missions.IsComplete(0), "Bir teslimat gorevi tamamlanmali.");
            ApplyMissionReward(missions, progress, 1);

            Assert.AreEqual(basta + 60, progress.Money, "Gorev odulu paraya eklenmeli.");
            Assert.AreEqual(basXp + 25, progress.Xp, "Gorev odulu XP'ye eklenmeli.");
        }

        [Test]
        public void TamamlanmayanGorev_ParayiDegistirmez()
        {
            var progress = new PlayerProgress(_config);
            int basta = progress.Money;

            var missions = SingleDeliveryMission(60, 25);
            // Hicbir teslimat raporu yok: gorev bitmedi.
            ApplyMissionReward(missions, progress, 1);

            Assert.AreEqual(basta, progress.Money, "Bitmemis gorev odulu verilmemeli.");
        }

        [Test]
        public void BasarisizTeslimat_GoreviBozarVeOdulVerilmez()
        {
            var progress = new PlayerProgress(_config);
            int basta = progress.Money;

            var defs = new[]
            {
                MissionDefinition.Create(MissionKind.NoFailures, 0, "Hicbir teslimat bozulmasin", 150, 60)
            };
            var missions = new MissionSystem(defs, null);
            missions.BeginDay(1);

            missions.Report(MissionKind.NoFailures, 0, false);
            missions.EndDay();

            Assert.IsFalse(missions.IsComplete(0), "Bozulan gorev tamamlanmis sayilmaz.");
            ApplyMissionReward(missions, progress, 1);
            Assert.AreEqual(basta, progress.Money, "Bozulan gorev icin odul verilmemeli.");
        }

        [Test]
        public void AyniGorevIcinOdulIkiKezVerilmez()
        {
            var progress = new PlayerProgress(_config);
            int basta = progress.Money;

            var missions = SingleDeliveryMission(60, 25);
            missions.Report(MissionKind.DeliverCount, 1, true);
            missions.Report(MissionKind.DeliverCount, 5, true);
            ApplyMissionReward(missions, progress, 1);

            // Odul "tamamlandigi anda" bir kez yazilir; fazladan teslimat
            // (veya gun sonunda ikinci bir cagri) toplami buyutmemeli.
            Assert.AreEqual(basta + 60, progress.Money,
                "Fazladan teslimat odulu ikiye katlamamali.");
        }

        [Test]
        public void YeniGun_GorevOduluSifirdanBaslar()
        {
            var progress = new PlayerProgress(_config);

            var missions = SingleDeliveryMission(60, 25);
            missions.Report(MissionKind.DeliverCount, 1, true);
            ApplyMissionReward(missions, progress, 1);
            int gun1 = progress.Money;

            missions.BeginDay(2);
            Assert.AreEqual(0, missions.TotalRewardMoney, "Yeni gun odul sifirdan baslamali.");

            ApplyMissionReward(missions, progress, 2);
            Assert.AreEqual(gun1, progress.Money,
                "Gun 2'de gorev bitmeden ikinci kez odul verilmemeli.");
        }
    }
}
