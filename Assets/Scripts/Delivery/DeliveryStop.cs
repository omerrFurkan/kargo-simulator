using System.Collections.Generic;
using KargoSim.Cargo;
using UnityEngine;

namespace KargoSim.Delivery
{
    /// <summary>
    /// Arac uzerindeki kargolardan OLUSAN runtime durak noktasi.
    ///
    /// "Durak" haritadaki bir teslimat noktasidir, ama kargo yuklenmeden
    /// once var olmaz: hic kimse o adrese gitmez. Bu yuzden MapLayout'ten
    /// okunan <see cref="DeliveryPointData"/> ile ayrilir - biri sabit
    /// harita verisi, digeri o anki oyun durumu.
    /// </summary>
    public sealed class DeliveryStop
    {
        private readonly List<Parcel> _parcels = new List<Parcel>();

        /// <summary>MapLayout'teki kimlik. Parcels.DestinationId ile eslesir.</summary>
        public int DestinationId { get; }

        public Vector3 Position { get; }

        public string DisplayName { get; }

        public IReadOnlyList<Parcel> Parcels => _parcels;

        /// <summary>Bu duraktaki TUM kargolar teslim edildi mi?</summary>
        public bool IsCompleted => _parcels.Count > 0 && _pending == 0;

        private int _pending;

        public int PendingCount => _pending;

        public DeliveryStop(int destinationId, Vector3 position, string displayName)
        {
            DestinationId = destinationId;
            Position = position;
            DisplayName = string.IsNullOrEmpty(displayName) ? "Adres" : displayName;
        }

        /// <summary>Duraga kargo ekler. Ayni kargo iki kez eklenemez.</summary>
        public void AddParcel(Parcel parcel)
        {
            if (parcel == null || _parcels.Contains(parcel)) return;

            _parcels.Add(parcel);
            _pending++;
        }

        /// <summary>Tek bir kargo teslim edildi.</summary>
        public void MarkParcelDelivered(Parcel parcel)
        {
            if (parcel == null) return;
            if (!_parcels.Contains(parcel)) return;

            _pending--;
        }

        /// <summary>Gun yeniden basladiginda duragi sifirlar.</summary>
        public void Reset()
        {
            _pending = _parcels.Count;
        }

        public override string ToString() =>
            DisplayName + " (" + _pending + "/" + _parcels.Count + ")";
    }
}
