using System;
using System.Collections.Generic;
using KargoSim.Cargo;
using KargoSim.Core;
using UnityEngine;

namespace KargoSim.Player
{
    /// <summary>
    /// Paketleme masasi: kuryenin ELINDEKI kargoyu adim adim paketler.
    ///
    /// KRITIK UX KARARI: kargo masada yere birakilmaz. Kurye kutuyu elinde
    /// tutar, masada E'ye basar, adimlar ilerler, paket teslimata hazir olur.
    /// Boylece akis KESILMEZ: raftan kutu al -> masaya git -> E -> hazir ->
    /// araca gotur. "Masaya birak, sonra tekrar al" akisinda oyuncu her
    /// adimda geri donmek zorunda kalir ve is hissi kaybolur.
    ///
    /// SORUMLULUK AYRIMI: adim kurallari (sira, sure, atlama yasagi)
    /// <see cref="PackagingSystem"/> icindeki SAF C# sinifindadir. Burada
    /// yalnizca "E'ye basildi" olayi o kurallara sorulur. Boylece paketleme
    /// mantigi sahnesiz test edilebilir.
    ///
    /// Update() YOK: mantik tamamen olay g driven. Sure olculmez, adim
    /// yalnizca oyuncu E'ye bastiginda ilerletilir. Bu, oyunun "tek Update"
    /// kuralina uyar ve bos bir metod cagrisini onler.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PackingTable : MonoBehaviour, IInteractable
    {
        [Header("Gorsel")]
        [Tooltip("Kargonun uzerinde calisildigi yuzey. Adima gore burada bir " +
                 "kutu cizilir. Bos birakilirsa masa bos gorunur.")]
        [SerializeField] private Transform _visualRoot;

        [Tooltip("ETKILESIM YARICAPI - YALNIZCA DOKUMAN. Mantikta kullanilmaz. " +
                 "Etkilesim menzilinin sahibi CourierController.interactRadius'tir; " +
                 "iki ayri yaricap tutmak hangisinin gecerli oldugunu " +
                 "belirsizlestirir. Bu deger sadece masanin kureye ne kadar " +
                 "yakininda durulmasi gerektigini anlatir.")]
        [SerializeField] private float workRange = 2.2f;

        // --- Baglantilar (Configure ile enjekte edilir) ---
        private PackagingSystem _packaging;
        private CourierController _courier;
        private Func<float> _timeProvider;
        private bool _subscribed;

        /// <summary>Masada gosterilen kargonun kimligi (yoksa 0).</summary>
        private int _workParcelId;

        /// <summary>Etkilesim yaricapi (dokumasyon amacli, mantikta kullanilmaz).</summary>
        public float WorkRange => workRange;

        /// <summary>Su an masada gosterilen kargonun kimligi (yoksa 0).</summary>
        public int WorkParcelId => _workParcelId;

        /// <summary>
        /// Masayi sistemlere baglar. <see cref="ShopParcelPile.Configure"/> ile
        /// ayni desen: sistem dogrudan verilir, sahne tanimasi burada yapilmaz.
        /// </summary>
        /// <param name="packaging">Adim kurallarinin sahibi (saf C# sinif).</param>
        /// <param name="courier">Eldeki kargonun sahibi kurye.</param>
        /// <param name="visual">Kargonun cizilecegi yuzey (null olabilir).</param>
        /// <param name="timeProvider">
        /// Sistem saati (saniye) saglayici. CompositionRoot simulasyon
        /// saatini (DeliverySystem.Elapsed) vermelidir. VERILMEZSE
        /// <see cref="Time.time"/> kullanilir; bu GECICI bir basitlestirmedir
        /// ve ayrica belirtilmistir: gun saati duraklatildiginda da masadaki
        /// is ilerler. Testler kendi saatlerini enjekte eder, bu yuzden
        /// testlerde saat elle surulur.
        /// </param>
        public void Configure(PackagingSystem packaging, CourierController courier,
                              Transform visual, Func<float> timeProvider = null)
        {
            // Cift baglanti (Configure iki kez cagrilirsa) ayni olaya iki kez
            // abone olmak demektir: her olayda is iki kez calisir.
            Unsubscribe();

            _packaging = packaging;
            _courier = courier;
            if (visual != null) _visualRoot = visual;
            _timeProvider = timeProvider;

            // Kargo degisince (raftan alindi, araca yuklendi, masaya kondu)
            // ve her adim bitince masa goruntusu tazelenir. Tum mantik bu iki
            // olaydan gelir; periyodik bir sorgu yoktur.
            GameEvents.CargoChanged += OnCargoChanged;
            GameEvents.PackagingStepCompleted += OnPackagingStepCompleted;
            _subscribed = true;

            RefreshVisual(FindUnpackaged(_courier));
        }

        /// <summary>
        /// Statik olaylardan cikilir. GameEvents statik bir kuyruk ve
        /// Domain Reload KAPALI calistirilabiliyor; abonelik birakilmazsa
        /// ikinci bir Play'de OLU nesneler calismaya devam eder ve
        /// "NullReferenceException" yagmuruna donusur.
        /// </summary>
        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            if (!_subscribed) return;

            GameEvents.CargoChanged -= OnCargoChanged;
            GameEvents.PackagingStepCompleted -= OnPackagingStepCompleted;
            _subscribed = false;
        }

        /// <summary>Sistem saati. Enjekte edilmediyse gercek zamana dusulur.</summary>
        private float Now => _timeProvider != null ? _timeProvider() : Time.time;

        // ------------------------------------------------------------------
        // IInteractable
        // ------------------------------------------------------------------

        public string Prompt
        {
            get
            {
                if (_packaging == null || _courier == null) return "";
                if (_courier.IsInVehicle) return "";

                if (_courier.CarriedCount <= 0) return "Elden kargon yok";

                Parcel target = FindUnpackaged(_courier);

                // Elde ne varsa paketli: masada yapilacak is yok. Oyuncu
                // E'ye basinca bir sey olmayacagini bilmeli.
                if (target == null) return "Tum paketler hazir";

                string action = PackagingSteps.ActionOf(target.PackagingProgress);

                // Kargo masada degilse ya da adimin suresi dolduysa oyuncu
                // E'ye basabilir.
                if (!_packaging.IsOnTable(target)) return "E: " + action;

                float left = _packaging.RemainingSeconds(target, Now);
                if (left > 0f)
                {
                    // Is suruyor: kalan sure yazilir, boylece oyuncu
                    // "ne kadar bekleyecegim" sorusunu E'ye basmadan gorur.
                    return action + "... (" + left.ToString("0.0") + "s)";
                }

                return "E: " + action;
            }
        }

        public bool CanInteract(CourierController courier)
        {
            // Arac icindeyken masayla etkilesim kurulamaz: kurye ayaktadir.
            if (courier == null || _packaging == null) return false;
            if (courier.IsInVehicle) return false;
            if (courier.CarriedCount <= 0) return false;

            // Elde HAZIR (paketlenmis) kargo varsa masada is yok: kutu zaten
            // teslimata hazir, oyuncu masaya bosuna gelmez.
            return FindUnpackaged(courier) != null;
        }

        public void Interact(CourierController courier)
        {
            if (!CanInteract(courier)) return;

            Parcel target = FindUnpackaged(courier);
            if (target == null) return;

            float now = Now;

            // Kargo masada degilse kon. Daha once masada calisip bosaltilmis
            // bir kargo geri gelirse ilerleme KORUNUR: PackagingSystem.Begin
            // yalnizca hic baslanmamis kargoyu sifirdan baslatir.
            if (!_packaging.IsOnTable(target)) _packaging.Begin(target, now);

            // Suresi dolmadiysa HICBIR sey olmaz: oyuncu E'ye bosuna
            // basmasin, animasyon da oynamasin.
            if (!_packaging.IsStepReady(target, now)) return;

            if (_packaging.Advance(target, now))
            {
                courier.PlayAction("interact-right", 0.35f);
                RefreshVisual(target);
            }
        }

        /// <summary>
        /// Eldeki ILK paketlenmemis kargo.
        ///
        /// Siralama onemsiz degil: kurye uc kutu tasiyorsa masada daima ayni
        /// kutu ilerler, oyuncu "bu hangi kutu" diye takip etmek zorunda
        /// kalmaz.
        ///
        /// "Paketlenebilir mi" sorusu PackagingSystem.CanPackage'a sorulur:
        /// yuklenmis veya teslim edilmis kargo masaya girmez, ama ayni kural
        /// iki yerde bakilmaz.
        /// </summary>
        private Parcel FindUnpackaged(CourierController courier)
        {
            if (courier == null) return null;

            IReadOnlyList<Parcel> carried = courier.CarriedParcels;
            if (carried == null) return null;

            for (int i = 0; i < carried.Count; i++)
            {
                Parcel parcel = carried[i];
                if (parcel == null) continue;
                if (parcel.IsPackaged) continue;
                if (_packaging != null && !_packaging.CanPackage(parcel)) continue;

                return parcel;
            }

            return null;
        }

        // ------------------------------------------------------------------
        // Gorsel: kargonun su anki adimi
        // ------------------------------------------------------------------

        // Renkler kodun icinde: yeni materyal dosyasi acmaya gerek yok.
        // Karton kahverengi, kapali kutu bej, bant sari, etiket beyaz.
        private static readonly Color CardboardColor = new Color(0.72f, 0.52f, 0.32f);
        private static readonly Color BoxColor = new Color(0.85f, 0.76f, 0.60f);
        private static readonly Color TapeColor = new Color(0.93f, 0.85f, 0.45f);
        private static readonly Color LabelColor = new Color(0.95f, 0.95f, 0.93f);
        private static readonly Color CutLineColor = new Color(0.42f, 0.30f, 0.16f);
        private static readonly Color DefaultProductColor = new Color(0.45f, 0.55f, 0.75f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        /// <summary>
        /// Masadaki gorseli kargonun ADIMINA gore yeniden kurar.
        ///
        /// Adim adim gorsel:
        ///   SheetPlaced     duz karton
        ///   Cut             duz karton + kesik cizgisi
        ///   Folded          acik kutu (taban + 4 duvar)
        ///   ProductInside   kisa kenarli kutu + icerideki urun
        ///   Taped           kapali kutu + bant
        ///   Labelled        kapali kutu + bant + etiket
        ///
        /// Adim gorseli "acik/kapa" ile degil, yeniden kurularak degisir.
        /// Gorsel kucuk ve adim basina bir kez degisiyor; hangi cube'in
        /// hangi adimdan kalacagini tutan ayri bir durum makinesi bugun
        /// kazanlacak bir sey degil, yarin eklenmesi muhtemel bir hatadir.
        /// </summary>
        public void RefreshVisual(Parcel parcel)
        {
            _workParcelId = parcel != null ? parcel.Id : 0;

            if (_visualRoot == null) return;

            // EditMode'da gorsel kurulmaz: Object.Destroy edit mode'da
            // calismaz ve konsola hata yazar. Sahne nesnesi ShopSetupTool
            // tarafindan kurulur; gorsel yalnizca oyun ici anlamlidir.
            if (Application.isPlaying == false) return;

            ClearVisual();
            if (parcel == null) return;

            switch (parcel.PackagingProgress)
            {
                case PackagingStep.Cut:
                    BuildSheet();
                    BuildCutLine();
                    break;

                case PackagingStep.Folded:
                    BuildOpenBox(0.16f);
                    break;

                case PackagingStep.ProductInside:
                    // Urun gorunebilsin diye duvarlar kisa.
                    BuildOpenBox(0.07f);
                    BuildProduct(parcel);
                    break;

                case PackagingStep.Taped:
                    BuildClosedBox();
                    BuildTape();
                    break;

                case PackagingStep.Labelled:
                    BuildClosedBox();
                    BuildTape();
                    BuildLabel();
                    break;

                // None ve SheetPlaced ayni gostergeyi paylasir: masada
                // yatay bir karton durur.
                default:
                    BuildSheet();
                    break;
            }
        }

        private void ClearVisual()
        {
            if (_visualRoot == null) return;

            for (int i = _visualRoot.childCount - 1; i >= 0; i--)
            {
                GameObject child = _visualRoot.GetChild(i).gameObject;
                if (child != null) Destroy(child);
            }
        }

        /// <summary>Kesilmemis karton: yatay, ince kutu.</summary>
        private void BuildSheet()
        {
            AddBlock("Sheet", new Vector3(0f, 0.01f, 0f),
                new Vector3(0.62f, 0.02f, 0.44f), CardboardColor);
        }

        /// <summary>Kesik cizgisi: kartonun uzerinde ince koyu bant.</summary>
        private void BuildCutLine()
        {
            AddBlock("CutLine", new Vector3(0f, 0.03f, 0f),
                new Vector3(0.62f, 0.005f, 0.03f), CutLineColor);
        }

        /// <summary>
        /// Acik kutu: taban + dort duvar. Duvar yuksekligi gorseli belirgin
        /// kilar (Folded = yuksek duvar, ProductInside = alcak duvar).
        /// </summary>
        private void BuildOpenBox(float wallHeight)
        {
            AddBlock("BoxBase", new Vector3(0f, 0.01f, 0f),
                new Vector3(0.42f, 0.02f, 0.34f), CardboardColor);

            float y = 0.02f + wallHeight * 0.5f;

            AddBlock("BoxWallFront", new Vector3(0f, y, 0.16f),
                new Vector3(0.42f, wallHeight, 0.02f), CardboardColor);
            AddBlock("BoxWallBack", new Vector3(0f, y, -0.16f),
                new Vector3(0.42f, wallHeight, 0.02f), CardboardColor);
            AddBlock("BoxWallLeft", new Vector3(-0.20f, y, 0f),
                new Vector3(0.02f, wallHeight, 0.34f), CardboardColor);
            AddBlock("BoxWallRight", new Vector3(0.20f, y, 0f),
                new Vector3(0.02f, wallHeight, 0.34f), CardboardColor);
        }

        /// <summary>Kutuya konmus urun (kargo turunun rengi).</summary>
        private void BuildProduct(Parcel parcel)
        {
            Color color = parcel.Type != null ? parcel.Type.color : DefaultProductColor;
            AddBlock("Product", new Vector3(0f, 0.13f, 0f),
                new Vector3(0.30f, 0.20f, 0.24f), color);
        }

        /// <summary>Kapali kutu.</summary>
        private void BuildClosedBox()
        {
            AddBlock("ClosedBox", new Vector3(0f, 0.11f, 0f),
                new Vector3(0.42f, 0.22f, 0.34f), BoxColor);
        }

        /// <summary>Bant: kutunun ustunde capraz iki ince serit.</summary>
        private void BuildTape()
        {
            AddBlock("TapeX", new Vector3(0f, 0.225f, 0f),
                new Vector3(0.44f, 0.008f, 0.05f), TapeColor);
            AddBlock("TapeZ", new Vector3(0f, 0.225f, 0f),
                new Vector3(0.05f, 0.008f, 0.36f), TapeColor);
        }

        /// <summary>Etiket: on yuzde beyaz kucuk kutu.</summary>
        private void BuildLabel()
        {
            AddBlock("Label", new Vector3(0.08f, 0.13f, 0.175f),
                new Vector3(0.16f, 0.11f, 0.01f), LabelColor);
        }

        /// <summary>
        /// Collider'siz kutu uretir.
        ///
        /// Collider neden yok: gorsel parca etkilesim almaz (etkilesim masanin
        /// kendi BoxCollider'i ile kurulur) ve fizik motoruna herkese bir
        /// cubuk daha eklemek sadece maliyettir. Karakter masanin icine
        /// girip gorseli itmez.
        /// </summary>
        private GameObject AddBlock(string blockName, Vector3 localPosition,
                                    Vector3 size, Color color)
        {
            if (_visualRoot == null) return null;

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = blockName;
            Destroy(go.GetComponent<Collider>());

            go.transform.SetParent(_visualRoot, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = size;
            go.transform.localRotation = Quaternion.identity;

            ApplyColor(go, color);
            return go;
        }

        /// <summary>
        /// Rengi renderer'a yazar. Yeni materyal dosyasi ACILMAZ: hepsi ayni
        /// varsayilan materyali paylasir, renk sadece bu renderer'a ozel
        /// olur (ParcelVisual ile ayni desen).
        /// </summary>
        private static void ApplyColor(GameObject go, Color color)
        {
            if (go == null) return;

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer == null) return;

            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            block.SetColor(ColorId, color);
            renderer.SetPropertyBlock(block);
        }

        // ------------------------------------------------------------------
        // Olay tasimalari
        // ------------------------------------------------------------------

        private void OnCargoChanged() => RefreshVisual(FindUnpackaged(_courier));

        private void OnPackagingStepCompleted(Parcel parcel, PackagingStep completed,
                                             PackagingStep current)
        {
            if (parcel == null) return;

            // Yalnizca masada gosterilen kargo icin ilgilenir: adim
            // tamamlanip gorsel bir sonraki kargoya gecmis olabilir.
            if (_workParcelId != 0 && parcel.Id != _workParcelId) return;

            RefreshVisual(parcel);
        }
    }
}
