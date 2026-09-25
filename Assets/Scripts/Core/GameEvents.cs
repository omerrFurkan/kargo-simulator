using System;
using UnityEngine;

namespace KargoSim.Core
{
    /// <summary>
    /// Sistemler arasi tek haberlesme kanali.
    ///
    /// KURAL: hicbir sistem baska bir sistemi referanslamaz; yazmak icin bu
    /// sinifa abone olur, okumak icin cift baglanir. Boylece bir sistem
    /// silindiginde veya degistirildiginde digerleri bozulmaz.
    ///
    /// Performans notu: C# event + null-conditional invoke, cagri yapilmadiginda
    /// sifir allocation uretir. Ayrica Update() icinde bu siniftan okuma yapmayin.
    /// </summary>
    public static class GameEvents
    {
        /// <summary>Yeni gun basladi. Parametre: gun numarasi (1'den baslar).</summary>
        public static event Action<int> DayStarted;

        /// <summary>Gun bitti. Parametre: gun numarasi.</summary>
        public static event Action<int> DayEnded;

        /// <summary>
        /// Gun ilerlemesi degisti. Parametreler: (0..1 ilerleme, saat 0-24).
        /// Her saniye degil, yalnizca deger gercekten degistiginde tetiklenir.
        /// </summary>
        public static event Action<float, float> DayProgressChanged;

        /// <summary>Saat duraklatildi / devam ediyor. Parametre: duraklatildi mi?</summary>
        public static event Action<bool> ClockPauseChanged;

        public static void RaiseDayStarted(int day) => DayStarted?.Invoke(day);

        public static void RaiseDayEnded(int day) => DayEnded?.Invoke(day);

        public static void RaiseDayProgressChanged(float progress, float hourOfDay) =>
            DayProgressChanged?.Invoke(progress, hourOfDay);

        public static void RaiseClockPauseChanged(bool paused) => ClockPauseChanged?.Invoke(paused);

        /// <summary>
        /// Editor ayarlarinda "Domain Reload: Disabled" kullanildigi icin statik
        /// event'ler Play'den cikarken temizlenmez. Play'e girildiginde hepsini
        /// sifirlamazsak eski (ölü) dinleyicilerle dolu bir bus ile calisiriz.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            DayStarted = null;
            DayEnded = null;
            DayProgressChanged = null;
            ClockPauseChanged = null;
        }
    }
}
