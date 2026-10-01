using UnityEngine;
using UnityEngine.UI;

namespace KargoSim.UI
{
    /// <summary>
    /// NESNE OZELLIK PENCERESI (gorev #31) - SAHNEDE HIC YOKTU.
    ///
    /// OLCUM: `Game.unity` icinde `tooltipView` alanina ATIF 0 kez.
    /// Yani `UIManager` bu alani bos birakiyor ve odaktaki esyayi
    /// fareyle uzatip ozelliklerini GORMEK mumkun degildi - gorev
    /// tamamlanmis sayilmisti ama hicbir sey calismiyordu.
    ///
    /// NEDEN AYRI DOSYA ve KODDA: envanter paneli gibi. Bu projede
    /// sahne YAML'i elle duzenlendi ve uc kez bozuldu; ayrica alani
    /// elle atamak Editor gerektiriyor.
    /// </summary>
    public static class TooltipBuilder
    {
        public const string PanelName = "NesneOzellik";

        public static ParcelTooltipView Build(Canvas canvas)
        {
            if (canvas == null) return null;

            var rootGo = new GameObject(PanelName, typeof(RectTransform));
            rootGo.transform.SetParent(canvas.transform, false);
            var root = (RectTransform)rootGo.transform;
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(320f, 120f);
            root.anchoredPosition = new Vector2(0f, -190f);

            var bg = rootGo.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.09f, 0.11f, 0.94f);
            bg.raycastTarget = false;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            var title = MakeText(root, "Baslik", font, 15, UITheme.Accent,
                                 new Vector2(14f, 10f), new Vector2(292f, 22f));
            var body = MakeText(root, "Icerik", font, 13, Color.white,
                                new Vector2(14f, 36f), new Vector2(292f, 74f));

            var view = rootGo.AddComponent<ParcelTooltipView>();
            Set(view, "titleText", title);
            Set(view, "bodyText", body);
            Set(view, "group", rootGo.AddComponent<CanvasGroup>());
            Set(view, "panel", root);

            rootGo.SetActive(false);
            return view;
        }

        private static Text MakeText(RectTransform parent, string name, Font font,
                                     int size, Color color, Vector2 topLeft,
                                     Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = topLeft;
            rt.sizeDelta = sizeDelta;

            var text = go.AddComponent<Text>();
            if (font != null) text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = UnityEngine.VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static void Set(object target, string field, object value)
        {
            var f = target.GetType().GetField(field,
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            if (f != null) f.SetValue(target, value);
        }
    }
}
