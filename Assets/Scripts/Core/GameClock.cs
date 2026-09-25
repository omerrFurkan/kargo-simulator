using UnityEngine;

namespace KargoSim.Core
{
    /// <summary>
    /// Oyun ici zaman ve gun sayaci.
    ///
    /// Bilerek MonoBehaviour DEGIL: mantik test edilebilir olsun diye saf C# sinifi
    /// olarak yazildi. Zaman ilerletme sorumlulugu GameCompositionRoot'ta
    /// (tek Update cagrisi) - boylece her sistem kendi Update'ini acmaz.
    /// </summary>
    public sealed class GameClock
    {
        public const float HoursPerDay = 24f;

        private readonly GameConfig _config;
        private float _lastBroadcastProgress = -1f;
        private float _lastBroadcastHour = -1f;

        public int Day { get; private set; }
        public float HourOfDay { get; private set; }
        public bool IsPaused { get; private set; }

        /// <summary>0 = sabah, 1 = bir sonraki sabah.</summary>
        public float DayProgress => HourOfDay / HoursPerDay;

        /// <summary>Kalan gercek saniye (gun sonuna).</summary>
        public float SecondsUntilDayEnd =>
            _config.dayLengthSeconds * (1f - DayProgress);

        public GameClock(GameConfig config)
        {
            _config = config;
            Day = 1;
            HourOfDay = config.startHour;
        }

        public void Tick(float deltaTime)
        {
            if (IsPaused || deltaTime <= 0f) return;

            float hoursPerSecond = HoursPerDay / _config.dayLengthSeconds;
            HourOfDay += deltaTime * hoursPerSecond;

            if (HourOfDay >= HoursPerDay)
            {
                HourOfDay -= HoursPerDay;
                GameEvents.RaiseDayEnded(Day);
                Day++;
                GameEvents.RaiseDayStarted(Day);
                _lastBroadcastProgress = -1f;
                _lastBroadcastHour = -1f;
            }

            BroadcastProgressIfChanged();
        }

        public void SetPaused(bool paused)
        {
            if (IsPaused == paused) return;
            IsPaused = paused;
            GameEvents.RaiseClockPauseChanged(paused);
        }

        public void TogglePause() => SetPaused(!IsPaused);

        /// <summary>Test ve hata ayiklama icin: saati dogrudan atar.</summary>
        public void SetHour(float hour)
        {
            HourOfDay = Mathf.Repeat(hour, HoursPerDay);
            BroadcastProgressIfChanged();
        }

        public string FormatTime()
        {
            int h = Mathf.FloorToInt(HourOfDay);
            int m = Mathf.FloorToInt((HourOfDay - h) * 60f);
            return string.Format("{0:00}:{1:00}", h, m);
        }

        private void BroadcastProgressIfChanged()
        {
            // UI her frame guncellemesin: sadece yuvarlanmis deger degistiginde gonder.
            float hour = Mathf.Round(HourOfDay * 100f) / 100f;
            if (Mathf.Approximately(hour, _lastBroadcastHour)) return;

            _lastBroadcastHour = hour;
            _lastBroadcastProgress = DayProgress;
            GameEvents.RaiseDayProgressChanged(_lastBroadcastProgress, HourOfDay);
        }
    }
}
