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

        private void OnValidate()
        {
            if (dayLengthSeconds < 30f) dayLengthSeconds = 30f;
        }
    }
}
