using System.Collections.Generic;
using KargoSim.Cargo;
using KargoSim.Delivery;
using KargoSim.Vehicle;
using UnityEngine;

namespace KargoSim.Player
{
    /// <summary>
    /// Kurye karakteri: kargoyu arac DEPOYA GIDEREK tasir, adrese
    /// gidince KENDISI birakir.
    ///
    /// Neden ayri bir katman: <see cref="CargoSystem"/> ve
    /// <see cref="DeliverySystem"/> saf durum mantigidir - kargo
    /// yuklendiginde aninda "yuklendi" derler. Onlar test edilebilir ve
    /// deterministik kalmalidir. Bu ajan ise o ANIN GORSELINI ve
    /// ZAMANINI yonetir: yurur, animasyon oynatir, sonra mantik
    /// sistemine "simdi yukle" der. Boylece:
    ///   - oyun anlasilir olur (kargo yerinden oynar),
    ///   - saf mantik testleri degismez,
    ///   - sure, kargonun GERCEKTEN yuklendigi andan baslar.
    ///
    /// Update() YOK: CompositionRoot her karede <see cref="Tick"/> cagirir
    /// (oyunda tek Update kurali).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CourierAgent : MonoBehaviour
    {
        public enum CourierState
        {
            /// <summary>Arac icinde; karakter gizli, arac surulebilir.</summary>
            InVehicle,
            WalkingToPickup,
            PickingUp,
            WalkingToVan,
            LoadingIntoVan,
            WalkingToAddress,
            DroppingOff,
            ReturningToVan,
        }

        [Header("Referanslar")]
        [Tooltip("Karakterin FBX modeli. Kapatilince karakter gizlenir.")]
        [SerializeField] private Transform visualRoot;
        [Tooltip("Elde tasinan kutunun duracagi nokta. Karakterin YEREL " +
                 "olcegi 1 olmali; model olcekli oldugu icin kutu " +
                 "buyemesin diye buraya baglanir.")]
        [SerializeField] private Transform carryAnchor;
        [SerializeField] private Transform vanCargoAnchor;
        [SerializeField] private Animator animator;
        [SerializeField] private CharacterController controller;
        [SerializeField] private ArcadeVehicleController vehicleController;

        [Header("Hareket")]
        [Tooltip("Yurume hizi (metre/saniye).")]
        [SerializeField] private float walkSpeed = 3.2f;
        [Tooltip("Kargo tasirken hiz (metre/saniye).")]
        [SerializeField] private float carrySpeed = 2.5f;
        [Tooltip("Hedefe bu kadar yakinlayinca varildi sayilir (metre).")]
        [SerializeField] private float arriveRadius = 0.5f;
        [SerializeField] private float turnSpeed = 12f;

        [Header("Sureler")]
        [Tooltip("Kargoyu alma suresi (saniye).")]
        [SerializeField] private float pickUpDuration = 0.65f;
        [Tooltip("Araca koyma suresi (saniye).")]
        [SerializeField] private float loadDuration = 0.55f;
        [Tooltip("Adrese birakma suresi (saniye).")]
        [SerializeField] private float dropDuration = 0.7f;

        // Kenney Mini Characters FBX'indeki gercek klip adlari.
        private const string ClipIdle = "idle";
        private const string ClipWalk = "walk";
        private const string ClipPickUp = "pick-up";
        private const string ClipCarryIdle = "holding-both";
        private const string ClipDrop = "interact-right";

        private const float Gravity = -22f;

        private CargoSystem _cargo;
        private DeliverySystem _delivery;
        private Transform _van;
        private Vector3 _depotPosition;

        private readonly Queue<Parcel> _loadQueue = new Queue<Parcel>();
        private readonly Dictionary<int, GameObject> _vanCargo = new Dictionary<int, GameObject>();
        private readonly List<int> _layoutOrder = new List<int>();
        private GameObject _carriedVisual;
        private Parcel _deliverParcel;

        private CourierState _state = CourierState.InVehicle;
        private Vector3 _target;
        private float _timer;
        private float _verticalVelocity;
        private bool _configured;
        private string _status = "";

        // ------------------------------------------------------------------
        // Genel API
        // ------------------------------------------------------------------

        public CourierState State => _state;

        /// <summary>Karakter arac disinda mi? (Arac o zaman surulemez.)</summary>
        public bool IsBusy => _state != CourierState.InVehicle;

        public bool IsCarrying => _carriedVisual != null;

        /// <summary>Arac bosaltma icin kullanilir: kutulari da gizlenir.</summary>
        public int LoadedVisualCount => _vanCargo.Count;

        /// <summary>HUD ipucunda gosterilecek o anki durum metni.</summary>
        public string StatusText => _status;

        public void Configure(CargoSystem cargo, DeliverySystem delivery, Transform van, Vector3 depot)
        {
            _cargo = cargo;
            _delivery = delivery;
            _van = van;
            _depotPosition = depot;
            _configured = true;

            EnterVehicle();
        }

        // ------------------------------------------------------------------
        // Oyuncu istekleri
        // ------------------------------------------------------------------

        /// <summary>
        /// Depodan secilen kargolari araca yukletmek icin kuyruga alir.
        /// Karakter sirayla teker teker tasir.
        /// </summary>
        public void QueueLoad(IList<Parcel> parcels)
        {
            if (!_configured || parcels == null || IsBusy) return;

            for (int i = 0; i < parcels.Count; i++)
            {
                if (parcels[i] != null && parcels[i].IsActive) _loadQueue.Enqueue(parcels[i]);
            }

            if (_loadQueue.Count > 0) BeginNextLoad();
        }

        /// <summary>Duraktaki kargoyu adrese tasimak icin.</summary>
        public void QueueDeliver(Parcel parcel)
        {
            if (!_configured || parcel == null || IsBusy) return;
            if (!parcel.IsLoaded) return;

            _deliverParcel = parcel;
            ExitVehicle();
            GoTo(_delivery != null && _delivery.CurrentStop != null
                      ? _delivery.CurrentStop.Position
                      : transform.position,
                  CourierState.WalkingToAddress);
            SetStatus(parcel.RecipientName + " adresine gidiyor");
        }

        /// <summary>Arac bosaltildiginda gorsel kutulari da temizler.</summary>
        public void ClearVanVisuals()
        {
            foreach (KeyValuePair<int, GameObject> pair in _vanCargo)
                if (pair.Value != null) Destroy(pair.Value);

            _vanCargo.Clear();
            _layoutOrder.Clear();
        }

        // ------------------------------------------------------------------
        // Her kare (CompositionRoot cagirir)
        // ------------------------------------------------------------------

        public void Tick(float deltaTime)
        {
            if (!_configured) return;

            switch (_state)
            {
                case CourierState.WalkingToPickup:
                case CourierState.WalkingToVan:
                case CourierState.WalkingToAddress:
                case CourierState.ReturningToVan:
                    TickWalking(deltaTime);
                    break;

                case CourierState.PickingUp:
                    TickTimed(pickUpDuration, CompletePickUp);
                    break;

                case CourierState.LoadingIntoVan:
                    TickTimed(loadDuration, CompleteLoad);
                    break;

                case CourierState.DroppingOff:
                    TickTimed(dropDuration, CompleteDrop);
                    break;
            }
        }

        // ------------------------------------------------------------------
        // Durum gecisleri
        // ------------------------------------------------------------------

        private void EnterVehicle()
        {
            _state = CourierState.InVehicle;
            SetVisible(false);
            if (animator != null) animator.CrossFadeInFixedTime(ClipIdle, 0.12f);
            if (controller != null) controller.enabled = false;
            if (vehicleController != null) vehicleController.ControlsLocked = false;
            ClearCarried();
            SetStatus("");
        }

        /// <summary>
        /// Karakteri aracin yanina cikarir ve araci kilitler.
        /// Karakter yururken arac surulemez: oyuncu ayni anda hem
        /// yuruyup hem arac kullanmaz, boylece "ben mi yuruyorum
        /// arac mi?" belirsizligi olusmaz.
        /// </summary>
        private void ExitVehicle()
        {
            SetVisible(true);

            if (controller != null)
            {
                controller.enabled = false;
                Vector3 p = VanSidePoint();
                controller.transform.position = new Vector3(p.x, p.y + 0.1f, p.z);
                controller.enabled = true;
                _verticalVelocity = 0f;
            }

            if (vehicleController != null) vehicleController.ControlsLocked = true;
        }

        private void BeginNextLoad()
        {
            if (_loadQueue.Count == 0)
            {
                ReturnToVan();
                return;
            }

            if (_state == CourierState.InVehicle) ExitVehicle();
            GoTo(_depotPosition, CourierState.WalkingToPickup);
            SetStatus("Depodan kargo aliniyor");
        }

        private void ReturnToVan()
        {
            if (_state == CourierState.InVehicle) return;

            // Kargo tasiyorsa once van'a birak, sonra don.
            if (IsCarrying)
            {
                GoTo(VanSidePoint(), CourierState.WalkingToVan);
                return;
            }

            ExitVehicle();
            GoTo(VanSidePoint(), CourierState.ReturningToVan);
            SetStatus("Araca donuluyor");
        }

        private void GoTo(Vector3 position, CourierState next)
        {
            _target = new Vector3(position.x, transform.position.y, position.z);
            _state = next;
        }

        // ------------------------------------------------------------------
        // Durum davranislari
        // ------------------------------------------------------------------

        private void TickWalking(float dt)
        {
            Vector3 toTarget = _target - transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;

            if (distance <= arriveRadius)
            {
                OnWalkFinished();
                return;
            }

            Vector3 direction = toTarget / distance;
            float speed = IsCarrying ? carrySpeed : walkSpeed;

            // Yere yapisik kal. Gercek -9.81 yerine -22: oyun olcegi
            // kucuk, hafif yer cekimi hissi yer serbest dusus verirdi.
            if (controller != null && controller.isGrounded) _verticalVelocity = -2f;
            else _verticalVelocity += Gravity * dt;

            Vector3 motion = direction * speed;
            motion.y = _verticalVelocity;

            if (controller != null) controller.Move(motion * dt);
            else transform.position += motion * dt;

            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(direction, Vector3.up), turnSpeed * dt);

            PlayClip(ClipWalk);
        }

        private void OnWalkFinished()
        {
            switch (_state)
            {
                case CourierState.WalkingToPickup:
                    _state = CourierState.PickingUp;
                    _timer = 0f;
                    PlayClip(ClipPickUp);
                    SetStatus("Kargo aliniyor");
                    break;

                case CourierState.WalkingToVan:
                    _state = CourierState.LoadingIntoVan;
                    _timer = 0f;
                    PlayClip(ClipWalk);
                    SetStatus("Araca yukleniyor");
                    break;

                case CourierState.WalkingToAddress:
                    _state = CourierState.DroppingOff;
                    _timer = 0f;
                    PlayClip(ClipDrop);
                    SetStatus("Kargo birakiliyor");
                    break;

                case CourierState.ReturningToVan:
                    EnterVehicle();
                    break;
            }
        }

        /// <summary>Sureli durumlar: sayaci isletir, bitince tamamlar.</summary>
        private void TickTimed(float duration, System.Action onComplete)
        {
            _timer += Time.deltaTime;
            if (_timer < duration) return;
            onComplete();
        }

        private void CompletePickUp()
        {
            // Kuyrugun basindaki kargo bu ana geldi; elde gorselini al.
            if (_loadQueue.Count > 0)
            {
                _carriedVisual = CreateParcelVisual(_loadQueue.Peek());
                PlayClip(ClipCarryIdle);
            }

            GoTo(VanSidePoint(), CourierState.WalkingToVan);
            SetStatus("Kargo araca tasiniyor");
        }

        private void CompleteLoad()
        {
            CommitLoad();
            BeginNextLoad();
        }

        private void CompleteDrop()
        {
            CommitDelivery();
            ReturnToVan();
        }

        // ------------------------------------------------------------------
        // Saf mantik sistemine bildirme
        // ------------------------------------------------------------------

        private void CommitLoad()
        {
            if (_loadQueue.Count == 0) return;

            Parcel parcel = _loadQueue.Dequeue();
            if (_cargo == null) return;

            // Kapasite kontrolu BURADA olur. Sigmayan kargo yolda
            // tutulmaz: kutu elde degil, karakter bos doner.
            if (!_cargo.TryLoadParcel(parcel.Id))
            {
                SetStatus(parcel.Label + " araca sigmiyor");
                ClearCarried();
                return;
            }

            if (_carriedVisual != null)
            {
                _carriedVisual.transform.SetParent(vanCargoAnchor, true);
                _vanCargo[parcel.Id] = _carriedVisual;
                _carriedVisual = null;
                LayoutVanCargo();
            }

            SetStatus(parcel.Label + " yuklendi");
        }

        private void CommitDelivery()
        {
            Parcel parcel = _deliverParcel;
            _deliverParcel = null;
            if (parcel == null || _delivery == null) return;

            _delivery.TryDeliver(parcel.Id);

            if (_vanCargo.TryGetValue(parcel.Id, out GameObject visual))
            {
                _vanCargo.Remove(parcel.Id);
                if (visual != null) Destroy(visual);
                LayoutVanCargo();
            }

            SetStatus(parcel.Label + " teslim edildi");
        }

        // ------------------------------------------------------------------
        // Gorsel yardimcilar
        // ------------------------------------------------------------------

        /// <summary>Aracin arka yuzu: kargo oradan yuklenir.</summary>
        private Vector3 VanSidePoint()
        {
            if (_van == null) return transform.position;
            return _van.position - _van.forward * 2.8f + _van.right * 1.3f;
        }

        private GameObject CreateParcelVisual(Parcel parcel)
        {
            Transform parent = carryAnchor != null ? carryAnchor : transform;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Parcel_" + (parcel != null ? parcel.Id : 0);
            Destroy(go.GetComponent<Collider>());

            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            go.AddComponent<ParcelVisual>().Apply(parcel != null ? parcel.Type : null);
            return go;
        }

        private void ClearCarried()
        {
            if (_carriedVisual == null) return;
            Destroy(_carriedVisual);
            _carriedVisual = null;
        }

        /// <summary>
        /// Arac ici kutulari yan yana dizer. Dictionary sirasi garanti
        /// olmadigi icin anahtarlar siralanir; yoksa kutular her
        /// yerlestirmede yer degistirir.
        /// </summary>
        private void LayoutVanCargo()
        {
            if (vanCargoAnchor == null) return;

            _layoutOrder.Clear();
            foreach (int id in _vanCargo.Keys) _layoutOrder.Add(id);
            _layoutOrder.Sort();

            for (int i = 0; i < _layoutOrder.Count; i++)
            {
                if (!_vanCargo.TryGetValue(_layoutOrder[i], out GameObject box) || box == null) continue;
                box.transform.localPosition =
                    new Vector3(-0.45f + (i % 3) * 0.45f, 0.15f + (i / 3) * 0.42f, 0f);
            }
        }

        /// <summary>
        /// Karakteri gizler. Tasinan kutu modelin altinda DEGIL, ayri
        /// bir tasma noktasinda durur; o yuzden ikisi birlikte
        /// kapatilir.
        /// </summary>
        private void SetVisible(bool visible)
        {
            if (visualRoot != null) visualRoot.gameObject.SetActive(visible);
            if (carryAnchor != null) carryAnchor.gameObject.SetActive(visible);
        }

        private void PlayClip(string clip)
        {
            if (animator == null || string.IsNullOrEmpty(clip)) return;
            animator.CrossFadeInFixedTime(clip, 0.12f);
        }

        private void SetStatus(string text)
        {
            _status = text;
        }
    }
}
