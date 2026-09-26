using System.Collections.Generic;
using KargoSim.Cargo;
using KargoSim.Player;
using NUnit.Framework;
using UnityEngine;

namespace KargoSim.Tests
{
    /// <summary>
    /// Aracin kasasindaki GERCEK kutular.
    ///
    /// Oyuncu istegi: kutular tek tek yuklenip aracin arkasinda gorunmeli ve
    /// kutunun kargosu hissedilmeli (kutu buyuklugu hacimden, rengi agirliktan
    /// turemeli). Bu testler o gorselin sistem verisinden uretilip uretilmedigini
    /// ve araca uymasini korur.
    ///
    /// Hepsi EditMode'da calisir: manifest saf C#, gorsel uretimi saf
    /// nesne yaratma. Sahne veya oyun dongusu gerekmez.
    /// </summary>
    public sealed class VanCargoVisualTests
    {
        private GameObject _host;
        private GameObject _bed;
        private VanCargoVisual _visual;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("VanCargoTest");
            _bed = new GameObject("CargoBed");
            _bed.transform.SetParent(_host.transform, false);

            _visual = _host.AddComponent<VanCargoVisual>();
            _visual.SetCargoBedForTests(_bed.transform);
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
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
            // Kapasite testin konusu degil: hepsi sigsin ki "kutu sayisi"
            // test edilebilsin.
            manifest.SetCapacity(new CargoCapacity(100000f, 100000f));
            manifest.BeginDay(parcels);

            for (int i = 0; i < parcels.Count; i++) manifest.TryLoad(parcels[i]);
            return manifest;
        }

        private float SideOf(int index)
        {
            return _visual.VisibleBoxes[index].transform.localScale.x;
        }

        private Vector3 PositionOf(int index)
        {
            return _visual.VisibleBoxes[index].transform.localPosition;
        }

        private Color ReadBoxColor(int index)
        {
            MeshRenderer renderer = _visual.VisibleBoxes[index].GetComponent<MeshRenderer>();
            Assert.IsNotNull(renderer, "Kutu primitive'i MeshRenderer icermiyor.");

            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            return block.GetColor(Shader.PropertyToID("_BaseColor"));
        }

        // ------------------------------------------------------------------
        // Kutu sayisi
        // ------------------------------------------------------------------

        [Test]
        public void BosManifest_KutuUretmez()
        {
            // Arac bosken kasada kutu olmamali: oyuncu "elimde kargo yokken
            // arac doluymus" saniyor.
            _visual.Rebuild(LoadedManifest(0, 2f, 12f));

            Assert.AreEqual(0, _visual.VisibleBoxCount, "Bos arakta kutu olmamali.");
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
            // Regresyon sinifi: kutu sayisi guncellenmezse eski kutular kasada
            // kalir ve oyuncu "3 kutu yukledim, kasada 6 kutu var" der.
            _visual.Rebuild(LoadedManifest(3, 2f, 12f));
            Assert.AreEqual(3, _visual.VisibleBoxCount);

            GameObject eski = _visual.VisibleBoxes[0];

            _visual.Rebuild(LoadedManifest(1, 2f, 12f));

            Assert.AreEqual(1, _visual.VisibleBoxCount, "Yukleme guncellenince kutu sayisi guncel olmali.");
            Assert.AreEqual(1, _bed.transform.childCount, "Eski kutular bed altinda kalmamali.");
            Assert.IsTrue(eski == null, "Onceki kutu yok edilmemis.");
        }

        // ------------------------------------------------------------------
        // Kutu olculeri ve rengi (kargonun "hissi")
        // ------------------------------------------------------------------

        [Test]
        public void KutuOlculeriParcelHacmineGoreDegisir()
        {
            // Hepsi ayni boyutlu kutular "kargolar ayni" izlenimi verir;
            // oyuncu hacmi kutu olculerinden okumalidir.
            _visual.Rebuild(LoadedManifest(1, 2f, 5f));
            float kucuk = SideOf(0);

            _visual.Rebuild(LoadedManifest(1, 2f, 60f));
            float buyuk = SideOf(0);

            Assert.Greater(buyuk, kucuk,
                "Hacim arttikca kutu buyumeli (5 L -> " + kucuk + " m, 60 L -> " + buyuk + " m).");
        }

        [Test]
        public void AgirlikArttikcaKutuKoyulasir()
        {
            // Kutu agirligi gorsel olarak okunmali. Renk URP/Lit materyalinde
            // property block ile verildigi icin okumak da ayni yoldan.
            _visual.Rebuild(LoadedManifest(1, 0.6f, 12f));
            Color hafif = ReadBoxColor(0);

            _visual.Rebuild(LoadedManifest(1, 8f, 12f));
            Color agir = ReadBoxColor(0);

            Assert.Greater(hafif.maxColorComponent, 0.001f,
                "Kutu rengi verilmemis (property block bos).");
            Assert.Less(agir.grayscale, hafif.grayscale - 0.01f,
                "Agir kutu daha koyu olmali (hafif " + hafif.grayscale.ToString("0.00") +
                ", agir " + agir.grayscale.ToString("0.00") + ").");
        }

        // ------------------------------------------------------------------
        // Izgara / istifleme
        // ------------------------------------------------------------------

        [Test]
        public void IzgaraTasmaGerektigindeUstKataDizer()
        {
            // Kasa tek kat sigmayacak kadar kutu: yan yana dizmek yetmez,
            // ust uste istiflenmeli. Varsayilan kasa 1.55 x 1.85 m, 45 L'lik
            // kutu ~0.45 m kenar -> kat basina 3x3 kutu. 16 kutu 2 kat.
            const int count = 16;
            _visual.Rebuild(LoadedManifest(count, 5f, 45f));

            Assert.AreEqual(count, _visual.VisibleBoxCount, "Tum kutular gorsel olmali.");

            var katlar = new HashSet<float>();
            for (int i = 0; i < _visual.VisibleBoxCount; i++)
                // Mathf.Round'un "digits" overload'i Unity 6'da KALDIRILDI
                // (CS1501). Onun yerine System.Math.Round.
                katlar.Add((float)System.Math.Round(PositionOf(i).y, 2));

            Assert.Greater(katlar.Count, 1,
                "Kasa tek satira sigmiyor, kutular ust kata cikmaliydi (hepsi ayni yukseklikte).");

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
        public void NullManifest_Cokmez()
        {
            // Oyun acilirken kargo sistemi henuz kurulmamis olabilir;
            // gorsel bunu sessizce tolere etmeli.
            Assert.DoesNotThrow(() => _visual.Rebuild(null),
                "Null manifest hatasi vermemeli.");
            Assert.AreEqual(0, _visual.VisibleBoxCount, "Null manifest kutu uretmemeli.");
        }

        [Test]
        public void BosCargoBed_KutuUretmez()
        {
            // Kasa referansi kaybolursa kutular havada kalmasin: kutu
            // uretilmez.
            _visual.SetCargoBedForTests(null);

            Assert.DoesNotThrow(() => _visual.Rebuild(LoadedManifest(3, 2f, 12f)),
                "Kasa referansi yoksa hata vermemeli.");
            Assert.AreEqual(0, _visual.VisibleBoxCount,
                "Kasa referansi yokken kutu uretilmemeli.");
        }
    }
}
