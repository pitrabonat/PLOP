using UnityEngine;

namespace Plop.Presentation
{
    /// <summary>
    /// "Soft Toy Minimalism" palette. Base colors from the creative brief; everything else is a warmer or
    /// cooler variation of them. No pure black shadows and no pure white highlights anywhere.
    /// Gameplay color ids (Plop.Core.PlopColor) are mapped to palette slots here — presentation only.
    /// </summary>
    public static class PlopPalette
    {
        // ---- brief ----
        public static readonly Color Milk = Hex(0xF7F2EB);      // background
        public static readonly Color Berry = Hex(0xE9AEC4);
        public static readonly Color Mint = Hex(0xA8D7CD);
        public static readonly Color Lavender = Hex(0xB6ADE3);
        public static readonly Color Honey = Hex(0xE7BC85);
        public static readonly Color Ink = Hex(0x4B4652);       // primary dark text

        // ---- extended slots (for levels with more than 4 colors) ----
        public static readonly Color Sky = Hex(0xA9C7E6);
        public static readonly Color Peach = Hex(0xF1B9A3);

        // ---- board ----
        public static readonly Color PlayBackground = Hex(0xFCE8D9);
        public static readonly Color BoardPanel = Hex(0xEBC4AE);
        public static readonly Color TrayRim = Hex(0xF5D5BF);
        public static readonly Color ContactShadow = Hex(0x996B64);
        public static readonly Color FaceInk = Hex(0x533143);
        public static readonly Color Tile = Hex(0xFFF0E3);
        public static readonly Color TileShade = Hex(0xF2D9C5);
        public static readonly Color PebbleTop = Hex(0xC8B6C2);      // soft lilac-grey toy block
        public static readonly Color PebbleSide = Hex(0xA18A96);
        public static readonly Color PebbleHighlight = Hex(0xDFD0D9);
        public static readonly Color Shadow = Hex(0x7D7184);         // tinted, never black
        public static readonly Color Highlight = Hex(0xFFFCF7);      // warm, never pure white

        // ---- UI ----
        public static readonly Color MutedText = Hex(0x8E8697);
        public static readonly Color ButtonFill = Hex(0xFFFCF8);
        public static readonly Color ButtonPressed = Hex(0xF1E9E1);
        public static readonly Color ButtonDisabled = new Color(1f, 0.988f, 0.973f, 0.55f);

        /// <summary>Palette slot for each gameplay color id (Red, Blue, Yellow, Green, Purple, Orange).</summary>
        private static readonly Color[] Slots = { Berry, Mint, Honey, Lavender, Sky, Peach };
        public static readonly string[] SlotNames = { "Berry", "Mint", "Honey", "Lavender", "Sky", "Peach" };

        public static int SlotCount => Slots.Length;
        public static Color Slot(int colorId) => Slots[Mathf.Clamp(colorId, 0, Slots.Length - 1)];
        public static Color Slot(Core.PlopColor c) => Slot((int)c);

        // The same identities use a more saturated material pigment on the rendered toys.
        private static readonly Color[] ToySlots =
        {
            Hex(0xF696AF), Hex(0x94D8C6), Hex(0xF1BF7A),
            Hex(0xC7A4E3), Hex(0x9EC6E8), Hex(0xF0B095)
        };

        public static Color ToySlot(int colorId) => ToySlots[Mathf.Clamp(colorId, 0, ToySlots.Length - 1)];
        public static Color PlopBody(int colorId) => Color.Lerp(ToySlot(colorId), Ink, 0.04f);
        public static Color NestOuter(int colorId) => Color.Lerp(ToySlot(colorId), TrayRim, 0.14f);
        public static Color NestInner(int colorId) => Color.Lerp(ToySlot(colorId), Highlight, 0.32f);

        public static Color Hex(uint rgb, float alpha = 1f) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);

        public static Color WithAlpha(this Color c, float a) { c.a = a; return c; }
    }
}
