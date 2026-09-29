using NUnit.Framework;
using KargoSim.Cargo;
using KargoSim.UI;
using UnityEngine;

namespace KargoSim.Tests.EditMode
{
    /// <summary>
    /// ASAMA 31 - ESYA OZELLIK PENCERESI.
    ///
    /// Oyuncu: "kullanicinin esyalarin ozelliklerinde uzerlerine
    /// tikladiginda ya da mouse ile beklerse kucuk bir pencerede de
    /// yazsin".
    ///
    /// PENCERE BIR UI NESNESIDIR ve otomatik test edilemez. Ama icinde
    /// YAZAN METIN saf bir fonksiyondur ve burada dogrudan dogrulanir.
    /// Boylece "dogru bilgi cikiyor mu" sorusu ekran goruntulenmeden
    /// cevaplanabilir.
    ///
    /// Oyuncu ayrica "gittigimiz yere gore kargolarimizi gorelim"
    /// dedi: ALICI ve ADRES bilgisi ilk satirlarda olmali.
    /// </summary>
    public class ParcelTooltipTextTests
    {
        private static Parcel MakeParcel(bool packaged, bool loaded, float kg, float litres)
        {
            var type = ScriptableObject.CreateInstance<ParcelType>();
            type.displayName = "Elektronik";
            type.minWeightKg = kg; type.maxWeightKg = kg;
            type.minVolumeLiters = litres; type.maxVolumeLiters = litres;

            var p = new Parcel(7, type, "Derya Akin", 3,
                               kg, litres, ParcelPriority.Normal, 12f, 100);
            if (packaged)
            {
                int guard = 0;
                while (!p.IsPackaged && guard++ < 32)
                    if (!p.AdvancePackaging()) break;
            }
            if (loaded) p.MarkLoaded(0f);
            return p;
        }

        [Test]
        public void KimlikVeTurYazilir()
        {
            var p = MakeParcel(true, false, 2.9f, 8.9f);
            string s = ParcelTooltipView.Describe(p);

            Assert.IsNotEmpty(s, "Metin bos uretildi.");
            StringAssert.Contains("#7", s, "Kutu kimligi gorunmeli.");
            StringAssert.Contains("Elektronik", s, "Kargo turu gorunmeli.");
        }

        [Test]
        public void AliciVeAdresYazilir()
        {
            // "Gittigimiz yere gore kargolarimizi gorelim" istegi:
            // oyuncu kutunun KIME gidecegini pencerede gormeli.
            var p = MakeParcel(true, false, 2.9f, 8.9f);
            string s = ParcelTooltipView.Describe(p);

            StringAssert.Contains("Derya Akin", s, "Alici adi gorunmeli.");
            StringAssert.Contains("3", s, "Adres numarasi gorunmeli.");
        }

        [Test]
        public void AgirlikVeHacimYazilir()
        {
            var p = MakeParcel(true, false, 2.9f, 8.9f);
            string s = ParcelTooltipView.Describe(p);

            // AYRAC NOTU: oyun Turkce locale'de calisiyor ve
            // `ToString("0.0")` "2,9" (virgul) uretiyor. Bu DOGRU davranis.
            // Test sabit bir nokta bekleyerek hata veriyordu. Beklenen
            // deger ayni bicimlendirmeyle uretilir; boylece test
            // locale'den bagimsiz olur.
            StringAssert.Contains(p.WeightKg.ToString("0.0"), s, "Agirlik gorunmeli.");
            StringAssert.Contains(p.VolumeLiters.ToString("0.0"), s, "Hacim gorunmeli.");
            StringAssert.Contains("kg", s, "Agirlik birimi olmali.");
            StringAssert.Contains("L", s, "Hacim birimi olmali.");
        }

        [Test]
        public void PaketliKutuAracKonulabilirYazar()
        {
            var p = MakeParcel(true, false, 2.9f, 8.9f);
            string s = ParcelTooltipView.Describe(p);
            StringAssert.Contains("paketli", s,
                "Paketli kutu 'paketli' yazmali; oyuncu araca konulabilecegini gorebilmeli.");
        }

        [Test]
        public void PaketlenmemisKutuUyarir()
        {
            var p = MakeParcel(false, false, 2.9f, 8.9f);
            string s = ParcelTooltipView.Describe(p);

            StringAssert.Contains("PAKETLENMED", s,
                "Paketlenmemis kutu belirtilmeli; oyuncu neden yuklenmedigini "
                + "pencereeden anlamali.");
        }

        [Test]
        public void AractakiKutuTeslimKoduGosterir()
        {
            var p = MakeParcel(true, true, 2.9f, 8.9f);
            string s = ParcelTooltipView.Describe(p);
            StringAssert.Contains("kod", s,
                "Aracdaki kutunun teslim kodu pencerede gorunmeli.");
        }

        [Test]
        public void BosKutuCokmez()
        {
            Assert.AreEqual("", ParcelTooltipView.Describe(null),
                "Null parcel icin metin uretilmemeli; yoksa ekranda 'Null' yazar.");
        }
    }
}
