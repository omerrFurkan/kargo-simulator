using System.Collections.Generic;
using KargoSim.Cargo;
using KargoSim.Core;
using KargoSim.Vehicle;
using NUnit.Framework;
using UnityEngine;

namespace KargoSim.Tests
{
    /// <summary>
    /// Aşama 3: kargo üretimi ve yükleme mantığı.
    ///
    /// Hepsi EditMode'da calisir: ParcelGenerator ve CargoManifest saf C#
    /// siniflari, sahne veya fizik motoru gerekmez. Saniyeler icinde onlarca
    /// senaryo denenebilir.
    /// </summary>
    public sealed class CargoSystemTests
    {
        private ParcelType _type;
        private GameConfig _config;

        [SetUp]
        public void SetUp()
        {
            _type = ScriptableObject.CreateInstance<ParcelType>();
            _type.displayName = "Test Kargo";
            _type.minWeightKg = 1f;
            _type.maxWeightKg = 5f;
            _type.minVolumeLiters = 2f;
            _type.maxVolumeLiters = 10f;
            _type.paymentPerKg = 50f;
            _type.fragile = false;
            _type.Sanitize();

            _config = ScriptableObject.CreateInstance<GameConfig>();
            _config.parcelsPerDay = 12;
            _config.destinationCount = 14;
            _config.baseDeliverySeconds = 300f;
        }

        [TearDown]
        public void TearDown()
        {
            if (_type != null) Object.DestroyImmediate(_type);
            if (_config != null) Object.DestroyImmediate(_config);
        }

        private static VehicleDefinition MakeVehicle(float kg, float liters)
        {
            var v = ScriptableObject.CreateInstance<VehicleDefinition>();
            v.cargoCapacityKg = kg;
            v.cargoCapacityLiters = liters;
            return v;
        }

        // ------------------------------------------------------------------
        // ParcelGenerator
        // ------------------------------------------------------------------

        [Test]
        public void Uretim_IstenenSayidaKargoVerir()
        {
            var gen = new ParcelGenerator(new[] { _type }, 14, 300);
            gen.ResetForNewDay(1);

            List<Parcel> parcels = gen.Generate(12);

            Assert.AreEqual(12, parcels.Count);
            Assert.IsTrue(gen.Generate(0).Count == 0, "0 talep edildiğinde boş liste dönmeli.");
        }

        [Test]
        public void Uretim_KimliklerBenzersizVeArtan()
        {
            var gen = new ParcelGenerator(new[] { _type }, 14, 300);
            gen.ResetForNewDay(3);
            List<Parcel> parcels = gen.Generate(20);

            var seen = new HashSet<int>();
            for (int i = 0; i < parcels.Count; i++)
            {
                Assert.IsTrue(seen.Add(parcels[i].Id),
                    "Ayni kargo kimligi iki kez uretildi: " + parcels[i].Id);
            }
        }

        [Test]
        public void Uretim_AyniTohumAyniKargolariVerir()
        {
            // Determinizm save/load icin sart: gun 5'te hangi kargolar
            // vardi sorusu cevaplanabilir olmali.
            var genA = new ParcelGenerator(new[] { _type }, 14, 300);
            genA.ResetForNewDay(5);
            List<Parcel> a = genA.Generate(10);

            var genB = new ParcelGenerator(new[] { _type }, 14, 300);
            genB.ResetForNewDay(5);
            List<Parcel> b = genB.Generate(10);

            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].WeightKg, b[i].WeightKg, 0.0001f, "Agirlik eslesmedi (index " + i + ")");
                Assert.AreEqual(a[i].DestinationId, b[i].DestinationId, "Hedef eslesmedi (index " + i + ")");
                Assert.AreEqual(a[i].Priority, b[i].Priority, "Oncelik eslesmedi (index " + i + ")");
                Assert.AreEqual(a[i].BaseValue, b[i].BaseValue, "Deger eslesmedi (index " + i + ")");
            }
        }

        [Test]
        public void Uretim_FarkliTohumFarkliKargoVerir()
        {
            var genA = new ParcelGenerator(new[] { _type }, 14, 300);
            genA.ResetForNewDay(1);
            var genB = new ParcelGenerator(new[] { _type }, 14, 300);
            genB.ResetForNewDay(99);

            // Ayni tohumun iki kez ayni sonucu vermesi, farkli tohumun
            // farkli vermesi icin kontrol noktasi.
            float a = genA.Generate(10)[0].WeightKg;
            float b = genB.Generate(10)[0].WeightKg;

            Assert.IsTrue(System.Math.Abs(a - b) > 0.0001f,
                "Farkli tohumlar birebir ayni kargoyu uretti (" + a + ") - rastgelelik yok.");
        }

        [Test]
        public void Uretim_AgirlikVeHacimTanimlananAraliktaKalir()
        {
            var gen = new ParcelGenerator(new[] { _type }, 14, 300);
            gen.ResetForNewDay(7);
            List<Parcel> parcels = gen.Generate(60);

            foreach (Parcel p in parcels)
            {
                Assert.GreaterOrEqual(p.WeightKg, _type.minWeightKg - 0.0001f,
                    "Agirlik alt sinirin altinda: " + p.WeightKg);
                Assert.LessOrEqual(p.WeightKg, _type.maxWeightKg + 0.0001f,
                    "Agirlik ust sinirin ustunda: " + p.WeightKg);
                Assert.GreaterOrEqual(p.VolumeLiters, _type.minVolumeLiters - 0.0001f,
                    "Hacim alt sinirin altinda: " + p.VolumeLiters);
                Assert.LessOrEqual(p.VolumeLiters, _type.maxVolumeLiters + 0.0001f,
                    "Hacim ust sinirin ustunda: " + p.VolumeLiters);
            }
        }

        [Test]
        public void Uretim_HedefAdresleriGecerliAralikta()
        {
            var gen = new ParcelGenerator(new[] { _type }, 14, 300);
            gen.ResetForNewDay(11);
            List<Parcel> parcels = gen.Generate(50);

            foreach (Parcel p in parcels)
            {
                Assert.GreaterOrEqual(p.DestinationId, 0, "Hedef kimligi negatif olamaz.");
                Assert.Less(p.DestinationId, 14, "Hedef kimligi adres sayisini asmamali.");
            }
        }

        [Test]
        public void Uretim_HicbirKargoSifirDegerDegil()
        {
            // Her kargo biraz para getirmeli: 0 degerli kargo oyuncuyu
            // oyuna kirmaz eder.
            var gen = new ParcelGenerator(new[] { _type }, 14, 300);
            gen.ResetForNewDay(2);
            List<Parcel> parcels = gen.Generate(40);

            foreach (Parcel p in parcels)
                Assert.Greater(p.BaseValue, 0, "Kargo #" + p.Id + " degeri sifir.");
        }

        [Test]
        public void Uretim_AcilKargoDahaCokKazandirir()
        {
            // Ayni turlen iki kargo uretip deger karsilastirmak yerine
            // matematiksel iliskiyi dogrula: ayni agirlik, farkli oncelik.
            float weight = 4f;
            int normal = (int)System.Math.Round(_type.paymentPerKg * weight * ParcelPriority.Normal.PaymentMultiplier());
            int urgent = (int)System.Math.Round(_type.paymentPerKg * weight * ParcelPriority.Urgent.PaymentMultiplier());

            Assert.Greater(urgent, normal, "Acil kargo normalden cok kazanmali.");
        }

        [Test]
        public void Uretim_AcilKargoDahaKisaSureyeSahip()
        {
            float normal = 300f * ParcelPriority.Normal.TimeMultiplier();
            float urgent = 300f * ParcelPriority.Urgent.TimeMultiplier();

            Assert.Less(urgent, normal, "Acil kargonun suresi daha kisadir.");
        }

        [Test]
        public void Uretim_BosTipListesiHataVerir()
        {
            Assert.Throws<System.ArgumentException>(
                () => new ParcelGenerator(new ParcelType[0], 14, 300),
                "Tur listesi bosken uretim yapilamaz.");
        }

        [Test]
        public void Uretim_AdresSayisiSifirHataVerir()
        {
            Assert.Throws<System.ArgumentException>(
                () => new ParcelGenerator(new[] { _type }, 0, 300),
                "Hedef adres yoksa kargo uretilemez.");
        }

        // ------------------------------------------------------------------
        // CargoManifest - kapasite
        // ------------------------------------------------------------------

        [Test]
        public void Yukleme_KapasiteIciKargoyuKabulEder()
        {
            var manifest = new CargoManifest();
            manifest.SetCapacity(new CargoCapacity(100f, 200f));
            manifest.BeginDay(MakeParcels(3, 10f, 20f));

            Assert.IsTrue(manifest.TryLoad(manifest.Available[0]), "Kapasite ici kargo yuklenmeli.");
            Assert.AreEqual(1, manifest.LoadedCount);
            Assert.AreEqual(2, manifest.AvailableCount,
                "3 kargonun 1'i yuklendi, depoda 2 kalmali.");
        }

        [Test]
        public void Yukleme_AgirlikAsanKargoyuReddeder()
        {
            var manifest = new CargoManifest();
            manifest.SetCapacity(new CargoCapacity(15f, 1000f));   // sadece agirlik kisitli
            manifest.BeginDay(MakeParcels(3, 10f, 5f));

            Assert.IsTrue(manifest.TryLoad(manifest.Available[0]), "Ilk kargo (10 kg) sigmali.");
            Assert.IsFalse(manifest.TryLoad(manifest.Available[0]),
                "Ikinci kargo toplam 20 kg > 15 kg sigmamali.");
        }

        [Test]
        public void Yukleme_HacimAsanKargoyuReddeder()
        {
            var manifest = new CargoManifest();
            manifest.SetCapacity(new CargoCapacity(1000f, 30f));   // sadece hacim kisitli
            manifest.BeginDay(MakeParcels(3, 1f, 20f));

            Assert.IsTrue(manifest.TryLoad(manifest.Available[0]), "20 L < 30 L, sigmali.");
            Assert.IsFalse(manifest.TryLoad(manifest.Available[0]), "Toplam 40 L > 30 L, sigmamali.");
        }

        [Test]
        public void Yukleme_AyriIkiKapasiteSinirindadir()
        {
            // Regresyon: sadece agirlik siniri olsaydi, hafif ama devasa
            // paketlerle sinirsiz yuk tasinirdi.
            var manifest = new CargoManifest();
            manifest.SetCapacity(new CargoCapacity(1000f, 25f));   // agirlik cok, hacim az
            manifest.BeginDay(MakeParcels(3, 1f, 20f));

            manifest.TryLoad(manifest.Available[0]);                // 1 kg, 20 L

            Parcel next = manifest.Available[0];
            Assert.IsFalse(manifest.TryLoad(next),
                "Agirlik sigiyor ama hacim sigmaz; ikinci kargo alinmamali.");
        }

        [Test]
        public void Yukleme_KapasitesizAracHicbirKargoAlmaz()
        {
            var manifest = new CargoManifest();     // kapasite 0
            manifest.BeginDay(MakeParcels(3, 1f, 1f));

            Assert.IsFalse(manifest.TryLoad(manifest.Available[0]),
                "Kapasitesiz arac kargo almamali.");
        }

        [Test]
        public void Yukleme_SinirTamDoldurulur()
        {
            // Tam sınır durumu: yuvarlama hatası yüzünden reddedilmemeli.
            var manifest = new CargoManifest();
            manifest.SetCapacity(new CargoCapacity(10f, 10f));
            manifest.BeginDay(MakeParcels(1, 10f, 10f));

            Assert.IsTrue(manifest.TryLoad(manifest.Available[0]),
                "Kapasiteyi tam dolduran kargo sigmali.");
        }

        [Test]
        public void Yukleme_SonrakiKargoyuSigarKontrolEdilebilir()
        {
            var manifest = new CargoManifest();
            manifest.SetCapacity(new CargoCapacity(15f, 1000f));
            manifest.BeginDay(MakeParcels(3, 10f, 1f));

            Assert.IsTrue(manifest.Fits(manifest.Available[0]), "10 kg < 15 kg, sigar.");
            manifest.TryLoad(manifest.Available[0]);
            Assert.IsFalse(manifest.Fits(manifest.Available[0]),
                "10+10 > 15, sigmaz. Fits() sadece sorgular, degistirmez.");
        }

        [Test]
        public void Yukleme_AyniKargoIkiKezYuklenemez()
        {
            var manifest = new CargoManifest();
            manifest.SetCapacity(new CargoCapacity(100f, 100f));
            manifest.BeginDay(MakeParcels(2, 1f, 1f));

            Parcel parcel = manifest.Available[0];
            Assert.IsTrue(manifest.TryLoad(parcel));
            Assert.IsFalse(manifest.TryLoad(parcel),
                "Ayni kargo ikinci kez yuklenememeli.");
            Assert.AreEqual(1, manifest.LoadedCount);
        }

        [Test]
        public void Yukleme_ToplamlariDogruGunceller()
        {
            var manifest = new CargoManifest();
            manifest.SetCapacity(new CargoCapacity(100f, 200f));
            manifest.BeginDay(MakeParcels(3, 10f, 25f));

            manifest.TryLoad(manifest.Available[0]);
            manifest.TryLoad(manifest.Available[0]);

            Assert.AreEqual(20f, manifest.LoadedWeightKg, 0.001f);
            Assert.AreEqual(50f, manifest.LoadedVolumeLiters, 0.001f);
        }

        [Test]
        public void Yukleme_DolulukYuzdesiDoluKapsiyiBaskandirir()
        {
            var manifest = new CargoManifest();
            manifest.SetCapacity(new CargoCapacity(100f, 50f));
            manifest.BeginDay(MakeParcels(3, 10f, 20f));

            manifest.TryLoad(manifest.Available[0]);   // 10/100 agirlik, 20/50 hacim

            // %40 (hacim) dolu; agirlik %10. HUD %40 gostermeli.
            Assert.AreEqual(0.4f, manifest.LoadRatio, 0.001f);
        }

        [Test]
        public void Yukleme_KapBosaltmaAgirlikVeHacmiSifirlar()
        {
            var manifest = new CargoManifest();
            manifest.SetCapacity(new CargoCapacity(100f, 100f));
            manifest.BeginDay(MakeParcels(3, 10f, 10f));
            manifest.TryLoad(manifest.Available[0]);

            manifest.UnloadAll();

            Assert.IsTrue(manifest.IsEmpty);
            Assert.AreEqual(0f, manifest.LoadedWeightKg, 0.0001f);
            Assert.AreEqual(0f, manifest.LoadedVolumeLiters, 0.0001f);
        }

        [Test]
        public void Yukleme_KapBosaltmaKargolariDepoyaGeriDondurur()
        {
            var manifest = new CargoManifest();
            manifest.SetCapacity(new CargoCapacity(100f, 100f));
            manifest.BeginDay(MakeParcels(3, 10f, 10f));
            manifest.TryLoad(manifest.Available[0]);
            Assert.AreEqual(ParcelState.LoadedOnVehicle, manifest.Loaded[0].State);

            manifest.UnloadAll();

            Assert.AreEqual(ParcelState.WaitingInDepot, manifest.Available[0].State,
                "Bosaltilan kargo depoya geri donmeli.");
        }

        [Test]
        public void GunBaslangici_AraciBosaltirVeDepoyuDoldurur()
        {
            var manifest = new CargoManifest();
            manifest.SetCapacity(new CargoCapacity(100f, 100f));
            manifest.BeginDay(MakeParcels(3, 5f, 5f));
            manifest.TryLoad(manifest.Available[0]);
            Assert.AreEqual(1, manifest.LoadedCount);

            manifest.BeginDay(MakeParcels(7, 5f, 5f));

            Assert.IsTrue(manifest.IsEmpty, "Yeni gun arac bos olmali.");
            Assert.AreEqual(7, manifest.AvailableCount, "Yeni gun depoda 7 kargo olmali.");
        }

        [Test]
        public void KimlikleAramaDepoVeAracIcinCalisir()
        {
            var manifest = new CargoManifest();
            manifest.SetCapacity(new CargoCapacity(100f, 100f));
            manifest.BeginDay(MakeParcels(3, 1f, 1f));

            int depotId = manifest.Available[0].Id;
            Assert.IsNotNull(manifest.FindById(depotId), "Depodaki kargo bulunamadi.");

            manifest.TryLoad(manifest.Available[0]);
            Assert.IsNotNull(manifest.FindById(depotId), "Yuklenen kargo aracanda bulunamadi.");

            Assert.IsNull(manifest.FindById(99999), "Olmayan kimlik null donmeli.");
        }

        private static List<Parcel> MakeParcels(int count, float weight, float volume)
        {
            var list = new List<Parcel>(count);
            for (int i = 0; i < count; i++)
            {
                list.Add(new Parcel(i + 1, null, "Test", i % 5, weight, volume,
                                    ParcelPriority.Normal, 300f, 100));
            }
            return list;
        }

        // ------------------------------------------------------------------
        // CargoSystem (orkestrasyon)
        // ------------------------------------------------------------------

        [Test]
        public void Sistem_GunBasindaKargoUretir()
        {
            var vehicle = MakeVehicle(500f, 1000f);
            var system = new CargoSystem(_config, new[] { _type }, vehicle);
            try
            {
                system.BeginDay(1);

                Assert.AreEqual(12, system.Manifest.AvailableCount);
                Assert.AreEqual(12, system.GeneratedToday);
            }
            finally
            {
                Object.DestroyImmediate(vehicle);
            }
        }

        [Test]
        public void Sistem_KapacityAracTanimindanAlir()
        {
            var vehicle = MakeVehicle(123f, 456f);
            var system = new CargoSystem(_config, new[] { _type }, vehicle);
            try
            {
                Assert.AreEqual(123f, system.Manifest.Capacity.MaxWeightKg, 0.001f);
                Assert.AreEqual(456f, system.Manifest.Capacity.MaxVolumeLiters, 0.001f);
            }
            finally
            {
                Object.DestroyImmediate(vehicle);
            }
        }

        [Test]
        public void Sistem_AracsizKapasiteSifirKalir()
        {
            var system = new CargoSystem(_config, new[] { _type }, null);
            Assert.IsFalse(system.Manifest.Capacity.IsValid,
                "Arac tanimi yoksa kapasite gecersiz olmali.");
        }

        [Test]
        public void Sistem_YuklemeOlayiYayilir()
        {
            var vehicle = MakeVehicle(5000f, 5000f);
            var system = new CargoSystem(_config, new[] { _type }, vehicle);
            Parcel loaded = null;
            int changedCount = 0;

            System.Action<Parcel> onLoaded = p => loaded = p;
            System.Action onChanged = () => changedCount++;
            GameEvents.ParcelLoaded += onLoaded;
            GameEvents.CargoChanged += onChanged;

            try
            {
                system.BeginDay(1);
                int before = changedCount;
                int parcelId = system.Manifest.Available[0].Id;

                Assert.IsTrue(system.TryLoadParcel(parcelId));
                Assert.IsNotNull(loaded, "ParcelLoaded olayi yayilmadi.");
                Assert.AreEqual(parcelId, loaded.Id);
                Assert.Greater(changedCount, before, "CargoChanged olayi yayilmadi.");
            }
            finally
            {
                GameEvents.ParcelLoaded -= onLoaded;
                GameEvents.CargoChanged -= onChanged;
                Object.DestroyImmediate(vehicle);
            }
        }

        [Test]
        public void Sistem_BulunamayanKargoYuklenmez()
        {
            var vehicle = MakeVehicle(500f, 500f);
            var system = new CargoSystem(_config, new[] { _type }, vehicle);
            try
            {
                system.BeginDay(1);
                Assert.IsFalse(system.TryLoadParcel(999999), "Olmayan kargo yuklenmemeli.");
            }
            finally
            {
                Object.DestroyImmediate(vehicle);
            }
        }

        [Test]
        public void Sistem_KapasiteAsanKargoYuklenmez()
        {
            var vehicle = MakeVehicle(2f, 2f);     // neredeyse hicbir sey sigmaz
            var system = new CargoSystem(_config, new[] { _type }, vehicle);
            try
            {
                system.BeginDay(1);
                Assert.IsFalse(system.TryLoadParcel(system.Manifest.Available[0].Id),
                    "Kapasiteyi asan kargo yuklenmemeli.");
            }
            finally
            {
                Object.DestroyImmediate(vehicle);
            }
        }

        [Test]
        public void Sistem_BosTurListesiHataVerir()
        {
            Assert.Throws<System.ArgumentException>(
                () => new CargoSystem(_config, new ParcelType[0], null),
                "Tur listesi bosken sistem kurulmamali.");
        }
    }
}
