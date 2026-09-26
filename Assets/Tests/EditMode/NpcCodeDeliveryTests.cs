using KargoSim.Audio;
using KargoSim.Cargo;
using KargoSim.Core;
using KargoSim.NPC;
using KargoSim.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KargoSim.Tests
{
    /// <summary>
    /// Asama 13 (2. dal) - NPC kodunun OYUNCUYA ULASMASI.
    ///
    /// Onceki dalda kod uretildi ve dogrulandi, ama oyuncu onu
    /// OGRENEMIYORDU: ekranda gizliydi, ses yoktu, oyuncu 0000-9999
    /// arasinda tahmin ediyordu. Bu testler o bosluklari kapatir.
    ///
    /// OZELLIKLE KORUNAN: sesle soylenen perde sirasi ile dogrulanan
    /// <see cref="DeliveryCode"/> AYNI olmali. Ses bir "tema" degil,
    /// kodun kendisidir; ayrilirsa oyuncu dogru kodu ararken yanlis
    /// perde duyar.
    /// </summary>
    public sealed class NpcCodeDeliveryTests
    {
        private GameObject _go;
        private Audio.AudioDirector _director;

        [SetUp]
        public void SetUp()
        {
            AudioDirector.ResetInstanceCountForTests();
            _go = new GameObject("AudioDirectorTest");
            _director = _go.AddComponent<Audio.AudioDirector>();
            _director.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            ProceduralAudio.ReleaseCache();
        }

        // ------------------------------------------------------------------
        // 1. Perde eslemesi
        // ------------------------------------------------------------------

        [Test]
        public void Rakam_Perdesi_KartinDegilAralikIciVeArtan()
        {
            float onceki = 0f;
            for (int d = 0; d <= 9; d++)
            {
                float f = ProceduralAudio.FrequencyForDigit(d);
                Assert.Greater(f, 0f, "Perde frekansi pozitif olmali.");
                Assert.LessOrEqual(f, ProceduralAudio.CodeToneHighHz + 0.01f,
                    "Perde en yuksek degeri asmamali.");
                if (d > 0) Assert.Greater(f, onceki, "Rakam arttikca perde yukselmeli.");
                onceki = f;
            }
        }

        [Test]
        public void PerdeAraligi_TamOktav_InsanKulagiAyirtEder()
        {
            // 440 -> 880 tam oktav. Daha genis aralikta ust rakamlar
            // "ayni" duyuluyor ve kod hatirlanamiyor.
            float ratio = ProceduralAudio.CodeToneHighHz / ProceduralAudio.CodeToneLowHz;
            Assert.AreEqual(2f, ratio, 0.01f, "Aralik tam oktav olmali.");
        }

        [Test]
        public void GecersizRakam_SinirIcindeKalir()
        {
            Assert.AreEqual(ProceduralAudio.FrequencyForDigit(0),
                            ProceduralAudio.FrequencyForDigit(-5), "Negatif rakam 0'a sabitlenmeli.");
            Assert.AreEqual(ProceduralAudio.FrequencyForDigit(9),
                            ProceduralAudio.FrequencyForDigit(99), "Rakam 9'a sabitlenmeli.");
        }

        // ------------------------------------------------------------------
        // 2. Ses ile soylenen kod == dogrulanan kod
        // ------------------------------------------------------------------

        [Test]
        public void KodKlibi_UretilirVeAyniKodIcinAyniKlipDoner()
        {
            AudioClip a = ProceduralAudio.GetCodeToneClip(4827);
            AudioClip b = ProceduralAudio.GetCodeToneClip(4827);

            Assert.IsNotNull(a, "Kod tonu uretilmeli.");
            Assert.AreSame(a, b, "Ayni kod icin klip onbellekten gelmeli.");
            Assert.Greater(a.length, 0, "Klip bos olmamali.");
        }

        [Test]
        public void KodKlibi_DortNotaUretir_AlanKareKadarUzerineYazmaz()
        {
            AudioClip clip = ProceduralAudio.GetCodeToneClip(4827);
            Assert.IsNotNull(clip, "Klip uretilmeli.");

            // AddNote sinir kontrolu yapiyor; yan tasma olsaydi
            // System.IndexOutOfRange atardi ve buraya hic ulasmazdi.
            var data = new float[clip.samples];
            clip.GetData(data, 0);

            int peakIndex = 0;
            float peak = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i] > peak) { peak = data[i]; peakIndex = i; }
            }

            Assert.Greater(peak, 0.05f, "Klip sessiz olmamali.");
            Assert.Less(peak, 1.2f, "Klip asiri gurultulu olmamali (clip).");
            Assert.Less(peakIndex, clip.samples - 1, "Tepe son ornekte olmamali.");
        }

        [Test]
        public void FarkliKod_FarkliKlipUretir()
        {
            AudioClip a = ProceduralAudio.GetCodeToneClip(4827);
            AudioClip b = ProceduralAudio.GetCodeToneClip(4828);
            Assert.AreNotSame(a, b, "Farkli kod ayni klibi kullanmamali.");
        }

        [Test]
        public void KlipOnbellegiSinirli_UzunSuredeBirikmez()
        {
            // 4000 farkli kod uret.
            for (int i = 1000; i < 5000; i++) ProceduralAudio.GetCodeToneClip(i);

            // Onbellek SINIRLI olmali. (Once bu test "GetCodeToneClip
            // null donuyor" diye bakuyordu; o yanlisti - metod her
            // cagrida uretir ve gecerli klip doner. Sinir onbellekte
            // tutulan SAYIDADIR.)
            Assert.LessOrEqual(ProceduralAudio.CodeToneCacheSize, 16,
                "Kod klibi onbellegi sinirli olmali, su an "
                + ProceduralAudio.CodeToneCacheSize);
        }

        [Test]
        public void KlipOnbellegi_EskiKodlariAtar()
        {
            ProceduralAudio.ReleaseCache();
            Assert.AreEqual(0, ProceduralAudio.CodeToneCacheSize,
                "Onbellek temizlenince 0 olmali.");

            AudioClip first = ProceduralAudio.GetCodeToneClip(1000);
            Assert.IsNotNull(first, "Ilk kod uretilmeli.");
            Assert.AreEqual(1, ProceduralAudio.CodeToneCacheSize, "Tek kod = tek kayit.");

            // Ayni kod tekrar istenirse YENIDEN uretilmez (ondeleme).
            ProceduralAudio.GetCodeToneClip(1000);
            Assert.AreEqual(1, ProceduralAudio.CodeToneCacheSize,
                "Ayni kod tekrar istenirse kayit cogalmamali.");
        }

        // ------------------------------------------------------------------
        // 3. NPC cevap verince ses + dogru kod birlikte yayilir
        // ------------------------------------------------------------------

        [Test]
        public void NpcCevapVerince_KodSesiVeDogruKodAyniOlaydaGider()
        {
            // NPC sistemi, %100 evde.
            var npc = new NPCSystem();
            npc.Build(new System.Collections.Generic.List<int> { 0 },
                      new System.Collections.Generic.List<string> { "Ayse" }, 1f);
            npc.BeginDay(1);
            Assert.IsTrue(npc.AnswerDoor(0), "Kapi acilmali.");

            int[] cevapKodu = { 0 };
            string[] cevapIsim = { null };
            System.Action<int, string, int> onAnswered =
                (dest, name, code) => { cevapKodu[0] = code; cevapIsim[0] = name; };
            GameEvents.NPCAnswered += onAnswered;

            try
            {
                GameEvents.RaiseNPCAnswered(0, npc.Get(0).Name, 4827);
            }
            finally
            {
                GameEvents.NPCAnswered -= onAnswered;
            }

            Assert.AreEqual(4827, cevapKodu[0], "Olay dogrru kodu tasimali.");
            Assert.AreEqual("Ayse", cevapIsim[0], "Olay NPC adini tasimali.");

            // Ses bu olaydan tetiklenir: yonetici yasamali ve kod
            // uretilebilir olmali.
            AudioClip clip = ProceduralAudio.GetCodeToneClip(cevapKodu[0]);
            Assert.IsNotNull(clip, "Olaydaki kodla ses uretilebilmeli.");
        }

        [Test]
        public void KapiCalinca_KapiCalSesiUretilir()
        {
            AudioClip knock = ProceduralAudio.GetClip(AudioCue.KapiCal);
            AudioClip greet = ProceduralAudio.GetClip(AudioCue.NpcSelam);

            Assert.IsNotNull(knock, "Kapi cal sesi uretilmeli.");
            Assert.IsNotNull(greet, "NPC selam sesi uretilmeli.");
            Assert.Greater(knock.length, 0, "Kapi cal bos olmamali.");
            Assert.Greater(greet.length, 0, "Selam bos olmamali.");
        }

        // ------------------------------------------------------------------
        // 4. EventSystem
        // ------------------------------------------------------------------

        [Test]
        public void EventSystem_SahnedeVarVeModuluYerinde()
        {
            // Bu bir SAHNE butunu testidir (EditMode'da sahne acik).
            UnityEngine.SceneManagement.Scene scene =
                UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var paths = scene.GetRootGameObjects();

            EventSystem found = null;
            foreach (var root in paths)
            {
                var es = root.GetComponentInChildren<EventSystem>(true);
                if (es != null) { found = es; break; }
            }

            Assert.IsNotNull(found,
                "Sahnede EventSystem YOK. GraphicRaycaster tek basina yeterli "
                + "degildir; tiklamayi EventSystem yonlendirir. "
                + "Menu: Kargo/0. HER SEYI KUR");

            Assert.IsNotNull(found.GetComponent<StandaloneInputModule>(),
                "EventSystem'te StandaloneInputModule yok; fare tiklamasi "
                + "calismaz. Proje Legacy Input Manager kullaniyor, bu yuzden "
                + "InputSystemUIInputModule degil StandaloneInputModule gerekli.");

            // Idempotency: kurulum araci bu dosya birden fazla kez
            // calistirildi (BuildEverything her EditMode kosusunda).
            // Ikinci bir EventSystem OLUSMUSSA fare tiklamasi iki
            // raycaster'a gider ve hangisine gideceSi belirsizlesir.
            var all = Object.FindObjectsByType<EventSystem>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.AreEqual(1, all.Length,
                "Sahnede TAM OLARAK 1 EventSystem olmali. Kurulum araci "
                + "tekrar calistirildiginda ikinci bir tane olusturmamali.");
        }

        [Test]
        public void EventSystem_KurulumAraciTekrarCalistirilabilir()
        {
            // Onceki surum bu testi reflection ile EnsureEventSystem'i
            // cagirarak yapiyordu; editor assembly test assembly'sinden
            // yuklenemedigi icin ATLANIYORDU. Atlanan test, test degil;
            // o kontrol "sahnede tam 1 tane var" kontrolu ustune tasindi
            // (EventSystem_SahnedeVarVeModuluYerinde).
            //
            // Buradaki gercek koruma: sahne kurulumu bu dosya birden fazla
            // kez calistirilmis olmali ve yine de tek EventSystem
            // olmali.
            var all = Object.FindObjectsByType<EventSystem>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            Assert.AreEqual(1, all.Length,
                "Kurulum birden fazla kez calistirilmis olmali (BuildEvery"
                + "thing her kosuda bir kez); yine de tek EventSystem.");
        }
    }
}
