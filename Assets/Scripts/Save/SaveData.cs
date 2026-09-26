using System;
using System.Collections.Generic;

namespace KargoSim.Save
{
    /// <summary>
    /// Diskteki kayit dosyasinin TAM icerigi (Asama 7).
    ///
    /// Duz bir veri tasiyicidir: hicbir kural, hesap veya sistem bilgisi icermez.
    /// Boylece dosya semasi okunabilir kalir ve "simdi hangi alanlar var?"
    /// sorusu tek bir yere bakilarak cevaplanir.
    ///
    /// KURALLAR:
    ///   - JsonUtility ile serilestirilecegi icin ALANLAR public ve [Serializable].
    ///   - DETERMINISTIK: dictionary yok, DateTime alani yok. Tarih bilgisi
    ///     yalnizca <see cref="savedAtIso"/> metni olarak tutulur.
    ///   - Oyun durumu BURAYA degil, oyunun kendi sistemlerine yazilir; bu
    ///     sinif sadece o durumun bir kopyasidir.
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        /// <summary>Dosya semasi surumu. SaveSystem.CurrentVersion ile karsilastirilir.</summary>
        public int version = 1;

        /// <summary>Kayit anindaki oyun gunu (1'den baslar).</summary>
        public int day = 1;

        /// <summary>Oyuncunun bakiyesi (TL).</summary>
        public int money;

        /// <summary>Toplam XP.</summary>
        public int xp;

        /// <summary>Su anki seviye.</summary>
        public int level = 1;

        /// <summary>Gun icindeki saat (0-24). Gunun neresinde kaldigimizi belirtir.</summary>
        public float hourOfDay = 8f;

        /// <summary>
        /// Siradaki kargo kimligi. Yuklemeden sonra yeni uretilecek kargolar
        /// buradan devam eder; boyce kimlik CAKISMAZ.
        /// </summary>
        public int nextParcelId = 1;

        /// <summary>Kayit anindaki tum kargolar (depo + arac).</summary>
        public List<SavedParcel> parcels = new List<SavedParcel>();

        /// <summary>Kaydin yazildigi an (ISO-8601 metin). Bilgi amacli, mantikta kullanilmaz.</summary>
        public string savedAtIso = string.Empty;

        /// <summary>Kargo sayisi. <see cref="parcels"/> null ise 0.</summary>
        public int ParcelCount => parcels != null ? parcels.Count : 0;

        /// <summary>
        /// Listedeki en buyuk kargo kimligi (liste bossa 0).
        ///
        /// nextParcelId bu degerden kucukse cakisma olur: yukleme sonrasi uretilen
        /// yeni kargo, kayittaki bir kargonun kimligini taklit eder ve
        /// FindById() yanlis kargoyu dondurur. SaveSystem bu degeri kontrol eder.
        /// </summary>
        public int MaxParcelId
        {
            get
            {
                int max = 0;
                if (parcels == null) return max;

                for (int i = 0; i < parcels.Count; i++)
                {
                    SavedParcel parcel = parcels[i];
                    if (parcel != null && parcel.id > max) max = parcel.id;
                }

                return max;
            }
        }
    }

    /// <summary>
    /// Tek bir kargonun kayit hali.
    ///
    /// Parcel nesnesi buraya DOKUNULMAZ kopyalanir: <see cref="Cargo.Parcel"/>
    /// saf C# ama ic durumu (State, TimeLoaded) var ve JsonUtility onu
    /// geri yukleyemez. Bu yuzden Restore() kargolari yeniden URETIR.
    /// </summary>
    [Serializable]
    public sealed class SavedParcel
    {
        /// <summary>Kayit anindaki kargo kimligi.</summary>
        public int id;

        /// <summary>
        /// Kargo turunun <c>ParcelType[]</c> dizisindeki sirasi.
        ///
        /// Neden nesne degil sira? ScriptableObject referansi JSON'da anlamsiz
        /// olur (dosya yolu platforma gore degisir). Sira ise platformdan
        /// bagimsiz ve insan tarafindan okunabilir.
        /// -1: tur bulunamadi (kayit, listede olmayan bir turu gosteriyor).
        /// </summary>
        public int typeIndex = -1;

        /// <summary>Alici adi.</summary>
        public string recipientName = string.Empty;

        /// <summary>Hedef adres kimligi (MapLayout.deliveryPoints ile eslesir).</summary>
        public int destinationId;

        /// <summary>Agirlik (kg).</summary>
        public float weightKg;

        /// <summary>Hacim (litre).</summary>
        public float volumeLiters;

        /// <summary>Oncelik degeri (<c>ParcelPriority</c> enumunun tam sayi karsiligi).</summary>
        public int priority;

        /// <summary>Teslim icin kalan sure (saniye).</summary>
        public float deadlineSeconds;

        /// <summary>
        /// Kargonun durumu (<c>ParcelState</c> enumunun tam sayi karsiligi).
        /// Bilinmeyen bir deger gelirse Restore() depona alir.
        /// </summary>
        public int state;
    }
}
