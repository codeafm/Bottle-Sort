using System;
using UnityEngine;

namespace BottleSort.Gameplay
{
    public static class BottleShapeIds
    {
        public const string Classic = "classic";
        public const string Cylinder = "cylinder";
        public const string Wine = "wine";
        public const string Jug = "jug";
        public const string Teapot = "teapot";
        public const string Glass = "glass";
        public const string Flask = "flask";
        public const string Potion = "potion";
        public const string Jar = "jar";

        public static readonly string[] All = { Classic, Cylinder, Wine, Jug, Teapot, Glass, Flask, Potion, Jar };
    }

    // Все размеры формы — в единицах макета (viewBox 100x240, y вниз). В мир переводит BottleGeometry.
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
    }

    public static class BottleGeometry
    {
        // При масштабе 1 бутылка шириной 100 единиц занимает 2 мировые единицы — это ширина 70 в макете на 1080x1920
        public const float UnitSize = 0.02f;
        public static readonly Vector2 Base = new Vector2(50f, 230f);
        public static readonly Rect TextureArea = new Rect(-30f, -30f, 160f, 300f);

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
