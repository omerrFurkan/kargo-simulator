using KargoSim.Cargo;
using UnityEngine;

namespace KargoSim.Player
{
    /// <summary>
    /// Bir kargonun dunyadaki gorseli: turune gore renkli bir kutu.
    ///
    /// Neden ayri component: ayni gorsel uc yerde kullaniliyor - depoda
    /// rafta, karakterin elinde ve aracın icinde. Renk ve olcek
    /// ParcelType'tan gelir; boylece "hangi kargo hangisi" sorusu
    /// oyun icinde gorsel olarak cevaplanir.
    ///
    /// Collider YOK: kargo simdilik etkilesime girmez. Kutu gorseldir.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ParcelVisual : MonoBehaviour
    {
        private MeshRenderer _renderer;
        private MaterialPropertyBlock _block;

        /// <summary>Kutunun kenar uzunlugu (metre).</summary>
        public float Size { get; private set; } = 0.42f;

        /// <summary>
        /// Kargo turunun rengi ve olcegine gore gorseli hazirlar.
        /// </summary>
        public void Apply(ParcelType type)
        {
            Size = type != null ? Mathf.Clamp(type.boxSize, 0.15f, 1.2f) : 0.42f;
            transform.localScale = new Vector3(Size, Size, Size);

            _renderer = GetComponent<MeshRenderer>();
            if (_renderer == null) _renderer = gameObject.AddComponent<MeshRenderer>();

            Color color = type != null ? type.color : new Color(0.80f, 0.60f, 0.32f);

            // Materyal paylasmamak icin property block: butun kutular tek
            // materyali kullanir, renk sadece burada degisir.
            _block = new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(_block);
            _block.SetColor("_BaseColor", color);
            _block.SetColor("_Color", color);
            _renderer.SetPropertyBlock(_block);
        }
    }
}
