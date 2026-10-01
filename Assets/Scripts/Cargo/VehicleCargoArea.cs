using System.Collections.Generic;
using UnityEngine;

namespace KargoSim.Cargo
{
    /// <summary>Yukleme denemesinin sonucu - oyuncuya gosterilecek acik sebep.</summary>
    public enum LoadResult
    {
        Success,
        NoParcel,            // elde kargo yok
        WrongState,          // kargo araclanabilir durumda degil
        TooHeavy,
        TooBulky,
        NoFreeSlot,          // kapasite var ama yerlesim noktasi kalmadi
        NotNearArea,         // cargo area'ya yakin degil (#34: 5 m teleport YASAK)
    }

    /// <summary>
    /// Araclardaki TEK kargo alani: gorunur, siniri belli, kapasite
    /// hesaplayan gercek alan (kargoMekanigi.md #30, #32, #75).
    ///
    /// Slot mantigi oyuncunun fiziksel yerlestirme ozgurlugunu
    /// ORTAMAZ (#35): oyuncu kutuyu kasaya kadar getirir, E'ye basar,
    /// kutu en yakin bos yerlesim noktasina oturur. Oyuncu yine de
    /// kutuyu tasimis olur.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VehicleCargoArea : MonoBehaviour
    {
        [Header("Kapasite")]
        [SerializeField] private float maxWeightKg = 60f;
        [SerializeField] private float maxVolumeLiters = 150f;

        [Header("Yerlesim")]
        [SerializeField] private Transform placementRoot;
        [SerializeField] private int slotCount = 6;
        [SerializeField] private float slotSpacing = 0.55f;

        private readonly List<Transform> _slots =
            new List<Transform>();
        private readonly List<ParcelWorldObject> _occupants =
            new List<ParcelWorldObject>();

        public float MaxWeightKg => maxWeightKg;
        public float MaxVolumeLiters => maxVolumeLiters;

        public float UsedWeightKg { get; private set; }
        public float UsedVolumeLiters { get; private set; }
        public int Count => _occupants.Count;
        public int FreeSlotCount => Mathf.Max(0, slotCount - _occupants.Count);

        public void Configure(CargoCapacity capacity)
        {
            maxWeightKg = capacity.MaxWeightKg;
            maxVolumeLiters = capacity.MaxVolumeLiters;
        }

        private void Awake() => BuildSlots();

        /// <summary>
        /// Yerlesim noktalarini olusturur. Onceki sistemde kasadaki
        /// kutular "Izgara" mantigiyla otomatik hesaplaniyordu; oyuncu
        /// nereye koyacagini gostermiyordu. Simdi her slot fiziksel
        /// bir Transform ve gorsel olarak gorunur.
        /// </summary>
        private void BuildSlots()
        {
            _slots.Clear();
            if (placementRoot != null)
            {
                for (int i = 0; i < placementRoot.childCount; i++)
                    _slots.Add(placementRoot.GetChild(i));
            }

            if (_slots.Count == slotCount) return;

            // Sahne YAML'ine dokunmadan runtime'da kur
            // (bu projede elle duzenleme uc kez bozuldu).
            if (placementRoot == null)
            {
                var go = new GameObject("YerlesimNoktalari");
                go.transform.SetParent(transform, false);
                placementRoot = go.transform;
            }
            else if (placementRoot.childCount > 0)
            {
                // Sahneden gelen noktalar varsa onlari kullan, hepsini silme.
                if (_slots.Count >= slotCount) return;
            }

            for (int i = 0; i < slotCount; i++)
            {
                if (i < _slots.Count) continue;
                var s = new GameObject("Slot_" + (i + 1).ToString("00"));
                s.transform.SetParent(placementRoot, false);
                int col = i % 2;
                int row = i / 2;
                s.transform.localPosition = new Vector3(
                    (col - 0.5f) * slotSpacing,
                    (row % 2) * 0.34f,
                    (row / 2) * -0.30f);
                s.transform.localRotation = Quaternion.identity;
                _slots.Add(s.transform);
            }
        }

        /// <summary>Boyle bir kutu bu araca sigar mi? (#39 ikili kural)</summary>
        public bool Fits(Parcel parcel)
        {
            if (parcel == null) return false;
            return UsedWeightKg + parcel.WeightKg <= maxWeightKg + 0.001f
                && UsedVolumeLiters + parcel.VolumeLiters <= maxVolumeLiters + 0.001f;
        }

        /// <summary>Oyuncuya gosterilecek kapasite sebebi (#40, #42).</summary>
        public string RejectionReason(Parcel parcel)
        {
            if (parcel == null) return "Elinde kargo yok.";
            if (UsedWeightKg + parcel.WeightKg > maxWeightKg + 0.001f)
                return "Bu kargo bisikletin kapasitesini asiyor.  "
                     + UsedWeightKg.ToString("0.0") + " / " + maxWeightKg.ToString("0.0") + " kg";
            if (UsedVolumeLiters + parcel.VolumeLiters > maxVolumeLiters + 0.001f)
                return "Bu kargo bisikletin kapasitesini asiyor.  "
                     + UsedVolumeLiters.ToString("0.0") + " / " + maxVolumeLiters.ToString("0.0") + " L";
            if (FreeSlotCount <= 0) return "Kasa dolu.";
            return "";
        }

        /// <summary>En yakin BOS yerlesim noktasi (#35 snap).</summary>
        public Transform FindFreeSlot(Vector3 referenceWorldPos)
        {
            float best = float.MaxValue;
            Transform bestT = null;
            for (int i = 0; i < _slots.Count; i++)
            {
                if (IsOccupied(i)) continue;
                float d = (_slots[i].position - referenceWorldPos).sqrMagnitude;
                if (d < best) { best = d; bestT = _slots[i]; }
            }
            return bestT;
        }

        public bool TryGetSlotOf(ParcelWorldObject box, out int index)
        {
            for (int i = 0; i < _occupants.Count; i++)
            {
                if (_occupants[i] == box)
                {
                    index = _occupants[i].SlotIndex;
                    return true;
                }
            }
            index = -1;
            return false;
        }

        private bool IsOccupied(int slotIndex)
        {
            for (int i = 0; i < _occupants.Count; i++)
                if (_occupants[i].SlotIndex == slotIndex) return true;
            return false;
        }

        public void Occupy(ParcelWorldObject box, int slotIndex)
        {
            if (box == null) return;
            if (!_occupants.Contains(box))
            {
                _occupants.Add(box);
                UsedWeightKg += box.Parcel.WeightKg;
                UsedVolumeLiters += box.Parcel.VolumeLiters;
            }
            box.SlotIndex = slotIndex;
        }

        public void Release(ParcelWorldObject box)
        {
            if (box == null) return;
            int i = _occupants.IndexOf(box);
            if (i < 0) return;
            _occupants.RemoveAt(i);
            UsedWeightKg = Mathf.Max(0f, UsedWeightKg - box.Parcel.WeightKg);
            UsedVolumeLiters = Mathf.Max(0f, UsedVolumeLiters - box.Parcel.VolumeLiters);
            box.SlotIndex = -1;
        }

        public IReadOnlyList<ParcelWorldObject> Occupants => _occupants;

        /// <summary>Kasa ici kutular (#105, #107): hepsi sinir icinde mi?</summary>
        public bool AllInsideBounds()
        {
            for (int i = 0; i < _occupants.Count; i++)
            {
                if (_occupants[i] == null) continue;
                if (!_slots[_occupants[i].SlotIndex].IsChildOf(transform) &&
                    _occupants[i].SlotIndex >= 0)
                {
                    Vector3 lp = transform.InverseTransformPoint(_occupants[i].transform.position);
                    if (Mathf.Abs(lp.x) > 1.2f || Mathf.Abs(lp.y) > 1.2f || Mathf.Abs(lp.z) > 1.2f)
                        return false;
                }
            }
            return true;
        }
    }
}
