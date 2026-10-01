using UnityEngine;

namespace KargoSim.Player
{
    /// <summary>
    /// KAPIYI ACAN NPC (oyuncu istegi).
    ///
    /// OYUNCUNUN RAPORU:
    ///   "Elimdeki kutuyu teslim edemiyorum, kapı çalma, bu kapıyı
    ///    açan NPC ve bu NPC de kargoyu verselim."
    ///
    /// SORUN: `NPCSystem` SAF VERI sinifi - `GameObject` uretmiyor.
    /// Kapida calinca ekranda kimse cikmiyor; sadece HUD metni
    /// degisiyor. Oyuncu "kapiyi kaldim, bir sey olmadi" diyor ve
    /// teslim edilmis gibi saniyor.
    ///
    /// COZUM: Bu bilesen kapinin onunde GERCEK bir insan figuru
    /// olusturur:
    ///   1. Kapı çalınır → NPC belirir (kapıyı açarak)
    ///   2. Kod sorulurken NPC bekler
    ///   3. Doğru kod girilince NPC kargoyu ALIR (kutu NPC'ye gider)
    ///   4. Teslim bitince NPC geri çekilir
    ///
    /// Figur primitive'lerden kurulur (projenin geri kalaniyla aynı
    /// stil: kutu + küre). Daha önce `CityDetailPass` kapı başına
    /// SABIT bir sakin koyuyordu; o bir dekordu, teslim almıyordu.
    /// Buradaki figur olaylara bağlıdır.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DoorNpc : MonoBehaviour
    {
        [Header("Kapı başına kim?")]
        [Tooltip("Adresten türetilir; aynı adres hep aynı kişiyi gösterir.")]
        [SerializeField] private int addressId;

        [Header("Görünürlük")]
        [SerializeField] private Vector3 standPosition = new Vector3(0f, 0f, 1.15f);
        [SerializeField] private float appearSpeed = 5f;

        private Transform _figure;
        private Transform _hand;
        private Vector3 _home;
        private bool _out;

        public int AddressId => addressId;
        public bool IsOutside => _out;

        /// <summary>
        /// Kapıya NPC figuru kurar (idempotent). Kapinin onune
        /// oturtur ama GIZLI baslar - sadece kapi calininca gorunur.
        /// </summary>
        public static DoorNpc Attach(Transform door, int addressId)
        {
            if (door == null) return null;

            var existing = door.GetComponent<DoorNpc>();
            if (existing != null)
            {
                existing.addressId = addressId;
                return existing;
            }

            var npc = door.gameObject.AddComponent<DoorNpc>();
            npc.addressId = addressId;
            npc.Build();
            return npc;
        }

        private void Build()
        {
            _home = standPosition;

            var root = new GameObject("KapıNpc");
            root.transform.SetParent(transform, false);
            root.transform.localPosition = _home;
            root.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            // Adres deterministik renk/kişi seçimi: aynı kapıda hep aynı NPC
            int h = Mathf.Abs(addressId * 7919) % 997;
            int h2 = Mathf.Abs(addressId * 4409) % 991;

            Color skin = SkinTone(h);
            Color hair = HairTone(h2);
            Color shirt = ShirtTone(h);

            // --- gövde
            Child(root.transform, "Govde", new Vector3(0f, 1.14f, 0f),
                  new Vector3(0.42f, 0.62f, 0.24f), shirt);
            Child(root.transform, "Omuz", new Vector3(0f, 1.40f, 0f),
                  new Vector3(0.46f, 0.14f, 0.25f), shirt);
            Child(root.transform, "Bel", new Vector3(0f, 0.85f, 0f),
                  new Vector3(0.36f, 0.16f, 0.22f),
                  new Color(0.18f, 0.20f, 0.26f));

            // --- kollar (biri kargo almaya uzanir)
            Child(root.transform, "KolSol", new Vector3(-0.27f, 1.12f, 0f),
                  new Vector3(0.13f, 0.58f, 0.14f), shirt);
            var kolSag = Child(root.transform, "KolSag", new Vector3(0.27f, 1.12f, 0f),
                  new Vector3(0.13f, 0.58f, 0.14f), shirt);
            kolSag.transform.localRotation = Quaternion.Euler(0f, 0f, -14f);

            var elSag = Child(root.transform, "ElSag", new Vector3(0.30f, 0.84f, -0.06f),
                  new Vector3(0.12f, 0.14f, 0.13f), skin);
            elSag.transform.localRotation = Quaternion.Euler(0f, 0f, -14f);

            Child(root.transform, "ElSol", new Vector3(-0.27f, 0.80f, 0f),
                  new Vector3(0.12f, 0.14f, 0.13f), skin);

            // --- kafa
            Child(root.transform, "Boyun", new Vector3(0f, 1.48f, 0f),
                  new Vector3(0.12f, 0.10f, 0.12f), skin);
            Ball(root.transform, "Kafa", new Vector3(0f, 1.66f, 0f),
                 new Vector3(0.25f, 0.29f, 0.25f), skin);
            Ball(root.transform, "Sac", new Vector3(0f, 1.735f, -0.005f),
                 new Vector3(0.27f, 0.19f, 0.27f), hair);

            // Yuz kameraya (oyuncuya) bakar
            Child(root.transform, "GozSol", new Vector3(-0.052f, 1.675f, 0.118f),
                  new Vector3(0.035f, 0.045f, 0.02f), new Color(0.10f, 0.10f, 0.12f));
            Child(root.transform, "GozSag", new Vector3(0.052f, 1.675f, 0.118f),
                  new Vector3(0.035f, 0.045f, 0.02f), new Color(0.10f, 0.10f, 0.12f));
            Child(root.transform, "Agiz", new Vector3(0f, 1.60f, 0.122f),
                  new Vector3(0.06f, 0.018f, 0.02f), new Color(0.55f, 0.28f, 0.26f));

            // --- kargo alma noktasi (elinin onu)
            var hand = new GameObject("ElNoktasi");
            hand.transform.SetParent(root.transform, false);
            hand.transform.localPosition = new Vector3(0.34f, 0.92f, -0.16f);
            _hand = hand.transform;

            _figure = root.transform;
            root.SetActive(false);   // kapi calininca gorunur
        }

        /// <summary>Kapi calindi: NPC kapidan cikip belirir.</summary>
        public void ComeToDoor()
        {
            if (_figure == null) Build();
            _figure.gameObject.SetActive(true);
            _out = true;
        }

        /// <summary>Teslim bitti: NPC geri girer.</summary>
        public void GoBackInside()
        {
            _out = false;
            if (_figure != null) _figure.gameObject.SetActive(false);
        }

        /// <summary>
        /// Kargoyu alir: kutuyu NPC'nin eline tasir, sonra kutu
        /// teslim edilmis olur ve YOK EDILIR.
        ///
        /// Bu, "bu NPC de kargoyu verselim" isteginin teslim kismi.
        /// Kutu ANCAK burada, dogru koddan sonra yok olur.
        /// </summary>
        public void ReceiveParcel(Cargo.ParcelWorldObject box)
        {
            if (box == null || _hand == null) return;

            // Kutu NPC'ye verilir (0.35 saniyede elde belirir)
            box.transform.SetParent(_hand, true);
            box.transform.localPosition = Vector3.zero;
            box.transform.localRotation = Quaternion.identity;
        }

        /// <summary>NPC'den alinan kutuyu teslim sonrasi yok et.</summary>
        public void DestroyReceivedParcel(Cargo.ParcelWorldObject box)
        {
            if (box == null) return;
            if (Application.isPlaying) Destroy(box.gameObject);
            else DestroyImmediate(box.gameObject);
        }

        public Transform HandPoint => _hand;

        // ---------------------------------------------------------------
        private static GameObject Child(Transform p, string n, Vector3 pos,
                                        Vector3 size, Color c)
        {
            var go = new GameObject(n);
            go.transform.SetParent(p, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            var mr = go.AddComponent<MeshRenderer>();
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = CubeMesh();
            mr.sharedMaterial = Mat(c);
            return go;
        }

        private static GameObject Ball(Transform p, string n, Vector3 pos,
                                       Vector3 size, Color c)
        {
            var go = new GameObject(n);
            go.transform.SetParent(p, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            var mr = go.AddComponent<MeshRenderer>();
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = SphereMesh();
            mr.sharedMaterial = Mat(c);
            return go;
        }

        private static Mesh _cube;
        private static Mesh _sphere;
        private static readonly System.Collections.Generic.Dictionary<Color, Material> _mats =
            new System.Collections.Generic.Dictionary<Color, Material>();

        private static Mesh CubeMesh()
        {
            if (_cube != null) return _cube;
            var t = GameObject.CreatePrimitive(PrimitiveType.Cube);
            t.hideFlags = HideFlags.HideAndDontSave;
            _cube = t.GetComponent<MeshFilter>().sharedMesh;
            return _cube;
        }

        private static Mesh SphereMesh()
        {
            if (_sphere != null) return _sphere;
            var t = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            t.hideFlags = HideFlags.HideAndDontSave;
            _sphere = t.GetComponent<MeshFilter>().sharedMesh;
            return _sphere;
        }

        private static Material Mat(Color c)
        {
            Material m;
            if (_mats.TryGetValue(c, out m) && m != null) return m;
            Shader sh = Shader.Find("Universal Render Pipeline/Lit")
                     ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                     ?? Shader.Find("Standard");
            m = new Material(sh) { name = "NpcMat_" + ColorUtility.ToHtmlStringRGB(c) };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.12f);
            _mats[c] = m;
            return m;
        }

        private static Color SkinTone(int h) => new[]
        {
            new Color(0.94f, 0.79f, 0.68f), new Color(0.87f, 0.70f, 0.57f),
            new Color(0.76f, 0.57f, 0.44f), new Color(0.55f, 0.39f, 0.28f),
            new Color(0.40f, 0.28f, 0.21f),
        }[h % 5];

        private static Color HairTone(int h) => new[]
        {
            new Color(0.10f, 0.08f, 0.07f), new Color(0.25f, 0.16f, 0.09f),
            new Color(0.48f, 0.33f, 0.16f), new Color(0.70f, 0.60f, 0.42f),
            new Color(0.72f, 0.72f, 0.74f),
        }[h % 5];

        private static Color ShirtTone(int h) => new[]
        {
            new Color(0.24f, 0.44f, 0.72f), new Color(0.78f, 0.30f, 0.28f),
            new Color(0.28f, 0.56f, 0.36f), new Color(0.88f, 0.68f, 0.20f),
            new Color(0.55f, 0.30f, 0.62f), new Color(0.90f, 0.88f, 0.84f),
        }[h % 6];
    }
}