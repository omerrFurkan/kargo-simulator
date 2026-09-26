using UnityEngine;
using UnityEngine.UI;

namespace KargoSim.UI
{
    /// <summary>
    /// ASAMA 19 - HUD YERLESIMI: BILGI KOSELERE DAGILIR.
    ///
    /// Oyuncu: "ekrandaki navigasyon ve diger ibareler ekranin cok
    /// ortasinda, koselere yerlestirmemiz gerek".
    ///
    /// SORUN: <see cref="UiSetupTool"/> HUD'i sol alt + alt orta
    /// kuracak sekilde yaziyordu, ama SAHNEDEKI degerler bundan
    /// tamamen farkliydi. Olcum:
    ///
    ///     Money      x=60  y=69     (sol alt olmaliydi)
    ///     Capacity   x=60  y=56
    ///     NextStop   x=50  y=55     <-- ekranin tam ortasi
    ///     Hint       x=50  y=52     <-- ekranin tam ortasi
    ///     Minimap    x=58  y=40     <-- gorus alaninin ortasi
    ///
    /// Yani oyuncu surerken baktigi alanin tam ortasinda para, saat,
    /// kapasite, rota ve HARITA duruyordu.
    ///
    /// COZUM: yerlesim sahnede degil KODDA belirlenir ve her yuklemede
    /// uygulanir. Boylece (a) sahnedeki yanlis degerler onemsiz olur,
    /// (b) duzenlemeler dosyada gorunur, (c) test edilebilir.
    /// Sahne elle degistirilse de yerlesim kendini duzeltir.
    ///
    /// YERLESIM SOZLESMESI:
    ///   Sol ust    : para, seviye, XP, saat
    ///   Sol alt    : kapasite + kapasite cubugu
    ///   Sag ust    : minimap (harita)
    ///   Sag alt    : sonraki hedef (rotanavigation)
    ///   Alt orta   : baglamsal ipucu (en altta, gorusu kapatmaz)
    ///   Merkez     : sadece MODAL ve gecici bildirim (odeme, seviye)
    /// </summary>
    public static class HudLayout
    {
        private const float Margin = 18f;

        /// <summary>
        /// HUD ve minimapin tum elemanlarini kose konumlarina tasir.
        /// Eksik eleman sessizce atlanir (sahne farkli olabilir).
        /// </summary>
        public static void Apply(HudView hud, MinimapView minimap)
        {
            if (hud == null)
            {
                Debug.LogWarning("[HudLayout] hud null; yerlesim uygulanmadi.");
                return;
            }

            RectTransform root = hud.transform as RectTransform;
            if (root == null)
            {
                Debug.LogWarning("[HudLayout] HUD kok RectTransform degil; yerlesim uygulanmadi.");
                return;
            }

            // Yalnizca bu bloktaki cocuklari tasir; modal paneller
            // (kod girisi, depo, gun ozeti) KENDI merkez konumlarini
            // korumalidir - onlar oyuncunun dikkatini gerektirir.
            var stats = FindChild(root, "Stats");

            // --- Sol ust: para, seviye, XP, saat ---
            RectTransform money = Reanchor(FindChild(root, "Money"), root);
            RectTransform level = Reanchor(FindChild(root, "Level"), root);
            RectTransform xp = Reanchor(FindChild(root, "XPBar"), root);
            RectTransform time = Reanchor(FindChild(root, "Time"), root);

            if (money != null) Place(money, Corner.TopLeft, Margin, -Margin, new Vector2(300f, 34f));
            if (level != null) Place(level, Corner.TopLeft, Margin, -Margin - 36f, new Vector2(300f, 24f));
            if (xp != null) Place(xp, Corner.TopLeft, Margin, -Margin - 64f, new Vector2(300f, 10f));
            if (time != null) Place(time, Corner.TopLeft, Margin, -Margin - 80f, new Vector2(300f, 24f));

            // --- Sol alt: kapasite ---
            RectTransform capacity = Reanchor(FindChild(root, "Capacity"), root);
            RectTransform capBar = Reanchor(FindChild(root, "CapacityBar"), root);

            if (capacity != null) Place(capacity, Corner.BottomLeft, Margin, Margin, new Vector2(300f, 66f), true);
            if (capBar != null) Place(capBar, Corner.BottomLeft, Margin, Margin + 70f, new Vector2(300f, 10f));

            // --- Sag alt: sonraki hedef (rotanavigation) ---
            RectTransform nextStop = Reanchor(FindChild(root, "NextStop"), root);
            if (nextStop != null)
                Place(nextStop, Corner.BottomRight, -Margin, Margin + 30f, new Vector2(460f, 28f), false, TextAnchor.MiddleRight);

            // --- Alt orta: baglamsal ipucu ---
            RectTransform hint = Reanchor(FindChild(root, "Hint"), root);
            if (hint != null)
                Place(hint, Corner.BottomCenter, 0f, Margin, new Vector2(760f, 22f), false, TextAnchor.MiddleCenter);

            // --- Sag ust: minimap (harita) ---
            //
            // Minimap koku bos bir kapsayici (sizeDelta 0,0); gercek
            // boyut ve konum `Background` cocugunda. Koku tasimak haritayi
            // ekranin disina (x=%100.6) itti. Bu yuzden haritanin kendi
            // cocuklari yerlestirilir: noktalar Background'a bagli oldugu
            // icin onunla birlikte tasinir.
            if (minimap != null)
            {
                var mapRoot = minimap.transform as RectTransform;
                if (mapRoot != null)
                {
                    // Koku tum ekrana yay: cocuklar kenarlanabilsin.
                    mapRoot.anchorMin = Vector2.zero;
                    mapRoot.anchorMax = Vector2.one;
                    mapRoot.pivot = new Vector2(0.5f, 0.5f);
                    mapRoot.offsetMin = Vector2.zero;
                    mapRoot.offsetMax = Vector2.zero;

                    // Harita paneli "Area" adli cocuktur; "Background" onun
                    // arka planidir. Alanin KENDISI tasinir, arka plan,
                    // noktalar ve "HARITA" yazisi onunla birlikte gelir.
                    //
                    // Yanlis isim verilince ("Background") panel bulunamaz,
                    // kok esnemis anchor'la sol-usta kalir - olcumde
                    // x=%7.8 cikti.
                    var mapPanel = FindChild(mapRoot, "Area");

                    if (mapPanel != null)
                    {
                        float side = mapPanel.sizeDelta.x > 1f
                            ? mapPanel.sizeDelta.x
                            : UITheme.MinimapSize;

                        mapPanel.anchorMin = mapPanel.anchorMax = new Vector2(1f, 1f);
                        mapPanel.pivot = new Vector2(1f, 1f);
                        mapPanel.sizeDelta = new Vector2(side, side);
                        mapPanel.anchoredPosition = new Vector2(-Margin, -Margin);
                    }
                }
            }

            // Artik bos bir cerceve olmayan eski istatistik panelini gizle.
            if (stats != null && stats.childCount == 0) stats.gameObject.SetActive(false);

            Debug.Log("[HudLayout] yerlesim uygulandi: money=" + (money != null) +
                      " capacity=" + (capacity != null) +
                      " nextStop=" + (nextStop != null) +
                      " hint=" + (hint != null) +
                      " minimap=" + (minimap != null) +
                      " kok=" + root.name);
        }

        /// <summary>
        /// Elemani Canvas kokune tasir.
        ///
        /// ASAMA 19: para/seviye/saat `Stats` panelinin cocuklariydi ve
        /// panelin kendi kosesine kenarlaniyorlardi. Panel sol altta
        /// oldugu icin "sol ust" deyimi bile altta kaliyordu.
        /// Kokun altina alininca kose koordinatlari Canvas'a gore
        /// calisir.
        /// </summary>
        private static RectTransform Reanchor(RectTransform rt, RectTransform canvasRoot)
        {
            if (rt == null) return null;
            if (rt.parent != canvasRoot) rt.SetParent(canvasRoot, false);
            return rt;
        }

        private enum Corner { TopLeft, TopRight, BottomLeft, BottomRight, BottomCenter }

        private static void Place(RectTransform rt, Corner corner,
                                  float offsetX, float offsetY, Vector2 size,
                                  bool fromBottom = false,
                                  TextAnchor align = TextAnchor.MiddleLeft)
        {
            if (rt == null) return;

            Vector2 min, max, pivot;
            switch (corner)
            {
                case Corner.TopLeft:
                    min = max = new Vector2(0f, 1f); pivot = new Vector2(0f, 1f); break;
                case Corner.TopRight:
                    min = max = new Vector2(1f, 1f); pivot = new Vector2(1f, 1f); break;
                case Corner.BottomLeft:
                    min = max = new Vector2(0f, 0f); pivot = new Vector2(0f, 0f); break;
                case Corner.BottomRight:
                    min = max = new Vector2(1f, 0f); pivot = new Vector2(1f, 0f); break;
                default:
                    min = max = new Vector2(0.5f, 0f); pivot = new Vector2(0.5f, 0f); break;
            }

            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.pivot = pivot;
            rt.sizeDelta = size;
            rt.anchoredPosition = new Vector2(offsetX, offsetY);

            // Yukaridan asagiya dizilimde ilk eleman en ustte: sola yasli
            // metinler sola, cok satirli kapasite metni ustten asagi.
            var text = rt.GetComponent<Text>();
            if (text != null)
            {
                text.alignment = align;
                if (fromBottom) text.alignment = TextAnchor.LowerLeft;
            }
        }

        /// <summary>Adla arar; bulamazsa null (sahne farkli olabilir).</summary>
        private static RectTransform FindChild(RectTransform root, string name)
        {
            var all = root.GetComponentsInChildren<RectTransform>(true);
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && all[i].name == name) return all[i];
            return null;
        }
    }
}
