using System.Collections.Generic;
using KargoSim.Cargo;
using KargoSim.Core;
using KargoSim.Delivery;
using KargoSim.Vehicle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace KargoSim.Tests
{
    /// <summary>
    /// Aşama 4: teslimat — rota sıralaması, süre, varış, teslim sonucu.
    /// </summary>
    public sealed class DeliverySystemTests
    {
        private ParcelType _type;
        private GameConfig _config;
        private MapLayout _layout;
        private VehicleDefinition _vehicle;
        private CargoSystem _cargo;

        [SetUp]
        public void SetUp()
        {
            _type = ScriptableObject.CreateInstance<ParcelType>();
            _type.displayName = "Test";
            _type.minWeightKg = 1f; _type.maxWeightKg = 2f;
            _type.minVolumeLiters = 1f; _type.maxVolumeLiters = 2f;
            _type.paymentPerKg = 10f;
            _type.Sanitize();

            _config = ScriptableObject.CreateInstance<GameConfig>();
            _config.parcelsPerDay = 8;

            // Bu sinif TESLIMAT kurallarini olcer, paketlemeyi degil.
            // Paketleme zorunlu birakilirsa ham kargo araca giremez ve
            // buradaki her yukleme denemesi sessizce false doner; testler
            // "0 kargo yuklendi" diye kacar. Paketleme kuralinin kendi
            // testleri PackagingSystemTests'te.
            _config.requiresPackaging = false;
            _config.destinationCount = 3;
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
                new DeliveryPointData { id = 2, displayName = "C", position = new Vector3(-30f, 0f, 0f) },
            };

            _cargo = new CargoSystem(_config, new[] { _type }, _vehicle);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in new Object[] { _type, _config, _layout, _vehicle })
                if (o != null) Object.DestroyImmediate(o);
        }

        // ------------------------------------------------------------------
        // RoutePlanner - saf algoritma
        // ------------------------------------------------------------------

        [Test]
        public void Rota_BosGirdideBosListeDoner()
        {
            var result = RoutePlanner.OrderByNearestNeighbour(Vector3.zero, new List<DeliveryStop>());
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void Rota_EnYakinDuragaOnceGider()
        {
            var stops = MakeStops(0, 1, 2);
            List<DeliveryStop> order = RoutePlanner.OrderByNearestNeighbour(Vector3.zero, stops);

            Assert.AreEqual(0, order[0].DestinationId, "Depoya en yakin durak ilk olmali (10 m).");
        }

        [Test]
        public void Rota_KalanlariSiraylaIsler()
        {
            var stops = MakeStops(0, 1, 2);
            List<DeliveryStop> order = RoutePlanner.OrderByNearestNeighbour(Vector3.zero, stops);

            // 0 (10 m) -> 1 (20 m) -> 2 (30 m)
            Assert.AreEqual(0, order[0].DestinationId);
            Assert.AreEqual(1, order[1].DestinationId);
            Assert.AreEqual(2, order[2].DestinationId);
        }

        [Test]
        public void Rota_GirdiListesiniDegistirmez()
        {
            var stops = MakeStops(2, 0, 1);
            var original = new List<int> { 2, 0, 1 };

            RoutePlanner.OrderByNearestNeighbour(Vector3.zero, stops);

            for (int i = 0; i < stops.Count; i++)
                Assert.AreEqual(original[i], stops[i].DestinationId,
                    "Rota siralama girdi listesini degistirmemeli.");
        }

        [Test]
        public void Rota_TumDuraklariIcerir()
        {
            var stops = MakeStops(0, 1, 2, 3, 4);
            List<DeliveryStop> order = RoutePlanner.OrderByNearestNeighbour(Vector3.zero, stops);

            Assert.AreEqual(stops.Count, order.Count, "Hiçbir durak atlanmamali.");

            var seen = new HashSet<int>();
            foreach (DeliveryStop s in order) seen.Add(s.DestinationId);
            Assert.AreEqual(stops.Count, seen.Count, "Duraklar tekrarlanmamali.");
        }

        [Test]
        public void Rota_YolUzunlugunuHesaplar()
        {
            var stops = MakeStops(0, 1, 2);
            List<DeliveryStop> order = RoutePlanner.OrderByNearestNeighbour(Vector3.zero, stops);

            // Sirayla: 0(10,0,0) -> 1(0,0,20) -> 2(-30,0,0)
            //   0 -> 1 : sqrt(10^2 + 20^2)   = 22.360
            //   1 -> 2 : sqrt(30^2 + 20^2)   = 36.056
            //   depo -> 0: 10.000
            float length = RoutePlanner.CalculateRouteLength(Vector3.zero, order);

            Assert.AreEqual(10f + 22.360f + 36.056f, length, 0.01f);
        }

        [Test]
        public void Rota_BosSiralamaSifirYolDoner()
        {
            Assert.AreEqual(0f,
                RoutePlanner.CalculateRouteLength(Vector3.zero, new List<DeliveryStop>()), 0.0001f);
        }

        private static List<DeliveryStop> MakeStops(params int[] ids)
        {
            var positions = new[]
            {
                new Vector3(10f, 0f, 0f),
                new Vector3(0f, 0f, 20f),
                new Vector3(-30f, 0f, 0f),
                new Vector3(5f, 0f, 5f),
                new Vector3(-5f, 0f, -40f),
            };

            var list = new List<DeliveryStop>(ids.Length);
            foreach (int id in ids)
                list.Add(new DeliveryStop(id, positions[id % positions.Length], "S" + id));

            return list;
        }

        // ------------------------------------------------------------------
        // Parcel - sure
        // ------------------------------------------------------------------

        [Test]
        public void Kargo_YukleneneKadarTamSureGosterir()
        {
            Parcel p = NewParcel(100f, 1);
            Assert.AreEqual(100f, p.RemainingSeconds(0f), 0.001f,
                "Yuklenmeden once sure islememeli.");
        }

        [Test]
        public void Kargo_SureYuklenmeAnindanIsler()
        {
            Parcel p = NewParcel(100f, 1);
            p.MarkLoaded(10f);

            Assert.AreEqual(100f, p.RemainingSeconds(10f), 0.001f);
            Assert.AreEqual(60f, p.RemainingSeconds(50f), 0.001f);
            Assert.AreEqual(0f, p.RemainingSeconds(110f), 0.001f);
        }

        [Test]
        public void Kargo_SureDoluncaIsaretlenir()
        {
            Parcel p = NewParcel(50f, 2);
            p.MarkLoaded(0f);

            Assert.IsFalse(p.IsOverdue(49f), "49 saniyede henuz gecikmemis.");
            Assert.IsTrue(p.IsOverdue(50f), "50 saniyede sure doldu.");
            Assert.IsTrue(p.IsOverdue(999f), "Sure coktan doldu.");
        }

        // ------------------------------------------------------------------
        // DeliverySystem
        // ------------------------------------------------------------------

        [Test]
        public void Teslimat_YuklenenKargodanDurakOlusur()
        {
            var system = new DeliverySystem(_layout, _cargo);
            _cargo.BeginDay(1);

            system.Tick(0.1f, false);
            _cargo.TryLoadParcel(_cargo.Manifest.Available[0].Id);
            system.Tick(0.1f, false);

            Assert.AreEqual(1, system.Route.Count, "Yuklenen kargo bir durak olusturmali.");
        }

        [Test]
        public void Teslimat_AyniAdreseGidenKargolarTekDurakOlusturur()
        {
            // Ayni hedefe 3 kargo: 3 kere durmamak dogru davranis.
            var system = new DeliverySystem(_layout, _cargo);
            system.Tick(0.1f, false);

            for (int i = 0; i < 3; i++)
            {
                Parcel p = new Parcel(i + 1, _type, "X", 0, 1f, 1f,
                                      ParcelPriority.Normal, 100f, 10);
                _cargo.Manifest.TryLoad(p, 0f);
            }

            system.Tick(0.1f, false);

            Assert.AreEqual(1, system.Route.Count, "Ayni adres icin tek durak olmali.");
            Assert.AreEqual(3, system.Route[0].Parcels.Count, "Durakta 3 kargo olmali.");
        }

        [Test]
        public void Teslimat_VaristaDurakBildirilir()
        {
            var system = new DeliverySystem(_layout, _cargo, 7f);
            Delivery.DeliveryStop arrived = null;
            System.Action<DeliveryStop> handler = s => arrived = s;
            GameEvents.DeliveryStopArrived += handler;

            try
            {
                _cargo.BeginDay(1);
                system.Tick(0.1f, false);
                _cargo.TryLoadParcel(_cargo.Manifest.Available[0].Id);
                system.Tick(0.1f, false);

                int destId = _cargo.Manifest.Loaded[0].DestinationId;
                DeliveryStop target = null;
                foreach (DeliveryStop s in system.Route) if (s.DestinationId == destId) target = s;

                Assert.IsNotNull(target, "Rota hedefi bulunamadi.");

                // Araci adrese gotur.
                system.VehiclePosition = target.Position;
                system.Tick(0.1f, false);

                Assert.IsNotNull(arrived, "DeliveryStopArrived olayi yayilmadi.");
                Assert.AreEqual(destId, arrived.DestinationId);
                Assert.IsNotNull(system.CurrentStop);
            }
            finally
            {
                GameEvents.DeliveryStopArrived -= handler;
            }
        }

        [Test]
        public void Teslimat_UzakkenVarisBildirilmez()
        {
            var system = new DeliverySystem(_layout, _cargo, 7f);
            bool arrived = false;
            System.Action<DeliveryStop> handler = s => arrived = true;
            GameEvents.DeliveryStopArrived += handler;

            try
            {
                _cargo.BeginDay(1);
                system.Tick(0.1f, false);
                _cargo.TryLoadParcel(_cargo.Manifest.Available[0].Id);
                system.Tick(0.1f, false);

                system.VehiclePosition = new Vector3(500f, 0f, 500f);
                system.Tick(0.1f, false);

                Assert.IsFalse(arrived, "Uzaktayken varis bildirilmemeli.");
            }
            finally
            {
                GameEvents.DeliveryStopArrived -= handler;
            }
        }

        [Test]
        public void Teslimat_SuresiDolmamisKargoBasariliTeslimEdilir()
        {
            var system = new DeliverySystem(_layout, _cargo, 7f);
            Delivery.DeliveryOutcome? result = null;
            System.Action<Parcel, DeliveryOutcome> handler = (p, o) => result = o;
            GameEvents.ParcelDelivered += handler;

            try
            {
                _cargo.BeginDay(1);
                system.Tick(0.1f, false);
                _cargo.TryLoadParcel(_cargo.Manifest.Available[0].Id);
                system.Tick(0.1f, false);

                int id = _cargo.Manifest.Loaded[0].Id;
                system.VehiclePosition = system.Route[0].Position;
                system.Tick(0.1f, false);

                DeliveryOutcome? outcome = system.TryDeliver(id);
                Assert.AreEqual(DeliveryOutcome.Success, outcome, "Suresinde teslim basarili olmali.");
                Assert.AreEqual(DeliveryOutcome.Success, result);
            }
            finally
            {
                GameEvents.ParcelDelivered -= handler;
            }
        }

        [Test]
        public void Teslimat_SuresiGecmisKargoGecKalir()
        {
            var system = new DeliverySystem(_layout, _cargo, 7f);

            _cargo.BeginDay(1);
            system.Tick(0.1f, false);
            _cargo.TryLoadParcel(_cargo.Manifest.Available[0].Id);
            system.Tick(0.1f, false);

            int id = _cargo.Manifest.Loaded[0].Id;
            system.VehiclePosition = system.Route[0].Position;

            // Suresi (100 s) gecsin.
            for (int i = 0; i < 200; i++) system.Tick(1f, false);

            DeliveryOutcome? outcome = system.TryDeliver(id);
            Assert.AreEqual(DeliveryOutcome.Late, outcome,
                "Sure gecmis teslimat GEC KALDI olmali, basarisiz degil.");
        }

        [Test]
        public void Teslimat_TeslimEdilenKargoAracCikar()
        {
            var system = new DeliverySystem(_layout, _cargo, 7f);

            _cargo.BeginDay(1);
            system.Tick(0.1f, false);
            _cargo.TryLoadParcel(_cargo.Manifest.Available[0].Id);
            system.Tick(0.1f, false);

            int before = _cargo.Manifest.LoadedCount;
            int id = _cargo.Manifest.Loaded[0].Id;
            system.VehiclePosition = system.Route[0].Position;
            system.Tick(0.1f, false);
            system.TryDeliver(id);

            Assert.AreEqual(before - 1, _cargo.Manifest.LoadedCount,
                "Teslim edilen kargo arac uzerinde kalmamali.");
        }

        [Test]
        public void Teslimat_TeslimSonrasiRotaODuragiIcermez()
        {
            // Regresyon: teslim edilen kargo depoya donerse, rota yeniden
            // kurulunca ayni adrese tekrar gidilirdi.
            var system = new DeliverySystem(_layout, _cargo, 7f);

            _cargo.BeginDay(1);
            system.Tick(0.1f, false);
            _cargo.TryLoadParcel(_cargo.Manifest.Available[0].Id);
            system.Tick(0.1f, false);

            int id = _cargo.Manifest.Loaded[0].Id;
            int destId = _cargo.Manifest.Loaded[0].DestinationId;
            system.VehiclePosition = system.Route[0].Position;
            system.Tick(0.1f, false);
            system.TryDeliver(id);

            system.Tick(0.1f, false);   // rota yeniden kurulsun

            foreach (DeliveryStop s in system.Route)
                Assert.AreNotEqual(destId, s.DestinationId,
                    "Teslim edilen adres rotada kalmamali.");
        }

        [Test]
        public void Teslimat_DuraktaTeslimCikarsaRotadaIlerler()
        {
            var system = new DeliverySystem(_layout, _cargo, 7f);

            _cargo.BeginDay(1);
            system.Tick(0.1f, false);

            // Iki farkli adrese birer kargo.
            for (int i = 0; i < 2; i++)
            {
                var p = new Parcel(i + 1, _type, "X", i, 1f, 1f,
                                   ParcelPriority.Normal, 100f, 10);
                _cargo.Manifest.TryLoad(p, 0f);
            }
            system.Tick(0.1f, false);
            Assert.AreEqual(2, system.Route.Count, "Iki adres = iki durak.");

            DeliveryStop first = system.Route[0];
            system.VehiclePosition = first.Position;
            system.Tick(0.1f, false);
            system.TryDeliver(first.Parcels[0].Id);

            Assert.IsNull(system.CurrentStop, "Durak bosaldiginda rota ilerlemeli.");
        }

        [Test]
        public void Teslimat_DuraktaKalmadanTeslimEdilemez()
        {
            var system = new DeliverySystem(_layout, _cargo, 7f);

            _cargo.BeginDay(1);
            system.Tick(0.1f, false);
            _cargo.TryLoadParcel(_cargo.Manifest.Available[0].Id);
            system.Tick(0.1f, false);

            int id = _cargo.Manifest.Loaded[0].Id;
            Assert.IsNull(system.TryDeliver(id),
                "Adrese varmadan teslim edilememeli (null donmeli).");
        }

        [Test]
        public void Teslimat_DuraklatmaZamanıDurdurur()
        {
            var system = new DeliverySystem(_layout, _cargo, 7f);
            _cargo.BeginDay(1);
            system.Tick(0.1f, false);

            float before = system.Elapsed;
            system.Tick(10f, true);      // duraklatilmis

            Assert.AreEqual(before, system.Elapsed, 0.0001f,
                "Duraklatilmisken simülasyon ilerlememeli.");
        }

        [Test]
        public void Teslimat_GunSonuKalanKargolarBasarisizOlur()
        {
            var system = new DeliverySystem(_layout, _cargo, 7f);
            int failed = 0;
            System.Action<Parcel, DeliveryOutcome> handler = (p, o) =>
            {
                if (o == DeliveryOutcome.Failed) failed++;
            };
            GameEvents.ParcelDelivered += handler;

            try
            {
                _cargo.BeginDay(1);
                system.Tick(0.1f, false);
                _cargo.TryLoadParcel(_cargo.Manifest.Available[0].Id);
                _cargo.TryLoadParcel(_cargo.Manifest.Available[0].Id);
                system.Tick(0.1f, false);

                int result = system.FailRemainingParcels();

                Assert.AreEqual(2, result, "Iki kargo basarisiz sayilmali.");
                Assert.AreEqual(2, failed);
                Assert.AreEqual(0, _cargo.Manifest.LoadedCount, "Gun sonunda arac bosalmali.");
            }
            finally
            {
                GameEvents.ParcelDelivered -= handler;
            }
        }

        [Test]
        public void Teslimat_GunBasindaSifirlanir()
        {
            var system = new DeliverySystem(_layout, _cargo, 7f);
            _cargo.BeginDay(1);
            system.Tick(5f, false);
            _cargo.TryLoadParcel(_cargo.Manifest.Available[0].Id);
            system.Tick(5f, false);

            system.BeginDay();

            Assert.AreEqual(0f, system.Elapsed, 0.0001f);
            Assert.AreEqual(0, system.Route.Count, "Yeni gun rotasiz baslamali.");
            Assert.IsNull(system.CurrentStop);
        }

        [Test]
        public void Teslimat_HaritadaOlmayanAdresUyarir()
        {
            // Hedef 99 haritada yok. Kargo olusturulamaz / teslim edilemez.
            var badLayout = ScriptableObject.CreateInstance<MapLayout>();
            badLayout.depotPosition = Vector3.zero;
            badLayout.deliveryPoints = new List<DeliveryPointData>();

            var system = new DeliverySystem(badLayout, _cargo, 7f);
            LogAssert.ignoreFailingMessages = true;
            try
            {
                _cargo.BeginDay(1);
                system.Tick(0.1f, false);
                _cargo.TryLoadParcel(_cargo.Manifest.Available[0].Id);
                system.Tick(0.1f, false);

                Assert.AreEqual(0, system.Route.Count,
                    "Haritada olmayan adres icin durak olusmamali.");
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
                Object.DestroyImmediate(badLayout);
            }
        }

        private Parcel NewParcel(float deadline, int destination)
        {
            return new Parcel(1, _type, "Ali", destination, 1f, 1f,
                              ParcelPriority.Normal, deadline, 10);
        }
    }
}
