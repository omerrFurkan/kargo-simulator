using KargoSim.Cargo;
using KargoSim.Delivery;
using KargoSim.Vehicle;
using UnityEngine;

namespace KargoSim.Player
{
    /// <summary>
    /// Aracin kargo kapisi: iki isi vardir.
    ///
    ///  - Elde kargo varken  E  -> kargoyu araca koy
    ///  - Elde kargo yokken  E  -> araca bin
    ///
    /// Tek bir noktada iki davranis olmasi oyuncunun tek bir yer
    /// bulmasini saglar ("buraya gel, E'ye bas").
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VanCargoPoint : MonoBehaviour, IInteractable
    {
        [Tooltip("Aracin kapi konumu (yerel). Karakter burada belirir.")]
        [SerializeField] private Vector3 doorOffset = new Vector3(0f, 0.1f, -2.4f);

        private CargoSystem _cargo;
        private DeliverySystem _delivery;
        private CourierController _courier;
        private ArcadeVehicleController _vehicle;
        private Camera _vehicleCamera;

        public void Configure(CargoSystem cargo, DeliverySystem delivery, CourierController courier,
                              ArcadeVehicleController vehicle, Camera vehicleCamera)
        {
            _cargo = cargo;
            _delivery = delivery;
            _courier = courier;
            _vehicle = vehicle;
            _vehicleCamera = vehicleCamera;
        }

        public string Prompt
        {
            get
            {
                if (_courier == null) return "";
                if (_courier.IsInVehicle) return "";

                if (_courier.IsCarrying)
                    return "E: Araca koy (" + _courier.CarriedCount + " kutu)";

                return "E: Araca bin";
            }
        }

        public bool CanInteract(CourierController courier)
        {
            return courier != null && !courier.IsInVehicle;
        }

        public void Interact(CourierController courier)
        {
            if (!CanInteract(courier)) return;

            if (courier.IsCarrying)
            {
                LoadCarried(courier);
                return;
            }

            BoardVehicle(courier);
        }

        private void LoadCarried(CourierController courier)
        {
            if (_cargo == null) return;

            int moved = 0;
            int rejected = 0;

            // ELDEKI KARGONUN KENDISI yüklenir. Once burasi depodaki
            // "ilk kargo"yu arıyordu; oyuncu elinde #7,#8,#9 gorurken
            // araca #1,#2,#3 kaliyordu ve oyuncu hangi kutuyu
            // nereden aldigini takip edemiyordu.
            var carried = new System.Collections.Generic.List<Parcel>(courier.CarriedParcels);

            for (int i = 0; i < carried.Count; i++)
            {
                Parcel parcel = carried[i];
                if (parcel == null) continue;

                if (_cargo.TryLoadParcel(parcel.Id, _delivery != null ? _delivery.Elapsed : 0f))
                {
                    courier.DropOne(parcel);
                    moved++;
                }
                else
                {
                    // Kapasite doldu: bu kutu ELDE kalir, oyuncu
                    // tekrar deneyebilir.
                    rejected++;
                }
            }

            courier.PlayAction("pick-up", 0.4f);

            if (rejected > 0)
                Debug.LogWarning("[VanCargoPoint] " + rejected + " kutu araca sigmadi (kapasite); elde kaldi.");
            if (moved > 0)
                Debug.Log("[VanCargoPoint] " + moved + " kutu araca yuklendi.");
        }

        private void BoardVehicle(CourierController courier)
        {
            Vector3 door = DoorWorldPosition();
            courier.ExitVehicle(door);
            courier.SetCursorLocked(true);
        }

        /// <summary>Karakterin aractan inebilecegi nokta.</summary>
        public Vector3 DoorWorldPosition()
        {
            Transform reference = _vehicle != null ? _vehicle.transform : transform;
            return reference.TransformPoint(doorOffset);
        }

        /// <summary>Arac lastiklerinin uzerine dogru nokta (karakter burada belirir).</summary>
        public Vector3 ExitSpot()
        {
            Transform reference = _vehicle != null ? _vehicle.transform : transform;
            return reference.TransformPoint(new Vector3(1.6f, 0.2f, 0f));
        }
    }
}
