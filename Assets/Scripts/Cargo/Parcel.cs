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

        /// <summary>
        /// KALICI olarak oyundan cikarildi (gun sonu basarisiz sayimi).
        /// Depoya geri donmez: ne yuklenebilir ne de teslim edilebilir.
        /// </summary>
        Discarded = 3,

        // --- Asama 12: Paketleme ---
        //
        // DIKKAT: asagidaki degerler SIRA SONUNA eklendi ve bir daha
        // degistirilmeyecek. Save dosyalari durumu duz sayi olarak yaziyor
        // (SaveSystem: state = (int)parcel.State). Ortaya bir deger eklense
        // yeni kod eski kaydi "bilinmeyen" diye reddeder ve oyuncunun ilerlemesi
        // kaybolurdu.

        /// <summary>
        /// Paketleme masasi basinda, arada bir adimda.
        /// </summary>
        Packaging = 4,

        /// <summary>
        /// Paketlendi ve teslimata hazir. Araca yuklenebilir.
        /// </summary>
        Ready = 5,
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

        // ------------------------------------------------------------------
        // Paketleme (Asama 12)
        // ------------------------------------------------------------------

        /// <summary>
        /// Paketlemede kaldigi adim. Yeni kargo <see cref="PackagingStep.None"/> ile baslar.
        /// </summary>
        public PackagingStep PackagingProgress { get; private set; } = PackagingStep.None;

        /// <summary>
        /// Paketlemeye baslandigi an (sistem saati, saniye). -1 = baslanmadi.
        ///
        /// Neden tutuluyor: oyuncu masada birakip giderse "bu kutu ne kadar
        /// sureydi duruyor" bilgisi kaybolurdu. Suresi dolan is asilir.
        /// </summary>
        public float PackagingStartedTime { get; private set; } = -1f;

        /// <summary>Paket teslimata hazir mi?</summary>
        public bool IsPackaged => PackagingSteps.IsReady(PackagingProgress);

        /// <summary>Paketleme masasi basinda mi?</summary>
        public bool IsBeingPackaged => State == ParcelState.Packaging;

        /// <summary>Araca yuklenebilir mi? (paketlenmisse)</summary>
        public bool IsLoadable => State == ParcelState.Ready;

        /// <summary>0..1 arasi paketleme ilerlemesi.</summary>
        public float PackagingProgress01 => PackagingSteps.Progress01(PackagingProgress);

        // ------------------------------------------------------------------
        // Teslimat kodu (Asama 13)
        // ------------------------------------------------------------------

        /// <summary>
        /// Kapida dogrulanacak teslimat kodu.
        ///
        /// KAYITTA SAKLANMAZ. <see cref="Cargo.DeliveryCode.Generate"/> ile
        /// kargo kimligi, gun ve tur indeksinden TURETILIR. Boylece
        /// SaveData'ya yeni alan eklemeye gerek kalmaz ve eski kayitlar
        /// bozuk kodla donmez.
        /// </summary>
        public int DeliveryCode => Cargo.DeliveryCode.Generate(Id, CodeDay, TypeIndex);

        /// <summary>Kodon hangi gun icin uretilecegi.</summary>
        public int CodeDay { get; private set; } = 1;

        /// <summary>
        /// Kargo turunun indeksi. Kod turetimine karisir; ayni kargo
        /// kimligi farkli turde farkli kod verir.
        /// </summary>
        private int TypeIndex { get; set; }

        /// <summary>Kodu gosterilebilir metin ("4821").</summary>
        public string DeliveryCodeText => Cargo.DeliveryCode.Format(DeliveryCode);

        /// <summary>
        /// Kargoyu belli bir gune baglar. Gun degistiginde kod degisir.
        /// <see cref="CargoSystem.BeginDay"/> cagirir.
        /// </summary>
        public void AssignCodeDay(int day)
        {
            CodeDay = day < 1 ? 1 : day;
        }

        /// <summary>Tur indeksini atar (kayit/uretim aninda bir kez).</summary>
        public void AssignTypeIndex(int index)
        {
            TypeIndex = index < 0 ? 0 : index;
        }

        /// <summary>
        /// Paketlemeye baslandi. Zaten baslanmistiysa hicbir sey olmaz
        /// (cift cagri ikinci kez sureyi sifirlamamali).
        /// </summary>
        public void MarkPackagingStarted(float systemTime)
        {
            if (PackagingProgress != PackagingStep.None) return;

            State = ParcelState.Packaging;
            PackagingStartedTime = systemTime;
        }

        /// <summary>
        /// Bir adim ilerlet. Sadece SIRADAKI adima gecilebilir; atlayarak
        /// ilerleme kurali PackagingSystem'de engellenir.
        /// </summary>
        /// <returns>Adim ilerlediyse true, son adimdaysa false.</returns>
        public bool AdvancePackaging()
        {
            if (PackagingProgress == PackagingStep.None)
            {
                State = ParcelState.Packaging;
                if (PackagingStartedTime < 0f) PackagingStartedTime = 0f;
            }

            if (PackagingSteps.IsReady(PackagingProgress)) return false;

            PackagingProgress = PackagingSteps.Next(PackagingProgress);

            if (PackagingSteps.IsReady(PackagingProgress)) MarkPackagingFinished();
            return true;
        }

        /// <summary>Paket tamamlandi: durum Ready olur.</summary>
        private void MarkPackagingFinished()
        {
            State = ParcelState.Ready;
        }

        /// <summary>
        /// Paketlemeyi bastan baslat (belki masayi temizledik).
        /// </summary>
        public void ResetPackaging()
        {
            PackagingProgress = PackagingStep.None;
            PackagingStartedTime = -1f;
            if (State == ParcelState.Packaging || State == ParcelState.Ready)
                State = ParcelState.WaitingInDepot;
        }

        /// <summary>
        /// Hala oyunda mi (depo, arac veya teslim)?
        ///
        /// DUZELTME (Discarded): Once yalnizca "Delivered" kontrol ediliyordu.
        /// Gun sonunda basarisiz sayilan kargo depoya donmedigi icin bu
        /// degiskene DEPO kargosu gibi gorunuyordu: oyuncu onu tekrar yukleyip
        /// ayni gun ikinci kez teslim edebiliyor, odeme ve XP iki kez
        /// yaziliyordu. Discarded ayrica "aktif" sayilmaz.
        /// </summary>
        public bool IsActive => State != ParcelState.Delivered && State != ParcelState.Discarded;

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
        /// <remarks>
        /// DIKKAT: paketlenmis kargo depoya donerken <c>Ready</c> durumunu
        /// KORUR. Aksi halde oyuncu aracindan bosalttigi bir paketi tekrar
        /// yukleyemezdi: kartonu kesip bantlamayi yeniden yapmak zorunda
        /// kalirdi, oysa o is bir kez yapildi. Paketleme isinin izi kargoda
        /// kalir.
        /// </remarks>
        public void MarkReturnedToDepot()
        {
            State = IsPackaged ? ParcelState.Ready : ParcelState.WaitingInDepot;
            TimeLoaded = -1f;
        }

        /// <summary>
        /// KALICI olarak oyundan cikarildi. Depoya GERI DONMEZ.
        ///
        /// DUZELTME (Hata 2): "Q ile bosalt" icin MarkReturnedToDepot yeterli
        /// degildi; ayni isaretleme gun sonu basarisiz sayiminda da kullanilirsa
        /// kargo depoda kalir ve oyuncu onu tekrar yukleyip teslim edebilir.
        /// Iki farkli yon var, bu yuzden iki farkli isaretleme olmali:
        ///   UnloadAll()      -> depoya doner   (oyuncu iade ediyor)
        ///   DiscardLoaded()  -> kalici kaybolur (basarisiz sayim)
        ///
        /// Bu metod Manifest tarafindan cagrilir. TimeLoaded sifirlanmaz:
        /// geri sayim bu andan itibaren islemeye devam etmemeli.
        /// </summary>
        public void MarkDiscarded()
        {
            State = ParcelState.Discarded;
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
