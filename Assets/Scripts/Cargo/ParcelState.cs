namespace KargoSim.Cargo
{
    /// <summary>
    /// KARGO DURUM MAKINESI - sifirdan (kargoMekanigi.md #62, #63).
    ///
    /// ALTIN KURAL: gecersiz gecis YOK. Ozellikle
    ///   Delivered -> Held / Loaded / Available   yasak
    ///   Loaded   -> Packaging                    yasak
    /// Gecisler `CanGoTo` tarafindan TEK YERDEN kontrol edilir; boylece
    /// UI, arac veya teslimat sistemi kendi kuralini uygulayamaz
    /// ("state degisiminin tek sahibi" - #64).
    ///
    /// KAYIT UYUMLULUGU (#140):
    /// SaveSystem durumu `(int)ParcelState` olarak duz sayi yaziyor.
    /// Eski enum'da WaitingInDepot=0, LoadedOnVehicle=1, Delivered=2,
    /// Discarded=3 idi. Yeni degerler AYNILARA eslendi; boylece eski
    /// kayitlar oyuncu ilerlemesini kaybetmeden okunur. "Discarded"
    /// yerine `Cancelled` geldi ama 3'te kaldi - ayni anlam.
    /// </summary>
    public enum ParcelState
    {
        /// <summary>Depoda, oyuncunun almasina hazir.</summary>
        WaitingInDepot = 0,

        /// <summary>Arac kargo alaninda. Fiziksel kutu kasada gorunur.</summary>
        LoadedOnVehicle = 1,

        /// <summary>Teslim edildi. Dunya nesnesi ANCAK simdi yok olur.</summary>
        Delivered = 2,

        /// <summary>Kalici olarak iptal (eski "Discarded").</summary>
        Cancelled = 3,

        /// <summary>
        /// Oyuncunun ELINDE. Ayni anda EN FAZLA 1 kargo (#10).
        /// </summary>
        Carried = 4,

        /// <summary>Paketleme masasinda.</summary>
        OnPackingTable = 5,

        /// <summary>Paketlenmis, teslimata hazir.</summary>
        Ready = 6,

        /// <summary>Dunyada bir yerde duruyor (bırakilmis / yerde).</summary>
        World = 7,

        /// <summary>Dogru kapinin onunde, teslim icin hazir.</summary>
        AtDelivery = 8,

        /// <summary>Paketleme suruyor.</summary>
        Packaging = 9,

        /// <summary>Gun sonu depoya iade edildi.</summary>
        ReturnedToDepot = 10,
    }

    /// <summary>
    /// Durum gecis kurallari - TEK dogruluk kaynagi.
    ///
    /// ESKI SISTEMDE gecisler `Parcel.MarkX()` metodlarina dagilmis,
    /// cagrilan her sistem kendi kuralini uyguluyordu; bu yuzden
    /// "teslim edilmis kargo tekrar yuklenebildi" gibi hatalar mumkun
    /// oldu. Simdi burada tek tablo var.
    /// </summary>
    public static class ParcelStateRules
    {
        /// <summary>
        /// `from` durumundan `to` durumuna GECILIR MI?
        /// Gecersizse false doner ve gecis yapilmaz.
        /// </summary>
        public static bool CanGoTo(ParcelState from, ParcelState to)
        {
            // Ayni durum: gecis degil, gecerli (idempotent).
            if (from == to) return true;

            // TESLIM EDILMIS KARGO OLARAK KALMAZ (#61 duplicate delivery)
            if (from == ParcelState.Delivered) return false;
            if (from == ParcelState.Cancelled) return false;

            switch (to)
            {
                case ParcelState.Carried:
                    // ElDE: depodan, dunyadan, kasadan, masadan alinabilir.
                    return from == ParcelState.WaitingInDepot
                        || from == ParcelState.World
                        || from == ParcelState.LoadedOnVehicle
                        || from == ParcelState.OnPackingTable
                        || from == ParcelState.Ready
                        || from == ParcelState.ReturnedToDepot
                        || from == ParcelState.AtDelivery;

                case ParcelState.World:
                    // G birakma. Elden dunya yuzeyine.
                    return from == ParcelState.Carried;

                case ParcelState.LoadedOnVehicle:
                    // E ile araca koyma. Dunya durumundan DOGRUDAN olmaz:
                    // once ele alinir, sonra araca konur (#63).
                    return from == ParcelState.Carried;

                case ParcelState.OnPackingTable:
                    return from == ParcelState.Carried || from == ParcelState.World;

                case ParcelState.Packaging:
                    // YALNIZCA masadan. Loaded -> Packaging YASAK.
                    return from == ParcelState.OnPackingTable;

                case ParcelState.Ready:
                    return from == ParcelState.Packaging
                        || from == ParcelState.WaitingInDepot
                        || from == ParcelState.Carried
                        || from == ParcelState.OnPackingTable;

                case ParcelState.AtDelivery:
                    return from == ParcelState.Carried;

                case ParcelState.WaitingInDepot:
                    return from == ParcelState.ReturnedToDepot;

                case ParcelState.ReturnedToDepot:
                    return from == ParcelState.LoadedOnVehicle
                        || from == ParcelState.World
                        || from == ParcelState.Carried;

                case ParcelState.Delivered:
                    // Yalnizca elde, dogru kapida ve dogrulanmis koddan sonra.
                    return from == ParcelState.Carried || from == ParcelState.AtDelivery;

                case ParcelState.Cancelled:
                    return true;   // gun sonu temizligi

                default:
                    return false;
            }
        }

        /// <summary>Bu durumda kutu DUNYADA FIZIKSEL olarak duruyor mu?</summary>
        public static bool HasWorldObject(ParcelState s)
        {
            return s == ParcelState.WaitingInDepot
                || s == ParcelState.World
                || s == ParcelState.LoadedOnVehicle
                || s == ParcelState.OnPackingTable
                || s == ParcelState.Packaging
                || s == ParcelState.ReturnedToDepot;
        }

        /// <summary>Bu durumda kutu oyuncunun kontrolunde mi?</summary>
        public static bool IsUnderPlayerControl(ParcelState s)
        {
            return s == ParcelState.Carried || s == ParcelState.AtDelivery;
        }

        /// <summary>Bu durumda kutu aracla birlikte mi hareket eder?</summary>
        public static bool IsVehicleRelative(ParcelState s)
        {
            return s == ParcelState.LoadedOnVehicle;
        }

        /// <summary>Oyuncu bu kutuyu aracina koyabilir mi?</summary>
        public static bool CanLoadToVehicle(ParcelState s)
        {
            return s == ParcelState.Carried;
        }

        /// <summary>Oyuncu bu kutuyu aracindan alabilir mi?</summary>
        public static bool CanUnloadFromVehicle(ParcelState s)
        {
            return s == ParcelState.LoadedOnVehicle;
        }
    }
}
