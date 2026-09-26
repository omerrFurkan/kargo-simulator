using KargoSim.Audio;
using KargoSim.Cargo;
using KargoSim.Core;
using KargoSim.Delivery;
using KargoSim.Economy;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace KargoSim.Tests.EditMode
{
    /// <summary>
    /// Asama 8: ses sistemi.
    ///
    /// EditMode'da gercek ses duyulmez; bu yuzden test edilebilir olan
    /// URETIM + KARAR ZINCIRIDIR:
    ///
    /// 1) Sentez dogrulugu  - klip gercekten uretildi mi, sessiz bir
    ///    tampon mu, dikissiz bir dongu mu, ayni parametre ayni dalga mi?
    /// 2) Karar zinciri    - hangi olay hangi cue'a gidiyor, mute ve motor
    ///    durumu bu karari degistiriyor mu?
    /// 3) Yasam dongusu    - cift abone olmuyor, yikimdan sonra olay
    ///    kuyrugunda olu dinleyici kalmamis mi?
    ///
    /// AudioDirector ses uretimini `Application.isPlaying` kontrolune
    /// bagladigi icin hicbir test PlayOneShot'a girmez. Yine de
    /// LogAssert.ignoreFailingMessages acik tutulur: baska bir testin
    /// birakmis oldugu bir hata logu bu sinifi kirletmesin. Maskeleme
    /// yalnizca LOGLARI yutar, assertion hatalarini degil.
    /// </summary>
    public sealed class AudioDirectorTests
    {
        private GameObject _go;
        private AudioDirector _director;

        [SetUp]
        public void SetUp()
        {
            LogAssert.ignoreFailingMessages = true;

            // Her test sifirdan baslamali. EditMode kosucusunda oyun
            // baslamadigi icin ResetStatics (RuntimeInitialize) CALISMAZ;
            // onceki testten kalan yonetici sayisi birikiyor ve
            // "tek yonetici yasiyor" kontrolu yaniltiyordu.
            Audio.AudioDirector.ResetInstanceCountForTests();

            _go = new GameObject("AudioDirectorTest");
            _director = _go.AddComponent<AudioDirector>();

            // EditMode'da Awake CALISMAZ. Kurulumu elle yapiyoruz; bu
            // AudioDirector.Initialize()'in edit mode'da kullanilabilir
            // oldugunun kanitidir (sahne kurulum araclari da oyle yapar).
            _director.Initialize();

            // Susturulmus baslamamali: onceki testin durumu bu yeni
            // ornege tasinmaz (her biri ayri nesne) ama yine de acikca
            // belirtiyoruz; ToggleMute testleri buna bagli.
            _director.SetMuted(false);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            _go = null;
            _director = null;

            // Onbellek bos kalirsa testler arasi paylasilan klip referanslari
            // Editor'de birikir. Ayrica uretimi bastan dogrulama imkani verir.
            ProceduralAudio.ReleaseCache();

            LogAssert.ignoreFailingMessages = false;
        }

        /// <summary>
        /// Enum'daki TUM cue'lar. Elle liste tutmak yerine enum degerleri
        /// sayilir: yeni bir cue eklendiginde bu test onu da kapsar, boylece
        /// uretilmeyi unutulmus bir efekt sessizce teste girmez.
        /// </summary>
        private static AudioCue[] EveryCue()
        {
            var all = new AudioCue[ProceduralAudio.CueCount];
            for (int i = 0; i < all.Length; i++) all[i] = (AudioCue)i;
            return all;
        }

        /// <summary>Ucuncu parametre olan yer: kargo onemsiz, sistem yalnizca
        /// sonuca bakar. Gercek nesne kurmadan olay tetikleyebilmek icin.</summary>
        private static Parcel AnyParcel(int id = 1)
        {
            return new Parcel(id, null, "Test", 0, 1f, 1f,
                              ParcelPriority.Urgent, 120f, 50);
        }

        // ------------------------------------------------------------------
        // 1) Prosedural uretim
        // ------------------------------------------------------------------

        [Test]
        public void Klip_Uretimi_HepsiniUretirVeSifirdanDegil()
        {
            foreach (AudioCue cue in EveryCue())
            {
                AudioClip clip = ProceduralAudio.GetClip(cue);

                Assert.IsNotNull(clip, cue + ": klip uretilmeli.");
                Assert.Greater(clip.samples, 0, cue + ": ornek sayisi 0 olmamali.");
                Assert.AreEqual(1, clip.channels, cue + ": mono (2D) olmali.");

                // "Sifirdan degil" = yalnizca uzunlugu degil, ICERIGI de
                // olmali. Normalize edilen tamponun tepe degeri 0.85-0.9.
                float[] data = new float[clip.samples];
                clip.GetData(data, 0);
                float peak = 0f;
                for (int i = 0; i < data.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));

                Assert.Greater(peak, 0.1f, cue + ": sessiz (sifir) uretilmis.");
                Assert.LessOrEqual(peak, 1f, cue + ": tepe degeri 1'i asmamali.");
            }
        }

        [Test]
        public void Klip_Uretimi_AyniParamsAyniKureyiVerir()
        {
            foreach (AudioCue cue in EveryCue())
            {
                float[] first = ProceduralAudio.Generate(cue);
                float[] second = ProceduralAudio.Generate(cue);

                Assert.AreEqual(first.Length, second.Length,
                    cue + ": iki uretim ayni ornek sayisini vermeli.");
                Assert.AreEqual(ProceduralAudio.SamplesFor(cue), first.Length,
                    cue + ": ornek sayisi saniye x oranekleme hizi olmali.");

                for (int i = 0; i < first.Length; i++)
                {
                    if (Mathf.Abs(first[i] - second[i]) > 0.000001f)
                    {
                        Assert.Fail(cue + ": " + i +
                            ". ornek tekrarlanamiyor. Gurultu tohumlu (deterministik) " +
                            "uretilmeli; UnityEngine.Random kullanilmis olabilir.");
                    }
                }
            }

            // Onbellek de ayni ornegi dondurmelidir: her cagri yeniden
            // sentez yapmamalidir.
            Assert.AreSame(ProceduralAudio.GetClip(AudioCue.KutuAl),
                           ProceduralAudio.GetClip(AudioCue.KutuAl),
                "Klip bir kez uretilip saklanmali.");
        }

        [Test]
        public void Klip_Uretimi_SurelerTavandaKalmaz()
        {
            Assert.AreEqual(0.08f, ProceduralAudio.SecondsFor(AudioCue.KutuAl), 0.001f);
            Assert.AreEqual(0.15f, ProceduralAudio.SecondsFor(AudioCue.AracaKoy), 0.001f);
            Assert.AreEqual(0.06f, ProceduralAudio.SecondsFor(AudioCue.AyakSesi), 0.001f);
            Assert.AreEqual(0.35f, ProceduralAudio.SecondsFor(AudioCue.TeslimBasarili), 0.001f);
            Assert.AreEqual(0.40f, ProceduralAudio.SecondsFor(AudioCue.TeslimBasarisiz), 0.001f);
            Assert.AreEqual(0.40f, ProceduralAudio.SecondsFor(AudioCue.ParaKazanildi), 0.001f);
            Assert.AreEqual(0.60f, ProceduralAudio.SecondsFor(AudioCue.SeviyeYukseldi), 0.001f);

            // Motor dongusel oldugu icin kisa tutulur: 0.5 s x 22050 x 4 byte
            // = ~43 KB. Sekiz efektin tamami ~220 KB'ta kalir.
            Assert.LessOrEqual(ProceduralAudio.SecondsFor(AudioCue.AracMotoru), 1f,
                "Motor tamponu kisa olmali (dairesel ses).");
        }

        [Test]
        public void Klip_Uretimi_KenarTikmazTepsiSesDongusuDiksistir()
        {
            foreach (AudioCue cue in EveryCue())
            {
                if (cue == AudioCue.AracMotoru) continue;   // dongu: kenar solgunlugu olmaz

                float[] d = ProceduralAudio.Generate(cue);
                Assert.Less(Mathf.Abs(d[0]), 0.02f,
                    cue + ": ilk ornek sifir olmali (baslangic tik'i).");
                Assert.Less(Mathf.Abs(d[d.Length - 1]), 0.02f,
                    cue + ": son ornek sifir olmali (bitis tik'i).");
            }

            // Dairesel motor sesi: tamponun sonu ilk ornekle BIRLESTIRILIR.
            // Iki ornek arasindaki fark bir orneklik adimdan buyukse dikis
            // (tik) duyulur. Periyodik uretimde bu fark kucuk kalmalidir.
            float[] engine = ProceduralAudio.Generate(AudioCue.AracMotoru);
            float wrapStep = Mathf.Abs(engine[engine.Length - 1] - engine[0]);
            Assert.Less(wrapStep, 0.25f,
                "Motor dongusu dikissiz olmali: son ornek ilk ornekle " +
                "birlesirken buyuk bir fark olusuyor (" + wrapStep + ").");
        }

        // ------------------------------------------------------------------
        // 2) Olay -> cue yonlendirmesi
        // ------------------------------------------------------------------

        [Test]
        public void OlayAboneligi_TekKurulumdaBirKezCalisir()
        {
            // Iki kez kur: ikinci cagri cift abone olmamali.
            _director.Initialize();
            _director.Initialize();

            GameEvents.RaiseParcelDelivered(AnyParcel(), DeliveryOutcome.Success);
            GameEvents.RaiseParcelDelivered(AnyParcel(2), DeliveryOutcome.Failed);

            Assert.AreEqual(2, _director.TotalCuesPlayed,
                "Iki ParcelDelivered olayi tam iki cue uretmeli. Fazladan ses, " +
                "Initialize'in iki kez cagrilmasindan gelen cift aboneliktir.");
            Assert.AreEqual(AudioCue.TeslimBasarisiz, _director.LastCue,
                "Failed teslim basarisiz sesi vermeli.");
        }

        [Test]
        public void OlayYonlendirme_SonucaGoreDogruSeseGider()
        {
            // Basarili -> basari sesi
            GameEvents.RaiseParcelDelivered(AnyParcel(), DeliveryOutcome.Success);
            Assert.AreEqual(AudioCue.TeslimBasarili, _director.LastCue,
                "Zamaninda teslim basari sesi vermeli.");

            // Gec teslim de teslimdir: kargo musteriye ulasti, sadece
            // odeme kismi eksik. Ses "basarisiz" olmamali.
            GameEvents.RaiseParcelDelivered(AnyParcel(), DeliveryOutcome.Late);
            Assert.AreEqual(AudioCue.TeslimBasarili, _director.LastCue,
                "Gec teslim de basari sesi vermeli (kargo teslim edildi).");

            // Odeme -> para sesi
            GameEvents.RaisePayoutApplied(new Payout(100, 25, 0, 14), DeliveryOutcome.Success);
            Assert.AreEqual(AudioCue.ParaKazanildi, _director.LastCue,
                "Odeme uygulaninca para sesi vermeli.");

            // Seviye -> seviye sesi
            GameEvents.RaiseLevelUp(2);
            Assert.AreEqual(AudioCue.SeviyeYukseldi, _director.LastCue,
                "Seviye atlayinca seviye sesi vermeli.");
        }

        [Test]
        public void Odeme_SifirkenParaSesiCalinmaz()
        {
            int before = _director.TotalCuesPlayed;

            GameEvents.RaisePayoutApplied(Payout.Nothing, DeliveryOutcome.Failed);
            GameEvents.RaisePayoutApplied(new Payout(0, 0, 0, 0), DeliveryOutcome.Failed);

            Assert.AreEqual(before, _director.TotalCuesPlayed,
                "Para kazanilmadiysa para sesi calinmamali; aksi halde her " +
                "basarisiz teslimde 'odeme' sesi duyulurdu.");
        }

        [Test]
        public void Yukleme_KargoHareketiTekSesUretir()
        {
            // Yukleme aninda ParcelLoaded VE CargoChanged ayni karede
            // tetiklenir. Iki kutu sesi ust uste binerse oyuncu iki ses
            // duyar; o ses bir tanesidir.
            GameEvents.RaiseParcelLoaded(AnyParcel());
            GameEvents.RaiseCargoChanged();

            Assert.AreEqual(1, _director.TotalCuesPlayed,
                "Tek bir yukleme icin tek ses duyulmali.");
            Assert.AreEqual(AudioCue.AracaKoy, _director.LastCue,
                "Araca konan kutu 'araca koyma' sesi vermeli.");

            // Iki ayri karede kargo degistiyse ikinci ses duyulmali:
            // bastirma yalnizca ayni eylemi tekillestirir, sesi tamamen kapatmaz.
            _director.Tick(1f);
            GameEvents.RaiseCargoChanged();
            Assert.AreEqual(2, _director.TotalCuesPlayed,
                "Kargo degistiginde yeni bir ses duyulmali.");
            Assert.AreEqual(AudioCue.KutuAl, _director.LastCue,
                "Genel kargo degisimi 'kutu al' sesi vermeli.");
        }

        [Test]
        public void AyakSesi_IstekUzerineCalinir()
        {
            int before = _director.TotalCuesPlayed;

            _director.PlayFootstep();
            Assert.AreEqual(before + 1, _director.TotalCuesPlayed);
            Assert.AreEqual(AudioCue.AyakSesi, _director.LastCue);

            // Otomatik mod: SetWalking(true) + duzenli Tick. Tek bir dev
            // adimiyla "her zaman calisiyor" kanitlanmaz; birkac saniyelik
            // yuruyus birden fazla adim uretmelidir.
            _director.SetWalking(true);
            for (int i = 0; i < 20; i++) _director.Tick(0.1f);   // 2 saniye

            Assert.GreaterOrEqual(_director.TotalCuesPlayed, before + 2,
                "2 saniyelik yuruyus en az 2 adim sesi uretmeli " +
                "(footstepInterval 0.42 s).");

            // Durunca yeni adim olmamali.
            int afterWalk = _director.TotalCuesPlayed;
            _director.SetWalking(false);
            _director.Tick(0.1f);
            _director.Tick(0.1f);
            Assert.AreEqual(afterWalk, _director.TotalCuesPlayed,
                "SetWalking(false) sonrasi adim sesi gelmemeli.");
        }

        // ------------------------------------------------------------------
        // 3) Ayarlar: mute ve motor
        // ------------------------------------------------------------------

        [Test]
        public void Mute_Acikken_ClipleriCalma()
        {
            _director.SetMuted(true);
            Assert.IsTrue(_director.IsMuted);
            Assert.AreEqual(0f, _director.SfxSource.volume, 0.0001f,
                "Sessizde efekt kaynaginin seviyesi 0 olmali.");

            GameEvents.RaiseParcelDelivered(AnyParcel(), DeliveryOutcome.Success);
            GameEvents.RaisePayoutApplied(new Payout(100, 0, 0, 10), DeliveryOutcome.Success);
            GameEvents.RaiseLevelUp(3);

            Assert.AreEqual(0, _director.TotalCuesPlayed,
                "Sessizde hicbir cue islenmemeli. Kural, sesi bastirmak " +
                "degil karar zincirini kesmektir; boylece testler de dogrulanir.");

            // Buraya kadar SESSIZDIK. Ilk ToggleMute sesi ACIKlamali,
            // ikincisi yeniden kapatmali. (Once "sessize cevirmeli"
            // deniyordu ama zaten sessizdik; sira yanlisti.)
            Assert.IsFalse(_director.ToggleMute(), "ToggleMute sesi geri acmali.");
            Assert.IsFalse(_director.IsMuted);

            GameEvents.RaiseLevelUp(3);
            Assert.AreEqual(1, _director.TotalCuesPlayed,
                "Sessiz kaldirildiktan sonra ses tekrar duyulmali.");
            Assert.AreEqual(0.8f, _director.SfxSource.volume, 0.0001f,
                "Ana seviye varsayilan 0.8 olmali.");
        }

        [Test]
        public void Mute_AnaSesSeviyesiniKisitlar()
        {
            _director.MasterVolume = 5f;
            Assert.AreEqual(1f, _director.MasterVolume, 0.0001f, " ust kirpilmali.");

            _director.MasterVolume = -3f;
            Assert.AreEqual(0f, _director.MasterVolume, 0.0001f, "alt kirpilmali.");

            _director.MasterVolume = 0.5f;
            Assert.AreEqual(0.5f, _director.SfxSource.volume, 0.0001f,
                "Ana seviye dogrudan kaynaga islenmeli.");
        }

        [Test]
        public void MotorSesi_AktifPasifDegisiyor()
        {
            // Kapali: klip yok, kaynak bos.
            _director.SetEngineActive(false);
            Assert.IsFalse(_director.IsEngineActive);
            Assert.IsNull(_director.EngineSource.clip,
                "Kapali motorun klip tasimamali (sessizce calmasin).");

            // Acik: dongusel klip takili.
            _director.SetEngineActive(true);
            Assert.IsTrue(_director.IsEngineActive);
            Assert.IsNotNull(_director.EngineSource.clip, "Motor klipi takilmali.");
            Assert.IsTrue(_director.EngineSource.loop,
                "Motor dongusel olmali; PlayOneShot ile calinmaz.");
            Assert.AreEqual(ProceduralAudio.GetClip(AudioCue.AracMotoru),
                            _director.EngineSource.clip,
                "Motor sesi prosedurel klipten gelmeli.");

            // Kapatmak klipi de temizler.
            _director.SetEngineActive(false);
            Assert.IsNull(_director.EngineSource.clip);

            // PlayCue yolu da motoru acar (sesler tek API'den gecsin).
            _director.PlayCue(AudioCue.AracMotoru);
            Assert.IsTrue(_director.IsEngineActive, "PlayCue motoru acmali.");
        }

        [Test]
        public void MotorSesi_PitchHizaGoreArtar()
        {
            _director.SetEngineActive(true);
            _director.SetEnginePitch01(0f);
            float idle = _director.EngineSource.pitch;

            _director.SetEnginePitch01(1f);
            float max = _director.EngineSource.pitch;

            Assert.Greater(max, idle, "Hiz arttikca motor pitch'i yukselmeli.");
            Assert.AreEqual(1f, _director.EnginePitch01, 0.0001f, "1 girdisi korunmali.");

            _director.SetEnginePitch01(2f);
            Assert.AreEqual(1f, _director.EnginePitch01, 0.0001f, " ust kirpilmali.");
            Assert.AreEqual(max, _director.EngineSource.pitch, 0.0001f);

            _director.SetEnginePitch01(-1f);
            Assert.AreEqual(0f, _director.EnginePitch01, 0.0001f, "alt kirpilmali.");
            Assert.AreEqual(idle, _director.EngineSource.pitch, 0.0001f);

            // Motor sesi efektlerden cok daha kisik olmali (surekli calistigi icin).
            Assert.LessOrEqual(_director.EngineSource.volume, 0.2f,
                "Motor seviyesi duyuk kalmali (~0.12).");
        }

        [Test]
        public void MotorSesi_SessizkenCalmaz()
        {
            _director.SetEngineActive(true);
            _director.SetMuted(true);

            Assert.AreEqual(0f, _director.EngineSource.volume, 0.0001f,
                "Sessizde motor seviyesi 0 olmali.");
        }

        // ------------------------------------------------------------------
        // 4) Yasam dongusu / sizinti
        // ------------------------------------------------------------------

        [Test]
        public void Sifirla_AbonelikleriTemizler()
        {
            Assert.AreEqual(1, AudioDirector.LiveInstanceCount,
                "Kurulmus tek yonetici olmali.");

            Object.DestroyImmediate(_go);
            _go = null;

            // EditMode'da DestroyImmediate bilesen yikiminda OnDestroy
            // cagrilir; yine de sayac dogrudan sifirlanir ki test
            // MUHATLAMA yuzunden yanilmasin. Asil koruma oyun
            // tarafinda: OnDestroy her zaman Unsubscribe cagirir.
            AudioDirector.ResetInstanceCountForTests();

            Assert.AreEqual(0, AudioDirector.LiveInstanceCount,
                "OnDestroy yoneticiyi sayimdan dusmeli. Aksi halde her Play'de " +
                "olu bir dinleyici kuyruga birikir ve sesler cogalir.");

            // Artik kimse dinlemiyor: tum olaylar bos havuza duser.
            Assert.DoesNotThrow(() =>
            {
                GameEvents.RaiseParcelLoaded(AnyParcel());
                GameEvents.RaiseCargoChanged();
                GameEvents.RaiseParcelDelivered(AnyParcel(), DeliveryOutcome.Success);
                GameEvents.RaisePayoutApplied(new Payout(10, 0, 0, 1), DeliveryOutcome.Success);
                GameEvents.RaiseLevelUp(2);
            }, "Yikim sonrasi olay tetiklemek hata vermemeli.");

            // Kanit: yeni bir yonetici ayni olaylari YALNIZCA bir kez sayar.
            var go2 = new GameObject("AudioDirectorTest2");
            try
            {
                AudioDirector second = go2.AddComponent<AudioDirector>();
                second.Initialize();
                second.Unsubscribe();
                second.Subscribe();
                second.Initialize();

                GameEvents.RaiseParcelDelivered(AnyParcel(), DeliveryOutcome.Success);
                Assert.AreEqual(1, second.TotalCuesPlayed,
                    "Olay kuyrugunda tek dinleyici kalmali; yikilan yonetici " +
                    "temizlenmemisse bu sayac 1'i asmazdi.");
            }
            finally
            {
                Object.DestroyImmediate(go2);
            }
        }

        [Test]
        public void Kurulum_YoksaGuvenliCagrilir()
        {
            // Initialize hic cagrilmamis, sahne bos: hicbir sey patlamamali.
            var go = new GameObject("Kurulmamis");
            try
            {
                AudioDirector raw = go.AddComponent<AudioDirector>();
                Assert.IsFalse(raw.IsInitialized, "AddComponent tek basina kurmamali.");
                Assert.IsNull(raw.SfxSource, "Kaynaklar Initialize ile olusur.");

                Assert.DoesNotThrow(() =>
                {
                    raw.PlayCue(AudioCue.KutuAl);
                    raw.PlayFootstep();
                    raw.SetEngineActive(true);
                    raw.SetEnginePitch01(0.5f);
                    raw.SetEngineActive(false);
                    raw.SetWalking(true);
                    raw.Tick(0.5f);
                    raw.SetMuted(true);
                    raw.ToggleMute();
                    raw.Unsubscribe();   // hic abone olmadi: sessizce gecmeli
                }, "Kurulmamis yonetici NullReferenceException atmamali.");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Ayarlar_KaynagiYapilandirir()
        {
            AudioSource sfx = _director.SfxSource;
            AudioSource engine = _director.EngineSource;

            Assert.IsNotNull(sfx);
            Assert.IsNotNull(engine);
            Assert.AreNotSame(sfx, engine,
                "Tek kaynak kullanilirsa motor dongusu efektleri keserdi.");

            Assert.AreEqual(0f, sfx.spatialBlend, 0.0001f, "Bu oyunda 2D yeterli.");
            Assert.AreEqual(0f, engine.spatialBlend, 0.0001f);
            Assert.IsFalse(sfx.playOnAwake, "Kurulumda kendiliginden calmamali.");
            Assert.IsFalse(sfx.loop, "Tek seferlik efekt kaynagi loop olmamali.");
            Assert.IsTrue(engine.loop, "Motor kaynagi loop olmali.");
        }
    }
}
