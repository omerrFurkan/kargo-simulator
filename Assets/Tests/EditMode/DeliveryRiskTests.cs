using System.Collections.Generic;
using KargoSim.Cargo;
using KargoSim.Core;
using KargoSim.Delivery;
using KargoSim.Vehicle;
using NUnit.Framework;
using UnityEngine;

namespace KargoSim.Tests
{
    /// <summary>
    /// Asama 11 - teslimat cesitliligi (risk).
    ///
    /// Iki seyi kanitlar:
    ///   1) ParcelType.baseSuccessRate, fragile ve isRare alanlari artik
    ///      GERCEKTEN okunuyor ve teslim kararini degistiriyor.
    ///   2) Zar atma deterministiktir: ayni tohum -> ayni sonuc. Testler
    ///      "sansi" tutmaz, tohum sayisini ve olculebilir oranlari kullanir.
    ///
    /// Olculeyen dagilimlar (100-200 tohum, gercek koddan olculdu):
    ///   taban oran 0.95, zamaninda teslim : ~%95 basari, ~%5 basarisiz
    ///   taban oran 0.95, gec teslim       : ~%43 basari, ~%57 basarisiz
    ///   kirkilabilir, zamaninda teslim    : ~%5 hasar, hasar en fazla %17
    ///   kirkilabilir, gec teslim          : ~%75 hasar, hasar %90 ve uzeri
    /// </summary>
    public sealed class DeliveryRiskTests
    {
        private readonly List<ParcelType> _types = new List<ParcelType>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _types.Count; i++)
                if (_types[i] != null) Object.DestroyImmediate(_types[i]);

            _types.Clear();
        }

        // ------------------------------------------------------------------
        // 1) Tur alani okunuyor mu?
        // ------------------------------------------------------------------

        [Test]
        public void BasariOrani_TurAlanindanOkur()
        {
            Parcel guvenilir = NewParcel(1, 0.95f, ParcelPriority.Normal);
            Parcel zor = NewParcel(2, 0.30f, ParcelPriority.Normal);

            float guvenilirOran = DeliveryRisk.EffectiveSuccessRate(guvenilir);
            float zorOran = DeliveryRisk.EffectiveSuccessRate(zor);

            Assert.AreEqual(0.95f, guvenilirOran, 0.0001f,
                "Normal oncelikli kargoda oran, turun taban degerine esit olmali.");
            Assert.AreEqual(0.30f, zorOran, 0.0001f,
                "Dusuk taban oranli kargo daha zor olmali.");
            Assert.Greater(guvenilirOran, zorOran,
                "ParcelType.baseSuccessRate okunmuyorsa iki oran ayni cikar.");
        }

        [Test]
        public void BasariOrani_OncelikVeNadirKargoSadeceKucukEtkiler()
        {
            Parcel normal = NewParcel(1, 0.90f, ParcelPriority.Normal);
            Parcel nadir = NewParcel(2, 0.90f, ParcelPriority.Normal, false, true);
            Parcel hizli = NewParcel(3, 0.90f, ParcelPriority.Express);
            Parcel acil = NewParcel(4, 0.90f, ParcelPriority.Urgent);

            float taban = DeliveryRisk.EffectiveSuccessRate(normal);
            float enFazlaFark = DeliveryRisk.EffectiveSuccessRate(acil) - taban;

            Assert.Greater(DeliveryRisk.EffectiveSuccessRate(nadir), taban,
                "Nadir kargo (isRare) bir ayricalik almali.");
            Assert.Greater(DeliveryRisk.EffectiveSuccessRate(hizli), taban,
                "Yuksek oncelik taban orani dusurmemeli, sadece notrlestirmeli.");
            Assert.GreaterOrEqual(DeliveryRisk.EffectiveSuccessRate(acil),
                DeliveryRisk.EffectiveSuccessRate(hizli),
                "Acil kargo, hizli kargodan dusuk olmamali.");
            Assert.LessOrEqual(enFazlaFark, 0.06f,
                "Oncelik duzeltmesi kucuk olmali: kritik teslimat cok daha kolay "
                + "ya da zor OLMAMALI. Fark: " + enFazlaFark);
        }

        // ------------------------------------------------------------------
        // 2) Kirpma
        // ------------------------------------------------------------------

        [Test]
        public void BasariOrani_SinirDisinaCikmaz()
        {
            Assert.AreEqual(1f,
                DeliveryRisk.EffectiveSuccessRate(NewParcel(1, 1f, ParcelPriority.Urgent)), 0.0001f,
                "Taban 1.0 + acil duzeltmesi 1'in ustune cikmamali.");

            Assert.AreEqual(1f,
                DeliveryRisk.EffectiveSuccessRate(NewParcel(2, 1f, ParcelPriority.Urgent, false, true)), 0.0001f,
                "Nadir kargo bonusu da 1'i gecmemeli.");

            Assert.AreEqual(0f,
                DeliveryRisk.EffectiveSuccessRate(NewParcel(3, 0f, ParcelPriority.Express)), 0.0001f,
                "Taban 0 iken nadir bonusu 0'in altina inememeli.");

            // Bozuk asset (Inspector'da elle bozulmus veri) da kirpilir.
            // Sanitize CAGRULANMAMIS tip kullanilir, cunku Sanitize zaten
            // kirpiyor ve testin konusu degil.
            Assert.AreEqual(0f, DeliveryRisk.EffectiveSuccessRate(NewRawParcel(4, -0.5f)), 0.0001f,
                "Negatif taban orani 0'a kirpilmali.");
            Assert.AreEqual(1f, DeliveryRisk.EffectiveSuccessRate(NewRawParcel(5, 3f)), 0.0001f,
                "1'den buyuk taban orani 1'e kirpilmali.");
        }

        // ------------------------------------------------------------------
        // 3-4) Zaman baski
        // ------------------------------------------------------------------

        [Test]
        public void ZamanindaTeslim_BasariyiArtirir()
        {
            Parcel parcel = NewParcel(1, 0.95f, ParcelPriority.Normal);

            float zamaninda = DeliveryRisk.SuccessProbability(parcel, 1f);
            float yari = DeliveryRisk.SuccessProbability(parcel, 0.1f);
            float gec = DeliveryRisk.SuccessProbability(parcel, 0f);

            Assert.Greater(zamaninda, yari,
                "Kalan sure %100 iken basari orani en yuksek olmali.");
            Assert.Greater(yari, gec,
                "Kalan sure azaldikca basari orani duser.");
            Assert.LessOrEqual(zamaninda, 1f);
            Assert.GreaterOrEqual(gec, 0f);

            Assert.AreEqual(zamaninda, DeliveryRisk.SuccessProbability(parcel, 5f), 0.0001f,
                "1'den buyuk kalan oran kirpilmis olmali.");
            Assert.AreEqual(gec, DeliveryRisk.SuccessProbability(parcel, -5f), 0.0001f,
                "Negatif kalan oran kirpilmis olmali.");
        }

        [Test]
        public void ZamanindaTeslim_BasarisizlikOraniniAzaltir()
        {
            Parcel parcel = NewParcel(1, 0.95f, ParcelPriority.Normal);

            int zamanindaBasarisiz = 0, gecBasarisiz = 0;
            for (int seed = 0; seed < 200; seed++)
            {
                if (DeliveryRisk.RollOutcome(parcel, 1f, seed) == DeliveryOutcome.Failed)
                    zamanindaBasarisiz++;

                if (DeliveryRisk.RollOutcome(parcel, 0.1f, seed) == DeliveryOutcome.Failed)
                    gecBasarisiz++;
            }

            Assert.Less(zamanindaBasarisiz, gecBasarisiz,
                "Zamaninda teslimde basarisizlik (" + zamanindaBasarisiz + "/200), gec "
                + "teslimdekinden (" + gecBasarisiz + "/200) kucuk olmali.");
            Assert.Less(zamanindaBasarisiz, 40,
                "Taban orani 0.95 iken zamaninda teslim pratikte basarili olmali.");
        }

        [Test]
        public void ZamanindaTeslim_SureDoluncaGecKaldiVerir()
        {
            Parcel parcel = NewParcel(1, 0.95f, ParcelPriority.Normal);

            int gecKalan = 0, zamanindaBasarili = 0, basarisiz = 0;
            for (int seed = 0; seed < 200; seed++)
            {
                DeliveryOutcome o = DeliveryRisk.RollOutcome(parcel, 0f, seed);
                if (o == DeliveryOutcome.Late) gecKalan++;
                else if (o == DeliveryOutcome.Success) zamanindaBasarili++;
                else basarisiz++;
            }

            Assert.AreEqual(0, zamanindaBasarili,
                "Sure dolmus teslim 'zamaninda basarili' sayilamaz.");
            Assert.Greater(gecKalan, 0, "Gec teslimde en az bir GEC KALDI olmali.");
            Assert.Greater(basarisiz, 0, "Gec teslimde risk gercekten ise yaramali.");
        }

        // ------------------------------------------------------------------
        // 5-6) Tohum
        // ------------------------------------------------------------------

        [Test]
        public void AyniTohum_AyniSonuc()
        {
            Parcel parcel = NewParcel(7, 0.95f, ParcelPriority.Normal, true);
            const int Seed = 123456;

            for (int i = 0; i < 20; i++)
            {
                Assert.AreEqual(DeliveryRisk.RollOutcome(parcel, 1f, Seed),
                                DeliveryRisk.RollOutcome(parcel, 1f, Seed),
                    "Ayni tohum ayni sonucu vermeli (tekrar " + i + ").");
                Assert.AreEqual(DeliveryRisk.RollOutcome(parcel, 0f, Seed),
                                DeliveryRisk.RollOutcome(parcel, 0f, Seed),
                    "Gec teslimde de ayni tohum ayni sonucu vermeli.");
                Assert.AreEqual(DeliveryRisk.FragileDamage(parcel, 0f, Seed),
                                DeliveryRisk.FragileDamage(parcel, 0f, Seed),
                    "Hasar cekimi de deterministik olmali.");
            }

            Assert.AreEqual(DeliveryRisk.OutcomeSeed(3, 42), DeliveryRisk.OutcomeSeed(3, 42));
            Assert.AreNotEqual(DeliveryRisk.OutcomeSeed(3, 42), DeliveryRisk.OutcomeSeed(4, 42),
                "Farkli gun ayni tohumu vermemeli.");
            Assert.AreNotEqual(DeliveryRisk.OutcomeSeed(3, 42), DeliveryRisk.OutcomeSeed(3, 43),
                "Farkli kargo ayni tohumu vermemeli.");
            Assert.AreNotEqual(DeliveryRisk.OutcomeSeed(3, 42), DeliveryRisk.DamageSeed(3, 42),
                "Sonuc ve hasar cekimleri ayri kanallar olmali.");
        }

        [Test]
        public void FarkliTohum_FarkliSonucDondur()
        {
            // %50 oran: iki sonucun da gorunmesi beklenir.
            Parcel parcel = NewParcel(1, 0.50f, ParcelPriority.Normal);

            int basarili = 0, basarisiz = 0;
            for (int seed = 0; seed < 200; seed++)
            {
                if (DeliveryRisk.RollOutcome(parcel, 1f, seed) == DeliveryOutcome.Success) basarili++;
                else basarisiz++;
            }

            Assert.AreEqual(200, basarili + basarisiz);
            Assert.Greater(basarili, 0, "Farkli tohumlar farkli sonuc vermeli.");
            Assert.Greater(basarisiz, 0,
                "Tohum gercekten etkili mi, yoksa hep ayni mi donuyor?");
        }

        [Test]
        public void FarkliTohum_GunDegisinceFarkliKararVerir()
        {
            // Tohum "gun + kargo kimligi"nden turer. Ayni kargo her gun ayni
            // karari verirse oyuncu riski bir kalip gibi ezberler.
            int farkli = 0;
            for (int id = 1; id <= 40; id++)
                if (DeliveryRisk.OutcomeSeed(1, id) != DeliveryRisk.OutcomeSeed(2, id)) farkli++;

            Assert.AreEqual(40, farkli, "Her kargo icin gun degisince tohum degismeli.");
        }

        // ------------------------------------------------------------------
        // 7-9) Kirilabilirlik
        // ------------------------------------------------------------------

        [Test]
        public void KirilabilirKargo_ZamanindaHasarsizGecer()
        {
            Parcel kirilgan = NewParcel(1, 0.95f, ParcelPriority.Normal, true);

            int hasarli = 0;
            int enBuyukHasar = 0;
            for (int seed = 0; seed < 100; seed++)
            {
                int hasar = DeliveryRisk.FragileDamage(kirilgan, 1f, seed);
                if (hasar > 0) hasarli++;
                if (hasar > enBuyukHasar) enBuyukHasar = hasar;
            }

            Assert.Less(hasarli, 15,
                "Zamaninda teslimde kirkilabilir hasar nadir olmali (100 tohumda "
                + hasarli + " kargo hasar gordu).");
            Assert.LessOrEqual(enBuyukHasar, 30,
                "Zamaninda teslimde hasar kucuk olmali, en buyuk: " + enBuyukHasar + "%.");

            Assert.AreEqual(0, DeliveryRisk.FragileDamage(kirilgan, 1f, 0),
                "Zamaninda teslim edilen kirkilabilir kargo belirli bir tohumda "
                + "hasarsiz gecmeli.");
        }

        [Test]
        public void KirilabilirDegil_AslaHasarAlmaz()
        {
            Parcel saglam = NewParcel(1, 0.95f, ParcelPriority.Normal, false);

            for (int seed = -50; seed < 500; seed++)
            {
                Assert.AreEqual(0, DeliveryRisk.FragileDamage(saglam, 0f, seed),
                    "Kirilabilir olmayan kargo hicbir kosulda hasar almamali (tohum "
                    + seed + ").");
                Assert.AreEqual(0, DeliveryRisk.FragileDamage(saglam, 1f, seed),
                    "Kirilabilir olmayan kargo zamaninda teslimde de hasarsiz.");
            }

            Assert.AreEqual(0, DeliveryRisk.FragileDamage(null, 0f, 1),
                "Kargo yoksa hasar da yok (istisna degil).");
        }

        [Test]
        public void Kirilgan_GecTeslimdeHasarOlasiligiDahaYuksek()
        {
            Parcel kirilgan = NewParcel(1, 0.95f, ParcelPriority.Normal, true);

            int zamanindaHasarli = 0, gecHasarli = 0;
            int gecEnKucukHasar = 101;

            for (int seed = 0; seed < 100; seed++)
            {
                if (DeliveryRisk.FragileDamage(kirilgan, 1f, seed) > 0) zamanindaHasarli++;
                if (DeliveryRisk.FragileDamage(kirilgan, 0.2f, seed) > 0) gecHasarli++;

                int gecHasar = DeliveryRisk.FragileDamage(kirilgan, 0f, seed);
                if (gecHasar > 0 && gecHasar < gecEnKucukHasar) gecEnKucukHasar = gecHasar;
            }

            Assert.Greater(gecHasarli, zamanindaHasarli,
                "Gec teslimde hasar olasiligi zamanindan yuksek olmali ("
                + gecHasarli + " > " + zamanindaHasarli + ").");
            Assert.Greater(gecEnKucukHasar, 50,
                "Gec teslimde hasar agir olmali, en kucuk: " + gecEnKucukHasar + "%.");
        }

        [Test]
        public void KirilganHasar_SifirIleYuzArasiArasindaKalir()
        {
            Parcel kirilgan = NewParcel(1, 0.95f, ParcelPriority.Normal, true);

            for (int seed = 0; seed < 200; seed++)
            {
                for (float ratio = 0f; ratio <= 1f; ratio += 0.25f)
                {
                    int hasar = DeliveryRisk.FragileDamage(kirilgan, ratio, seed);
                    Assert.GreaterOrEqual(hasar, 0);
                    Assert.LessOrEqual(hasar, 100, "Hasar yuzdesi 0..100 araliginda olmali.");
                }
            }
        }

        // ------------------------------------------------------------------
        // 10) Arac disi kargo
        // ------------------------------------------------------------------

        [Test]
        public void TeslimSonucu_AracDisiKurulumdaCokmez()
        {
            Assert.AreEqual(0f, DeliveryRisk.EffectiveSuccessRate(null), 0.0001f,
                "Kargo yoksa oran 0 (basarisiz) olmali.");
            Assert.AreEqual(DeliveryOutcome.Failed,
                DeliveryRisk.RollOutcome(null, 1f, 12345),
                "Null kargo BASARISIZ donmeli, istisna atmamali.");
            Assert.AreEqual(DeliveryOutcome.Failed,
                DeliveryRisk.RollOutcome(null, 0f, 0),
                "Null kargo gec teslimde de BASARISIZ olmali.");
            Assert.AreEqual(0, DeliveryRisk.FragileDamage(null, 1f, 7));

            // Turu tanimsiz kargo (bozuk veri) da cokmez.
            Parcel turuYok = new Parcel(9, null, "Ali", 0, 1f, 1f,
                                        ParcelPriority.Normal, 100f, 10);
            Assert.AreEqual(0f, DeliveryRisk.EffectiveSuccessRate(turuYok), 0.0001f);
            Assert.AreEqual(DeliveryOutcome.Failed,
                DeliveryRisk.RollOutcome(turuYok, 1f, 99),
                "Turu bilinmeyen kargo guvenilir sayilmamali.");
            Assert.AreEqual(0, DeliveryRisk.FragileDamage(turuYok, 0f, 99));
        }

        [Test]
        public void KalanSureOrani_KargodanOkunur()
        {
            Parcel parcel = NewParcel(1, 0.95f, ParcelPriority.Normal);

            Assert.AreEqual(1f, DeliveryRisk.RemainingRatio(parcel, 50f), 0.0001f,
                "Yuklenmeden once oran 1 olmali (sayac baslamadi).");

            parcel.MarkLoaded(0f);
            Assert.AreEqual(1f, DeliveryRisk.RemainingRatio(parcel, 0f), 0.0001f);
            Assert.AreEqual(0.5f, DeliveryRisk.RemainingRatio(parcel, 50f), 0.0001f);
            Assert.AreEqual(0f, DeliveryRisk.RemainingRatio(parcel, 100f), 0.0001f,
                "Sure dolduysa oran 0 (kirpilmis) olmali.");
            Assert.AreEqual(0f, DeliveryRisk.RemainingRatio(parcel, 500f), 0.0001f,
                "Sure coktan gectiyse oran 0'da kalmali, negatif olmamali.");
            Assert.AreEqual(0f, DeliveryRisk.RemainingRatio(null, 10f), 0.0001f);
        }

        // ------------------------------------------------------------------
        // Yardimcilar
        // ------------------------------------------------------------------

        /// <summary>Her kargo icin AYRI bir tur: testler birbirini kirletmesin.</summary>
        private Parcel NewParcel(int id, float baseSuccessRate, ParcelPriority priority,
                                 bool fragile = false, bool rare = false)
        {
            ParcelType type = NewType();
            type.baseSuccessRate = baseSuccessRate;
            type.fragile = fragile;
            type.isRare = rare;
            type.Sanitize();

            return new Parcel(id, type, "Ali", 1, 1f, 1f, priority, 100f, 10);
        }

        /// <summary>Sanitize CAGRULMAMIS tur (bozuk asset taklidi).</summary>
        private Parcel NewRawParcel(int id, float baseSuccessRate)
        {
            ParcelType type = NewType();
            type.baseSuccessRate = baseSuccessRate;

            return new Parcel(id, type, "Ali", 1, 1f, 1f,
                              ParcelPriority.Normal, 100f, 10);
        }

        private ParcelType NewType()
        {
            ParcelType type = ScriptableObject.CreateInstance<ParcelType>();
            type.displayName = "Test";
            type.minWeightKg = 1f; type.maxWeightKg = 2f;
            type.minVolumeLiters = 1f; type.maxVolumeLiters = 2f;
            type.paymentPerKg = 10f;
            _types.Add(type);
            return type;
        }
    }

    /// <summary>
    /// Asama 11 - ENTEGRASYON: risk karari teslimat sisteminden geciyor.
    ///
    /// Buradaki asil konu su: deger kaybi EKONOMIYE DOKUNMADAN nasil
    /// aktariliyor? Cevap: kirilabilir kargo hasar gorduyse teslim
    /// BASARISIZ sayiliyor (mevcut DeliveryOutcome ucunlugu yeterli), ve
    /// hasar yuzdesi DeliverySystem.LastFragileDamage ile okunuyor.
    /// </summary>
    public sealed class DeliveryRiskSystemTests
    {
        private ParcelType _type;
        private GameConfig _config;
        private MapLayout _layout;
        private VehicleDefinition _vehicle;
        private CargoSystem _cargo;

        private DeliveryOutcome? _firstOutcome;
        private int _firstDamage;

        [SetUp]
        public void SetUp()
        {
            _type = ScriptableObject.CreateInstance<ParcelType>();
            _type.displayName = "Test";
            _type.minWeightKg = 1f; _type.maxWeightKg = 2f;
            _type.minVolumeLiters = 1f; _type.maxVolumeLiters = 2f;
            _type.paymentPerKg = 10f;
            _type.baseSuccessRate = 0.95f;
            _type.fragile = false;
            _type.isRare = false;
            _type.Sanitize();

            _config = ScriptableObject.CreateInstance<GameConfig>();
            _config.parcelsPerDay = 4;
            _config.destinationCount = 2;

            // Bu sinif BASARI/hasar oranlarini olcer. Paketleme zorunlu
            // olsaydi kargolar araca hic giremez ve oran testleri bos
            // kargoyla calisirdi.
            _config.requiresPackaging = false;
            _config.baseDeliverySeconds = 100f;

            _vehicle = ScriptableObject.CreateInstance<VehicleDefinition>();
            _vehicle.cargoCapacityKg = 1000f;
            _vehicle.cargoCapacityLiters = 1000f;

            _layout = ScriptableObject.CreateInstance<MapLayout>();
            _layout.depotPosition = Vector3.zero;
            _layout.deliveryPoints = new List<DeliveryPointData>
            {
                new DeliveryPointData { id = 0, displayName = "A", position = new Vector3(10f, 0f, 0f) },
                new DeliveryPointData { id = 1, displayName = "B", position = new Vector3(0f, 0f, 20f) },
            };

            _cargo = new CargoSystem(_config, new[] { _type }, _vehicle);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in new Object[] { _type, _config, _layout, _vehicle })
                if (o != null) Object.DestroyImmediate(o);
        }

        /// <summary>
        /// Kirkilabilir kargo hasar gorduyse teslim BASARISIZ olur ve
        /// hasar yuzdesi LastFragileDamage'den okunur. Ekonomi tarafi
        /// aynen calisir: Failed odemesi = 0 + ceza.
        /// </summary>
        [Test]
        public void Teslimat_KirilabilirKargoHasarliysaBasarisizOlur()
        {
            _type.fragile = true;
            var system = new DeliverySystem(_layout, _cargo, 7f);

            // Gecikmis teslim: kalan sure orani 0, hasar olasiligi en yuksek.
            var parcel = new Parcel(11, _type, "Ali", 0, 1f, 1f,
                                    ParcelPriority.Normal, 10f, 10);
            _cargo.Manifest.TryLoad(parcel, 0f);

            system.Tick(0.1f, false);
            system.VehiclePosition = system.Route[0].Position;
            system.Tick(0.1f, false);

            Assert.IsNotNull(system.CurrentStop, "Adrese varilmadi.");

            for (int i = 0; i < 20; i++) system.Tick(1f, false);   // sure doldu

            DeliveryOutcome? outcome = system.TryDeliver(11);

            Assert.Greater(system.LastFragileDamage, 0,
                "Bu tohumda gec teslim edilen kirkilabilir kargo hasar gordugu icin "
                + "hasar yuzdesi okunabilir olmali.");
            Assert.AreEqual(DeliveryOutcome.Failed, outcome,
                "Hasarli teslim BASARISIZ sayilmali (deger kaybi buradan gecer).");
            Assert.LessOrEqual(system.LastFragileDamage, 100);
        }

        [Test]
        public void Teslimat_KirilabilirDegilseHasarSifirKalir()
        {
            _type.fragile = false;
            var system = new DeliverySystem(_layout, _cargo, 7f);

            var parcel = new Parcel(11, _type, "Ali", 0, 1f, 1f,
                                    ParcelPriority.Normal, 100f, 10);
            _cargo.Manifest.TryLoad(parcel, 0f);

            system.Tick(0.1f, false);
            system.VehiclePosition = system.Route[0].Position;
            system.Tick(0.1f, false);

            DeliveryOutcome? outcome = system.TryDeliver(11);

            Assert.AreEqual(0, system.LastFragileDamage,
                "Kirilabilir olmayan kargo hasar goremez (sure dolmus olsa bile).");
            Assert.AreEqual(DeliveryOutcome.Success, outcome,
                "Zamaninda teslim edilen saglam kargo basarili olmali.");
        }

        /// <summary>
        /// Ayni kargo, ayni gun, ayni karar: iki ayri sistem ayni sonucu
        /// vermeli. Aksi halde "kader" degil, zarlik olurdu.
        /// </summary>
        [Test]
        public void Teslimat_AyniKargoAyniGunAyniKararVerir()
        {
            _type.fragile = true;

            for (int deneme = 0; deneme < 3; deneme++)
            {
                var system = new DeliverySystem(_layout, _cargo, 7f);

                var parcel = new Parcel(21, _type, "Ali", 0, 1f, 1f,
                                        ParcelPriority.Normal, 100f, 10);
                _cargo.Manifest.TryLoad(parcel, 0f);

                system.Tick(0.1f, false);
                system.VehiclePosition = system.Route[0].Position;
                system.Tick(0.1f, false);

                DeliveryOutcome? outcome = system.TryDeliver(21);

                if (deneme == 0)
                {
                    _firstOutcome = outcome;
                    _firstDamage = system.LastFragileDamage;
                    continue;
                }

                Assert.AreEqual(_firstOutcome, outcome,
                    "Ayni kargo (id 21), ayni gun ayni sonucu vermeli.");
                Assert.AreEqual(_firstDamage, system.LastFragileDamage,
                    "Ayni kargo, ayni gun ayni hasari vermeli.");
            }
        }

        [Test]
        public void Teslimat_AdreseVarilmadanHasarSifirlanir()
        {
            _type.fragile = true;
            var system = new DeliverySystem(_layout, _cargo, 7f);

            var parcel = new Parcel(11, _type, "Ali", 0, 1f, 1f,
                                    ParcelPriority.Normal, 10f, 10);
            _cargo.Manifest.TryLoad(parcel, 0f);

            system.Tick(0.1f, false);

            Assert.IsNull(system.TryDeliver(11),
                "Adrese varmadan teslim edilememeli.");
            Assert.AreEqual(0, system.LastFragileDamage,
                "Teslim olmayan bir deneme hasar birikmemeli.");
        }
    }
}
