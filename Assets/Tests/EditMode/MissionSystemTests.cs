using System.Collections.Generic;
using KargoSim.Cargo;
using KargoSim.Economy;
using KargoSim.Mission;
using NUnit.Framework;
using UnityEngine;

namespace KargoSim.Tests
{
    /// <summary>
    /// Asama 10: gorev (mission) sistemi. MissionSystem saf C# oldugu icin
    /// Play moduna girmeden, saniyeler icinde calisir.
    ///
    /// Test havuzu 4 cesit gorev iceriyor; gun secimi deterministik oldugu
    /// icin testler "hangi gorev secildi" varsaymaz, yalnizca secilen
    /// gorevin KENDI hedefine gore ilerler.
    /// </summary>
    public sealed class MissionSystemTests
    {
        private EconomyConfig _economy;

        [SetUp]
        public void SetUp()
        {
            _economy = ScriptableObject.CreateInstance<EconomyConfig>();
            _economy.startingMoney = 250;
        }

        [TearDown]
        public void TearDown()
        {
            if (_economy != null) Object.DestroyImmediate(_economy);
        }

        // ------------------------------------------------------------------
        // Hazirlik yardimcilari
        // ------------------------------------------------------------------

        /// <summary>Cesitli 4 gorevden olusan havuz (determinizm testleri icin).</summary>
        private static List<MissionDefinition> Pool()
        {
            return new List<MissionDefinition>
            {
                MissionDefinition.Create(MissionKind.DeliverCount, 6, "Alti Kargo", 120, 30),
                MissionDefinition.Create(MissionKind.EarnMoney, 800, "Kazanc", 150, 25),
                MissionDefinition.CreatePriority(MissionKind.DeliverPriority, 3,
                    "Acil Teslimat", ParcelPriority.Urgent, 180, 40),
                MissionDefinition.Create(MissionKind.OnTimeRate, 80, "Zamaninda", 200, 50),
            };
        }

        /// <summary>Tek tanimli havuz: secim ne olursa olsun indeks 0 o tanimdir.</summary>
        private static List<MissionDefinition> Single(MissionDefinition def)
        {
            return new List<MissionDefinition> { def };
        }

        private static List<string> Ids(MissionSystem sys)
        {
            var ids = new List<string>();
            for (int i = 0; i < sys.Missions.Count; i++) ids.Add(sys.Missions[i].SafeId);
            return ids;
        }

        // ------------------------------------------------------------------
        // 1) Gun basinda sifirlama
        // ------------------------------------------------------------------

        [Test]
        public void GunBasindaGorevlerSifirlanir()
        {
            var def = MissionDefinition.Create(MissionKind.DeliverCount, 2, "Iki Kargo", 100, 20);
            var sys = new MissionSystem(Single(def), _economy);

            sys.Report(MissionKind.DeliverCount, 1, true);
            sys.Report(MissionKind.DeliverCount, 1, true);
            Assert.IsTrue(sys.IsComplete(0), "Iki teslim hedefi bitirmeli.");

            sys.BeginDay(2);

            Assert.AreEqual(2, sys.CurrentDay, "Gun numarasi guncellenmeli.");
            Assert.IsTrue(sys.IsDayOpen, "Yeni gun acik baslamali.");
            Assert.IsFalse(sys.IsComplete(0), "Yeni gun tamamlanma bayragini tasimamali.");
            Assert.IsFalse(sys.CompletedFlags[0], "Bayrak da sifirlanmali.");
            Assert.AreEqual(0, sys.CurrentValue(0), "Ilerleme sifirlanmali.");
            Assert.AreEqual(0f, sys.Progress01(0), 0.0001f, "Oran sifirlanmali.");
            Assert.AreEqual(2, sys.TargetValue(0), "Hedef degeri korunmali.");
            Assert.AreEqual(0, sys.TotalRewardMoney, "Odul yeni gune tasinmamali.");
            Assert.AreEqual(0, sys.TotalRewardXp, "XP yeni gune tasinmamali.");
            Assert.AreEqual(0, sys.FailureCount, "Gun toplamlari temizlenmeli.");
        }

        // ------------------------------------------------------------------
        // 2) Sayac olcumu
        // ------------------------------------------------------------------

        [Test]
        public void TeslimSayaci_GerekenSayiyaUlasincaTamamlanir()
        {
            var def = MissionDefinition.Create(MissionKind.DeliverCount, 3, "Uc Kargo", 150, 30);
            var sys = new MissionSystem(Single(def), _economy);

            int completedEvents = 0;
            int completedIndex = -1;
            int progressEvents = 0;
            sys.MissionCompleted += i => { completedEvents++; completedIndex = i; };
            sys.MissionProgressed += (i, ratio) =>
            {
                progressEvents++;
                Assert.LessOrEqual(ratio, 1f, "Olay orani 1'i asmamali.");
            };

            Assert.AreEqual(1, sys.Missions.Count, "Tek tanimli havuzda 1 gorev olmali.");
            Assert.AreEqual("DeliverCount_uc_kargo", sys.Missions[0].SafeId,
                "Kimlik basliktan deterministik uretilmeli.");
            Assert.AreEqual(3, sys.TargetValue(0));

            sys.Report(MissionKind.DeliverCount, 1, true);
            sys.Report(MissionKind.DeliverCount, 1, true);

            Assert.IsFalse(sys.IsComplete(0), "2/3 tamamlanma degil.");
            Assert.AreEqual(2, sys.CurrentValue(0), "Sayac iki teslimi saymali.");
            Assert.AreEqual(2f / 3f, sys.Progress01(0), 0.0001f);
            Assert.AreEqual(0, completedEvents, "Tamamlanmadan olay yayilmamali.");

            sys.Report(MissionKind.DeliverCount, 1, true);

            Assert.IsTrue(sys.IsComplete(0), "3/3 tamamlanmali.");
            Assert.AreEqual(1f, sys.Progress01(0), 0.0001f);
            Assert.AreEqual(1, completedEvents, "Tamamlanma olayi tam bir kez yayilmali.");
            Assert.AreEqual(0, completedIndex, "Olay dogru indeksi bildirmeli.");
            Assert.GreaterOrEqual(progressEvents, 3, "Her adimda ilerleme olayi yayilmali.");
            Assert.AreEqual(150, sys.TotalRewardMoney, "Tamamlanan odul biriktirilmeli.");
            Assert.AreEqual(30, sys.TotalRewardXp);

            // Hedef asilirsa sayac artmaya devam etmemeli.
            sys.Report(MissionKind.DeliverCount, 5, true);
            Assert.AreEqual(1, completedEvents, "Tamamlandiktan sonra olay tekrarlanmamali.");
            Assert.AreEqual(1f, sys.Progress01(0), 0.0001f);
            Assert.AreEqual(3, sys.CurrentValue(0), "Tamamlanan sayac donmus kalmali.");
        }

        [Test]
        public void BasarisizTeslimat_SayacGorevleriniIlerletmez()
        {
            var def = MissionDefinition.Create(MissionKind.DeliverCount, 3, "Uc Kargo", 150, 30);
            var sys = new MissionSystem(Single(def), _economy);

            sys.Report(MissionKind.DeliverCount, 1, false);
            sys.Report(MissionKind.DeliverCount, 1, false);

            Assert.AreEqual(0, sys.CurrentValue(0), "Basarisiz teslimat sayaca girmemeli.");
            Assert.IsFalse(sys.IsComplete(0));
            Assert.AreEqual(2, sys.FailureCount, "Gun istatistigi yine de sayilmali.");
        }

        // ------------------------------------------------------------------
        // 3) Para (kumulatif) olcumu
        // ------------------------------------------------------------------

        [Test]
        public void ParaGorevi_AnlikDegerUzerindenIlerler()
        {
            var def = MissionDefinition.Create(MissionKind.EarnMoney, 1000, "Kazanc", 200, 50);
            var sys = new MissionSystem(Single(def), _economy);

            sys.Report(MissionKind.EarnMoney, 250, true);
            Assert.AreEqual(250, sys.CurrentValue(0), "Kazanc degeri sayaca yazilmali.");
            Assert.AreEqual(0.25f, sys.Progress01(0), 0.0001f);
            Assert.IsFalse(sys.IsComplete(0), "250/1000 tamamlanma degil.");

            // Adim adim gelen kazanc TOPLANIR: her teslim kendi tutarini
            // ekler, "anlik deger" her seferinde sifirlanmaz.
            sys.Report(MissionKind.EarnMoney, 300, true);
            sys.Report(MissionKind.EarnMoney, 500, true);

            Assert.AreEqual(1050, sys.CurrentValue(0), "Kazanc kumulatif toplanmali.");
            Assert.IsTrue(sys.IsComplete(0), "Toplam hedefi gecen an tamamlanmali.");
            Assert.AreEqual(1f, sys.Progress01(0), 0.0001f, "Hedef asilimda oran 1'de kirpilir.");
            Assert.AreEqual(200, sys.TotalRewardMoney);

            sys.Report(MissionKind.EarnMoney, 900, true);
            Assert.AreEqual(200, sys.TotalRewardMoney, "Tamamlanan odul bir kez verilmeli.");
        }

        // ------------------------------------------------------------------
        // 4) NoFailures: bozulma kalicidir
        // ------------------------------------------------------------------

        [Test]
        public void BasarisizTeslimat_NoFailuresGoreviniBozar()
        {
            var def = MissionDefinition.CreateBonus(MissionKind.NoFailures, 1,
                "Hatasiz Gun", 400, 80);
            var sys = new MissionSystem(Single(def), _economy);

            Assert.AreEqual(MissionKind.NoFailures, sys.Missions[0].kind);
            Assert.IsTrue(sys.Missions[0].requiresAll, "Bonus gorevu olarak isaretlenmisti.");
            Assert.AreEqual(1f, sys.Progress01(0), 0.0001f,
                "Gun temiz basladigi icin sayac dolu (1) olmali.");

            sys.Report(MissionKind.DeliverCount, 1, true);
            Assert.AreEqual(1f, sys.Progress01(0), 0.0001f,
                "Basarili teslimat temizligi bozmamali.");

            sys.Report(MissionKind.DeliverCount, 1, false);

            Assert.IsTrue(sys.IsBroken(0), "Tek basarisiz teslimat gorevi bozmali.");
            Assert.AreEqual(0f, sys.Progress01(0), 0.0001f, "Bozulan gorevde ilerleme sifir.");
            Assert.IsFalse(sys.IsComplete(0));
            Assert.AreEqual(0, sys.TotalRewardMoney, "Bozulan gorev odul vermemeli.");
            Assert.IsFalse(sys.AllCompleted, "requiresAll gorevi bozulunca bonus verilmemeli.");
        }

        [Test]
        public void BozulanGorev_GunIciDuzelmez()
        {
            var def = MissionDefinition.CreateBonus(MissionKind.NoFailures, 1,
                "Hatasiz Gun", 400, 80);
            var sys = new MissionSystem(Single(def), _economy);

            sys.Report(MissionKind.DeliverCount, 1, false);

            for (int i = 0; i < 5; i++)
                sys.Report(MissionKind.DeliverCount, 1, true);

            Assert.IsTrue(sys.IsBroken(0), "Bozulma geri alinmamali.");
            Assert.IsFalse(sys.IsComplete(0), "Basarili teslimatlar bozulan gorevi canlandirmamali.");
            Assert.AreEqual(0f, sys.Progress01(0), 0.0001f);

            sys.EndDay();

            Assert.IsFalse(sys.IsComplete(0), "Gun sonunda da bozuk gorev tamamlanmamali.");
            Assert.AreEqual(0, sys.TotalRewardMoney, "Gun sonunda da odul verilmemeli.");
            Assert.IsFalse(sys.IsDayOpen, "Gun kapanmis olmali.");

            // Gun kapaliyken gelen raporlar yok sayilmali.
            sys.Report(MissionKind.DeliverCount, 1, true);
            Assert.IsFalse(sys.IsComplete(0), "Gun bitince yeni teslimat sayilmamali.");

            // Ancak yeni gun sifirlar: ertesi gun tekrar sans var.
            sys.BeginDay(2);
            Assert.IsTrue(sys.IsDayOpen, "Yeni gun yeniden acilmali.");
            Assert.IsFalse(sys.IsBroken(0), "Yeni gunde bozulma sifirlanmali.");
            Assert.AreEqual(1f, sys.Progress01(0), 0.0001f, "Yeni gunde sayac tekrar 1.");
            Assert.AreEqual(0, sys.FailureCount, "Gun toplamlari sifirlanmali.");
        }

        [Test]
        public void TemizGun_NoFailuresGoreviGunSonundaTamamlanir()
        {
            var def = MissionDefinition.CreateBonus(MissionKind.NoFailures, 1,
                "Hatasiz Gun", 400, 80);
            var sys = new MissionSystem(Single(def), _economy);

            sys.Report(MissionKind.DeliverCount, 3, true);

            Assert.IsFalse(sys.IsComplete(0),
                "Gun bitmeden 'gun temiz mi?' karari verilmemeli.");

            sys.EndDay();

            Assert.IsTrue(sys.IsComplete(0), "Basarisiz teslimat yoksa gorev tamamlanmali.");
            Assert.AreEqual(1f, sys.Progress01(0), 0.0001f);
            Assert.AreEqual(400, sys.TotalRewardMoney);
            Assert.AreEqual(80, sys.TotalRewardXp);
            Assert.IsTrue(sys.AllCompleted, "Tek gorev de bonus isaretli, hepsi bitti.");
        }

        // ------------------------------------------------------------------
        // 5) Odul toplamlari
        // ------------------------------------------------------------------

        [Test]
        public void OdulTamamlananGorevlerinToplamiDondurulur()
        {
            var a = MissionDefinition.Create(MissionKind.DeliverCount, 2, "A Gorevi", 100, 10);
            var b = MissionDefinition.Create(MissionKind.DeliverCount, 3, "B Gorevi", 250, 20);
            var sys = new MissionSystem(new List<MissionDefinition> { a, b }, _economy);

            Assert.AreEqual(0, sys.TotalRewardMoney, "Hicbiri bitmeden odul verilmemeli.");
            Assert.AreEqual(0, sys.TotalRewardXp);

            sys.BeginDay(4);
            Assert.GreaterOrEqual(sys.Missions.Count, 1, "En az bir gorev secilmeli.");
            Assert.LessOrEqual(sys.Missions.Count, 2, "Gunde en fazla 2 gorev secilmeli.");

            // Sadece ilk gorevi bitir.
            for (int i = 0; i < sys.TargetValue(0); i++)
                sys.Report(MissionKind.DeliverCount, 1, true);

            Assert.IsTrue(sys.IsComplete(0));
            Assert.AreEqual(sys.Missions[0].SafeRewardMoney, sys.TotalRewardMoney,
                "Toplam yalnizca TAMAMLANAN gorevlerin odulunden olusmali.");
            Assert.AreEqual(sys.Missions[0].SafeRewardXp, sys.TotalRewardXp);
            Assert.Greater(sys.TotalRewardMoney, 0, "Tamamlanan gorev odul vermeli.");

            // Kalanlari da bitir.
            for (int i = 1; i < sys.Missions.Count; i++)
            {
                if (sys.IsComplete(i)) continue;
                for (int n = 0; n < sys.TargetValue(i); n++)
                    sys.Report(MissionKind.DeliverCount, 1, true);
            }

            int expectedMoney = 0;
            int expectedXp = 0;
            for (int i = 0; i < sys.Missions.Count; i++)
            {
                if (!sys.CompletedFlags[i]) continue;
                expectedMoney += sys.Missions[i].SafeRewardMoney;
                expectedXp += sys.Missions[i].SafeRewardXp;
            }

            Assert.IsTrue(sys.EveryMissionCompleted, "Tum secilen gorevler bitmeli.");
            Assert.AreEqual(expectedMoney, sys.TotalRewardMoney, "Toplam bayraklarla tutmali.");
            Assert.AreEqual(expectedXp, sys.TotalRewardXp, "XP toplami bayraklarla tutmali.");
            Assert.GreaterOrEqual(sys.TotalRewardMoney, 100);

            // Odul donmus kalmali: fazladan teslimat yeni odul eklemez.
            for (int i = 0; i < 5; i++) sys.Report(MissionKind.DeliverCount, 1, true);
            Assert.AreEqual(expectedMoney, sys.TotalRewardMoney,
                "Tamamlandiktan sonra odul tekrar artmamali.");
            Assert.AreEqual(expectedXp, sys.TotalRewardXp);
        }

        [Test]
        public void BeyrakListesiAnlikKopyaDondurur()
        {
            var def = MissionDefinition.Create(MissionKind.DeliverCount, 1, "Tek Kargo", 60, 15);
            var sys = new MissionSystem(Single(def), _economy);

            IReadOnlyList<bool> eski = sys.CompletedFlags;
            Assert.AreEqual(1, eski.Count);
            Assert.IsFalse(eski[0]);

            sys.Report(MissionKind.DeliverCount, 1, true);

            Assert.IsFalse(eski[0], "Onceki kopya guncellenmemeli (anlik kopya).");
            Assert.IsTrue(sys.CompletedFlags[0], "Yeni okuma guncel durumu gostermeli.");
        }

        // ------------------------------------------------------------------
        // 6-7) Deterministik gun secimi
        // ------------------------------------------------------------------

        [Test]
        public void Deterministik_AYNI_gun_AYNI_gorevler()
        {
            var a = new MissionSystem(Pool(), _economy);
            var b = new MissionSystem(Pool(), _economy);

            for (int day = 1; day <= 10; day++)
            {
                a.BeginDay(day);
                b.BeginDay(day);

                CollectionAssert.AreEqual(Ids(a), Ids(b),
                    day + ". gun: iki ayri sistem ayni gorevleri secmeli.");
                Assert.AreEqual(a.Missions.Count, b.Missions.Count,
                    day + ". gun: gorev sayisi degismemeli.");

                // Ayni ornegi ayni gunla yeniden baslatmak da ayni sonucu
                // vermeli (kaydedip geri yukleme senaryosu).
                a.BeginDay(day);
                CollectionAssert.AreEqual(Ids(a), Ids(b),
                    day + ". gun: tekrar baslatmak secimi degistirmemeli.");
            }
        }

        [Test]
        public void FarkliGun_FarkliGorevler()
        {
            // KARSIT NOTU: iki farkli gunun ayni gorevleri vermesi HIC
            // bir hata degildir (tohumlar komsu olabilir). Bu yuzden test
            // "gun N ile gun M farkli" diye degil, "havuz birden fazla cesit
            // sunuyor ve tohum degistikce secim cesitleniyor" diye yazilir.
            var sys = new MissionSystem(Pool(), _economy);
            var setler = new HashSet<string>();
            var tekil = new HashSet<string>();

            for (int day = 1; day <= 60; day++)
            {
                sys.BeginDay(day);

                Assert.GreaterOrEqual(sys.Missions.Count, 1, day + ". gun: gorev secilmeli.");
                Assert.LessOrEqual(sys.Missions.Count, 2, day + ". gun: en fazla 2 gorev.");

                setler.Add(string.Join("|", Ids(sys)));
                for (int i = 0; i < sys.Missions.Count; i++)
                    tekil.Add(sys.Missions[i].SafeId);
            }

            Assert.GreaterOrEqual(setler.Count, 2,
                "Gun tohumu degistikce secim de degismeli.");
            Assert.GreaterOrEqual(tekil.Count, 2,
                "Havuz en az 2 cesit gorev sunmali.");
        }

        [Test]
        public void GirdiListesiDegistirilmez()
        {
            var kaynak = Pool();
            var kimlikler = new List<string>();
            for (int i = 0; i < kaynak.Count; i++) kimlikler.Add(kaynak[i].id);

            var sys = new MissionSystem(kaynak, _economy);

            for (int day = 1; day <= 10; day++)
            {
                sys.BeginDay(day);
                sys.Report(MissionKind.DeliverCount, 1, true);
                sys.Report(MissionKind.EarnMoney, 25, true);
                sys.Report(MissionKind.DeliverPriority, 1, true, ParcelPriority.Urgent);
                sys.Report(MissionKind.OnTimeRate, 1, day % 2 == 0);
                sys.EndDay();
            }

            Assert.AreEqual(4, kaynak.Count, "Aksiyi (girdi) liste degistirilmemeli.");
            for (int i = 0; i < kaynak.Count; i++)
                Assert.AreEqual(kimlikler[i], kaynak[i].id, "Tanim nesnesi degistirilmemeli.");

            Assert.AreEqual(4, sys.Pool.Count, "Havuz kopyasi ayni boyutta olmali.");
            Assert.AreEqual(6, sys.Pool[0].SafeTargetValue, "Havuz hedefleri degismemeli.");
        }

        // ------------------------------------------------------------------
        // 8) Bos / bozuk tanim listesi
        // ------------------------------------------------------------------

        [Test]
        public void BosTanimListesi_GunBasindaCokmez()
        {
            // null liste
            var nullListe = new MissionSystem(null, _economy);
            Assert.DoesNotThrow(() => nullListe.BeginDay(3));
            Assert.AreEqual(0, nullListe.Missions.Count, "Havuz yoksa gorev de olmamali.");
            Assert.AreEqual(0, nullListe.ActiveCount);
            Assert.AreEqual(0, nullListe.Pool.Count);
            Assert.IsTrue(nullListe.AllCompleted, "Gorev yoksa kosul otomatik saglanir.");
            Assert.IsTrue(nullListe.EveryMissionCompleted);
            Assert.AreEqual(0, nullListe.TotalRewardMoney);
            Assert.AreEqual(0, nullListe.TotalRewardXp);
            Assert.IsFalse(nullListe.IsComplete(0), "Gecersiz indeks patlamamali.");
            Assert.IsFalse(nullListe.IsBroken(-1));
            Assert.AreEqual(0f, nullListe.Progress01(0), 0.0001f);
            Assert.AreEqual(0, nullListe.CurrentValue(0));
            Assert.AreEqual(0, nullListe.TargetValue(5));
            Assert.AreEqual(0, nullListe.CompletedFlags.Count);
            Assert.DoesNotThrow(() => nullListe.Report(MissionKind.DeliverCount, 3, true));
            Assert.DoesNotThrow(() => nullListe.Report(MissionKind.NoFailures, 1, false));
            Assert.DoesNotThrow(() => nullListe.EndDay());
            Assert.IsFalse(nullListe.IsComplete(0));

            // bos liste
            var bosListe = new MissionSystem(new List<MissionDefinition>(), _economy);
            bosListe.BeginDay(1);
            Assert.AreEqual(0, bosListe.Missions.Count);

            // yalnizca null tanimlar
            var nullTanimlar = new MissionSystem(
                new List<MissionDefinition> { null, null }, _economy);
            nullTanimlar.BeginDay(5);
            Assert.AreEqual(0, nullTanimlar.Missions.Count, "Null tanimlar havuzdan dusmeli.");

            // null + gecerli karisik: yalnizca gecerli olan kalir
            var gecerli = MissionDefinition.Create(MissionKind.DeliverCount, 1, "Tek", 10, 5);
            var karisik = new MissionSystem(
                new List<MissionDefinition> { null, gecerli, null }, _economy);
            Assert.AreEqual(1, karisik.Missions.Count, "Tek gecerli tanim secilmeli.");
            Assert.AreEqual(gecerli.SafeId, karisik.Missions[0].SafeId);

            // ekonomi ayari olmadan da calismali
            Assert.DoesNotThrow(() =>
            {
                var ekonomisiz = new MissionSystem(Pool(), null);
                ekonomisiz.BeginDay(2);
                ekonomisiz.Report(MissionKind.DeliverCount, 1, true);
                ekonomisiz.EndDay();
            });
        }

        // ------------------------------------------------------------------
        // 9) Kalan turler
        // ------------------------------------------------------------------

        [Test]
        public void SeviyeGorevi_EnYuksekSeviyeyiTutar()
        {
            var def = MissionDefinition.Create(MissionKind.ReachLevel, 5, "Seviye", 150, 30);
            var sys = new MissionSystem(Single(def), _economy);

            sys.Report(MissionKind.ReachLevel, 3, true);
            Assert.AreEqual(3, sys.CurrentValue(0));
            Assert.AreEqual(0.6f, sys.Progress01(0), 0.0001f);

            sys.Report(MissionKind.ReachLevel, 2, true);
            Assert.AreEqual(3, sys.CurrentValue(0),
                "Seviye kumulatif degil; dusuk seviye eski degeri dusurmemeli.");
            Assert.IsFalse(sys.IsComplete(0));

            sys.Report(MissionKind.ReachLevel, 5, true);
            Assert.IsTrue(sys.IsComplete(0), "Hedef seviyeye ulasinca tamamlanmali.");
            Assert.AreEqual(5, sys.CurrentValue(0), "Deger 3+2+5 degil, 5 olmali.");
            Assert.AreEqual(150, sys.TotalRewardMoney);
        }

        [Test]
        public void OncelikGorevi_YalnizcaEsitOnceligiSayar()
        {
            var def = MissionDefinition.CreatePriority(MissionKind.DeliverPriority, 2,
                "Acil Kargo", ParcelPriority.Urgent, 200, 40);
            var sys = new MissionSystem(Single(def), _economy);

            Assert.IsTrue(sys.Missions[0].FiltersPriority);
            Assert.AreEqual(ParcelPriority.Urgent, sys.Missions[0].requiredPriority);

            sys.Report(MissionKind.DeliverPriority, 1, true, ParcelPriority.Normal);
            Assert.AreEqual(0, sys.CurrentValue(0), "Normal kargo sayaca girmemeli.");

            sys.Report(MissionKind.DeliverPriority, 1, true, ParcelPriority.Express);
            Assert.AreEqual(0, sys.CurrentValue(0), "Hizli kargo sayaca girmemeli.");

            sys.Report(MissionKind.DeliverPriority, 1, true, ParcelPriority.Urgent);
            Assert.AreEqual(1, sys.CurrentValue(0));
            Assert.AreEqual(0.5f, sys.Progress01(0), 0.0001f);
            Assert.IsFalse(sys.IsComplete(0));

            sys.Report(MissionKind.DeliverPriority, 1, true, ParcelPriority.Urgent);
            Assert.IsTrue(sys.IsComplete(0), "Iki acil kargo hedefi tamamlamali.");
            Assert.AreEqual(200, sys.TotalRewardMoney);
        }

        [Test]
        public void ZamanindaOranGorevi_GunSonundaDegerlendirilir()
        {
            var def = MissionDefinition.Create(MissionKind.OnTimeRate, 80, "Zamaninda", 300, 60);
            var sys = new MissionSystem(Single(def), _economy);

            Assert.AreEqual(0, sys.OnTimePercent, "Gun basinda teslimat yok.");

            sys.Report(MissionKind.OnTimeRate, 1, true);
            sys.Report(MissionKind.OnTimeRate, 1, true);
            sys.Report(MissionKind.OnTimeRate, 1, false);
            sys.Report(MissionKind.OnTimeRate, 1, true);

            Assert.AreEqual(75, sys.OnTimePercent, "3/4 teslimat = %75.");
            Assert.AreEqual(75f / 80f, sys.Progress01(0), 0.0001f,
                "Canli oran hedefe gore olceklenmeli (75/80).");
            Assert.IsFalse(sys.IsComplete(0), "Gun bitmeden oran gorevi karar vermemeli.");

            sys.Report(MissionKind.OnTimeRate, 1, true);
            Assert.AreEqual(80, sys.OnTimePercent, "4/5 teslimat = %80.");
            Assert.AreEqual(1f, sys.Progress01(0), 0.0001f,
                "Hedef %80'e ulasinca cubuk dolmali.");
            Assert.IsFalse(sys.IsComplete(0), "Hedef olsa bile gun sonu beklenir.");

            sys.EndDay();

            Assert.IsTrue(sys.IsComplete(0), "Gun sonunda oran degerlendirilmeli.");
            Assert.AreEqual(300, sys.TotalRewardMoney);
        }

        [Test]
        public void ZamanindaOranDusukseGorevTamamlanmaz()
        {
            var def = MissionDefinition.Create(MissionKind.OnTimeRate, 90, "Zamaninda", 300, 60);
            var sys = new MissionSystem(Single(def), _economy);

            for (int i = 0; i < 8; i++)
                sys.Report(MissionKind.OnTimeRate, 1, i < 6);   // 6/8 = %75

            Assert.AreEqual(75, sys.OnTimePercent);
            sys.EndDay();

            Assert.IsFalse(sys.IsComplete(0), "%75 < %90 oldugu icin tamamlanmamali.");
            Assert.AreEqual(0, sys.TotalRewardMoney);
        }

        [Test]
        public void TumGorevlerBittigindeBonusAcilir()
        {
            var zorunlu = MissionDefinition.Create(MissionKind.DeliverCount, 1, "Zorunlu", 50, 10);
            var bonuslu = MissionDefinition.CreateBonus(MissionKind.EarnMoney, 100,
                "Bonus", 500, 100);

            // Ayni turden iki gorev: boylece ikisi de ayni anda secilip
            // "hepsi bitti" kontrolu gercekten anlamli olur.
            var zorunlu2 = MissionDefinition.Create(MissionKind.DeliverCount, 1, "Zorunlu 2", 70, 20);
            var sys = new MissionSystem(
                new List<MissionDefinition> { zorunlu, zorunlu2, bonuslu }, _economy);

            // Gun secimi deterministik oldugu icin, iki gorevin bir arada
            // secildigi gunu arariz (hangi gun oldugunu varsaymayiz).
            bool ikiGorevlu = false;
            for (int day = 1; day <= 30 && !ikiGorevlu; day++)
            {
                sys.BeginDay(day);
                ikiGorevlu = sys.Missions.Count > 1;
            }

            Assert.IsTrue(ikiGorevlu, "Iki gorevin secildigi bir gun bulunmali.");

            int bonusIndex = -1;
            for (int i = 0; i < sys.Missions.Count; i++)
                if (sys.Missions[i].requiresAll) bonusIndex = i;

            for (int i = 0; i < sys.Missions.Count; i++)
            {
                if (sys.Missions[i].requiresAll) continue;
                sys.Report(MissionKind.DeliverCount, 1, true);
            }

            if (bonusIndex >= 0)
            {
                // AllCompleted yalnizca requiresAll gorevlerinin KENDI
                // bitmesine bakar: digerleri bitse de bonus gor bitmeden
                // kosul saglanmaz.
                Assert.IsFalse(sys.AllCompleted,
                    "Bonus gorev kendisi bitmeden AllCompleted false olmali.");
                Assert.IsFalse(sys.EveryMissionCompleted);

                sys.Report(MissionKind.EarnMoney, 100, true);
            }

            Assert.IsTrue(sys.AllCompleted, "requiresAll gorev bitince kosul saglanmali.");
            Assert.IsTrue(sys.EveryMissionCompleted, "Tum gorevler bitmeli.");
            Assert.GreaterOrEqual(sys.TotalRewardMoney, 500, "Bonus odulu de toplama girdi.");
        }
    }
}
