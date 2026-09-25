using UnityEngine;

namespace KargoSim.Core
{
    /// <summary>
    /// Oyunun ayarlanabilir sabitleri. Kod icine dagitik sabit yazmamak icin
    /// Inspector uzerinden tek noktadan degistirilebilir hale getirilir.
    /// </summary>
    [CreateAssetMenu(
        fileName = "GameConfig",
        menuName = "KargoSim/Game Config",
        order = 0)]
    public sealed class GameConfig : ScriptableObject
    {
        [Header("Gun Dongusu")]
        [Tooltip("Bir oyun gununun gercek saniye cinsinden uzunlugu. 300 = 5 dakika.")]
        [Range(30f, 1800f)]
        public float dayLengthSeconds = 300f;

        [Tooltip("Oyunun basladigi saat (0-24).")]
        [Range(0f, 23f)]
        public float startHour = 8f;

        [Tooltip("Gun bitince otomatik olarak depo konumuna geri don.")]
        public bool forceReturnToDepotOnDayEnd = true;

        [Header("Zamanlama")]
        [Tooltip("Simulasyon duraklatildiginda gercek zaman da akmaya devam etsin mi? " +
                 "Oyun ici gun sayaci her zaman duraklar, bu sadece UI debounce icindir.")]
        public bool pauseClockOnFocusLoss = true;

        [Header("Kargo")]
        [Tooltip("Gun basina depoda belirecek kargo sayisi.")]
        [Range(1, 40)] public int parcelsPerDay = 12;

        [Tooltip("Normal oncelikli bir kargonun temel teslim suresi (saniye). " +
                 "Hizli/acil kargolarda bu sure carpanlarla kisalir. " +
                 "300 = 5 dakikalik bir gun icinde 5 dakika.")]
        [Range(30f, 1200f)] public float baseDeliverySeconds = 300f;

        [Tooltip("Hedef adres sayisi. Kargolar 0..buSayi-1 arasinda bir " +
                 "adrese atanir. Gercek koordinatlar Aşama 4'te baglanir.")]
        [Range(1, 60)] public int destinationCount = 14;

        private void OnValidate()
        {
            if (dayLengthSeconds < 30f) dayLengthSeconds = 30f;
        }
    }
}
