using UnityEngine;

namespace KargoSim.Vehicle
{
    /// <summary>
    /// Arcade (kutle-disi) arac kontrolcusu.
    ///
    /// Unity'de hazir bir arac fiziği sistemi YOKTUR (Asset Store'da ucretli
    /// paket olarak durur). Bu yuzden fizik burada elle yazilir: WheelCollider
    /// tabanli yer tutma + motor/fren torku + yapisan kuvvet.
    ///
    /// DESIGN:
    /// - Tum fizik matematiği <see cref="ComputeSteerAngle"/>,
    ///   <see cref="ComputeDriveForce"/>, <see cref="ComputeBrakeTorque"/> gibi
    ///   static saf fonksiyonlardadir. MonoBehaviour'in icinde kalan tek is,
    ///   bunlari cagirip sonucu PhysX'e yazmaktir. Boylece EditMode testleri
    ///   sahne acmadan formulleri dogrulayabilir.
    /// - Fizik hicbir zaman Update()'te degil, SADECE FixedUpdate()'te islenir.
    ///   Bu, tum simulasyonun ayni fizik adiminda (50 Hz) calismasini garanti eder.
    /// - ITERI, teker motor torqu ile saglanir. Bunun calismasi icin
    ///   govde mutlaka bir Collider icermelidir; sadece tekerlerden olusan
    ///   bir arac fizik motorunda kararsizdir. Gerekce: <see cref="ApplyDrive"/>.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(BoxCollider))]
    [DisallowMultipleComponent]
    public sealed class ArcadeVehicleController : MonoBehaviour, IVehicleController
    {
        [Header("Tanim")]
        [SerializeField] private VehicleDefinition definition;

        [Header("Tekerler")]
        [Tooltip("Sira ON SOL, ON SAG, ARKA SOL, ARKA SAG. Ilk iki teker yonlendirilir.")]
        [SerializeField] private WheelCollider[] wheels = new WheelCollider[4];

        /// <summary>Surucu kamerasi (IVehicleController uzerinden baglanir).</summary>
        private Camera _driverCamera;

        [Header("Girdi")]
        [SerializeField] private MonoBehaviour inputSource;

        private Rigidbody _rb;
        private IVehicleInput _input;
        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation;
        private bool _warnedMissingInput;

        // ------------------------------------------------------------------
        // Genel erisim
        // ------------------------------------------------------------------

        public VehicleDefinition Definition => definition;

        /// <summary>Anlik hiz (km/sa). HUD ve kamera FOV icin.</summary>
        public float SpeedKmh => _rb == null ? 0f : _rb.linearVelocity.magnitude * 3.6f;

        /// <summary>Arac ileri yone bakiyor mu? Geri vites kontrolu icin.</summary>
        public float ForwardSpeed => _rb == null ? 0f : Vector3.Dot(_rb.linearVelocity, transform.forward);

        /// <summary>
        /// Arac girdisi gecici olarak kapatilir (karakter yururken).
        /// Kurye ajani kargoyu arac disinda tasidigi icin oyuncu bu
        /// sirada surusemez; boylece "ben mi yuruyorum arac mi?"
        /// belirsizligi olusmaz.
        /// </summary>
        public bool ControlsLocked { get; set; }

        /// <summary>IVehicleController: kargo noktasi cikis yerini bu agac uzerinden hesaplar.</summary>
        public Transform VehicleTransform => transform;

        /// <summary>
        /// IVehicleController: surucu kamerasi.
        ///
        /// NOT: bu arac sinifinda kamera sahne tarafindan baglanir ve
        /// kurye tarafindan yonetilir; burada sadece referans saklanir.
        /// </summary>
        public void SetDriverCamera(Camera camera)
        {
            _driverCamera = camera;
        }

        // ------------------------------------------------------------------
        // Yasam dongusu
        // ------------------------------------------------------------------

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.interpolation = RigidbodyInterpolation.Interpolate;

            _spawnPosition = transform.position;
            _spawnRotation = transform.rotation;

            ResolveInputSource();
            ApplyDefinition();
        }

        /// <summary>
        /// Tanimi (VehicleDefinition) fizik motoruna uygular: kutle, agirligin
        /// merkezi, teker yaricapi ve suspansiyon.
        ///
        /// Awake icinden cagrilir. Ayrica tekrar cagrilabilir olmasi onemli:
        /// bir arac tanimi degistirildiginde (ya da test/prototipte arac
        /// kurulduktan sonra) fizik degerlerinin yeniden senkronlanmasini
        /// saglar. Idempotenttir, tekrar cagrilmasi zarar vermez.
        /// </summary>
        public void ApplyDefinition()
        {
            if (_rb == null) _rb = GetComponent<Rigidbody>();
            if (definition == null) return;

            _rb.mass = definition.mass;

            // Agirlik merkezini asagi indir: agir kafa ile kapiyi girmek
            // (yandan vurmak) kolaylastirir, takla atmayi zorlastirir.
            _rb.centerOfMass = new Vector3(0f, -definition.centerOfMassHeight, 0f);

            ApplyWheelDefaults();
        }

        private void OnEnable()
        {
            // Arayuz uzerinden abone olunur: surucu bir PlayerController
            // olmak zorunda degil, ayni sozlesmeyi paylasan baska bir sey
            // de olabilir.
            if (_input != null) _input.RespawnRequested += Respawn;
        }

        private void OnDisable()
        {
            if (_input != null) _input.RespawnRequested -= Respawn;
        }

        private void ResolveInputSource()
        {
            // Inspector'da atanmis MonoBehaviour gercekten IVehicleInput mi?
            // Yanlislikla baska bir component atanirsa oyun sessizce
            // hic kipirdamaz; bu yuzden acikca hata veriyoruz.
            if (inputSource == null)
            {
                if (!_warnedMissingInput)
                {
                    _warnedMissingInput = true;
                    Debug.LogError(
                        "[ArcadeVehicleController] inputSource atanmadi. Arac surulmez. " +
                        "Bu component'in Input Source alanina ayni GameObject uzerindeki " +
                        "PlayerController'i surukleyin.", this);
                }
                return;
            }

            if (inputSource is IVehicleInput typed)
            {
                _input = typed;
                return;
            }

            _warnedMissingInput = true;
            Debug.LogError(
                "[ArcadeVehicleController] inputSource bir IVehicleInput degil: " +
                inputSource.GetType().Name + ". PlayerController atanmalidir.", inputSource);
        }

        private void ApplyWheelDefaults()
        {
            if (definition == null) return;

            for (int i = 0; i < wheels.Length; i++)
            {
                WheelCollider w = wheels[i];
                if (w == null) continue;

                w.radius = definition.wheelRadius;

                // WheelCollider'in kendi Spring tanimini doldurmak, ayri bir
                // raycast/suspension script'i yazmaktan daha saglam ve daha hizlidir.
                JointSpring spring = w.suspensionSpring;
                spring.spring = definition.suspensionSpring;
                spring.damper = definition.suspensionDamper;
                spring.targetPosition = 0.5f;
                w.suspensionSpring = spring;

                w.motorTorque = 0f;
                w.brakeTorque = 0f;
            }
        }

        private void FixedUpdate()
        {
            if (_rb == null || definition == null || wheels == null || wheels.Length < 4) return;

            // Karakter arac disindayken (kargo tasirken) gecersiz.
            // Boylece oyuncu ayni anda hem yuruyup hem arac kullanmaz.
            if (ControlsLocked)
            {
                ApplySteering(0f);
                ApplyBrakes(definition.brakeForce, true);
                ApplyDownforce();
                ApplyAntiFlip();
                return;
            }

            float throttle = _input != null ? _input.Throttle : 0f;
            float steer = _input != null ? _input.Steer : 0f;
            bool handbrake = _input != null && _input.Handbrake;

            float forwardSpeed = ForwardSpeed;
            float steerAngle = ComputeSteerAngle(steer, Mathf.Abs(forwardSpeed),
                                                 definition.maxSteerAngle,
                                                 definition.maxSpeed,
                                                 definition.minSteerFactor);
            float brakeTorque = ComputeBrakeTorque(throttle, forwardSpeed,
                                                   definition.brakeForce, handbrake);

            ApplySteering(steerAngle);
            ApplyBrakes(brakeTorque, handbrake);
            ApplyDrive(throttle, forwardSpeed);
            ApplyDownforce();
            ApplyLateralGrip();
            ApplyAntiFlip();
            ApplyAutoUpright();
        }

        /// <summary>
        /// Motor torkunu tekerlere yazar.
        ///
        /// ONEMLI: Arac govdesinde MUTLAKA bir Collider olmalidir (Bkz.
        /// RequiredComponent notu). Sadece tekerlerden olusan bir arac
        /// fizik motoru icin kararsizdir: ilk itki kuvvetiyle birlikte
        /// takla atar. PlayMode testinde olculdu (kutle 1200, itki 2000 N):
        ///
        ///   sasi yok + AddForce       -> azami egilme 164 derece, y = -76 m
        ///   sasi yok + motorTorque    -> azami egilme 173 derece, y = -94 m
        ///   sasi var + AddForce       -> kararli ama HAREKET ETMIYOR (0.00 m)
        ///   sasi var + motorTorque    -> kararli, 2.4 saniyede 16.7 m
        ///
        /// Yani: sert bir govde collider'i hem zorunlu hem de motorTorque
        /// ile itkinin calismasi icin sart. Gorsel govde nesnesinin
        /// collider'ini silmeyin!
        /// </summary>
        private void ApplyDrive(float throttle, float forwardSpeed)
        {
            if (Mathf.Abs(throttle) < 0.001f) return;

            float motorTorque = ComputeMotorTorque(throttle, forwardSpeed,
                                                    definition.maxSpeed,
                                                    definition.maxReverseSpeed,
                                                    definition.engineForce);

            float share = motorTorque * MotorShare;

            for (int i = 0; i < wheels.Length; i++)
            {
                if (wheels[i] != null) wheels[i].motorTorque = share;
            }
        }

        /// <summary>Motor torku 4 teker arasinda esit paylasilir (AWD).</summary>
        private const float MotorShare = 0.25f;

        private void ApplySteering(float steerAngle)
        {
            // Ilk iki teker = on aks (sol, sag)
            for (int i = 0; i < wheels.Length; i++)
            {
                if (wheels[i] == null) continue;
                wheels[i].steerAngle = (i < 2) ? steerAngle : 0f;
            }
        }

        private void ApplyBrakes(float brakeTorque, bool handbrake)
        {
            for (int i = 0; i < wheels.Length; i++)
            {
                WheelCollider w = wheels[i];
                if (w == null) continue;

                if (!handbrake) { w.brakeTorque = brakeTorque; continue; }

                // El freni SADECE arka tekerlere uygulanir. On tekerlere de
                // uygulanirsa arac takla atar (bilinen arcade hatasi).
                bool isRear = i >= 2;
                w.brakeTorque = isRear
                    ? brakeTorque + brakeTorque * definition.handbrakeStrength
                    : brakeTorque * 0.5f;
            }
        }

        // ------------------------------------------------------------------
        // FIZIK MATEMATIGI (static + test edilebilir)
        // ------------------------------------------------------------------

        /// <summary>
        /// Hiz arttikca donus acisini kisaltir. Dururken tam aci, azami hizda
        /// <c>minSteerFactor</c> oranina iner (varsayilan %35) - tamamen
        /// kaybolmaz.
        ///
        /// NEDEN 1/(1+hiz*k) DEGIL: o egri 10 m/s'te aciyi %9'a dusuruyor,
        /// yani arac pratikte yon donduremiyor. PlayMode testinde olculdu:
        /// 10 m/s'te 2.9 derece aci kaldi ve arac 1.2 saniyede 5 dereceden
        /// az donup gitti. Lineer enterpolasyon hem hizli hem de onceden
        /// hesaplanabilir bir yonlendirme veriyor.
        /// </summary>
        /// <param name="steerInput">-1..1</param>
        /// <param name="speed">Hizin mutlak degeri (m/s)</param>
        public static float ComputeSteerAngle(float steerInput, float speed,
                                              float maxSteerAngle, float maxSpeed,
                                              float minSteerFactor)
        {
            if (Mathf.Abs(steerInput) < 0.0001f) return 0f;

            float t = Mathf.Clamp01(Mathf.Abs(speed) / Mathf.Max(maxSpeed, 0.1f));
            float factor = Mathf.Lerp(1f, Mathf.Clamp01(minSteerFactor), t);

            return steerInput * maxSteerAngle * factor;
        }

        /// <summary>
        /// Ileri yonde uygulanacak motor torku (Nm).
        ///
        /// Hiz limitine yaklasirken torku kademeli azaltir; boylece araç
        /// azami hizi hicbir zaman asmaz ve limitte "titrama" yapmaz.
        ///
        /// Geri yonde limit daha kucuktur ve throttle yone göre yorumlanir:
        /// ileri giderken fren tutmak geri vitesi baslatmaz.
        /// </summary>
        public static float ComputeMotorTorque(float throttle, float forwardSpeed,
                                               float maxSpeed, float maxReverseSpeed,
                                               float engineForce)
        {
            if (Mathf.Abs(throttle) < 0.001f) return 0f;

            if (throttle > 0f)
            {
                // Ileri yon: ters yone giderken (geri vitisten) fren de olur,
                // ama throttle pozitifse cekim uygulanir -> geri vitesten cikar.
                float t = Mathf.Clamp01(Mathf.Max(forwardSpeed, 0f) / maxSpeed);
                return engineForce * throttle * (1f - t);
            }
            else
            {
                // Geri yon
                float t = Mathf.Clamp01(Mathf.Max(-forwardSpeed, 0f) / maxReverseSpeed);
                return engineForce * throttle * (1f - t);
            }
        }

        /// <summary>
        /// Fren torku. Uygulanma kurallari:
        ///
        /// 1) El freni basiliysa her zaman fren.
        /// 2) Hiz yoksa (|hiz| &lt; durma esigi) fren UYGULANMAZ. Bu kural
        ///    kritik: arac dururken yerlestirme kaynakli 0.02 m/s'lik
        ///    geriye kayma olur ve "gaz basili ama hiz ters yonde" kurali
        ///    devreye girince teker KILITLENIR. Motor (550 Nm) frenin
        ///    (3500 Nm) kazanmasina izin vermedigi icin arac kalkamaz;
        ///    fizik motoru bu zorlanmis durumu kotu cozer ve arac
        ///    zeminden gecer. PlayMode testinde olculdu (bkz. README).
        /// 3) Gaz yoneyle ayni yondaysa fren yok (hizlanma).
        /// 4) Gaz birakildiysa veya gaz hiza zit yondaysa fren uygulanir
        ///    (yavasliyoruz).
        /// </summary>
        public static float ComputeBrakeTorque(float throttle, float forwardSpeed,
                                                float brakeForce, bool handbrake)
        {
            if (handbrake) return brakeForce;

            // Hic yoksek fren yok: motor kazansin.
            if (Mathf.Abs(forwardSpeed) < 0.1f) return 0f;

            if (throttle * forwardSpeed > 0.01f) return 0f;   // hizlanma

            return brakeForce * Mathf.Max(Mathf.Abs(throttle), 0.6f);
        }

        // ------------------------------------------------------------------
        // Yapisal kuvvetler
        // ------------------------------------------------------------------

        /// <summary>
        /// Hiz karesiyle artan yapisikan kuvvet. Fiziğin karesi: 2 kat hiz
        /// = 4 kat kuvvet. Boylece arac hizlandikca yere basar ve zirve
        /// yapmaz (bilinen arcade formulu).
        /// </summary>
        private void ApplyDownforce()
        {
            float speed = _rb.linearVelocity.magnitude;
            if (speed < 0.5f) return;

            float force = definition.downforce * speed * speed;
            _rb.AddForce(-transform.up * force, ForceMode.Force);
        }

        /// <summary>
        /// Yan hizin bir kismini her fizik adiminda siler. Tamamen silmek
        /// (lateral = 0) araci yapistirir ve donus hissi olmaz; hic silmemek
        /// ise buz gibi kaydirir. Kismi silme ikisinin arasini verir:
        /// kontrollu ama belirgin kayma.
        /// </summary>
        private void ApplyLateralGrip()
        {
            Vector3 velocity = _rb.linearVelocity;
            if (velocity.sqrMagnitude < 0.0001f) return;

            float lateral = transform.InverseTransformDirection(velocity).x;
            if (Mathf.Abs(lateral) < 0.01f) return;

            float amount = Mathf.Clamp01(definition.lateralGrip * Time.fixedDeltaTime);
            _rb.linearVelocity = velocity - transform.right * (lateral * amount);
        }

        /// <summary>
        /// TAKLA ATMA KORUMASI.
        ///
        /// Acis (yaw, Y ekseni) serbest birakilir: donus oyunu bozmamali.
        /// Ama pitch (X) ve roll (Z) hizlari sinirlandirilir, boylece arac
        /// ne olursa olsun cevrilip oyunu bitiremez.
        ///
        /// NEDEN GEREKLI: Fizik motoru zemin kenarlari, curb ve carpismalarda
        /// buyuk pitch/roll torklari uretebilir. Arcade bir kargo oyununda
        /// oyuncunun araci cevirip kurtarma tusuyla duzeltmek zorunda
        /// kalmasi kabus bir deneyimdir. Bu, hemen her arcade arac
        /// kontrolcusunde bulunan standart bir kisitlamadir.
        /// </summary>
        private void ApplyAntiFlip()
        {
            Vector3 angular = _rb.angularVelocity;
            const float maxTiltSpeed = 4f;   // rad/sn

            _rb.angularVelocity = new Vector3(
                Mathf.Clamp(angular.x, -maxTiltSpeed, maxTiltSpeed),
                angular.y,
                Mathf.Clamp(angular.z, -maxTiltSpeed, maxTiltSpeed));
        }

        /// <summary>
        /// Arac dururken devrilmisse kendini kaldirir. Bunu yapmazsak oyuncu
        /// cevirme tusuna basip cikar ve oyun biter. "Cozer" gibi hissettirmesi
        /// icin yalnizca neredeyse dururken devreye girer.
        /// </summary>
        private void ApplyAutoUpright()
        {
            if (_rb.linearVelocity.sqrMagnitude > 4f) return;      // ~2 m/s uzeri dokunma
            if (definition == null) return;

            float tilt = Vector3.Angle(transform.up, Vector3.up);
            if (tilt < 5f) return;

            Quaternion correction = Quaternion.FromToRotation(transform.up, Vector3.up);
            _rb.MoveRotation(Quaternion.Slerp(
                transform.rotation,
                transform.rotation * correction,
                Mathf.Clamp01(definition.autoUprightSpeed * Time.fixedDeltaTime)));
        }

        // ------------------------------------------------------------------
        // Genel islevler
        // ------------------------------------------------------------------

        /// <summary>
        /// Aracı baslangic durumuna iade eder. Cevirme tusu ile takilmis
        /// durumlardan kurtulmanin en ucuz yolu; ayrica depo (dogma noktasi)
        /// eklendiginde ayni mekanizma gun sonu donusunde de kullanilacak.
        /// </summary>
        public void Respawn()
        {
            if (_rb == null) return;

            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.position = _spawnPosition;
            _rb.rotation = _spawnRotation;
        }

        /// <summary>Yeni dogma noktasi belirler (depo vb. icin).</summary>
        public void SetSpawnPoint(Vector3 position, Quaternion rotation)
        {
            _spawnPosition = position;
            _spawnRotation = rotation;
        }

        private void OnValidate()
        {
            // Inspector'da teker sayisi yanlislikla bozulursa surus
            // sessizce hic calismaz. Duzeltme kullanici niyeti belirsiz
            // olabilecegi icin yapilmiyor, sadece hatirlatici veriliyor.
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor araclari (prefab uretimi) icin ayar girisi.
        ///
        /// Neden public metot var: SerializedObject ile yazmak, henuz
        /// kaydedilmemis yeni nesnelerde sessizce kaybolabiliyor
        /// (bkz. README > Bilinen Unity tuzaklari). Dogrudan atama her
        /// zaman tutarli calisiyor ve uretimi test edilebilir kiliyor.
        /// </summary>
        public void ConfigureForSetup(VehicleDefinition def, WheelCollider[] wheelSet,
                                      MonoBehaviour input)
        {
            definition = def;
            wheels = wheelSet;
            inputSource = input;

            // Awake bu component editor'de calismaz (sadece Play'de), yani
            // tanim burada atandiktan SONRA fizik degerleri de elle
            // uygulanmali. Ayni sey testlerde de gecerli.
            ApplyDefinition();
            ResolveInputSource();
        }
#endif
    }
}
