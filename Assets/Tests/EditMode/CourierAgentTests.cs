using System.Collections.Generic;
using KargoSim.Cargo;
using KargoSim.Core;
using KargoSim.Delivery;
using KargoSim.Player;
using KargoSim.Vehicle;
using NUnit.Framework;
using UnityEngine;

namespace KargoSim.Tests.EditMode
{
    /// <summary>
    /// Kurye ajani: kargo artik arac disinda tasindigi icin ajanin
    /// "fiziksel" oldugunu ve saf mantigin BOZULMADIGINI dogrular.
    ///
    /// En onemli kural: ajan kargoyu ANINDA yuklemez. Yukleme, karakter
    /// van'a varip animasyonu bitirdikten SONRA olur. Bu, sure baslangicinin
    /// kargonun gercekten yuklendigi an olmasini garanti eder.
    /// </summary>
    public class CourierAgentTests
    {
        private GameObject _vehicleGo;
        private GameObject _courierGo;
        private CourierAgent _agent;
        private CharacterController _cc;

        private VehicleDefinition _vehicle;
        private CargoSystem _cargo;
        private DeliverySystem _delivery;
        private MapLayout _layout;

        [SetUp]
        public void SetUp()
        {
            _vehicle = ScriptableObject.CreateInstance<VehicleDefinition>();
            _vehicle.cargoCapacityKg = 500f;
            _vehicle.cargoCapacityLiters = 2000f;
            _vehicle.mass = 1200f;

            var config = ScriptableObject.CreateInstance<GameConfig>();
            config.destinationCount = 4;
            config.parcelsPerDay = 3;
            config.dayLengthSeconds = 600f;

            var types = new ParcelType[1];
            types[0] = ScriptableObject.CreateInstance<ParcelType>();
            types[0].displayName = "Kutu";
            types[0].minWeightKg = 1f;
            types[0].maxWeightKg = 2f;
            types[0].minVolumeLiters = 1f;
            types[0].maxVolumeLiters = 2f;

            _vehicleGo = new GameObject("Vehicle");
            _vehicleGo.layer = 11;
            _vehicleGo.AddComponent<Rigidbody>();
            _vehicleGo.AddComponent<ArcadeVehicleController>();

            _cargo = new CargoSystem(config, types, _vehicle);
            _cargo.BeginDay(1);

            _layout = ScriptableObject.CreateInstance<MapLayout>();
            _layout.depotPosition = Vector3.zero;
            _layout.deliveryPoints = new List<DeliveryPointData>
            {
                new DeliveryPointData { id = 0, displayName = "A", position = new Vector3(30f, 0f, 0f) },
            };

            _delivery = new DeliverySystem(_layout, _cargo, 7f);
            _delivery.BeginDay();

            _courierGo = new GameObject("Courier");
            _cc = _courierGo.AddComponent<CharacterController>();
            _cc.height = 1.75f;
            _cc.radius = 0.3f;
            _cc.center = new Vector3(0f, 0.9f, 0f);
            _agent = _courierGo.AddComponent<CourierAgent>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_courierGo);
            Object.DestroyImmediate(_vehicleGo);
            Object.DestroyImmediate(_layout);
            Object.DestroyImmediate(_vehicle);
        }

        private void ConfigureAgent()
        {
            var so = new UnityEditor.SerializedObject(_agent);
            so.FindProperty("visualRoot").objectReferenceValue = null;
            so.FindProperty("carryAnchor").objectReferenceValue = _courierGo.transform;
            so.FindProperty("vanCargoAnchor").objectReferenceValue = _vehicleGo.transform;
            so.FindProperty("animator").objectReferenceValue = null;
            so.FindProperty("controller").objectReferenceValue = _cc;
            so.FindProperty("vehicleController").objectReferenceValue =
                _vehicleGo.GetComponent<ArcadeVehicleController>();
            so.ApplyModifiedPropertiesWithoutUndo();

            _agent.Configure(_cargo, _delivery, _vehicleGo.transform, _layout.depotPosition);
        }

        /// <summary>Karakteri hedefe goturup hicbir sey yuklemeden onceki hali.</summary>
        private CourierAgent.CourierState StateAfterFirstTick()
        {
            _agent.Tick(0.016f);
            return _agent.State;
        }

        [Test]
        public void BaslangictaKarakterAracIcinde()
        {
            ConfigureAgent();

            Assert.AreEqual(CourierAgent.CourierState.InVehicle, _agent.State);
            Assert.IsFalse(_agent.IsBusy, "Baslangicta karakter arac disinda olmamali.");
        }

        [Test]
        public void YuklemeIstegiKarakteriHareketeGecirir()
        {
            ConfigureAgent();

            var first = _cargo.Manifest.Available[0];
            _agent.QueueLoad(new List<Parcel> { first });

            Assert.IsTrue(_agent.IsBusy, "Kargo istegi karakteri harekete gecirmeli.");
            Assert.AreEqual(CourierAgent.CourierState.WalkingToPickup, _agent.State,
                "Once depoya yurunmeli.");
        }

        [Test]
        public void YurumeBitmedenKargoYuklenmez()
        {
            ConfigureAgent();

            var first = _cargo.Manifest.Available[0];
            _agent.QueueLoad(new List<Parcel> { first });

            // Tek kare: karakter depoya yurumeye basladi, kargo HENUZ yuklenmedi.
            StateAfterFirstTick();

            Assert.IsFalse(first.IsLoaded,
                "Kargo yalnızca karakter van'a varip animasyonu bitince yuklenmeli. " +
                "Ilk karede yukleniyorsa sure, kargo aracda olmadan basliyor demektir.");
        }

        [Test]
        public void AyniAndaIkiKargoTalebiKabulEdilmez()
        {
            ConfigureAgent();

            var first = _cargo.Manifest.Available[0];
            var second = _cargo.Manifest.Available[1];

            _agent.QueueLoad(new List<Parcel> { first });
            CourierAgent.CourierState afterFirst = StateAfterFirstTick();

            // Ikinci istek reddedilmeli: karakter zaten yolda.
            _agent.QueueLoad(new List<Parcel> { second });

            Assert.AreEqual(afterFirst, _agent.State,
                "Karakter yururken yeni kargo talebi durumu bozmamali.");
        }

        [Test]
        public void YuklenmemisKargoTeslimEdilemez()
        {
            ConfigureAgent();

            var waiting = _cargo.Manifest.Available[0];

            _agent.QueueDeliver(waiting);

            Assert.IsFalse(_agent.IsBusy,
                "Aracda olmayan kargo adrese tasinamaz; istek reddedilmeli.");
        }

        [Test]
        public void DuraktakiKargoIcinTeslimBaslar()
        {
            ConfigureAgent();

            // Bir kargoyu gercekten yukle (mantik sistemi dogrudan).
            var parcel = _cargo.Manifest.Available[0];
            Assert.IsTrue(_cargo.TryLoadParcel(parcel.Id), "Test kurgusu: kargo yuklenemedi.");

            _agent.QueueDeliver(parcel);

            Assert.IsTrue(_agent.IsBusy);
            Assert.AreEqual(CourierAgent.CourierState.WalkingToAddress, _agent.State,
                "Karakter adrese yonlenmeli.");
        }

        [Test]
        public void DurumMetniOyuncuyaNeOldugunuAnlatir()
        {
            ConfigureAgent();

            var first = _cargo.Manifest.Available[0];
            _agent.QueueLoad(new List<Parcel> { first });

            Assert.IsNotEmpty(_agent.StatusText,
                "HUD ipucu bos kalirsa oyuncu kargonun nereye gittigini bilemez.");
        }
    }
}
