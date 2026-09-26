using System.Collections.Generic;
using System.IO;
using System.Text;
using KargoSim.Delivery;
using KargoSim.Player;
using KargoSim.Vehicle;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KargoSim.EditorTools
{
    /// <summary>
    /// Sahnenin SEHIR DEKORUNU kurar: yol, bina, agac, sokak lambasi,
    /// kaldirim.
    ///
    /// Neden ayri bir asama: MapLayout yalnizca NOKTA listesi verir. Adresin
    /// noktasi var ama "burada bir ev, once onun onunde bir yol var" bilgisi
    /// yoktur. Bu arac o bilgiyi sahnenin USTUNE kurar; MapLayout asset'i
    /// degismez.
    ///
    /// Iki kural bu dosyanin tamamini belirler:
    ///
    /// 1) CAKISMA YOK. Dekor, DeliveryStops (14 renkli ped), Shop,
    ///    AddressDoors, Courier, PlayerVehicle ve Ground ile ust uste
    ///    binmemelidir. Bunun icin her nesne "serbest alan" testinden gecer;
    ///    testi gecen aday yoksa o konum ATLANIR (baska aday denenir).
    ///
    /// 2) BELIRLENIMCILIK. Agac, lamba ve bina konumlari Mathf.Sin
    ///    tabanli bir seudo-rastgele ile uretilir; tohum sabit. Menuye iki
    ///    kez basmak ayni sehiri verir, her seferinde farkli gormem.
    ///
    /// Menu: Kargo/9. Sehir Dekorunu Kur
    /// </summary>
    public static class CitySetupTool
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";
        private const string DecorRootName = "CityDecor";
        private const string MaterialFolder = "Assets/Art/Materials";

        private const string ShopRootName = "Shop";
        private const string GroundRootName = "Ground";

        // ------------------------------------------------------------------
        // Olculer (metre)
        // ------------------------------------------------------------------

        /// <summary>Yol genisligi. 5 m bir tirlik rahat gecer.</summary>
        private const float RoadWidth = 5f;
        private const float RoadHalfWidth = RoadWidth * 0.5f;

        /// <summary>
        /// Yol kalinligi. Zemine COK YAKIN olmali: StopMarker pedi
        /// 0.11 m yukseklikte, yol onun ALTINDA kalsin ki ped gorunsun.
        /// </summary>
        private const float RoadThickness = 0.06f;

        /// <summary>Bina ile yol kenar arasinda birakilacak pay (m).</summary>
        private const float RoadEdgeMargin = 0.7f;

        private const float PavementHeight = 0.16f;
        private const float FloorHeight = 3.2f;

        /// <summary>
        /// Zemin 200x200 (ust yuzey y = 0), yani yarim genislik 100 m.
        /// Binalar kenara tasmasin diye 99 m'de kesiyoruz.
        /// </summary>
        private const float MaxDecorDistance = 99f;

        /// <summary>Agac sayisi tavani (performans butcesi).</summary>
        private const int MaxTrees = 36;

        /// <summary>
        /// StopMarker ile dekor arasinda olmasi gereken minimum bosluk.
        /// Ped 6x6 m; yaricap 3 m + 1 m pay = 4 m.
        /// </summary>
        private const float MarkerClearance = 0.3f;

        /// <summary>Verify icin: dekor merkezi StopMarker'a bu kadar yakin olmamali.</summary>
        private const float VerifyMarkerDistance = 3f;

        private const float TrunkHeight = 2.2f;
        private const float LampHeight = 4.4f;

        // Katmanlar (ProjectSetupTool ile ayni indeksler)
        private const int LayerRoad = 9;
        private const int LayerBuilding = 10;

        // Ayak izi uzerinde 9 ornek noktasi: merkez + 4 kose + 4 kenar ortasi.
        private const int SampleCount = 9;

        // ------------------------------------------------------------------
        // Menu
        // ------------------------------------------------------------------

        [MenuItem("Kargo/9. Sehir Dekorunu Kur", priority = 8)]
        public static void Build()
        {
            var report = new StringBuilder();
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var root = Object.FindFirstObjectByType<Core.GameCompositionRoot>();
            if (root == null)
            {
                Debug.LogError("[Kargo] GameCompositionRoot yok.");
                return;
            }

            var layoutSo = new SerializedObject(root);
            var layout = layoutSo.FindProperty("mapLayout").objectReferenceValue as MapLayout;
            if (layout == null)
            {
                Debug.LogError("[Kargo] MapLayout atanmadi. Once Kargo/4. Harita kur.");
                return;
            }

            if (layout.deliveryPoints == null || layout.deliveryPoints.Count == 0)
            {
                Debug.LogError("[Kargo] MapLayout bos; dekor uretilemez. Once Kargo/4. Harita kur.");
                return;
            }

            // Yalnizca kendi kokunu sil. Baska hicbir nesneye dokunma.
            RemovePreviousDecor();

            var plan = BuildPlan(layout);
            Palette palette = Palette.Create();

            var decorRoot = new GameObject(DecorRootName);
            decorRoot.transform.position = Vector3.zero;
            Transform decor = decorRoot.transform;

            Transform roadRoot = NewChild("Roads", decor);
            Transform buildRoot = NewChild("Buildings", decor);
            Transform treeRoot = NewChild("Trees", decor);
            Transform lampRoot = NewChild("Lamps", decor);
            Transform paveRoot = NewChild("Pavements", decor);

            // Sira onemli: yollar once cizilir, cunku agac ve lamba
            // yerlesimi yol merkez cizgisine gore hesaplanir.
            BuildRoads(plan, roadRoot, palette, report);
            BuildPavements(plan, paveRoot, palette, report);
            BuildBuildings(plan, buildRoot, palette, report);
            BuildLamps(plan, lampRoot, palette, report);
            BuildTrees(plan, treeRoot, palette, report);

            int totalGameObjects = CountUnder(decor);

            report.AppendLine("  zemin        : " + CountRootObjects(GroundRootName) + " adet (Ground)");
            report.AppendLine("  StopMarker   : " + CountStopMarkers() + " adet (cakisma kontrolu)");
            report.AppendLine("  materyal     : " + palette.MaterialCount + " adet (paylasilan)");
            report.AppendLine("  toplam GameObject: " + totalGameObjects);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            AssetDatabase.SaveAssets();

            Verify(decor, plan, totalGameObjects);

            Debug.Log("[Kargo] Sehir dekoru kuruldu.\n" + report);
        }

        // ------------------------------------------------------------------
        // Temizlik
        // ------------------------------------------------------------------

        /// <summary>
        /// Onceki dekoru siler. Sadece "CityDecor" kokunu hedefler; Shop,
        /// DeliveryStops, AddressDoors gibi baska araclarin urettigi
        /// nesnelere DOKUNMAZ.
        /// </summary>
        private static void RemovePreviousDecor()
        {
            var stale = GameObject.Find(DecorRootName);
            if (stale != null) Object.DestroyImmediate(stale);

            // GameObject.Find yalnizca ILK eslesmeyi dondurur. Menu iki kez
            // calistirilip bir kople birakildiginda ikinci kop kalirdi;
            // bu yuzden tum kokler taranir (DeliverySetupTool deseni).
            GameObject[] roots = EditorSceneManager.GetActiveScene().GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
                if (roots[i].name == DecorRootName) Object.DestroyImmediate(roots[i]);
        }

        // ------------------------------------------------------------------
        // Plan: engel listesi + yol agi
        // ------------------------------------------------------------------

        private static CityPlan BuildPlan(MapLayout layout)
        {
            var plan = new CityPlan();
            Vector3 depot = layout.depotPosition;
            plan.depot = new Vector2(depot.x, depot.z);

            // --- Statik engeller -------------------------------------------
            // Shop: cati sacakli (9+0.6) x (7+0.6) bir dikdortgen.
            var shop = GameObject.Find(ShopRootName);
            if (shop != null)
            {
                plan.obstacles.Add(Rect("Shop", new Vector2(shop.transform.position.x,
                                                            shop.transform.position.z),
                                        new Vector2(5.1f, 3.8f), 0f, 1f));
            }
            else
            {
                Debug.LogWarning("[Kargo] Shop bulunamadi; dukkan engeli atlandi.");
            }

            // Depot: arac burada dogar, doner.
            plan.obstacles.Add(Circle("Depo", plan.depot, 4f, 1f));

            // Oyuncu araci (yerinde durur ama gider).
            var vehicle = Object.FindFirstObjectByType<ArcadeVehicleController>();
            if (vehicle != null)
            {
                Vector3 p = vehicle.transform.position;
                plan.obstacles.Add(Circle("Arac", new Vector2(p.x, p.z), 1.6f, 2.5f));
            }

            var courier = Object.FindFirstObjectByType<CourierController>();
            if (courier != null)
            {
                Vector3 p = courier.transform.position;
                plan.obstacles.Add(Circle("Kurye", new Vector2(p.x, p.z), 0.6f, 2.4f));
            }

            // --- Adres basina engeller -------------------------------------
            for (int i = 0; i < layout.deliveryPoints.Count; i++)
            {
                DeliveryPointData point = layout.deliveryPoints[i];
                Vector2 at = new Vector2(point.position.x, point.position.z);

                plan.addresses.Add(at);
                plan.addressIds.Add(point.id);

                // StopMarker pedi: 6x6 m, yaricap 3 m.
                plan.obstacles.Add(Circle("Ped_" + point.id, at, 3f, MarkerClearance));

                // Adres kapisi: ShopSetupTool kapiyi point + (0,0,2.2)
                // konumuna koyar. Ayni formulu tekrarliyoruz ki bina
                // kapinin USTUNE gelmesin.
                Vector2 door = at + new Vector2(0f, 2.2f);
                plan.doors.Add(door);
                plan.obstacles.Add(Circle("Kapi_" + point.id, door, 0.7f, 1f));

                // Kapinin onundeki etkilesim tetikleyicisi (2.4 x 2.2 m).
                // Uzerine bina kurulursa oyuncu tetikleyiciye giremez.
                plan.obstacles.Add(Rect("Tetik_" + point.id, door + new Vector2(0f, 1.4f),
                                        new Vector2(1.2f, 1.1f), 0f, 0.5f));
            }

            // --- Yol agi ---------------------------------------------------
            List<List<DeliveryPointData>> rings = ClusterByRadius(layout.deliveryPoints, depot);
            BuildRoadNetwork(rings, plan);

            return plan;
        }

        /// <summary>
        /// Noktalari depoya uzakligina gore halkalara ayirir ve her halkanin
        /// icinde aciya gore siralar.
        ///
        /// Neden kova (bucket): harita "izgara" olsa da noktalar tek bir
        /// kure uzerinde degil. Depoya uzakliga gore gruplamak her iki
        /// duzende de calisir ve koda ozel varsayim sokmaz.
        /// </summary>
        private static List<List<DeliveryPointData>> ClusterByRadius(
            List<DeliveryPointData> points, Vector3 depot)
        {
            var buckets = new Dictionary<int, List<DeliveryPointData>>();

            for (int i = 0; i < points.Count; i++)
            {
                Vector3 p = points[i].position;
                float d = Vector2.Distance(new Vector2(p.x - depot.x, p.z - depot.z), Vector2.zero);
                int key = Mathf.RoundToInt(d / 25f);

                if (!buckets.TryGetValue(key, out var list))
                {
                    list = new List<DeliveryPointData>();
                    buckets[key] = list;
                }

                list.Add(points[i]);
            }

            var keys = new List<int>(buckets.Keys);
            keys.Sort();

            var result = new List<List<DeliveryPointData>>();
            Vector2 depotFlat = new Vector2(depot.x, depot.z);

            for (int i = 0; i < keys.Count; i++)
            {
                List<DeliveryPointData> list = buckets[keys[i]];
                if (list.Count < 2) continue;   // tek noktali halka cizilmez

                list.Sort((a, b) => AngleAround(a.position, depotFlat)
                                     .CompareTo(AngleAround(b.position, depotFlat)));
                result.Add(list);
            }

            return result;
        }

        private static float AngleAround(Vector3 p, Vector2 center)
        {
            return Mathf.Atan2(p.x - center.x, p.z - center.y) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Yol agi: her halka icinde kapali cevrim + depoya konusan
        /// kollu yol + halkanarasi baglanti.
        ///
        /// Boylece ag, hem bir "izgara" hem de halka duzeninde bagli kalir:
        /// arac hicbir adrese yalnizca cimenin uzerinden gitmez.
        /// </summary>
        private static void BuildRoadNetwork(List<List<DeliveryPointData>> rings, CityPlan plan)
        {
            var seen = new HashSet<long>();

            for (int r = 0; r < rings.Count; r++)
            {
                List<DeliveryPointData> ring = rings[r];
                int n = ring.Count;

                for (int i = 0; i < n; i++)
                {
                    DeliveryPointData a = ring[i];
                    DeliveryPointData b = ring[(i + 1) % n];
                    AddSegment(plan, seen, a.id, b.id, a.position, b.position);
                }

                // En ic halka depoya dogrudan baglanir.
                if (r == 0)
                {
                    for (int i = 0; i < n; i++)
                    {
                        DeliveryPointData a = ring[i];
                        AddSegment(plan, seen, -1, a.id,
                                   new Vector3(plan.depot.x, 0f, plan.depot.y), a.position);
                    }
                }
                else
                {
                    // Dis halkadaki her nokta bir onceki halkadaki EN YAKIN
                    // noktaya baglanir: ag kesintisiz kalir.
                    List<DeliveryPointData> inner = rings[r - 1];
                    for (int i = 0; i < n; i++)
                    {
                        DeliveryPointData a = ring[i];
                        int best = NearestIndex(a.position, inner);
                        if (best < 0) continue;

                        AddSegment(plan, seen, a.id, inner[best].id, a.position, inner[best].position);
                    }
                }
            }
        }

        private static int NearestIndex(Vector3 position, List<DeliveryPointData> list)
        {
            int best = -1;
            float bestDist = float.MaxValue;

            for (int i = 0; i < list.Count; i++)
            {
                float d = (list[i].position - position).sqrMagnitude;
                if (d >= bestDist) continue;
                bestDist = d;
                best = i;
            }

            return best;
        }

        /// <summary>
        /// Yoldaki iki nokta arasina bir parca ekler. Ayni cift iki kez
        /// cizilmesin diye (kapali cevrimde ilk==son olabiliyor) kimlik
        /// cifti ile elenir.
        /// </summary>
        private static void AddSegment(CityPlan plan, HashSet<long> seen,
                                       int idA, int idB, Vector3 a, Vector3 b)
        {
            long key = ((long)Mathf.Min(idA, idB) << 32) | (uint)Mathf.Max(idA, idB);
            if (!seen.Add(key)) return;

            Vector2 pa = new Vector2(a.x, a.z);
            Vector2 pb = new Vector2(b.x, b.z);
            if (Vector2.Distance(pa, pb) < 4f) return;   // cok kisa parca, gorsel yogunluk

            plan.roads.Add(new RoadSegment(pa, pb));
        }

        // ------------------------------------------------------------------
        // Yollar
        // ------------------------------------------------------------------

        private static void BuildRoads(CityPlan plan, Transform parent, Palette palette,
                                       StringBuilder report)
        {
            int count = 0;

            for (int i = 0; i < plan.roads.Count; i++)
            {
                RoadSegment s = plan.roads[i];
                CreateRoadBox("Road_" + i.ToString("00"), parent, s.A, s.B, palette.Road);
                count++;
            }

            // Depo alani: aracin dogdugu yerde genis bir asfalt paftasi.
            // Duz bir zemin parcasi; hicbir seyin USTUNE binmez.
            CreateBox("Depo_Alani", parent,
                      new Vector3(plan.depot.x, RoadThickness * 0.5f, plan.depot.y + 5f),
                      new Vector3(16f, RoadThickness, 8f), palette.Road, LayerRoad);
            count++;

            report.AppendLine("  yol          : " + count + " parca ("
                              + plan.roads.Count + " segment + 1 depo alani), genislik "
                              + RoadWidth.ToString("0") + " m, kalinlik "
                              + RoadThickness.ToString("0.00") + " m");
        }

        /// <summary>
        /// Iki nokta arasinda, zemine cok yakin ince bir kutu cizer.
        /// Uclardan yarim yol genisligi kadar uzatilir: boylece iki komsu
        /// parca birlesip kopuk gorunmez.
        /// </summary>
        private static void CreateRoadBox(string name, Transform parent,
                                          Vector2 a, Vector2 b, Material material)
        {
            Vector2 delta = b - a;
            float length = delta.magnitude;
            if (length < 0.01f) return;

            Vector2 mid = (a + b) * 0.5f;
            float angle = Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg;
            float total = length + RoadWidth;

            GameObject go = CreateBox(name, parent,
                                      new Vector3(mid.x, RoadThickness * 0.5f, mid.y),
                                      new Vector3(RoadWidth, RoadThickness, total),
                                      material, LayerRoad);

            // Yerel +Z yolun boyunca: aci = Atan2(dx, dz).
            go.transform.localRotation = Quaternion.Euler(0f, angle, 0f);
        }

        // ------------------------------------------------------------------
        // Kaldirim (kapi onu)
        // ------------------------------------------------------------------

        private static void BuildPavements(CityPlan plan, Transform parent, Palette palette,
                                           StringBuilder report)
        {
            int count = 0;

            for (int i = 0; i < plan.doors.Count; i++)
            {
                // Kapinin onune koyulan kisa serit: "kapiya yuru" adimini
                // gorsel olarak destekler. Yolu bilerek KESER; kaldirim
                // yolun uzerine gelir ve ondan 0.10 m yuksekte durur.
                //
                // Derinlik 2.6 m: hem StopMarker pedinden (z = 3) cikip
                // hem de binanin tabanina (z = 5.3) girmez.
                Vector2 door = plan.doors[i];
                Vector2 center = door + new Vector2(0f, 1.9f);

                CreateBox("Kaldirim_" + i.ToString("00"), parent,
                          new Vector3(center.x, PavementHeight * 0.5f, center.y),
                          new Vector3(2.8f, PavementHeight, 2.6f),
                          palette.Pavement, LayerRoad);
                count++;
            }

            report.AppendLine("  kaldirim     : " + count + " adet (kapi onu seridi)");
        }

        // ------------------------------------------------------------------
        // Binalar
        // ------------------------------------------------------------------

        private static void BuildBuildings(CityPlan plan, Transform parent, Palette palette,
                                           StringBuilder report)
        {
            int expected = plan.addresses.Count * 2;
            int placed = 0;
            int skipped = 0;

            for (int i = 0; i < plan.addresses.Count; i++)
            {
                Vector2 at = plan.addresses[i];
                int addressId = plan.addressIds[i];
                Material wall = WallFor(palette, addressId);

                // Adres basina iki bina, farkli boy.
                for (int b = 0; b < 2; b++)
                {
                    int seed = 1000 + addressId * 7 + b * 131;
                    float width = 6.6f + Hash01(seed) * 2.4f;
                    float depth = 4.2f + Hash01(seed + 11) * 1.4f;
                    int floors = b == 0 ? 2 : 3;

                    if (TryPlaceBuilding(plan, at, b == 0 ? 0 : 40, seed, width, depth,
                                         floors, wall, parent, addressId, b))
                    {
                        placed++;
                    }
                    else
                    {
                        skipped++;
                    }
                }
            }

            report.AppendLine("  bina        : " + placed + " adet (" + plan.addresses.Count
                              + " adres x 2), atlanan: " + skipped + "  [beklenen: " + expected + "]");
        }

        /// <summary>
        /// Bir adres icin bina yerlestirmeyi dener. Aday konumlar sirayla
        /// denenir; HEPSI cakisiyorsa o adres icin bina konmaz (baska
        /// konuma zorlama daha kotu sonuc verir).
        ///
        /// Aday listesi once kapiya yakin ve disari bakanlari dener, sonra
        /// genisler. Boylece cogu adres dogru bir kapi onu yerlesimi alir,
        /// kenardaki adresler (dis halkanin zemin sinirina yakin olanlari)
        /// da yine bina alabilir.
        /// </summary>
        private static bool TryPlaceBuilding(CityPlan plan, Vector2 address, float angleOffset,
                                             int seed, float width, float depth, int floors,
                                             Material wall, Transform parent,
                                             int addressId, int variant)
        {
            // 0 derece = +Z (kapi yonu). Sira tercih sirasi.
            float[] angles =
            {
                0f, 25f, -25f, 50f, -50f, 75f, -75f, 100f, -100f,
                120f, -120f, 150f, -150f, 180f,
                angleOffset, angleOffset + 30f, angleOffset - 30f,
                angleOffset + 60f, angleOffset - 60f
            };

            // UZAKLAR ONCE, sonra YAKIN.
            //
            // Sebep: adres NOKTASININ KENDISI yolun uzerinde (yollar
            // teslimat noktalarindan gecer). Bu yuzden serbest alan
            // testi bir ayak izi kenarindan geciyor: binanin MERKEZI
            // pedden en az (pedYaricap 3 + pay 0.3 + ayakIziYaricap ~3.3)
            // ~= 6.6 m uzakta olmali. 4.6 m asla gecemez, 8 m de
            // bircok acida yetmez; bu yuzden 6,6-9,5 m taraniyor.
            float[] distances = { 9.5f, 8.6f, 7.8f, 7.0f, 6.6f, 6.2f };

            // Bina kapiya donuk dursun: genisligi yola degil, kapinin
            // onune baksin diye yonu adrese gore kirdirilir.
            float rotDeg = Mathf.Atan2(address.x - plan.depot.x, address.y - plan.depot.y)
                           * Mathf.Rad2Deg + Hash01(seed + 5) * 12f - 6f;

            Vector2 half = new Vector2(width * 0.5f, depth * 0.5f);

            // ACI ONCE, sonra mesafe: kapiya bakan yonler denenir.
            for (int a = 0; a < angles.Length; a++)
            {
                float rad = angles[a] * Mathf.Deg2Rad;

                for (int d = 0; d < distances.Length; d++)
                {
                    Vector2 offset = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad)) * distances[d];
                    Vector2 center = address + offset;

                    if (!IsFree(plan, center, half, rotDeg, 0.2f, true, RoadEdgeMargin))
                        continue;

                    CreateBuilding(plan, parent, center, half, rotDeg, floors,
                                   wall, addressId, variant);
                    return true;
                }
            }

            return false;
        }

        private static void CreateBuilding(CityPlan plan, Transform parent, Vector2 center,
                                           Vector2 half, float rotDeg, int floors,
                                           Material wall, int addressId, int variant)
        {
            float height = floors * FloorHeight;
            string name = "Bina_" + addressId + "_" + variant;

            // ZEMIN CUKMASI YOK: taban tam y = 0'da bitsin. Kutu yerel
            // olcegi kadar buyuk oldugu icin merkez = height/2.
            GameObject go = CreateBox(name, parent,
                                      new Vector3(center.x, height * 0.5f, center.y),
                                      new Vector3(half.x * 2f, height, half.y * 2f),
                                      wall, LayerBuilding);
            go.transform.localRotation = Quaternion.Euler(0f, rotDeg, 0f);

            // Yerlestirilen bina, sonraki denemeler icin ENGEL olur:
            // ikinci bina ilkinin uzerine binmez.
            plan.obstacles.Add(Rect(name, center, half, rotDeg, 0.5f));
            plan.buildings++;
        }

        private static Material WallFor(Palette palette, int addressId)
        {
            // Adres kimliginden deterministik renk: oyuncu "kirmizi ev" diye
            // hatirlayabiliyor.
            switch (Mathf.Abs(addressId) % 4)
            {
                case 0: return palette.WallRed;
                case 1: return palette.WallTan;
                case 2: return palette.WallBrown;
                default: return palette.WallGrey;
            }
        }

        // ------------------------------------------------------------------
        // Sokak lambalari
        // ------------------------------------------------------------------

        private static void BuildLamps(CityPlan plan, Transform parent, Palette palette,
                                       StringBuilder report)
        {
            int count = 0;

            for (int i = 0; i < plan.doors.Count; i++)
            {
                Vector2 at = plan.addresses[i];

                // Yolun hangi yondan geldigini bul: adresin deposeden
                // uzakligi yonu verir, dik olani yolun yanidir.
                Vector2 radial = at - plan.depot;
                if (radial.sqrMagnitude < 0.01f) radial = new Vector2(0f, 1f);
                radial.Normalize();
                Vector2 side = new Vector2(-radial.y, radial.x);

                int seed = 4000 + i * 17;
                bool done = false;

                // Lambalar yol KENARINDA durur; bu yuzden yol kontrolu
                // uygulanmaz (IsFree'in checkRoads parametresi false).
                // Once yol kontrolu ile hicbiri gecmiyordu: lamba yari
                // genisligi 0.35 m olsa bile komsu yolun 3.2 m payi
                // icinde kaliyordu.
                for (int s = 0; s < 4 && !done; s++)
                {
                    float sign = (s % 2 == 0) ? 1f : -1f;
                    float baseOffset = 3.1f + (s / 2) * 0.7f;
                    float along = (s < 2 ? -0.6f : 0.6f);

                    Vector2 pos = at + side * sign * baseOffset + radial * along;
                    Vector2 half = new Vector2(0.3f, 0.3f);

                    if (!IsFree(plan, pos, half, 0f, 0.15f, false, 0f)) continue;

                    CreateLamp(parent, "Lamba_" + i.ToString("00"), pos, palette);
                    plan.obstacles.Add(Circle("Lamba_" + i, pos, 0.45f, 0.5f));
                    done = true;
                    count++;
                }
            }

            report.AppendLine("  lamba        : " + count + " adet (direk + parlak baslik)");
        }

        private static void CreateLamp(Transform parent, string name, Vector2 at, Palette palette)
        {
            // Iki nesne: ince direk + kucuk parlak baslik. Toplam nesne
            // butcesini zorlamamak icin ayri GameObject yerine ayni
            // isim ailesi altinda iki parca yeterli.
            CreateBox(name + "_Direk", parent,
                      new Vector3(at.x, LampHeight * 0.5f, at.y),
                      new Vector3(0.16f, LampHeight, 0.16f), palette.Road, LayerBuilding);

            CreateBox(name + "_Baslik", parent,
                      new Vector3(at.x, LampHeight + 0.12f, at.y),
                      new Vector3(0.55f, 0.24f, 0.55f), palette.LampGlow, LayerBuilding);
        }

        // ------------------------------------------------------------------
        // Agaclar
        // ------------------------------------------------------------------

        private static void BuildTrees(CityPlan plan, Transform parent, Palette palette,
                                       StringBuilder report)
        {
            int count = 0;
            int seed = 9000;

            for (int i = 0; i < plan.roads.Count && count < MaxTrees; i++)
            {
                RoadSegment s = plan.roads[i];

                Vector2 dir = (s.B - s.A).normalized;
                Vector2 side = new Vector2(-dir.y, dir.x);

                // Yol boyunca seyrek: iki noktada bir deneme, iki yanda bir.
                for (int k = 0; k < 2 && count < MaxTrees; k++)
                {
                    float t = k == 0 ? 0.32f : 0.68f;
                    float sign = k == 0 ? 1f : -1f;
                    seed++;

                    Vector2 basePoint = Vector2.Lerp(s.A, s.B, t);
                    float offset = 5.4f + Hash01(seed) * 1.6f;
                    float jitter = (Hash01(seed + 3) - 0.5f) * 6f;
                    float angle = Hash01(seed + 7) * 360f;

                    Vector2 pos = basePoint + side * sign * offset + dir * jitter;
                    Vector2 half = new Vector2(1.1f, 1.1f);

                    if (!IsFree(plan, pos, half, angle, 0.4f, true, 0.25f)) continue;

                    CreateTree(parent, "Agac_" + count.ToString("00"), pos, angle, seed, palette);
                    plan.obstacles.Add(Circle("Agac_" + count, pos, 1.2f, 1.6f));
                    plan.trees++;
                    count++;
                }
            }

            report.AppendLine("  agac         : " + count + " adet (govde + tac, tavani "
                              + MaxTrees + ")");
        }

        private static void CreateTree(Transform parent, string name, Vector2 at, float angle,
                                       int seed, Palette palette)
        {
            float trunk = TrunkHeight * (0.85f + Hash01(seed + 21) * 0.4f);
            float crown = 2.4f + Hash01(seed + 31) * 0.7f;

            GameObject trunkGo = CreateBox(name + "_Govde", parent,
                                           new Vector3(at.x, trunk * 0.5f, at.y),
                                           new Vector3(0.45f, trunk, 0.45f),
                                           palette.WallBrown, LayerBuilding);
            trunkGo.transform.localRotation = Quaternion.Euler(0f, angle, 0f);

            // Tac: kure. Gobde yarim gomulerek agac tek parca gibi durur.
            GameObject crownGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crownGo.name = name + "_Tac";
            Object.DestroyImmediate(crownGo.GetComponent<Collider>());
            crownGo.transform.SetParent(parent, false);
            crownGo.transform.position = new Vector3(at.x, trunk + crown * 0.32f, at.y);
            crownGo.transform.localScale = new Vector3(crown, crown * 0.92f, crown);
            crownGo.layer = LayerBuilding;

            var renderer = crownGo.GetComponent<MeshRenderer>();
            if (renderer != null && palette.Foliage != null) renderer.sharedMaterial = palette.Foliage;
        }

        // ------------------------------------------------------------------
        // Serbest alan testi
        // ------------------------------------------------------------------

        /// <summary>
        /// Bir ayak izi (dikdortgen) yerlestirilebilir mi?
        ///
        /// Ayak izi UZERINDE 9 ornek noktasi (merkez, 4 kose, 4 kenar
        /// ortasi) alinir ve her biri icin:
        ///   - statik engellere (ped, kapi, dukkan, arac, kurye) minimum
        ///     bosluk sagliyor mu?
        ///   - yol merkez cizgisine yarim yol genisligi + pay kadar uzak mi?
        /// kontrol edilir.
        ///
        /// Kosul saglanmazsa konum ATLANIR. "Zorla koy" yok: cakisan bina
        /// oyuncuyu kapatir, atlanan bina sadece eksik gorseldir.
        /// </summary>
        private static bool IsFree(CityPlan plan, Vector2 center, Vector2 half, float rotDeg,
                                   float selfClearance, bool checkRoads, float roadClearance)
        {
            // Zemin siniri: ayak izi AABB olarak 200x200 zemini asmamali.
            float rad = rotDeg * Mathf.Deg2Rad;
            float c = Mathf.Cos(rad);
            float s = Mathf.Sin(rad);
            float aabbX = Mathf.Abs(half.x * c) + Mathf.Abs(half.y * s);
            float aabbZ = Mathf.Abs(half.x * s) + Mathf.Abs(half.y * c);
            if (Mathf.Abs(center.x) + aabbX > MaxDecorDistance) return false;
            if (Mathf.Abs(center.y) + aabbZ > MaxDecorDistance) return false;

            for (int i = 0; i < SampleCount; i++)
            {
                Vector2 sp = SampleAt(center, half, rotDeg, i);

                for (int j = 0; j < plan.obstacles.Count; j++)
                {
                    if (GapTo(plan.obstacles[j], sp) < selfClearance + plan.obstacles[j].MinGap)
                        return false;
                }

                if (!checkRoads) continue;

                for (int j = 0; j < plan.roads.Count; j++)
                {
                    RoadSegment r = plan.roads[j];
                    if (DistanceToSegment(sp, r.A, r.B) < RoadHalfWidth + roadClearance)
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Nokta -> engel arasindaki bosluk. Negatif/0 deger = nokta engelin
        /// icinde.
        /// </summary>
        private static float GapTo(Area area, Vector2 p)
        {
            if (!area.IsRect) return Vector2.Distance(p, area.Center) - area.Radius;

            float rad = area.RotDeg * Mathf.Deg2Rad;
            float c = Mathf.Cos(rad);
            float s = Mathf.Sin(rad);
            Vector2 d = p - area.Center;

            float lx = d.x * c - d.y * s;
            float lz = d.x * s + d.y * c;

            float dx = Mathf.Abs(lx) - area.Half.x;
            float dz = Mathf.Abs(lz) - area.Half.y;
            if (dx <= 0f && dz <= 0f) return 0f;

            dx = Mathf.Max(dx, 0f);
            dz = Mathf.Max(dz, 0f);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static Vector2 SampleAt(Vector2 center, Vector2 half, float rotDeg, int index)
        {
            Vector2 local;
            switch (index)
            {
                case 0: local = Vector2.zero; break;
                case 1: local = new Vector2(half.x, half.y); break;
                case 2: local = new Vector2(-half.x, half.y); break;
                case 3: local = new Vector2(-half.x, -half.y); break;
                case 4: local = new Vector2(half.x, -half.y); break;
                case 5: local = new Vector2(half.x, 0f); break;
                case 6: local = new Vector2(-half.x, 0f); break;
                case 7: local = new Vector2(0f, half.y); break;
                default: local = new Vector2(0f, -half.y); break;
            }

            // Yerel -> dunya: Unity'nin Y donmesi ile ayni.
            // (yerel (0,0,1) -> dunya (sin t, cos t))
            float rad = rotDeg * Mathf.Deg2Rad;
            float c = Mathf.Cos(rad);
            float s = Mathf.Sin(rad);
            return new Vector2(center.x + local.x * c + local.y * s,
                               center.y - local.x * s + local.y * c);
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lenSqr = ab.sqrMagnitude;
            if (lenSqr < 0.0001f) return Vector2.Distance(p, a);

            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSqr);
            return Vector2.Distance(p, a + ab * t);
        }

        // ------------------------------------------------------------------
        // Dogrulama
        // ------------------------------------------------------------------

        /// <summary>
        /// Sayisal kontrol. Ekran goruntusune bakmak yaniltir; olcmek
        /// tartismayi bitirir.
        ///
        /// Kontroller:
        ///  1) Dekor koku sahnede var mi?
        ///  2) Bina / agac sayisi beklenenden dusuk mu?
        ///  3) Hicbir dekor nesnesi bir StopMarker'a 3 m'den yakin mi?
        ///  4) Binalarin tabani gercekten y = 0'da mi (zemin cokmesi)?
        ///  5) Toplam nesne butcesi asildi mi?
        /// </summary>
        private static void Verify(Transform decorRoot, CityPlan plan, int totalGameObjects)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[Kargo] --- Sehir dekoru dogrulama ---");

            if (decorRoot == null)
            {
                Debug.LogError("[Kargo] Dogrulama BASARISIZ: dekor koku sahnede yok.");
                return;
            }

            StopMarker[] markers = Object.FindObjectsByType<StopMarker>(FindObjectsSortMode.None);

            if (plan.buildings < plan.doors.Count)
            {
                sb.AppendLine("  ! bina sayisi dusuk: " + plan.buildings + " / beklenen en az "
                              + plan.doors.Count + ". Adreslerin bir kismina yerlesim "
                              + "bulunamadi (zemin siniri veya engel yogunlugu).");
            }
            else if (plan.buildings < plan.doors.Count * 2)
            {
                sb.AppendLine("  ! bina sayisi kismi: " + plan.buildings + " / "
                              + (plan.doors.Count * 2) + ". Bazi adreslerde tek bina var.");
            }

            if (plan.trees < MaxTrees / 2)
            {
                sb.AppendLine("  ! agac sayisi dusuk: " + plan.trees + " / tavani " + MaxTrees
                              + ". Yol kenarlari engel sayesinde bos kaldi.");
            }

            if (totalGameObjects > 300)
            {
                sb.AppendLine("  ! toplam GameObject " + totalGameObjects
                              + " > butce 300. Performans duser.");
            }

            // --- Cakisma: dekor vs StopMarker -----------------------------
            Transform[] decorObjects = decorRoot.GetComponentsInChildren<Transform>(true);
            int tooClose = 0;
            float worst = float.MaxValue;
            string worstName = "-";

            for (int i = 0; i < decorObjects.Length; i++)
            {
                // Dekor kokleri ve grup kokleri oyunla etkilesmez; onlar da
                // bir grup dekoru temsil eder ama kontrol edilmeye deger.
                Vector3 p = decorObjects[i].position;

                for (int m = 0; m < markers.Length; m++)
                {
                    float d = Vector3.Distance(p, markers[m].transform.position);

                    // Once sayac: "en yakin" kaydi guncellemeden devam etmek
                    // cok yakin ama en-yakin olmayan ciftleri gizlerdi.
                    if (d < VerifyMarkerDistance) tooClose++;
                    if (d >= worst) continue;

                    worst = d;
                    worstName = decorObjects[i].name;
                }
            }

            if (tooClose > 0)
            {
                sb.AppendLine("  ! " + tooClose + " dekor/StopMarker cifti " + VerifyMarkerDistance
                              + " m'den yakin (en yakin: " + worstName + " / "
                              + worst.ToString("0.00") + " m).");
            }

            // --- Zemin cokmesi -------------------------------------------
            // Renderer.bounds YENI OLUSTURULMUS bir nesnede birkac kare
            // guncellenmeden kalabilir; bu yuzden donusumden hesaplanir.
            // Bina sadece Y ekseninde donduruldugu icin alt yuz tam olarak
            // position.y - scale.y/2'dir.
            int sunkBuildings = 0;
            Transform buildings = decorRoot.Find("Buildings");
            if (buildings != null)
            {
                var boxes = buildings.GetComponentsInChildren<Transform>(true);

                // DIZI 0 GRUP KOKUDUR; o y=0, olcek 1 oldugu icin
                // minY = -0.5 cikar ve HICBIR CIZILMEMIS kastedilen
                // "batik bina" sanilir. Atla.
                for (int i = 1; i < boxes.Length; i++)
                {
                    float minY = boxes[i].position.y - boxes[i].localScale.y * 0.5f;
                    if (minY < -0.05f) sunkBuildings++;
                }
            }

            if (sunkBuildings > 0)
                sb.AppendLine("  ! " + sunkBuildings + " bina zeminin altinda kaldi (yarim gomulu).");

            if (markers.Length == 0)
            {
                sb.AppendLine("  StopMarker yok; mesafe kontrolu yapilamadi.");
            }
            else
            {
                sb.AppendLine("  en yakin dekor-ped mesafesi : " + worst.ToString("0.00")
                              + " m  (" + worstName + ")");
            }

            sb.AppendLine("  bina / agac                : " + plan.buildings + " / " + plan.trees);
            sb.AppendLine("  toplam GameObject          : " + totalGameObjects);
            sb.AppendLine("  zemin altinda kalan bina   : " + sunkBuildings);

            if (tooClose > 0 || sunkBuildings > 0)
                Debug.LogError("[Kargo] Dogrulama UYARI:\n" + sb);
            else
                Debug.Log("[Kargo] Dogrulama OK:\n" + sb);
        }

        // ------------------------------------------------------------------
        // Yardimcilar
        // ------------------------------------------------------------------

        private static Transform NewChild(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static GameObject CreateBox(string name, Transform parent, Vector3 localPosition,
                                            Vector3 size, Material material, int layer)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;

            // Collider kaldirilir: dekor oyuncuyu ENGELLEMEmeli. Bina
            // engellemese daha iyi olurdu ama oyuncu icinden gecmesin diye
            // kutu dekoru cozulmedi; en azindan yoldan gecis serbest.
            Object.DestroyImmediate(go.GetComponent<Collider>());

            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = size;
            go.layer = layer;

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null && material != null) renderer.sharedMaterial = material;
            return go;
        }

        private static Area Circle(string label, Vector2 center, float radius, float minGap)
        {
            return new Area
            {
                Label = label,
                Center = center,
                Radius = radius,
                Half = Vector2.zero,
                RotDeg = 0f,
                IsRect = false,
                MinGap = minGap
            };
        }

        private static Area Rect(string label, Vector2 center, Vector2 half, float rotDeg,
                                 float minGap)
        {
            return new Area
            {
                Label = label,
                Center = center,
                Radius = 0f,
                Half = half,
                RotDeg = rotDeg,
                IsRect = true,
                MinGap = minGap
            };
        }

        /// <summary>
        /// Deterministik seudo-rastgele [0,1).
        ///
        /// Mathf.Sin tabanli (klasik GLSL hash). System.Random kullanmamizin
        /// sebebi: tohum verilse de Unity her platformda ayni sonucu
        /// vermez; bu formuller [0,1) araliginda sabit kalir ve "menuye iki
        /// kez bas -> ayni sehir" garantisini verir.
        /// </summary>
        private static float Hash01(int seed)
        {
            float v = Mathf.Sin(seed * 12.9898f + 78.233f) * 43758.5453f;
            return v - Mathf.Floor(v);
        }

        private static int CountUnder(Transform root)
        {
            return root.GetComponentsInChildren<Transform>(true).Length;
        }

        private static int CountRootObjects(string rootName)
        {
            int n = 0;
            GameObject[] roots = EditorSceneManager.GetActiveScene().GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
                if (roots[i].name == rootName) n++;
            return n;
        }

        private static int CountStopMarkers()
        {
            return Object.FindObjectsByType<StopMarker>(FindObjectsSortMode.None).Length;
        }

        // ------------------------------------------------------------------
        // Materyaller
        // ------------------------------------------------------------------

        /// <summary>
        /// Sekiz PAYLASILAN materyal. Binlerce nesne olmasina ragmen
        /// malzeme cesidi sinirli tutulur: draw call ve shader
        /// degisimi azalir.
        ///
        ///   Road        : yollar, depo alani, lamba direkleri
        ///   Pavement    : kaldirim seridi
        ///   WallRed/Tan/Brown/Grey : bina renkleri (adres kimligine gore)
        ///   Foliage     : agac taclari
        ///   LampGlow    : lamba basligi (emissive)
        /// </summary>
        private sealed class Palette
        {
            public Material Road;
            public Material Pavement;
            public Material WallRed;
            public Material WallTan;
            public Material WallBrown;
            public Material WallGrey;
            public Material Foliage;
            public Material LampGlow;

            public int MaterialCount
            {
                get { return 8; }
            }

            public static Palette Create()
            {
                return new Palette
                {
                    Road      = CreateMaterial("CityRoad", new Color(0.16f, 0.16f, 0.17f)),
                    Pavement  = CreateMaterial("CityPavement", new Color(0.56f, 0.55f, 0.51f)),
                    WallRed   = CreateMaterial("CityWallRed", new Color(0.60f, 0.31f, 0.26f)),
                    WallTan   = CreateMaterial("CityWallTan", new Color(0.72f, 0.61f, 0.42f)),
                    WallBrown = CreateMaterial("CityWallBrown", new Color(0.44f, 0.33f, 0.24f)),
                    WallGrey  = CreateMaterial("CityWallGrey", new Color(0.47f, 0.48f, 0.50f)),
                    Foliage   = CreateMaterial("CityFoliage", new Color(0.22f, 0.40f, 0.18f)),
                    LampGlow  = CreateMaterial("CityLampGlow", new Color(1.00f, 0.90f, 0.65f),
                                               0.4f, 2.5f)
                };
            }
        }

        private static Material CreateMaterial(string fileName, Color color,
                                               float smoothness = 0.1f, float emission = 0f)
        {
            string path = MaterialFolder + "/" + fileName + ".mat";

            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                // Yalnizca KENDI materyallerimizi tazeleriz (isim "City"
                // onekiyle baslar); baska ajanlarin materyallerine
                // dokunmayiz.
                ApplyMaterial(existing, color, smoothness, emission);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[Kargo] URP/Lit shader bulunamadi.");
                return null;
            }

            if (!AssetDatabase.IsValidFolder(MaterialFolder))
                AssetDatabase.CreateFolder("Assets/Art", "Materials");

            var material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
            ApplyMaterial(material, color, smoothness, emission);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void ApplyMaterial(Material material, Color color, float smoothness,
                                          float emission)
        {
            material.color = color;
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0f);

            if (emission > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * emission);
                material.globalIlluminationFlags =
                    MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);
            }
        }

        // ------------------------------------------------------------------
        // Veri tipleri
        // ------------------------------------------------------------------

        /// <summary>
        /// Kurulumun butun verisini tasir. Metodlara dokuz parametre
        /// gecirmek yerine tek bir baglam; ayrica "simdiye kadar konulan
        /// her sey" engel listesine eklenir.
        /// </summary>
        private sealed class CityPlan
        {
            public readonly List<Area> obstacles = new List<Area>();
            public readonly List<RoadSegment> roads = new List<RoadSegment>();
            public readonly List<Vector2> addresses = new List<Vector2>();
            public readonly List<Vector2> doors = new List<Vector2>();
            public readonly List<int> addressIds = new List<int>();

            public Vector2 depot;
            public int buildings;
            public int trees;
        }

        private struct RoadSegment
        {
            public Vector2 A;
            public Vector2 B;

            public RoadSegment(Vector2 a, Vector2 b)
            {
                A = a;
                B = b;
            }
        }

        private struct Area
        {
            public string Label;
            public Vector2 Center;
            public float Radius;   // IsRect == false ise yaricap
            public Vector2 Half;   // IsRect == true  ise yari boyutlar
            public float RotDeg;
            public bool IsRect;
            public float MinGap;   // bu engel ile dekor arasindaki minimum bosluk
        }
    }
}
