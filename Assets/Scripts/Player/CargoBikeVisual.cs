using System.Collections.Generic;
using KargoSim.Cargo;
using KargoSim.Core;
using UnityEngine;

namespace KargoSim.Player
{
    /// <summary>
    /// 3 tekerli kargo bisikletinin arka kasasinda duran GERCEK kutular.
    ///
    /// Sorun: kargo sistemi kac kutu yuklendigini biliyor ama bisikletin
    /// kasasi bos bir cerceve olarak duruyordu; oyuncu "kargoyu yukledim"
    /// dediginde hicbir sey gorunmuyordu. Cozum: kasada kutularin gercekten
    /// istiflenmesi.
    ///
    /// VanCargoVisual ile ayni isi yapar, uc noktada ayrilir:
    ///
    ///  1) RENK PALETI KODUN ICINDE. VanCargoVisual kutu rengini UITheme'den
    ///     turuyor; arayuz bir kozmetik kaynagi degil. Arayuzun kapali
    ///     oldugu bir build'de bisikletin kasasi renksiz kalmamali.
    ///
    ///  2) IZGARA KATMAN KATMAN kurulur ve ayni katmandaki kutular AYNI
    ///     yukseklikte durur. Van gorselinde her kutu biraz daha yuksege
    ///     konuluyor (merdiven); kasa dar ve derin oldugu icin bizim
    ///     merdiven gorseli oyuncuyu kasanin icinde yigilmis gibi
    ///     gostermiyordu.
    ///
    ///  3) KASA KUCUK. Bisiklet kasasi arac govdesinden belirgin dar;
    ///     vanin izgarasi (1.55 x 1.85 m) buraya sigmazdi.
    ///
    /// Update() YOK: gorsel tamamen olay g driven'dir. Kargo degisince
    /// <see cref="GameEvents.CargoChanged"/> haber verir, manifest okunur
    /// ve kutular bastan kurulur. Kimsenin bunu elle cagirma sorumlulugu
    /// yoktur.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CargoBikeVisual : MonoBehaviour
    {
        [Header("Kasa")]
        [Tooltip("Kasa boslugunun bos oldugu cocuk nesne. Kutular bunun cocugu olur.")]
        [SerializeField] private Transform cargoBed;

        [Tooltip("Kasa icinin en/boy olculeri (metre). Izgara buradan hesaplanir.")]
        [SerializeField] private Vector2 bedSize = new Vector2(0.95f, 1.15f);

        [Tooltip("Iki kutu arasindaki bosluk (metre).")]
        [SerializeField] private float gap = 0.04f;

        [Tooltip("En fazla gosterilecek kutu. Kasa dolarsa fazlasi gorsel olarak kirpilir.")]
        [SerializeField] private int maxVisibleBoxes = 24;

        // Kutu olculeri: hacimden yana bir kenar. Hepsi ayni olcekte
        // olsaydi kargonun yogunlugu hissedilmezdi.
        private const float MinBoxSide = 0.16f;
        private const float MaxBoxSide = 0.46f;

        // Agirlik -> renk araligi. Hafif kutu aci karton, agir kutu koyu.
        private const float LightWeightKg = 0.5f;
        private const float HeavyWeightKg = 8f;

        private static readonly Color LightBoxColor = new Color(0.82f, 0.63f, 0.36f);
        private static readonly Color HeavyBoxColor = new Color(0.26f, 0.17f, 0.10f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private readonly List<GameObject> _boxes = new List<GameObject>();

        private CargoManifest _manifest;
        private Material _boxMaterial;
        private bool _subscribed;

        /// <summary>Kasada duran kutu sayisi (test ve tani icin).</summary>
        public int VisibleBoxCount => _boxes.Count;

        /// <summary>Kutular yukleme sirasina gore sirali (test ve tani icin).</summary>
        public IReadOnlyList<GameObject> VisibleBoxes => _boxes;

        /// <summary>Kasa boslugu. Bos ise kutu uretilmez.</summary>
        public Transform CargoBed => cargoBed;

        /// <summary>Kasa ic olculeri (x = en, y = boy). Izgara buradan gelir.</summary>
        public Vector2 BedSize => bedSize;

        // ------------------------------------------------------------------
        // Editor kurulum kancalari
        // ------------------------------------------------------------------

        /// <summary>
        /// Kasa referansini atar. Editor kurulum araci (CargoBikeSetupTool)
        /// bunu cagirir; Inspector'a elle yazmaya gerek kalmasin diye ozel
        /// kanca. <see cref="SetCargoBedForTests"/> ile ayni isi yapar,
        /// yalnizca niyeti belirtir.
        /// </summary>
        public void ConfigureForSetup(Transform bed)
        {
            if (bed != null) cargoBed = bed;
        }

        /// <summary>Test kancas: kasa referansini dogrudan atar.</summary>
        public void SetCargoBedForTests(Transform bed) => cargoBed = bed;

        /// <summary>
        /// Kargo sistemini baglar. CompositionRoot cagirir; elle
        /// baglanmazsa manifest ilk kargo olayinda sahneden bulunur.
        /// </summary>
        public void Configure(CargoSystem cargo)
        {
            _manifest = cargo != null ? cargo.Manifest : null;
            Rebuild(_manifest);
        }

        // ------------------------------------------------------------------
        // Unity yasam dongusu
        // ------------------------------------------------------------------

        private void Awake()
        {
            // Statik olay kuyruguna baglanmak yeterli: UIManager veya
            // baska bir sistem CargoBikeVisual'i tanimak zorunda degil.
            Subscribe();

            // Prefab'da elle atanmissa dokunma; yoksa adiyla bul.
            if (cargoBed == null) cargoBed = transform.Find("CargoBed");
        }

        private void OnDestroy()
        {
            // Abonelik kaldirilmazsa statik kuyrukta OLU nesne kalir ve
            // sonraki her kargo degisiminde yok nesne cagrilir.
            Unsubscribe();

            if (_boxMaterial != null) Kill(_boxMaterial);
            _boxMaterial = null;
        }

        private void Subscribe()
        {
            if (_subscribed) return;
            GameEvents.CargoChanged += OnCargoChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            GameEvents.CargoChanged -= OnCargoChanged;
            _subscribed = false;
        }

        private void OnCargoChanged()
        {
            if (_manifest == null) _manifest = ResolveManifest();
            Rebuild(_manifest);
        }

        /// <summary>
        /// Manifest'i sahneden bulur. Arac prefabinda kablo olmadigi icin
        /// (CompositionRoot arac sinif referansi tutmuyor) tek yol bu.
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
        /// Once yan yana (izgara), katman dolunca ustune (katman) konur;
        /// boylece kasa "yigilmis" gorunur. Ayni katmandaki kutular ayni
        /// yukseklikte durur.
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
            float layerBase = 0f;    // bu katmanin taban yuksekligi
            float layerHeight = 0f;  // bu katmandaki en yuksek kutu
            float pitch = 0f;
            float rowCenterZ = 0f;

            for (int i = 0; i < loaded.Count; i++)
            {
                Parcel parcel = loaded[i];
                if (parcel == null) continue;
                if (_boxes.Count >= maxVisibleBoxes) break;

                float side = BoxSideFor(parcel.VolumeLiters);
                pitch = side + Mathf.Max(0f, gap);

                // Yeni katman basliyorsa izgara olculeri burada bir kez
                // hesaplanir: kucuk kutu daha cok, buyuk kutu daha az sigar.
                if (inLayer == 0)
                {
                    columns = GridCount(bedSize.x, pitch);
                    rows = GridCount(bedSize.y, pitch);
                    rowCenterZ = RowsCenterZ(rows, pitch);
                    layerHeight = 0f;
                }

                int row = inLayer / columns;
                int column = inLayer % columns;

                // Izgara kasanin icinde kalir: yatayda ortalar, derinlikte
                // kasanin icine dogru siralanir.
                float x = ColumnX(column, columns, pitch);

                // ASAMA 16: satirlar ARKADAN one dogru doldurulur.
                //
                // Oyuncu "kargo merkezinde kaliyor", "kutular aracin arkasina
                // yerlestirilmiyor" dedi. Sebep burasiydi: satirlar
                // rowCenterZ ile ORTAYA yerlestiriliyordu, bu yuzden TEK
                // kutu katanin on kenari (bisiklet yerel z=-0.61) duruyordu
                // - yani arka tekerin (-0.72) ONUNDE, kasanin disinda.
                //
                // Simdi ilk kutu kasanin ARKA kenarindan baslar ve one
                // dogru ilerler: tek kutu z=-1.29, kasa icinde.
                float z = -rowCenterZ + row * pitch;
                float y = layerBase + side * 0.5f;

                CreateBox(parcel, side, new Vector3(x, y, z));

                if (side > layerHeight) layerHeight = side;

                inLayer++;
                if (inLayer >= columns * rows)
                {
                    // Katman doldu: sonraki katman bu katmanin ustunden baslar.
                    inLayer = 0;
                    layerBase += layerHeight;
                }
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
            return Color.Lerp(LightBoxColor, HeavyBoxColor, t);
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

            var material = new Material(shader) { name = "CargoBikeBox" };
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
