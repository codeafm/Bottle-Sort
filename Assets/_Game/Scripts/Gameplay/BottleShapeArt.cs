using System;
using UnityEngine;

namespace BottleSort.Gameplay
{
    public static class BottleShapeIds
    {
        public const string Classic = "classic";
        public const string RoundFlask = "roundflask";
        // Мерная колба из макета; старые уровни и сохранения ещё ссылаются на этот id, рисуется круглой колбой
        public const string Cylinder = "cylinder";
        public const string Wine = "wine";
        public const string Jug = "jug";
        public const string Teapot = "teapot";
        public const string Glass = "glass";
        public const string Flask = "flask";
        public const string Potion = "potion";
        public const string Jar = "jar";

        public static readonly string[] All = { Classic, RoundFlask, Wine, Jug, Teapot, Glass, Flask, Potion, Jar };

        // Фигурки спасений, BOTTLE_MODELS §5.2, в порядке появления
        public static readonly string[] Figures = { "hippo", "whale", "owl", "turtle", "elephant", "fish", "fox", "bear", "rabbit", "dragon" };
    }

    // Все размеры формы — в единицах макета (viewBox 100x240, y вниз). В мир переводит BottleGeometry.
    // Фигурка нарисована в viewBox 200x240 и сдвинута на 50 влево, чтобы её середина тоже была на x = 50.
    [Serializable]
    public sealed class BottleShapeArt
    {
        public string id;
        public Texture2D back;
        public Texture2D front;
        public Texture2D mask;
        public float liquidTop;
        public float liquidBottom;
        public Vector2 mouth;
        public Rect outline;
        public Rect textureArea = BottleGeometry.TextureArea;
        // Ширина коробки viewBox, в которую раскладка ставит форму: 100 у бутылок, 200 у фигурок
        public float boxWidth = 100f;
        // Где по x лежит жидкость: по этой полосе идёт блик и тёмный край слоя
        public Vector2 liquidSpan = new Vector2(16f, 84f);
        public bool isFigure;
        // Ширина жидкости через каждую единицу высоты от дна. Пусто — слои равной высоты.
        public float[] layerWidths;
        public float layerWidthPower = 0.5f;
        // Узкое горло над широким шаром: блик и тёмный край слоя идут по горлу (x0, x1) выше y = z,
        // по liquidSpan ниже y = w, между ними плавно. w <= z — не используется.
        public Vector4 neckTone;

        // Высоты верхних краёв слоёв от дна жидкости в edges[0..capacity], дальше повторяется последнее значение.
        // Без профиля слои равные; с профилем каждый слой забирает равную долю ширина^power,
        // поэтому в широкой части слои ниже, в узкой выше, а на вид одинаково заметны.
        public static void ComputeEdges(float[] widths, float power, float height, int capacity, float[] edges)
        {
            capacity = Math.Max(1, capacity);
            if (widths == null || widths.Length < 2)
            {
                for (int k = 0; k < edges.Length; k++)
                    edges[k] = height * Math.Min(k, capacity) / capacity;
                return;
            }

            float step = height / (widths.Length - 1);
            var cumulative = new float[widths.Length];
            for (int i = 1; i < widths.Length; i++)
            {
                float a = Mathf.Pow(Mathf.Max(widths[i - 1], 0.1f), power);
                float b = Mathf.Pow(Mathf.Max(widths[i], 0.1f), power);
                cumulative[i] = cumulative[i - 1] + (a + b) * 0.5f * step;
            }
            float total = cumulative[cumulative.Length - 1];
            int j = 1;
            edges[0] = 0f;
            for (int k = 1; k < edges.Length; k++)
            {
                if (k >= capacity)
                {
                    edges[k] = height;
                    continue;
                }
                float target = total * k / capacity;
                while (j < cumulative.Length - 1 && cumulative[j] < target)
                    j++;
                float t = Mathf.InverseLerp(cumulative[j - 1], cumulative[j], target);
                edges[k] = (j - 1 + t) * step;
            }
        }
    }

    [Serializable]
    public sealed class RescueHeroArt
    {
        public string id;
        public Sprite sprite;
        public Sprite happySprite;
        public Sprite pawSprite;
        public Sprite signSprite;
    }

    public static class BottleGeometry
    {
        // При масштабе 1 бутылка шириной 100 единиц занимает 2 мировые единицы — это ширина 70 в макете на 1080x1920
        public const float UnitSize = 0.02f;
        public static readonly Vector2 Base = new Vector2(50f, 230f);
        public static readonly Rect TextureArea = new Rect(-30f, -30f, 160f, 300f);
        public static readonly Rect FigureTextureArea = new Rect(-80f, -30f, 260f, 300f);

        public static Vector2 ToLocal(Vector2 units) =>
            new Vector2((units.x - Base.x) * UnitSize, (Base.y - units.y) * UnitSize);

        public static float ToLocalY(float unitsY) => (Base.y - unitsY) * UnitSize;

        public static Rect ToLocal(Rect units)
        {
            var min = ToLocal(new Vector2(units.xMin, units.yMax));
            var max = ToLocal(new Vector2(units.xMax, units.yMin));
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
