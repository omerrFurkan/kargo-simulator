using System.Collections.Generic;
using KargoSim.Cargo;
using KargoSim.Delivery;
using KargoSim.Vehicle;
using UnityEngine;

namespace KargoSim.Player
{
    /// <summary>
    /// Oyunun kontrolundeki kurye karakteri: FPS (birinci sahis).
    ///
    /// SORUMLULUKLAR
    ///  - Yurume (W/A/S/D, bakis yonune gore)
    ///  - Fare ile bakis (imlec oyuna kilitli, Esc ile serbest)
    ///  - Kargo tasima (gorsel yigin elde)
    ///  - Yakindaki etkilesim noktasini bulma ve ipucunu bildirme
    ///
    /// Update() YOK: CompositionRoot her karede <see cref="Tick"/> cagirir
    /// (oyunda tek Update kurali). Fare girdisi GetAxis ile okunur;
    /// dondurulmus bir kamera acisini biriktirmek gerekmez.
    ///
    /// Araca binince karakter gizlenir, <see cref="ControlsLocked"/>
    /// ile arac surusu devralir ve kamera arac kamerasina doner.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CourierController : MonoBehaviour
    {
        [Header("Referanslar")]
        [SerializeField] private CharacterController controller;
        [Tooltip("Karakterin FPS kamerasi (kafa yuksekliginde, cocuk).")]
        [SerializeField] private Camera fpsCamera;
        [Tooltip("Arac surulurken kullanilan takip kamerasi (Main Camera).")]
        [SerializeField] private Camera vehicleCamera;
        [SerializeField] private Transform carryAnchor;
        [SerializeField] private Animator animator;
        [SerializeField] private ArcadeVehicleController vehicleController;

        [Header("Hareket")]
        [SerializeField] private float walkSpeed = 3.4f;
        [SerializeField] private float runSpeed = 5.6f;
        [SerializeField] private float jumpHeight = 1.1f;
        [SerializeField] private float mouseSensitivity = 2.4f;
        [Tooltip("Bakis dikeyde bu acidan fazla yukselmez (derece).")]
        [SerializeField] private float maxPitch = 80f;
        [SerializeField] private float gravity = -20f;

        [Header("Tasma")]
        [Tooltip("Ayni anda tasinabilecek kutu adedi.")]
        [SerializeField] private int maxCarry = 3;

        [Header("Etkilesim")]
        [Tooltip("Bu yaricapta etkilesim noktasi aranir (metre).")]
        [SerializeField] private float interactRadius = 2.6f;
        [SerializeField] private LayerMask interactMask = ~0;

        // --- Animasyon klipleri (Kenney Mini Characters) ---
        private const string ClipIdle = "idle";
        private const string ClipWalk = "walk";
        private const string ClipSprint = "sprint";
        private const string ClipPickUp = "pick-up";
        private const string ClipCarryIdle = "holding-both";
        private const string ClipDrop = "interact-right";

        private readonly List<GameObject> _carried = new List<GameObject>();

        /// <summary>
        /// Elde tutulan KARGOLARIN KENDISI (gorsel degil).
        ///
        /// NAYI AYRI TUTUYORUZ: Once sadece kutu gorseli tasiniyordu,
        /// boylece araca konulan kargo oyuncunun elindekinden
        /// FARKLI cikardi (elde #7,#8,#9 gorunurken #1,#2,#3
        /// yukleniyordu). Iki liste da ayni sirayi paylasir.
        /// </summary>
        private readonly List<Parcel> _carriedParcels = new List<Parcel>();
        private readonly Collider[] _hitBuffer = new Collider[12];

        private float _pitch;
        private float _verticalVelocity;
        private bool _inVehicle = true;
        private bool _cursorLocked;

        private IInteractable _focus;
        private Transform _focusTransform;

        // ------------------------------------------------------------------
        // Genel API
        // ------------------------------------------------------------------

        /// <summary>Karakter arac icinde mi (yoksa ayakta mi)?</summary>
        public bool IsInVehicle => _inVehicle;

        /// <summary>Ayaktayken ve bir sey tasirken.</summary>
        public bool IsCarrying => _carriedParcels.Count > 0;

        public int CarriedCount => _carriedParcels.Count;

        /// <summary>
        /// Karakter yuruyor mu? Ses yoneticisi adim sesi icin bunu
        /// okur. Girdi yonu (duzune donuk mu) DEGIL, sadece tus basili
        /// mi diye bakilir; yoksa duvara dayanip zorlanan kurye
        /// kosar gibi ses cikarirdi.
        /// </summary>
        public bool IsMoving => _isMoving;

        /// <summary>Odakta olan etkilesim noktasi (yoksa null).</summary>
        public IInteractable Focus => _focus;

        public bool Configure(Transform carryAnchorTransform, Camera fpsCam, Camera vehicleCam,
                              Animator anim, ArcadeVehicleController vehicle)
        {
            if (carryAnchorTransform != null) carryAnchor = carryAnchorTransform;
            if (fpsCam != null) fpsCamera = fpsCam;
            if (vehicleCam != null) vehicleCamera = vehicleCam;
            if (anim != null) animator = anim;
            if (vehicleController == null) vehicleController = vehicle;
            return true;
        }

        // ------------------------------------------------------------------
        // Her kare (CompositionRoot cagirir)
        // ------------------------------------------------------------------

        public void Tick(float deltaTime)
        {
            if (fpsCamera == null) return;

            if (_inVehicle)
            {
                // Araçta: kameraya bakmayı bırak, girdi araca gitsin.
                ScanForFocus();
                return;
            }

            Look();
            HandleClickToRecapture();
            Move(deltaTime);
            ScanForFocus();
            UpdateLocomotionAnimation();
        }

        // ------------------------------------------------------------------
        // Bakis
        // ------------------------------------------------------------------

        private void Look()
        {
            // Fare girdisi yalnizca imlec kilitliyken okunur; kilitli
            // degilse fareyi kaybetmemek icin harici kontrol gerekir
            // (oyun ici imlec gosterimi). Editor'de fare Editor'a gider.
            if (!_cursorLocked) return;

            float mx = Input.GetAxis("Mouse X") * mouseSensitivity;
            float my = Input.GetAxis("Mouse Y") * mouseSensitivity;

            transform.Rotate(Vector3.up, mx, Space.World);
            _pitch = Mathf.Clamp(_pitch - my, -maxPitch, maxPitch);
            fpsCamera.transform.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        public void SetCursorLocked(bool locked)
        {
            _cursorLocked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        // ------------------------------------------------------------------
        // Hareket
        // ------------------------------------------------------------------

        private void Move(float dt)
        {
            if (controller == null || !controller.enabled) return;

            float x = Input.GetAxisRaw("Horizontal");
            float z = Input.GetAxisRaw("Vertical");

            // Bakis yonune gore: ileri baktigin yone W ile gidersin.
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
            Vector3 wish = (forward * z + right * x).normalized;

            bool running = Input.GetKey(KeyCode.LeftShift) && !IsCarrying;
            float speed = running ? runSpeed : walkSpeed;
            if (IsCarrying) speed *= 0.75f;

            if (controller.isGrounded)
            {
                if (_verticalVelocity < 0f) _verticalVelocity = -2f;
                if (Input.GetKeyDown(KeyCode.Space)) _verticalVelocity = jumpHeight;
            }
            _verticalVelocity += gravity * dt;

            Vector3 motion = wish * speed;
            motion.y = _verticalVelocity;
            controller.Move(motion * dt);

            _isRunning = running && wish.sqrMagnitude > 0.01f;
            _isMoving = wish.sqrMagnitude > 0.01f;
        }

        private bool _isMoving;
        private bool _isRunning;

        private void UpdateLocomotionAnimation()
        {
            if (animator == null) return;
            if (animator.gameObject.activeInHierarchy == false) return;

            if (!string.IsNullOrEmpty(_actionClip) && _actionTimer > 0f)
            {
                _actionTimer -= Time.deltaTime;
                return;
            }

            string clip;
            if (IsCarrying) clip = _isMoving ? ClipWalk : ClipCarryIdle;
            else if (!_isMoving) clip = ClipIdle;
            else clip = _isRunning ? ClipSprint : ClipWalk;

            if (clip != _lastClip)
            {
                _lastClip = clip;
                animator.CrossFadeInFixedTime(clip, 0.15f);
            }
        }

        private string _lastClip = "";
        private string _actionClip = "";
        private float _actionTimer;

        /// <summary>Tek seferlik bir animasyon oynatir (alma / birakma).</summary>
        public void PlayAction(string clip, float seconds)
        {
            if (animator == null) return;
            if (animator.gameObject.activeInHierarchy == false) return;

            _actionClip = clip;
            _actionTimer = seconds;
            _lastClip = clip;
            animator.CrossFadeInFixedTime(clip, 0.1f);
        }

        // ------------------------------------------------------------------
        // Etkilesim
        // ------------------------------------------------------------------

        private void ScanForFocus()
        {
            _focus = null;
            _focusTransform = null;
            if (controller == null) return;

            Vector3 origin = controller.transform.position + Vector3.up;
            int count = Physics.OverlapSphereNonAlloc(origin, interactRadius, _hitBuffer, interactMask, QueryTriggerInteraction.Collide);

            IInteractable best = null;
            Transform bestTransform = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider c = _hitBuffer[i];
                if (c == null) continue;

                // Çarpışan nesnenin üstündeki (veya kendisindeki) etkilesim
                // bileşenini ara: kutu yığını gibi görsel katmanlar
                // tetikleyicinin farklı bir nesnede olabilir.
                var interactable = c.GetComponentInParent<IInteractable>();
                if (interactable == null) continue;
                if (!interactable.CanInteract(this)) continue;

                float d = (c.transform.position - origin).sqrMagnitude;
                if (d >= bestDistance) continue;

                bestDistance = d;
                best = interactable;
                bestTransform = c.transform;
            }

            _focus = best;
            _focusTransform = bestTransform;
        }

        /// <summary>
        /// Odakta bir etkilesim noktasi varsa E ile tetikler.
        /// UIManager bunu cagirir; boylece tus yonetimi tek yerde kalir.
        /// </summary>
        public bool TryInteract()
        {
            if (_focus == null) return false;
            _focus.Interact(this);
            return true;
        }

        // ------------------------------------------------------------------
        // Kargo tasima
        // ------------------------------------------------------------------

        public bool CanCarryMore => _carriedParcels.Count < maxCarry;

        /// <summary>Elde tasinan kargolar (systeme yazilacak GERCEK kargo).</summary>
        public IReadOnlyList<Parcel> CarriedParcels => _carriedParcels;

        /// <summary>Depo/dükkân rafından bir kutu alır.</summary>
        public bool PickUp(Parcel parcel)
        {
            if (!CanCarryMore) return false;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Parcel_" + (parcel != null ? parcel.Id : 0);
            Destroy(go.GetComponent<Collider>());

            Transform parent = carryAnchor != null ? carryAnchor : transform;
            go.transform.SetParent(parent, false);

            // Yığın: biraz yukarı ve öne doğru açılı dursun, tek
            // kutudan çok daha okunur olsun.
            int index = _carriedParcels.Count;
            go.transform.localPosition = new Vector3(0f, 0.1f + index * 0.44f, 0.42f);
            go.transform.localRotation = Quaternion.Euler(0f, (index % 2 == 0 ? 6f : -6f), 0f);

            go.AddComponent<ParcelVisual>().Apply(parcel != null ? parcel.Type : null);

            _carried.Add(go);
            _carriedParcels.Add(parcel);
            return true;
        }

        /// <summary>
        /// Elden TEK kargo birakir (gorseli de silinir).
        /// Araca koyarken ve kapida birakirken kullanilir; kalanlar
        /// elde kalir.
        /// </summary>
        public bool DropOne(Parcel parcel)
        {
            int index = _carriedParcels.IndexOf(parcel);
            if (index < 0) return false;

            if (index < _carried.Count)
            {
                if (_carried[index] != null) Destroy(_carried[index]);
                _carried.RemoveAt(index);
            }
            _carriedParcels.RemoveAt(index);
            return true;
        }

        /// <summary>Eldeki tum kutulari bosaltir (gorsellerle birlikte).</summary>
        public int DropAll()
        {
            int moved = 0;
            for (int i = _carried.Count - 1; i >= 0; i--)
            {
                if (_carried[i] == null) continue;
                Destroy(_carried[i]);
                moved++;
            }
            _carried.Clear();
            _carriedParcels.Clear();
            return moved;
        }

        public void ClearCarried()
        {
            DropAll();
        }

        // ------------------------------------------------------------------
        // Araca bin / in
        // ------------------------------------------------------------------

        public void EnterVehicle()
        {
            _inVehicle = true;
            SetVisible(false);
            if (fpsCamera != null) fpsCamera.enabled = false;
            if (vehicleCamera != null) vehicleCamera.enabled = true;
            if (vehicleController != null) vehicleController.ControlsLocked = false;
            SetCursorLocked(false);
        }

        public void ExitVehicle(Vector3 doorPosition)
        {
            _inVehicle = false;
            if (controller != null)
            {
                controller.enabled = false;
                controller.transform.position = doorPosition;
                controller.enabled = true;
                _verticalVelocity = 0f;
            }
            // Karakter aracin bakis yonune doner: ileri yon kargo
            // yukleme / kapi yonu olsun.
            if (vehicleController != null)
                transform.rotation = vehicleController.transform.rotation;

            SetVisible(true);
            if (fpsCamera != null) fpsCamera.enabled = true;
            if (vehicleCamera != null) vehicleCamera.enabled = false;
            if (vehicleController != null) vehicleController.ControlsLocked = true;

            // KRITIK: imleci kilitlemiyorduk. Bu olmadan Look() hemen
            // donuyor ve FPS kamera donmus kaliyor; oyuncu WASD ile
            // yurur ama nereye baktigini degistiremez.
            SetCursorLocked(true);
        }

        /// <summary>
        /// Ayaktayken fare tiklamasi imleci yeniden kilitler.
        ///
        /// Neden: Esc imleci serbest birakir (panel kapatma). Unity
        /// Editor'de fare Editor'a gider ve oyuncu tekrar oyuna
        /// odaklanmadan bakis kontrolu gelmez. Sol tik bu boslugu
        /// kapatir.
        /// </summary>
        public void HandleClickToRecapture()
        {
            if (_inVehicle) return;
            if (_cursorLocked) return;
            if (Input.GetMouseButtonDown(0)) SetCursorLocked(true);
        }

        private void SetVisible(bool visible)
        {
            Transform model = transform.childCount > 0 ? transform.GetChild(0) : null;
            if (model != null && !(carryAnchor != null && model == carryAnchor))
                model.gameObject.SetActive(visible);
            if (carryAnchor != null) carryAnchor.gameObject.SetActive(visible);
        }
    }
}
