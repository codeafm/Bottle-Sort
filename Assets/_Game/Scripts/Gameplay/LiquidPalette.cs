using BottleSort.Core;
using UnityEngine;

namespace BottleSort.Gameplay
{
    // Утверждённые цвета жидкостей в порядке Palette.Letters (RBYGPCOKLNWD)
    public static class LiquidPalette
    {
        static readonly Color32[] Colors =
        {
            new Color32(0xC9, 0x28, 0x3A, 0xFF),
            new Color32(0x2D, 0x5F, 0xC4, 0xFF),
            new Color32(0xE3, 0xB2, 0x1E, 0xFF),
            new Color32(0x2A, 0x9E, 0x4A, 0xFF),
            new Color32(0x6E, 0x3B, 0xB0, 0xFF),
            new Color32(0x14, 0x95, 0xAA, 0xFF),
            new Color32(0xD9, 0x77, 0x2A, 0xFF),
            new Color32(0xC7, 0x32, 0x78, 0xFF),
            new Color32(0x85, 0xB5, 0x24, 0xFF),
            new Color32(0x8A, 0x55, 0x30, 0xFF),
            new Color32(0xD5, 0xD9, 0xE6, 0xFF),
            new Color32(0x2A, 0x2D, 0x57, 0xFF),
        };

        public static Color Get(int color)
        {
            if (color < 0 || color >= Colors.Length)
                return Color.white;
            return Colors[color];
        }

        public static Color Highlight(int color) => Color.Lerp(Get(color), Color.white, 0.28f);

        public static Color Edge(int color)
        {
            var c = Get(color);
            return new Color(c.r * 0.62f, c.g * 0.62f, c.b * 0.62f, 1f);
        }

        public static int Count => Palette.MaxColors;
    }
}
