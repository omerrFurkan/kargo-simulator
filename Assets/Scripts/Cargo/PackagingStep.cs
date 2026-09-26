using System;

namespace KargoSim.Cargo
{
    /// <summary>
    /// Paketleme masasindaki is adimlari.
    ///
    /// SIRALAMA KURALI: degerler olusturulduktan SONRA degistirilemez.
    /// Save dosyalari bu sayilari duz sayi olarak yaziyor; bir deger
    /// kaydirilirsa eski kayitlar yanlis adima okunur. Yeni adim sadece
    /// SONUNA eklenir.
    /// </summary>
    public enum PackagingStep
    {
        /// <summary>Hic baslanmadi. Kargo depoda ham haliyle durur.</summary>
        None = 0,

        /// <summary>Karton masaya kondu, kesilmeyi bekliyor.</summary>
        SheetPlaced = 1,

        /// <summary>Karton kesildi.</summary>
        Cut = 2,

        /// <summary>Karton katlandi, kutu haline geldi.</summary>
        Folded = 3,

        /// <summary>Urun kutuya kondu.</summary>
        ProductInside = 4,

        /// <summary>Bant yapistirildi; paket kapali.</summary>
        Taped = 5,

        /// <summary>Etiket basildi; paket teslimata hazir.</summary>
        Labelled = 6,
    }

    /// <summary>
    /// Bir adimin tanimi: kac saniye surdugu ve oyuncuya ne yaptigini
    /// anlatan metin.
    ///
    /// Neden ScriptableObject DEGIL: bunlar kural, icerik degil. Bir
    /// kargo turune gore degismiyorlar. ScriptableObject yapilsaydi her
    /// adim icin ayri bir asset ve menu gerekirdi - kural icin fazla
    /// maliyet. Icerik (urun, fiyat) ayri katmanda.
    /// </summary>
    public readonly struct PackagingStepInfo
    {
        public PackagingStep Step { get; }

        /// <summary>Adimin gercek suresi (saniye).</summary>
        public float DurationSeconds { get; }

        /// <summary>Masa ipucunda gosterilen eylem ("Karton kes").</summary>
        public string ActionLabel { get; }

        public PackagingStepInfo(PackagingStep step, float durationSeconds, string actionLabel)
        {
            Step = step;
            DurationSeconds = durationSeconds;
            ActionLabel = actionLabel;
        }
    }

    /// <summary>
    /// Paketleme adimlarinin tek kaynagi.
    ///
    /// Neden ayri statik sinif: hem <see cref="PackagingSystem"/> hem de
    /// arayuz (PackingTable) ayni sirayi ve ayni metinleri kullanir.
    /// Ikisi ayri ayri yazilirsa "Kartonu katla" ile "Kartonu katla" farkli
    /// yere kayar ve oyuncu iki farkli oyun oynar.
    /// </summary>
    public static class PackagingSteps
    {
        /// <summary>Toplam adim sayisi (None haric).</summary>
        public const int StepCount = 6;

        // Her adim farkli surer: kesmek hizli, katlamak zahmetli,
        // etiket basmak yavas. Hepsi esit olsaydi oyuncu "tik al" gibi
        // hissetmezdi.
        //
        // SheetPlaced SIFIR sureli bir GIRIS isaretidir, is degil: masaya
        // koydugun an. Bekletmesi bir sey kazandirmiyor; geri bildirimi
        // zaten "karton gorundu" ve PackagingStarted olayi veriyor. Asil is
        // bir sonraki adimda (kesme) baslar.
        private const float DurationSheetPlaced = 0.0f;
        private const float DurationCut = 1.2f;
        private const float DurationFold = 2.0f;
        private const float DurationProduct = 1.5f;
        private const float DurationTape = 1.8f;
        private const float DurationLabel = 1.0f;

        private static readonly PackagingStepInfo[] Infos =
        {
            new PackagingStepInfo(PackagingStep.None,         DurationSheetPlaced, "Kartona basla"),
            new PackagingStepInfo(PackagingStep.SheetPlaced, DurationSheetPlaced, "Kartonu kes"),
            new PackagingStepInfo(PackagingStep.Cut,         DurationCut,         "Kartonu katla"),
            new PackagingStepInfo(PackagingStep.Folded,      DurationFold,        "Urunu kutuya koy"),
            new PackagingStepInfo(PackagingStep.ProductInside, DurationProduct,   "Bantla"),
            new PackagingStepInfo(PackagingStep.Taped,       DurationTape,        "Etiket bas"),
            new PackagingStepInfo(PackagingStep.Labelled,    DurationLabel,       "Paket hazir"),
        };

        /// <summary>
        /// Adimin tanimini dondurur. Gecersiz deger icin None bilgisi
        /// doner - cagiran taraf null kontrolu yapmak zorunda kalmasin.
        /// </summary>
        public static PackagingStepInfo Info(PackagingStep step)
        {
            int index = (int)step;
            if (index < 0 || index >= Infos.Length) return Infos[0];
            return Infos[index];
        }

        /// <summary>Su anki adimin suresi (saniye).</summary>
        public static float DurationOf(PackagingStep step) => Info(step).DurationSeconds;

        /// <summary>Su anki adimin oyuncuya gosterilen eylemi.</summary>
        public static string ActionOf(PackagingStep step) => Info(step).ActionLabel;

        /// <summary>Bu adimdan sonraki adim. Labelled son adimdir.</summary>
        public static PackagingStep Next(PackagingStep step)
        {
            int index = (int)step;
            if (index < 0) index = 0;
            if (index >= StepCount) return PackagingStep.Labelled;
            return (PackagingStep)(index + 1);
        }

        /// <summary>Paket teslimata hazir mi?</summary>
        public static bool IsReady(PackagingStep step) => step == PackagingStep.Labelled;

        /// <summary>En bastan baslamis mi (hicbir is yapilmamis)?</summary>
        public static bool IsUntouched(PackagingStep step) => step == PackagingStep.None;

        /// <summary>
        /// 0..1 arasi ilerleme. HUD/masa gostergesi bunu kullanir.
        /// </summary>
        public static float Progress01(PackagingStep step)
        {
            int index = (int)step;
            if (index <= 0) return 0f;
            if (index >= StepCount) return 1f;
            return (float)index / StepCount;
        }

        /// <summary>Tum sirayi sifirdan uretir (testler ve arayuz icin).</summary>
        public static PackagingStep[] AllSteps()
        {
            var all = new PackagingStep[Infos.Length];
            for (int i = 0; i < Infos.Length; i++) all[i] = (PackagingStep)i;
            return all;
        }
    }
}
