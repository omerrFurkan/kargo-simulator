using KargoSim.Cargo;
using KargoSim.Delivery;
using UnityEngine;

namespace KargoSim.Player
{
    /// <summary>
    /// Adres kapısı: kargo buraya bırakılır.
    ///
    /// Karakter aracı bırakıp eve yürür, kapının önünde E'ye basar ve
    /// elindeki kutu teslim edilir. Yükleme ile teslim simetriktir:
    /// ikisi de karakterin eliyle olur.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AddressDoor : MonoBehaviour, IInteractable
    {
        [Tooltip("Kapinin onundeki etkilesim noktasi (yerel).")]
        [SerializeField] private Vector3 interactOffset = new Vector3(0f, 0f, 1.6f);

        private DeliverySystem _delivery;
        private CourierController _courier;

        public void Configure(DeliverySystem delivery, CourierController courier)
        {
            _delivery = delivery;
            _courier = courier;
        }

        public string Prompt
        {
            get
            {
                if (_courier == null) return "";
                if (_delivery == null || _delivery.CurrentStop == null) return "";

                int pending = _delivery.CurrentStop.PendingCount;
                if (pending <= 0) return "Adres tamamlandi";
                if (!_courier.IsCarrying) return "Kargo yok (elimde)";
                return "E: Karguyu birak (" + pending + " bekliyor)";
            }
        }

        public bool CanInteract(CourierController courier)
        {
            if (courier == null || _delivery == null) return false;
            if (_delivery.CurrentStop == null) return false;
            if (_delivery.CurrentStop.PendingCount <= 0) return false;
            return courier.IsCarrying;
        }

        public void Interact(CourierController courier)
        {
            if (!CanInteract(courier)) return;

            DeliveryStop stop = _delivery.CurrentStop;
            if (stop == null) return;

            // ELDEKI KARGONUN KENDISI teslim edilir. Once duraktaki
            // "ilk aktif kargo"yu veriyordu; oyuncu 3 kutuyla gelip
            // 1 teslim edince eli bosalip geri kalani aracta kaliyordu.
            var carried = new System.Collections.Generic.List<Parcel>(courier.CarriedParcels);
            for (int i = 0; i < carried.Count; i++)
            {
                Parcel parcel = carried[i];
                if (parcel == null) continue;

                // Bu adrese ait mi? Degilse dokunma: yalnizca DOGRU
                // adresin kargosu teslim edilmeli.
                if (parcel.DestinationId != stop.DestinationId) continue;

                _delivery.TryDeliver(parcel.Id);
                courier.DropOne(parcel);
                courier.PlayAction("interact-right", 0.6f);
                return;
            }
        }

        public Vector3 InteractWorldPosition()
        {
            return transform.TransformPoint(interactOffset);
        }
    }
}
