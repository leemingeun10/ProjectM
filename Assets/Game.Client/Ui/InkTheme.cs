using UnityEngine;

namespace Game.Client.Ui
{
    // Palette for the ink-wash (먹색 무협풍) UI skin. Every screen pulls its colours from here so the
    // look can be retuned in one place.
    public static class InkTheme
    {
        // Backgrounds: ink black to charcoal.
        public static readonly Color Ink = Hex(0x1A1A1A);
        public static readonly Color Charcoal = Hex(0x2B2A28);
        // Translucent dark panel laid over the 3D scene or a background.
        public static readonly Color InkPanel = Hex(0x1C1B19, 0.88f);
        public static readonly Color InkPanelLight = Hex(0x34322E, 0.92f);
        public static readonly Color Dim = Hex(0x0E0E0D, 0.78f);

        // Hanji (Korean paper) cards.
        public static readonly Color Hanji = Hex(0xE8E0CC);
        public static readonly Color HanjiShade = Hex(0xD3C8AE);

        // Primary accent: vermilion, matched to the sun in the ink-wash backdrop.
        public static readonly Color Vermilion = Hex(0xC8432A);
        public static readonly Color VermilionDeep = Hex(0x8E2C1A);

        // Secondary accent: dull gold for borders, dividers and ranks.
        public static readonly Color Gold = Hex(0xB8923F);
        public static readonly Color GoldDim = Hex(0xB8923F, 0.45f);

        public static readonly Color TextLight = Hex(0xF2ECDD);
        public static readonly Color TextMuted = Hex(0xA39A88);
        public static readonly Color TextDark = Hex(0x1F1D1A);

        // Disabled controls: a flat dark stone with faded text, on any background.
        public static readonly Color DisabledFill = Hex(0x3A3834);
        public static readonly Color DisabledText = Hex(0x77705F);

        public static Color Hex(int rgb, float alpha = 1f)
            => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);
    }
}
