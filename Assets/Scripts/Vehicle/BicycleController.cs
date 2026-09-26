using UnityEngine;

namespace KargoSim.Vehicle
{
    /// <summary>
    /// 3 TEKERLEKLI KARGO BISIKLETI (ilk arac).
    ///
    /// NEDEN AYRI SINIF, ArcadeVehicleController DEGIL:
    /// Mevcut controller dort teker varsayiyor ve `steerAngle = (i < 2)`
    /// ile ilk iki tekeri yonlendiriyor. Uc tekerli bisiklette TUM
    /// guc on tekerdedir, direksiyon yalnizca ona uygulanir; iki arka
    /// teker sabit durur. Ayni sinifi zorlamak ya direksiyonu bozardi
    /// ya da motor torkunu 3'e bolmeydi. O yuzden fizik AYRI yazildi.
    ///
    /// YINEDE YENIDEN KULLANILDI:
    ///   - <see cref="VehicleDefinition"/> (ScriptableObject veri kaynagi)
    ///   - ArcadeVehicleController'in SAF STATIK matematigi
    ///     (ComputeSteerAngle / ComputeMotorTorque / ComputeBrakeTorque).
    ///     Bunlar durustur ve arac tipinden bagimsizdir; tork egilmesi
    ///     gibi zor fizik yeniden hesaplanmadi.
    ///   - <see cref="IVehicleController"/> sozlesmesi.
    ///   - WheelVisual ile teker gorsel senkronu.
    ///
    /// FIZIK: WheelCollider tabanli, sert gorsel ama stabil. Gercek
    /// bisiklet dinamiği (ağırlık transferi, direksiyon eğimi, kaldırma
    /// denge açısı) MVP icin modellenmedi: oyuncu aracin devrilmesini
    /// degil, gitmesini istiyor.
    ///
    /// PERFORMANS: `FixedUpdate` (fizik tick'i, render tick'i degil).
    /// `Update` YOK. Altyapida LINQ, boxing ve kare-basina allocation
    /// yoktur.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BicycleController : MonoBehaviour, IVehicleController
    {
        [Header("Tekerler (3)")]
        [Tooltip("0 = ON teker (direksiyon + motor). 1,2 = ARKA tekerler (sabit).")]
        [SerializeField] private WheelCollider frontWheel;
        [SerializeField] private WheelCollider rearLeft;
        [SerializeField] private WheelCollider rearRight;

        [Header("Girdi")]
        [SerializeField] private float throttleResponse = 9f;
        [SerializeField] private float brakeResponse = 14f;

        [Header("Kararlilik (bisiklet)")]
        [Tooltip("Yan devrilmeyi engelleyen kuvvet. Bisiklette dusuk tutulur: "
                 + "yuksek deger araci yapistirir, oyuncu ucramaz.")]
        [SerializeField] private float antiRollStrength = 6f;
        [SerializeField] private float uprightStrength = 3f;

        [Header("Fizik")]
        [SerializeField] private VehicleDefinition definition;
        [SerializeField] private Rigidbody body;

        private Camera _driverCamera;
        private float _throttleSmooth;
        private float _brakeSmooth;
        private float _steerSmooth;
        private Vector3 _spawnPoint;
        private Quaternion _spawnRotation;
        private bool _spawnCaptured;

        // ------------------------------------------------------------------
        // IVehicleController
        // ------------------------------------------------------------------

        public float SpeedKmh =>
            body == null ? 0f : body.linearVelocity.magnitude * 3.6f;

        public float ForwardSpeed =>
            body == null ? 0f : Vector3.Dot(body.linearVelocity, transform.forward);

        public bool ControlsLocked { get; set; } = true;

        public Transform VehicleTransform => transform;

        public VehicleDefinition Definition => definition;

        public void SetDriverCamera(Camera camera) => _driverCamera = camera;

        public void Respawn()
        {
            if (body == null) return;
            if (!_spawnCaptured) return;

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = _spawnPoint;
            body.rotation = _spawnRotation;
        }

        // ------------------------------------------------------------------
        // Editor kurulum kancası
        // ------------------------------------------------------------------

        /// <summary>
        /// Editor araci tarafindan doldurulur. Inspector'a elle yazmak
        /// gerekmesin diye ozel kanca.
        /// </summary>
        public void ConfigureForSetup(VehicleDefinition def, WheelCollider front,
                                      WheelCollider rearL, WheelCollider rearR)
        {
            definition = def;
            frontWheel = front;
            rearLeft = rearL;
            rearRight = rearR;
            ApplyDefinition();
        }

        // ------------------------------------------------------------------
        // Unity yasam dongusu
        // ------------------------------------------------------------------

        private void Awake()
        {
            if (body == null) body = GetComponent<Rigidbody>();
            if (body != null && !_spawnCaptured)
            {
                _spawnPoint = body.position;
                _spawnRotation = body.rotation;
                _spawnCaptured = true;
            }

            ApplyDefinition();
        }

        private void FixedUpdate()
        {
            if (body == null) return;

            if (ControlsLocked)
            {
                // Kilitliyken guc yok: arac yoldan gecse bile yavaslamaz,
                // oyuncu onu surmedi.
                ReadInput(0f, 0f, 0f, 0f);
            }
            else
            {
                ReadInput(Input.GetAxis("Vertical"),
                          Input.GetAxis("Horizontal"),
                          Input.GetKey(KeyCode.Space) ? 1f : 0f,
                          Input.GetAxis("Vertical") < -0.05f && _throttleSmooth > 0.1f ? 1f : 0f);
            }

            ApplyStability();
        }

        private void ReadInput(float vertical, float horizontal, float handbrake, float reverseBrake)
        {
            float dt = Time.fixedDeltaTime;

            // Girdi yumusatma: klavye 0/1 iken direk uygulanirsa bisiklet
            // "tik" gibi calisir. Uc saniyelik bir yumusatma his verir.
            _throttleSmooth = Mathf.MoveTowards(_throttleSmooth, vertical, throttleResponse * dt);
            _brakeSmooth = Mathf.MoveTowards(_brakeSmooth, handbrake, brakeResponse * dt);
            _steerSmooth = Mathf.MoveTowards(_steerSmooth, horizontal, 12f * dt);

            if (definition == null) return;

            float fwd = ForwardSpeed;
            float absSpeed = Mathf.Abs(fwd);

            // DIREKSIYON: yalnizca ON teker. Statik matematik
            // ArcadeVehicleController ile ayni (hiz arttikca acik azalir).
            float steer = ArcadeVehicleController.ComputeSteerAngle(
                _steerSmooth, absSpeed,
                definition.maxSteerAngle, definition.maxSpeed,
                definition.minSteerFactor);

            if (frontWheel != null) frontWheel.steerAngle = steer;

            // TORK: guc on tekerde. Ileri tork hiz limitine yakin
            // kademeli azalir (statik yardimci, titrama yapmaz).
            float motor = ArcadeVehicleController.ComputeMotorTorque(
                _throttleSmooth, fwd, definition.maxSpeed, definition.maxReverseSpeed,
                definition.engineForce);

            if (frontWheel != null) frontWheel.motorTorque = motor;

            // FREN: throttle yonunun karsi yonu fren. El freni
            // (Space) tum tekerlere tam guc.
            float brake = ArcadeVehicleController.ComputeBrakeTorque(
                -_throttleSmooth, fwd, definition.brakeForce, _brakeSmooth > 0.5f);

            // Geri gitmek: hizliyken S fren, durunca geri vitese gecer.
            if (reverseBrake > 0.5f && fwd > 0.5f) brake *= 2f;

            ApplyToWheel(frontWheel, brake);
            ApplyToWheel(rearLeft, brake);
            ApplyToWheel(rearRight, brake);
        }

        private static void ApplyToWheel(WheelCollider w, float brake)
        {
            if (w == null) return;
            w.brakeTorque = brake;
            if (w.motorTorque != 0f) w.motorTorque = 0f;
        }

        /// <summary>
        /// Bisiklet devrilirse oyun bitmez. Iki kuvvet birlikte calisir:
        ///
        ///  1) Yan devrilmeyi bastiran tork (anti-roll). Bisikletin dogal
        ///     denge sorunu 3 noktali aracla cozulmez; kucuk bir yana
        ///     kuvvet yeter.
        ///  2) Kendini dik tutma (upright). Tam dik cevirme degil -
        ///     egim kademeli duzeltilir, ani cevirme yapilirsa oyuncu
        ///     gorsel olarak sersemler.
        /// </summary>
        private void ApplyStability()
        {
            if (antiRollStrength <= 0f && uprightStrength <= 0f) return;

            Vector3 up = transform.up;
            Vector3 right = transform.right;

            // Yan egim: right'in yatay izdusumu ne kadar dikeyden sapti.
            float lean = Vector3.Dot(right, Vector3.up);

            // Tork yatay eksende: araci yana dogru dusurur.
            float antiRoll = -lean * antiRollStrength;
            body.AddTorque(transform.up * antiRoll, ForceMode.Acceleration);

            // Dikey duzeltme: pitch/roll hizini sifirla.
            Vector3 angVel = body.angularVelocity;
            float pitch = Vector3.Dot(angVel, transform.right);
            float roll = Vector3.Dot(angVel, transform.forward);

            body.AddTorque(-transform.right * pitch * uprightStrength -
                           transform.forward * roll * uprightStrength,
                           ForceMode.Acceleration);

            // Dikey hiz sifirla: ziplamayi onler (bisiklet ziplamaz).
            Vector3 vel = body.linearVelocity;
            if (Mathf.Abs(vel.y) > 0.01f)
                body.linearVelocity = new Vector3(vel.x, vel.y * 0.85f, vel.z);
        }

        /// <summary>
        /// VehicleDefinition degerlerini fizik govdeye yazar.
        /// Tasarim hem editor kurulumunda hem runtime'da cagrilir; ikinci
        /// cagri degerleri ayni yazar (idempotent).
        /// </summary>
        public void ApplyDefinition()
        {
            if (body == null || definition == null) return;

            body.mass = definition.mass;
            body.centerOfMass = new Vector3(0f, definition.centerOfMassHeight, 0f);
            body.drag = 0.2f;
            body.angularDrag = 4f;

            // Otomatik devrilme KAPALI: bisiklet kendi yonetimini
            // ApplyStability ile yapiyor, Unity'nin auto-upright'i
            // ikinci bir denge sistemi olurdu ve cakisir.
            body.angularVelocity = Vector3.zero;

            ApplySuspension(frontWheel);
            ApplySuspension(rearLeft);
            ApplySuspension(rearRight);
        }

        private void ApplySuspension(WheelCollider w)
        {
            if (w == null || definition == null) return;

            JointSpring spring = w.suspensionSpring;
            spring.spring = definition.suspensionSpring;
            spring.damper = definition.suspensionDamper;
            w.suspensionSpring = spring;

            w.suspensionDistance = definition.suspensionDistance;
            w.wheelDampingRate = 0.3f;

            // Bisikletin kucuk tekeri: asiri yatkinlik degil, sadece
            // tekerin dikey hareketi icin kucuk bir aci.
            w.forceAppPointDistance = 0f;
        }

        /// <summary>
        /// EditMode testleri icin: direksiyon ve gaz saf olarak hesaplanir.
        /// Sahne olmadan dogrulanabilir olsun diye ayri statik giris.
        /// </summary>
        public static float ComputeFrontSteer(float steerInput, float speed, VehicleDefinition def)
        {
            if (def == null) return 0f;
            return ArcadeVehicleController.ComputeSteerAngle(
                steerInput, speed, def.maxSteerAngle, def.maxSpeed, def.minSteerFactor);
        }
    }
}
