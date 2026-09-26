using System.Collections.Generic;
using System.Reflection;
using KargoSim.Cargo;
using KargoSim.Core;
using KargoSim.Player;
using KargoSim.Vehicle;
using NUnit.Framework;
using UnityEngine;

namespace KargoSim.Tests
{
    /// <summary>
    /// Kargo bisikletinin KASASINDAKI KUTULAR ve bisiklete yuklenen
    /// kargonun KAPASITE kurallari.
    ///
    /// Iki katman birlikte test edilir, cunku oyuncuya gosterdikleri birlik:
    ///
    ///   - "kargoyu yukledim" dediginde kasada bir kutu BELIRIR (gorsel),
    ///   - ama o kutu araca SIGMIYORSA hic belirmez (kapasite).
    ///
    /// Kapasite testleri <see cref="CargoSystem"/> uzerinden kurulur
    /// (<c>GameConfig</c> + <c>ParcelType</c> + <c>VehicleDefinition</c>).
    /// ParcelType araliklari min == max yapilir: ParcelGenerator bu
    /// durumda TAM olarak o degeri uretir, test rastgelelige bagli
    /// kalmaz.
    ///
    /// Hepsi EditMode'da calisir: manifest saf C#, gorsel uretimi saf
    /// nesne yaratma. Sahne veya oyun dongusu gerekmez.
    /// </summary>
    public sealed class CargoBikeTests
    {
        // Bisiklet taniminin kapasitesi. VehicleDefinitionTests ile ayni
        // degerler; tek kaynak orada.
        private const float BikeCapacityKg = 60f;
        private const float BikeCapacityLiters = 150f;

        private GameObject _host;
        private GameObject _bed;
        private CargoBikeVisual _visual;

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private readonly List<Object> _assets = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("CargoBikeTest");
            _spawned.Add(_host);

            _bed = new GameObject("CargoBed");
            _bed.transform.SetParent(_host.transform, false);
            _spawned.Add(_bed);

            _visual = _host.AddComponent<CargoBikeVisual>();
            _visual.SetCargoBedForTests(_bed.transform);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) Object.DestroyImmediate(_spawned[i]);
            _spawned.Clear();

            for (int i = 0; i < _assets.Count; i++)
                if (_assets[i] != null) Object.DestroyImmediate(_assets[i]);
            _assets.Clear();
        }

        // ------------------------------------------------------------------
        // Kutu sayisi
        // ------------------------------------------------------------------

        [Test]
        public void BosManifest_KutuUretmez()
        {
            // Bisiklet bosken kasada kutu olmamali: oyuncu "elimde kargo
            // yokken kasam doluymus" saniyor.
            _visual.Rebuild(LoadedManifest(0, 2f, 12f));

            Assert.AreEqual(0, _visual.VisibleBoxCount, "Bos kasada kutu olmamali.");
            Assert.AreEqual(0, _bed.transform.childCount, "Kasada yalniz kutu cocugu olmali.");
        }

        [Test]
        public void YuklenenKadarKutuUretir()
        {
            // Oyuncu kutu TEK TEK koyuyor; her yuklemede bir kutu daha
            // gorunmeli.
            _visual.Rebuild(LoadedManifest(3, 2f, 12f));

            Assert.AreEqual(3, _visual.VisibleBoxCount, "3 kargo -> 3 kutu.");
            Assert.AreEqual(3, _bed.transform.childCount,
                "Kutular kasa (CargoBed) altinda olmali.");
        }

        [Test]
        public void GuncellemeEskiKutulariTemizler()
        {
            // Regresyon sinifi: kutu sayisi guncellenmezse eski kutular
            // kasada kalir ve oyuncu "3 kutu yukledim, kasada 6 kutu var"
            // der.
            _visual.Rebuild(LoadedManifest(3, 2f, 12f));
            Assert.AreEqual(3, _visual.VisibleBoxCount);

            GameObject eski = _visual.VisibleBoxes[0];

            _visual.Rebuild(LoadedManifest(1, 2f, 12f));

            Assert.AreEqual(1, _visual.VisibleBoxCount,
                "Yukleme guncellenince kutu sayisi guncel olmali.");
            Assert.AreEqual(1, _bed.transform.childCount, "Eski kutular bed altinda kalmamali.");
            Assert.IsTrue(eski == null, "Onceki kutu yok edilmemis.");
        }

        // ------------------------------------------------------------------
        // Kutu olculeri
        // ------------------------------------------------------------------

        [Test]
        public void KutuOlculeriHacmeGoreDegisir()
        {
            // Hepsi ayni boyutlu kutular "kargolar ayni" izlenimi verir;
            // oyuncu hacmi kutu olculerinden okumalidir.
            _visual.Rebuild(LoadedManifest(1, 2f, 5f));
            float kucuk = SideOf(0);

            _visual.Rebuild(LoadedManifest(1, 2f, 60f));
            float buyuk = SideOf(0);

            Assert.Greater(buyuk, kucuk + 0.05f,
                "Hacim arttikca kutu buyumeli (5 L -> " + kucuk.ToString("0.00")
                + " m, 60 L -> " + buyuk.ToString("0.00") + " m).");
        }

        [Test]
        public void IzgaraTasmaGerektigindeUstKataDizer()
        {
            // Kasa tek kat sigmayacak kadar kutu: yan yana dizmek yetmez,
            // ust uste istiflenmeli. Varsayilan kasa 0.95 x 1.15 m,
            // 20 L'lik kutu ~0.36 m kenar -> kat basina 2x2 kutu.
            // 9 kutu 3 kat.
            const int count = 9;
            _visual.Rebuild(LoadedManifest(count, 5f, 20f));

            Assert.AreEqual(count, _visual.VisibleBoxCount, "Tum kutular gorsel olmali.");

            var katlar = new HashSet<float>();
            for (int i = 0; i < _visual.VisibleBoxCount; i++)
                // Mathf.Round'un "digits" overload'i Unity 6'da KALDIRILDI
                // (CS1501). Onun yerine System.Math.Round.
                katlar.Add((float)System.Math.Round(PositionOf(i).y, 2));

            Assert.Greater(katlar.Count, 1,
                "Kasa tek satira sigmiyor, kutular ust kata cikmaliydi "
                + "(hepsi ayni yukseklikte).");

            // Kutular yine de kasanin disina tasmamali: yigin aracin
            // disinda kalirsa gorsel anlamini yitirir.
            Vector2 bed = _visual.BedSize;
            for (int i = 0; i < _visual.VisibleBoxCount; i++)
            {
                Vector3 p = PositionOf(i);
                float half = SideOf(i) * 0.5f;
                Assert.LessOrEqual(Mathf.Abs(p.x) + half, bed.x * 0.5f + 0.001f,
                    "Kutu kasa genisligini asiyor (index " + i + ").");
                Assert.LessOrEqual(Mathf.Abs(p.z) + half, bed.y * 0.5f + 0.001f,
                    "Kutu kasa derinligini asiyor (index " + i + ").");
            }
        }

        // ------------------------------------------------------------------
        // Dayaniklilik
        // ------------------------------------------------------------------

        [Test]
        public void NullCargoBed_Cokmez()
        {
            // Kasa referansi kaybolursa kutular havada kalmasin.
            _visual.SetCargoBedForTests(null);

            Assert.DoesNotThrow(() => _visual.Rebuild(LoadedManifest(3, 2f, 12f)),
                "Kasa referansi yoksa hata vermemeli.");
            Assert.AreEqual(0, _visual.VisibleBoxCount,
                "Kasa referansi yokken kutu uretilmemeli.");
        }

        [Test]
        public void AboneTemizligi()
        {
            // Awake yalnizca Play'de calisir; EditMode testinde elle
            // cagrilir. Boylece statik GameEvents kuyruguna GERCEKTEN
            // abone olur ve "silindikten sonra olay firlatmaz" iddiasi
            // test edilir (abone hic kurulmamis olsaydi test bos gecerdi).
            InvokeAwake(_visual);

            int index = _spawned.IndexOf(_host);
            Object.DestroyImmediate(_host);
            _spawned.RemoveAt(index);

            Assert.DoesNotThrow(() => GameEvents.RaiseCargoChanged(),
                "Silinen gorsel olaya abone kalmamali. GameEvents statik ve "
                + "Domain Reload KAPALI calisabiliyor: birakilmayan abonelik "
                + "olu nesneyi cagirir ve sonraki testleri bozar.");
        }

        private static void InvokeAwake(CargoBikeVisual visual)
        {
            MethodInfo awake = typeof(CargoBikeVisual).GetMethod(
                "Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(awake, "CargoBikeVisual.Awake bulunamadi.");
            awake.Invoke(visual, null);
        }

        // ------------------------------------------------------------------
        // Kapasite: CargoSystem uzerinden
        // ------------------------------------------------------------------

        [Test]
        public void PaketlenmemisKargoYuklenemez()
        {
            // requiresPackaging acikken ham kargo araca giremez. Bu kural
            // manifest'te; bisiklet farkli bir kural TAnIMAZ.
            CargoSystem system = MakeSystem(true, 2f, 10f);
            system.BeginDay(1);

            Parcel raw = system.Manifest.Available[0];
            Assert.IsFalse(raw.IsPackaged, "Depodaki kargo henuz paketlenmemis olmali.");

            Assert.IsFalse(system.TryLoadParcel(raw.Id, 0f),
                "Paketlenmemis kargo araca yuklenmemeli.");
            Assert.AreEqual(0, system.Manifest.LoadedCount, "Arac bos kalmali.");
        }

        [Test]
        public void PaketlenmisKargoYuklenebilir()
        {
            CargoSystem system = MakeSystem(true, 2f, 10f);
            system.BeginDay(1);

            Parcel parcel = system.Manifest.Available[0];
            Package(parcel);

            Assert.IsTrue(parcel.IsPackaged, "Kargo paketlenmis olmali.");
            Assert.IsTrue(system.TryLoadParcel(parcel.Id, 0f),
                "Paketlenmis kargo araca girmeli.");
            Assert.AreEqual(1, system.Manifest.LoadedCount);
            Assert.AreEqual(parcel.WeightKg, system.Manifest.LoadedWeightKg, 0.0001f);
        }

        [Test]
        public void KapasiteAsilirsaYuklemeEngellenir()
        {
            // 40 kg'lik kargolar, bisikletin 60 kg siniri: biri girer,
            // ikincisi toplami asar.
            CargoSystem system = MakeSystem(false, 40f, 5f);
            system.BeginDay(1);

            Parcel first = system.Manifest.Available[0];
            Parcel second = system.Manifest.Available[1];

            Assert.IsTrue(system.TryLoadParcel(first.Id, 0f), "40 kg < 60 kg, sigmali.");
            Assert.IsFalse(system.TryLoadParcel(second.Id, 0f),
                "40 + 40 = 80 kg > 60 kg, sigmamali.");

            Assert.AreEqual(1, system.Manifest.LoadedCount, "Yalnizca ilk kargo binmeli.");
            Assert.AreEqual(2, system.Manifest.AvailableCount,
                "Sigmayan kargo ELDE kalmali; kaybolmamali.");
            Assert.IsNotNull(system.Manifest.FindById(second.Id),
                "Sigmayan kargo depoda durmali; oyuncu tekrar deneyebilmeli.");
        }

        [Test]
        public void HacimKapasitesiDolduguYuklemeyiEngeller()
        {
            // Agirlik siniri gevsek, hacim siniri dar: 100 L'lik kargo
            // biri girer, ikincisi 200 L > 150 L olur.
            CargoSystem system = MakeSystem(false, 1f, 100f);
            system.BeginDay(1);

            Parcel first = system.Manifest.Available[0];
            Parcel second = system.Manifest.Available[1];

            Assert.IsTrue(system.TryLoadParcel(first.Id, 0f), "100 L < 150 L, sigmali.");
            Assert.IsFalse(system.TryLoadParcel(second.Id, 0f),
                "100 + 100 = 200 L > 150 L, hacim sigmamali.");

            Assert.AreEqual(1, system.Manifest.LoadedCount,
                "Hacim dolduysa ikinci kargo binmemeli.");
            Assert.AreEqual(100f, system.Manifest.LoadedVolumeLiters, 0.0001f);
        }

        [Test]
        public void TekPaketBikletinKapasitesiniAsmaz()
        {
            // Bisiklet 150 L: 160 L'lik TEK bir kargo girmemeli. Aksi halde
            // oyuncu kutu sayisina bakmadan her seyi bir seferde doldurur.
            CargoSystem system = MakeSystem(false, 1f, 160f);
            system.BeginDay(1);

            Assert.IsFalse(system.TryLoadParcel(system.Manifest.Available[0].Id, 0f),
                "160 L > 150 L, tek paket bile sigmamali.");
        }

        // ------------------------------------------------------------------
        // Gorsel yardimcilar
        // ------------------------------------------------------------------

        private float SideOf(int index)
        {
            return _visual.VisibleBoxes[index].transform.localScale.x;
        }

        private Vector3 PositionOf(int index)
        {
            return _visual.VisibleBoxes[index].transform.localPosition;
        }

        /// <summary>Istenen sayida kargoyu araca yuklenmis bir manifest uretir.</summary>
        private static CargoManifest LoadedManifest(int count, float weightKg, float volumeLiters)
        {
            var parcels = new List<Parcel>(count);
            for (int i = 0; i < count; i++)
            {
                parcels.Add(new Parcel(i + 1, null, "Test Kargo", i, weightKg,
                    volumeLiters, ParcelPriority.Normal, 300f, 100));
            }

            var manifest = new CargoManifest();
            // Kapasite testin konusu degil: hepsi sigsin ki izgara ve
            // kutu sayisi test edilebilsin.
            manifest.SetCapacity(new CargoCapacity(10000f, 10000f));
            manifest.BeginDay(parcels);

            for (int i = 0; i < parcels.Count; i++) manifest.TryLoad(parcels[i]);
            return manifest;
        }

        // ------------------------------------------------------------------
        // CargoSystem kurulumu
        // ------------------------------------------------------------------

        /// <summary>
        /// Bisiklet kapasiteli, istenen agirlik/hacimde TEK tip ureten bir
        /// kargo sistemi kurar.
        ///
        /// ParcelType araliklari min == max yapildigi icin ParcelGenerator
        /// tam olarak o degeri uretir; test rastgele bir parcaya degil,
        /// hesaplanmis bir parcaya bakar.
        /// </summary>
        private CargoSystem MakeSystem(bool requiresPackaging, float weightKg, float volumeLiters)
        {
            var type = ScriptableObject.CreateInstance<ParcelType>();
            type.displayName = "Test Kargo";
            type.minWeightKg = weightKg;
            type.maxWeightKg = weightKg;
            type.minVolumeLiters = volumeLiters;
            type.maxVolumeLiters = volumeLiters;
            type.paymentPerKg = 50f;
            type.fragile = false;
            type.Sanitize();
            _assets.Add(type);

            var config = ScriptableObject.CreateInstance<GameConfig>();
            config.parcelsPerDay = 3;
            config.destinationCount = 5;
            config.baseDeliverySeconds = 300f;
            config.requiresPackaging = requiresPackaging;
            _assets.Add(config);

            var vehicle = ScriptableObject.CreateInstance<VehicleDefinition>();
            vehicle.cargoCapacityKg = BikeCapacityKg;
            vehicle.cargoCapacityLiters = BikeCapacityLiters;
            _assets.Add(vehicle);

            return new CargoSystem(config, new[] { type }, vehicle);
        }

        /// <summary>Kargoyu bastan sona paketler (domuz domuza, masada degil).</summary>
        private static void Package(Parcel parcel)
        {
            for (int guard = 0; guard < 12 && !parcel.IsPackaged; guard++)
                parcel.AdvancePackaging();
        }
    }
}
