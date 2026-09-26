using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using KargoSim.Cargo;
using KargoSim.Core;
using KargoSim.Delivery;
using KargoSim.Economy;
using KargoSim.Player;
using KargoSim.Save;
using KargoSim.Vehicle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace KargoSim.Tests
{
    /// <summary>
    /// Asama 7: kayit / yukleme sistemi.
    ///
    /// Hepsi EditMode'da calisir: SaveSystem statik yardimcilar, oyun sahnesi
    /// veya fizik motoru gerektirmez. Gercek dosya sistemi kullanilir
    /// (Application.persistentDataPath) cunku asil risk dosya yazma/okuma
    /// katmaninda: yarim kalmis dosya, bozuk JSON, ustune yazma.
    ///
    /// DOSYA IZOLASYONU: testler gercek kayit dosyasini kullanir ve TearDown'da
    /// SaveSystem.Delete() ile silinir. Oyuncu kaydi testlerle bolunmez.
    /// </summary>
    public sealed class SaveSystemTests
    {
        private GameConfig _config;
        private EconomyConfig _economy;
        private ParcelType[] _types;
        private VehicleDefinition _vehicle;

        [SetUp]
        public void SetUp()
        {
            // Once temiz basla: onceki testin dosyasi kalmis olabilir.
            SaveSystem.Delete();

            _config = ScriptableObject.CreateInstance<GameConfig>();
            _config.parcelsPerDay = 8;
            _config.destinationCount = 10;
            _config.baseDeliverySeconds = 300f;

            _economy = ScriptableObject.CreateInstance<EconomyConfig>();
            _economy.startingMoney = 100;
            _economy.maxLevel = 20;
            _economy.levelUpBaseCost = 100;
            _economy.levelUpCostGrowth = 1.35f;

            var kutu = NewType("Kutu", 1f, 5f, 2f, 10f, 50f);
            var kagit = NewType("Kagit", 0.5f, 1.5f, 1f, 3f, 40f);
            _types = new[] { kutu, kagit };

            // Buyuk kapasite: kayittaki arac yuku sigsin ki yeniden yuklenebilsin.
            _vehicle = ScriptableObject.CreateInstance<VehicleDefinition>();
            _vehicle.cargoCapacityKg = 5000f;
            _vehicle.cargoCapacityLiters = 5000f;
        }

        [TearDown]
        public void TearDown()
        {
            SaveSystem.Delete();

            for (int i = 0; i < _types.Length; i++)
                if (_types[i] != null) Object.DestroyImmediate(_types[i]);

            if (_vehicle != null) Object.DestroyImmediate(_vehicle);
            if (_economy != null) Object.DestroyImmediate(_economy);
            if (_config != null) Object.DestroyImmediate(_config);
        }

        private static ParcelType NewType(string name, float minKg, float maxKg,
                                          float minL, float maxL, float payPerKg)
        {
            ParcelType type = ScriptableObject.CreateInstance<ParcelType>();
            type.displayName = name;
            type.minWeightKg = minKg;
            type.maxWeightKg = maxKg;
            type.minVolumeLiters = minL;
            type.maxVolumeLiters = maxL;
            type.paymentPerKg = payPerKg;
            type.Sanitize();
            return type;
        }

        // ------------------------------------------------------------------
        // 1) Para / XP / seviye korunumu
        // ------------------------------------------------------------------

        [Test]
        public void Kaydet_Yukle_ParayiVeSeviyeyiKorur()
        {
            var data = SaveSystem.Capture(
                day: 7, money: 1250, xp: 340, level: 4, hourOfDay: 15.5f,
                parcels: new List<Parcel>(), types: _types, nextId: 1);

            Assert.IsTrue(SaveSystem.TrySave(data), "Kayit dosyasi yazilamadi.");
            Assert.IsTrue(SaveSystem.Exists(), "Kayit dosyasi olusmadi.");

            SaveData loaded = SaveSystem.Load();
            Assert.IsNotNull(loaded, "Yazilan kayit okunamadi.");

            // YENI bir PlayerProgress: kaydin gercekten uygulandigini gormek icin
            // eskisini kullanmamak lazim.
            var progress = new PlayerProgress(_economy);
            RestoreResult result = SaveSystem.Restore(loaded, progress, null, _types);

            Assert.IsTrue(result.Success, "Restore basarisiz olmamali.");
            Assert.AreEqual(1250, progress.Money, "Bakiye korunmadi.");
            Assert.AreEqual(340, progress.Xp, "XP korunmadi.");
            Assert.AreEqual(4, progress.Level, "Seviye korunmadi.");

            // PlayerProgress gun tutmaz; gun/saat geri cagrana dondurulur.
            Assert.AreEqual(7, result.Day, "Gun korunmadi.");
            Assert.AreEqual(15.5f, result.HourOfDay, 0.001f, "Saat korunmadi.");
        }

        [Test]
        public void Kaydet_Yukle_BaslangicBakiyesiUzerineYazmaz()
        {
            // Regresyon: RestoreFromSave cagrilmiyorsa bakiye startingMoney
            // olarak kalir ve oyuncu parasini kaybetmis olur.
            var data = SaveSystem.Capture(
                day: 1, money: 4000, xp: 0, level: 1, hourOfDay: 8f,
                parcels: new List<Parcel>(), types: _types, nextId: 1);
            Assert.IsTrue(SaveSystem.TrySave(data));

            var progress = new PlayerProgress(_economy);
            SaveSystem.Restore(SaveSystem.Load(), progress, null, _types);

            Assert.AreNotEqual(_economy.startingMoney, progress.Money,
                "Bakiye hala baslangic degerinde - kayit uygulanmamis.");
        }

        // ------------------------------------------------------------------
        // 2) Kargo adedi ve icerigi
        // ------------------------------------------------------------------

        [Test]
        public void Kaydet_Yukle_KargolariAdetVeIcerikleGeriGetirir()
        {
            // Elle kurulan iki kargo: her alan farkli, eslesme hatasi gorunsun.
            var parcels = new List<Parcel>
            {
                new Parcel(1, _types[0], "Ahmet Yilmaz", 3, 2.5f, 4f,
                           ParcelPriority.Normal, 300f, 125),
                new Parcel(2, _types[1], "Zeynep Aydin", 7, 0.75f, 1.5f,
                           ParcelPriority.Urgent, 150f, 66)
            };
            parcels[1].MarkLoaded(12f);      // ikinci kargo aracda

            var data = SaveSystem.Capture(
                day: 2, money: 500, xp: 100, level: 2, hourOfDay: 9f,
                parcels: parcels, types: _types, nextId: 3);
            Assert.IsTrue(SaveSystem.TrySave(data));

            SaveData loaded = SaveSystem.Load();
            Assert.IsNotNull(loaded, "Kayit okunamadi.");
            Assert.AreEqual(2, loaded.parcels.Count, "Dosyaya iki kargo yazilmali.");

            var cargo = new CargoSystem(_config, _types, _vehicle);
            var progress = new PlayerProgress(_economy);
            RestoreResult result = SaveSystem.Restore(loaded, progress, cargo, _types, 12f);

            Assert.AreEqual(2, result.RestoredCount, "Iki kargo da geri gelmeli.");
            Assert.AreEqual(1, cargo.Manifest.AvailableCount, "Depoda bir kargo kalmali.");
            Assert.AreEqual(1, cargo.Manifest.LoadedCount, "Aracda bir kargo kalmali.");

            Parcel a = cargo.Manifest.FindById(1);
            Assert.IsNotNull(a, "#1 geri gelmedi.");
            Assert.AreEqual("Ahmet Yilmaz", a.RecipientName, "Alici adi eslesmedi.");
            Assert.AreEqual(2.5f, a.WeightKg, 0.0001f, "Agirlik eslesmedi.");
            Assert.AreEqual(4f, a.VolumeLiters, 0.0001f, "Hacim eslesmedi.");
            Assert.AreEqual(ParcelPriority.Normal, a.Priority, "Oncelik eslesmedi.");
            Assert.AreEqual(3, a.DestinationId, "Varis adresi eslesmedi.");
            Assert.AreEqual(300f, a.DeadlineSeconds, 0.0001f, "Teslim suresi eslesmedi.");
            Assert.AreEqual(ParcelState.WaitingInDepot, a.State, "Durum eslesmedi.");
            Assert.AreSame(_types[0], a.Type, "Kargo turu eslesmedi.");
            // Deger dosyada tutulmaz, ParcelGenerator formuluyle yeniden hesaplanir:
            // 50 TL/kg * 2.5 kg * 1.0 = 125.
            Assert.AreEqual(125, a.BaseValue, "Deger yeniden hesaplanmadi.");

            Parcel b = cargo.Manifest.FindById(2);
            Assert.IsNotNull(b, "#2 geri gelmedi.");
            Assert.AreEqual("Zeynep Aydin", b.RecipientName, "Alici adi eslesmedi.");
            Assert.AreEqual(0.75f, b.WeightKg, 0.0001f, "Agirlik eslesmedi.");
            Assert.AreEqual(ParcelPriority.Urgent, b.Priority, "Oncelik eslesmedi.");
            Assert.AreEqual(7, b.DestinationId, "Varis adresi eslesmedi.");
            Assert.AreEqual(ParcelState.LoadedOnVehicle, b.State, "Aracdaki kargo depoya dustu.");
            Assert.AreEqual(12f, b.TimeLoaded, 0.0001f,
                "Yukleme ani korunmadi; geri sayim yanlis baslar.");
            Assert.AreSame(_types[1], b.Type, "Kargo turu eslesmedi.");
            // 40 TL/kg * 0.75 kg * 2.2 (acil carpani) = 66
            Assert.AreEqual(66, b.BaseValue, "Deger yeniden hesaplanmadi.");
        }

        [Test]
        public void Kaydet_Yukle_AracAgirlikVeHacminiKorur()
        {
            // Arada kargo varken kaydedip yuklemek, aracdaki toplamlari da
            // (agirlik/hacim) dogru kurmali; aksi halde HUD yanlis doluluk gosterir.
            var cargo = new CargoSystem(_config, _types, _vehicle);
            cargo.BeginDay(1);
            cargo.TryLoadParcel(cargo.Manifest.Available[0].Id, 5f);
            cargo.TryLoadParcel(cargo.Manifest.Available[0].Id, 5f);

            float beklenenAgirlik = cargo.Manifest.LoadedWeightKg;
            float beklenenHacim = cargo.Manifest.LoadedVolumeLiters;

            var hepsi = new List<Parcel>();
            hepsi.AddRange(cargo.Manifest.Available);
            hepsi.AddRange(cargo.Manifest.Loaded);

            var data = SaveSystem.Capture(
                day: 1, money: 100, xp: 0, level: 1, hourOfDay: 8f,
                parcels: hepsi, types: _types, nextId: hepsi.Count + 1);
            Assert.IsTrue(SaveSystem.TrySave(data));

            var yeniCargo = new CargoSystem(_config, _types, _vehicle);
            RestoreResult result = SaveSystem.Restore(SaveSystem.Load(), null, yeniCargo, _types, 5f);

            // RestoredCount DEPODAKI + ARAKTAKI toplam kargodur; sadece
            // yuklenen 2 kargo degil. (Once 2 bekleniyordu, oysa listede
            // depodaki kargolar da var ve onlar da geri gelmeli.)
            Assert.AreEqual(hepsi.Count, result.RestoredCount,
                "Depodaki + aractaki tum kargolar geri gelmeli.");
            Assert.AreEqual(2, yeniCargo.Manifest.LoadedCount, "Iki kargo aracda kalmali.");
            Assert.AreEqual(beklenenAgirlik, yeniCargo.Manifest.LoadedWeightKg, 0.001f,
                "Arac agirligi kayit sonrasi degismemeli.");
            Assert.AreEqual(beklenenHacim, yeniCargo.Manifest.LoadedVolumeLiters, 0.001f,
                "Arac hacmi kayit sonrasi degismemeli.");
        }

        [Test]
        public void Kayit_BilinmeyenDurumVeAdresiTespitEder()
        {
            // Elle bozulmus kayit: gecersiz durum, gecersiz oncelik, gecersiz
            // tur sirasi ve haritada olmayan bir adres.
            var data = SaveSystem.Capture(
                day: 3, money: 200, xp: 10, level: 1, hourOfDay: 10f,
                parcels: new List<Parcel>(), types: _types, nextId: 1);
            data.parcels.Add(new SavedParcel
            {
                id = 42,
                typeIndex = 99,               // gecersiz tur sirasi
                recipientName = "Bozuk Kargo",
                destinationId = 999,          // haritada yok
                weightKg = 1f,
                volumeLiters = 1f,
                priority = 77,                // gecersiz oncelik
                deadlineSeconds = 300f,
                state = 99                     // gecersiz durum
            });
            Assert.IsTrue(SaveSystem.TrySave(data));

            // Haritada yalnizca 0 numarali adres var; kayit 999'a gidiyor.
            var layout = ScriptableObject.CreateInstance<MapLayout>();
            layout.deliveryPoints.Add(new DeliveryPointData
            {
                id = 0,
                displayName = "Depo Onu",
                position = Vector3.zero
            });

            try
            {
                var cargo = new CargoSystem(_config, _types, _vehicle);
                RestoreResult result = SaveSystem.Restore(SaveSystem.Load(), null, cargo,
                                                          _types, 0f, layout);

                Assert.AreEqual(1, result.RestoredCount, "Bozuk kargo depoya alinmali.");

                Parcel parcel = cargo.Manifest.FindById(42);
                Assert.IsNotNull(parcel, "Bozuk kargo depoda olmali.");
                Assert.AreEqual(ParcelState.WaitingInDepot, parcel.State,
                    "Bilinmeyen durum depoya (WaitingInDepot) cevrilmeli.");
                Assert.AreEqual(ParcelPriority.Normal, parcel.Priority,
                    "Bilinmeyen oncelik Normal olmali.");
                Assert.IsNotNull(parcel.Type, "Gecersiz typeIndex'te tur null olmamali.");
                Assert.AreEqual(999, parcel.DestinationId,
                    "Olmayan adres korunmali - kargo SILINMEMELI, oyuncu verisidir.");

                // Adres haritada yoksa teslimat mumkun degil; kargo depoda
                // birakilir, oyuncu elle karar verir.
                Assert.AreEqual(1, cargo.Manifest.AvailableCount,
                    "Teslim edilemeyecek kargo depoda kalmali.");
            }
            finally
            {
                Object.DestroyImmediate(layout);
            }
        }

        // ------------------------------------------------------------------
        // 3-4) Bozuk dosya ve dosya yok
        // ------------------------------------------------------------------

        [Test]
        public void BozukDosya_Load_NullDonerVeHataVerir()
        {
            Directory.CreateDirectory(Application.persistentDataPath);
            File.WriteAllText(SaveSystem.SavePath, "{ bu gecerli bir json degil [[");

            LogAssert.Expect(LogType.Error, new Regex("bozuk", RegexOptions.IgnoreCase));
            SaveData loaded = SaveSystem.Load();

            Assert.IsNull(loaded, "Bozuk dosyadan veri donmemeli.");
        }

        [Test]
        public void BosDosya_Load_NullDoner()
        {
            Directory.CreateDirectory(Application.persistentDataPath);
            File.WriteAllText(SaveSystem.SavePath, "   ");

            LogAssert.Expect(LogType.Error, new Regex("bos", RegexOptions.IgnoreCase));
            Assert.IsNull(SaveSystem.Load(), "Bos dosya null donmeli.");
        }

        [Test]
        public void DosyaYoksa_Load_NullDoner()
        {
            SaveSystem.Delete();
            Assert.IsFalse(SaveSystem.Exists(), "Silmeden sonra dosya durmamali.");

            Assert.IsNull(SaveSystem.Load(), "Kayit yokken null donmeli.");
        }

        // ------------------------------------------------------------------
        // 5) Surum kontrolu
        // ------------------------------------------------------------------

        [Test]
        public void EskiSurum_Yuklenmez()
        {
            // Oyunun TANIYAMADIGI, daha yeni bir surumden gelen kayit.
            var data = SaveSystem.Capture(
                day: 1, money: 100, xp: 0, level: 1, hourOfDay: 8f,
                parcels: new List<Parcel>(), types: _types, nextId: 1);
            data.version = SaveSystem.CurrentVersion + 1;

            // TrySave surumu sessizce dusurMEMELI, yoksa bu test anlamini yitirir.
            Assert.AreEqual(SaveSystem.CurrentVersion + 1, data.version,
                "TrySave ileri surumu sessizce dusurdu.");
            Assert.IsTrue(SaveSystem.TrySave(data));

            LogAssert.Expect(LogType.Error, new Regex("desteklenmiyor", RegexOptions.IgnoreCase));
            Assert.IsNull(SaveSystem.Load(), "ileri surumlu kayit reddedilmeli.");
        }

        [Test]
        public void EskiSurum_Yuklenir()
        {
            // Karsi yonu da kilitli: eski kayit okunamamali DEGIL.
            // Simdilik tek sema surumu var (CurrentVersion = 1), yani "eski
            // surum" 0'dir. TrySave 0'i damgalar (sema hic yazilmamis demektir),
            // bu yuzden dosya dogrudan yazilir: amac Load()'in dusuk surumu
            // REDDETMEDIGINI gormektir.
            var data = new SaveData
            {
                version = 0,
                day = 5,
                money = 640,
                xp = 80,
                level = 2,
                hourOfDay = 12f,
                nextParcelId = 1,
                parcels = new List<SavedParcel>()
            };

            Directory.CreateDirectory(Application.persistentDataPath);
            File.WriteAllText(SaveSystem.SavePath, JsonUtility.ToJson(data));

            SaveData loaded = SaveSystem.Load();
            Assert.IsNotNull(loaded, "Eski surumlu kayit reddedilmemeli.");
            Assert.AreEqual(5, loaded.day, "Gun okunmadi.");
            Assert.AreEqual(640, loaded.money, "Bakiye okunmadi.");
            Assert.AreEqual(0, loaded.parcels.Count, "Kargo listesi null gelmemeli.");
        }

        // ------------------------------------------------------------------
        // 6) Kimlik cakismasi - EN ONEMLI TEST
        // ------------------------------------------------------------------

        [Test]
        public void Tur_Ids_Cakismaz_YuklemeSonrasiYeniKargoId_Carpismaz()
        {
            // 1) Bir gun kargolarini gercekten uret (kimlikler 1..N).
            var ilkCargo = new CargoSystem(_config, _types, _vehicle);
            ilkCargo.BeginDay(1);

            var canliParcels = new List<Parcel>();
            canliParcels.AddRange(ilkCargo.Manifest.Available);
            canliParcels.AddRange(ilkCargo.Manifest.Loaded);

            int maxId = 0;
            for (int i = 0; i < canliParcels.Count; i++)
                if (canliParcels[i].Id > maxId) maxId = canliParcels[i].Id;

            Assert.Greater(maxId, 0, "Test kurgusu: kargo uretilmedi.");

            // 2) Kaydet
            var ilkProgress = new PlayerProgress(_economy);
            var data = SaveSystem.Capture(
                day: 1, money: ilkProgress.Money, xp: ilkProgress.Xp,
                level: ilkProgress.Level, hourOfDay: 8f,
                parcels: canliParcels, types: _types, nextId: maxId + 1);
            Assert.IsTrue(SaveSystem.TrySave(data));

            SaveData loaded = SaveSystem.Load();
            Assert.IsNotNull(loaded);
            Assert.AreEqual(maxId + 1, loaded.nextParcelId, "Sayac kayitta bozulmus.");

            // 3) Yukle (DUNKI oyundan, o yuzden yeni CargoSystem/PlayerProgress)
            var ikinciCargo = new CargoSystem(_config, _types, _vehicle);
            var ikinciProgress = new PlayerProgress(_economy);
            RestoreResult result = SaveSystem.Restore(loaded, ikinciProgress, ikinciCargo, _types);

            Assert.AreEqual(canliParcels.Count, result.RestoredCount, "Tum kargolar geri gelmeli.");
            Assert.AreEqual(100, ikinciProgress.Money, "Bakiye geri yuklenmeli.");

            // 4) Yeni kargo uret: kimlik cakismamali.
            Assert.Greater(result.NextParcelId, maxId,
                "Siradaki kimlik, kayittaki en buyuk kimligi asmali.");

            var yeni = new Parcel(result.NextParcelId, _types[0], "Yeni Alici", 0, 1f, 1f,
                                  ParcelPriority.Normal, 300f, 50);

            Assert.IsNull(ikinciCargo.Manifest.FindById(yeni.Id),
                "Yeni kargo kimligi zaten kullaniliyor - FindById() yanlis kargoyu donerdi.");
            Assert.AreEqual(loaded.nextParcelId, result.NextParcelId,
                "Yukleme sayaci kayit degerini degistirmemeli.");
        }

        [Test]
        public void BozukSayac_YuklemedeDuzeltilir()
        {
            // nextParcelId elle 1'e cekilmis (ya da eski bir surum hatasi):
            // yukleme bunu DUZELTMELI, yoksa 2. gunun ilk kargosu 1. gunun
            // kargosuyla ayni kimlige sahip olur ve depo iki kargoyu birlestirir.
            var cargo = new CargoSystem(_config, _types, _vehicle);
            cargo.BeginDay(1);

            var parcels = new List<Parcel>();
            parcels.AddRange(cargo.Manifest.Available);
            parcels.AddRange(cargo.Manifest.Loaded);

            int maxId = 0;
            for (int i = 0; i < parcels.Count; i++)
                if (parcels[i].Id > maxId) maxId = parcels[i].Id;

            var data = SaveSystem.Capture(
                day: 1, money: 100, xp: 0, level: 1, hourOfDay: 8f,
                parcels: parcels, types: _types, nextId: maxId + 1);

            // Sayac ELLE bozuluyor (1). Kaydetme yolunda duzeltilmeli.
            data.nextParcelId = 1;
            Assert.IsTrue(SaveSystem.TrySave(data));
            Assert.Greater(data.nextParcelId, maxId, "TrySave bozuk sayaci duzeltmedi.");

            // Okuma yolunda da duzeltilmeli. Bunu gormek icin dosya dogradan
            // yaziliyor: duzeltme yalnizca TrySave'de olsaydi test gecerdi.
            data.nextParcelId = 1;
            Directory.CreateDirectory(Application.persistentDataPath);
            File.WriteAllText(SaveSystem.SavePath, JsonUtility.ToJson(data));

            SaveData loaded = SaveSystem.Load();
            Assert.IsNotNull(loaded);
            Assert.Greater(loaded.nextParcelId, maxId,
                "Load bozuk nextParcelId'i duzeltmedi.");

            var hedefCargo = new CargoSystem(_config, _types, _vehicle);
            RestoreResult result = SaveSystem.Restore(loaded, null, hedefCargo, _types);
            Assert.Greater(result.NextParcelId, maxId, "Restore ciktisinda sayac duzeltilmedi.");

            var yeni = new Parcel(result.NextParcelId, _types[0], "Test", 0, 1f, 1f,
                                  ParcelPriority.Normal, 300f, 50);
            Assert.IsNull(hedefCargo.Manifest.FindById(yeni.Id), "Kimlik yine cakisti.");
        }

        // ------------------------------------------------------------------
        // 7) Uzerine yazma (atomiklik)
        // ------------------------------------------------------------------

        [Test]
        public void KaydetIki_Kere_UzerineYazar_VeriBozulmaz()
        {
            var ilk = SaveSystem.Capture(
                day: 1, money: 100, xp: 10, level: 1, hourOfDay: 8f,
                parcels: new List<Parcel>(), types: _types, nextId: 1);
            Assert.IsTrue(SaveSystem.TrySave(ilk), "Ilk kayit yazilamadi.");
            Assert.IsTrue(SaveSystem.Exists());

            var ikinci = SaveSystem.Capture(
                day: 4, money: 2750, xp: 480, level: 5, hourOfDay: 19.25f,
                parcels: new List<Parcel>(), types: _types, nextId: 1);
            Assert.IsTrue(SaveSystem.TrySave(ikinci), "Ikinci kayit yazilamadi.");

            SaveData loaded = SaveSystem.Load();
            Assert.IsNotNull(loaded, "Uzerine yazilan kayit okunamadi.");

            // Yarim yazma olsaydi ilk kayitta kalirdik: ikinci yazma
            // TAMAMLANMADI veya dosya karisti.
            Assert.AreEqual(4, loaded.day, "Ikinci kayit birincinin uzerine yazilmadi.");
            Assert.AreEqual(2750, loaded.money, "Bakiye eski degerde kaldi.");
            Assert.AreEqual(480, loaded.xp, "XP eski degerde kaldi.");
            Assert.AreEqual(5, loaded.level, "Seviye eski degerde kaldi.");
            Assert.AreEqual(19.25f, loaded.hourOfDay, 0.001f, "Saat eski degerde kaldi.");

            // Atomik yazmanin izi: gecici dosya geride kalmamali.
            Assert.IsFalse(File.Exists(SaveSystem.TempPath),
                "Gecici dosya silinmemis - yazma yarim kalmis olabilir.");
        }

        [Test]
        public void KaydetIki_Kere_KargoListesiDegistirilir()
        {
            // Ayni dosya, farkli kargo listesi: eski kargolar KALMAMALI.
            var ilkParcels = new List<Parcel>
            {
                new Parcel(1, _types[0], "Ilk", 0, 1f, 1f, ParcelPriority.Normal, 300f, 50)
            };
            var ikinciParcels = new List<Parcel>
            {
                new Parcel(1, _types[0], "Degisen", 2, 3f, 4f, ParcelPriority.Express, 210f, 225)
            };

            Assert.IsTrue(SaveSystem.TrySave(SaveSystem.Capture(
                day: 1, money: 100, xp: 0, level: 1, hourOfDay: 8f,
                parcels: ilkParcels, types: _types, nextId: 2)));
            Assert.IsTrue(SaveSystem.TrySave(SaveSystem.Capture(
                day: 1, money: 100, xp: 0, level: 1, hourOfDay: 8f,
                parcels: ikinciParcels, types: _types, nextId: 2)));

            SaveData loaded = SaveSystem.Load();
            Assert.IsNotNull(loaded);
            Assert.AreEqual(1, loaded.parcels.Count, "Kargo listesi birismis.");
            Assert.AreEqual("Degisen", loaded.parcels[0].recipientName,
                "Eski kargo icerigi dosyada kaldi.");
        }

        // ------------------------------------------------------------------
        // Dosya yardimcilari
        // ------------------------------------------------------------------

        [Test]
        public void SavePath_ProfilKlasorundeVeSabit()
        {
            string yol = SaveSystem.SavePath;
            Assert.AreEqual(SaveSystem.FileName, Path.GetFileName(yol),
                "Dosya adi degismemeli.");
            Assert.IsTrue(yol.StartsWith(Application.persistentDataPath),
                "Kayit persistentDataPath icinde olmali: " + yol);

            // Oyun yeniden baslatildiginda ayni kayit bulunmali.
            Assert.AreEqual(yol, SaveSystem.SavePath, "Yol her cagrida ayni olmali.");
        }

        [Test]
        public void Delete_KaydiVeGeciciDosyayiKaldirir()
        {
            Assert.IsTrue(SaveSystem.TrySave(SaveSystem.Capture(
                day: 1, money: 10, xp: 0, level: 1, hourOfDay: 8f,
                parcels: new List<Parcel>(), types: _types, nextId: 1)));

            // Yarim kalmis gecici dosya senaryosunu da taklit et.
            File.WriteAllText(SaveSystem.TempPath, "yarim");
            Assert.IsTrue(File.Exists(SaveSystem.TempPath));

            SaveSystem.Delete();

            Assert.IsFalse(SaveSystem.Exists(), "Kayit silinmedi.");
            Assert.IsFalse(File.Exists(SaveSystem.TempPath), "Gecici dosya silinmedi.");

            SaveSystem.Delete();     // ikinci cagri hata vermemeli
        }
    }
}
