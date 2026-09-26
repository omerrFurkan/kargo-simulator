using System.Collections.Generic;
using KargoSim.Cargo;
using NUnit.Framework;
using UnityEngine;

namespace KargoSim.Tests
{
    /// <summary>
    /// Asama 12 (paketleme) kural testleri.
    ///
    /// Hepsi saf C#: PackagingSystem/Parcel/CargoManifest MonoBehaviour
    /// degildir, motor olmadan da calisir. Bu yuzden saat elle
    /// surulur ("simulasyon saati" parametreleri) ve testler saniyeler
    /// icinde biter.
    ///
    /// Kuralin TEK sahibi PackagingSystem; bu testler o sahibi kilitler.
    /// "Sure dolmadan ilerlemez" ve "bitisik iki cagri ayni adimi iki kez
    /// gecirmez" maddeleri oyuncunun E tusuna basilma hizina bagli
    /// hatalarin (adim atlanmasi / iki adimin tek karede gecmesi)
    /// asil kaynagidir.
    /// </summary>
    public sealed class PackagingSystemTests
    {
        private ParcelType _type;
        private int _nextId = 1;

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
            _type.color = new Color(0.3f, 0.6f, 0.9f);
            _type.Sanitize();

            _nextId = 1;
        }

        [TearDown]
        public void TearDown()
        {
            if (_type != null) Object.DestroyImmediate(_type);
        }

        /// <summary>Depoda bekleyen, hic paketlenmemis bir kargo uretir.</summary>
        private Parcel NewParcel()
        {
            int id = _nextId++;
            return new Parcel(id, _type, "Ali Veli", 0, 2f, 4f,
                              ParcelPriority.Normal, 300f, 200);
        }

        /// <summary>
        /// Kargoyu bastan sona paketler. Saat buyuk adimlarla atlatilir:
        /// testin konusu adim suresi degil, SIRANIN sonuca ulasmasi.
        /// </summary>
        private static void PackageFully(Parcel parcel, PackagingSystem system)
        {
            system.Begin(parcel, 0f);

            float now = 0f;
            for (int guard = 0; guard < 20 && !parcel.IsPackaged; guard++)
            {
                now += 10f;
                system.Advance(parcel, now);
            }
        }

        private static CargoManifest NewManifest(bool requiresPackaging)
        {
            var manifest = new CargoManifest();
            manifest.SetCapacity(new CargoCapacity(500f, 500f));
            manifest.RequiresPackaging = requiresPackaging;
            return manifest;
        }

        // ------------------------------------------------------------------
        // 1) Adim sirasi
        // ------------------------------------------------------------------

        [Test]
        public void AdimlarSiraylaIlerler()
        {
            var system = new PackagingSystem(true);
            Parcel parcel = NewParcel();

            PackagingStep[] expected =
            {
                PackagingStep.SheetPlaced,
                PackagingStep.Cut,
                PackagingStep.Folded,
                PackagingStep.ProductInside,
                PackagingStep.Taped,
                PackagingStep.Labelled,
            };

            Assert.AreEqual(PackagingStep.None, parcel.PackagingProgress,
                "Yeni kargo hicbir adimdan baslamaz.");

            system.Begin(parcel, 0f);

            float now = 0f;
            for (int i = 0; i < expected.Length; i++)
            {
                now += 10f;
                Assert.IsTrue(system.Advance(parcel, now),
                    "Adim " + i + " (" + expected[i] + ") ilerlemeli.");
                Assert.AreEqual(expected[i], parcel.PackagingProgress,
                    "Adim sirasi atlanmamali.");
            }

            Assert.AreEqual(PackagingStep.Labelled, parcel.PackagingProgress,
                "Son adimdan sonra Labelled'da kalinmali.");
        }

        // ------------------------------------------------------------------
        // 2) Bitisik cagri
        // ------------------------------------------------------------------

        [Test]
        public void BitisikIkiCagriAyniAdimiIkiKezGecirmez()
        {
            var system = new PackagingSystem(true);
            Parcel parcel = NewParcel();

            system.Begin(parcel, 0f);

            // Ilk cagri: None -> SheetPlaced (suresi 0, aninda hazir).
            Assert.IsTrue(system.Advance(parcel, 5f), "Ilk adim aninda ilerlemeli.");
            Assert.AreEqual(PackagingStep.SheetPlaced, parcel.PackagingProgress);

            // Ayni saniyede ikinci cagri: SheetPlaced'in suresi de 0, bu yuzden
            // TEK adim daha gecmeli.
            Assert.IsTrue(system.Advance(parcel, 5f),
                "Ayni anda ikinci adim da gecmeli (SheetPlaced suresi 0).");
            Assert.AreEqual(PackagingStep.Cut, parcel.PackagingProgress);

            // Cut'in suresi 1.2s: ayni anda bir kez daha denenirse GECMEZ.
            bool third = system.Advance(parcel, 5f);
            Assert.IsFalse(third,
                "Sure dolmadan adim ilerlememeli (Cut = 1.2s).");
            Assert.AreEqual(PackagingStep.Cut, parcel.PackagingProgress,
                "Basarisiz Advance adimi degistirmemeli.");
        }

        // ------------------------------------------------------------------
        // 3-4) Sure kurali
        // ------------------------------------------------------------------

        [Test]
        public void SureDolmadanAdimIlerlemez()
        {
            var system = new PackagingSystem(true);
            Parcel parcel = NewParcel();

            system.Begin(parcel, 0f);
            system.Advance(parcel, 0f);   // -> SheetPlaced
            system.Advance(parcel, 0f);   // -> Cut (1.2s beklenecek)

            Assert.AreEqual(PackagingStep.Cut, parcel.PackagingProgress);
            float duration = PackagingSteps.DurationOf(PackagingStep.Cut);
            Assert.Greater(duration, 0f, "Cut adiminin suresi pozitif olmali.");

            Assert.IsFalse(system.IsStepReady(parcel, duration * 0.5f),
                "Yari yolda adim hazir olmamali.");

            Assert.IsFalse(system.Advance(parcel, duration - 0.05f),
                "Sure tamamlanmadan adim ilerlememeli.");
            Assert.AreEqual(PackagingStep.Cut, parcel.PackagingProgress,
                "Adim ayni kalmali.");
        }

        [Test]
        public void SureDoluncaAdimIlerler()
        {
            var system = new PackagingSystem(true);
            Parcel parcel = NewParcel();

            system.Begin(parcel, 10f);
            system.Advance(parcel, 10f);  // -> SheetPlaced
            system.Advance(parcel, 10f);  // -> Cut, bitis = 11.2

            float duration = PackagingSteps.DurationOf(PackagingStep.Cut);
            float readyAt = 10f + duration;

            Assert.IsTrue(system.IsStepReady(parcel, readyAt),
                "Sure dolunca adim hazir olmali.");
            Assert.IsTrue(system.Advance(parcel, readyAt),
                "Sure dolunca adim ilerlemeli.");
            Assert.AreEqual(PackagingStep.Folded, parcel.PackagingProgress);
        }

        // ------------------------------------------------------------------
        // 5) Kargonun durumu
        // ------------------------------------------------------------------

        [Test]
        public void PaketlenenKargoReadyDurumunaGecer()
        {
            var system = new PackagingSystem(true);
            Parcel parcel = NewParcel();

            PackageFully(parcel, system);

            Assert.IsTrue(parcel.IsPackaged, "Paket bitince IsPackaged dogru olmali.");
            Assert.AreEqual(ParcelState.Ready, parcel.State,
                "Paketlenen kargo Ready durumuna gecmeli.");
            Assert.IsTrue(parcel.IsLoadable, "Ready durumu araca yuklenebilir olmali.");
            Assert.IsFalse(parcel.IsBeingPackaged,
                "Paket bitince kargo masada olmamali.");
            Assert.AreEqual(1, system.CompletedCount,
                "Tamamlanan paket sayaci bir artmali.");
        }

        // ------------------------------------------------------------------
        // 6-8) Manifest ile yukleme
        // ------------------------------------------------------------------

        [Test]
        public void PaketlenenKargoAracaYuklenebilir()
        {
            var system = new PackagingSystem(true);
            CargoManifest manifest = NewManifest(true);
            Parcel parcel = NewParcel();

            PackageFully(parcel, system);

            Assert.IsTrue(manifest.CanLoad(parcel),
                "Paketlenmis kargo araca girmeye uygun olmali.");

            bool loaded = manifest.TryLoad(parcel, 0f);
            Assert.IsTrue(loaded, "Paketlenmis kargo araca yuklenmeli.");
            Assert.IsTrue(parcel.IsLoaded, "Kargo arac uzerinde olmali.");
        }

        [Test]
        public void PaketlenmemisKargoAracaYuklenemez()
        {
            var system = new PackagingSystem(true);
            CargoManifest manifest = NewManifest(true);
            Parcel parcel = NewParcel();

            system.Begin(parcel, 0f);
            system.Advance(parcel, 0f);
            system.Advance(parcel, 0f);
            system.Advance(parcel, 99f);   // -> Folded: henuz HAZIR degil

            Assert.IsFalse(parcel.IsPackaged, "Kargo henuz paketlenmemis olmali.");
            Assert.IsFalse(manifest.CanLoad(parcel),
                "Paketlenmemis kargo CanLoad vermemeli.");
            Assert.IsFalse(manifest.TryLoad(parcel, 0f),
                "Paketlenmemis kargo araca yuklenmemeli.");
            Assert.AreEqual(0, manifest.LoadedCount,
                "Basarisiz yuklemede arac bos kalmali.");
        }

        [Test]
        public void RequiresPackagingKapaliykenHamKargoYuklenir()
        {
            // Paketleme ZORUNLU DEGILSE eski akis aynen calisir:
            // raf -> arac. Bu, geriye donuk uyumluluk kuralidir; oyuncu
            // ayari kapattiginda eski kurali AGLAMAZ.
            var system = new PackagingSystem(false);
            CargoManifest manifest = NewManifest(false);
            Parcel parcel = NewParcel();

            Assert.AreEqual(PackagingStep.None, parcel.PackagingProgress);

            Assert.IsTrue(manifest.CanLoad(parcel),
                "Paketleme kapaliyken ham kargo araca girebilmeli.");
            Assert.IsTrue(manifest.TryLoad(parcel, 0f),
                "Paketleme kapaliyken ham kargo yuklenebilmeli.");
            Assert.IsTrue(parcel.IsLoaded, "Kargo arac uzerinde olmali.");
        }

        // ------------------------------------------------------------------
        // 9) Depoya donus
        // ------------------------------------------------------------------

        [Test]
        public void DepoyaDonenPaketlenmisKargoHazirKalir()
        {
            var system = new PackagingSystem(true);
            CargoManifest manifest = NewManifest(true);
            Parcel parcel = NewParcel();

            PackageFully(parcel, system);
            Assert.IsTrue(manifest.TryLoad(parcel, 0f));

            manifest.UnloadAll();

            Assert.AreEqual(ParcelState.Ready, parcel.State,
                "Paketlenmis kargo depoya donerken Ready kalmali.");
            Assert.IsTrue(parcel.IsPackaged,
                "Paketleme isinin izi kargoda kalmali.");
            Assert.IsTrue(manifest.CanLoad(parcel),
                "Aracindan bosaltilan paket TEKRAR yuklenebilmeli.");
        }

        // ------------------------------------------------------------------
        // 10) Masadan bosalma
        // ------------------------------------------------------------------

        [Test]
        public void MasadanBosalincaIlerlemeKorunur()
        {
            var system = new PackagingSystem(true);
            Parcel parcel = NewParcel();

            system.Begin(parcel, 0f);
            system.Advance(parcel, 0f);   // SheetPlaced
            system.Advance(parcel, 0f);   // Cut

            Assert.IsTrue(system.IsTableBusy, "Kargo masada olmali.");
            Assert.AreEqual(parcel.Id, system.TableParcelId, "Masadaki kargo o olmali.");

            Assert.IsTrue(system.Abandon(parcel), "Masadan bosaltilmali.");
            Assert.IsFalse(system.IsTableBusy, "Masa bosalmali.");
            Assert.IsFalse(system.IsOnTable(parcel), "Kargo masada olmamali.");
            Assert.AreEqual(PackagingStep.Cut, parcel.PackagingProgress,
                "Abandon ilerlemeyi SIFIRLAMAMALI; oyuncu isini kaybetmemeli.");

            // Kargo tekrar masaya girdiginde ayni adimdan devam eder.
            Assert.IsTrue(system.Begin(parcel, 30f), "Kargo tekrar masaya girebilmeli.");
            Assert.AreEqual(PackagingStep.Cut, parcel.PackagingProgress,
                "Geri donen kargo bastan baslamamali.");
            Assert.IsTrue(system.IsStepReady(parcel, 30f),
                "Baska zaman gecmis bir adim: geri donen kargo beklemek "
                + "zorunda kalmamali, kaldigi adimdan devam etmeli.");
        }

        // ------------------------------------------------------------------
        // 11) Gun sonu
        // ------------------------------------------------------------------

        [Test]
        public void GunBasindaMasaTemizlenir()
        {
            var system = new PackagingSystem(true);
            Parcel parcel = NewParcel();

            system.Begin(parcel, 0f);
            system.Advance(parcel, 0f);
            system.Advance(parcel, 0f);
            PackageFully(NewParcel(), system);

            Assert.IsTrue(system.IsTableBusy, "Masa dolu olmali.");
            Assert.AreEqual(1, system.CompletedCount);

            system.BeginDay();

            Assert.IsFalse(system.IsTableBusy, "Yeni gunde masa bosalmali.");
            Assert.AreEqual(0, system.TableParcelId, "Yeni gunde masada kargo kalmamali.");
            Assert.AreEqual(0, system.CompletedCount,
                "Gun istatistigi sifirlanmali (bugun, bu gunun sayisi).");
        }

        // ------------------------------------------------------------------
        // 12) Gecersiz kargo
        // ------------------------------------------------------------------

        [Test]
        public void GecersizKargoyuPaketlemez()
        {
            var system = new PackagingSystem(true);

            Assert.IsFalse(system.CanPackage(null), "null paketlenememeli.");
            Assert.IsFalse(system.Begin(null, 0f), "null masaya girememeli.");

            Parcel delivered = NewParcel();
            delivered.MarkDelivered();
            Assert.IsFalse(system.CanPackage(delivered), "Teslim edilmis paketlenmemeli.");
            Assert.IsFalse(system.Begin(delivered, 0f));

            Parcel loaded = NewParcel();
            loaded.MarkLoaded(0f);
            Assert.IsFalse(system.CanPackage(loaded), "Araca yuklenmis paketlenmemeli.");
            Assert.IsFalse(system.Begin(loaded, 0f));

            Parcel discarded = NewParcel();
            discarded.MarkDiscarded();
            Assert.IsFalse(system.CanPackage(discarded),
                "Oyundan kalici cikan kargo paketlenmemeli.");
            Assert.IsFalse(system.Begin(discarded, 0f));

            Assert.IsFalse(system.IsTableBusy,
                "Gecersiz kargo denemeleri masayi kirletmemeli.");
        }

        // ------------------------------------------------------------------
        // 13) Yuzde
        // ------------------------------------------------------------------

        [Test]
        public void IlerlemeYuzdesiDogruHesaplanir()
        {
            Assert.AreEqual(0f, PackagingSteps.Progress01(PackagingStep.None), 0.0001f);
            Assert.AreEqual(1f, PackagingSteps.Progress01(PackagingStep.Labelled), 0.0001f);

            var system = new PackagingSystem(true);
            Parcel parcel = NewParcel();
            Assert.AreEqual(0f, parcel.PackagingProgress01, 0.0001f);

            // Her adimda yuzde artar ve 0..1 araliginda kalir.
            float previous = 0f;
            system.Begin(parcel, 0f);

            float now = 0f;
            for (int guard = 0; guard < 10 && !parcel.IsPackaged; guard++)
            {
                now += 10f;
                system.Advance(parcel, now);

                float value = parcel.PackagingProgress01;
                Assert.GreaterOrEqual(value, previous,
                    "Yuzde geriye gitmemeli (adim " + parcel.PackagingProgress + ").");
                Assert.LessOrEqual(value, 1f, "Yuzde 1'i asmamali.");
                previous = value;
            }

            Assert.AreEqual(1f, parcel.PackagingProgress01, 0.0001f,
                "Paketlenen kargo %100 gosterilmeli.");
            Assert.AreEqual(1f / PackagingSteps.StepCount,
                PackagingSteps.Progress01(PackagingStep.SheetPlaced), 0.0001f,
                "Ilk adimda yuzde 1/6 olmali.");
        }

        // ------------------------------------------------------------------
        // 14) Sure cesitliligi
        // ------------------------------------------------------------------

        [Test]
        public void AdimSureleriFarklidir()
        {
            // Her adimin suresi AYNI olsaydi oyuncu "tik tik tik" yerine
            // adimlar arasinda tempo hissetmezdi. Kural, gorsel ve ipucu
            // metinleriyle birlikte oyunun temposunu tasiyan sey.
            //
            // None sayilmaz: o bir eylem degil, "hic baslanmadi" durumu.
            var distinct = new HashSet<float>();
            var steps = PackagingSteps.AllSteps();
            int counted = 0;

            for (int i = 0; i < steps.Length; i++)
            {
                  if (PackagingSteps.IsUntouched(steps[i])) continue;

                  // SheetPlated de bir IS degil: masaya koyma ANI. Suresi
                  // sifirdir; bekletmesi oyuncuya bir sey kazandirmaz.
                  // Beklenen tempo, kesme/katlama/urun/bant/etiket
                  // adimlarinda aranir.
                  if (steps[i] == PackagingStep.SheetPlaced) continue;


                float duration = PackagingSteps.DurationOf(steps[i]);
                Assert.Greater(duration, 0f,
                    "Gercek bir adimin suresi pozitif olmali (" + steps[i] + ").");
                distinct.Add(duration);
                counted++;
            }

              Assert.AreEqual(PackagingSteps.StepCount - 1, counted,
                  "Sayilan IS adimi sayisi StepCount - 1 olmali (SheetPlaced haric).");
              Assert.AreEqual(PackagingSteps.StepCount - 1, distinct.Count,
                  "Her is adiminin suresi farkli olmali; en az bir ikililik tempo " +
                  "doldurmaz.");
        }
    }
}
