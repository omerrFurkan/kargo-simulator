using UnityEngine;

namespace KargoSim.Player
{
    /// <summary>
    /// Araci yandan ve arkadan takip eden kamera.
    ///
    /// Duz transform takibi (Lerp) yapmak yerine <see cref="SmoothDamp"/>
    /// kullanilir: kritik sonlanma (velocity = 0) olmadan yumuşak ve dogru
    /// sekilde yaklasir. Kamera, aracin ROTASYONUNU yavasca takip eder -
    /// aninda takip ederse donuslerde midide bulanti yaratir.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraFollow : MonoBehaviour
    {
        [Header("Hedef")]
        [SerializeField] private Transform target;

        [Header("Konum")]
        [Tooltip("Aracin yerel koordinatlarindaki kamera konumu. Varsayilan: arkada, yukari, hafif yanda.")]
        [SerializeField] private Vector3 localOffset = new Vector3(0f, 6.5f, -9.5f);

        [Tooltip("Kameranin baktigi nokta aracin local koordinatinda. " +
                 "Aracin onune bakmasi: kameranin arabanin onundeki yolu gormesini saglar.")]
        [SerializeField] private Vector3 lookAtLocalOffset = new Vector3(0f, 1.2f, 4f);

        [Header("Yumusatma")]
        [Tooltip("Kameranin konuma yonelme suresi. Kucuk = daha sert, buyuk = daha yavas.")]
        [Range(0.05f, 2f)] public float positionSmoothTime = 0.25f;

        [Tooltip("Kameranin yonelme suresi. Konumdan DAHA YAVAS olmali " +
                 "(0.35-0.5) yoksa donuslerde kamera aracin onunden gecer.")]
        [Range(0.05f, 2f)] public float rotationSmoothTime = 0.45f;

        [Header("Hiza Gore FOV")]
        [Tooltip("Hiz arttikca kameranin gorus acisi acilsin: hiz hissi belirginlesir.")]
        [SerializeField] private bool useSpeedFov = true;
        [SerializeField] private float baseFov = 60f;
        [Tooltip("Azami hizda kameranin acilacagi FOV degeri.")]
        [SerializeField] private float maxSpeedFov = 78f;
        [Tooltip("Azami hiz referansi (km/sa). VehicleDefinition ile eslesmeli.")]
        [SerializeField] private float speedForMaxFovKmh = 110f;

        [Header("Surucu modu (Asama 14)")]
        [Tooltip("Kapali: arac arkasindan takip eden kamera (van). "
                 + "Acik: gidon uzerinden surucu bakis acisi (bisiklet).")]
        [SerializeField] private bool driverMode = false;

        [Tooltip("Surucu modunda kameranin aracin yerel konumu. "
                 + "Gidonun hemen arkasinda, kafanin hizinda.")]
        [SerializeField] private Vector3 driverLocalOffset = new Vector3(0f, 1.25f, 0.35f);

        private Camera _camera;
        private Vector3 _positionVelocity;
        private float _initialLocalHeight;
        private Vehicle.IVehicleController _vehicle;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            if (_camera == null)
            {
                Debug.LogError("[CameraFollow] Bu GameObject uzerinde Camera yok. " +
                               "Kamerayi bu component'i olan nesneye tasiyin.", this);
                enabled = false;
            }
        }

        private void Start()
        {
            if (target == null) return;

            // Kamerayi baslangicta arac konumuna ANINDA oturt: yavas takiple
            // sahne acilirken kamera yerinden kayarak gelmesin.
            transform.position = target.TransformPoint(localOffset);
            transform.rotation = target.rotation;
            _initialLocalHeight = localOffset.y;
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            _vehicle = null;
        }

        /// <summary>
        /// Takip edilen nesne (arac). CompositionRoot kameranin hangi
        /// araca ait oldugunu bu ile ayirt eder.
        ///
        /// ASAMA 16: vanin kamerasi aracin COCUGU degil, sahne nesnesi
        /// ("Main Camera"). Onu arac altinda aramak bulamiyordu ve
        /// kodsayilan cozum Camera.main'e dusuyordu.
        /// </summary>
        public Transform Target => target;

        /// <summary>Editor kurulum kancası: surucu modunu ayarlar.</summary>
        public void ConfigureForSetup(bool isDriverMode, Vector3 driverOffset, Transform followTarget)
        {
            driverMode = isDriverMode;
            driverLocalOffset = driverOffset;
            if (followTarget != null) target = followTarget;
        }

        private void LateUpdate()
        {
            // LateUpdate: kamera, aracin bu karedeki konumunu GORDUKTEN sonra
            // guncellenmeli. Update'de calissaydi bir kare geride kalirdi.
            if (target == null) return;

            // SURUCU MODU: kamera aracin USTUNDE sabit durur, yalnizca
            // aracin rotasyonunu takip eder. Kureyle yoldan gider gibi
            // hissettirir; bisiklet icin uzak takip kamerasi oyuncuyu
            // kask gorunumu yapardı.
            if (driverMode)
            {
                // Konum: aracla birlikte tasinir, yumusatma YOK (gecikme
                // surerken gider, kask hissi verir).
                transform.position = target.TransformPoint(driverLocalOffset);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, target.rotation,
                    1f - Mathf.Exp(-rotationSmoothTime * 10f * Time.deltaTime));
                UpdateFov();
                return;
            }

            Vector3 desiredPosition = target.TransformPoint(localOffset);
            transform.position = Vector3.SmoothDamp(
                transform.position, desiredPosition, ref _positionVelocity, positionSmoothTime);

            Vector3 lookPoint = target.TransformPoint(lookAtLocalOffset);
            Quaternion desiredRotation = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);

            transform.rotation = Quaternion.Slerp(
                transform.rotation, desiredRotation, 1f - Mathf.Exp(-rotationSmoothTime * 10f * Time.deltaTime));

            UpdateFov();
        }

        private void UpdateFov()
        {
            if (_camera == null || !useSpeedFov) return;

            // Asama 14: arac tipi degisti, sabit sinif degil SOZLESME.
            // (Once GetComponentInParent<ArcadeVehicleController>() idi ve
            // bisiklete baglaninca hiz FOV'u sessizce kapanirdi.)
            if (_vehicle == null)
            {
                _vehicle = target.GetComponentInParent<Vehicle.IVehicleController>();
                if (_vehicle == null) return;
            }

            float t = Mathf.Clamp01(_vehicle.SpeedKmh / Mathf.Max(speedForMaxFovKmh, 1f));
            float targetFov = Mathf.Lerp(baseFov, maxSpeedFov, t);

            // FOV'yu dogrudan atamak yerine yumusat: ani degisim sersemletir.
            _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, targetFov, Time.deltaTime * 4f);
        }
    }
}
