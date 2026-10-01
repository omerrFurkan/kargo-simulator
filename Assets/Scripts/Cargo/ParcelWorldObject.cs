using System;
using UnityEngine;

namespace KargoSim.Cargo
{
    /// <summary>
    /// FIZIKSEL KARGO NESNESI (kargoMekanigi.md #5B, #13, #67, #72, #117).
    ///
    /// Para Datasidir, GameObject DEGILDIR. Data ile fiziksel nesne
    /// birbirine gereksiz baglanmaz; bu nesne sadece Parcel'a referans
    /// tutar ve STATE'e gore fizigini degistirir:
    ///
    ///   World / OnPackingTable : Rigidbody DYNAMIC, gercek carpisma
    ///   Held (Carried)         : Rigidbody KINEMATIC, CargoHoldAnchor'i izler
    ///   Loaded                 : Rigidbody KINEMATIC, arac slotuna sabit
    ///
    /// ONCEKI SISTEMDE kutu duz bir "Cube" idi ve kuryenin altinda
    /// yigilmis halde duruyordu; simdi gercek bir nesne, gercek
    /// collider, gercek rigidbody var.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class ParcelWorldObject : MonoBehaviour
    {
        [Header("Boyut")]
        [SerializeField] private BoxSizeKind sizeKind = BoxSizeKind.Medium;

        [Header("Fizik")]
        [SerializeField] private float mass = 2.2f;
        [SerializeField] private float drag = 2.4f;
        [SerializeField] private float angularDrag = 6f;
        [SerializeField] private float maxSpeedCap = 6f;

        [Tooltip("Kutu bir digerinin icinde dogarsa disari itme hizi. "
                 + "Cok yuksekse kutu 'firlatilir'.")]
        [SerializeField] private float depenetrationCap = 0.6f;

        private Rigidbody _body;
        private BoxCollider _box;
        private Collider _playerCollider;
        private Collider _vehicleCollider;

        /// <summary>Kutunun bagli oldugu DATA. Asla null olmamalidir.</summary>
        public Parcel Parcel { get; private set; }

        /// <summary>Bu kutu arac kasa hangi slottu? -1 = kasada degil.</summary>
        public int SlotIndex { get; set; } = -1;

        public BoxSizeKind SizeKind => sizeKind;
        public bool IsHeld => Parcel != null && Parcel.State == ParcelState.Carried;
        public bool IsLoaded => Parcel != null && Parcel.State == ParcelState.LoadedOnVehicle;

        /// <summary>Etiket gozlugu icin kutu boyutu (metre cinsinden).</summary>
        public Vector3 BoxSize => SizeTable.Dimensions(sizeKind);

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _box = GetComponent<BoxCollider>();
            ApplyPhysicsForState(ParcelState.World);
        }

        public void Bind(Parcel parcel)
        {
            if (parcel == null)
                throw new ArgumentNullException(nameof(parcel));
            if (Parcel != null && Parcel != parcel)
                throw new InvalidOperationException(
                    "ParcelWorldObject baska bir Parcel'a baglanamaz (#84 duplicate).");

            Parcel = parcel;
            sizeKind = SizeTable.KindFor(parcel.VolumeLiters);
            ApplySize();
        }

        private void ApplySize()
        {
            Vector3 d = BoxSize;
            transform.localScale = d;
            if (_box != null) _box.size = Vector3.one;   // kutu primitive'i 1 unit
        }

        // ===============================================================
        // FIZIK - STATE'E GORE
        // ===============================================================
        /// <summary>
        /// #67: fizik state'e gore degisir. Held ve Loaded kargolarda
        /// simulasyon KAPALI olur (#111 performans: gereksiz Rigidbody
        /// simulation yapma).
        /// </summary>
        public void ApplyPhysicsForState(ParcelState state)
        {
            if (_body == null) _body = GetComponent<Rigidbody>();

            switch (state)
            {
                case ParcelState.Carried:
                case ParcelState.LoadedOnVehicle:
                case ParcelState.Packaging:
                case ParcelState.Delivered:
                case ParcelState.Cancelled:
                    _body.isKinematic = true;
                    _body.useGravity = false;
                    _body.velocity = Vector3.zero;
                    _body.angularVelocity = Vector3.zero;
                    _body.interpolation = RigidbodyInterpolation.None;
                    break;

                default:
                    _body.isKinematic = false;
                    _body.useGravity = true;
                    _body.mass = mass;
                    _body.drag = drag;
                    _body.angularDrag = angularDrag;
                    _body.interpolation = RigidbodyInterpolation.Interpolate;
                    _body.maxDepenetrationVelocity = depenetrationCap;
                    break;
            }
        }

        /// <summary>
        /// Dunya durumunda kutunun "sakin" kalmasi (#19, #110, #145).
        /// Gercekci fizik DEGIL, kontrollu fizik: kutular roket gibi
        /// savrulmaz, ziplamaz, sonsuza kadar sallanmaz.
        /// </summary>
        private void FixedUpdate()
        {
            if (_body == null || _body.isKinematic) return;
            if (_body.velocity.sqrMagnitude > maxSpeedCap * maxSpeedCap)
                _body.velocity = _body.velocity.normalized * maxSpeedCap;
        }

        // ===============================================================
        // TASIYICI BAGLANTILARI (#68, #69)
        // ===============================================================
        /// <summary>
        /// Kuryeyi tutarken fizik motoru saçmalamamasin diye player
        /// carpismasi kapatilir ve geri ACILIR. Ilk basta kapatip
        /// unutmak, kutuya yakinlasinca "hayalet kutu" hatasina yol
        /// acti; bu yuzden cift yonlu baglanti kuruluyor.
        /// </summary>
        public void AttachIgnore(Collider player, Collider vehicle)
        {
            DetachIgnore();
            _playerCollider = player;
            _vehicleCollider = vehicle;
            ApplyIgnore(true);
        }

        public void DetachIgnore()
        {
            ApplyIgnore(false);
            _playerCollider = null;
            _vehicleCollider = null;
        }

        private void ApplyIgnore(bool on)
        {
            if (_box == null) _box = GetComponent<BoxCollider>();
            if (_box == null) return;
            if (on)
            {
                if (_playerCollider != null)
                    Physics.IgnoreCollision(_box, _playerCollider, true);
                if (_vehicleCollider != null)
                    Physics.IgnoreCollision(_box, _vehicleCollider, true);
            }
            else
            {
                if (_playerCollider != null)
                    Physics.IgnoreCollision(_box, _playerCollider, false);
                if (_vehicleCollider != null)
                    Physics.IgnoreCollision(_box, _vehicleCollider, false);
            }
        }

        /// <summary>Kasaya sabitlendiginde aracla birlikte hareket etsin.</summary>
        public void SnapTo(Transform slot)
        {
            if (slot == null) return;
            slot.SetParent(transform.parent, true);
            transform.position = slot.position;
            transform.rotation = slot.rotation;
        }

        private void OnDestroy()
        {
            // ALTIN KURAL (#87): kutu yalnizca Delivered/Cancelled ile
            // yok edilir. Bu yuzden OnDestroy'da "temizlik" yapmayan tek
            // sey: teslim oncesi her sey korunur. Buradaki tek is
            // collider referanslarini birakmaktir.
            DetachIgnore();
        }
    }

    // ==================================================================
    // KUTU BOYUTLARI (#6, #7, #143)
    // ==================================================================
    public enum BoxSizeKind { Small = 0, Medium = 1, Large = 2 }

    /// <summary>
    /// Kargo turu -> fiziksel kutu boyutu.
    ///
    /// #7: agirlik ve hacim yalnizca UI sayisi degil; kutunun fiziksel
    /// boyutunu da etkilemeli. Bu esleme olmazsa "3 kg elektronik"
    /// ile "8 kg esya" ayni kutuda durur ve oyuncu kapasiteyi anlamaz.
    /// </summary>
    public static class SizeTable
    {
        // 1 Unity unit = 1 metre (#143)
        public static Vector3 Dimensions(BoxSizeKind k)
        {
            switch (k)
            {
                case BoxSizeKind.Small: return new Vector3(0.30f, 0.22f, 0.26f);
                case BoxSizeKind.Large: return new Vector3(0.56f, 0.44f, 0.50f);
                default: return new Vector3(0.42f, 0.32f, 0.36f);
            }
        }

        /// <summary>Hacme gore kutu boyutu: kucucuk degil, hacim belirler.</summary>
        public static BoxSizeKind KindFor(float volumeLiters)
        {
            if (volumeLiters <= 3.5f) return BoxSizeKind.Small;    // belge, ilac
            if (volumeLiters <= 14f) return BoxSizeKind.Medium;    // kirtasiye, elektronik, gida
            return BoxSizeKind.Large;                              // esya
        }

        public static string Label(BoxSizeKind k)
        {
            switch (k)
            {
                case BoxSizeKind.Small: return "Kucuk";
                case BoxSizeKind.Large: return "Buyuk";
                default: return "Orta";
            }
        }
    }
}
