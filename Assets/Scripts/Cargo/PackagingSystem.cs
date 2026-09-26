using System;
using System.Collections.Generic;
using KargoSim.Core;

namespace KargoSim.Cargo
{
    /// <summary>
    /// Paketleme adimlarini yoneten saf C# sinif.
    ///
    /// MONOBEHAVIOUR DEGIL: masadaki buton, adimi ilerletir; kurallar
    /// buradadir. Boylece "hangi adimdan hangisine gecilebilir" sorusu
    /// sahne olmadan test edilebilir.
    ///
    /// Kurallar:
    ///   - Adimlar SIRALI ilerler, atlanamaz.
    ///   - Her adimin bir suresi var; oyuncu o kadar bekler.
    ///   - Bitisik adimlar ayni karede ilerletilemez.
    ///   - Kargo arac olduktan sonra paketlenemez.
    /// </summary>
    public sealed class PackagingSystem
    {
        private readonly Dictionary<int, float> _stepReadyAt =
            new Dictionary<int, float>();

        private readonly HashSet<int> _busy = new HashSet<int>();

        /// <summary>Paketleme zorunlu mu? Kapaliysa ham kargo araca yuklenebilir.</summary>
        public bool Required { get; }

        /// <summary>Tamamlanan paket sayisi (gun ici istatistik).</summary>
        public int CompletedCount { get; private set; }

        public PackagingSystem(bool required)
        {
            Required = required;
        }

        /// <summary>
        /// Kargo bu sistemde paketlenebilir mi?
        ///
        /// "Zorunlu degilse" hicbir zaman true doner: eski akis (raf ->
        /// arac) aynen calisir. Bu bayrak, paketlemeyi zorunlu kilacak
        /// icindir.
        /// </summary>
        public bool CanPackage(Parcel parcel)
        {
            if (parcel == null) return false;
            if (!parcel.IsActive) return false;

            // Yolda olan kargo paketlenmez.
            if (parcel.IsLoaded || parcel.IsDelivered) return false;

            return true;
        }

        /// <summary>
        /// Paketleme masasi bos mu? Yani masada uzerinde calisacak
        /// baska bir kargo var mi?
        ///
        /// NOT: bu tek kargo lu bir masa icindir. Birden fazla kuru
        /// calistirmak gerekirse ileride sure listesi (queue) eklenir;
        /// simdilik tek is yeterli ve anlasilir.
        /// </summary>
        public bool IsTableBusy => _busy.Count > 0;

        /// <summary>Masadaki kargonun kimligi (yoksa 0).</summary>
        public int TableParcelId
        {
            get
            {
                foreach (int id in _busy) return id;
                return 0;
            }
        }

        /// <summary>Bu kargo masada mi?</summary>
        public bool IsOnTable(Parcel parcel) =>
            parcel != null && _busy.Contains(parcel.Id);

        /// <summary>
        /// Kargoyu masaya koy: paketlemeyi baslatir.
        /// </summary>
        /// <returns>Basariliysa true.</returns>
        public bool Begin(Parcel parcel, float systemTime)
        {
            if (!CanPackage(parcel)) return false;
            if (parcel.IsPackaged) return false;
            if (_busy.Contains(parcel.Id)) return true;

            // Masa TEK ISLIK bir masadir. Oyuncu elinde birden fazla
            // kargo tasiyabilir; baska bir kargoyu masaya koyarsa
            // oncekindi masadan KALDIRILIR - ama ilerlemesi KORUNUR.
            //
            // Neden evirt (displacement) gerekli: kurye 3 kutu
            // tasiyabiliyor. Kural olmasaydi oyuncu 1. kutuyu yarida
            // birakip 2. kutuya gecince masada ikisi birden "is
            // altinda" sayilir, IsTableBusy ve TableParcelId anlamini
            // yitirir ve hangi kutunun ne kadar suresi kaldigi
            // belirsizlesir.
            EvictOthers(parcel.Id);

            _busy.Add(parcel.Id);
            parcel.MarkPackagingStarted(systemTime);

            GameEvents.RaisePackagingStarted(parcel);
            GameEvents.RaiseCargoChanged();
            return true;
        }

        /// <summary>
        /// Başka kargoyu masadan kaldırır; ilerlemesi korunur.
        /// </summary>
        private void EvictOthers(int keepId)
        {
            if (_busy.Count <= 1) return;

            // _busy HashSet oldugu icin "kendisi disinda hepsini sil"
            // tek gecisle; toplama (allocation) olmasin.
            int[] snapshot = new int[_busy.Count];
            _busy.CopyTo(snapshot);
            for (int i = 0; i < snapshot.Length; i++)
                if (snapshot[i] != keepId) _busy.Remove(snapshot[i]);
        }

        /// <summary>
        /// Kargoyu masadan geri al (bosa cikarildi veya oyuncu vazgeclti).
        ///
        /// DIKKAT: ilerleme SIFIRLANMAZ. Oyuncu 3. adimdayken vazgecip
        /// sonra donerse kaldigi yerden devam eder. Aksi halde "paketleme
        /// isini kaybetmek" oyuncuya zarar verir ve istenen his bu degil.
        /// </summary>
        public bool Abandon(Parcel parcel)
        {
            if (parcel == null) return false;
            if (!_busy.Remove(parcel.Id)) return false;

            GameEvents.RaisePackagingAbandoned(parcel);
            GameEvents.RaiseCargoChanged();
            return true;
        }

        /// <summary>
        /// Su anki adimin suresi doldu mu?
        /// </summary>
        public bool IsStepReady(Parcel parcel, float systemTime)
        {
            if (parcel == null) return false;
            if (!_busy.Contains(parcel.Id)) return false;
            if (PackagingSteps.IsReady(parcel.PackagingProgress)) return false;

            return systemTime >= ReadyAt(parcel);
        }

        /// <summary>
        /// Bu kargonun su anki adimi ne zaman bitecek?
        /// </summary>
        public float ReadyAt(Parcel parcel)
        {
            if (parcel == null) return 0f;
            if (_stepReadyAt.TryGetValue(parcel.Id, out float ready)) return ready;

            float started = parcel.PackagingStartedTime < 0f ? 0f : parcel.PackagingStartedTime;
            return started + PackagingSteps.DurationOf(parcel.PackagingProgress);
        }

        /// <summary>
        /// Kalan sure (saniye). Bittiyse 0.
        /// </summary>
        public float RemainingSeconds(Parcel parcel, float systemTime)
        {
            float left = ReadyAt(parcel) - systemTime;
            return left > 0f ? left : 0f;
        }

        /// <summary>
        /// Suresi dolmus adimi ilerletir.
        ///
        /// Iki kez ardisik cagrilirsa ikincisi <c>false</c> doner: adim
        /// basina bir kez ilerlenir. Bu, oyuncunun E tusuna basilma
        /// hizina bagli olarak bir adimi atlamasini engeller.
        /// </summary>
        public bool Advance(Parcel parcel, float systemTime)
        {
            if (parcel == null) return false;
            if (!_busy.Contains(parcel.Id)) return false;
            if (!IsStepReady(parcel, systemTime)) return false;

            PackagingStep before = parcel.PackagingProgress;
            if (!parcel.AdvancePackaging()) return false;

            // Yeni adimin bitis zamani simdiden itibaren.
            _stepReadyAt[parcel.Id] = systemTime +
                PackagingSteps.DurationOf(parcel.PackagingProgress);

            GameEvents.RaisePackagingStepCompleted(parcel, before, parcel.PackagingProgress);

            if (parcel.IsPackaged)
            {
                CompletedCount++;
                _busy.Remove(parcel.Id);
                _stepReadyAt.Remove(parcel.Id);
                GameEvents.RaiseParcelPackaged(parcel);
            }

            GameEvents.RaiseCargoChanged();
            return true;
        }

        /// <summary>
        /// Gun basinda masayi temizler. Kargolar DEPOYA GERI DONDURULMEZ;
        /// yeni gun yeni kargolarla gelir.
        /// </summary>
        public void BeginDay()
        {
            _busy.Clear();
            _stepReadyAt.Clear();
            CompletedCount = 0;
        }
    }
}
