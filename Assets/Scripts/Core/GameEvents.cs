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

        // ---- Kargo (Aşama 3) ----

        /// <summary>Yeni gunun kargolari uretildi. Parametre: kargo sayisi.</summary>
        public static event Action<int> ParcelsGenerated;

        /// <summary>Bir kargo araca yuklendi.</summary>
        public static event Action<Cargo.Parcel> ParcelLoaded;

        /// <summary>
        /// Kargo durumu degisti (uretim, yukleme, bosaltma).
        /// UI'nin tek abone olup her seyi dinlemesi icin bir "toplu" olay:
        /// ayri ayri dinlemek yerine buna baglanmak yeterlidir.
        /// </summary>
        public static event Action CargoChanged;

        public static void RaiseDayStarted(int day) => DayStarted?.Invoke(day);

        public static void RaiseDayEnded(int day) => DayEnded?.Invoke(day);

        public static void RaiseDayProgressChanged(float progress, float hourOfDay) =>
            DayProgressChanged?.Invoke(progress, hourOfDay);

        public static void RaiseClockPauseChanged(bool paused) => ClockPauseChanged?.Invoke(paused);

        public static void RaiseParcelsGenerated(int count) => ParcelsGenerated?.Invoke(count);

        public static void RaiseParcelLoaded(Cargo.Parcel parcel) => ParcelLoaded?.Invoke(parcel);

        public static void RaiseCargoChanged() => CargoChanged?.Invoke();

        // ---- Teslimat (Aşama 4) ----

        /// <summary>Arac bir teslimat duragina vardi. UI burada yonlendirir.</summary>
        public static event Action<Delivery.DeliveryStop> DeliveryStopArrived;

        /// <summary>Bir kargo teslim edildi (basarili, gec veya basarisiz).</summary>
        public static event Action<Cargo.Parcel, Delivery.DeliveryOutcome> ParcelDelivered;

        /// <summary>Rota yeniden siralandi (kargo yuklendi veya teslim edildi).</summary>
        public static event Action RouteChanged;

        public static void RaiseDeliveryStopArrived(Delivery.DeliveryStop stop) =>
            DeliveryStopArrived?.Invoke(stop);

        public static void RaiseParcelDelivered(Cargo.Parcel parcel,
                                                Delivery.DeliveryOutcome outcome) =>
            ParcelDelivered?.Invoke(parcel, outcome);

        public static void RaiseRouteChanged() => RouteChanged?.Invoke();

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
            ParcelsGenerated = null;
            ParcelLoaded = null;
            CargoChanged = null;
            DeliveryStopArrived = null;
            ParcelDelivered = null;
            RouteChanged = null;
        }
    }
}
