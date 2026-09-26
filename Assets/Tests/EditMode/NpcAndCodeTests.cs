using KargoSim.Cargo;
using KargoSim.NPC;
using NUnit.Framework;
using UnityEngine;

namespace KargoSim.Tests
{
    /// <summary>
    /// Asama 13 - NPC ve teslimat kodu SAF LOGIC testleri.
    ///
    /// Burada sahne, MonoBehaviour veya Unity olay dongusu YOK. Kurallar
    /// (kod turetme, dogrulama, NPC evde/evde degil) sahnesiz dogrulanir.
    /// Sahne tarafi PackagingPlayFlowTests ve NpcDeliveryPlayModeTests'te.
    /// </summary>
    public sealed class NpcAndCodeTests
    {
        // ------------------------------------------------------------------
        // Teslimat kodu
        // ------------------------------------------------------------------

        [Test]
        public void Kod_DortHaneliVeGecerliAralikta()
        {
            for (int id = 1; id <= 200; id++)
            {
                int code = DeliveryCode.Generate(id, 1, 0);
                Assert.GreaterOrEqual(code, DeliveryCode.MinValue, "Kod 1000'den kucuk.");
                Assert.LessOrEqual(code, DeliveryCode.MaxValue, "Kod 9999'dan buyuk.");
                Assert.AreEqual(4, DeliveryCode.Format(code).Length, "Kod 4 hane.");
            }
        }

        [Test]
        public void Kod_AyniKargoIcindeSabitKalir()
        {
            int a = DeliveryCode.Generate(42, 3, 1);
            int b = DeliveryCode.Generate(42, 3, 1);
            Assert.AreEqual(a, b, "Ayni kargo ayni gun ayni kod vermeli.");
        }

        [Test]
        public void Kod_GunDegisinceDegisir()
        {
            int gun1 = DeliveryCode.Generate(42, 1, 0);
            int gun2 = DeliveryCode.Generate(42, 2, 0);
            Assert.AreNotEqual(gun1, gun2,
                "Gun degisince kod degismeli; eski kodla teslimat yapilamaz.");
        }

        [Test]
        public void Kod_TurIndeksineGoreDegisir()
        {
            int tur0 = DeliveryCode.Generate(42, 1, 0);
            int tur3 = DeliveryCode.Generate(42, 1, 3);
            Assert.AreNotEqual(tur0, tur3,
                "Farkli tur ayni kargo kimligiyle ayni kodu vermemeli.");
        }

        [Test]
        public void Kod_FarkliKargolarFarkliKodAlir()
        {
            // 200 kargo icinde cakisma olmamali: ayni kod iki kargoya
            // verilirse oyuncu yanlis kutuyu dogru sanabilir.
            var seen = new System.Collections.Generic.HashSet<int>();
            for (int id = 1; id <= 200; id++)
                seen.Add(DeliveryCode.Generate(id, 1, 0));

            Assert.Greater(seen.Count, 150,
                "200 kargo icinde en fazla 9000 farkli kod olmali; cok az "
                + "cesitlilik cift koda yol acar.");
        }

        [Test]
        public void Dogrulama_DogruKoduKabul()
        {
            int code = DeliveryCode.Generate(7, 1, 0);
            Assert.IsTrue(DeliveryCode.Verify(code, DeliveryCode.Format(code)),
                "Dogru kod kabul edilmeli.");
            Assert.IsTrue(DeliveryCode.Verify(code, " " + code + " "),
                "Bastaki/sondaki bosluk tolere edilmeli.");
        }

        [Test]
        public void Dogrulama_YanlisKoduReddet()
        {
            int code = DeliveryCode.Generate(7, 1, 0);
            int yanlis = code == 1111 ? 2222 : 1111;
            Assert.IsFalse(DeliveryCode.Verify(code, DeliveryCode.Format(yanlis)),
                "Yanlis kod kabul edilmemeli.");
        }

        [Test]
        public void Dogrulama_BosGirisGecersizdir()
        {
            int code = DeliveryCode.Generate(7, 1, 0);
            Assert.IsFalse(DeliveryCode.Verify(code, ""), "Bos kod gecmemeli.");
            Assert.IsFalse(DeliveryCode.Verify(code, "   "), "Bosluk gecmemeli.");
            Assert.IsFalse(DeliveryCode.Verify(code, null), "Null gecmemeli.");
        }

        [Test]
        public void Dogrulama_YazidanAyristirma()
        {
            int code = DeliveryCode.Generate(7, 1, 0);
            string text = DeliveryCode.Format(code);
            int parsed;
            Assert.IsTrue(DeliveryCode.TryParse(text, out parsed), "Gecerli kod okunmali.");
            Assert.AreEqual(code, parsed, "Yazidan okunan kod esit olmali.");

            Assert.IsFalse(DeliveryCode.TryParse("abc", out parsed), "Yazi reddedilmeli.");
            Assert.IsFalse(DeliveryCode.TryParse("12", out parsed), "Kisa kod reddedilmeli.");
            Assert.IsFalse(DeliveryCode.TryParse("10000", out parsed), "Uzun kod reddedilmeli.");
            Assert.IsFalse(DeliveryCode.TryParse("-500", out parsed), "Eksi reddedilmeli.");
        }

        // ------------------------------------------------------------------
        // NPC sistemi
        // ------------------------------------------------------------------

        private static NPCSystem BuildSystem(int count, float chance)
        {
            var ids = new System.Collections.Generic.List<int>();
            var names = new System.Collections.Generic.List<string>();
            for (int i = 0; i < count; i++)
            {
                ids.Add(i);
                names.Add("Musteri " + (i + 1));
            }

            var npc = new NPCSystem();
            npc.Build(ids, names, chance);
            return npc;
        }

        [Test]
        public void Npc_AdresBasinaKayitOlur()
        {
            NPCSystem npc = BuildSystem(14, 0.8f);
            Assert.AreEqual(14, npc.Count, "14 adres = 14 NPC olmali.");
            Assert.IsTrue(npc.HasNpc(0), "Ilk adres kayitli olmali.");
            Assert.IsFalse(npc.HasNpc(99), "Olmayan adres icin kayit olmamali.");
            Assert.IsNull(npc.Get(99), "Olmayan adres null donmeli.");
        }

        [Test]
        public void Npc_KapiCalincaKoduSohler()
        {
            NPCSystem npc = BuildSystem(5, 1f);   // %100 evde
            npc.BeginDay(1);

            Assert.IsTrue(npc.IsHome(0), "Yuzde 100 evde kurali: evde olmali.");
            Assert.IsTrue(npc.AnswerDoor(0), "Kapi acilmali.");
            Assert.AreEqual(NpcState.Answered, npc.Get(0).State, "Durum Answered olmali.");
            Assert.AreEqual(1, npc.AnsweredCount, "Cevap sayaci artmali.");
        }

        [Test]
        public void Npc_EvdeDegilseKapiAcilmaz()
        {
            NPCSystem npc = BuildSystem(5, 0f);   // %0 evde
            npc.BeginDay(1);

            Assert.IsFalse(npc.IsHome(0), "Yuzde 0 evde kurali: evde olmamali.");
            Assert.IsFalse(npc.AnswerDoor(0), "Evde yoksa kapi acilmamali.");
            Assert.AreEqual(NpcState.NotHome, npc.Get(0).State, "Durum NotHome olmali.");
            Assert.AreEqual(1, npc.NotHomeCount, "Bos bulunma sayaci artmali.");
            Assert.AreEqual(0, npc.AnsweredCount, "Cevap sayaci artmamali.");
        }

        [Test]
        public void Npc_EvdeDurumuDeterministik()
        {
            // Ayni gun iki kez kurulunca ayni sonuc: testler ve oyun
            // "bugun musteri evde mi" sorusunda kararli cevap aliyor.
            for (int day = 1; day <= 20; day++)
            {
                var a = BuildSystem(14, 0.7f);
                var b = BuildSystem(14, 0.7f);
                a.BeginDay(day);
                b.BeginDay(day);

                for (int i = 0; i < 14; i++)
                    Assert.AreEqual(a.IsHome(i), b.IsHome(i),
                        "Gun " + day + " adres " + i + ": karar tekrarlanabilir olmali.");
            }
        }

        [Test]
        public void Npc_FarkliGunlerdeEvdeDurumuDegisir()
        {
            var npc = BuildSystem(14, 0.5f);

            int toplamDegisim = 0;
            bool[] onceki = new bool[14];

            npc.BeginDay(1);
            for (int i = 0; i < 14; i++) onceki[i] = npc.IsHome(i);

            for (int day = 2; day <= 30; day++)
            {
                npc.BeginDay(day);
                for (int i = 0; i < 14; i++)
                {
                    if (npc.IsHome(i) != onceki[i]) toplamDegisim++;
                    onceki[i] = npc.IsHome(i);
                }
            }

            Assert.Greater(toplamDegisim, 0,
                "30 gunde EVDE/EVDE DEGIL durumu hic degismediyse oyun "
                + "statik: her gun ayni. Deterministik yanlislik olabilir.");
        }

        [Test]
        public void Npc_YeniGunKonusmalariKapatir()
        {
            NPCSystem npc = BuildSystem(3, 1f);
            npc.BeginDay(1);
            npc.AnswerDoor(0);

            npc.BeginDay(2);
            Assert.AreEqual(NpcState.HomeIdle, npc.Get(0).State,
                "Yeni gun konusma durumu sifirlanmali.");
            Assert.AreEqual(0, npc.AnsweredCount, "Istatistik sifirlanmali.");
        }

        [Test]
        public void Npc_KonusmaKapatilincaSifirlanir()
        {
            NPCSystem npc = BuildSystem(3, 1f);
            npc.BeginDay(1);
            npc.AnswerDoor(0);
            npc.CloseConversation(0);

            Assert.AreEqual(NpcState.HomeIdle, npc.Get(0).State,
                "Konusma kapaninca NPC evde bos duruma donmeli.");
            Assert.IsTrue(npc.IsHome(0), "Kapatma evde olma durumunu degistirmemeli.");
        }

        [Test]
        public void Npc_YoksaKapiCalinamaz()
        {
            var npc = new NPCSystem();
            npc.BeginDay(1);
            Assert.IsFalse(npc.AnswerDoor(0), "Kayitli NPC yoksa kapi acilmamali.");
            Assert.IsFalse(npc.IsHome(0), "Kayitli NPC yoksa evde sayilmamali.");
        }

        [Test]
        public void Npc_BosAdresListesiCokmez()
        {
            var npc = new NPCSystem();
            npc.Build(null, null, 0.8f);
            npc.Build(new System.Collections.Generic.List<int>(), null, 0.8f);
            npc.BeginDay(1);
            Assert.AreEqual(0, npc.Count, "Bos girdi hatasi vermemeli.");
            Assert.IsFalse(npc.IsHome(0), "Bos sistemde kimse evde olmamali.");
        }

        [Test]
        public void Npc_KonusmaYokkenKargoKabulEdilmez()
        {
            NPCSystem npc = BuildSystem(2, 1f);
            npc.BeginDay(1);

            // Kapi hic calinmadi: NPC dinlemiyor.
            Assert.IsFalse(npc.AcceptsParcel(0, 5),
                "Konusma baslamadan kargo kabul edilmemeli.");

            npc.AnswerDoor(0);
            Assert.IsTrue(npc.AcceptsParcel(0, 5), "Kapidan sonra kabul etmeli.");
        }
    }
}
