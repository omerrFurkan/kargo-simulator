using System.Collections.Generic;
using UnityEngine;

namespace KargoSim.Delivery
{
    /// <summary>
    /// Rota siralamasi.
    ///
    /// Klasik "en yakin komsu" (greedy nearest neighbour) algoritmasi:
    /// su an en yakin duraga git, sonra kalanlar icin tekrarla.
    ///
    /// NEDEN BU KADAR BASIT: Optimal cozum (TSP) 14 noktada bile pahalidir
    /// ve oyuncuya kazanci gorunmez. Onemli olan oyuncunun hangi sirayla
    /// gidecegini GOREBILMESIDIR; Aşama 6'da rota minimapta cizilecek.
    ///
    /// Tum fonksiyonlar static ve saf: listeyi degistirmeden yeni bir
    /// sirali kopya dondurur. Test edilebilir ve yan etkisiz.
    /// </summary>
    public static class RoutePlanner
    {
        /// <summary>
        /// Duraklari en-yakin-komsu sirasina gore dondurur.
        /// Girdi listesi DEGISTIRILMEZ.
        /// </summary>
        public static List<DeliveryStop> OrderByNearestNeighbour(
            Vector3 startPosition, IReadOnlyList<DeliveryStop> stops)
        {
            var ordered = new List<DeliveryStop>();
            if (stops == null || stops.Count == 0) return ordered;

            // Kalan duraklar: henuz sıralanmayanlar. Kopyalamak, girdi
            // listesini degistirmemizi garantiler.
            var remaining = new List<DeliveryStop>(stops.Count);
            for (int i = 0; i < stops.Count; i++)
                if (stops[i] != null) remaining.Add(stops[i]);

            Vector3 cursor = startPosition;

            while (remaining.Count > 0)
            {
                int bestIndex = FindNearestIndex(remaining, cursor);
                DeliveryStop best = remaining[bestIndex];

                ordered.Add(best);
                remaining.RemoveAt(bestIndex);
                cursor = best.Position;
            }

            return ordered;
        }

        private static int FindNearestIndex(List<DeliveryStop> candidates, Vector3 from)
        {
            int bestIndex = 0;
            float bestSqr = float.MaxValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                float sqr = (candidates[i].Position - from).sqrMagnitude;
                if (sqr >= bestSqr) continue;

                bestSqr = sqr;
                bestIndex = i;
            }

            return bestIndex;
        }

        /// <summary>
        /// Sirali duraklarin toplam yolunu hesaplar (metre).
        /// Minimapta "kalan mesafe" gostermek icin.
        /// </summary>
        public static float CalculateRouteLength(Vector3 startPosition,
                                                IReadOnlyList<DeliveryStop> orderedStops)
        {
            if (orderedStops == null || orderedStops.Count == 0) return 0f;

            float total = 0f;
            Vector3 cursor = startPosition;

            for (int i = 0; i < orderedStops.Count; i++)
            {
                if (orderedStops[i] == null) continue;
                total += Vector3.Distance(cursor, orderedStops[i].Position);
                cursor = orderedStops[i].Position;
            }

            return total;
        }

        /// <summary>
        /// Siradaki durak. Rota bittiyse null.
        /// </summary>
        public static DeliveryStop PeekNext(IReadOnlyList<DeliveryStop> orderedStops, int currentIndex)
        {
            if (orderedStops == null) return null;
            if (currentIndex < 0 || currentIndex >= orderedStops.Count) return null;
            return orderedStops[currentIndex];
        }
    }
}
