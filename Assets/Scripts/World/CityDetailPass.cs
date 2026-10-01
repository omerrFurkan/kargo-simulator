using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace KargoSim.World
{
    /// <summary>
    /// GORSEL KATMAN 2: 1. turda kalan eksikler.
    ///
    /// OLCULEREK CIKARILDI, TAHMIN DEGIL:
    /// 1) Adres 9'un binasi YOK. 18 bina var, `Bina_9` listede degil.
    ///    Sebep: `CitySetupTool.TryPlaceBuilding` 18 acı x 6 mesafe
    ///    dener, hepsi cakisirsa binayi SESSIZCE ATAR. Sonuc: teslimat
    ///    adresi var, evi yok. Oyuncu o eve gidiyor, karsisinda bostan
    ///    bosluk.
    /// 2) Adres kapilarinda NPC FIGURU YOK. `NPCSystem` saf veri
    ///    sinifi; hic `GameObject` uretmiyor. Kapida sadece
    ///    "Musteri evde degil" METNI cikiyor.
    /// 3) Lamba isik vermiyor. `CityLampGlow` emissive malzeme ama
    ///    sahne/`CitySetupTool`da HICBIR `Light` bileseni yok.
    /// 4) Agaclar tek Sphere ("lolipop").
    /// 5) Dukkan catisi duz kutu; evler gibi egimli degil.
    /// 6) Ufukta camurlu gri bant (zemin bitiyor, sisi yetersiz).
    /// </summary>
    public static class CityDetailPass
    {
        public static int ObjectsAdded { get; private set; }

        private static readonly Dictionary<Color, Material> _mats =
            new Dictionary<Color, Material>();
        private static Mesh _cube;
        private static Mesh _sphere;

        // ---------------------------------------------------------------
        private static Material Mat(Color c)
        {
            Color key = new Color(Mathf.Round(c.r * 40f) / 40f,
                                  Mathf.Round(c.g * 40f) / 40f,
                                  Mathf.Round(c.b * 40f) / 40f, 1f);
            Material m;
            if (_mats.TryGetValue(key, out m) && m != null) return m;

            Shader sh = Shader.Find("Universal Render Pipeline/Lit")
                     ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                     ?? Shader.Find("Standard");
            m = new Material(sh) { name = "CityVis2_" + ColorUtility.ToHtmlStringRGB(key) };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", key);
            if (m.HasProperty("_Color")) m.SetColor("_Color", key);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.12f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            _mats[key] = m;
            return m;
        }

        private static Mesh Cube()
        {
            if (_cube != null) return _cube;
            GameObject t = GameObject.CreatePrimitive(PrimitiveType.Cube);
            t.hideFlags = HideFlags.HideAndDontSave;
            _cube = t.GetComponent<MeshFilter>().sharedMesh;
            return _cube;
        }

        private static Mesh SphereMesh()
        {
            if (_sphere != null) return _sphere;
            GameObject t = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            t.hideFlags = HideFlags.HideAndDontSave;
            _sphere = t.GetComponent<MeshFilter>().sharedMesh;
            return _sphere;
        }

        private static GameObject Box(Transform p, string n, Vector3 pos,
                                      Vector3 size, Color c, int layer = 10)
        {
            var go = new GameObject(n, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(p, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            go.layer = layer;
            go.GetComponent<MeshFilter>().sharedMesh = Cube();
            go.GetComponent<MeshRenderer>().sharedMaterial = Mat(c);
            ObjectsAdded++;
            return go;
        }

        private static GameObject Ball(Transform p, string n, Vector3 pos,
                                       Vector3 size, Color c, int layer = 10)
        {
            var go = new GameObject(n, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(p, false);
            go.transform.localPosition = pos;
            go.transform.localScale = size;
            go.layer = layer;
            go.GetComponent<MeshFilter>().sharedMesh = SphereMesh();
            go.GetComponent<MeshRenderer>().sharedMaterial = Mat(c);
            ObjectsAdded++;
            return go;
        }

        private static int Hash(string s)
        {
            unchecked { int h = 17; for (int i = 0; i < s.Length; i++) h = h * 31 + s[i]; return Mathf.Abs(h); }
        }

        // ===============================================================
        public static void Run()
        {
            BuildTrees();
            PitchShopRoof();
            FillMissingHouses();
            SpawnAddressResidents();
            AddLampLights();
            FixHorizon();

            Debug.Log("[CityDetailPass] " + ObjectsAdded
                      + " parca eklendi (agac/cati/eksik ev/kapi NPC/lamba/ufuk).");
        }

        // ---------------------------------------------------------------
        // 1) AGACLAR: tek kure yerine kademeli tac
        // ---------------------------------------------------------------
        private static void BuildTrees()
        {
            Transform city = GameObject.Find("CityDecor")?.transform;
            Transform trees = city != null ? city.Find("Trees") : null;
            if (trees == null) return;

            Color[] leaf = {
                new Color(0.22f, 0.40f, 0.18f),
                new Color(0.27f, 0.46f, 0.21f),
                new Color(0.31f, 0.50f, 0.24f),
            };
            Color bark = new Color(0.38f, 0.28f, 0.20f);

            for (int i = 0; i < trees.childCount; i++)
            {
                Transform t = trees.GetChild(i);
                if (!t.name.StartsWith("Agac_")) continue;

                // DIKKAT: her agac IKI nesnedir: `Agac_NN_Govde` (kutu)
                // ve `Agac_NN_Tac` (kure). ONCEKI HATA: ikisine de
                // detay eklendi; hem golge hem taca taç kondu ve
                // agaclar 5 m'ye sişti. Sadece GOVDE islenir, eski
                // tek kure ise GORUNMEZ yapilir.
                bool isTrunk = t.name.EndsWith("Govde");
                if (!isTrunk) continue;

                // Eski "lolipop" taci kapat.
                string crownName = t.name.Substring(0, t.name.Length - "Govde".Length) + "Tac";
                Transform oldCrown = trees.Find(crownName);
                if (oldCrown != null)
                {
                    var oldMr = oldCrown.GetComponent<MeshRenderer>();
                    if (oldMr != null) oldMr.enabled = false;
                }

                if (t.Find("Detay") != null) continue;

                Vector3 s = t.localScale;
                // Govde kutusu: 0.45 x 2.2 x 0.45. Yerel metre
                // cinsinden konumlar 1/S ile duzeltilir.
                var detay = new GameObject("Detay");
                detay.transform.SetParent(t, false);
                detay.transform.localScale = new Vector3(
                    Mathf.Abs(s.x) < 1e-4f ? 1f : 1f / s.x,
                    Mathf.Abs(s.y) < 1e-4f ? 1f : 1f / s.y,
                    Mathf.Abs(s.z) < 1e-4f ? 1f : 1f / s.z);

                int h = Hash(t.name);
                Color c = leaf[h % leaf.Length];
                float crown = 1.7f + (h % 7) * 0.07f;   // 1.7 - 2.1 m
                float trunk = 0.5f;                     // govde yaricapindan

                // 3 kademeli, asagiya dogru genisleyen koni
                Ball(detay.transform, "Tacl1", new Vector3(0f, trunk + crown * 0.35f, 0f),
                     new Vector3(crown, crown * 0.78f, crown), c);
                Ball(detay.transform, "Tacl2",
                     new Vector3(crown * 0.20f, trunk + crown * 0.70f, -crown * 0.14f),
                     new Vector3(crown * 0.74f, crown * 0.58f, crown * 0.74f),
                     new Color(c.r * 1.12f, c.g * 1.12f, c.b * 1.12f));
                Ball(detay.transform, "Tacl3",
                     new Vector3(-crown * 0.22f, trunk + crown * 0.52f, crown * 0.16f),
                     new Vector3(crown * 0.62f, crown * 0.50f, crown * 0.62f),
                     new Color(c.r * 0.86f, c.g * 0.86f, c.b * 0.86f));

                // Dallar
                Box(detay.transform, "DalSol", new Vector3(-0.16f, trunk * 0.9f, 0f),
                    new Vector3(0.06f, 0.30f, 0.06f), bark);
                Box(detay.transform, "DalSag", new Vector3(0.16f, trunk * 1.15f, 0f),
                    new Vector3(0.06f, 0.26f, 0.06f), bark);
            }
        }

        // ---------------------------------------------------------------
        // 2) DUKKAN CATISI: duz tabla -> egimli
        // ---------------------------------------------------------------
        private static void PitchShopRoof()
        {
            Transform shop = GameObject.Find("Shop")?.transform;
            if (shop == null) return;
            Transform roof = shop.Find("Roof");
            if (roof == null || roof.Find("CatıDetay") != null) return;

            Vector3 s = roof.localScale;
            float w = s.x, d = s.z;
            Vector3 c = roof.localPosition;

            // Eski duz tabla KALIR (tavan olarak); uzerine egimli cati.
            //
            // ONCEKI HATA: kaplayiciya 1/s olcegi verildi ama `s.y`
            // = 0.3 (cati tabaninin kalinligi) oldugu icin 1/s.y = 3.3
            // carpani bacayi 1.8 m degil 6 m yapti: dukkandin gecen
            // dev bir sivri kule cikti. Burada OLCEK BIRIMDIR (Shop
            // kokunun olcegi 1), olcek dondurulmez; cocuk boyutlari
            // dogrudan metre cinsindendir.
            var g = new GameObject("CatıDetay");
            g.transform.SetParent(shop, false);
            g.transform.localPosition = new Vector3(c.x, c.y + s.y * 0.5f, c.z);
            g.transform.localScale = Vector3.one;
            g.layer = 10;

            g.AddComponent<MeshFilter>().sharedMesh = Gable(w + 0.5f, d + 0.5f, 1.6f, true);
            g.AddComponent<MeshRenderer>().sharedMaterial =
                RoofMat(new Color(0.46f, 0.22f, 0.18f));
            ObjectsAdded++;

            // Oluk (yağmur oluğu)
            Box(g.transform, "OlukOn", new Vector3(0f, -0.06f, d * 0.5f + 0.28f),
                new Vector3(w + 0.5f, 0.12f, 0.14f), new Color(0.85f, 0.85f, 0.82f));
            // Baca
            Box(g.transform, "Baca", new Vector3(w * 0.3f, 0.9f, 0f),
                new Vector3(0.55f, 1.8f, 0.55f), new Color(0.45f, 0.32f, 0.28f));
        }

        /// <summary>
        /// EGIMLI CATI MESH'I.
        ///
        /// ONCEKI HATA: `ridgeAlongZ` dalinda dortgenler yanlis
        /// noktalara birlestirilmis; dort noktasi da ayni z
        /// duzleminde olan ucgenler cikti ve catinin ucunda ince
        /// koyu bir diken belirdi. Asagida mahya yonune gore
        /// GERCEK yuzler veriliyor.
        ///
        /// Ayrica mesh iki yonlu cizilir (her ucgenin ters yuzu de
        /// eklenir) ve malzeme cull-off; boylece normal yonu
        /// hatasinda kara parca kaybolmaz.
        /// </summary>
        private static Mesh Gable(float length, float width, float rise,
                                  bool ridgeAlongZ)
        {
            float hl = length * 0.5f, hw = width * 0.5f;
            var v = new List<Vector3>();
            var t = new List<int>();

            if (ridgeAlongZ)
            {
                // Mahya Z boyunca: kenar dikdortgeni + mahya uc noktalari
                v.Add(new Vector3(-hw, 0f, -hl));   // 0 sol-on
                v.Add(new Vector3( hw, 0f, -hl));   // 1 sag-on
                v.Add(new Vector3( hw, 0f,  hl));   // 2 sag-arka
                v.Add(new Vector3(-hw, 0f,  hl));   // 3 sol-arka
                v.Add(new Vector3(0f, rise, -hl));  // 4 mahya-on
                v.Add(new Vector3(0f, rise,  hl));  // 5 mahya-arka

                Quad(t, 0, 3, 5, 4);   // -X egmen
                Quad(t, 1, 2, 5, 4);   // +X egmen
                Tri(t, 0, 1, 4);       // -Z alinlik
                Tri(t, 2, 3, 5);       // +Z alinlik
            }
            else
            {
                // Mahya X boyunca
                v.Add(new Vector3(-hl, 0f, -hw));  // 0 on-sol
                v.Add(new Vector3(-hl, 0f,  hw));  // 1 arka-sol
                v.Add(new Vector3( hl, 0f,  hw));  // 2 arka-sag
                v.Add(new Vector3( hl, 0f, -hw));  // 3 on-sag
                v.Add(new Vector3(-hl, rise, 0f));  // 4 mahya-sol
                v.Add(new Vector3( hl, rise, 0f));  // 5 mahya-sag

                Quad(t, 0, 3, 5, 4);   // -Z egmen
                Quad(t, 1, 2, 5, 4);   // +Z egmen
                Tri(t, 0, 1, 4);       // -X alinlik
                Tri(t, 3, 2, 5);       // +X alinlik
            }

            // Iki yonlu: her ucgenin tersini de ekle
            int n = t.Count;
            for (int i = 0; i < n; i += 3)
            {
                t.Add(t[i + 2]);
                t.Add(t[i + 1]);
                t.Add(t[i]);
            }

            var m = new Mesh { name = "Gable2" };
            m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(v);
            m.SetTriangles(t, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Cift yonlu (cull off) malzeme: kara delik olmasin.</summary>
        private static Material RoofMat(Color c)
        {
            Material m = Mat(c);
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
            return m;
        }

        private static void Tri(List<int> t, int a, int b, int c, int d)
        {
            t.Add(a); t.Add(b); t.Add(c);
            t.Add(a); t.Add(c); t.Add(d);
        }

        private static void Quad(List<int> t, int a, int b, int c, int d)
        {
            t.Add(a); t.Add(b); t.Add(c);
            t.Add(a); t.Add(c); t.Add(d);
        }

        private static void Tri(List<int> t, int a, int b, int c)
        {
            t.Add(a); t.Add(b); t.Add(c);
        }

        // ---------------------------------------------------------------
        // 3) EKSIK EV: adresi var, binasi olmayan noktalar
        // ---------------------------------------------------------------
        private static void FillMissingHouses()
        {
            Transform buildings = GameObject.Find("CityDecor")?.transform.Find("Buildings");
            if (buildings == null) return;

            var have = new HashSet<int>();
            for (int i = 0; i < buildings.childCount; i++)
            {
                string n = buildings.GetChild(i).name;      // Bina_<id>_<b>
                string[] p = n.Split('_');
                if (p.Length >= 2 && int.TryParse(p[1], out int id)) have.Add(id);
            }

            var stops = Object.FindObjectsByType<Player.AddressDoor>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (stops == null) return;

            for (int i = 0; i < stops.Length; i++)
            {
                int id = IdFromName(stops[i].transform);
                if (id < 0 || have.Contains(id)) continue;

                // Kapinin 7-8 m onunde, yola bakmayan bir ev kur.
                Vector3 p = stops[i].transform.position
                          + new Vector3(0f, 0f, -7.6f);
                CreateHouse(buildings, "Bina_" + id + "_0", p, id);
                have.Add(id);
                Debug.Log("[CityDetailPass] Adres " + id + " icin EKSIK olan ev "
                          + "tamamlandi. Oyuncu bu teslim adresine gidiyordu "
                          + "ama karsisinda hicbir sey yoktu.");
            }
        }

        /// <summary>
        /// Adres numarasini nesne adindan bulur.
        ///
        /// ONCEKI HATA: bilesen `Door_4` uzerinde degil, alt cocuk
        /// `Interact` uzerindeydi. Sadece kendi adina bakan kod
        /// "Interact" goruyor ve hicbir adresi eslesmiyordu; bu
        /// yuzden EKSIK EV eklenmedi ve ekran goruntusundeki mesaj
        /// hic cikmadi. Bu yuzden yukari dogru da bakiliyor.
        /// </summary>
        private static int IdFromName(Transform t)
        {
            for (int up = 0; t != null && up < 4; up++, t = t.parent)
            {
                string n = t.name;
                int i = n.LastIndexOf('_');
                if (i < 0) continue;
                string tail = n.Substring(i + 1);
                if (int.TryParse(tail, out int v)) return v;
            }
            return -1;
        }

        private static void CreateHouse(Transform parent, string name, Vector3 at,
                                        int seed)
        {
            int h = Hash(name);
            float w = 7.2f + (h % 5) * 0.4f;
            float d = 4.8f + (h % 3) * 0.4f;
            float floors = (h % 3 == 0) ? 3f : 2f;
            float height = floors * 3.2f;

            Color[] walls = {
                new Color(0.80f, 0.72f, 0.60f),
                new Color(0.72f, 0.60f, 0.54f),
                new Color(0.76f, 0.80f, 0.76f),
            };
            Color wall = walls[h % walls.Length];

            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = at;
            root.layer = 10;

            // Govde: Unity kutu MERKEZDE, taban -h/2
            Box(root.transform, "Govde", new Vector3(0f, height * 0.5f, 0f),
                new Vector3(w, height, d), wall);

            // Egrimli cati
            var roof = new GameObject("Catı");
            roof.transform.SetParent(root.transform, false);
            roof.transform.localPosition = new Vector3(0f, height, 0f);
            roof.layer = 10;
            roof.transform.localScale = new Vector3(1f / w, 1f / 1f, 1f / d);
            roof.AddComponent<MeshFilter>().sharedMesh =
                Gable(w + 0.5f, d + 0.5f, 1.5f, d >= w);
            roof.AddComponent<MeshRenderer>().sharedMaterial =
                RoofMat(new Color(0.48f, 0.24f, 0.20f));
            ObjectsAdded++;

            // Kat bantlari
            for (int f = 1; f < floors; f++)
            {
                Box(root.transform, "KatBandi" + f, new Vector3(0f, height * f / floors, 0f),
                    new Vector3(w + 0.12f, 0.14f, d + 0.12f),
                    new Color(0.92f, 0.91f, 0.87f));
            }

            // Pencereler
            int per = Mathf.Max(1, Mathf.FloorToInt(w / 2.4f));
            for (int f = 0; f < floors; f++)
            {
                float y = height * (f + 0.58f) / floors;
                for (int i = 0; i < per; i++)
                {
                    float x = Mathf.Lerp(-w * 0.5f + 1.2f, w * 0.5f - 1.2f,
                                         per == 1 ? 0.5f : i / (float)(per - 1));
                    bool lit = (h + f * 3 + i) % 4 != 0;
                    Color glass = lit ? new Color(0.95f, 0.82f, 0.45f)
                                      : new Color(0.45f, 0.62f, 0.75f);
                    Box(root.transform, "Cerceve", new Vector3(x, y, d * 0.5f + 0.03f),
                        new Vector3(0.95f, 1.25f, 0.08f), new Color(0.92f, 0.91f, 0.87f));
                    Box(root.transform, "Cam", new Vector3(x, y, d * 0.5f + 0.08f),
                        new Vector3(0.77f, 1.07f, 0.06f), glass);
                }
            }

            // Kapi + basamak + canik
            float dh = 2.05f;
            Box(root.transform, "Kapi", new Vector3(0f, dh * 0.5f, d * 0.5f + 0.06f),
                new Vector3(1.05f, dh, 0.12f), new Color(0.30f, 0.18f, 0.15f));
            Box(root.transform, "Cerceve", new Vector3(0f, dh * 0.5f, d * 0.5f + 0.02f),
                new Vector3(1.25f, dh + 0.2f, 0.08f), new Color(0.92f, 0.91f, 0.87f));
            Box(root.transform, "Basamak", new Vector3(0f, 0.09f, d * 0.5f + 0.45f),
                new Vector3(1.7f, 0.18f, 0.8f), new Color(0.62f, 0.61f, 0.58f));
            Box(root.transform, "Canik", new Vector3(0f, dh + 0.28f, d * 0.5f + 0.35f),
                new Vector3(1.5f, 0.16f, 0.7f), new Color(0.46f, 0.22f, 0.18f));
        }

        // ---------------------------------------------------------------
        // 4) KAPI SAKINLERI: insana benzer figur
        // ---------------------------------------------------------------
        private static void SpawnAddressResidents()
        {
            Color[] skin = {
                new Color(0.94f, 0.79f, 0.68f), new Color(0.87f, 0.70f, 0.57f),
                new Color(0.76f, 0.57f, 0.44f), new Color(0.55f, 0.39f, 0.28f),
                new Color(0.40f, 0.28f, 0.21f),
            };
            Color[] hair = {
                new Color(0.10f, 0.08f, 0.07f), new Color(0.25f, 0.16f, 0.09f),
                new Color(0.48f, 0.33f, 0.16f), new Color(0.70f, 0.60f, 0.42f),
                new Color(0.72f, 0.72f, 0.74f),
            };
            Color[] shirt = {
                new Color(0.24f, 0.44f, 0.72f), new Color(0.78f, 0.30f, 0.28f),
                new Color(0.28f, 0.56f, 0.36f), new Color(0.88f, 0.68f, 0.20f),
                new Color(0.55f, 0.30f, 0.62f), new Color(0.90f, 0.88f, 0.84f),
            };
            Color[] pants = {
                new Color(0.18f, 0.20f, 0.26f), new Color(0.30f, 0.30f, 0.34f),
                new Color(0.42f, 0.34f, 0.26f), new Color(0.20f, 0.32f, 0.40f),
            };

            var doors = Object.FindObjectsByType<Player.AddressDoor>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (doors == null) return;

            var root = new GameObject("Sakinler");
            for (int i = 0; i < doors.Length; i++)
            {
                int id = IdFromName(doors[i].transform);
                if (id < 0) continue;

                // Kapi ile bitisik, yola bakmayan konum
                Vector3 p = doors[i].transform.position
                          + new Vector3(1.6f + (id % 2) * 0.7f, 0f, 1.5f);

                var go = new GameObject("Sakin_" + id);
                go.transform.SetParent(root.transform, false);
                go.transform.position = p;
                go.transform.rotation = Quaternion.Euler(0f, 190f + (id % 5) * 24f, 0f);
                go.layer = 10;

                float sc = 0.92f + (id % 4) * 0.05f;
                var b = new GameObject("Figur");
                b.transform.SetParent(go.transform, false);
                b.transform.localScale = Vector3.one * sc;

                Color sk = skin[id % skin.Length];
                Color hr = hair[(id + 2) % hair.Length];
                Color sh = shirt[id % shirt.Length];
                Color pa = pants[(id + 1) % pants.Length];

                Box(b.transform, "BacakSol", new Vector3(-0.11f, 0.42f, 0f), new Vector3(0.17f, 0.84f, 0.19f), pa);
                Box(b.transform, "BacakSag", new Vector3(0.11f, 0.42f, 0f), new Vector3(0.17f, 0.84f, 0.19f), pa);
                Box(b.transform, "AyakSol", new Vector3(-0.11f, 0.045f, 0.03f), new Vector3(0.19f, 0.09f, 0.28f), new Color(0.16f, 0.15f, 0.16f));
                Box(b.transform, "AyakSag", new Vector3(0.11f, 0.045f, 0.03f), new Vector3(0.19f, 0.09f, 0.28f), new Color(0.16f, 0.15f, 0.16f));
                Box(b.transform, "Govde", new Vector3(0f, 1.14f, 0f), new Vector3(0.42f, 0.62f, 0.24f), sh);
                Box(b.transform, "Omuz", new Vector3(0f, 1.40f, 0f), new Vector3(0.46f, 0.14f, 0.25f), sh);
                Box(b.transform, "Bel", new Vector3(0f, 0.85f, 0f), new Vector3(0.36f, 0.16f, 0.22f), pa);
                Box(b.transform, "KolSol", new Vector3(-0.27f, 1.12f, 0f), new Vector3(0.13f, 0.58f, 0.14f), sh);
                Box(b.transform, "KolSag", new Vector3(0.27f, 1.12f, 0f), new Vector3(0.13f, 0.58f, 0.14f), sh);
                Box(b.transform, "ElSol", new Vector3(-0.27f, 0.80f, 0f), new Vector3(0.12f, 0.14f, 0.13f), sk);
                Box(b.transform, "ElSag", new Vector3(0.27f, 0.80f, 0f), new Vector3(0.12f, 0.14f, 0.13f), sk);
                Box(b.transform, "Boyun", new Vector3(0f, 1.48f, 0f), new Vector3(0.12f, 0.10f, 0.12f), sk);
                Ball(b.transform, "Kafa", new Vector3(0f, 1.66f, 0f), new Vector3(0.25f, 0.29f, 0.25f), sk);
                Ball(b.transform, "Sac", new Vector3(0f, 1.735f, -0.005f), new Vector3(0.27f, 0.19f, 0.27f), hr);
                Box(b.transform, "GozSol", new Vector3(-0.052f, 1.675f, -0.118f), new Vector3(0.035f, 0.045f, 0.02f), new Color(0.10f, 0.10f, 0.12f));
                Box(b.transform, "GozSag", new Vector3(0.052f, 1.675f, -0.118f), new Vector3(0.035f, 0.045f, 0.02f), new Color(0.10f, 0.10f, 0.12f));
                Box(b.transform, "Agiz", new Vector3(0f, 1.60f, -0.122f), new Vector3(0.06f, 0.018f, 0.02f), new Color(0.55f, 0.28f, 0.26f));

                ObjectsAdded += 18;
            }
        }

        // ---------------------------------------------------------------
        // 5) LAMBALAR: emissive malzeme yerine GERCEK isik
        // ---------------------------------------------------------------
        private static void AddLampLights()
        {
            Transform city = GameObject.Find("CityDecor")?.transform;
            Transform lamps = city != null ? city.Find("Lamps") : null;
            if (lamps == null) return;

            int made = 0;
            for (int i = 0; i < lamps.childCount && made < 10; i++)
            {
                Transform l = lamps.GetChild(i);
                if (!l.name.Contains("Baslik")) continue;
                if (l.Find("Isik") != null) continue;

                var go = new GameObject("Isik");
                go.transform.SetParent(l, false);
                go.layer = 10;
                Light lt = go.AddComponent<Light>();
                lt.type = LightType.Point;
                lt.color = new Color(1f, 0.90f, 0.70f);
                lt.intensity = 2.2f;
                lt.range = 14f;
                lt.shadows = LightShadows.None;   // perf: 10 nokta isik + golge pahali
                made++;
                ObjectsAdded++;
            }
            if (made > 0)
                Debug.Log("[CityDetailPass] " + made + " lamba isigi eklendi. "
                          + "Once sadece parlak MALZEME vardi, isik yoymuyordu.");
        }

        // ---------------------------------------------------------------
        // 6) UFUK: zemini 400 m'ye uzat, sisi kisalt
        // ---------------------------------------------------------------
        private static void FixHorizon()
        {
            RenderSettings.fogStartDistance = 45f;
            RenderSettings.fogEndDistance = 150f;
            RenderSettings.fogColor = new Color(0.72f, 0.80f, 0.87f);

            // Zemin 200x200; adresler r=95'te. 200 m yaricapinda
            // zemin BITIYOR ve arkasinda sis bant gri kaliyordu.
            var ground = GameObject.Find("Ground");
            if (ground != null)
            {
                ground.transform.localScale =
                    new Vector3(ground.transform.localScale.x, 1f,
                                ground.transform.localScale.z);

                // Tam kaplama: 420x420 yeni zemin (mevcutin USTUNE,
                // eskisine dokunmadan).
                var wide = new GameObject("ZeminGenis");
                var mr = wide.AddComponent<MeshFilter>();
                mr.sharedMesh = Cube();
                var m = wide.AddComponent<MeshRenderer>();
                m.sharedMaterial = Mat(new Color(0.30f, 0.44f, 0.24f));
                wide.transform.position = new Vector3(0f, -0.6f, 0f);
                wide.transform.localScale = new Vector3(420f, 1f, 420f);
                wide.layer = 8;
                ObjectsAdded++;
            }
        }
    }
}
