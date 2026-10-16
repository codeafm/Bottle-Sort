using System;

namespace BottleSort.Core
{
    // Порядок цветов совпадает с LEVEL_DESIGN.md §9.4.6: первые 10 вводятся сразу, W с 1101, D с 1601.
    public static class Palette
    {
        public const string Letters = "RBYGPCOKLNWD";
        public const int MaxColors = 12;
        public const int AnyColor = -1;

        public const byte Red = 0;
        public const byte Blue = 1;
        public const byte Yellow = 2;
        public const byte Green = 3;
        public const byte Purple = 4;
        public const byte Cyan = 5;
        public const byte Orange = 6;
        public const byte Pink = 7;
        public const byte Lime = 8;
        public const byte Brown = 9;
        public const byte Pearl = 10;
        public const byte Ink = 11;

        public static char ToLetter(int color) => Letters[color];

        public static byte FromLetter(char letter)
        {
            int index = Letters.IndexOf(char.ToUpperInvariant(letter));
            if (index < 0)
                throw new ArgumentException($"Unknown color letter '{letter}'");
            return (byte)index;
        }

        // Пары, которые на маленьком экране путаются: K/R, L/G, C/B, D/B.
        public static bool AreLookalikes(int a, int b)
        {
            return IsPair(a, b, Pink, Red) || IsPair(a, b, Lime, Green)
                || IsPair(a, b, Cyan, Blue) || IsPair(a, b, Ink, Blue);
        }

        private static bool IsPair(int a, int b, int x, int y) => (a == x && b == y) || (a == y && b == x);
    }
}
