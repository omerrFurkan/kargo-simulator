using UnityEngine;

namespace KargoSim.UI
{
    /// <summary>
    /// Arayuz renkleri ve olculer. TEK kaynak: butun ekranlar ayni rengi
    /// ayni yerde okur. Bir rengi burada degistirince tum arayuz degisir.
    ///
    /// Renkler URP/Lit ile degil, arayuzun kendi materialiyle cizilir
    /// (UI/Default), bu yuzden `Color` dogrudan kullanilir.
    /// </summary>
    public static class UITheme
    {
        // ---- Renkler ----
        public static readonly Color PanelBackground = new Color(0.06f, 0.07f, 0.09f, 0.88f);
        public static readonly Color PanelBorder = new Color(1f, 1f, 1f, 0.15f);
        public static readonly Color TextPrimary = new Color(0.95f, 0.96f, 0.98f);
        public static readonly Color TextMuted = new Color(0.62f, 0.66f, 0.72f);
        public static readonly Color TextDisabled = new Color(0.40f, 0.43f, 0.48f);

        public static readonly Color Money = new Color(0.45f, 0.85f, 0.42f);
        public static readonly Color Experience = new Color(0.32f, 0.72f, 0.95f);
        public static readonly Color Warning = new Color(0.98f, 0.72f, 0.25f);
        public static readonly Color Danger = new Color(0.95f, 0.35f, 0.32f);
        public static readonly Color Success = new Color(0.40f, 0.85f, 0.45f);
        public static readonly Color Accent = new Color(0.98f, 0.62f, 0.20f);

        public static readonly Color BarFill = new Color(0.32f, 0.72f, 0.95f);
        public static readonly Color BarBackground = new Color(1f, 1f, 1f, 0.12f);

        public static readonly Color DepotColor = new Color(0.25f, 0.80f, 0.95f);
        public static readonly Color StopColor = new Color(0.98f, 0.72f, 0.25f);
        public static readonly Color VehicleColor = new Color(0.98f, 0.98f, 0.98f);
        public static readonly Color RouteColor = new Color(0.30f, 0.80f, 0.95f);

        // ---- Olculer ----
        public const int FontTiny = 14;
        public const int FontSmall = 18;
        public const int FontNormal = 22;
        public const int FontLarge = 30;
        public const int FontHuge = 44;

        public const float PanelPadding = 14f;
        public const float RowHeight = 34f;
        public const float MinimapSize = 220f;
    }
}
