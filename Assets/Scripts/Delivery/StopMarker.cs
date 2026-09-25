using System.Collections.Generic;
using KargoSim.Delivery;
using UnityEngine;

namespace KargoSim.Delivery
{
    /// <summary>
    /// Bir teslimat noktasinin DUNYA GORSELI: yerde renkli kare ve
    /// uzerinde adres adi.
    ///
    /// Simdilik kutu + yazi (primitive). Aşama 8'de ayni yerde gercek
    /// bina olacak; bu gorsel O BINA OLUŞTUKTAN SONRA kapanabilir.
    ///
    /// Neden gerekli: sadece minimap yetersiz. Oyuncu 3D dunyada binalarin
    /// arasinda dolasirken nereye gidecegini gormesi lazim; yoksa hedefi
    /// aramak kuru oyuna doner.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StopMarker : MonoBehaviour
    {
        [Header("Parcalar")]
        [SerializeField] private Renderer padRenderer;
        [SerializeField] private Transform billboard;

        [Header("Gorsel")]
        [SerializeField] private float padSize = 6f;

        private Color _baseColor = Color.white;

        /// <summary>Rotadaki sirasi (-1 = rotada degil).</summary>
        public int RouteIndex { get; private set; } = -1;

        /// <summary>Siradaki hedef mi? Vurgulanir.</summary>
        public bool IsNext => RouteIndex == 0;

        public void Configure(int destinationId, string displayName)
        {
            name = "Stop_" + destinationId;
            gameObject.layer = LayerMask.NameToLayer("Building");

            if (padRenderer != null)
            {
                _baseColor = PadColorFor(destinationId);
                SetColor(_baseColor);
            }

            if (billboard != null)
                billboard.name = "Label_" + destinationId;
        }

        /// <summary>Rotadaki yeri gunceller; renk ve olcek degisir.</summary>
        public void SetRouteIndex(int index)
        {
            RouteIndex = index;

            if (padRenderer != null)
                SetColor(index == 0 ? Color.white : _baseColor);

            // Siradaki durak biraz daha buyuk olsun: gozde ilk o gelir.
            float scale = index == 0 ? padSize * 1.25f : padSize;
            if (padRenderer != null)
                padRenderer.transform.localScale = new Vector3(scale, 1f, scale);
        }

        private void SetColor(Color color)
        {
            // URP/Lit materyalinin rengini degistirmek material INSTANCE'i
            // gerektirir; aksi halde tum noktalar ayni rengi paylasir.
            var block = new MaterialPropertyBlock();
            padRenderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            padRenderer.SetPropertyBlock(block);
        }

        /// <summary>
        /// Kimlige gore kararli bir renk: ayni adres her gun ayni renkte,
        /// oyuncu "mavi ev" diye hatirlayabiliyor.
        /// </summary>
        private static Color PadColorFor(int id)
        {
            switch (id % 5)
            {
                case 0: return new Color(0.30f, 0.65f, 0.95f);
                case 1: return new Color(0.95f, 0.60f, 0.25f);
                case 2: return new Color(0.55f, 0.85f, 0.40f);
                case 3: return new Color(0.80f, 0.45f, 0.90f);
                default: return new Color(0.95f, 0.45f, 0.45f);
            }
        }

        // DIKKAT: Burada Update/LateUpdate YOK. 14 marker oldugu icin her
        // birine bir geri cagri yazmak, oyunun "tek Update" kuralini bozar
        // ve gereksiz maliyet olur. Etiket yere yatay durur; kamera 35
        // derece yukardan baktigi icin okunur ve sifir kare maliyeti var.
    }
}
