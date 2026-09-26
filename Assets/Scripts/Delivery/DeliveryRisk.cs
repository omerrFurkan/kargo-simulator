using System;
using KargoSim.Cargo;

namespace KargoSim.Delivery
{
    /// <summary>
    /// ASAMA 11 - TESLIMAT CESITLILIGI (risk).
    ///
    /// Bu sinif SADECE hesap yapar: MonoBehaviour degil, sahne tanimaz,
    /// kare cagrisi yapmaz, para hesaplamaz. Iki soruyu yanitlar:
    ///
    ///   1) Bu kargo zamaninda ve saglam teslim edilir mi?
    ///   2) Kirilabilir kargo yolda hasar gordu mu?
    ///
    /// NEDEN AYRI BIR SINIF: ParcelType.baseSuccessRate, ParcelType.fragile ve
    /// ParcelType.isRare alanlari vardi ama HICBIR YERDE okunmuyordu; oyun
    /// her teslimatta %100 basariliydi. Risk, odulu degistirmeden once
    /// karar vermeyi saglar: ayni kargo, ayni gun, ayni sonucu verir.
    ///
    /// DETERMINIZM: rastgelelik System.Random veya UnityEngine.Random
    /// DEGILDIR. Metotlar `int seed` alir ve iclerde kucuk bir LCG
    /// (linear congruential generator) kullanilir. Ayni tohum -> ayni sonuc,
    /// testte de oyunda da. "Ayni kargo, ayni gun, ayni karar" kurali bu
    /// yuzden bozulmaz; ayrica oyuncu save/yukleyerek ayni duruma
    /// dondugunde kargoyu tekrar denerse eski karar degismez.
    ///
    /// KATMAN KURALI: ekonomi katmani bizi referans almaz, biz de onu
    /// referans almayiz. Odeme/ceza yine DeliveryOutcome uclusuyle
    /// yapilir; risk yalnizca o ucun hangisine dustugunu belirler.
    /// </summary>
    public static class DeliveryRisk
    {
        // ------------------------------------------------------------------
        // Ayarlar (tek yerde, ki denetleyici bunlari okuyabilsin)
        // ------------------------------------------------------------------

        /// <summary>
        /// Kargo turu tanimlanmamis kargo icin taban oran. "Tur bilinmiyor"
        /// demektir; bilinmeyen kargoyu guvenilir saymiyoruz.
        /// </summary>
        public const float UnknownTypeSuccessRate = 0f;

        /// <summary>Sure tam doluyken (kalan oran 1.0) zaman carpani.</summary>
        public const float OnTimeFactorMax = 1f;

        /// <summary>
        /// Sure doldugunda (kalan oran 0) zaman carpani. Gece teslim "hizli
        /// kuryi" degil, "kargiyi zoraki butunlestiren" bir teslimattir.
        /// </summary>
        public const float OnTimeFactorMin = 0.45f;

        /// <summary>
        /// Oncelik duzeltmesi. Kucuk tutulur: kritik teslimat ne cok daha
        /// kolay ne cok daha zor olmamali, sadece FARKLI HISSETTIRMELI.
        /// Normal 0, Hizli +0.03, Acil +0.05. Acil kargonun riski zaten
        /// yarim sureyle gelir; duzeltme o riski biraz norderlestirir,
        /// carpan degil.
        /// </summary>
        public const float PriorityBonusExpress = 0.03f;
        public const float PriorityBonusUrgent = 0.05f;

        /// <summary>
        /// Nadir kargo ayricaligi: deger gordugu icin daha sorumlu tasinir,
        /// guvenilir teslimat orani bir tik yukselir.
        /// </summary>
        public const float RareBonus = 0.02f;

        /// <summary>Kirilabilir kargo icin zamaninda hasar olasiligi.</summary>
        public const float FragileDamageChanceOnTime = 0.05f;

        /// <summary>Kirilabilir kargo icin gec teslimde hasar olasiligi.</summary>
        public const float FragileDamageChanceLate = 0.75f;

        /// <summary>Zamaninda hasar olursa hasar yuzdesi bu civarda kalir.</summary>
        public const int FragileDamageMin = 8;

        /// <summary>Sure tamamen gecmis kargoda hasar en kotu bu kadar olur.</summary>
        public const int FragileDamageMax = 90;

        // Tohum sabitleri. Kanal basina ayri tuz: ayni kargo icin "teslim
        // sonucu" ve "hasar" cekimleri birbirine bagli olmaz. Birine
        // denk gelmesi digerine etki etmemeli.
        private const int OutcomeChannelSalt = 999983;
        private const int DamageChannelSalt = unchecked((int)0x5bd1e995);

        // ------------------------------------------------------------------
        // Oran hesaplari (saf, yan etkisiz)
        // ------------------------------------------------------------------

        /// <summary>
        /// Kargonun gercek basari orani (0..1).
        ///
        ///   oran = baseSuccessRate * (1 + oncelikDuzeltmesi [+ nadirBonus])
        ///
        /// Oncelik riski KOKSELTMEZ, sadece NOTRLESTIRIR: yuksek oncelikli
        /// kargonun taban oranini bir tik yukselter. Sonuc 0..1 araligina
        /// kirpilir (base 1.0 + acil bonusu 1.0'i gecmemeli).
        /// </summary>
        public static float EffectiveSuccessRate(Parcel parcel)
        {
            // Kargo ya da turu yoksa "teslimat yapilamaz": 0 doner, boylece
            // RollOutcome her zaman BASARISIZ uretir ve istisna atmaz.
            if (parcel == null || parcel.Type == null) return UnknownTypeSuccessRate;

            float adjustment = PriorityAdjustment(parcel.Priority);
            if (parcel.Type.isRare) adjustment += RareBonus;

            return Clamp01(parcel.Type.baseSuccessRate * (1f + adjustment));
        }

        /// <summary>
        /// Zaman baski carpani (0..1 arasi deger doner). Kalan sure orani
        /// 1.0 ise <see cref="OnTimeFactorMax"/>, 0 ise
        /// <see cref="OnTimeFactorMin"/>. Arada lineer.
        /// </summary>
        public static float OnTimeFactor(float remainingRatio)
        {
            float ratio = Clamp01(remainingRatio);
            return OnTimeFactorMin + (OnTimeFactorMax - OnTimeFactorMin) * ratio;
        }

        /// <summary>
        /// Bu kargo, bu anda teslim edilirse basari olasiligi (0..1).
        /// RollOutcome'in kullandigi olasilik budur; HUD ve testler de
        /// okuyabilsin diye acik tutulur.
        /// </summary>
        public static float SuccessProbability(Parcel parcel, float remainingRatio)
        {
            return Clamp01(EffectiveSuccessRate(parcel) * OnTimeFactor(remainingRatio));
        }

        /// <summary>
        /// Kirilabilir kargo icin hasar olasiligi (0..1).
        ///
        /// DIKKAT yon: kalan sure orani ARTtikca olasilik AZALIR. Kargo
        /// yolda bekletilip gece teslim edildiginde hasar riski cikar.
        /// </summary>
        public static float FragileDamageChance(float remainingRatio)
        {
            float ratio = Clamp01(remainingRatio);
            return FragileDamageChanceLate
                 + (FragileDamageChanceOnTime - FragileDamageChanceLate) * ratio;
        }

        // ------------------------------------------------------------------
        // Kargo oran yardimcisi
        // ------------------------------------------------------------------

        /// <summary>
        /// Kargonun kalan suresinin baslangic suresine orani (0..1).
        /// Henuz araca yuklenmediyse 1 doner (sayac baslamadi).
        ///
        /// DIKKAT: ekonomi katmaninda ayni hesap
        /// (PayoutCalculator.RemainingRatio) var. Burada BILEREK tekrarlaniyor:
        /// teslimat katmani ekonomi katmanini referans almaz. Iki katman
        /// birbirine baglanirsa biri silindiginde digeri de kirilir.
        /// </summary>
        public static float RemainingRatio(Parcel parcel, float elapsed)
        {
            if (parcel == null) return 0f;
            if (parcel.DeadlineSeconds <= 0f) return 0f;
            if (!parcel.IsLoaded) return 1f;

            return Clamp01(parcel.RemainingSeconds(elapsed) / parcel.DeadlineSeconds);
        }

        // ------------------------------------------------------------------
        // Tohum
        // ------------------------------------------------------------------

        /// <summary>
        /// Teslim sonucu cekimi icin tohum. Ayni kargo, ayni gun, ayni sonuc.
        /// </summary>
        public static int OutcomeSeed(int day, int parcelId) =>
            MakeSeed(day, parcelId, OutcomeChannelSalt);

        /// <summary>Kirilabilir hasar cekimi icin tohum.</summary>
        public static int DamageSeed(int day, int parcelId) =>
            MakeSeed(day, parcelId, DamageChannelSalt);

        /// <summary>
        /// (gun, kargo kimligi, kanal) uclusunu tek bir tohuma indirger.
        /// unchecked aritmetik kullanilir: tasma olsa bile sonuc kritik
        /// degildir, yalnizca dagilim degisir; tutarlilik (ayni girdi ->
        /// ayni cikti) her zaman korunur.
        /// </summary>
        private static int MakeSeed(int day, int parcelId, int channelSalt)
        {
            unchecked
            {
                uint h = (uint)(day * 73856093) ^ (uint)(parcelId * 19349663) ^ (uint)channelSalt;
                h ^= h >> 16; h *= 0x7feb352d;
                h ^= h >> 15; h *= 0x846ca68b;
                h ^= h >> 16;

                int seed = (int)h;
                return seed == 0 ? 1 : seed;   // sifir tohum kargi degil, onu da ele al
            }
        }

        // ------------------------------------------------------------------
        // Zar atma
        // ------------------------------------------------------------------

        /// <summary>
        /// Teslim sonucunu atar.
        ///
        ///   basari = EffectiveSuccessRate * OnTimeFactor(kalan oran)
        ///
        /// Zamaninda teslim edilen kargo basarili olabilir ya da musteri
        /// teslimi reddedebilir (Failed). Sure gecmis kargo ya "gec kaldi"
        /// (Late) olur ya da reddedilir. Sonuc ucunun disina cikmaz, cunku
        /// ekonomi tarafi yalnizca bu ucunu taniyor.
        /// </summary>
        /// <param name="parcel">Teslim edilen kargo (null ise BASARISIZ).</param>
        /// <param name="remainingRatio">Kalan surenin baslangica orani (0..1).</param>
        /// <param name="seed">Tohum: ayni tohum ayni sonuc.</param>
        public static DeliveryOutcome RollOutcome(Parcel parcel, float remainingRatio, int seed)
        {
            float probability = SuccessProbability(parcel, remainingRatio);

            // Kargo yoksa (veya taban orani 0 olan tur bilinmeyen kargoysa)
            // olasilik 0'dir. Yine de cevrim ici kontrol koyuyoruz: null kargo
            // "basarili" sayilmamali, istisna da atmamali.
            if (probability <= 0f) return DeliveryOutcome.Failed;

            if (new Lcg(seed).Next01() < probability)
                return remainingRatio > 0f ? DeliveryOutcome.Success : DeliveryOutcome.Late;

            return DeliveryOutcome.Failed;
        }

        /// <summary>
        /// Kirilabilir kargonun hasar yuzdesi (0..100). Kirilabilir degilse
        /// HER ZAMAN 0 doner. Zamaninda teslimde hasar olasiligi dusuk ve
        /// hasar kucuktur; sure gectikce olasilik artar, hasar buyur.
        ///
        /// 0 donmesi "kargoyu yaralayacak kadar bozulsun" demektir: deger
        /// kaybi DeliverySystem tarafinda Failed'a cevrilir.
        /// </summary>
        public static int FragileDamage(Parcel parcel, float remainingRatio, int seed)
        {
            if (parcel == null || !parcel.Fragile) return 0;

            float chance = FragileDamageChance(remainingRatio);
            if (chance <= 0f) return 0;

            var rng = new Lcg(seed);
            if (rng.Next01() >= chance) return 0;

            // Hasar agirligi: sure ne kadar bitmisse hasar o kadar buyuk.
            float lateness = 1f - Clamp01(remainingRatio);
            float severity = FragileDamageMin
                           + (FragileDamageMax - FragileDamageMin) * lateness;
            severity += rng.Next01() * 10f;      // ayni kosulda iki kargo farkli olsun

            if (severity > 100f) severity = 100f;
            return (int)Math.Round(severity, MidpointRounding.AwayFromZero);
        }

        // ------------------------------------------------------------------
        // Ic yardimcilar
        // ------------------------------------------------------------------

        /// <summary>
        /// Oncelik duzeltmesi. Kucuk ve tek yonlu: normal kargo tam taban
        /// oraninda kalir, hizli/acil bir tik yukselir.
        /// </summary>
        private static float PriorityAdjustment(ParcelPriority priority)
        {
            switch (priority)
            {
                case ParcelPriority.Urgent: return PriorityBonusUrgent;
                case ParcelPriority.Express: return PriorityBonusExpress;
                default: return 0f;
            }
        }

        private static float Clamp01(float value)
        {
            if (float.IsNaN(value)) return 0f;
            if (value < 0f) return 0f;
            return value > 1f ? 1f : value;
        }

        /// <summary>
        /// Kucuk, hizli, tamamen deterministik rastgelelik.
        /// (Numerical Recipes LCG: carpan 1664525, artik 1013904223.)
        ///
        /// Neden System.Random degil: (a) her cagrida nesne ayirmak
        /// gerekir, (b) surumden surume degisebilir, (c) testte "bu tohum
        /// su sonucu verir" dedigimiz sey garanti olmaz. UnityEngine.Random
        /// ise global durum tutar ve EditMode testinde kirlidir.
        /// </summary>
        private struct Lcg
        {
            /// <summary>
            /// Isitma adimi. Tek bir LCG adimindan gelen deger, tohumla
            /// neredeyse dogrusal iliskilidir: 0,1,2... tohumlari sirayla
            /// artan bir seri verir ve "farkli tohum farkli sonuc verir"
            /// testi yaniltir. Birkac bos adimdan sonra cikti, tohumdan
            /// bagimsiz gorunur.
            /// </summary>
            private const int WarmupSteps = 8;

            private uint _state;

            public Lcg(int seed)
            {
                _state = unchecked((uint)seed);
                for (int i = 0; i < WarmupSteps; i++) _state = Next(_state);
            }

            public float Next01()
            {
                _state = Next(_state);

                // Ust 24 bit: LCG'nin dusuk bitleri zayif, yuksek bitleri
                // daha duzgun dagiliyor.
                return (_state >> 8) * (1f / 16777216f);
            }

            private static uint Next(uint state)
            {
                return unchecked(state * 1664525u + 1013904223u);
            }
        }
    }
}
