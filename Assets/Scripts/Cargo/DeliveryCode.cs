using System;

namespace KargoSim.Cargo
{
    /// <summary>
    /// TESLIMAT KODU: kuryenin kapida dogrulamasi gereken rakam.
    ///
    /// Neden ayri dosya: kod hem kargoya AIT hem de gecici bir dogrulama
    /// araci. Kargonun icinde dursa, kargo sinifi hem teslimat hem
    /// dogrulama sorumlulugu tasirdi.
    ///
    /// KOD NEREDEN GELIYOR (onemli tasarim karari):
    /// Kod kargodan TURETILIR, kayit dosyasinda SAKLANMAZ.
    ///   kod = Hash(parcelId, gun, turIndex)
    /// Boylece:
    ///   - Kayit/ yukleme KOD KAYBETMEZ. Kayda yeni alan eklemek, eski
    ///     kayitlari bos kodla dondururdu ve oyuncunun teslimati imkansiz
    ///     hale gelirdi.
    ///   - Ayni kargo icin kod her zaman AYNIDIR; oyuncu "kutu degisti,
    ///     kodum degisti" diye korkmaz.
    ///   - Farkli gunlerde ayni kargo farkli kod alir: eski bilgiyle
    ///     teslimat yapilamaz.
    ///
    /// BILINEN SINIR: kod kargo kimliginden deterministik turetildigi
    /// icin, formulu bilen bir oyuncu kodu hesaplayabilir. Bu MVP icin
    /// kabul edildi: gercek rastgelelik icin koda gunluk bir tuz (salt)
    /// eklenmesi yeterli, mimari degismez.
    /// </summary>
    public static class DeliveryCode
    {
        /// <summary>Kodun hane sayisi (4 haneli).</summary>
        public const int Digits = 4;

        /// <summary>Gecerli kod araligi (1000 - 9999; 4 haneli ve sifir baslamaz).</summary>
        public const int MinValue = 1000;
        public const int MaxValue = 9999;

        /// <summary>Ozgun tuz: ayni kargo farkli gunde farkli kod alir.</summary>
        private const int DaySalt = 7919;

        /// <summary>
        /// Kargonun teslimat kodunu uretir. Ayni girdi icin her zaman
        /// ayni sonucu doner.
        /// </summary>
        /// <param name="parcelId">Kargo kimligi.</param>
        /// <param name="day">Gun numarasi (1'den baslar).</param>
        /// <param name="typeIndex">Kargo turunun indeksi; ayni kargo icin
        /// sabittir, farkli turde ayni koda denk gelmeyi engeller.</param>
        public static int Generate(int parcelId, int day, int typeIndex)
        {
            // unchecked: int tasmasi hesabi degistirmesin.
            unchecked
            {
                int mixed = parcelId * 73856093;
                mixed ^= day * DaySalt;
                mixed ^= typeIndex * 19349663;
                mixed ^= mixed >> 13;
                mixed *= 1274126177;
                mixed ^= mixed >> 16;

                int span = MaxValue - MinValue + 1;
                return MinValue + (Abs(mixed) % span);
            }
        }

        /// <summary>int.MinValue'de Abs patlamasin diye.</summary>
        private static int Abs(int v) => v == int.MinValue ? int.MaxValue : (v < 0 ? -v : v);

        /// <summary>Kodu gosterilebilir metne cevirir ("4821").</summary>
        public static string Format(int code)
        {
            // 4 haneye tamamla: 7 -> "0007". Kod 1000-9999 araliginda
            // oldugu icin bu pratikte hep 4 haneli olur, ama kod
            // dogrulamasinda "007" ile "7" ayrimi yapilabilsin.
            return code.ToString("D" + Digits);
        }

        /// <summary>Kod metnini guvenli ayristirir. Gecersizse null.</summary>
        public static bool TryParse(string text, out int code)
        {
            code = 0;
            if (string.IsNullOrEmpty(text)) return false;

            string trimmed = text.Trim();
            if (trimmed.Length == 0) return false;

            int parsed;
            if (int.TryParse(trimmed, out parsed) == false) return false;

            if (parsed < MinValue || parsed > MaxValue) return false;

            code = parsed;
            return true;
        }

        /// <summary>
        /// Oyuncunun girdigi kod dogru mu?
        ///
        /// BOS KOD GECERLI SAYILMAZ: hicbir sey yazmadan Onayla'ya basmak
        /// teslimati acmaz.
        /// </summary>
        public static bool Verify(int expectedCode, string playerInput)
        {
            int given;
            if (TryParse(playerInput, out given) == false) return false;
            return given == expectedCode;
        }
    }
}
