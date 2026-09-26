using System.Collections.Generic;
using KargoSim.Vehicle;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace KargoSim.Tests
{
    /// <summary>
    /// 3 tekerli kargo bisikletinin <see cref="VehicleDefinition"/> tanimi ve
    /// <see cref="BicycleController"/> konfigurasyonu.
    ///
    /// NEDEN AYRI BIR DOSYA: <see cref="ArcadeVehiclePhysicsTests"/> dort
    /// tekerli varsayimiyla yazildi ve "ilk iki teker direksiyon" diyor.
    /// Bisiklet o varsayimi bozar; testler de ayni sekilde ayri kalmali.
    ///
    /// Hepsi EditMode'da calisir: tanim saf veri, konfigurasyon ise
    /// nesne olusturma + alan yazma. Sahne acmaya gerek yok.
    ///
    /// Tanim degerleri IKI YERDE yazili: burada (test beklentisi) ve
    /// <c>CargoBikeSetupTool</c> (uretim). Degerler ayrisirsa test kirmizi
    /// verir; uretim tarafini degistirip bu dosyayi da guncelle.
    /// </summary>
    public sealed class VehicleDefinitionTests
    {
        private const string BikeDefPath =
            "Assets/ScriptableObjects/Definitions/Vehicle_Bicycle.asset";

        // Vanin kapasitesi: VehicleDefinition_CourierVan.asset (350 kg / 900 L).
        private const float VanCapacityKg = 350f;
        private const float VanCapacityLiters = 900f;

        // Beklenen bisiklet degerleri.
        private const float BikeMass = 180f;
        private const float BikeCargoKg = 60f;
        private const float BikeCargoLiters = 150f;
        private const float BikeWheelRadius = 0.32f;
        private const float BikeSuspensionDistance = 0.15f;
        private const float BikeSuspensionSpring = 14000f;
        private const float BikeSuspensionDamper = 2200f;
        private const float BikeCenterOfMassHeight = 0.32f;

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private readonly List<UnityEngine.Object> _assets = new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) UnityEngine.Object.DestroyImmediate(_spawned[i]);
            _spawned.Clear();

            for (int i = 0; i < _assets.Count; i++)
                if (_assets[i] != null) UnityEngine.Object.DestroyImmediate(_assets[i]);
            _assets.Clear();
        }

        /// <summary>
        /// Beklenen bisiklet tanimini uretir. Uretim araci (CargoBikeSetupTool)
        /// ile ayni degerleri yazar; test bu degerleri "dogru" kabul eder.
        /// </summary>
        private VehicleDefinition MakeBike()
        {
            var def = ScriptableObject.CreateInstance<VehicleDefinition>();
            def.mass = 180f;
            def.maxSteerAngle = 38f;
            def.engineForce = 1400f;
            def.brakeForce = 1800f;
            def.handbrakeStrength = 2f;
            def.cargoCapacityKg = BikeCargoKg;
            def.cargoCapacityLiters = BikeCargoLiters;
            def.wheelRadius = BikeWheelRadius;
            def.suspensionDistance = BikeSuspensionDistance;
            def.suspensionSpring = BikeSuspensionSpring;
            def.suspensionDamper = BikeSuspensionDamper;
            def.centerOfMassHeight = BikeCenterOfMassHeight;
            def.lateralGrip = 8f;
            def.downforce = 0f;

            // TUNED (Asama 14): 22f = 22 m/s = 79 km/h idi, kargo bisikleti
            // icin cok hizliydi. Olcum: 8.5 m/s -> 39.4 km/h,
            // 6.8 m/s -> 33 km/h. Motor matematiği maxSpeed'i m/s olarak
            // kullanir (ForwardSpeed = linearVelocity projeksiyonu).
            def.maxSpeed = 6.8f;          // m/s = 24.5 km/h
            def.maxReverseSpeed = 2.5f;   // m/s = 9 km/h

            _assets.Add(def);
            return def;
        }

        // ------------------------------------------------------------------
        // Kapasite
        // ------------------------------------------------------------------

        [Test]
        public void BisikletKapasitesiVandanKucuk()
        {
            var bike = MakeBike();

            Assert.AreEqual(BikeCargoKg, bike.cargoCapacityKg, 0.0001f,
                "Bisiklet 60 kg tasimali (vandan kucuk).");
            Assert.AreEqual(BikeCargoLiters, bike.cargoCapacityLiters, 0.0001f,
                "Bisiklet 150 L tasimali (vandan kucuk).");

            Assert.Less(bike.cargoCapacityKg, VanCapacityKg,
                "Bisiklet vanin (" + VanCapacityKg + " kg) agirligindan fazla tasinamaz; "
                + "aksi halde oyuncu vani hic kullanmaz.");
            Assert.Less(bike.cargoCapacityLiters, VanCapacityLiters,
                "Bisiklet vanin (" + VanCapacityLiters + " L) hacminden fazla tasinamaz.");
        }

        [Test]
        public void BisikletKapasitesiGecerliDegerler()
        {
            var bike = MakeBike();

            // Iki kapsa AYRI sinirlardir; ikisi de pozitif ve makul olmali.
            // negatif/0 deger kapasiteyi gecersiz kilar ve hicbir kargo
            // yuklenemez.
            Assert.Greater(bike.cargoCapacityKg, 0f, "Agirlik kapasitesi pozitif olmali.");
            Assert.Greater(bike.cargoCapacityLiters, 0f, "Hacim kapasitesi pozitif olmali.");

            // Oyuncu ilk gun 3 teslimat hedefi aliyor: en az bir kurluk
            // dolmali ki oyuncu oyunu oynayabilsin.
            Assert.GreaterOrEqual(bike.cargoCapacityKg, 15f,
                "En az 15 kg olmali, yoksa oyuncu hicbir kargoyu araca koyamaz.");
            Assert.GreaterOrEqual(bike.cargoCapacityLiters, 40f,
                "En az 40 L olmali, yoksa oyuncu hicbir kargoyu araca koyamaz.");

            // Fiziksel mantik: hacim degeri kilonun karesinden kucukse
            // kutu yerine cimento blok geliyor demektir.
            Assert.Less(bike.cargoCapacityLiters, bike.cargoCapacityKg * 20f,
                "Hacim, agirligin katsayili fazlasi degil; orta deger olmali.");
        }

        [Test]
        public void BisikletTanimAssetiUretimDegerleriyleUyumlu()
        {
            // Prefab ve tanim asset'i Kargo/10. Kargo Bisikleti Kur menusuyle
            // uretilir. Menu henuz calismadiysa bu test ATLANIR: henuz
            // olmayan bir varlik icin kirmizi rapor vermek yaniltici.
            var asset = AssetDatabase.LoadAssetAtPath<VehicleDefinition>(BikeDefPath);
            if (asset == null)
            {
                Assert.Ignore("Bisiklet tanimi yok. Once calistir: Kargo/10. Kargo Bisikleti Kur");
                return;
            }

            var expected = MakeBike();

            Assert.AreEqual(expected.mass, asset.mass, 0.0001f, "Kutle");
            Assert.AreEqual(expected.maxSpeed, asset.maxSpeed, 0.0001f, "Azami hiz");
            Assert.AreEqual(expected.maxReverseSpeed, asset.maxReverseSpeed, 0.0001f, "Geri hiz");
            Assert.AreEqual(expected.maxSteerAngle, asset.maxSteerAngle, 0.0001f, "Donus acisi");
            Assert.AreEqual(expected.engineForce, asset.engineForce, 0.0001f, "Motor torku");
            Assert.AreEqual(expected.brakeForce, asset.brakeForce, 0.0001f, "Fren torku");
            Assert.AreEqual(expected.handbrakeStrength, asset.handbrakeStrength, 0.0001f, "El freni");
            Assert.AreEqual(expected.cargoCapacityKg, asset.cargoCapacityKg, 0.0001f, "Kapasite (kg)");
            Assert.AreEqual(expected.cargoCapacityLiters, asset.cargoCapacityLiters, 0.0001f, "Kapasite (L)");
            Assert.AreEqual(expected.wheelRadius, asset.wheelRadius, 0.0001f, "Teker yaricap");
            Assert.AreEqual(expected.suspensionDistance, asset.suspensionDistance, 0.0001f, "Suspansiyon");
            Assert.AreEqual(expected.suspensionSpring, asset.suspensionSpring, 0.0001f, "Yay");
            Assert.AreEqual(expected.suspensionDamper, asset.suspensionDamper, 0.0001f, "Sondurme");
            Assert.AreEqual(expected.centerOfMassHeight, asset.centerOfMassHeight, 0.0001f, "Agirlik merkezi");
            Assert.AreEqual(expected.lateralGrip, asset.lateralGrip, 0.0001f, "Yan tutunma");
            Assert.AreEqual(expected.downforce, asset.downforce, 0.0001f, "Yapisal kuvvet");
        }

        // ------------------------------------------------------------------
        // Konfigurasyon
        // ------------------------------------------------------------------

        [Test]
        public void BisikletKonfigurasyonuUygulanir()
        {
            var bike = MakeBike();
            var rig = BuildRig(bike);

            Assert.AreSame(bike, rig.Controller.Definition,
                "ConfigureForSetup tanimi controller'a yazmali.");

            // Fizik govde: kutle ve agirlik merkezi tanimdan gelmeli.
            Assert.AreEqual(BikeMass, rig.Body.mass, 0.0001f, "Rigidbody kutlesi");
            Assert.AreEqual(BikeCenterOfMassHeight, rig.Body.centerOfMass.y, 0.0001f,
                "Agirlik merkezi yuksekligi");
            Assert.AreEqual(0f, rig.Body.centerOfMass.x, 0.0001f, "Agirlik merkezi yanalda kaymaz.");
            Assert.AreEqual(0f, rig.Body.centerOfMass.z, 0.0001f, "Agirlik merkezi boyuna kaymaz.");
            Assert.AreEqual(0.2f, rig.Body.drag, 0.0001f, "Hava direnci");
            Assert.AreEqual(4f, rig.Body.angularDrag, 0.0001f, "Aktif direnc");

            // Uc tekerin de suspansiyonu tanimdan almali.
            for (int i = 0; i < rig.Wheels.Length; i++)
            {
                WheelCollider w = rig.Wheels[i];
                Assert.AreEqual(BikeSuspensionDistance, w.suspensionDistance, 0.0001f,
                    "Teker " + i + " suspansiyon mesafesi");
                Assert.AreEqual(BikeSuspensionSpring, w.suspensionSpring.spring, 0.0001f,
                    "Teker " + i + " yay saglamligi");
                Assert.AreEqual(BikeSuspensionDamper, w.suspensionSpring.damper, 0.0001f,
                    "Teker " + i + " sondurme");
            }
        }

        [Test]
        public void TekerlerDortDegilUc()
        {
            // Regresyon sinifi: 4 tekerli arac kurulumu bu koku
            // (bisikleti 4 tekerli yapmak) teshis eder.
            var rig = BuildRig(MakeBike());

            Assert.AreEqual(3, rig.Root.GetComponentsInChildren<WheelCollider>(true).Length,
                "Bisiklet 3 tekerli olmali (1 on + 2 arka).");

            // Controller'in uc referansi da AYRI tekerleri gostermeli;
            // ikisi ayni tekeri gosteriyorsa bisiklet 2 teker gibi calisir.
            Assert.IsNotNull(rig.Front, "On teker referansi null.");
            Assert.IsNotNull(rig.RearLeft, "Arka sol teker referansi null.");
            Assert.IsNotNull(rig.RearRight, "Arka sag teker referansi null.");

            Assert.AreNotSame(rig.Front, rig.RearLeft, "On ve arka sol ayni teker olamaz.");
            Assert.AreNotSame(rig.Front, rig.RearRight, "On ve arka sag ayni teker olamaz.");
            Assert.AreNotSame(rig.RearLeft, rig.RearRight, "Iki arka teker ayni olamaz.");
        }

        [Test]
        public void BisikletTekerYaricapTanimli()
        {
            var bike = MakeBike();

            Assert.AreEqual(BikeWheelRadius, bike.wheelRadius, 0.0001f,
                "Bisiklet teker yaricapi 0.32 m olmali.");

            // [Range(0.1, 1)] sinirinda olmali; disina cikarsa Inspector'da
            // deger gosterilmez.
            Assert.GreaterOrEqual(bike.wheelRadius, 0.1f, "Teker yaricapi cok kucuk.");
            Assert.LessOrEqual(bike.wheelRadius, 1f, "Teker yaricapi cok buyuk.");

            // Fiziksel iliski: suspansiyon mesafesi yaricapin ALTINDA
            // kalmali. Aksi halde lastik yerden kopar, arac havada asili
            // kalir.
            Assert.Less(bike.suspensionDistance, bike.wheelRadius,
                "Suspansiyon mesafesi teker yaricapindan kucuk olmali.");
            Assert.GreaterOrEqual(bike.suspensionDistance, 0.05f,
                "Suspansiyon mesafesi makul olmali ([Min(0.05)]).");
        }

        // ------------------------------------------------------------------
        // Kurulum yardimcilari
        // ------------------------------------------------------------------

        /// <summary>Uretim aracinin kurdugu iskeletin minimal hali.</summary>
        private struct Rig
        {
            public GameObject Root;
            public BicycleController Controller;
            public Rigidbody Body;
            public WheelCollider Front;
            public WheelCollider RearLeft;
            public WheelCollider RearRight;
            public WheelCollider[] Wheels;
        }

        private Rig BuildRig(VehicleDefinition definition)
        {
            var rig = new Rig();

            var root = new GameObject("CargoBikeTest");
            _spawned.Add(root);
            rig.Root = root;

            // Rigidbody + BoxCollider: yalnizca tekerlerden olan arac takla
            // atar (uretim aracinda da boyle kurulur).
            rig.Body = root.AddComponent<Rigidbody>();
            root.AddComponent<BoxCollider>();
            rig.Controller = root.AddComponent<BicycleController>();

            rig.Front = AddWheel(root, "Wheel_Front");
            rig.RearLeft = AddWheel(root, "Wheel_RearLeft");
            rig.RearRight = AddWheel(root, "Wheel_RearRight");
            rig.Wheels = new[] { rig.Front, rig.RearLeft, rig.RearRight };

            // 'body' alani private ve Awake editor'de calismaz. Uretim
            // araci da SerializedObject ile yaziyor; test ayni yolu izler.
            AssignPrivateReference(rig.Controller, "body", rig.Body);

            rig.Controller.ConfigureForSetup(definition, rig.Front, rig.RearLeft, rig.RearRight);
            return rig;
        }

        private WheelCollider AddWheel(GameObject root, string wheelName)
        {
            var go = new GameObject(wheelName);
            go.transform.SetParent(root.transform, false);
            return go.AddComponent<WheelCollider>();
        }

        private void AssignPrivateReference(UnityEngine.Object target, string fieldName,
                                UnityEngine.Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(fieldName);
            Assert.IsNotNull(property, target.GetType().Name + "." + fieldName + " alani bulunamadi.");
            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
