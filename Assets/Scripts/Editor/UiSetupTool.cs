using System.Text;
using KargoSim.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace KargoSim.EditorTools
{
    /// <summary>
    /// Arayuzu sifirdan kurar. Elle Inspector'da dizmek yerine buradan
    /// tek komutla uretilir: yeniden kurulabilir, incelenebilir ve
    /// git'te metin olarak izlenir.
    ///
    /// Menu: Kargo/6. Arayuzu Kur
    /// </summary>
    public static class UiSetupTool
    {
        private const string ScenePath = "Assets/Scenes/Game.unity";
        private const string UIRootName = "UI";
        private const string UguiResourcePath = "UI/Skin/UISprite.psd";
        private const string DefaultFontPath = "UI/Skin/UISprite.psd";

        /// <summary>
        /// Her seyi dogru sirayla kurar.
        ///
        /// Neden gerekli: kurulum birkac ayri menuye bolunmus duruyor ve
        /// bunlarin SIRASI onemli (icerik -> arayuz). Kullanici her seferinde
        /// menuleri elle sirayla calistirmak zorunda kalmasin.
        /// </summary>
        [MenuItem("Kargo/0. HER SEYI KUR (Arayuz dahil)", priority = -1)]
        public static void BuildEverything()
        {
            Debug.Log("[Kargo] === Tam kurulum basliyor ===");

            ProjectSetupTool.RunAllSetup();
            Debug.Log("[Kargo] 1/6 proje ayarlari");

            VehiclePrefabBuilder.Build();
            Debug.Log("[Kargo] 2/6 arac prefabi + sahne");

            ContentSetupTool.BuildContent();
            Debug.Log("[Kargo] 3/6 kargo turleri");

            DeliverySetupTool.BuildLayout();
            Debug.Log("[Kargo] 4/6 harita + durak isaretleri");

            ProgressionSetupTool.BuildEconomyConfig();
            Debug.Log("[Kargo] 5/6 ekonomi ayarlari");

            BuildUI();
            Debug.Log("[Kargo] 6/6 arayuz");

            CourierSetupTool.Build();
            Debug.Log("[Kargo] 7/7 kurye karakteri");

            ShopSetupTool.Build();
            Debug.Log("[Kargo] 8/8 dukkan + etkilesim noktalari");

            CitySetupTool.Build();
            Debug.Log("[Kargo] 9/9 sehir dekoru");

            Debug.Log("[Kargo] === Tam kurulum bitti ===");
        }

        [MenuItem("Kargo/6. Arayuzu Kur", priority = 5)]
        public static void BuildUI()
        {
            Font font = LoadDefaultFont();
            if (font == null)
            {
                Debug.LogError("[Kargo] Varsayilan font yuklenemedi; arayuz kurulamaz.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // Eski UI'yi temizle (yeniden calistirilabilir olsun diye)
            var old = GameObject.Find(UIRootName);
            if (old != null) Object.DestroyImmediate(old);

            var root = new GameObject(UIRootName);
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            root.AddComponent<GraphicRaycaster>();

            // KRITIK: Canvas kokunun pivot/anchoredPosition degerleri
            // Screen Space - Overlay'da Unity tarafindan surulur, ama
            // eski surumden kalan bir pivot degeri (0.5,0.5) kalirsa
            // cocuklar anchor (0,0) olmasina ragmen canvasin ORTASINDAN
            // baslar. "HUD ekranin ortasinda" belirtisinin kok nedeni
            // buydu. Orijini sol altta sabitliyoruz.
            var rootRect = (RectTransform)root.transform;
            rootRect.pivot = Vector2.zero;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            rootRect.localScale = Vector3.one;
            rootRect.anchoredPosition = Vector2.zero;

            var manager = root.AddComponent<UIManager>();

            HudView hud = BuildHud(root.transform, font);
            MinimapView minimap = BuildMinimap(root.transform, font);
            DepotView depot = BuildDepot(root.transform, font);
            StopView stop = BuildStop(root.transform, font);
            DaySummaryView summary = BuildSummary(root.transform, font);

            Assign(manager, hud, depot, stop, summary, minimap);
            WireToCompositionRoot(manager);
            ReportLayout();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[Kargo] Arayuz kuruldu.");
        }

        private static void WireToCompositionRoot(UIManager manager)
        {
            var root = Object.FindFirstObjectByType<Core.GameCompositionRoot>();
            if (root == null)
            {
                Debug.LogError("[Kargo] GameCompositionRoot bulunamadi. " +
                               "Once sahneyi kurun: Kargo/1. Tum Kurulumu Calistir");
                return;
            }

            root.SetUiForSetup(manager);
            EditorUtility.SetDirty(root);
            Debug.Log("[Kargo] Composition Root'a UIManager baglandi.");
        }

        // ------------------------------------------------------------------
        // UIManager alan atamalari
        // ------------------------------------------------------------------

        private static void Assign(UIManager manager, HudView hud, DepotView depot,
                                  StopView stop, DaySummaryView summary, MinimapView minimap)
        {
            var so = new SerializedObject(manager);
            so.FindProperty("hud").objectReferenceValue = hud;
            so.FindProperty("minimap").objectReferenceValue = minimap;
            so.FindProperty("depotView").objectReferenceValue = depot;
            so.FindProperty("stopView").objectReferenceValue = stop;
            so.FindProperty("summaryView").objectReferenceValue = summary;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(manager);
        }

        // ------------------------------------------------------------------
        // Font
        // ------------------------------------------------------------------

        private static Font LoadDefaultFont()
        {
            // Unity 6'da varsayilan uGUI fontu bu adla gelir.
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            if (font == null)
            {
                Debug.LogWarning("[Kargo] Builtin font bulunamadi. " +
                                 "Kendi fontunu Assets/Art/Fonts altina koyup " +
                                 "FontProbe yerine ataman gerekir.");
            }

            return font;
        }

        // ------------------------------------------------------------------
        // HUD
        // ------------------------------------------------------------------

        private static HudView BuildHud(Transform parent, Font font)
        {
            var go = new GameObject("HUD");
            go.transform.SetParent(parent, false);

            var view = go.AddComponent<HudView>();

            // ----------------------------------------------------------------
            // SOL ALT: bilgi paneli (para, seviye, saat, kapasite)
            // ----------------------------------------------------------------
            RectTransform panel = CreatePanel("Stats", go.transform,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(18f, 18f),
                new Vector2(320f, 250f));
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.zero;
            panel.pivot = Vector2.zero;

            // Panelin sol-ust kosesine gore konumlandir (pivot degil).
            Text money = CreateText("Money", panel, font, UITheme.FontLarge, UITheme.Money,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -10f), new Vector2(300f, 34f));
            money.alignment = TextAnchor.MiddleLeft;

            Text level = CreateText("Level", panel, font, UITheme.FontNormal, UITheme.TextPrimary,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -46f), new Vector2(300f, 24f));
            level.alignment = TextAnchor.MiddleLeft;

            Image xpFill = CreateBar("XPBar", panel, new Vector2(0f, -74f));
            Image capFill = CreateBar("CapacityBar", panel, new Vector2(0f, -150f));

            Text time = CreateText("Time", panel, font, UITheme.FontNormal, UITheme.TextPrimary,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -82f), new Vector2(300f, 24f));
            time.alignment = TextAnchor.MiddleLeft;

            Text capacity = CreateText("Capacity", panel, font, UITheme.FontSmall, UITheme.TextMuted,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -164f), new Vector2(300f, 66f));
            capacity.alignment = TextAnchor.UpperLeft;

            // ----------------------------------------------------------------
            // Alt orta: sonraki hedef + ipucu
            // ----------------------------------------------------------------
            Text nextStop = CreateText("NextStop", go.transform, font, UITheme.FontNormal, UITheme.Accent,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 44f), new Vector2(820f, 28f));
            nextStop.alignment = TextAnchor.MiddleCenter;

            Text hint = CreateText("Hint", go.transform, font, UITheme.FontSmall, UITheme.TextMuted,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(900f, 22f));
            hint.alignment = TextAnchor.MiddleCenter;

            // Odeme / seviye bildirimi: ekranin ust ortasi
            Text payout = CreateText("Payout", go.transform, font, UITheme.FontHuge, UITheme.Success,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(820f, 60f));
            payout.alignment = TextAnchor.MiddleCenter;
            payout.gameObject.SetActive(false);

            var so = new SerializedObject(view);
            so.FindProperty("moneyText").objectReferenceValue = money;
            so.FindProperty("levelText").objectReferenceValue = level;
            so.FindProperty("timeText").objectReferenceValue = time;
            so.FindProperty("capacityText").objectReferenceValue = capacity;
            so.FindProperty("nextStopText").objectReferenceValue = nextStop;
            so.FindProperty("hintText").objectReferenceValue = hint;
            so.FindProperty("payoutText").objectReferenceValue = payout;
            so.FindProperty("xpBarFill").objectReferenceValue = xpFill;
            so.FindProperty("capacityBarFill").objectReferenceValue = capFill;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);

            return view;
        }

        // ------------------------------------------------------------------
        // Minimap
        // ------------------------------------------------------------------

        private static MinimapView BuildMinimap(Transform parent, Font font)
        {
            var go = new GameObject("Minimap");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<MinimapView>();

            // SOL UST: harita. Para bilgisi altta kalir; ikisi cakismaz.
            RectTransform panel = CreatePanel("Area", go.transform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -18f),
                new Vector2(UITheme.MinimapSize, UITheme.MinimapSize));
            panel.anchorMin = new Vector2(0f, 1f);
            panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(0f, 1f);

            // Arka plan
            Image bg = CreateImage("Background", panel, new Color(0f, 0f, 0f, 0.55f));
            bg.rectTransform.anchorMin = Vector2.zero;
            bg.rectTransform.anchorMax = Vector2.one;
            bg.rectTransform.offsetMin = Vector2.zero;
            bg.rectTransform.offsetMax = Vector2.zero;

            RectTransform dotRoot = CreateRect("Dots", panel);
            dotRoot.anchorMin = new Vector2(0.5f, 0.5f);
            dotRoot.anchorMax = new Vector2(0.5f, 0.5f);
            dotRoot.pivot = new Vector2(0.5f, 0.5f);
            dotRoot.anchoredPosition = Vector2.zero;

            // Nokta sablonu: hem durak hem rota cizgisi icin kullanilir
            Image dotPrefab = CreateImage("Dot", dotRoot, UITheme.StopColor);
            dotPrefab.rectTransform.sizeDelta = new Vector2(8f, 8f);
            dotPrefab.gameObject.SetActive(false);

            Image depot = CreateImage("Depot", dotRoot, UITheme.DepotColor);
            depot.rectTransform.sizeDelta = new Vector2(16f, 16f);

            Image vehicle = CreateImage("Vehicle", dotRoot, UITheme.VehicleColor);
            vehicle.rectTransform.sizeDelta = new Vector2(10f, 10f);

            Text label = CreateText("Label", panel, font, UITheme.FontTiny, UITheme.TextMuted,
                new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 2f),
                new Vector2(UITheme.MinimapSize, 18f));
            label.alignment = TextAnchor.LowerCenter;
            label.text = "HARITA";

            var so = new SerializedObject(view);
            so.FindProperty("area").objectReferenceValue = panel;
            so.FindProperty("dotPrefab").objectReferenceValue = dotPrefab.rectTransform;
            so.FindProperty("routeRoot").objectReferenceValue = dotRoot;
            so.FindProperty("depotDot").objectReferenceValue = depot.rectTransform;
            so.FindProperty("vehicleDot").objectReferenceValue = vehicle.rectTransform;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);

            return view;
        }

        // ------------------------------------------------------------------
        // Depo paneli
        // ------------------------------------------------------------------

        private static DepotView BuildDepot(Transform parent, Font font)
        {
            var go = new GameObject("DepotPanel");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<DepotView>();

            RectTransform panel = CreatePanel("Panel", go.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(900f, 640f));
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);

            Text header = CreateText("Header", panel, font, UITheme.FontNormal, UITheme.TextPrimary,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -14f), new Vector2(870f, 60f));
            header.alignment = TextAnchor.UpperCenter;

            Text footer = CreateText("Footer", panel, font, UITheme.FontSmall, UITheme.TextMuted,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 16f), new Vector2(870f, 50f));
            footer.alignment = TextAnchor.LowerCenter;

            RectTransform container = CreateRect("Rows", panel);
            container.anchorMin = new Vector2(0f, 1f);
            container.anchorMax = new Vector2(1f, 1f);
            container.offsetMin = new Vector2(16f, 80f);
            container.offsetMax = new Vector2(-16f, -80f);
            container.pivot = new Vector2(0.5f, 1f);
            container.anchoredPosition = Vector2.zero;

            // Satir sablonu
            var rowGo = new GameObject("RowPrefab");
            rowGo.transform.SetParent(container, false);
            var row = rowGo.AddComponent<ParcelRowView>();

            RectTransform rowRect = rowGo.AddComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(0f, UITheme.RowHeight);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);

            Image background = CreateImage("Background", rowRect, new Color(1f, 1f, 1f, 0.05f));
            Stretch(background.rectTransform);

            Image selection = CreateImage("Selection", rowRect, UITheme.Accent);
            Stretch(selection.rectTransform);

            Button button = rowGo.AddComponent<Button>();
            button.targetGraphic = background;

            Text label = CreateText("Label", rowRect, font, UITheme.FontNormal, UITheme.TextPrimary,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(10f, -3f), new Vector2(0f, 20f));
            label.alignment = TextAnchor.MiddleLeft;

            Text detail = CreateText("Detail", rowRect, font, UITheme.FontSmall, UITheme.TextMuted,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(10f, 3f), new Vector2(0f, 18f));
            detail.alignment = TextAnchor.LowerLeft;

            var rowSo = new SerializedObject(row);
            rowSo.FindProperty("button").objectReferenceValue = button;
            rowSo.FindProperty("background").objectReferenceValue = background;
            rowSo.FindProperty("selectionMark").objectReferenceValue = selection;
            rowSo.FindProperty("label").objectReferenceValue = label;
            rowSo.FindProperty("detail").objectReferenceValue = detail;
            rowSo.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(row);

            // Sablon gizli olmali; DepotView havuzdan Instantiate ediyor.
            rowGo.SetActive(false);

            var so = new SerializedObject(view);
            so.FindProperty("panel").objectReferenceValue = panel.gameObject;
            so.FindProperty("rowContainer").objectReferenceValue = container;
            so.FindProperty("rowPrefab").objectReferenceValue = row;
            so.FindProperty("headerText").objectReferenceValue = header;
            so.FindProperty("footerText").objectReferenceValue = footer;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);

            // DIKKAT: KOK GameObject degil, sadece IC panel kapatilir.
            // Kok kapatilirsa panel.SetActive(true) cagrisi etkisiz kalir
            // (GameObject inactive iken cocugu da gorunmez) ve oyuncu
            // "Tab calismiyor" sanar.
            panel.gameObject.SetActive(false);
            return view;
        }

        // ------------------------------------------------------------------
        // Teslimat paneli
        // ------------------------------------------------------------------

        private static StopView BuildStop(Transform parent, Font font)
        {
            var go = new GameObject("StopPanel");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<StopView>();

            RectTransform panel = CreatePanel("Panel", go.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -60f),
                new Vector2(720f, 420f));
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);

            Text header = CreateText("Header", panel, font, UITheme.FontLarge, UITheme.Accent,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -18f), new Vector2(680f, 40f));
            header.alignment = TextAnchor.UpperCenter;

            Text list = CreateText("List", panel, font, UITheme.FontNormal, UITheme.TextPrimary,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(24f, 60f), new Vector2(-24f, -70f));
            list.alignment = TextAnchor.UpperLeft;

            Text hint = CreateText("Hint", panel, font, UITheme.FontNormal, UITheme.Experience,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 16f), new Vector2(680f, 30f));
            hint.alignment = TextAnchor.LowerCenter;

            var so = new SerializedObject(view);
            so.FindProperty("panel").objectReferenceValue = panel.gameObject;
            so.FindProperty("headerText").objectReferenceValue = header;
            so.FindProperty("listText").objectReferenceValue = list;
            so.FindProperty("hintText").objectReferenceValue = hint;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);

            // Kok degil, ic panel kapatilir (aciklama BuildDepot'ta).
            panel.gameObject.SetActive(false);
            return view;
        }

        // ------------------------------------------------------------------
        // Gun sonu paneli
        // ------------------------------------------------------------------

        private static DaySummaryView BuildSummary(Transform parent, Font font)
        {
            var go = new GameObject("DaySummary");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<DaySummaryView>();

            RectTransform panel = CreatePanel("Panel", go.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(660f, 420f));
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);

            Text title = CreateText("Title", panel, font, UITheme.FontHuge, UITheme.Experience,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -24f), new Vector2(620f, 54f));
            title.alignment = TextAnchor.UpperCenter;

            Text body = CreateText("Body", panel, font, UITheme.FontNormal, UITheme.TextPrimary,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(30f, 60f), new Vector2(-30f, -90f));
            body.alignment = TextAnchor.UpperLeft;

            Text hint = CreateText("Hint", panel, font, UITheme.FontSmall, UITheme.TextMuted,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 16f), new Vector2(620f, 24f));
            hint.alignment = TextAnchor.LowerCenter;

            var so = new SerializedObject(view);
            so.FindProperty("panel").objectReferenceValue = panel.gameObject;
            so.FindProperty("titleText").objectReferenceValue = title;
            so.FindProperty("bodyText").objectReferenceValue = body;
            so.FindProperty("hintText").objectReferenceValue = hint;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);

            // Kok degil, ic panel kapatilir (aciklama BuildDepot'ta).
            panel.gameObject.SetActive(false);
            return view;
        }

        // ------------------------------------------------------------------
        // Yerlesim olcumu
        // ------------------------------------------------------------------

        /// <summary>
        /// HUD ve minimapin GERCEKTEN nereye dustugunu sayilarla yazar.
        ///
        /// Neden gerekli: ekran goruntusunden "sol altta mi" diye
        /// bakmak yaniltir (Game view olcegi, CanvasScaler esleme
        /// modu, cozunurluk hepsi etkili). Once "panel referansi bossa"
        /// diye sahte bir sorun aradik; aslinda yerlesim de farkli
        /// kurulmus olabilir. Sayi olcmek tartismayi bitirir.
        ///
        /// Beklenen: Stats ve Area panelinin sol-alt KOSESI (0,0) tabanli
        /// canvas koordinatlarinda yakin (18,18) ve (352,18) olmali.
        /// </summary>
        private static void ReportLayout()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[Kargo] --- Arayuz yerlesimi ---");

            // Canvas'in "rect"i yoktur; olcum Canvas'in RectTransform'undan
            // yapilir. (rootCanvas kullanmaya calisinca None doner.)
            Canvas canvas = Object.FindFirstObjectByType<Canvas>();
            RectTransform canvasRect = canvas != null ? canvas.GetComponent<RectTransform>() : null;
            if (canvasRect == null)
            {
                Debug.Log("[Kargo] Canvas bulunamadi, yerlesim olculemedi.");
                return;
            }

            sb.AppendLine("  canvas boyutu : " + canvasRect.rect.size.ToString("0")
                          + "  (ekran: " + Screen.width + "x" + Screen.height + ")");
            sb.AppendLine("  canvas koku   : pivot=" + canvasRect.pivot.ToString("0.0")
                          + " anchor=" + canvasRect.anchorMin.ToString("0.0")
                          + "-" + canvasRect.anchorMax.ToString("0.0")
                          + " olcek=" + canvasRect.localScale.ToString("0.00"));

            // CIZGI: anchor degeri ile GERCEKTEN nereye dustugu ayridir.
            // once "panel sol altta mi" diye EKRAN GORUNTUSUNE bakmak
            // yanilticiydi; burada sayisal olceruyoruz.
            CheckBottomLeft(sb, "Istatistik paneli", "Stats", 60f);
            CheckBottomLeft(sb, "Minimap", "Area", 60f);
            CheckTopLeft(sb, "Minimap", "Area", 60f);

            ReportRect(sb, "Depo paneli", "Panel");
            ReportRect(sb, "Hedef yazisi", "NextStop");

            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// Bir panelin sol-alt kosesinin canvasin sol-altina yakin
        /// olup olmadigini olcer. "Yakindir" esigi: 60 px. Buyuk
        /// sapma varsa yerlesim bozuk demektir.
        /// </summary>
        private static void CheckBottomLeft(StringBuilder sb, string label, string name, float tolerance)
        {
            RectTransform rt = FindByName(name);
            if (rt == null)
            {
                sb.AppendLine("  " + label.PadRight(20) + ": BULUNAMADI (" + name + ")");
                return;
            }

            Vector3[] c = new Vector3[4];
            rt.GetWorldCorners(c);

            bool ok = c[0].x <= tolerance && c[0].y <= tolerance;
            sb.AppendLine("  " + label.PadRight(20) + ": solAlt=" + c[0].ToString("0")
                          + "  " + (ok ? "SOL ALTTA OK" : "SOL ALTTA DEGIL (yerlesim bozuk)"));
        }

        private static void CheckTopLeft(StringBuilder sb, string label, string name, float tolerance)
        {
            RectTransform rt = FindByName(name);
            if (rt == null) return;

            Vector3[] c = new Vector3[4];
            rt.GetWorldCorners(c);

            // Sag-ust kose x kucuk, y yuksek (canvas yuksekligine gore)
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null) return;

            float top = canvas.GetComponent<RectTransform>().rect.height;
            bool ok = c[2].x <= tolerance && c[2].y >= top - tolerance;
            sb.AppendLine("  " + label.PadRight(20) + ": sagUst=" + c[2].ToString("0")
                          + "  " + (ok ? "SOL USTTE OK" : "SOL USTTE DEGIL (yerlesim bozuk)"));
        }

        private static RectTransform FindByName(string objectName)
        {
            // FindObjectsInactive.Include SART: depo/teslimat panelleri
            // kurulumda KAPALI birakiliyor ve varsayilan arama onlari
            // gormuyordu ("BULUNAMADI" diye raporlaniyordu).
            foreach (RectTransform rt in Object.FindObjectsByType<RectTransform>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (rt.name == objectName) return rt;
            return null;
        }

        private static void ReportRect(System.Text.StringBuilder sb, string label, string path)
        {
            Transform found = null;
            foreach (Transform t in Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None))
            {
                if (t.name == path) { found = t; break; }
            }

            if (found == null)
            {
                sb.AppendLine("  " + label.PadRight(20) + ": BULUNAMADI (" + path + ")");
                return;
            }

            RectTransform rt = (RectTransform)found;
            Vector3[] corners = new Vector3[4];
            rt.GetWorldCorners(corners);

            sb.AppendLine("  " + label.PadRight(20)
                          + ": solAlt=" + corners[0].ToString("0")
                          + "  sagUst=" + corners[2].ToString("0")
                          + "  anchor=" + rt.anchorMin.ToString("0.0")
                          + "  pivot=" + rt.pivot.ToString("0.0")
                          + "  pos=" + rt.anchoredPosition.ToString("0")
                          + "  boyut=" + rt.sizeDelta.ToString("0"));
        }

        // ------------------------------------------------------------------
        // Yardimcilar
        // ------------------------------------------------------------------

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static RectTransform CreatePanel(string name, Transform parent,
            Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            RectTransform rt = CreateRect(name, parent);
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var image = go.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static Text CreateText(string name, Transform parent, Font font, int size,
                                      Color color, Vector2 anchorMin, Vector2 anchorMax,
                                      Vector2 offset, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(anchorMin.x, anchorMax.y);

            if (anchorMin == anchorMax)
            {
                // Sabit konumlu kutu: sizeDelta + anchoredPosition.
                rt.sizeDelta = sizeDelta;
                rt.anchoredPosition = offset;
                return text;
            }

            // ESNETILMIS BANT (orn. baslik: soldan saga, ust kenara
            // yapisik). offset = kenardan iceri bosluk, sizeDelta = bandin
            // YUKSEKLIGI.
            //
            // ONEMLI: eskide sizeDelta burada tamamen yok sayiliyordu:
            //   offsetMin = (0, 16); offsetMax = (0, -16)  ->  yukseklik -32
            // Baslik ve alt bilgi bu yuzden CÖKÜp üst üste biniyordu
            // ("metinler ic ice girdi"). Simdi yukseklik korunuyor.
            float insetX = offset.x;
            float bottom, top;

            if (anchorMin.y >= 0.5f)
            {
                // Ust kenara (ya da ortaya) yapisik
                top = -offset.y;
                bottom = top - sizeDelta.y;
            }
            else
            {
                // Alt kenara yapisik
                bottom = offset.y;
                top = bottom + sizeDelta.y;
            }

            rt.offsetMin = new Vector2(insetX, bottom);
            rt.offsetMax = new Vector2(-insetX, top);

            return text;
        }

        private static Image CreateBar(string name, Transform parent, Vector2 position)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = position;
            rt.sizeDelta = new Vector2(300f, 12f);

            Image background = go.AddComponent<Image>();
            background.color = UITheme.BarBackground;

            Image fill = CreateImage("Fill", rt, UITheme.BarFill);
            var fillRt = fill.rectTransform;
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillAmount = 0f;

            return fill;
        }
    }
}
