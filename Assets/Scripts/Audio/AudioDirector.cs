using KargoSim.Cargo;
using KargoSim.Core;
using KargoSim.Delivery;
using KargoSim.Economy;
using KargoSim.Vehicle;
using UnityEngine;

namespace KargoSim.Audio
{
    /// <summary>
    /// Oyunun cikaracagi tek ses listesi.
    ///
    /// Neden enum: sesler sistemlere dagilmis birer "zamanlanmis gorev" degil,
    /// hepsi AudioDirector'un bir dallanma noktasi. Yeni bir ses eklemek
    /// icin iki dosyaya dokunmak yeterli olsun diye tanim burada toplandi.
    /// </summary>
    public enum AudioCue
    {
        /// <summary>Kutu raftan / depoda alindi. Kisa yuksek tik.</summary>
        KutuAl = 0,

        /// <summary>Kutu araca kondu. Alcak "gum" sesi.</summary>
        AracaKoy = 1,

        /// <summary>Karakter adimi. Kis filtrelenmis gurultu patlamasi.</summary>
        AyakSesi = 2,

            /// <summary>Kargo zamaninda teslim edildi. Yukselen iki nota.</summary>
            TeslimBasarili = 3,


        /// <summary>Kargo teslim edilemedi (gun sonu). Dusen iki nota.</summary>
        TeslimBasarisiz = 4,

        /// <summary>Odeme uygulandi. Uclu arpej.</summary>
        ParaKazanildi = 5,

        /// <summary>Yeni seviye. Dortlu yukselen arpej.</summary>
        SeviyeYukseldi = 6,

        /// <summary>
        /// Kapi calindi. Iki kuru vurus: tok bir "tok" ve yumrugun
        /// bosluSa degenmesi. Asama 13.
        /// </summary>
        KapiCal = 7,

        /// <summary>
        /// NPC kapidi acti ve selam verdi. Iki notali yumusakYukselis.
        /// Asama 13.
        /// </summary>
        NpcSelam = 8,

        /// <summary>
        /// Arac motoru. DONGUSEL: PlayOneShot ile degil, ozel bir AudioSource
        /// uzerinde loop = true olarak calinir.
        /// </summary>
        AracMotoru = 7,
    }

    /// <summary>
    /// PROSEDUREL SES SENTETIKLEYICI.
    ///
    /// Neden ses dosyasi yok: projede tek bir wav/mp3/ogg bulunmuyor ve bu
    /// fazda ikili dosya uretilemiyor. Bunun yerine her efekt kod icinde
    /// sinus + gurultu karisimindan sentezlenir. Bu sadece bir zorunluluk
    /// degil, projenin "gereksiz paket yok / dusuk sistem gereksinimi"
    /// kuraliyla da tam uyumlu: 8 efektin toplam bellegi ~220 KB'tir
    /// (22050 Hz mono, 16-bit-benzeri float tampon), lisans sorunu yoktur
    /// ve ornek verisi disari aktarilmaz.
    ///
    /// DETERMINIZM KURALI: burada <c>UnityEngine.Random</c> KULLANILMAZ.
    /// UnityEngine.Random global durumu paylasir; ayni cagri iki farkli sonuc
    /// verirse (farkli test sirasi, farkli platform) testler tekrarlanamaz
    /// hale gelir. Onun yerine tohumlu (LCG) ureticimiz vardir: ayni
    /// parametre -> birebir ayni dalga formu.
    ///
    /// TEST YAPISI: uretim saf bir fonksiyondur (float[] doner), klip nesnesi
    /// ayrica bir katmandir. Boylece EditMode testleri ses dinlemeden hem
    /// dalga formunu hem de klip sayisini dogrulayabilir.
    /// </summary>
    public static class ProceduralAudio
    {
        /// <summary>Ses etiketi sayisi (enum boyutu).</summary>
        public const int CueCount = 10;

        /// <summary>
        /// Ornekleme hizi. 22050 Hz (CD yarisi) bilincli bir tercih:
        /// bu efektlerin en yuksek bileseni ~1.3 kHz, 44.1 kHz'in yarisi
        /// hem duyulabilir sonuc verir hem de bellegi yarilar.
        /// </summary>
        public const int SampleRate = 22050;

        private const float TwoPi = 6.2831853f;

        /// <summary>Bir kez uretilip saklanan klip kumesi. 0 = henuz uretilmedi.</summary>
        private static readonly AudioClip[] _cache = new AudioClip[CueCount];

        // ------------------------------------------------------------------
        // Boyut
        // ------------------------------------------------------------------

        /// <summary>Effektin suresi (saniye). Dairesel motor sesi haric.</summary>
        public static float SecondsFor(AudioCue cue)
        {
            switch (cue)
            {
                case AudioCue.KutuAl: return 0.08f;
                case AudioCue.AracaKoy: return 0.15f;
                case AudioCue.AyakSesi: return 0.06f;
                case AudioCue.TeslimBasarili: return 0.35f;
                case AudioCue.TeslimBasarisiz: return 0.40f;
                case AudioCue.ParaKazanildi: return 0.40f;
                case AudioCue.SeviyeYukseldi: return 0.60f;
        case AudioCue.KapiCal: return 0.26f;
        case AudioCue.NpcSelam: return 0.34f;
                default: return 0.50f;   // AracMotoru: dongu tamponu
            }
        }

        /// <summary>Effektin ornek sayisi. Asla 0 degil (AudioClip.Create 0'i reddeder).</summary>
        public static int SamplesFor(AudioCue cue)
        {
            return Mathf.Max(1, Mathf.RoundToInt(SampleRate * SecondsFor(cue)));
        }

        // ------------------------------------------------------------------
        // Uretim
        // ------------------------------------------------------------------

        /// <summary>
        /// Verilen cue icin ham (mono) ornek dizisini uretir. Deterministiktir:
        /// ayni cue icin iki cagri birebir ayni diziyi dondurur.
        /// </summary>
        public static float[] Generate(AudioCue cue)
        {
            int n = SamplesFor(cue);
            var data = new float[n];

            switch (cue)
            {
                case AudioCue.KutuAl: SynthClickUp(data); break;
                case AudioCue.AracaKoy: SynthClickDown(data); break;
                case AudioCue.AyakSesi: SynthFootstep(data); break;
                case AudioCue.TeslimBasarili:
                    SynthTwoNotes(data, 660f, 990f, 0.15f, 9f, 0.34f, 0.22f);
                    break;
                case AudioCue.TeslimBasarisiz:
                    // Dusuk nota + guclu 3. harmonik: "kabul edilmedi" hissi.
                    SynthTwoNotes(data, 392f, 262f, 0.17f, 7f, 0.34f, 0.55f);
                    break;
                case AudioCue.ParaKazanildi:
                    SynthArpeggio(data, new[] { 784f, 988f, 1319f }, 0.11f, 0.16f, 0.28f, 0.3f);
                    break;
        case AudioCue.SeviyeYukseldi:
                        SynthArpeggio(data, new[] { 523f, 659f, 784f, 1047f }, 0.13f, 0.20f, 0.28f, 0.3f);
                        break;
                    case AudioCue.KapiCal:
                        SynthKnock(data);
                        break;
                    case AudioCue.NpcSelam:
                        SynthTwoNotes(data, 392f, 523f, 0.14f, 0.24f, 0.26f, 0.3f);
                        break;
                default:
                    SynthEngineLoop(data);
                    break;
            }

            // Motor sesi dongusel oldugu icin kenar solgunlugu ALMAZ: solgunluk
            // loop'un birlesme noktasinda surekli bir sekez olusurdu.
            if (cue != AudioCue.AracMotoru) ApplyEdgeFade(data);

            Normalize(data, cue == AudioCue.AracMotoru ? 0.9f : 0.85f);
            return data;
        }

        /// <summary>
        /// Cue icin klip uretir ve statik olarak saklar. Ilk cagri uretir,
        /// sonrakiler ayni ornegi dondurur (her seferinde yeniden sentez
        /// yapmaz, bellek ve CPU icin onemlidir).
        /// </summary>
        public static AudioClip GetClip(AudioCue cue)
        {
            int i = (int)cue;
            if (i < 0 || i >= CueCount) return null;
            if (_cache[i] != null) return _cache[i];

            int samples = SamplesFor(cue);
            AudioClip clip = AudioClip.Create(cue.ToString(), samples, 1, SampleRate, false);
            clip.SetData(Generate(cue), 0);

            // Sahneye dusmesin, kayit dosyasina hic yazilmasin.
            clip.hideFlags = HideFlags.DontSave;
            _cache[i] = clip;
            return clip;
        }

        /// <summary>
        /// Yeni sinif statik onbellekler: teslimat kodu tonlari.
        /// </summary>
        private sealed class CodeToneCache
        {
            /// <summary>
            /// Kod -> klip. BAYAT ve SIRALI silme yerine kucuk bir havuz:
            /// bir gunde 12 kargo, ayni kod bir kez uretilir. Havuz sinirli
            /// tutuluyor ki uzun bir oturumda bellek sismesin.
            /// </summary>
            public const int MaxEntries = 16;

            public readonly System.Collections.Generic.Dictionary<int, AudioClip> Clips =
                new System.Collections.Generic.Dictionary<int, AudioClip>();

            public readonly System.Collections.Generic.Queue<int> Order =
                new System.Collections.Generic.Queue<int>();

            public void Clear()
            {
                foreach (var kv in Clips)
                {
                    AudioClip clip = kv.Value;
                    if (clip == null) continue;
                    if (Application.isPlaying) Object.Destroy(clip);
                    else Object.DestroyImmediate(clip);
                }
                Clips.Clear();
                Order.Clear();
            }
        }

        private static readonly CodeToneCache _codeTones = new CodeToneCache();

        /// <summary>
        /// Bir rakamin perdeye eslenmesi: 0 = dusuk, 9 = yuksek.
        ///
        /// Neden perde: sesli olarak "dort" demek yerine dOrt FARKLI
        /// PERDE duyulur ve oyuncu bunu kodun RAKAMI olarak degil,
        /// SIRAYA KONULACAK bir melodi olarak hatirlar. Eski telefon
        /// tuslarinin DTMF mantigi da boyledir.
        ///
        /// Aralik bilincli: 440 Hz (A4) ile 880 Hz arasinda tam oktav.
        /// Insan kulaSi bu araligi kolay ayirt eder; cok aralija
        /// yaymak "yarisi ayni" duyulmasina yol acar.
        /// </summary>
        public const float CodeToneLowHz = 440f;
        public const float CodeToneHighHz = 880f;
        public const float CodeToneStepSeconds = 0.30f;
        public const float CodeToneNoteSeconds = 0.16f;

        /// <summary>Rakamin perde frekansi (0..9).</summary>
        public static float FrequencyForDigit(int digit)
        {
            if (digit < 0) digit = 0;
            if (digit > 9) digit = 9;

            float t = digit / 9f;
            return Mathf.Lerp(CodeToneLowHz, CodeToneHighHz, t);
        }

        /// <summary>Kod tonlarinin toplam suresi (saniye).</summary>
        public static float CodeToneSeconds(int digits) =>
            Mathf.Max(1, digits) * CodeToneStepSeconds;

        /// <summary>
        /// NPC'nin soyleyeceSi kod tonunu uretir ve onbellekler.
        ///
        /// ONEMLI: uretilen perde sirasi ile <c>Cargo.DeliveryCode</c>'in
        /// rakamlari BIREBIR ayni olur - ses ile gosterilen kod arasinda
        /// ayrilma olmaz. Ses bir "tema" degil, kodun kendisidir.
        /// </summary>
        public static AudioClip GetCodeToneClip(int code)
        {
            AudioClip cached;
            if (_codeTones.Clips.TryGetValue(code, out cached) && cached != null)
                return cached;

            int digits = 4;
            float[] data = new float[Mathf.RoundToInt(SampleRate * CodeToneSeconds(digits))];

            for (int n = 0; n < digits; n++)
            {
                // 4827 -> 4, 8, 2, 7
                int digit = (code / Pow10(digits - 1 - n)) % 10;
                float freq = FrequencyForDigit(digit);

                int start = Mathf.RoundToInt(SampleRate * CodeToneStepSeconds * n);
                if (start >= data.Length) break;
                int count = Mathf.Min(data.Length - start,
                                      Mathf.RoundToInt(SampleRate * CodeToneNoteSeconds));
                if (count <= 0) continue;

                AddNote(data, start, freq, count, 0.30f, 9f, 0.45f);
            }

            var clip = AudioClip.Create("Code_" + code, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            clip.hideFlags = HideFlags.DontSave;

            // Havuz sinirini asarsa en eskiyi at (bir gunde 12 kargo
            // uretilir; 16 yeterli).
            if (_codeTones.Order.Count >= CodeToneCache.MaxEntries)
            {
                int oldest = _codeTones.Order.Dequeue();
                AudioClip dead;
                if (_codeTones.Clips.TryGetValue(oldest, out dead))
                {
                    if (dead != null)
                    {
                        if (Application.isPlaying) Object.Destroy(dead);
                        else Object.DestroyImmediate(dead);
                    }
                    _codeTones.Clips.Remove(oldest);
                }
            }

            _codeTones.Clips[code] = clip;
            _codeTones.Order.Enqueue(code);
            return clip;
        }

        /// <summary>
        /// Su an onbellekte tutulan kod klibi sayisi.
        ///
        /// Neden public: havuzun SINIRLI oldugunu test etmek icin.
        /// Onbellek dogru calisiyorsa bu sayi kac kod uretilirse
        /// uretilsin sabit kalir. Erisilebilir olmasa bu bir
        /// "umarim tasmaz" varsayimi olurdu.
        /// </summary>
        public static int CodeToneCacheSize => _codeTones.Clips.Count;

        private static int Pow10(int e)
        {
            int r = 1;
            for (int i = 0; i < e; i++) r *= 10;
            return r;
        }

        /// <summary>Uretilmis tum klipleri yok eder ve onbellegi bosaltir.</summary>
        public static void ReleaseCache()
        {
            for (int i = 0; i < CueCount; i++)
            {
                AudioClip clip = _cache[i];
                if (clip == null) continue;
                if (Application.isPlaying) Object.Destroy(clip);
                else Object.DestroyImmediate(clip);
                _cache[i] = null;
            }

            _codeTones.Clear();
        }

        /// <summary>
        /// "Domain Reload: Disabled" kullanildiginda statik onbellek Play'den
        /// cikarken temizlenmez. Oyun tekrar baslayinca bayat klip referanslari
        /// kullanilirdi; bu yuzden SubsystemRegistration'da sifirlanir.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            for (int i = 0; i < CueCount; i++) _cache[i] = null;
        }

        // ------------------------------------------------------------------
        // Sentez yardimcilari
        // ------------------------------------------------------------------

        /// <summary>
        /// Kutu alma: 620 Hz'den 980 Hz'e cikan kisa bir sweep.
        /// Faz, sabit frekans degil ANLIK frekansin integrali olarak
        /// hesaplanir; aksi halde sweep sirasinda faz sarmalani ve "tik"
        /// duyulurdu.
        /// </summary>
        private static void SynthClickUp(float[] d)
        {
            const float f0 = 620f;
            const float f1 = 980f;
            float dur = Duration(d);
            float slope = (f1 - f0) / dur;

            for (int i = 0; i < d.Length; i++)
            {
                float t = TimeOf(i);
                float env = Attack(t, 700f) * Mathf.Exp(-t * 38f);
                d[i] = env * Mathf.Sin(TwoPi * (f0 * t + 0.5f * slope * t * t));
            }
        }

        /// <summary>
        /// Araca koyma: 480 Hz'den 170 Hz'e dusen sweep + hafif gurultu.
        /// Gurultu tek kutuplu alcak geciriciden gecer; boylece kutu kagit
        /// gibi "patirtili" degil, agir bir "gum" gibi duyulur.
        /// </summary>
        private static void SynthClickDown(float[] d)
        {
            const float f0 = 480f;
            const float f1 = 170f;
            float dur = Duration(d);
            float slope = (f1 - f0) / dur;

            var rng = new Lcg(0x5EED1234u);
            float lowpass = 0f;

            for (int i = 0; i < d.Length; i++)
            {
                float t = TimeOf(i);
                float env = Attack(t, 600f) * Mathf.Exp(-t * 22f);
                lowpass += (rng.NextSigned() - lowpass) * 0.35f;
                d[i] = env * (Mathf.Sin(TwoPi * (f0 * t + 0.5f * slope * t * t)) + 0.45f * lowpass);
            }
        }

        /// <summary>
        /// Ayak sesi: iki kutuplu alcak gecirilmis gurultu patlamasi + kucuk
        /// bir 95 Hz "topuk" darbesi. Gurultunun kendisi beyaz gurultudur;
        /// filtre onu koyulastirir, toptan atilan darbe "beton" hissi verir.
        /// </summary>
        private static void SynthFootstep(float[] d)
        {
            var rng = new Lcg(0xA11CE5u);
            float stage1 = 0f;
            float stage2 = 0f;

            for (int i = 0; i < d.Length; i++)
            {
                float t = TimeOf(i);
                float env = Attack(t, 1400f) * Mathf.Exp(-t * 55f);
                stage1 += (rng.NextSigned() - stage1) * 0.22f;
                stage2 += (stage1 - stage2) * 0.55f;
                float thump = Mathf.Sin(TwoPi * 95f * t) * Mathf.Exp(-t * 45f);
                d[i] = env * stage2 * 1.6f + thump * 0.55f;
            }
        }

        /// <summary>
        /// Iki notali yukselen/dusen figur: ikinci nota tamponun SONUNA
        /// yapistirilir. Boylece sonda bosluk kalmaz ve sure tamamen kullanilir.
        /// </summary>
        /// <summary>
        /// Kapi calma: iki kuru vurus.
        ///
        /// Yumruk sert oldugu icin kisa ve kuru: hizli attack, hizli
        /// decay, guclu alakalanma (bozulmus) tasi. Ikinci vurus biraz
        /// sonra ve biraz kisa - tek yumruk degil, "tok-tok".
        /// </summary>
        private static void SynthKnock(float[] d)
        {
            float dur = Duration(d);

            // Iki vurus: baslangic ve tam yarida.
            for (int hit = 0; hit < 2; hit++)
            {
                float start = dur * (hit == 0 ? 0f : 0.42f);
                int s0 = Mathf.RoundToInt(SampleRate * start);
                int count = Mathf.Min(d.Length - s0,
                                      Mathf.RoundToInt(SampleRate * 0.16f));
                if (count <= 0) continue;

                float amp = hit == 0 ? 0.42f : 0.30f;

                for (int i = 0; i < count; i++)
                {
                    int index = s0 + i;
                    if (index < 0 || index >= d.Length) continue;

                    float t = TimeOf(i);
                    // Cok hizli attack: temas aninda zirve, sonra aninda
                    // sonum. Yumrugun sesi "tik"tir, "gun" degil.
                    float env = Mathf.Min(1f, t * 900f) * Mathf.Exp(-t * 34f);

                    // Govde: dusuk frekans. Tirnak: yuksek, kisa.
                    float body = Mathf.Sin(TwoPi * 148f * t) * Mathf.Exp(-t * 26f);
                    float slap = Mathf.Sin(TwoPi * 720f * t) * Mathf.Exp(-t * 62f);

                    d[index] += env * (body * 0.8f + slap * 0.35f) * amp;
                }
            }
        }

        private static void SynthTwoNotes(float[] d, float first, float second,
                                          float noteSeconds, float decay,
                                          float amp, float brightness)
        {
            int firstCount = Mathf.Min(d.Length, Mathf.RoundToInt(SampleRate * noteSeconds));
            int secondCount = Mathf.Max(1, Mathf.Min(d.Length - firstCount, firstCount));
            AddNote(d, 0, first, firstCount, amp, decay, brightness);
            AddNote(d, d.Length - secondCount, second, secondCount, amp, decay, brightness);
        }

        /// <summary>Arpej: notalar sirayla, birbirisine binerek toplanir.</summary>
        private static void SynthArpeggio(float[] d, float[] freqs, float stepSeconds,
                                          float noteSeconds, float amp, float brightness)
        {
            for (int n = 0; n < freqs.Length; n++)
            {
                int start = Mathf.RoundToInt(SampleRate * stepSeconds * n);
                if (start >= d.Length) break;
                int count = Mathf.Min(d.Length - start, Mathf.RoundToInt(SampleRate * noteSeconds));
                if (count <= 0) continue;
                AddNote(d, start, freqs[n], count, amp, 8f, brightness);
            }
        }

        /// <summary>
        /// Tek bir nota. Temel + 2. ve 3. harmonik: saf sinus "tel" gibi
        /// duyulur, harmonikler "tahta" hissi verir.
        /// </summary>
        private static void AddNote(float[] d, int start, float frequency, int count,
                                    float amp, float decay, float brightness)
        {
            for (int i = 0; i < count; i++)
            {
                int index = start + i;
                if (index < 0 || index >= d.Length) continue;

                float t = TimeOf(i);
                float env = Attack(t, 650f) * Mathf.Exp(-t * decay);
                float v = Mathf.Sin(TwoPi * frequency * t)
                          + brightness * Mathf.Sin(TwoPi * frequency * 2f * t)
                          + brightness * 0.4f * Mathf.Sin(TwoPi * frequency * 3f * t);
                d[index] += amp * env * v;
            }
        }

        /// <summary>
        /// DONGUSEL MOTOR SESI.
        ///
        /// Iki kural burada kritik:
        ///
        /// 1) GURULTU YOK. Beyaz gurultu dongusel bir tampona kopyalandiginda
        ///    son ornek ilk ornekle baglanir ve "tik" duyulur. Bu yuzden
        ///    sinyal tamamen harmonik yiginindan kurulur.
        /// 2) HER BIPLESEN TAM PERIYODIK. Tampon 0.5 s; 56 Hz temel 28,
        ///    28 Hz alt-_osilator 14, 58 Hz detune 29, 4 Hz genlik
        ///    modulasyonu 2 donus yapar. Tam sayili donus sayisi = dikissiz
        ///    dongu. (Basitce 0.1'lik periyod kullanmak burada yeterli
        ///    olurdu; "hangi hizda dogru" diye sorulmadan kesin sonuc verir.)
        ///
        /// Oyun tarafi hiz degisimini <c>AudioSource.pitch</c> ile yapar;
        /// bu yuzden klip TEK bir tonda uretilir.
        /// </summary>
        private static void SynthEngineLoop(float[] d)
        {
            const float fundamental = 56f;    // 28 donus / 0.5 s
            const float detune = 58f;          // 29 donus / 0.5 s
            const float sub = 28f;             // 14 donus / 0.5 s
            const float thumpHz = 4f;          // 2 donus  / 0.5 s

            for (int i = 0; i < d.Length; i++)
            {
                float t = TimeOf(i);
                float v = 0.5f * Mathf.Sin(TwoPi * sub * t);

                for (int h = 1; h <= 12; h++)
                {
                    // Tek harmonikler daha guclu: bu bir testere dalga
                    // yaklasimidir, motorun "gurt" hissini verir.
                    float a = (h % 2 == 1) ? 1f : 0.6f;
                    v += (a / h) * Mathf.Sin(TwoPi * fundamental * h * t + HarmonicPhase(h));
                }

                // 2 Hz'lik vuruntu: iki sesin carpismasiyla "putt-putt".
                v += 0.35f * Mathf.Sin(TwoPi * detune * t + 1.1f);

                // 4 Hz genlik modulasyonu: dongu basina 2 vurus.
                float env = 0.7f + 0.3f * Mathf.Sin(TwoPi * thumpHz * t);
                d[i] = v * env;
            }
        }

        /// <summary>
        /// Harmonik fazlari. SABIT bir fonksiyondur (rastgelelik icermez),
        /// boylece motor sesi her uretimde ayni cikar; harmoniklerin hepsi
        /// ayni fazda olsaydi darbe treni gibi duyulurdu.
        /// </summary>
        private static float HarmonicPhase(int harmonic)
        {
            return Mathf.Sin(harmonic * 1.7f) * 0.9f;
        }

        /// <summary>Baslangic "tik"ini (click) onleyen hizli yukselis.</summary>
        private static float Attack(float t, float rate)
        {
            return 1f - Mathf.Exp(-t * rate);
        }

        private static float TimeOf(int index)
        {
            return index / (float)SampleRate;
        }

        private static float Duration(float[] d)
        {
            return Mathf.Max(d.Length / (float)SampleRate, 0.0001f);
        }

        /// <summary>Tamponun bas ve son 3 ms'ini solgunlastirir (tik yok).</summary>
        private static void ApplyEdgeFade(float[] d)
        {
            int edge = Mathf.Min(d.Length / 4, Mathf.RoundToInt(SampleRate * 0.003f));
            for (int i = 0; i < edge; i++)
            {
                float g = i / (float)edge;
                d[i] *= g;
                d[d.Length - 1 - i] *= g;
            }
        }

        /// <summary>
        /// Tepe degeri sabitler. Boylece "hangi efekt ne kadar gur" karari
        /// elle ayar yapilmaz; normalize etmezsek nota sayisi degisince
        /// ses seviyesi de degisirdi.
        /// </summary>
        private static void Normalize(float[] d, float peak)
        {
            float max = 0f;
            for (int i = 0; i < d.Length; i++) max = Mathf.Max(max, Mathf.Abs(d[i]));
            if (max < 0.0001f) return;

            float gain = peak / max;
            for (int i = 0; i < d.Length; i++) d[i] = Mathf.Clamp(d[i] * gain, -1f, 1f);
        }

        /// <summary>
        /// Tohumlu dogrusal esdeger gurultu ureticisi.
        ///
        /// Neden UnityEngine.Random degil: Unity her cagriya global durumu
        /// ilerletir, yani uretim sirasi degisse sonuc degisir ve testler
        /// tekrarlanamaz olur. LCG tek basina, durumu cagri ici tasinan
        /// bir tamsayi olarak ilerletir: ayni tohum -> ayni gurultu.
        /// </summary>
        private struct Lcg
        {
            private uint _state;

            public Lcg(uint seed)
            {
                _state = seed == 0u ? 1u : seed;   // 0 duraklanan bir tohumdur
            }

            /// <summary>[-1, 1) araliginda tek deger uretir.</summary>
            public float NextSigned()
            {
                _state = _state * 1664525u + 1013904223u;
                return ((_state >> 8) & 0xFFFFFFu) / 8388608f - 1f;   // 24 bit
            }
        }
    }

    /// <summary>
    /// Ses yoneticisi.
    ///
    /// SORUMLULUKLAR
    /// - Efektleri PROSEDUREL olarak uretir (<see cref="ProceduralAudio"/>),
    ///   projede tek bir ses dosyasi yoktur.
    /// - Oyun olaylarini dinleyip dogru cue'u calar. Baska hicbir sistemi
    ///   referanslamaz; <see cref="GameEvents"/>'e abone olur (projenin
    ///   tek haberlesme kanali).
    /// - Motor sesini aracin hizina gore pitch'ler.
    /// - Update() YOK. Tum zamanlama disaridan <c>Tick(dt)</c> ile gelir;
    ///   efektler, motor takibi ve adim araligi yalnizca orada ilerler.
    ///
    /// HAFIF DOKUNUS
    /// Ses calma yollari iki katmanli korumalidir:
    /// 1) <c>Application.isPlaying</c> degilse hicbir ses uretilmez. EditMode
    ///    testleri bu sayede PlayOneShot'in "audio source disabled" hatasini
    ///    hic gormez.
    /// 2) Sahnede <see cref="AudioListener"/> yoksa yalnizca ONCE bir uyari
    ///    yazilir (her karede degil) ve o da LogType.Warning'tir; oyun akisi
    ///    ve testler kirmizi olmaz.
    /// </summary>
    [AddComponentMenu("KargoSim/Audio Director")]
    [DisallowMultipleComponent]
    public sealed class AudioDirector : MonoBehaviour
    {
        [Header("Ayarlar")]
        [Tooltip("Ana ses seviyesi (0 = sessiz, 1 = tam).")]
        [Range(0f, 1f)]
        [SerializeField] private float masterVolume = 0.8f;

        [Tooltip("Baslangicta sessiz mi? HUD'daki M tusu bunu degistirir.")]
        [SerializeField] private bool muted;

        [Header("Motor")]
        [Tooltip("Motor kaynaginin taban seviyesi. Efektlere gore dusuk: " +
                 "motor surekli calistigi icin kafayi yormamalidir.")]
        [Range(0f, 1f)]
        [SerializeField] private float engineVolume = 0.12f;

        [Tooltip("Arac duruyorken motor pitch'i.")]
        [Range(0.5f, 1.5f)]
        [SerializeField] private float engineIdlePitch = 0.55f;

        [Tooltip("Azami hizda motor pitch'i.")]
        [Range(1f, 2.5f)]
        [SerializeField] private float engineMaxPitch = 1.75f;

        [Header("Zamanlama")]
        [Tooltip("Iki 'kargo hareketi' sesi arasindaki en kisa sure. Yukleme " +
                 "aninda ParcelLoaded VE CargoChanged ayni karede tetiklenir; " +
                 "bu koruma ikisinin tek kutu sesi olarak duyulmasini engeller.")]
        [Min(0f)]
        [SerializeField] private float cargoCueMinInterval = 0.08f;

        [Tooltip("Otomatik adim sesi araligi (yalnizca SetWalking(true) iken).")]
        [Min(0.1f)]
        [SerializeField] private float footstepInterval = 0.42f;

        // ------------------------------------------------------------------
        // Durum
        // ------------------------------------------------------------------

        private AudioSource _sfx;
        private AudioSource _engine;
        private ArcadeVehicleController _vehicle;

        private bool _initialized;
        private bool _subscribed;
        private bool _warnedNoListener;
        private bool _listenerPresent;
        private float _nextListenerCheck;

        private bool _engineActive;
        private float _enginePitch01;

        /// <summary>Tick ile ilerletilen oyun ici saat (saniye).</summary>
        private float _clock;
        private float _lastCargoCueTime = -1000f;
        private float _footstepTimer;
        private bool _walking;

        /// <summary>
        /// Ayakta olan yonetici sayisi. DIAGNOSTIK + TEST icin:
        /// sahne icinde iki AudioDirector birden varsa (kopyala/yapistir
        /// tuzaklari) oyun sesleri iki kez calar ve bunu oyuncuya
        /// soylememiz gerekir.
        /// </summary>
        public static int LiveInstanceCount { get; private set; }

        /// <summary>Ana ses seviyesi (0-1). Atandiginda kaynaklara islenir.</summary>
        public float MasterVolume
        {
            get => masterVolume;
            set
            {
                masterVolume = Mathf.Clamp01(value);
                ApplyVolumes();
            }
        }

        public bool IsMuted => muted;

        public bool IsInitialized => _initialized;

        /// <summary>Islenen cue sayisi. Testlerin sayac burasiyla tutulur.</summary>
        public int TotalCuesPlayed { get; private set; }

        /// <summary>En son calinan cue. TotalCuesPlayed == 0 ise gecersizdir.</summary>
        public AudioCue LastCue { get; private set; }

        public AudioSource SfxSource => _sfx;
        public AudioSource EngineSource => _engine;

        public bool IsEngineActive => _engineActive;

        /// <summary>Motorun hiza gore normalize girisi (0 = duruyor, 1 = max hiz).</summary>
        public float EnginePitch01 => _enginePitch01;

        // ------------------------------------------------------------------
        // Yasam dongusu
        // ------------------------------------------------------------------

        private void Awake()
        {
            Initialize();
        }

        private void OnDestroy()
        {
            // Domain Reload KAPALIYKEN statik event'ler oyuncu cikarken
            // temizlenmez. Bu satiri yazmazsak her Play'de olu (olmus)
            // bir dinleyici kuyruga birikir ve sesler giderek cogalir.
            Unsubscribe();
            StopEngine();

            if (_initialized) LiveInstanceCount--;
            _initialized = false;
        }

        /// <summary>
        /// Herkese acik kurulum. Awake cagrilir; EditMode testlerinde Awake
        /// calismadigi icin testler bunu dogrudan cagirir.
        /// Iki kez cagrilmasi GUVENLIDIR (cift abone olmaz).
        /// </summary>
        public void Initialize()
        {
            EnsureSources();

            if (!_initialized)
            {
                _initialized = true;
                LiveInstanceCount++;
            }

            Subscribe();
        }

        private void EnsureSources()
        {
            if (_sfx == null) _sfx = gameObject.GetComponent<AudioSource>();
            if (_sfx == null) _sfx = gameObject.AddComponent<AudioSource>();

            if (_engine == null)
            {
                AudioSource[] existing = gameObject.GetComponents<AudioSource>();
                _engine = existing.Length > 1 ? existing[1] : gameObject.AddComponent<AudioSource>();
            }

            Configure2D(_sfx, false);
            Configure2D(_engine, true);
            ApplyVolumes();
        }

        /// <summary>Bu oyunda 2D yeterli: kaynak konumdan bagimsiz calisin.</summary>
        private static void Configure2D(AudioSource source, bool looping)
        {
            if (source == null) return;
            source.playOnAwake = false;
            source.loop = looping;
            source.spatialBlend = 0f;      // 2D
            source.dopplerLevel = 0f;      // hareket eden kamerada titresin
        }

        // ------------------------------------------------------------------
        // Olay aboneligi
        // ------------------------------------------------------------------

        /// <summary>Oyun olay busuna abone olur. Idempotent.</summary>
        public void Subscribe()
        {
            if (_subscribed) return;

            GameEvents.ParcelLoaded += OnParcelLoaded;
            GameEvents.CargoChanged += OnCargoChanged;
            GameEvents.ParcelDelivered += OnParcelDelivered;
            GameEvents.PayoutApplied += OnPayoutApplied;
            GameEvents.LevelUp += OnLevelUp;

        // Asama 13: kapi calma -> vurus; NPC cevap verdi -> selam + kod.
        GameEvents.DoorKnocked += OnDoorKnocked;
        GameEvents.NPCAnswered += OnNpcAnswered;

            _subscribed = true;
        }

        /// <summary>
        /// Olay busundan ayrilir. Idempotent; OnDestroy bunu cagirir.
        /// Kutu alinip birakilirsa sistem sessizce oturur, hata vermez.
        /// </summary>
        public void Unsubscribe()
        {
            if (!_subscribed) return;

            GameEvents.ParcelLoaded -= OnParcelLoaded;
            GameEvents.CargoChanged -= OnCargoChanged;
            GameEvents.ParcelDelivered -= OnParcelDelivered;
            GameEvents.PayoutApplied -= OnPayoutApplied;
            GameEvents.LevelUp -= OnLevelUp;
        GameEvents.DoorKnocked -= OnDoorKnocked;
        GameEvents.NPCAnswered -= OnNpcAnswered;

            _subscribed = false;
        }

        private void OnParcelLoaded(Parcel parcel)
        {
            Emit(AudioCue.AracaKoy);
            // Ayni fiziksel eylemin ikinci olayi: bir sonraki ses yutulur.
            SuppressCargoCue();
        }

        private void OnCargoChanged()
        {
            // Yukleme aninda ParcelLoaded ile ayni karede tetiklenir; iki ses
            // ust uste binerse "kutu kutu" gibi duyulur.
            if (_clock - _lastCargoCueTime < cargoCueMinInterval) return;

            _lastCargoCueTime = _clock;
            Emit(AudioCue.KutuAl);
        }

        private void OnParcelDelivered(Parcel parcel, DeliveryOutcome outcome)
        {
            // Late (gec teslim) de teslimdir: musteri kargoyu aldi, sadece
            // odeme kismi eksik. Bu yuzden basari sesi calinir; basarisiz
            // olan yalnizca Failed (kargo hic teslim edilemedi).
            Emit(outcome == DeliveryOutcome.Failed
                ? AudioCue.TeslimBasarisiz
                : AudioCue.TeslimBasarili);
            SuppressCargoCue();
        }

        private void OnPayoutApplied(Payout payout, DeliveryOutcome outcome)
        {
            // Para kazanilmadiginda (0 veya ceza) para sesi calinmaz;
            // aksi halde her basarisiz teslimde "odeme" sesi duyulurdu.
            if (payout.TotalMoney <= 0) return;
            Emit(AudioCue.ParaKazanildi);
        }

        private void OnLevelUp(int level)
        {
            Emit(AudioCue.SeviyeYukseldi);
        }

        /// <summary>Asama 13 - kapi calindi.</summary>
        private void OnDoorKnocked(int destinationId)
        {
            Emit(AudioCue.KapiCal);
        }

        /// <summary>
        /// Asama 13 - NPC kapidi acti ve kodu soyluyor.
        ///
        /// <paramref name="code"/> dogrudan <c>AddressDoor</c>'dan gelir ve
        /// <c>Parcel.DeliveryCode</c> ile ayni degerdir (AddressDoor kodu
        /// kargodan okur). Burada YENIDEN uretilmez; boylece ses ile
        /// soylenen kod ile dogrulanan kod ayrilamaz.
        /// </summary>
        private void OnNpcAnswered(int destinationId, string npcName, int code)
        {
            PlayCue(AudioCue.NpcSelam);

            AudioClip tones = ProceduralAudio.GetCodeToneClip(code);
            if (_sfx != null && tones != null) _sfx.PlayOneShot(tones, 0.9f);
        }

        private void SuppressCargoCue()
        {
            _lastCargoCueTime = _clock;
        }

        // ------------------------------------------------------------------
        // Ses calma
        // ------------------------------------------------------------------

        /// <summary>
        /// Cue calar. <see cref="AudioCue.AracMotoru"/> ozel: tek seferlik
        /// efekt degil, donguseldir; motoru acar.
        /// </summary>
        /// <summary>
        /// NPC'nin teslimat kodunu sesli soyler.
        ///
        /// Sira: once kapi Calinir, sonra selam, sonra kod. Oyuncu
        /// duyduklarini sirayla hatirlar.
        /// </summary>
        public void PlayDoorSequence(int code)
        {
            if (_sfx == null) return;

            PlayCue(AudioCue.KapiCal);
            PlayCue(AudioCue.NpcSelam);

            AudioClip tones = ProceduralAudio.GetCodeToneClip(code);
            if (tones != null) _sfx.PlayOneShot(tones, 0.9f);
        }

        public void PlayCue(AudioCue cue)
        {
            if (cue == AudioCue.AracMotoru)
            {
                SetEngineActive(true);
                return;
            }

            Emit(cue);
        }

        /// <summary>Bir adim sesi calar (TICK'TEN BAGIMSIZ, anlik).</summary>
        public void PlayFootstep()
        {
            Emit(AudioCue.AyakSesi);
        }

        /// <summary>
        /// Merkezi ses cikisi. Sirasiyla: mute -> sayac -> Play guard ->
        /// dinleyici -> PlayOneShot.
        ///
        /// SAYAC Play guard'INDAN ONCE artirilir. Boylece EditMode'da ses
        /// uretilmese bile "hangi olay hangi cue'a gitti" dogrulanabilir;
        /// testler duyamadan da karar zincirini olcer.
        /// </summary>
        private void Emit(AudioCue cue)
        {
            if (muted) return;

            TotalCuesPlayed++;
            LastCue = cue;

            // HAFIF DOKUNUS 1: EditMode'da ses calinmaz. PlayOneShot
            // calismayan bir sahne yuzunden hata loglar ve testleri kirletir.
            if (!Application.isPlaying) return;
            if (_sfx == null) return;   // Initialize cagrilmadi

            // HAFIF DOKUNUS 2: dinleyici yoksa uyar ama cokme.
            if (!ListenerPresent()) return;

            AudioClip clip = ProceduralAudio.GetClip(cue);
            if (clip == null) return;

            _sfx.PlayOneShot(clip);
        }

        private bool ListenerPresent()
        {
            if (_clock < _nextListenerCheck) return _listenerPresent;
            _nextListenerCheck = _clock + 1f;
            _listenerPresent = FindAnyObjectByType<AudioListener>() != null;

            if (!_listenerPresent && !_warnedNoListener)
            {
                _warnedNoListener = true;
                Debug.LogWarning("[AudioDirector] Sahne'de AudioListener yok; " +
                                 "sesler uretilir ama duyulmaz.");
            }

            return _listenerPresent;
        }

        // ------------------------------------------------------------------
        // Ayarlar
        // ------------------------------------------------------------------

        public void SetMuted(bool value)
        {
            muted = value;

            // Susturulunca motoru de durdur: dongu bitmeye devam ederse
            // susturduktan sonra sessizce calmaya devam eder.
            if (muted) StopEngine();
            else if (_engineActive && Application.isPlaying) PlayEngine();

            ApplyVolumes();
        }

        public bool ToggleMute()
        {
            SetMuted(!muted);
            return muted;
        }

        private void ApplyVolumes()
        {
            if (_sfx != null) _sfx.volume = muted ? 0f : masterVolume;
            if (_engine != null) _engine.volume = muted ? 0f : engineVolume * masterVolume;
        }

        // ------------------------------------------------------------------
        // Motor
        // ------------------------------------------------------------------

        /// <summary>Motoru acar/kapatir. Kapatma clip'i de temizler.</summary>
        public void SetEngineActive(bool active)
        {
            _engineActive = active;
            if (_engine == null) return;

            _engine.loop = true;
            _engine.clip = active ? ProceduralAudio.GetClip(AudioCue.AracMotoru) : null;

            if (!active)
            {
                if (Application.isPlaying) _engine.Stop();
                _enginePitch01 = 0f;
                _engine.pitch = engineIdlePitch;
                return;
            }

            _engine.pitch = Mathf.Lerp(engineIdlePitch, engineMaxPitch, _enginePitch01);
            if (Application.isPlaying) PlayEngine();
        }

        private void PlayEngine()
        {
            if (_engine == null || _engine.clip == null) return;
            if (muted) return;
            if (!_engine.isPlaying) _engine.Play();
        }

        public void StopEngine()
        {
            _engineActive = false;
            if (_engine == null) return;
            if (Application.isPlaying) _engine.Stop();
        }

        /// <summary>
        /// Motor pitch girdisi (0 = duruyor, 1 = max hiz). Harici arac
        /// okunmadan da elle surulebilir.
        /// </summary>
        public void SetEnginePitch01(float t)
        {
            t = Mathf.Clamp01(t);
            _enginePitch01 = t;
            if (_engine == null) return;
            _engine.pitch = Mathf.Lerp(engineIdlePitch, engineMaxPitch, t);
        }

        /// <summary>Araci baglar. Arayuz Tick ile surulur; motoru o okur.</summary>
        public void BindVehicle(ArcadeVehicleController vehicle)
        {
            _vehicle = vehicle;
        }

        // ------------------------------------------------------------------
        // Zamanlama
        // ------------------------------------------------------------------

        /// <summary>
        /// Ayak seslerini otomatik uretir. Acik/kapali; acikken Tick her
        /// <see cref="footstepInterval"/> saniyede bir adim sesi calar.
        /// </summary>
        public void SetWalking(bool walking)
        {
            if (_walking == walking) return;
            _walking = walking;
            _footstepTimer = walking ? footstepInterval : 0f;
        }

        /// <summary>
        /// Tek zaman dalgasi. Projede Update yazilmaz; buradaki butun
        /// zamanlama (adim araligi, motor takibi, olay bastirma penceresi)
        /// bu cagri ile ilerler.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (deltaTime > 0f) _clock += deltaTime;

            FollowVehicleSpeed();

            if (!_walking) return;
            _footstepTimer -= deltaTime;
            if (_footstepTimer > 0f) return;

            PlayFootstep();
            _footstepTimer = footstepInterval;
        }

        /// <summary>
        /// Aracin hizini maxSpeed'e bolup motor pitch'ine cevirir.
        /// SpeedKmh km/sa, maxSpeed m/s: once birimi cevir, sonra normalize et.
        /// </summary>
        private void FollowVehicleSpeed()
        {
            if (_vehicle == null) return;

            VehicleDefinition definition = _vehicle.Definition;
            float maxSpeedMs = definition != null ? Mathf.Max(definition.maxSpeed, 1f) : 30f;
            float maxKmh = maxSpeedMs * 3.6f;
            if (maxKmh < 0.01f) return;

            float ratio = Mathf.Clamp01(_vehicle.SpeedKmh / maxKmh);
            SetEnginePitch01(ratio);

            // Hareket varken motor kendiliginden acilir (araca binilince
            // SetEngineActive cagrilmasina gerek kalmasin). Durulunca kapatmaz:
            // arac sahne disinda da motor rahat durur.
            if (ratio > 0.02f && !_engineActive) SetEngineActive(true);
        }

        /// <summary>Statik sayaci Play basinda sifirlar (domain reload kapaliyken).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            LiveInstanceCount = 0;
        }

        /// <summary>
        /// Statik sayaci sifirlar.
        ///
        /// Neden ayrica bir kapak: yukaridaki ResetStatics YALNIZCA Play
        /// basinda (RuntimeInitialize) calisir. EditMode test kosucusunda
        /// oyun hic baslamadigi icin sayac testler arasi birikir ve
        /// "kac yonetici yasiyor" kontrolu anlamini yitirir.
        /// </summary>
        public static void ResetInstanceCountForTests() => LiveInstanceCount = 0;
    }
}
