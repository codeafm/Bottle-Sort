using BottleSort.Core;
using UnityEngine;

namespace BottleSort.UI
{
    // Цвета из исходников макета (Bg, Game, Bottle.dc.html).
    public static class UiColors
    {
        public static Color Hex(string hex, float alpha = 1f)
        {
            ColorUtility.TryParseHtmlString(hex.StartsWith("#") ? hex : "#" + hex, out var color);
            color.a = alpha;
            return color;
        }

        public struct ThemeColors
        {
            public Color Center;
            public Color Middle;
            public Color Edge;
            public Color Halo;
            public Color Spark;
        }

        public static ThemeColors Of(Theme theme)
        {
            switch (theme)
            {
                case Theme.Violet: return Make("#3A1E78", "#1A0F45", "#0B0624", "#9A5CFF", "#D2AAFF");
                case Theme.Teal: return Make("#0B3E4E", "#05222D", "#020E14", "#1BA9C0", "#96F0FF");
                case Theme.Rose: return Make("#6E1240", "#3A0A26", "#1A0412", "#FF3D8D", "#FFAAD2");
                case Theme.Emerald: return Make("#0F5A3E", "#06301F", "#02150D", "#2DFFB0", "#AAFFD2");
                case Theme.Amber: return Make("#6B4310", "#2E1C06", "#120A02", "#FFB547", "#FFDC96");
                default: return Make("#12306E", "#071338", "#040A22", "#3A7BFF", "#96BEFF");
            }
        }

        static ThemeColors Make(string center, string middle, string edge, string halo, string spark) => new ThemeColors
        {
            Center = Hex(center), Middle = Hex(middle), Edge = Hex(edge), Halo = Hex(halo), Spark = Hex(spark)
        };

        // Основной, светлый и тёмный тон каждого цвета палитры, порядок как в Core.Palette.
        static readonly string[,] Liquids =
        {
            { "#C9283A", "#D86471", "#7D1924" }, { "#2D5FC4", "#688CD5", "#1C3B7A" }, { "#E3B21E", "#EBC85D", "#8D6E13" },
            { "#2A9E4A", "#66B97D", "#1A622E" }, { "#6E3BB0", "#9772C6", "#44256D" }, { "#1495AA", "#56B3C2", "#0C5C69" },
            { "#D9772A", "#E49D66", "#874A1A" }, { "#C73278", "#D76B9E", "#7B1F4A" }, { "#85B524", "#A7CA61", "#527016" },
            { "#8A5530", "#AB856A", "#56351E" }, { "#D5D9E6", "#E1E4ED", "#84878F" }, { "#2A2D57", "#666886", "#1A1C36" }
        };

        public static Color Liquid(int color, int tone = 0) =>
            color >= 0 && color < Palette.MaxColors ? Hex(Liquids[color, tone]) : Color.clear;

        public static (Color top, Color bottom) TagOf(int level)
        {
            if (HandmadeLevels.IsTutorial(level))
                return (Hex("#4FD8FF"), Hex("#1E9A8C"));
            switch (LevelPlan.TypeOf(level))
            {
                case LevelType.Special: return (Hex("#FF8BCB"), Hex("#D81B60"));
                case LevelType.BigFlask: return (Hex("#4FD8FF"), Hex("#0870E8"));
                case LevelType.Rescue: return (Hex("#FFB066"), Hex("#E06A00"));
                case LevelType.Boss:
                case LevelType.ChapterBoss: return (Hex("#FFD957"), Hex("#E07800"));
                default: return (Hex("#7BE84A"), Hex("#1E9A2C"));
            }
        }

        public static readonly Color Selected = Hex("#35F4FF");
        public static readonly Color HintGlow = Hex("#FFC83D");
        public static readonly Color DoneGlow = Hex("#FFEC96");
    }
}
