using KargoSim.Cargo;
using UnityEngine;

namespace KargoSim.Player
{
    /// <summary>
    /// YERDE DURAN KUTU: oyuncunun bıraktığı, dünyada fiziksel olarak
    /// var olan kargo.
    ///
    /// NEDEN VAR: once bırakma YOKTU. <see cref="CourierController.DropOne"/>
    /// kutuyu yere koymaz, GORSELI YOK EDERDI. Oyuncu raftan aldigini
    /// masaya goturup "buraya birak" dediginde kutu kayboluyordu.
    ///
    /// Bu, dikey dilimin EN ONEMLI ADIMINI imkansiz kiliyordu:
    ///   kutu al -> masaya gotur -> BIRAK -> paketle -> tekrar al
    /// ikinci ok cogu kutu olmadan BITEMIYORDU.
    ///
    /// ETKILESIM (IInteractable): E ile oyuncunun eline alinir. Boylece
    /// kutu gercek bir nesne gibi tasinir, isinlanmaz.
    ///
    /// PAKETLEME MASASI KURALI: masanin uzerinde duran bir kutu OYUNCUYA
    /// VERILMEZ; masaya aittir ve orada paketlenir. Aksi halde oyuncu
    /// E'ye basip "kutuyu al" yerine "paketlemeye bas" der ve akis
    /// takilir. Bu kural <see cref="CanInteract"/> icinde acikca yazilidir.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GroundParcel : MonoBehaviour, IInteractable
    {
        [Tooltip("Kutunun hangi kargoyu temsil ettigi.")]
        [SerializeField] private int parcelId;

        [Tooltip("Gorsel kok. Kutu yeniden yaratildiginda buraya baglanir.")]
        [SerializeField] private Transform visualRoot;

        private Parcel _parcel;
        private bool _released;

        public Parcel Parcel => _parcel;

        public int ParcelId => parcelId;

        /// <summary>Kutu oyuncunun eline gecmisse true.</summary>
        public bool Released => _released;

        /// <summary>
        /// Editor kancası: kutu nesnesini hazırlar.
        /// </summary>
        public void Configure(Parcel parcel, Transform visual)
        {
            _parcel = parcel;
            parcelId = parcel != null ? parcel.Id : 0;
            visualRoot = visual;
        }

        private void Start()
        {
            if (visualRoot == null) return;

            // Kutu ASLA oyuncunun bir parcası değil: masa üzerinde
            // paketlenirken bile yerinde durur.
            visualRoot.SetParent(transform, true);
        }

        // ------------------------------------------------------------------
        // IInteractable
        // ------------------------------------------------------------------

        public string Prompt
        {
            get
            {
                if (_released) return "";

                // Kutunun durumu ipucunda: oyuncu ham mu hazir mi
                // ayirt edebilsin, yoksa neden paketlemeye calistigini
                // anlamaz.
                if (_parcel != null && _parcel.IsPackaged)
                    return "E: Paketi al";

                return "E: Kutu al";
            }
        }

        public bool CanInteract(CourierController courier)
        {
            if (_released) return false;
            if (courier == null) return false;
            if (courier.IsInVehicle) return false;
            if (!courier.CanCarryMore) return false;

            // Paketleme masası üzerindeyse kutu MASAYA AİTTİR: E ile
            // alınmaz, paketlenir. Aksi halde oyuncu kutuyu almak
            // isteyerek masada iş yapamaz.
            if (OnPackingTable()) return false;

            return true;
        }

        public void Interact(CourierController courier)
        {
            if (!CanInteract(courier)) return;
            if (_parcel == null) return;

            if (courier.PickUp(_parcel) == false) return;

            // Kutu artık oyuncunun elinde: bu nesne yok olmali.
            _released = true;
            Destroy(gameObject);
        }

        /// <summary>
        /// Kutu paketleme masasının çalışma alanında mı?
        ///
        /// Masanın kendi hacmini kullanırız; ayrı bir "bırakma noktası"
        /// icat etmeyiz, cunku oyuncu masanin UZERINE birakacak ve
        /// baska bir yere birakmayi denemeyecek.
        /// </summary>
        private bool OnPackingTable()
        {
            var table = GetComponentInParent<PackingTable>();
            if (table == null) return false;

            Vector3 local = table.transform.InverseTransformPoint(transform.position);

            // Masanin tezgah ust yuzeyi: yerel X/Z sinirlari icinde ve
            // tezgahin biraz ustunde.
            const float halfX = 1.4f;
            const float halfZ = 1.0f;
            const float minY = 0.4f;
            const float maxY = 2.0f;

            return Mathf.Abs(local.x) <= halfX
                && Mathf.Abs(local.z) <= halfZ
                && local.y >= minY && local.y <= maxY;
        }

        /// <summary>
        /// Test kancası: kutunun masaya ait olup olmadığını sorgular.
        /// </summary>
        public bool IsOnTableForTests() => OnPackingTable();
    }
}
