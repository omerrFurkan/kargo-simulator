using System;
using System.Collections.Generic;
using UnityEngine;

namespace KargoSim.Delivery
{
    /// <summary>
    /// Bir teslimat noktasinin HARITA VERISI. Kod degil icerik.
    ///
    /// Simdilik kutudan uretilen bir izgara; Aşama 8'de ayni ID'li
    /// gercek binalar bu konumlara baglanacak. Bu yuzden teslimat sistemi
    /// konumu buradan okur, kendi icinde gecmez konumlari tutmaz.
    /// </summary>
    [Serializable]
    public struct DeliveryPointData
    {
        [Tooltip("Bu noktanin kargo hedeflerinde kullandigi kimlik. " +
                 "Binalar bu kimlige baglanir.")]
        public int id;

        [Tooltip("Depo listesinde gorunecek ad.")]
        public string displayName;

        [Tooltip("Dunya konumu. Aracin varis burada sayilir.")]
        public Vector3 position;
    }

    /// <summary>
    /// Harita duzeni: teslimat noktalari ve depo konumu.
    ///
    /// MapLayout tek bir "icerik veritabanidir". Sistemler buradan okur;
    /// haritanin gercek gorseli (binalar, yollar) Aşama 8'de bu verinin
    /// USTUNE kurulur, bu asset degismez.
    /// </summary>
    [CreateAssetMenu(
        fileName = "MapLayout",
        menuName = "KargoSim/Map Layout",
        order = 3)]
    public sealed class MapLayout : ScriptableObject
    {
        [Header("Depo")]
        [Tooltip("Kurye aracinin dogdugu ve gun sonu dondugu konum.")]
        public Vector3 depotPosition = new Vector3(0f, 0f, 0f);

        [Header("Teslimat Noktalari")]
        [Tooltip("Teslimat adresleri. Kargolar bu listeye gore uretilir " +
                 "(GameConfig.destinationCount ile uyumlu olmali).")]
        public List<DeliveryPointData> deliveryPoints = new List<DeliveryPointData>();

        /// <summary>
        /// Kimlige gore nokta arar. Bulunamazsa null doner.
        /// </summary>
        public DeliveryPointData? Find(int id)
        {
            for (int i = 0; i < deliveryPoints.Count; i++)
                if (deliveryPoints[i].id == id) return deliveryPoints[i];

            return null;
        }

        /// <summary>Konuma gore nokta arar (arac varis kontrolunde kullanilir).</summary>
        public DeliveryPointData? FindNearest(Vector3 position, float maxDistance)
        {
            float bestSqr = maxDistance * maxDistance;
            DeliveryPointData? best = null;

            for (int i = 0; i < deliveryPoints.Count; i++)
            {
                float sqr = (deliveryPoints[i].position - position).sqrMagnitude;
                if (sqr > bestSqr) continue;

                bestSqr = sqr;
                best = deliveryPoints[i];
            }

            return best;
        }

        private void OnValidate()
        {
            if (deliveryPoints == null) deliveryPoints = new List<DeliveryPointData>();

            // Struct oldugu icin list[index] uzerinden alan atamak DERLENMEZ.
            // Yerel degiskene alip, degistirip geri yaziyoruz.
            for (int i = 0; i < deliveryPoints.Count; i++)
            {
                DeliveryPointData point = deliveryPoints[i];
                if (string.IsNullOrEmpty(point.displayName))
                {
                    point.displayName = "Adres " + point.id;
                    deliveryPoints[i] = point;
                }
            }
        }
    }
}
