using System.Collections.Generic;
using KargoSim.Cargo;
using KargoSim.Core;
using KargoSim.UI;
using UnityEngine;

namespace KargoSim.Player
{
    /// <summary>
    /// Aracin kasasinda duran GERCEK kutular.
    ///
    /// Sorun: kargo sistemi kac kutu yuklendigini biliyordu ama arac
    /// gorseli kapali kasa bir van'du; kutular hic gorunmuyordu. Oyuncu
    /// first person kamerasiyla kabindeydi, arkayi da goremiyordu; yukleme
    /// tamamen soyut kaliyordu. Cozum: acik kasali bir pickup + kutularin
    /// kasa icinde gercekten istiflenmesi.
    ///
    /// Bu bilesen olay g driven'dir (Update YOK): kargo degistiginde
    /// <see cref="GameEvents.CargoChanged"/> ile haber verir, manifest'teki
    /// listeyi okuyup kutulari bastan kurar. Kimse bunu elle cagirmak
    /// zorunda degil.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VanCargoVisual : MonoBehaviour
    {
        [Tooltip("Kasa boslugunun bos oldugu cocuk nesne. Kutular bunun cocugu olur.")]
        [SerializeField] private Transform cargoBed;

        [Tooltip("Kasa icinin en/boy olculeri (metre). Izgara buradan hesaplanir.")]
        [SerializeField] private Vector2 bedSize = new Vector2(1.55f, 1.85f);

        [Tooltip("Iki kutu arasindaki bosluk (metre).")]
        [SerializeField] private float gap = 0.04f;

        [Tooltip("En fazla gosterilecek kutu. Kasa tasarsa ust katmanlar " +
                 "gorsel olarak kirpilir.")]
        [SerializeField] private int maxVisibleBoxes = 40;

        // Kutu olculeri: hacimden yana bir kenar. Tum kutular ayni olcekte
        // olsaydi kargonun yogunlugu hissedilmezdi.
        private const float MinBoxSide = 0.20f;
        private const float MaxBoxSide = 0.62f;

        // Kutu rengi agirliga gore: hafif kutu aci karton, agir kutu koyu.
        // Agirlik bu araliktaysa hepsi ayni tonda kalir.
        private const float LightWeightKg = 0.5f;
        private const float HeavyWeightKg = 8f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private readonly List<GameObject> _boxes = new List<GameObject>();

        private CargoManifest _manifest;
        private Material _boxMaterial;

        /// <summary>Kurada duran kutu sayisi (test ve tani icin).</summary>
        public int VisibleBoxCount => _boxes.Count;

        /// <summary>Kutular yukleme sirasina gore sirali (test ve tani icin).</summary>
        public IReadOnlyList<GameObject> VisibleBoxes => _boxes;

        /// <summary>Kasa boslugu. Bos ise kutu uretilmez.</summary>
        public Transform CargoBed => cargoBed;

        /// <summary>Kasa ic olculeri (x = en, y = boy). Izgara buradan gelir.</summary>
        public Vector2 BedSize => bedSize;

        private void Awake()
        {
            // Statik olay kuyruguna baglanmak yeterli: UIManager veya
            // baska bir sistem VanCargoVisual'i tanimak zorunda degil.
            GameEvents.CargoChanged += OnCargoChanged;

            // Prefab'da elle atanmissa dokunma; yoksa adiyla bul.
            if (cargoBed == null) cargoBed = transform.Find("CargoBed");
        }

        private void OnDestroy()
        {
            // Abonelik kaldirilmazsa statik kuyrukta OLU nesne kalir ve
            // sonraki her kargo degisiminde yok nesne cagrilir.
            GameEvents.CargoChanged -= OnCargoChanged;

            if (_boxMaterial != null) Kill(_boxMaterial);
            _boxMaterial = null;
        }

        /// <summary>
        /// Kargo sistemini elle baglar. Kurulum rolu bunu cagirir; elle
        /// baglanmazsa manifest ilk kargo olayinda sahneden bulunur.
        /// </summary>
        public void Configure(CargoSystem cargo)
        {
            _manifest = cargo != null ? cargo.Manifest : null;
            Rebuild(_manifest);
        }

        /// <summary>Test kancasi: kasa referansini dogrudan atar.</summary>
        public void SetCargoBedForTests(Transform bed) => cargoBed = bed;

        private void OnCargoChanged()
        {
            if (_manifest == null) _manifest = ResolveManifest();
            Rebuild(_manifest);
        }

        /// <summary>
        /// Manifest'i sahneden bulur. Arac prefabinda kablo olmadigi icin
        /// (CompositionRoot araci sinif referansi tutmuyor) tek yol bu.
        /// </summary>
        private static CargoManifest ResolveManifest()
        {
            var root = FindFirstObjectByType<GameCompositionRoot>();
            return root != null ? root.Cargo?.Manifest : null;
        }

        // ------------------------------------------------------------------
        // Yerlesim
        // ------------------------------------------------------------------

        /// <summary>
        /// Manifest'teki yuklu kargolari sirayla kasaya dizer.
        ///
        /// Kutular once yan yana (izgara), sonra ust uste (katman) yerlestirilir;
        /// boylece tek kat dolmadan yukariga cikilir ve kasa "yigilmis" gorunur.
        /// </summary>
        public void Rebuild(CargoManifest manifest)
        {
            ClearBoxes();

            // Manifest yoksa / bos kasa referansi varsa kutu uretilmez:
            // oyun acilirken henuz kargo sistemi kurulmamis olabilir.
            if (manifest == null || cargoBed == null) return;

            IReadOnlyList<Parcel> loaded = manifest.Loaded;
            if (loaded == null || loaded.Count == 0) return;

            int columns = 1;
            int rows = 1;
            int inLayer = 0;
            float pitch = 0f;
            float rowCenterZ = 0f;
            float top = 0f;

            for (int i = 0; i < loaded.Count; i++)
            {
                Parcel parcel = loaded[i];
                if (parcel == null) continue;
                if (_boxes.Count >= maxVisibleBoxes) break;

                float side = BoxSideFor(parcel.VolumeLiters);
                pitch = side + Mathf.Max(0f, gap);

                // Izgara olculeri kutuya gore degisir: kucuk kutu daha cok
                // sigar, buyuk kutu daha az. Satirlar dolunca ust katmana
                // cikilir, boylece kasa "yigilmis" gorunur.
                if (inLayer == 0)
                {
                    columns = GridCount(bedSize.x, pitch);
                    rows = GridCount(bedSize.y, pitch);
                    rowCenterZ = RowsCenterZ(rows, pitch);
                }

                int row = inLayer / columns;
                int column = inLayer % columns;

                // Izgara kasanin icinde kalir: yatayda ortalar, derinlikte
                // kasanin icine dogru siralanir.
                float x = ColumnX(column, columns, pitch);
                float z = rowCenterZ - row * pitch;
                float y = top + side * 0.5f;

                CreateBox(parcel, side, new Vector3(x, y, z));

                // Sonraki katman, bu katmandaki en yuksek kutunun ustunden
                // baslar: ust uste tam oturur.
                top += side;
                inLayer++;
                if (inLayer >= columns * rows) inLayer = 0;
            }
        }

        /// <summary>Verilen eksende kac adet kutu sigar (en az 1).</summary>
        private static int GridCount(float extent, float pitch)
        {
            return Mathf.Max(1, Mathf.FloorToInt(Mathf.Max(0.05f, extent) / pitch));
        }

        private static float ColumnX(int column, int columns, float pitch)
        {
            float used = (columns - 1) * pitch;
            return -used * 0.5f + column * pitch;
        }

        private static float RowsCenterZ(int rows, float pitch)
        {
            float used = (rows - 1) * pitch;
            return used * 0.5f;
        }

        /// <summary>
        /// Kargonun hacmine gore kutu kenar uzunlugu (metre).
        ///
        /// 1 litre = 0.001 m3. Hacmin kuba kokunden bir kenar bulunur, sonra
        /// karton kabin kalinligi kadar bir pay eklenir. Hepsi ayni olcekte
        /// olsaydi "bu kocaman, bu ucuz kutu" ayrimi kaybolurdu.
        /// </summary>
        public static float BoxSideFor(float volumeLiters)
        {
            float cubicMeters = Mathf.Max(0.001f, volumeLiters) * 0.001f;
            float side = Mathf.Pow(cubicMeters, 1f / 3f) + 0.09f;
            return Mathf.Clamp(side, MinBoxSide, MaxBoxSide);
        }

        /// <summary>Agirlik arttikca kutu koyulasir (0 = acik, 1 = koyu).</summary>
        public static Color ColorFor(float weightKg)
        {
            float t = Mathf.InverseLerp(LightWeightKg, HeavyWeightKg, weightKg);

            // Tonlar arayuz paletinden turetilir: Accent (sicak karton) ve
            // Danger (koyu kahve). Yeni dosya acmadan tutarli kutu rengi.
            Color light = new Color(UITheme.Accent.r, UITheme.Accent.g * 0.92f, UITheme.Accent.b * 0.55f);
            Color heavy = new Color(UITheme.Danger.r * 0.44f, UITheme.Danger.g * 0.32f, UITheme.Danger.b * 0.26f);
            return Color.Lerp(light, heavy, t);
        }

        private void CreateBox(Parcel parcel, float side, Vector3 localPosition)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "CargoBox_" + parcel.Id;
            go.layer = gameObject.layer;

            // Kutu gorseldir: arac fizigi elle degistirilmemeli, ilk itkide
            // kutu aracin altina girip araci devirmesin.
            Collider box = go.GetComponent<Collider>();
            if (box != null) Kill(box);

            go.transform.SetParent(cargoBed, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = new Vector3(side, side, side);

            ApplyColor(go, ColorFor(parcel.WeightKg));
            _boxes.Add(go);
        }

        private void ApplyColor(GameObject box, Color color)
        {
            MeshRenderer renderer = box.GetComponent<MeshRenderer>();
            if (renderer == null) return;

            if (_boxMaterial == null) _boxMaterial = CreateBoxMaterial();
            if (_boxMaterial != null) renderer.sharedMaterial = _boxMaterial;

            // Materyali paylasmadan renk degistirilir: butun kutular tek
            // materyali kullanir.
            //
            // MaterialPropertyBlock ALAN OLARAK TUTULMAZ. Unity, MonoBehaviour
            // kurucusunda (veya alan baslaticisinda) UnityEngine nesnesi
            // olusturulmasina izin vermiyor: "CreateImpl is not allowed to be
            // called from a MonoBehaviour constructor". Yerelde uretilir -
            // zaten ucuza bir struct kopyasi.
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
            renderer.SetPropertyBlock(block);
        }

        private static Material CreateBoxMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                            ?? Shader.Find("Universal Render Pipeline/Simple Lit");
            if (shader == null) return null;   // primitive materyali kalir

            var material = new Material(shader) { name = "VanCargoBox" };
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.08f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            return material;
        }

        /// <summary>Yuklenen tum kutulari kaldirir (havuz yok, kutu sayisi kucuk).</summary>
        private void ClearBoxes()
        {
            for (int i = 0; i < _boxes.Count; i++)
                if (_boxes[i] != null) Kill(_boxes[i]);

            _boxes.Clear();
        }

        /// <summary>
        /// Editor'de Destroy yasak oldugu icin yikim turunce gore secilir.
        /// Testler EditMode'da calistigi icin bu ayr onemli.
        /// </summary>
        private static void Kill(Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }
    }
}
