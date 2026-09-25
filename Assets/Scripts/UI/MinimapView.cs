using System.Collections.Generic;
using KargoSim.Delivery;
using UnityEngine;
using UnityEngine.UI;

namespace KargoSim.UI
{
    /// <summary>
    /// Kucuk harita: depo, duraklar, rota cizgisi ve arac.
    ///
    /// Neden ayri minimap: 3D dunyada 14 noktayi gozlemek zor; oyuncu
    /// "nereye gidecegim" sorusunu her saniye sormamali.
    ///
    /// PERFORMANS: Durak noktalari rota DEGISTIGINDE yeniden kurulur
    /// (olay g driven). Her kare yalnizca arac noktasi tasinir - tek bir
    /// RectTransform ayari, en ucuz islem.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MinimapView : MonoBehaviour
    {
        [Header("Parcalar")]
        [SerializeField] private RectTransform area;
        [SerializeField] private RectTransform depotDot;
        [SerializeField] private RectTransform vehicleDot;
        [SerializeField] private RectTransform routeRoot;
        [SerializeField] private RectTransform dotPrefab;

        [Header("Olcek")]
        [Tooltip("Dunya koordinati -> minimap pikseli donustururken kullanilan yaricap (metre).")]
        [SerializeField] private float worldRadius = 110f;

        private readonly List<RectTransform> _stopDots = new List<RectTransform>();
        private readonly List<Image> _routeSegments = new List<Image>();
        private MapLayout _layout;
        private DeliverySystem _delivery;

        public int StopDotCount => _stopDots.Count;

        public void Rebuild(MapLayout layout, DeliverySystem delivery)
        {
            _layout = layout;
            _delivery = delivery;

            if (area == null) return;

            // --- Depo ---
            if (depotDot != null && layout != null)
                depotDot.anchoredPosition = WorldToLocal(layout.depotPosition);

            BuildStopDots();
            BuildRoute();
        }

        private void BuildStopDots()
        {
            int needed = _delivery != null ? _delivery.Route.Count : 0;

            while (_stopDots.Count < needed && dotPrefab != null && routeRoot != null)
            {
                var dot = Instantiate(dotPrefab, routeRoot);
                dot.gameObject.SetActive(false);
                _stopDots.Add(dot);
            }

            for (int i = 0; i < _stopDots.Count; i++)
            {
                bool active = _delivery != null && i < _delivery.Route.Count;
                _stopDots[i].gameObject.SetActive(active);
                if (!active) continue;

                var image = _stopDots[i].GetComponent<Image>();
                if (image != null) image.color = UITheme.StopColor;

                _stopDots[i].anchoredPosition = WorldToLocal(_delivery.Route[i].Position);
            }
        }

        private void BuildRoute()
        {
            // Rota cizgisi: duraklari birbirine baglayan ince cizgiler.
            // LineRenderer yerine UI cizgileri tercih edildi: kamera
            // dondugunce de okunakli kalir ve ekstra component gerektirmez.
            int segments = _delivery != null ? Mathf.Max(0, _delivery.Route.Count - 1) : 0;

            while (_routeSegments.Count < segments && dotPrefab != null && routeRoot != null)
            {
                var seg = Instantiate(dotPrefab, routeRoot);
                var image = seg.GetComponent<Image>();
                if (image != null) image.color = UITheme.RouteColor;
                _routeSegments.Add(image);
            }

            for (int i = 0; i < _routeSegments.Count; i++)
            {
                bool active = i < segments;
                _routeSegments[i].gameObject.SetActive(active);
                if (!active || _delivery == null) continue;

                RectTransform from = _stopDots[i];
                RectTransform to = _stopDots[i + 1];
                if (from == null || to == null) continue;

                Vector2 a = from.anchoredPosition;
                Vector2 b = to.anchoredPosition;
                Vector2 mid = (a + b) * 0.5f;

                var rt = _routeSegments[i].rectTransform;
                rt.anchoredPosition = mid;

                float length = Vector2.Distance(a, b);
                float angle = Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg;
                rt.sizeDelta = new Vector2(length, 2f);
                rt.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        /// <summary>Arac noktasini her kare gunceller (en ucuz islem).</summary>
        public void SetVehiclePosition(Vector3 worldPosition)
        {
            if (vehicleDot == null) return;
            vehicleDot.anchoredPosition = WorldToLocal(worldPosition);
        }

        /// <summary>Dunya konumunu minimap yerel koordinatina cevirir.</summary>
        private Vector2 WorldToLocal(Vector3 world)
        {
            if (area == null) return Vector2.zero;

            Vector3 origin = _layout != null ? _layout.depotPosition : Vector3.zero;
            float scale = area.rect.width / (worldRadius * 2f);

            // Harita merkezden simetrik: +x sag, +z yukari
            return new Vector2((world.x - origin.x) * scale, (world.z - origin.z) * scale);
        }
    }
}
