using KargoSim.Core;
using NUnit.Framework;
using UnityEngine;

namespace KargoSim.Tests
{
    /// <summary>
    /// Asama 1'in dogrulama testleri. GameClock saf C# oldugu icin
    /// Play moduna girmeden, saniyeler icinde calisir.
    /// </summary>
    public sealed class GameClockTests
    {
        private GameConfig _config;
        private GameClock _clock;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<GameConfig>();
            _config.dayLengthSeconds = 300f;   // 5 dakikalik gun
            _config.startHour = 8f;
            _clock = new GameClock(_config);
        }

        [TearDown]
        public void TearDown()
        {
            if (_config != null) Object.DestroyImmediate(_config);
        }

        [Test]
        public void BaslangicSaatVeGunDogru()
        {
            Assert.AreEqual(1, _clock.Day);
            Assert.AreEqual(8f, _clock.HourOfDay, 0.001f);
            Assert.IsFalse(_clock.IsPaused);
        }

        [Test]
        public void Tick_SaatIlerletir()
        {
            // 300 saniyelik gun -> 24 saat / 300 sn = 0.08 saat/sn
            _clock.Tick(10f);

            Assert.AreEqual(8.8f, _clock.HourOfDay, 0.01f);
        }

        [Test]
        public void Tick_GunSonundaGunDegisir()
        {
            // 8:00'ten 24:00'a kalan 16 saat = 16 / 0.08 = 200 saniye
            _clock.Tick(200f);

            Assert.AreEqual(2, _clock.Day, "Saat 24:00 olunca gun 2'ye donmeli.");
            Assert.AreEqual(0f, _clock.HourOfDay, 0.01f);
        }

        [Test]
        public void Duraklatma_ZamaniDurdurur()
        {
            _clock.Tick(50f);
            float before = _clock.HourOfDay;

            _clock.SetPaused(true);
            _clock.Tick(100f);

            Assert.AreEqual(before, _clock.HourOfDay, 0.0001f,
                "Duraklatilmis saatte Tick zaman ilerletmemeli.");
        }

        [Test]
        public void GunBittiVeGunBasladiEventleriFirlatilir()
        {
            int endedCount = 0;
            int startedCount = 0;
            int startedDay = -1;

            System.Action<int> onEnded = _ => endedCount++;
            System.Action<int> onStarted = d => { startedCount++; startedDay = d; };

            GameEvents.DayEnded += onEnded;
            GameEvents.DayStarted += onStarted;
            try
            {
                _clock.Tick(200f);
            }
            finally
            {
                GameEvents.DayEnded -= onEnded;
                GameEvents.DayStarted -= onStarted;
            }

            Assert.AreEqual(1, endedCount);
            Assert.AreEqual(1, startedCount);
            Assert.AreEqual(2, startedDay);
        }

        [Test]
        public void GunIlerlemesiGunBasindaSifirdir()
        {
            Assert.AreEqual(8f / 24f, _clock.DayProgress, 0.001f);
        }

        [Test]
        public void KalanSureGunSonunaKadarAzalir()
        {
            float before = _clock.SecondsUntilDayEnd;
            _clock.Tick(30f);
            float after = _clock.SecondsUntilDayEnd;

            Assert.Less(after, before);
            Assert.AreEqual(before - 30f, after, 0.5f);
        }

        [Test]
        public void SaatBicimlendirmeIkiHanelidir()
        {
            _clock.SetHour(9.5f);
            Assert.AreEqual("09:30", _clock.FormatTime());

            _clock.SetHour(14f);
            Assert.AreEqual("14:00", _clock.FormatTime());
        }

        [Test]
        public void NegatifTickZamanIlerletmez()
        {
            float before = _clock.HourOfDay;
            _clock.Tick(-5f);
            Assert.AreEqual(before, _clock.HourOfDay, 0.0001f);
        }
    }
}
