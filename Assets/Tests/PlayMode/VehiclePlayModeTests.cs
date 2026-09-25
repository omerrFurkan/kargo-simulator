using System.Collections;
using KargoSim.Vehicle;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace KargoSim.Tests
{
    /// <summary>
    /// Gercek fizik motoruyla calisan arac testleri.
    ///
    /// EditMode testleri sadece formulleri dogrular. Burada hedef su soruyu
    /// cevaplamak: "Sahnede gercekten gecen bir arac, gaz verildiginde
    /// ilerliyor mu?" Formuller dogru olsa bile bir yerlestirme hatasi
    /// (teker yaricapi yanlis, arac zemine gomulmus, motor torqu baglanmamis)
    /// oyunu tamamen oynanmaz hale getirir.
    /// </summary>
    public sealed class VehiclePlayModeTests
    {
        /// <summary>Testte kullanilan sahte surucu. Gercek klavyeyi beklemez.</summary>
        private sealed class FakeInput : MonoBehaviour, IVehicleInput
        {
            public float ThrottleValue;
            public float SteerValue;
            public bool HandbrakeValue;

            public float Throttle => ThrottleValue;
            public float Steer => SteerValue;
            public bool Handbrake => HandbrakeValue;

            public event System.Action RespawnRequested;
            public void FireRespawn() => RespawnRequested?.Invoke();
        }

        private VehicleDefinition _definition;
        private GameObject _ground;
        private GameObject _vehicle;
        private FakeInput _input;
        private ArcadeVehicleController _controller;
        private WheelCollider[] _wheels;

        [SetUp]
        public void SetUp()
        {
            _definition = ScriptableObject.CreateInstance<VehicleDefinition>();

            // Zemin: aracin uzerinde duracagi duz kure. Kutu (BoxCollider)
            // kullanilir, Plane (MeshCollider) DEGIL: tek parca quad olan
            // bir MeshCollider WheelCollider ile guvenilir carpismaz ve
            // arac zeminden gecer. Bu, PlayMode testinin yakaladigi gercek
            // bir oyun hatasidi - bkz. README > Bilinen Unity tuzaklari.
            //
            // OLCUDEKİ COK GENIS olmali: azami hiz testi 15 saniye boyunca
            // tam gaz gidiyor ve arac yuzlerce metre yol aliyor. Kucuk bir
            // zeminde test zeminden "dusup" boşluğa kaçar ve hiz testi
            // sahte sekilde basarisiz olur.
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.name = "TestGround";
            _ground.transform.position = new Vector3(0f, -0.5f, 0f);
            _ground.transform.localScale = new Vector3(10000f, 1f, 10000f);
            _ground.transform.rotation = Quaternion.identity;

            Assert.IsNotNull(_ground.GetComponent<Collider>(),
                "Test zemini collider'siz: arac zemine degip duramaz.");

            _vehicle = BuildVehicle();
            _input = _vehicle.AddComponent<FakeInput>();
            _controller = _vehicle.GetComponent<ArcadeVehicleController>();

            // OnEnable'da abone olmus olabilir; girdi sonradan eklendigi icin
            // burada yeniden baglanti kuruyoruz.
            _controller.ConfigureForSetup(_definition, GetWheels(_vehicle.transform), _input);

            // Baglantilar kuruldu: simdi uyandir. Awake burada, hepsi hazir
            // oldugu icin hata vermeden calisir.
            _vehicle.SetActive(true);

            _wheels = _vehicle.GetComponentsInChildren<WheelCollider>(true);
            System.Array.Sort(_wheels, (a, b) => a.name.CompareTo(b.name));

            // Bu proje `autoSyncTransforms = KAPALI` ile kuruldu (performans
            // karari: bkz. README > Performans). Bedeli: yeni olusturulan
            // nesnelerin transform'lari fizik motoruna elle senkronlanmali.
            // Test sahnesi tek karede kuruldugu icin bunu burada bir kez
            // yapmamiz gerekiyor - yapmazsak arac zemini hic tanimaz ve
            // sonsuza kadar bosluga duser.
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            if (_vehicle != null) Object.DestroyImmediate(_vehicle);
            if (_ground != null) Object.DestroyImmediate(_ground);
            if (_definition != null) Object.DestroyImmediate(_definition);
        }

        private static GameObject BuildVehicle()
        {
            var root = new GameObject("TestVehicle");
            root.transform.position = new Vector3(0f, 0.8f, 0f);

            // NESNEYI PASIF KUR: PlayMode'da AddComponent aninda Awake'i
            // cagirir. Aktifken kurarsak Awake, girdi henuz atanmadigi icin
            // hata loglar ve tum testler SetUp'ta duser. Pasifken kurup
            // baglantilari yapip sonra aktiflestirmek, gercek prefab
            // akisinin (hepsi serialize edilmis halde dogmak) aynisidir.
            root.SetActive(false);

            var rb = root.AddComponent<Rigidbody>();
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // [RequireComponent(BoxCollider)] kokte bir BoxCollider ekler.
            // Bu SASI'dir ve tekerlerin hemen ustunde duracak sekilde
            // ayarlanir. Sadece tekerlerden olusan (sasisiz) arac ilk gazda
            // takla atar - bkz. README > Bilinen Unity tuzaklari.
            // DIKKAT: ayrica bir "Chassis" cocugu EKLEMEK, koktekiyle
            // birlikte iki collider yaratir ve arac sıkışıp hareket edemez.
            var chassis = root.AddComponent<ArcadeVehicleController>().GetComponent<BoxCollider>();
            chassis.center = new Vector3(0f, 0.75f, 0f);
            chassis.size = new Vector3(1.8f, 0.8f, 4.2f);

            AddWheel(root.transform, "Wheel_FL", new Vector3(-0.85f, 0.35f, 1.35f));
            AddWheel(root.transform, "Wheel_FR", new Vector3(0.85f, 0.35f, 1.35f));
            AddWheel(root.transform, "Wheel_RL", new Vector3(-0.85f, 0.35f, -1.35f));
            AddWheel(root.transform, "Wheel_RR", new Vector3(0.85f, 0.35f, -1.35f));

            return root;
        }

        private static void AddWheel(Transform parent, string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.AddComponent<WheelCollider>();
        }

        private static WheelCollider[] GetWheels(Transform parent)
        {
            var wheels = new WheelCollider[4];
            // Onden arkaya: FL, FR, RL, RR (ArcadeVehicleController ilk ikiyi yonlendirir)
            var all = parent.GetComponentsInChildren<WheelCollider>(true);
            System.Array.Sort(all, (a, b) =>
                a.name.CompareTo(b.name));  // FL, FR, RL, RR alfabetik = dogru sira
            System.Array.Copy(all, wheels, 4);
            return wheels;
        }

        /// <summary>Fizigin oturmasini bekler.</summary>
        private static IEnumerator Settle(int steps = 40)
        {
            for (int i = 0; i < steps; i++)
                yield return new WaitForFixedUpdate();
        }

        [UnityTest]
        public IEnumerator Arac_GazVerilinceIleriGider()
        {
            yield return Settle();

            float startZ = _vehicle.transform.position.z;

            _input.ThrottleValue = 1f;
            for (int i = 0; i < 120; i++)     // 120 * 0.02 sn = 2.4 saniye
                yield return new WaitForFixedUpdate();

            float travelled = _vehicle.transform.position.z - startZ;

            Assert.Greater(travelled, 1f,
                "Gaz verildigi halde arac ilerlemedi (" + travelled + " m). " +
                "Teker yaricapi, motor torqu baglantisi ya da zemin kontrol et.");
        }

        [UnityTest]
        public IEnumerator Arac_MaksimumHiziAsmaz()
        {
            yield return Settle();
            _input.ThrottleValue = 1f;

            // 12 saniye tam gaz. Azami hiz (30 m/s) coktan yakalanmis olmali.
            // En YUKSEK hiz olculur, anlik deger degil: anlik deger
            // torkun azalma evresinde salinabilir.
            float peakKmh = 0f;
            for (int i = 0; i < 600; i++)
            {
                yield return new WaitForFixedUpdate();
                peakKmh = Mathf.Max(peakKmh, _controller.SpeedKmh);
            }

            float limitKmh = _definition.maxSpeed * 3.6f;

            Assert.Less(peakKmh, limitKmh * 1.15f,
                "Arac azami hizi asti: sinir " + limitKmh.ToString("0") +
                " km/sa, olculen tepe " + peakKmh.ToString("0") + " km/sa. " +
                "Cekim-hiz azaltma formulu calismiyor.");
        }

        [UnityTest]
        public IEnumerator Arac_GazBirakilinceDurur()
        {
            yield return Settle();
            _input.ThrottleValue = 1f;
            for (int i = 0; i < 90; i++)
                yield return new WaitForFixedUpdate();

            _input.ThrottleValue = 0f;
            for (int i = 0; i < 250; i++)     // 5 saniye
                yield return new WaitForFixedUpdate();

            Assert.Less(_controller.SpeedKmh, 3f,
                "Gaz birakildiktan sonra arac durmadi (" + _controller.SpeedKmh + " km/sa). " +
                "Fren torku uygulanmiyor olabilir.");
        }

        [UnityTest]
        public IEnumerator Arac_GeriViteseGirer()
        {
            yield return Settle();

            float startZ = _vehicle.transform.position.z;
            _input.ThrottleValue = -1f;
            for (int i = 0; i < 120; i++)
                yield return new WaitForFixedUpdate();

            float travelled = _vehicle.transform.position.z - startZ;

            Assert.Less(travelled, -0.5f,
                "Geri gaz verildi ama arac ileri gitti (" + travelled + " m).");
        }

        [UnityTest]
        public IEnumerator Arac_Yonlendirir_veDoner()
        {
            yield return Settle();

            // Once dogrudan git, sonra don
            _input.ThrottleValue = 1f;
            for (int i = 0; i < 60; i++)
                yield return new WaitForFixedUpdate();

            float speedAtTurn = _controller.SpeedKmh;
            Vector3 forwardBefore = _vehicle.transform.forward;
            float yawBefore = _vehicle.transform.eulerAngles.y;

            _input.SteerValue = 1f;
            for (int i = 0; i < 60; i++)
                yield return new WaitForFixedUpdate();

            Vector3 forwardAfter = _vehicle.transform.forward;
            float turned = Vector3.Angle(forwardBefore, forwardAfter);

            // Donus OLMALI: en az 15 derece. (Burada once "Less" yazilip
            // tam TERSI dogrulanmis, test yanlislikla hep basarisiz oldu.)
            Assert.Greater(turned, 15f,
                "Direksiyon cevirdikten sonra arac yeterince donmedi. " +
                "Donus=" + turned.ToString("F1") + " derece, " +
                "hiz=" + speedAtTurn.ToString("F1") + " km/sa, " +
                "on teker acisi=" + _wheels[0].steerAngle.ToString("F1") + " derece, " +
                "yaw " + yawBefore.ToString("F0") + " -> " +
                _vehicle.transform.eulerAngles.y.ToString("F0") + ". " +
                "On tekerler yonlendirilmiyor olabilir.");
        }

        [UnityTest]
        public IEnumerator Arac_YerCekimiyleYereOturur()
        {
            yield return Settle(120);

            float y = _vehicle.transform.position.y;
            Assert.Greater(y, 0f, "Arac zeminin altina gomuldu.");
            Assert.Less(y, 2f,
                "Arac havada asili kaldi (y = " + y + "). " +
                "Teker yaricapi ya da suspansiyon degerleri hatali.");
        }

        [UnityTest]
        public IEnumerator Arac_KurtarmaIstegeUygunDoner()
        {
            yield return Settle();

            // Araci devir ve kurtarma iste
            _vehicle.transform.rotation = Quaternion.Euler(0f, 0f, 170f);
            yield return new WaitForFixedUpdate();

            Vector3 flippedUp = _vehicle.transform.up;
            _input.FireRespawn();
            yield return new WaitForFixedUpdate();

            Assert.Greater(Vector3.Dot(flippedUp, Vector3.up), -0.5f,
                "Arac cevrilmis durumda kurtarma istegini isledi.");
            Assert.Greater(Vector3.Dot(_vehicle.transform.up, Vector3.up), 0.9f,
                "Kurtarma sonrasi arac dik olmali.");
        }
    }
}
