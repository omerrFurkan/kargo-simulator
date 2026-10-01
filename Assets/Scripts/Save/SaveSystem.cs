using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using KargoSim.Cargo;
using KargoSim.Core;
using KargoSim.Delivery;
using KargoSim.Player;
using UnityEngine;

namespace KargoSim.Save
{
    /// <summary>
    /// Restore() cagrisinin ciktisi. Geri yuklenen gun/saat ve sayac
    /// cagrana dondurulur: PlayerProgress gun tutmaz, GameClock da oyuncudan
    /// ayri bir nesnedir, dolayisiyla bunlari veri olarak geri vermek zorundayiz.
    /// </summary>
    public readonly struct RestoreResult
    {
        /// <summary>Yukleme yapildi mi?</summary>
        public bool Success { get; }

        /// <summary>Kayittaki gun (cagiran GameClock'u buna gore ayarlar).</summary>
        public int Day { get; }

        /// <summary>Kayittaki saat 0-24.</summary>
        public float HourOfDay { get; }

        /// <summary>
        /// Siradaki kargo kimligi. listedeki en buyuk id'den DAHA buyuktur,
        /// yeni uretilecek kargo bu degeri kullanmali.
        /// </summary>
        public int NextParcelId { get; }

        /// <summary>Depoya/araca geri konulan kargo sayisi.</summary>
        public int RestoredCount { get; }

        /// <summary>
        /// Geri yuklenemeyen kayit girdisi (null, teslim edilmis, kalici atilmis).
        /// Sessizce yutulmaz: sayac cagrana bildirilir.
        /// </summary>
        public int SkippedCount { get; }

        public RestoreResult(bool success, int day, float hourOfDay, int nextParcelId,
                             int restoredCount, int skippedCount)
        {
            Success = success;
            Day = day;
            HourOfDay = hourOfDay;
            NextParcelId = nextParcelId;
            RestoredCount = restoredCount;
            SkippedCount = skippedCount;
        }

        /// <summary>Yuklenemedi durumu (null veri).</summary>
        public static RestoreResult Failed =>
            new RestoreResult(false, 1, 0f, 1, 0, 0);
    }

    /// <summary>
    /// Kaydet / yukle (Asama 7).
    ///
    /// TEK SORUMLULUK: oyunun duz bir JSON kopyasini diske yazmak ve geri
    /// okumak. Karar verme, zamanlama ve UI burada degil; oyun akisi
    /// (gun sonunda kaydet, "Devam Et" tiklaninca yukle) GameCompositionRoot'ta.
    ///
    /// TASARIM KARARLARI:
    ///   - JSON + JsonUtility: insan okunabilir, difflenebilir, elle duzeltilebilir.
    ///     XML/binary/shader gibi gizli bicim yok.
    ///   - ATOMIK YAZMA: once .tmp yazilir, sonra yerine tasinir. Yazma
    ///     yarida kesilirse (uygulama kapandi, cift tiklandi) eski kayit saglam kalir.
    ///   - SURUM KONTROLU: dosya surumu oyundan buyukse reddedilir. Daha yeni
    ///     bir surumden gelen kayit eski oyunda bozuk anlam tasiyabilir.
    ///   - Kargolar JSON'dan NESNE olarak okunmaz, yeniden uretilir
    ///     (Restore). Parcel saf C# ama ic durumu var; ayrica ParcelType gibi
    ///     ScriptableObject referanslari JSON'da anlamsizdir.
    ///
    /// MonoBehaviour DEGIL: statik yardimcilar, test edilebilir.
    /// </summary>
    public static class SaveSystem
    {
        /// <summary>Kayit dosyasinin adi (persistentDataPath icinde).</summary>
        public const string FileName = "kargosave.json";

        /// <summary>Atomik yazma sirasinda kullanilan gecici dosya eki.</summary>
        public const string TempSuffix = ".tmp";

        /// <summary>
        /// Bu kodun anlayacagi kayit semasi surumu.
        /// Yeni alan eklendiginde artirilir; artirmak "eski kayitlar okunur"
        /// anlamina gelir, okunamaz hale getirmek icin ayri bir bayrak gerekir.
        /// </summary>
        public const int CurrentVersion = 1;

        /// <summary>Kayit dosyasinin tam yolu.</summary>
        public static string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>Yazma sirasinda kullanilan gecici dosyanin tam yolu.</summary>
        public static string TempPath => SavePath + TempSuffix;

        /// <summary>Kayit dosyasi var mi?</summary>
        public static bool Exists() => File.Exists(SavePath);

        /// <summary>
        /// Kaydi (ve yarim kalmis gecici dosyayi) siler.
        /// "Yeni oyun" ve testler bunu kullanir. Dosya yoksa sessiz gecer.
        /// </summary>
        public static void Delete()
        {
            try
            {
                if (File.Exists(SavePath)) File.Delete(SavePath);
                if (File.Exists(TempPath)) File.Delete(TempPath);
            }
            catch (Exception e)
            {
                // Silme basarisiz olsa bile oyun devam edebilmeli; sadece bildir.
                Debug.LogWarning("Kayit silinemedi: " + e.Message);
            }
        }

        /// <summary>
        /// Kaydi yazar. Atomik: once .tmp, sonra yerine tasima.
        /// </summary>
        /// <returns>Basariliysa true, yazma hatasi varsa false.</returns>
        public static bool TrySave(SaveData data)
        {
            if (data == null)
            {
                Debug.LogError("Kayit yapilamadi: veri null.");
                return false;
            }

            if (data.parcels == null) data.parcels = new List<SavedParcel>();

            // Elle kurulmus (new SaveData()) bir veride surum 0 gelir; onu
            // damgala. Ancak DOSYADAN okunmus veya testte ileri surume
            // zorlanmis bir degeri sessizce dusurME: boylece surum kontrolu
            // calisir ve hatayi gorunur kilabiliriz.
            if (data.version <= 0) data.version = CurrentVersion;

            data.savedAtIso = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            SanitizeNextParcelId(data);

            string json;
            try
            {
                json = JsonUtility.ToJson(data, true);
            }
            catch (Exception e)
            {
                Debug.LogError("Kayit serilestirilemedi: " + e.Message);
                return false;
            }

            try
            {
                WriteAtomic(json);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError("Kayit yazilamadi (" + SavePath + "): " + e.Message);

                // Yarim kalmis gecici dosya bir sonraki yazmayi bozmamali.
                TryDeleteTemp();
                return false;
            }
        }

        /// <summary>
        /// Kaydi okur. Surum kontrolu burada yapilir.
        /// </summary>
        /// <returns>
        /// Gecerli kayit, ya da yuklenemiyorsa <c>null</c>
        /// (dosya yok, okunamaz, bozuk veya ileri surumlu).
        /// </returns>
        public static SaveData Load()
        {
            if (!File.Exists(SavePath))
            {
                // Ilk calistirma: bu bir hata degil, normal durum.
                Debug.Log("Kayit dosyasi bulunamadi, yeni oyun basliyor: " + SavePath);
                return null;
            }

            string json;
            try
            {
                json = File.ReadAllText(SavePath);
            }
            catch (Exception e)
            {
                Debug.LogError("Kayit okunamadi (" + SavePath + "): " + e.Message);
                return null;
            }

            if (string.IsNullOrEmpty(json) || json.Trim().Length == 0)
            {
                Debug.LogError("Kayit dosyasi bos (" + SavePath + ")");
                return null;
            }

            SaveData data;
            try
            {
                data = JsonUtility.FromJson<SaveData>(json);
            }
            catch (Exception e)
            {
                Debug.LogError("Kayit dosyasi bozuk, okunamadi (" + SavePath + "): " + e.Message);
                return null;
            }

            if (data == null)
            {
                Debug.LogError("Kayit dosyasi bozuk: icerik null (" + SavePath + ")");
                return null;
            }

            // SURUM KONTROLU: ileri surumden olusturulmus kayit, bu oyunda
            // farkli anlam tasiyan alanlar icerebilir. Sessizce yuklemek
            // oyuncunun parasini/kargosunu bozabilir; reddet ve bildir.
            if (data.version > CurrentVersion)
            {
                Debug.LogError("Kayit surumu desteklenmiyor: dosya surumu " + data.version +
                               ", oyunun anladigi surum " + CurrentVersion +
                               ". Kayit daha yeni bir oyun surumunden olusmus olabilir.");
                return null;
            }

            // Eski surumlerde eksik alanlar null gelebilir; yazma tarafinda
            // hep liste var, okuma tarafinda garanti etmeliyiz.
            if (data.parcels == null) data.parcels = new List<SavedParcel>();

            // Bozuk/elle duzenlenmis sayaci duzelt: kimlik cakismasi olmasin.
            SanitizeNextParcelId(data);

            return data;
        }

        // ------------------------------------------------------------------
        // Canli oyun durumu -> SaveData
        // ------------------------------------------------------------------

        /// <summary>
        /// Kargolar DEPO + ARAC listesinin birlesimi olarak verilir; SaveSystem
        /// nereden geldiklerini bilmez, sadece durumlarini okur.
        /// </summary>
        /// <param name="types">
        /// Kargo turlerinin dizisi. <c>typeIndex</c> bu dizideki sirasi olur;
        /// oyunda uretim icin kullanilan dizinin AYNIsi olmalidir.
        /// </param>
        /// <param name="nextId">Siradaki kargo kimligi (cakisma olmasin diye dogrulanir).</param>
        public static SaveData Capture(int day, int money, int xp, int level, float hourOfDay,
                                       IReadOnlyList<Parcel> parcels, ParcelType[] types,
                                       int nextId)
        {
            var data = new SaveData
            {
                version = CurrentVersion,
                day = Mathf.Max(1, day),
                money = Mathf.Max(0, money),
                xp = Mathf.Max(0, xp),
                level = Mathf.Max(1, level),
                hourOfDay = Mathf.Clamp(hourOfDay, 0f, GameClock.HoursPerDay),
                nextParcelId = nextId,
                savedAtIso = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                parcels = new List<SavedParcel>(parcels != null ? parcels.Count : 0)
            };

            if (parcels != null)
            {
                for (int i = 0; i < parcels.Count; i++)
                {
                    Parcel parcel = parcels[i];
                    if (parcel == null) continue;

                    data.parcels.Add(CaptureParcel(parcel, types));
                }
            }

            SanitizeNextParcelId(data);
            return data;
        }

        private static SavedParcel CaptureParcel(Parcel parcel, ParcelType[] types)
        {
            int typeIndex = IndexOfType(types, parcel.Type);

            // -1 kaydedilir: kargo turu listede yoksa dosya yine de yazilir,
            // oyuncu parasini kaybetmez. Yuklemede varsayilan ture donusulur.
            if (typeIndex < 0)
            {
                Debug.LogWarning("Kargo #" + parcel.Id + " kaydedilirken turu listede " +
                                 "bulunamadi (typeIndex=-1). Yuklemede varsayilan tur kullanilacak.");
            }

            return new SavedParcel
            {
                id = parcel.Id,
                typeIndex = typeIndex,
                recipientName = parcel.RecipientName ?? string.Empty,
                destinationId = parcel.DestinationId,
                weightKg = parcel.WeightKg,
                volumeLiters = parcel.VolumeLiters,
                priority = (int)parcel.Priority,
                deadlineSeconds = parcel.DeadlineSeconds,
                state = (int)parcel.State
            };
        }

        private static int IndexOfType(ParcelType[] types, ParcelType type)
        {
            if (types == null || type == null) return -1;

            // ReferenceEquals: ScriptableObject'un kimligi budur. Unity'de
            // "==" overload'i yazilmis nesnelerde identity kararidir ama
            // burada acik yazmak daha durust.
            for (int i = 0; i < types.Length; i++)
                if (ReferenceEquals(types[i], type)) return i;

            return -1;
        }

        // ------------------------------------------------------------------
        // SaveData -> canli oyun durumu
        // ------------------------------------------------------------------

        /// <summary>
        /// Kayittan oyun durumunu geri kurar.
        ///
        /// Kargolar JSON'dan NESNE olarak okunmaz, yeniden uretilir: Parcel
        /// saf C# bir sinif ama ic durumu (State, TimeLoaded) ve tip belirteci
        /// var; JsonUtility bunlari geri yukleyemez.
        /// </summary>
        /// <param name="progress">Para/XP/seviye geri yuklenecek nesne. null olabilir.</param>
        /// <param name="cargo">
        /// Kargolarin yazilacagi sistem. null ise yalnizca ilerleme geri yuklenir
        /// (testler ve "sadece ayarlar" senaryolari icin).
        /// </param>
        /// <param name="types">Kayitta saklanan <c>typeIndex</c> degerlerinin cozulecegi dizi.</param>
        /// <param name="systemTime">
        /// Simulasyon saati (saniye). Aracdaki kargolarin teslim geri sayimi
        /// BU andan itibaren islemeye devam etmeli; 0f gecmek tum yuklemeleri
        /// "gun basindan sayar" hale getirir (Asama 3'teki Hata 1).
        /// </param>
        /// <param name="layout">
        /// Istege bagli harita. Varsa hedef adresler dogrulanir; olmayan adrese
        /// bagli kargo SILINMEZ (veri kaybi olur) sadece uyarilir.
        /// </param>
        public static RestoreResult Restore(SaveData data, PlayerProgress progress,
                                            CargoSystem cargo, ParcelType[] types,
                                            float systemTime = 0f, MapLayout layout = null)
        {
            if (data == null)
            {
                Debug.LogError("Yukleme yapilamadi: kayit verisi null.");
                return RestoreResult.Failed;
            }

            // Bozuk sayaci burada da duzelt: Restore'a elle kurulmus veri
            // gecirilse bile kimlik cakismasi olusmasin.
            SanitizeNextParcelId(data);

            // Elle kurulmus SaveData'da liste null gelebilir.
            if (data.parcels == null) data.parcels = new List<SavedParcel>();

            if (progress != null)
                progress.RestoreFromSave(data.money, data.xp, data.level);

            if (cargo == null)
            {
                return new RestoreResult(true, data.day, data.hourOfDay,
                                         data.nextParcelId, 0, data.ParcelCount);
            }

            if (types == null || types.Length == 0)
            {
                // Tur listesi olmadan kargo yeniden uretilemez: agirlik/hacim
                // ve deger hesaplari turden okur.
                Debug.LogError("Yukleme yapilamadi: ParcelType listesi bos, " +
                               "kargolar yeniden uretilemiyor.");
                return new RestoreResult(false, data.day, data.hourOfDay,
                                         data.nextParcelId, 0, data.ParcelCount);
            }

            List<SavedParcel> saved = data.parcels;
            var depot = new List<Parcel>(saved.Count);

            // Aracta OLANLAR ayri listede tutulur.
            //
            // Neden: new Parcel(...) HER ZAMAN WaitingInDepot ile baslar.
            // Kayittan okunan durumu bir degiskende tutup sonra
            // parcel.State'e bakmak calismazdi: hicbir kargo "yuklendi"
            // gorunmedigi icin hicbiri araca donmuyordu ve oyuncunun
            // aracindaki tum kargolar kaybolup depoya duserdi.
            var toLoad = new List<Parcel>();
            int skipped = 0;
            int unknownType = 0;

            for (int i = 0; i < saved.Count; i++)
            {
                SavedParcel record = saved[i];
                if (record == null) { skipped++; continue; }

                ParcelState state = ReadState(record.state);

                // Teslim edilmis / kalici atilmis kargo listede bulunmamalidir.
                // Depoya eklersek oyuncu ayni kargoyu ikinci kez yukleyip
                // TESLIM EDEBILIR: odeme iki kez yazilir. Bu yuzden atlanir.
                if (state == ParcelState.Delivered || state == ParcelState.Cancelled)
                {
                    skipped++;
                    continue;
                }

                ParcelType type = ReadType(types, record.typeIndex, ref unknownType);
                ParcelPriority priority = ReadPriority(record.priority);

                  var parcel = new Parcel(
                      record.id, type, record.recipientName, record.destinationId,
                      record.weightKg, record.volumeLiters, priority,
                      record.deadlineSeconds,
                      ComputeBaseValue(type, record.weightKg, priority));

                  // Teslimat kodu kayit dosyasinda SAKLANMAZ; kargo
                  // kimligi + tur indeksi + gunden TURETILIR. Burada
                  // tur indeksini atamazsak kod tum yuklenen kayitlarda
                  // 0'a duser ve "eski kayitlar farkli kod uretir" hale
                  // gelir. Gun atamasi da ayni sebeple: oyuncunun kaldigi
                  // gunun kodu uretilmeli.
                  parcel.AssignTypeIndex(record.typeIndex);
                  parcel.AssignCodeDay(data.day);

                if (layout != null && !layout.Find(record.destinationId).HasValue)
                {
                    Debug.LogWarning("Kargo #" + record.id + " kayitli adres (" +
                                     record.destinationId + ") haritada yok. " +
                                     "Kargo depoda birakildi.");
                }

                depot.Add(parcel);

                if (state == ParcelState.LoadedOnVehicle) toLoad.Add(parcel);
            }

            // Once hepsini depoya yaz, sonra aracdakileri geri yukle.
            // Tersi yapilsa yukleme kapasitesi depoyu bos gosterirdi.
            cargo.Manifest.BeginDay(depot);

            for (int i = 0; i < toLoad.Count; i++)
            {
                Parcel parcel = toLoad[i];

                if (cargo.Manifest.TryLoad(parcel, systemTime)) continue;

                // Kapasite degismis olabilir (kucuk arac, aracsiz oturum).
                // Kargoyu kaybetmek yerine depoya al ve oyuncuyu uyar.
                Debug.LogWarning("Kargo #" + parcel.Id + " arac kapasitesine sigmiyor, " +
                                 "depoya alindi. Kargo kaybolmadi, elle yukleyebilirsin.");
            }

            if (unknownType > 0)
            {
                Debug.LogWarning(unknownType + " kargonun turu kayitta gecersiz, " +
                                 "varsayilan tur atandi.");
            }

            // Depo ve arac tamamen degisti: UI'in yeniden cizilmesi gerekir.
            GameEvents.RaiseCargoChanged();

            return new RestoreResult(true, data.day, data.hourOfDay,
                                     data.nextParcelId, depot.Count, skipped);
        }

        /// <summary>Kayit durum degerini guvenli okur.</summary>
        private static ParcelState ReadState(int raw)
        {
            if (!Enum.IsDefined(typeof(ParcelState), raw))
            {
                // Dosya elle duzenlmis ya da yeni surumde yeni bir durum
                // eklenmis olabilir. Kargo depoya girsin; oyuncu isterse
                // kendi karariyla yukler.
                Debug.LogWarning("Bilinmeyen kargo durumu (" + raw + ") bulundu, " +
                                 "kargo depoya alindi (WaitingInDepot).");
                return ParcelState.WaitingInDepot;
            }

            return (ParcelState)raw;
        }

        private static ParcelPriority ReadPriority(int raw)
        {
            if (!Enum.IsDefined(typeof(ParcelPriority), raw))
            {
                Debug.LogWarning("Bilinmeyen oncelik degeri (" + raw + ") bulundu, " +
                                 "Normal varsayildi.");
                return ParcelPriority.Normal;
            }

            return (ParcelPriority)raw;
        }

        private static ParcelType ReadType(ParcelType[] types, int index, ref int unknownCount)
        {
            // Gecerli bir aralikta VE bos olmayan bir tur: kullan.
            if (index >= 0 && index < types.Length && types[index] != null)
                return types[index];

            // Bilinmeyen: listedeki ILK dolu ture duser. types[0] her zaman
            // var olmak zorunda DEGIL, o yuzden tek tek bakilir.
            unknownCount++;
            for (int i = 0; i < types.Length; i++)
                if (types[i] != null) return types[i];

            return null;
        }

        /// <summary>
        /// Kargo degerini yeniden hesaplar.
        ///
        /// ParcelGenerator ile BIREBIR ayni formul kullanilir; boylece kayit
        /// yuklendikten sonra bir kargonun odemesi uretim anindakiyle ayni olur
        /// (baseValue dosyada tutulmaz, agirligi ve turu yeter).
        /// </summary>
        private static int ComputeBaseValue(ParcelType type, float weightKg,
                                            ParcelPriority priority)
        {
            if (type == null) return 0;

            return (int)Math.Round(
                type.paymentPerKg * weightKg * priority.PaymentMultiplier(),
                MidpointRounding.AwayFromZero);
        }

        // ------------------------------------------------------------------
        // Ic yardimcilar
        // ------------------------------------------------------------------

        /// <summary>
        /// nextParcelId'yi listedeki en buyuk id'den buyuk olacak sekilde
        /// duzeltir. Bozuk veya elle yazilmis bir sayac, yuklemeden sonra yeni
        /// uretilen kargoyla cakisir ve FindById() yanlis kargoyu dondurur.
        /// </summary>
        private static int SanitizeNextParcelId(SaveData data)
        {
            int maxId = data.MaxParcelId;

            if (data.nextParcelId <= maxId) data.nextParcelId = maxId + 1;
            if (data.nextParcelId < 1) data.nextParcelId = 1;

            return data.nextParcelId;
        }

        /// <summary>
        /// Once gecici dosyaya yazar, sonra yerine tasir.
        ///
        /// Boylece yazma yarida kesilirse (cihaz kapandi, uygulama oldu)
        /// yazilmamis kayit eskisinin yerine gecmez: ya eski kayit durur ya
        /// hicbir kayit olmaz. Iki durumda da oyuncu parasini kaybetmez.
        /// </summary>
        private static void WriteAtomic(string json)
        {
            string directory = Path.GetDirectoryName(SavePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            // BOM'sUZ UTF8: JsonUtility dosya basindaki BOM'a takilabilir.
            File.WriteAllText(TempPath, json, new UTF8Encoding(false));

            if (File.Exists(SavePath))
            {
                try
                {
                    // Atomik degistirme: hedef anlik degistirilir.
                    File.Replace(TempPath, SavePath, null);
                    return;
                }
                catch (PlatformNotSupportedException)
                {
                    // Bazi platformlarda Replace yok (orn. WebGL): normal tasima.
                }
                catch (IOException)
                {
                    // Dosya kilitliyse ya da dosya sistemi desteklemiyorsa.
                }

                File.Delete(SavePath);
            }

            File.Move(TempPath, SavePath);
        }

        private static void TryDeleteTemp()
        {
            try
            {
                if (File.Exists(TempPath)) File.Delete(TempPath);
            }
            catch (Exception)
            {
                // Gecici dosya silinemiyorsa bir sonraki yazma zaten uzerine
                // yazacak; oyuncuyu bu ikincil hatla rahatsiz etmeyelim.
            }
        }
    }
}
