using System;

namespace KargoSim.Cargo
{
    /// <summary>
    /// Kargonun icinde bulundugu yer.
    /// </summary>
    public enum ParcelState
    {
        /// <summary>Depoda, teslim edilmedi. Oyuncu inceleyip yukleyebilir.</summary>
        WaitingInDepot = 0,

        /// <summary>Araca yuklendi. Teslimat bekliyor (Aşama 4).</summary>
        LoadedOnVehicle = 1,
    }

    /// <summary>
    /// Tek bir kargo. SAF C# sinifi (MonoBehaviour degil) - test edilebilir
    /// ve havuzlanabilir olsun diye.
    ///
    /// "Nereye" sorusu burada YALNIZCA bir tamsayidir. Gercek adres/koordinat
    /// Aşama 4'te (teslimat) eklenir; simdilik kargo sistemi haritayi bilmez.
    /// </summary>
    public sealed class Parcel
    {
        /// <summary>Gun icinde benzersiz. Save/Load icin de kullanilacak.</summary>
        public int Id { get; }

        public ParcelType Type { get; }

        public string RecipientName { get; }

        /// <summary>Hedef adres kimligi. Aşama 4'te koordinata cozumlenir.</summary>
        public int DestinationId { get; }

        public float WeightKg { get; }

        public float VolumeLiters { get; }

        public ParcelPriority Priority { get; }

        /// <summary>Teslim icin kalan sure (saniye). Gun saatine gore azalir.</summary>
        public float DeadlineSeconds { get; }

        public bool Fragile => Type != null && Type.fragile;

        public ParcelState State { get; private set; }

        /// <summary>
        /// Taban deger (TL). Oncelik carpani DAHIL DEGIL: odeme hesabini
        /// ekonomi sistemi yapar (Aşama 5). Burada sadece kargonun kendi
        /// degeri duruyor, boylece iki yer hesap yapmaz.
        /// </summary>
        public int BaseValue { get; }

        public Parcel(int id, ParcelType type, string recipientName, int destinationId,
                      float weightKg, float volumeLiters, ParcelPriority priority,
                      float deadlineSeconds, int baseValue)
        {
            Id = id;
            Type = type;
            RecipientName = recipientName;
            DestinationId = destinationId;
            WeightKg = weightKg;
            VolumeLiters = volumeLiters;
            Priority = priority;
            DeadlineSeconds = deadlineSeconds;
            BaseValue = baseValue;
            State = ParcelState.WaitingInDepot;
        }

        /// <summary>Araca yuklendi. Manifest tarafindan cagrilir.</summary>
        public void MarkLoaded() => State = ParcelState.LoadedOnVehicle;

        /// <summary>Depoya geri alindi (gun sonu bos arac vb.).</summary>
        public void MarkReturnedToDepot() => State = ParcelState.WaitingInDepot;

        /// <summary>
        /// Kisa, okunur etiket. UI'da kullanilir; tek yerde tutulur ki
        /// her ekran ayni metni gosterir.
        /// </summary>
        public string Label
        {
            get
            {
                string name = string.IsNullOrEmpty(RecipientName) ? "Bilinmeyen" : RecipientName;
                return $"#{Id} {name} - {Type?.displayName ?? "Kargo"}";
            }
        }

        public override string ToString() => Label;
    }
}
