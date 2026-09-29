using UnityEngine;
using UnityEngine.UI;

namespace KargoSim.UI
{
    /// <summary>
    /// Envanter panelini KODDA kurar.
    ///
    /// NEDEN AYRI DOSYA ve sahne yerine:
    /// Bu projede sahne YAML'i elle duzenlendi ve uc kez bozuldu
    /// (kamera baglantilari, tag'ler, RectTransform'lar). Uretim kodu
    /// her zaman daha guvenilir olmustur.
    ///
    /// Panel baslangicta KAPALI; I tusu ile acilir.
    /// </summary>
    public static class CargoInventoryBuilder
    {
        public const string PanelName = "CargoInventory";

        /// <summary>Paneli kurar ve baglar. Icine yazilan butun metin.</summary>
        public static CargoInventoryView Build(Canvas canvas, Cargo.CargoSystem cargo,
                                               Player.CourierController courier,
                                               Delivery.DeliverySystem delivery)
        {
            if (canvas == null || courier == null) return null;

            var rootGo = new GameObject(PanelName, typeof(RectTransform));
            rootGo.transform.SetParent(canvas.transform, false);
            var root = (RectTransform)rootGo.transform;
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = new Vector2(760f, 480f);
            root.anchoredPosition = Vector2.zero;

            var group = rootGo.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            var hand = AddText(root, "Hand", font, 15, UITheme.TextPrimary,
                               TextAnchor.UpperLeft, new Vector2(-350f, 120f), 700f);
            var vehicle = AddText(root, "Vehicle", font, 15, UITheme.BarFill,
                                  TextAnchor.UpperLeft, new Vector2(-350f, -80f), 700f);
            var capacity = AddText(root, "Capacity", font, 14, UITheme.Money,
                                   TextAnchor.UpperLeft, new Vector2(-350f, -200f), 700f);
            var hint = AddText(root, "Hint", font, 13, UITheme.TextMuted,
                               TextAnchor.UpperLeft, new Vector2(-350f, -230f), 700f);

            var view = rootGo.AddComponent<CargoInventoryView>();
            Set(view, "handText", hand);
            Set(view, "vehicleText", vehicle);
            Set(view, "capacityText", capacity);
            Set(view, "hintText", hint);
            Set(view, "group", group);
            Set(view, "panel", root);

            view.Configure(cargo, courier, delivery);

            rootGo.SetActive(false);
            return view;
        }

        /// <summary>
        /// SerializedObject KULLANMAZ.
        ///
        /// UnityEditor yalnizca Editor'de derlenir; runtime assembly'de
        /// referans vermek build'i kirmaya yol acar. Bu yuzden alan
        /// atamasi reflection ile yapilir.
        /// </summary>
        private static void Set(object target, string field, object value)
        {
            var f = target.GetType().GetField(field,
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            if (f != null) f.SetValue(target, value);
        }

        private static Text AddText(RectTransform parent, string name, Font font,
                                    int size, Color color, TextAnchor anchor,
                                    Vector2 pos, float width)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(width, 190f);

            var text = go.AddComponent<Text>();
            if (font != null) text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }
    }
}
