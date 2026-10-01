using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace KargoSim.World
{
    /// <summary>
    /// GORSEL KATMAN: sehir, dukan, evler, sokaklar, NPC.
    ///
    /// NEDEN AYRI DOSYA, NEDEN RUNTIME'DA:
    /// 1) Bu projede sahne/prefab YAML'i elle duzenlendi ve UC KEZ
    ///    bozuldu (kamera baglantilari, tag'ler, RectTransform'lar).
    ///    Bu yuzden gorsel duzenleme koda tasindi.
    /// 2) Mevcut nesnelere SADECE COCUK EKLER; hicbir seyi SILMEZ,
    ///    tasirmaz. Boylece oyunun calistigi geometri bozulmaz.
    ///
    /// OLCULEN SORUNLAR (ekran goruntusu ile dogrulandi):
    ///  - Dukkan: onu tamamen acik bir kutu. Vitrin, tabela, sundurma,
    ///    kapi, kaldirim YOK.
    ///  - Evler: tek parca dolu kutu. Pencere, kapi, cati, kat bandi YOK.
    ///  - Sokak: duz koyu serit. Yol cizgisi, kenar borduru YOK.
    ///  - NPC: beyaz Capsule. Yuz, sac, uzuv, kiyafet YOK.
    ///  - Zemin: 200x200 tek gri kutu. Cim rengi/dokusu YOK.
    ///  - Aydinlatma: tek Directional, ambient cok koyu, sisi yok.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CityVisualUpgrade : MonoBehaviour
    {
        private const int LayerBuilding = 10;
        private const int LayerRoad = 9;

        private readonly List<Object> _made = new List<Object>();

        public static int ObjectsAdded { get; private set; }

        // ---------------------------------------------------------------
        // PALET
        // ---------------------------------------------------------------
        private static readonly Color[] SkinTones =
        {
            new Color(0.94f, 0.79f, 0.68f),
            new Color(0.87f, 0.70f, 0.57f),
            new Color(0.76f, 0.57f, 0.44f),
            new Color(0.55f, 0.39f, 0.28f),
            new Color(0.40f, 0.28f, 0.21f),
        };

        private static readonly Color[] HairTones =
        {
            new Color(0.10f, 0.08f, 0.07f),
            new Color(0.25f, 0.16f, 0.09f),
            new Color(0.48f, 0.33f, 0.16f),
            new Color(0.70f, 0.60f, 0.42f),
            new Color(0.72f, 0.72f, 0.74f),
        };

        private static readonly Color[] ShirtTones =
        {
            new Color(0.24f, 0.44f, 0.72f),
            new Color(0.78f, 0.30f, 0.28f),
            new Color(0.28f, 0.56f, 0.36f),
            new Color(0.88f, 0.68f, 0.20f),
            new Color(0.55f, 0.30f, 0.62f),
            new Color(0.90f, 0.88f, 0.84f),
        };

        private static readonly Color[] PantsTones =
        {
            new Color(0.18f, 0.20f, 0.26f),
            new Color(0.30f, 0.30f, 0.34f),
            new Color(0.42f, 0.34f, 0.26f),
            new Color(0.20f, 0.32f, 0.40f),
        };

        private static readonly Color[] RoofTones =
        {
            new Color(0.52f, 0.24f, 0.20f),
            new Color(0.38f, 0.30f, 0.28f),
            new Color(0.44f, 0.34f, 0.22f),
            new Color(0.30f, 0.32f, 0.38f),
        };

        private static readonly Color[] WallTones =
        {
            new Color(0.86f, 0.80f, 0.68f),
            new Color(0.80f, 0.70f, 0.62f),
            new Color(0.74f, 0.78f, 0.74f),
            new Color(0.88f, 0.84f, 0.76f),
            new Color(0.70f, 0.68f, 0.72f),
        };

        private static readonly Color Glass = new Color(0.45f, 0.62f, 0.75f);
        private static readonly Color FrameWhite = new Color(0.92f, 0.91f, 0.87f);
        private static readonly Color Grass = new Color(0.30f, 0.44f, 0.24f);
        private static readonly Color KerbGrey = new Color(0.62f, 0.61f, 0.58f);
        private static readonly Color KerbDark = new Color(0.44f, 0.43f, 0.41f);
        private static readonly Color LineWhite = new Color(0.88f, 0.88f, 0.84f);
        private static readonly Color ShopAccent = new Color(0.82f, 0.34f, 0.24f);
        private static readonly Color ShopSign = new Color(0.13f, 0.42f, 0.40f);
        private static readonly Color Awning = new Color(0.85f, 0.30f, 0.26f);

        // ---------------------------------------------------------------
        // ORTAK MALZEME HAVUZU (draw call dogrusu: malzeme cesidi sinirli)
        // ---------------------------------------------------------------
        private readonly Dictionary<Color, Material> _mats =
            new Dictionary<Color, Material>();

        private Material Mat(Color c)
        {
            Color key = new Color(
                Mathf.Round(c.r * 40f) / 40f,
                Mathf.Round(c.g * 40f) / 40f,
                Mathf.Round(c.b * 40f) / 40f, 1f);

            Material m;
            if (_mats.TryGetValue(key, out m) && m != null) return m;

            Shader sh = Shader.Find("Universal Render Pipeline/Lit")
                     ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                     ?? Shader.Find("Standard");

            m = new Material(sh) { name = "CityVis_" + ColorUtility.ToHtmlStringRGB(key) };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", key);
            if (m.HasProperty("_Color")) m.SetColor("_Color", key);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.12f);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.12f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);

            _mats[key] = m;
            _made.Add(m);
            return m;
        }

        /// <summary>
        /// CAPI: buyuk kutulari (ev, yol parcasi, zemin) SIFIR
        /// OLCEKLI bir kaplayicinin altina alir.
        ///
        /// NEDEN GEREKLI: sahnedeki her sey Unity "Cube" primitifidir
        /// ve geometriyi `localScale` ile tasir. Bir cocuk Box'un
        /// olcegini MIRAS ALIR; 9 m genisligindeki bir evin penceresi
        /// 0.95 m degil 0.95*9 = 8.5 m olur, cati de goge ucar.
        ///
        /// Bu yuzden detaylar 1/S olcekli kaplayicinin icine konur:
        ///   worldOlcek = S * (1/S) * detay = detay
        /// Boylece detaylar "insan olceginde" kalir.
        /// </summary>
        private Transform DetailContainer(Transform parent, string name)
        {
            if (parent == null) return null;

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.layer = parent.gameObject.layer;

            Vector3 s = parent.localScale;
            go.transform.localScale = new Vector3(
                Mathf.Abs(s.x) < 0.0001f ? 1f : 1f / s.x,
                Mathf.Abs(s.y) < 0.0001f ? 1f : 1f / s.y,
                Mathf.Abs(s.z) < 0.0001f ? 1f : 1f / s.z);
            return go.transform;
        }

        /// <summary>
        /// Paylasilan kupe mesh'i.
        ///
        /// ONCEKI HATA: `Box()` sadece `MeshRenderer` ekleyen bos bir
        /// GameObject yaratiyordu. MeshRenderer olmasi TEK BASINA
        /// HICBIR SEY CIZMAZ - bir MeshFilter + mesh gerekir. Bu
        /// yuzden 1500+ parca eklendi ve HICBIRI gorunmedi; sadece
        /// elle mesh verilen catilar ve kureler cikti.
        /// </summary>
        private Mesh _cubeMesh;
        private Mesh CubeMesh()
        {
            if (_cubeMesh != null) return _cubeMesh;

            var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tmp.name = "__kup_mesh_kaynagi";
            tmp.hideFlags = HideFlags.HideAndDontSave;
            _cubeMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
            return _cubeMesh;
        }

        private GameObject Box(Transform parent, string name, Vector3 localPos,
                               Vector3 size, Color color, int layer = LayerBuilding)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = size;
            go.layer = layer;
            go.GetComponent<MeshFilter>().sharedMesh = CubeMesh();
            go.GetComponent<MeshRenderer>().sharedMaterial = Mat(color);
            ObjectsAdded++;
            return go;
        }

        private GameObject Sphere(Transform parent, string name, Vector3 localPos,
                                  Vector3 size, Color color, int layer = LayerBuilding)
        {
            var mf = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mf.name = name;
            // `Destroy` YALNIZCA oyun modunda calisir; editor araciyla
            // (ekran goruntusu) calistiginda sessizce birakir ve
            // kureler kolliderle birlikte gelir.
            Collider col = mf.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }
            mf.transform.SetParent(parent, false);
            mf.transform.localPosition = localPos;
            mf.transform.localScale = size;
            mf.layer = layer;
            mf.GetComponent<MeshRenderer>().sharedMaterial = Mat(color);
            ObjectsAdded++;
            return mf;
        }

        // ===============================================================
        // ANA GIRIS
        // ===============================================================
        private void Start() => Apply();

        /// <summary>
        /// Gorselleri kur. Hem oyunda (Start) hem de editor araciyla
        /// (ekran goruntusu) cagrilabilir olmasi icin public.
        /// </summary>
        public void Apply()
        {
            if (ObjectsAdded > 0) return;   // tekrar calistirma

            DressGround();
            BuildHouses();
            BuildRoadMarkings();
            BuildShop();
            HumanizeCustomers();
            TuneLighting();

            Debug.Log("[CityVisualUpgrade] " + ObjectsAdded
                      + " gorsel parca eklendi (dukkan/ev/sokak/zemin/NPC).");
        }

        // ---------------------------------------------------------------
        // ZEMIN: 200x200 gri kutuyu cim yapisina cevir
        // ---------------------------------------------------------------
        private void DressGround()
        {
            GameObject ground = GameObject.Find("Ground");
            if (ground == null) return;

            var mr = ground.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = Mat(Grass);

            // Zemin 200x200 tek parca; kenarinda gorus bittigi icin
            // ufukta gri bant gorunuyor. Dis cevreye toprak/alan ekle.
            Transform root = DetailContainer(ground.transform, "Cevre");
            if (root == null) return;
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 2f / 8f;
                Vector3 p = new Vector3(Mathf.Cos(a), -0.4f, Mathf.Sin(a)) * 96f;
                Box(root, "Cevre_" + i, p, new Vector3(60f, 2f, 60f),
                    new Color(0.26f, 0.36f, 0.22f), 8);
            }
        }

        // ---------------------------------------------------------------
        // EVLER: pencere, kapi, cati, kat bandi
        // ---------------------------------------------------------------
        private void BuildHouses()
        {
            Transform city = GameObject.Find("CityDecor")?.transform;
            Transform buildings = city != null ? city.Find("Buildings") : null;
            if (buildings == null) return;

            int index = 0;
            for (int i = 0; i < buildings.childCount; i++)
            {
                Transform b = buildings.GetChild(i);
                if (!b.name.StartsWith("Bina_")) continue;

                var mr = b.GetComponent<MeshRenderer>();
                Vector3 size = b.localScale;
                float w = size.x, h = size.y, d = size.z;

                // Mevcut tek kutuyu dokunmadan USTUNE detay ekliyoruz.
                // DIKKAT: bina localScale ile olceklendirilmis bir Cube;
                // caplayici olcegi sifirlar, aksi halde pencere 8 m olur.
                Transform detay = DetailContainer(b, "Detay");
                if (detay == null) continue;

                // --- Cati: eikimli iki egme + mahya
                BuildPitchedRoof(detay, w, d, h, RoofTones[index % RoofTones.Length]);

                // --- Kat bandi (her kat bir cizgi)
                int floors = Mathf.Max(1, Mathf.RoundToInt(h / 3.2f));
                for (int f = 1; f < floors; f++)
                {
                    float y = -h * 0.5f + h * f / floors;
                    Box(detay, "KatBandi_" + f, new Vector3(0f, y, 0f),
                        new Vector3(w + 0.12f, 0.14f, d + 0.12f), FrameWhite);
                }

                // --- Pencereler: dort yuz
                AddWindows(detay, w, h, d, floors, index);

                // --- Kapi: en genis yuzun -Z tarafi
                AddDoor(detay, w, h, d, index);

                index++;
            }
        }

        private void BuildPitchedRoof(Transform b, float w, float d, float h,
                                      Color roofColor)
        {
            // Mahya (cati tepe cizgisi) uzunlugu: kisa kenar.
            float span = Mathf.Min(w, d);
            float ridge = span * 0.5f;
            float rise = Mathf.Min(1.5f, span * 0.32f);
            float len = Mathf.Max(w, d);
            float overhang = 0.35f;
            bool ridgeAlongZ = d >= w;

            var roof = new GameObject("Catı");
            roof.transform.SetParent(b, false);
            roof.layer = LayerBuilding;
            var rmr = roof.AddComponent<MeshRenderer>();
            rmr.sharedMaterial = Mat(roofColor);

            roof.AddComponent<MeshFilter>().sharedMesh =
                BuildGableMesh(len + overhang * 2f, span + overhang * 2f,
                           rise, ridgeAlongZ, roofColor);
            rmr.sharedMaterial = Mat(roofColor);
            roof.transform.localPosition = new Vector3(0f, h * 0.5f, 0f);
            roof.transform.localRotation = Quaternion.identity;
            ObjectsAdded++;

            // Baca: her evde olmasin, konuma bagli karar verilsin.
            // DIKKAT: `b` artik "Detay" kaplayicisi; asil ev adi bir
            // ust seviyede. Hash'i oradan aliyoruz, yoksa her evde ayni
            // karar cikar.
            string ownerName = b.parent != null ? b.parent.name : b.name;
            if (Mathf.Abs(Mathf.Sin(index01(ownerName) * 3.1f)) > 0.35f)
            {
                Box(roof.transform, "Baca", new Vector3(len * 0.25f, rise * 0.9f, 0f),
                    new Vector3(0.5f, rise * 1.3f, 0.5f),
                    new Color(0.45f, 0.32f, 0.28f));
            }
        }

        private static float index01(string name)
        {
            int h = 17;
            for (int i = 0; i < name.Length; i++) h = unchecked(h * 31 + name[i]);
            return Mathf.Abs(h % 1000) / 1000f;
        }

        /// <summary>
        /// EIKIMLI CATI MESH'I.
        ///
        /// Unity'de hazir "cati" primitifi YOK; primitives duz kutudur.
        /// Bu yuzden kenar noktalari elle verilip ucgenler uretiliyor.
        /// Onceki hatali denemeler hep "Cube'i rotate et" idi; bu
        /// calismiyordu cunku koseleri acik kaliyordu.
        /// </summary>
        private static Mesh BuildGableMesh(float length, float width, float rise,
                                           bool ridgeAlongZ, Color color)
        {
            float hl = length * 0.5f;
            float hw = width * 0.5f;

            var m = new Mesh { name = "GableRoof" };
            m.indexFormat = IndexFormat.UInt32;

            // Mahya Z boyunca uzanirsa: 4 egme noktasi + 2 mahya ucu.
            var v = new List<Vector3>();
            var t = new List<int>();

            if (ridgeAlongZ)
            {
                v.Add(new Vector3(-hw, 0f, -hl));
                v.Add(new Vector3( hw, 0f, -hl));
                v.Add(new Vector3( hw, 0f,  hl));
                v.Add(new Vector3(-hw, 0f,  hl));
                v.Add(new Vector3(0f, rise, -hl));
                v.Add(new Vector3(0f, rise,  hl));

                Quad(t, 0, 4, 5, 1);   // -Z egme
                Quad(t, 3, 2, 5, 4);   // +Z egme
                Tri(t, 0, 3, 4);       // -X alinlik
                Tri(t, 2, 1, 5);       // +X alinlik
            }
            else
            {
                v.Add(new Vector3(-hl, 0f, -hw));
                v.Add(new Vector3(-hl, 0f,  hw));
                v.Add(new Vector3( hl, 0f,  hw));
                v.Add(new Vector3( hl, 0f, -hw));
                v.Add(new Vector3(-hl, rise, 0f));
                v.Add(new Vector3( hl, rise, 0f));

                Quad(t, 0, 1, 4, 5);   // -Z egme
                Quad(t, 3, 2, 5, 4);   // +Z egme
                Tri(t, 1, 2, 4);       // +Z alinlik
                Tri(t, 3, 0, 5);       // -Z alinlik
            }

            m.SetVertices(v);
            m.SetTriangles(t, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        private static void Quad(List<int> t, int a, int b, int c, int d)
        {
            Tri(t, a, b, c);
            Tri(t, a, c, d);
        }

        private static void Tri(List<int> t, int a, int b, int c)
        {
            t.Add(a); t.Add(b); t.Add(c);
        }

        private void AddWindows(Transform b, float w, float h, float d,
                                int floors, int seed)
        {
            int perFloor = Mathf.Max(1, Mathf.FloorToInt(w / 2.4f));
            float winW = 0.95f, winH = 1.25f;

            for (int f = 0; f < floors; f++)
            {
                float y = -h * 0.5f + h * (f + 0.58f) / floors;
                for (int i = 0; i < perFloor; i++)
                {
                    float x = Mathf.Lerp(-w * 0.5f + 1.2f, w * 0.5f - 1.2f,
                                         perFloor == 1 ? 0.5f : i / (float)(perFloor - 1));
                    bool lit = (seed + f * 3 + i) % 4 != 0;
                    Color glass = lit ? new Color(0.95f, 0.80f, 0.42f) : Glass;

                    // On yuz (-Z) ve arka yuz (+Z)
                    Box(b, "Pencere", new Vector3(x, y, -d * 0.5f - 0.03f),
                        new Vector3(winW, winH, 0.08f), FrameWhite);
                    Box(b, "Cam", new Vector3(x, y, -d * 0.5f - 0.08f),
                        new Vector3(winW - 0.18f, winH - 0.18f, 0.06f), glass);
                    Box(b, "Pencere", new Vector3(x, y, d * 0.5f + 0.03f),
                        new Vector3(winW, winH, 0.08f), FrameWhite);
                    Box(b, "Cam", new Vector3(x, y, d * 0.5f + 0.08f),
                        new Vector3(winW - 0.18f, winH - 0.18f, 0.06f), glass);
                }

                // Yan yuzler
                for (int i = 0; i < perFloor; i++)
                {
                    float z = Mathf.Lerp(-d * 0.5f + 1.0f, d * 0.5f - 1.0f,
                                         perFloor == 1 ? 0.5f : i / (float)(perFloor - 1));
                    bool lit = (seed + f * 5 + i) % 3 != 0;
                    Color glass = lit ? new Color(0.95f, 0.82f, 0.45f) : Glass;

                    Box(b, "Pencere", new Vector3(-w * 0.5f - 0.03f, y, z),
                        new Vector3(0.08f, winH, winW), FrameWhite);
                    Box(b, "Cam", new Vector3(-w * 0.5f - 0.08f, y, z),
                        new Vector3(0.06f, winH - 0.18f, winW - 0.18f), glass);
                    Box(b, "Pencere", new Vector3(w * 0.5f + 0.03f, y, z),
                        new Vector3(0.08f, winH, winW), FrameWhite);
                    Box(b, "Cam", new Vector3(w * 0.5f + 0.08f, y, z),
                        new Vector3(0.06f, winH - 0.18f, winW - 0.18f), glass);
                }
            }
        }

        private void AddDoor(Transform b, float w, float h, float d, int seed)
        {
            float dh = 2.05f;
            float y = -h * 0.5f + dh * 0.5f;
            Color doorColor = (seed % 3 == 0)
                ? new Color(0.35f, 0.20f, 0.16f)
                : new Color(0.22f, 0.30f, 0.42f);

            Box(b, "Kapi", new Vector3(0f, y, -d * 0.5f - 0.06f),
                new Vector3(1.05f, dh, 0.12f), doorColor);
            Box(b, "KapiCerceve", new Vector3(0f, y, -d * 0.5f - 0.02f),
                new Vector3(1.25f, dh + 0.2f, 0.08f), FrameWhite);

            // Basamak
            Box(b, "Basamak", new Vector3(0f, -h * 0.5f + 0.09f, -d * 0.5f - 0.45f),
                new Vector3(1.7f, 0.18f, 0.8f), KerbGrey);

            // Kapı üstü çatılık
            Box(b, "Canik", new Vector3(0f, -h * 0.5f + dh + 0.28f, -d * 0.5f - 0.35f),
                new Vector3(1.5f, 0.16f, 0.7f),
                RoofTones[seed % RoofTones.Length]);
        }

        // ---------------------------------------------------------------
        // SOKAK: orta cizgi + kenar borduru
        // ---------------------------------------------------------------
        private void BuildRoadMarkings()
        {
            Transform city = GameObject.Find("CityDecor")?.transform;
            Transform roads = city != null ? city.Find("Roads") : null;
            if (roads == null) return;

            for (int i = 0; i < roads.childCount; i++)
            {
                Transform r = roads.GetChild(i);
                var mr = r.GetComponent<MeshRenderer>();
                if (mr == null) continue;

                Vector3 s = r.localScale;
                // Depo alani (16x8) ve kucuk parcalar cizilmez.
                // ONCEKI HATA: her yol parcasi iki kenar cizgisi
                // aliyordu; depoya yuzlerce yol parcasi ucu burada
                // bulustugu icin yerde gokkusu bir "yildiz" deseni
                // olusuyordu.
                if (s.x < 6f || s.z < 8f) continue;

                // Yol parcasi da olceklenmis Cube; detaylar caplayiciye.
                Transform detay = DetailContainer(r, "YolDetay");
                if (detay == null) continue;

                // Yolun uzun ekseni Z (yol parcalari Z boyunca uzar).
                bool alongZ = s.z >= s.x;
                float len = alongZ ? s.z : s.x;
                float wid = alongZ ? s.x : s.z;

                // Orta cizgi: kesikli
                int dashes = Mathf.Clamp(Mathf.FloorToInt(len / 4f), 1, 14);
                for (int d = 0; d < dashes; d++)
                {
                    float f = (d + 0.5f) / dashes - 0.5f;
                    Vector3 p = alongZ
                        ? new Vector3(0f, 0.05f, f * len)
                        : new Vector3(f * len, 0.05f, 0f);
                    Vector3 sc = alongZ
                        ? new Vector3(0.16f, 0.02f, 1.5f)
                        : new Vector3(1.5f, 0.02f, 0.16f);
                    Box(detay, "YolCizgisi", p, sc, LineWhite, LayerRoad);
                }

                // Kenar borduru (kaldirim/kerb)
                for (int e = 0; e < 2; e++)
                {
                    float off = (e == 0 ? 1f : -1f) * (wid * 0.5f + 0.10f);
                    Vector3 p = alongZ
                        ? new Vector3(off, 0.06f, 0f)
                        : new Vector3(0f, 0.06f, off);
                    Vector3 sc = alongZ
                        ? new Vector3(0.18f, 0.12f, len)
                        : new Vector3(len, 0.12f, 0.18f);
                    Box(detay, "Kerb", p, sc, KerbDark, LayerRoad);
                }
            }
        }

        // ---------------------------------------------------------------
        // DUKKAN: vitrin, tabela, sundurma, kapi, kaldirim
        // ---------------------------------------------------------------
        private void BuildShop()
        {
            Transform shop = GameObject.Find("Shop")?.transform;
            if (shop == null) return;

            Vector3 roof = Vector3.zero;
            Transform roofT = shop.Find("Roof");
            if (roofT != null) roof = roofT.localScale;
            float w = Mathf.Max(9f, roof.x);
            float h = 4.35f;
            float d = Mathf.Max(7f, roof.z);
            float halfW = w * 0.5f;

            // --- Vitrin cercevesi: on yuz (-Z yonundeki aciklik)
            // Onceki durumda on tamamen acikti; oyuncu iceri giriyordu
            // ama disaridan bakildiginda "bina degil, bos kutu" idi.
            Box(shop, "VitrinUst", new Vector3(0f, 3.55f, d * 0.5f - 0.15f),
                new Vector3(w - 0.6f, 0.7f, 0.35f), FrameWhite);
            Box(shop, "VitrinSol", new Vector3(-halfW + 0.45f, 1.75f, d * 0.5f - 0.15f),
                new Vector3(0.55f, 3.5f, 0.35f), FrameWhite);
            Box(shop, "VitrinSag", new Vector3(halfW - 0.45f, 1.75f, d * 0.5f - 0.15f),
                new Vector3(0.55f, 3.5f, 0.35f), FrameWhite);

            // Cam: yalnizca UST kisminda, altinda gecis acik kalsin
            // (oyuncu rafla teblaslasmadan girip cikiyor).
            Box(shop, "VitrinCam", new Vector3(0f, 2.9f, d * 0.5f - 0.32f),
                new Vector3(w - 1.7f, 1.15f, 0.08f),
                new Color(0.55f, 0.72f, 0.82f, 1f));

            // --- Tabela
            //
            // DERINLIK SIRASI ONEMLI: kamera +Z'den bakiyor, yani
            // BUYUK z = ONDE. Cerceve tahtanin ONUNE konulursa
            // (onceki hata) tabelanin tamamini gri bir levhayla
            // kapatir ve tabela bom bos gorunur.
            float tZ = d * 0.5f;
            Box(shop, "TabelaCerceve", new Vector3(0f, 4.05f, tZ + 0.02f),
                new Vector3(5.0f, 1.15f, 0.14f), FrameWhite);
            Box(shop, "TabelaTahta", new Vector3(0f, 4.05f, tZ + 0.12f),
                new Vector3(4.8f, 0.95f, 0.12f), ShopSign);

            // Tabela "yazisi": iki kutu ve bir nokta, harf yerine.
            for (int i = -1; i <= 1; i++)
            {
                Box(shop, "TabelaYazi", new Vector3(i * 0.85f, 4.05f, tZ + 0.20f),
                    new Vector3(0.55f, 0.34f, 0.05f), FrameWhite);
            }

            // Tabela isiklari
            for (int i = -1; i <= 1; i++)
            {
                Sphere(shop, "TabelaIsik" + i,
                       new Vector3(i * 0.85f, 4.62f, tZ + 0.16f),
                       Vector3.one * 0.17f, new Color(1f, 0.90f, 0.60f));
            }

            // --- Sundurma (tent)
            //
            // ONCEKI HATA: (a) cati mesh'i 90 derece donduruluyordu,
            // 9.6 m'lik DIKEY kirmizi sutun cikiyordu; (b) duz ve
            // kalin bir tabla konunca "raf" gibi gorunuyordu.
            // Simdi: ince, dik eikimli, cizgili (gercek tente).
            float awnW = w - 1.0f;
            float awnD = 1.5f;
            Vector3 awnPos = new Vector3(0f, 2.98f, tZ + awnD * 0.5f);

            var awn = Box(shop, "Tent", awnPos,
                          new Vector3(awnW, 0.07f, awnD), Awning);
            awn.transform.localRotation = Quaternion.Euler(-26f, 0f, 0f);

            // Cizgili kumas: beyaz seritler
            int stripes = 9;
            for (int i = 0; i < stripes; i++)
            {
                if (i % 2 == 1) continue;
                float x = Mathf.Lerp(-awnW * 0.5f + 0.3f, awnW * 0.5f - 0.3f,
                                     i / (float)(stripes - 1));
                var st = Box(shop, "TentCizgi", awnPos + new Vector3(x, 0.055f, 0f),
                             new Vector3(awnW / stripes * 0.95f, 0.03f, awnD), FrameWhite);
                st.transform.localRotation = awn.transform.localRotation;
            }

            // Tentin ondeki sarkan kenar
            Box(shop, "TentKenar", new Vector3(0f, 2.42f, tZ + awnD * 0.94f),
                new Vector3(awnW, 0.34f, 0.06f), Awning);

            // Tente askilari
            for (int s = -1; s <= 1; s += 2)
            {
                Box(shop, "TentAskisi",
                    new Vector3(s * (awnW * 0.5f - 0.12f), 3.05f, tZ + awnD * 0.5f),
                    new Vector3(0.07f, 0.07f, awnD), KerbGrey);
            }

            // --- Kaldirim + basamak
            Box(shop, "Kaldirim", new Vector3(0f, 0.08f, d * 0.5f + 2.2f),
                new Vector3(w + 1.6f, 0.16f, 4.4f), KerbGrey, 8);
            Box(shop, "Esik", new Vector3(0f, 0.2f, d * 0.5f + 0.1f),
                new Vector3(w - 0.4f, 0.4f, 0.5f), KerbGrey, 8);

            // --- Kapı (arka duvarda)
            Box(shop, "PersonelKapisi", new Vector3(-halfW + 1.2f, 1.05f, -d * 0.5f + 0.2f),
                new Vector3(1.0f, 2.1f, 0.12f), new Color(0.34f, 0.20f, 0.16f));

            // --- Ic raf dolabi (arka duvar)
            Box(shop, "RafArka", new Vector3(0f, 1.6f, -d * 0.5f + 0.35f),
                new Vector3(w - 1.2f, 2.4f, 0.3f), new Color(0.45f, 0.32f, 0.20f));
            for (int i = 0; i < 3; i++)
            {
                Box(shop, "RafRaf" + i,
                    new Vector3(0f, 0.7f + i * 0.75f, -d * 0.5f + 0.6f),
                    new Vector3(w - 1.6f, 0.08f, 0.45f), new Color(0.55f, 0.38f, 0.22f));
            }

            // --- Isik: vitrindEN isik gelsin
            var ltGo = new GameObject("DukkanIsigi");
            ltGo.transform.SetParent(shop, false);
            ltGo.transform.localPosition = new Vector3(0f, 3.4f, 0f);
            Light lt = ltGo.AddComponent<Light>();
            lt.type = LightType.Point;
            lt.color = new Color(1f, 0.92f, 0.78f);
            lt.intensity = 3.2f;
            lt.range = 11f;
            lt.shadows = LightShadows.None;
            ObjectsAdded++;
        }

        // ---------------------------------------------------------------
        // NPC: beyaz kapsul -> insana benzer figur
        // ---------------------------------------------------------------
        /// <summary>
        /// GORSEL KONTROL: figuru bos bir nesne uzerine kurar.
        ///
        /// Musteriler sadece OYUN MODUNDA doguyor; editorde sahne
        /// acildiginda ortada kapsul bile yok. Bu metot sayesinde
        /// "insana benziyor mu" sorusu ekran goruntusuyle
        /// yanitlanabiliyor.
        /// </summary>
        public GameObject PreviewHuman(int seed, string name = "__preview_npc")
        {
            var go = new GameObject(name);
            go.transform.position = new Vector3(0f, 0f, 0f);
            BuildHuman(go.transform, seed);
            return go;
        }

        private void HumanizeCustomers()
        {
            var customers = Object.FindObjectsByType<Player.ShopCustomer>(
                FindObjectsSortMode.None);
            if (customers == null || customers.Length == 0)
            {
                // Runtime'da dogarlar; editor'de sahne acikken olmayabilir.
                return;
            }

            for (int i = 0; i < customers.Length; i++)
            {
                Transform t = customers[i].transform;

                // Var olan Capsule: collider ODAK SISTEMI icin gerekli,
                // SILINMEZ; sadece gorunmez yapilir.
                var capsule = t.GetComponentInChildren<MeshRenderer>();
                if (capsule != null) capsule.enabled = false;

                BuildHuman(t, i);
            }
        }

        /// <summary>
        /// INSANA BENZER FIGUR: sac, yuz, govde, kol, bacak, ayakkabi.
        ///
        /// NEDEN PRIMITIVE: projede tek insan modeli (Kenney FBX) var
        /// ama o sahnede serilestirilmis; runtime'da AssetDatabase
        /// kullanilamaz. Primitive'lardan kurulan figur stil olarak
        /// oyunun geri kalaniyla (kutu/kure) tutarli.
        /// </summary>
        private void BuildHuman(Transform root, int seed)
        {
            float s = 0.94f + (seed % 3) * 0.05f;          // boy
            Color skin = SkinTones[seed % SkinTones.Length];
            Color hair = HairTones[(seed + 2) % HairTones.Length];
            Color shirt = ShirtTones[seed % ShirtTones.Length];
            Color pants = PantsTones[(seed + 1) % PantsTones.Length];

            var body = new GameObject("Insan");
            body.transform.SetParent(root, false);
            body.transform.localScale = Vector3.one * s;

            // Bacaklar
            Box(body.transform, "BacakSol", new Vector3(-0.11f, 0.42f, 0f),
                new Vector3(0.17f, 0.84f, 0.19f), pants);
            Box(body.transform, "BacakSag", new Vector3(0.11f, 0.42f, 0f),
                new Vector3(0.17f, 0.84f, 0.19f), pants);
            Box(body.transform, "AyakSol", new Vector3(-0.11f, 0.045f, 0.03f),
                new Vector3(0.19f, 0.09f, 0.28f), new Color(0.16f, 0.15f, 0.16f));
            Box(body.transform, "AyakSag", new Vector3(0.11f, 0.045f, 0.03f),
                new Vector3(0.19f, 0.09f, 0.28f), new Color(0.16f, 0.15f, 0.16f));

            // Govde: omuzlari genis, beli dar
            Box(body.transform, "Govde", new Vector3(0f, 1.14f, 0f),
                new Vector3(0.42f, 0.62f, 0.24f), shirt);
            Box(body.transform, "Omuz", new Vector3(0f, 1.40f, 0f),
                new Vector3(0.46f, 0.14f, 0.25f), shirt);
            Box(body.transform, "Bel", new Vector3(0f, 0.85f, 0f),
                new Vector3(0.36f, 0.16f, 0.22f), pants);

            // Kollar
            Box(body.transform, "KolSol", new Vector3(-0.27f, 1.12f, 0f),
                new Vector3(0.13f, 0.58f, 0.14f), shirt);
            Box(body.transform, "KolSag", new Vector3(0.27f, 1.12f, 0f),
                new Vector3(0.13f, 0.58f, 0.14f), shirt);
            Box(body.transform, "ElSol", new Vector3(-0.27f, 0.80f, 0f),
                new Vector3(0.12f, 0.14f, 0.13f), skin);
            Box(body.transform, "ElSag", new Vector3(0.27f, 0.80f, 0f),
                new Vector3(0.12f, 0.14f, 0.13f), skin);

            // Boyun + kafa
            Box(body.transform, "Boyun", new Vector3(0f, 1.48f, 0f),
                new Vector3(0.12f, 0.10f, 0.12f), skin);
            Sphere(body.transform, "Kafa", new Vector3(0f, 1.66f, 0f),
                   new Vector3(0.25f, 0.29f, 0.25f), skin);

            // Sac: kafanin ustunde kep
            Sphere(body.transform, "Sac", new Vector3(0f, 1.735f, -0.005f),
                   new Vector3(0.27f, 0.19f, 0.27f), hair);

            // Yuz: gozler (kamera hep onunden bakar)
            Box(body.transform, "GozSol", new Vector3(-0.052f, 1.675f, -0.118f),
                new Vector3(0.035f, 0.045f, 0.02f), new Color(0.10f, 0.10f, 0.12f));
            Box(body.transform, "GozSag", new Vector3(0.052f, 1.675f, -0.118f),
                new Vector3(0.035f, 0.045f, 0.02f), new Color(0.10f, 0.10f, 0.12f));
            Box(body.transform, "Agiz", new Vector3(0f, 1.60f, -0.122f),
                new Vector3(0.06f, 0.018f, 0.02f), new Color(0.55f, 0.28f, 0.26f));

            ObjectsAdded += 18;
        }

        // ---------------------------------------------------------------
        // AYDINLATMA / ATMOSFER
        // ---------------------------------------------------------------
        private void TuneLighting()
        {
            // Ambient cok koyu oldugu icin golge taraflari simsiyah.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.52f, 0.58f, 0.68f);
            RenderSettings.ambientEquatorColor = new Color(0.42f, 0.40f, 0.36f);
            RenderSettings.ambientGroundColor = new Color(0.20f, 0.18f, 0.15f);
            RenderSettings.ambientIntensity = 1f;

            // Uzaklik sisi: 95 m yaricapindaki adresler sisinir, ufuk
            // gri bant olmaktan cikar.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 55f;
            RenderSettings.fogEndDistance = 210f;
            RenderSettings.fogColor = new Color(0.66f, 0.74f, 0.82f);

            // Gunesi yumusaklastir: sert golgeler oyunu kirli gosteriyordu.
            Light sun = Object.FindFirstObjectByType<Light>();
            if (sun != null)
            {
                sun.color = new Color(1f, 0.95f, 0.86f);
                sun.intensity = 1.35f;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.72f;
                sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            }
        }
    }
}
