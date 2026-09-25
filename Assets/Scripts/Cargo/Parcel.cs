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

        /// <summary>Teslim edildi. Artik listeden cikar (Aşama 4).</summary>
        Delivered = 2,
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
        /// Kargo araca YUKLENDIGI an (sistem saati, saniye).
        /// Teslim suresi buradan isler, gun basindan degil.
        /// </summary>
        public float TimeLoaded { get; private set; } = -1f;

        /// <summary>Araca yuklendi mi (yoksa teslim mi)?</summary>
        public bool IsLoaded => State == ParcelState.LoadedOnVehicle;

        /// <summary>Teslim edildi mi?</summary>
        public bool IsDelivered => State == ParcelState.Delivered;

        /// <summary>Hala oyunda mi (depo, araç veya teslim)?</summary>
        public bool IsActive => State != ParcelState.Delivered;

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
        public void MarkLoaded(float systemTime)
        {
            State = ParcelState.LoadedOnVehicle;
            TimeLoaded = systemTime;
        }

        /// <summary>Teslim edildi (Aşama 4). Artik araçta degildir.</summary>
        public void MarkDelivered() => State = ParcelState.Delivered;

        /// <summary>Depoya geri alindi (gun sonu bos arac vb.).</summary>
        public void MarkReturnedToDepot()
        {
            State = ParcelState.WaitingInDepot;
            TimeLoaded = -1f;
        }

        /// <summary>
        /// Teslim icin kalan sure (saniye). Negatifse sure dolmus demektir.
        /// Henuz yuklenmediyse tam sure verilir.
        /// </summary>
        public float RemainingSeconds(float systemTime)
        {
            if (!IsLoaded) return DeadlineSeconds;
            return DeadlineSeconds - (systemTime - TimeLoaded);
        }

        /// <summary>Sure doldu mu?</summary>
        public bool IsOverdue(float systemTime) => IsLoaded && RemainingSeconds(systemTime) <= 0f;

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
