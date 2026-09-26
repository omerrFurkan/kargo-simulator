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

        // ---- Ekonomi / ilerleme (Aşama 5) ----

        /// <summary>Bir teslimin odemesi uygulandi.</summary>
        public static event Action<Economy.Payout, Delivery.DeliveryOutcome> PayoutApplied;

        /// <summary>Yeni seviye.</summary>
        public static event Action<int> LevelUp;

        /// <summary>Gun sonu ozeti hazir.</summary>
        public static event Action<Player.DaySummary> DaySummaryReady;

        // ---- Paketleme (Asama 12) ----

        /// <summary>Kargo paketleme masasina kondu.</summary>
        public static event Action<Cargo.Parcel> PackagingStarted;

        /// <summary>Kargo masadan bosaltildi (ilerleme KORUNUR).</summary>
        public static event Action<Cargo.Parcel> PackagingAbandoned;

        /// <summary>
        /// Bir paketleme adimi bitti.
        /// Parametreler: (kargo, biten adim, yeni adim).
        /// </summary>
        public static event Action<Cargo.Parcel, Cargo.PackagingStep, Cargo.PackagingStep>
            PackagingStepCompleted;

        /// <summary>Kargo tamamen paketlendi ve teslimata hazir.</summary>
        public static event Action<Cargo.Parcel> ParcelPackaged;

        public static void RaisePackagingStarted(Cargo.Parcel parcel) =>
            PackagingStarted?.Invoke(parcel);

        public static void RaisePackagingAbandoned(Cargo.Parcel parcel) =>
            PackagingAbandoned?.Invoke(parcel);

        public static void RaisePackagingStepCompleted(Cargo.Parcel parcel,
                                                      Cargo.PackagingStep completed,
                                                      Cargo.PackagingStep current) =>
            PackagingStepCompleted?.Invoke(parcel, completed, current);

        public static void RaiseParcelPackaged(Cargo.Parcel parcel) =>
            ParcelPackaged?.Invoke(parcel);

        // ---- NPC / teslimat kodu (Asama 13) ----

        /// <summary>
        /// Bir adresin kapisi calindi. Parametre: adres kimligi.
        ///
        /// NOT: teslim YAPILMADIGI icin ParcelDelivered YAYINLANMAZ. Bu
        /// ayrim kritik: ekonomi ParcelDelivered dinler, bu yuzden kapı
        /// calmak odeme tetiklemez.
        /// </summary>
        public static event Action<int> DoorKnocked;

        /// <summary>
        /// NPC kapidi acti ve teslimat kodu soruluyor.
        /// Parametreler: (adres kimligi, musteri adi, beklenen kod).
        /// </summary>
        public static event Action<int, string, int> NPCAnswered;

        /// <summary>Musteri evde degildi. Parametre: adres kimligi.</summary>
        public static event Action<int> CustomerNotHome;

        /// <summary>
        /// Oyuncu bir teslimat kodu girdi. Parametreler:
        /// (kargo, girilen metin, dogrumu).
        ///
        /// HATA DUYUSU: bu olay HER giris icin yayilir. Basarisiz
        /// girisler de sayilmali; aksi halde oyuncu dogru kodu bulana
        /// kadar deneme sayisi gorunmez.
        /// </summary>
        public static event Action<Cargo.Parcel, string, bool> DeliveryCodeSubmitted;

        /// <summary>Kod dogrulandi. Parametre: teslim edilen kargo.</summary>
        public static event Action<Cargo.Parcel> DeliveryCodeCorrect;

        /// <summary>
        /// Kod yanlis. Parametre: reddedilen kargo.
        ///
        /// Kargo ELDE KALIR; teslim edilmez. Ceza bu olaydan uygulanir.
        /// </summary>
        public static event Action<Cargo.Parcel> DeliveryCodeWrong;

        public static void RaiseDoorKnocked(int destinationId) =>
            DoorKnocked?.Invoke(destinationId);

        public static void RaiseNPCAnswered(int destinationId, string name, int code) =>
            NPCAnswered?.Invoke(destinationId, name, code);

        public static void RaiseCustomerNotHome(int destinationId) =>
            CustomerNotHome?.Invoke(destinationId);

        public static void RaiseDeliveryCodeSubmitted(Cargo.Parcel parcel, string input, bool correct) =>
            DeliveryCodeSubmitted?.Invoke(parcel, input, correct);

        public static void RaiseDeliveryCodeCorrect(Cargo.Parcel parcel) =>
            DeliveryCodeCorrect?.Invoke(parcel);

        public static void RaiseDeliveryCodeWrong(Cargo.Parcel parcel) =>
            DeliveryCodeWrong?.Invoke(parcel);

        public static void RaisePayoutApplied(Economy.Payout payout,
                                              Delivery.DeliveryOutcome outcome) =>
            PayoutApplied?.Invoke(payout, outcome);

        public static void RaiseLevelUp(int level) => LevelUp?.Invoke(level);

        public static void RaiseDaySummaryReady(Player.DaySummary summary) =>
            DaySummaryReady?.Invoke(summary);

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
            PayoutApplied = null;
            LevelUp = null;
            DaySummaryReady = null;
            PackagingStarted = null;
            PackagingAbandoned = null;
            PackagingStepCompleted = null;
            ParcelPackaged = null;
            DoorKnocked = null;
            NPCAnswered = null;
            CustomerNotHome = null;
            DeliveryCodeSubmitted = null;
            DeliveryCodeCorrect = null;
            DeliveryCodeWrong = null;
        }
    }
}
