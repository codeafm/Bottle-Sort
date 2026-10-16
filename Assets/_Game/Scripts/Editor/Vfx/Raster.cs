using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BottleSort.Editor.Vfx
{
    // Маленький софтверный растеризатор под пути из макета: заливка nonzero, обводка с круглыми концами,
    // пунктир, градиенты и размытие. Цвет хранится премультиплицированным — так же его ждут шейдеры.
    public sealed class Raster
    {
        const int Subrows = 4;

        public readonly int Width;
        public readonly int Height;
        public readonly float Scale;
        public readonly Vector2 Origin;
        public readonly Color[] Pixels;

        // origin — координаты макета в левом верхнем углу, scale — пикселей на единицу макета
        public Raster(int width, int height, Vector2 origin, float scale)
        {
            Width = width;
            Height = height;
            Origin = origin;
            Scale = scale;
            Pixels = new Color[width * height];
        }

        public Vector2 ToUnits(float px, float py) => Origin + new Vector2(px / Scale, py / Scale);

        public float[] NewMask() => new float[Width * Height];

        public float[] Fill(SvgPath path)
        {
            var mask = NewMask();
            if (path == null || path.IsEmpty)
                return mask;

            var edges = new List<(Vector2 a, Vector2 b)>();
            foreach (var contour in path.Contours)
            {
                var pts = contour.Points;
                for (int i = 0; i < pts.Count; i++)
                {
                    var a = ToPixels(pts[i]);
                    var b = ToPixels(pts[(i + 1) % pts.Count]);
                    if (Mathf.Abs(a.y - b.y) > 1e-6f)
                        edges.Add((a, b));
                }
            }

            var bounds = path.Bounds;
            int yMin = Mathf.Max(0, Mathf.FloorToInt((bounds.yMin - Origin.y) * Scale) - 1);
            int yMax = Mathf.Min(Height - 1, Mathf.CeilToInt((bounds.yMax - Origin.y) * Scale) + 1);
            var crossings = new List<(float x, int dir)>();

            for (int y = yMin; y <= yMax; y++)
            for (int s = 0; s < Subrows; s++)
            {
                float sy = y + (s + 0.5f) / Subrows;
                crossings.Clear();
                foreach (var (a, b) in edges)
                {
                    bool down = a.y < b.y;
                    float y0 = down ? a.y : b.y, y1 = down ? b.y : a.y;
                    if (sy < y0 || sy >= y1)
                        continue;
                    float t = (sy - a.y) / (b.y - a.y);
                    crossings.Add((a.x + (b.x - a.x) * t, down ? 1 : -1));
                }
                if (crossings.Count < 2)
                    continue;
                crossings.Sort((p, q) => p.x.CompareTo(q.x));

                int winding = 0;
                for (int i = 0; i < crossings.Count - 1; i++)
                {
                    winding += crossings[i].dir;
                    if (winding != 0)
                        AddSpan(mask, y, crossings[i].x, crossings[i + 1].x, 1f / Subrows);
                }
            }

            for (int i = 0; i < mask.Length; i++)
                mask[i] = Mathf.Clamp01(mask[i]);
            return mask;
        }

        void AddSpan(float[] mask, int y, float x0, float x1, float weight)
        {
            x0 = Mathf.Max(0f, x0);
            x1 = Mathf.Min(Width, x1);
            if (x1 <= x0)
                return;
            int first = Mathf.FloorToInt(x0), last = Mathf.Min(Width - 1, Mathf.FloorToInt(x1));
            int row = y * Width;
            for (int x = first; x <= last; x++)
            {
                float cover = Mathf.Min(x1, x + 1) - Mathf.Max(x0, x);
                if (cover > 0f)
                    mask[row + x] += cover * weight;
            }
        }

        // softness в единицах макета: 0 — чёткий край, больше — мягкий штрих
        public float[] Stroke(SvgPath path, float width, float softness = 0f, float dash = 0f, float gap = 0f, bool closeOpen = false)
        {
            var mask = NewMask();
            if (path == null || path.IsEmpty)
                return mask;

            float half = width * 0.5f * Scale;
            float soft = Mathf.Max(1f, softness * Scale);
            foreach (var contour in path.Contours)
            {
                var pts = new List<Vector2>(contour.Points);
                if (contour.Closed || closeOpen)
                    pts.Add(pts[0]);
                if (pts.Count == 1)
                    pts.Add(pts[0]);

                var segments = dash > 0f ? Dashed(pts, dash, gap) : new List<List<Vector2>> { pts };
                foreach (var line in segments)
                    for (int i = 0; i < line.Count - 1; i++)
                        StrokeSegment(mask, ToPixels(line[i]), ToPixels(line[i + 1]), half, soft);
            }
            return mask;
        }

        static List<List<Vector2>> Dashed(List<Vector2> pts, float dash, float gap)
        {
            var result = new List<List<Vector2>>();
            List<Vector2> current = new List<Vector2> { pts[0] };
            bool drawing = true;
            float left = dash;
            for (int i = 0; i < pts.Count - 1; i++)
            {
                Vector2 a = pts[i], b = pts[i + 1];
                float length = Vector2.Distance(a, b), walked = 0f;
                while (length - walked > left)
                {
                    walked += left;
                    var p = Vector2.Lerp(a, b, walked / length);
                    if (drawing)
                    {
                        current.Add(p);
                        result.Add(current);
                    }
                    else
                        current = new List<Vector2> { p };
                    drawing = !drawing;
                    left = drawing ? dash : gap;
                }
                left -= length - walked;
                if (drawing)
                    current.Add(b);
            }
            if (drawing && current.Count > 1)
                result.Add(current);
            return result;
        }

        void StrokeSegment(float[] mask, Vector2 a, Vector2 b, float half, float soft)
        {
            float reach = half + soft;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - reach));
            int x1 = Mathf.Min(Width - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + reach));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - reach));
            int y1 = Mathf.Min(Height - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + reach));
            var ab = b - a;
            float lengthSq = Mathf.Max(ab.sqrMagnitude, 1e-6f);

            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSq);
                float d = Vector2.Distance(p, a + ab * t);
                float cover = Mathf.Clamp01((half - d) / soft + 0.5f);
                int i = y * Width + x;
                if (cover > mask[i])
                    mask[i] = cover;
            }
        }

        public void Paint(float[] mask, Color color, float opacity = 1f)
        {
            Paint(mask, _ => color, opacity);
        }

        public void Paint(float[] mask, Func<Vector2, Color> paint, float opacity = 1f)
        {
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                int i = y * Width + x;
                float cover = mask[i] * opacity;
                if (cover <= 0f)
                    continue;
                var c = paint(ToUnits(x + 0.5f, y + 0.5f));
                float a = c.a * cover;
                var dst = Pixels[i];
                Pixels[i] = new Color(c.r * a + dst.r * (1f - a), c.g * a + dst.g * (1f - a), c.b * a + dst.b * (1f - a), a + dst.a * (1f - a));
            }
        }

        Vector2 ToPixels(Vector2 units) => (units - Origin) * Scale;

        public static float[] Multiply(float[] a, float[] b)
        {
            var result = new float[a.Length];
            for (int i = 0; i < a.Length; i++)
                result[i] = a[i] * b[i];
            return result;
        }

        public static float[] Union(params float[][] masks)
        {
            var result = new float[masks[0].Length];
            foreach (var mask in masks)
                for (int i = 0; i < result.Length; i++)
                    result[i] = 1f - (1f - result[i]) * (1f - mask[i]);
            return result;
        }

        public static float[] Subtract(float[] a, float[] b)
        {
            var result = new float[a.Length];
            for (int i = 0; i < a.Length; i++)
                result[i] = a[i] * (1f - b[i]);
            return result;
        }

        // Гаусс через три прохода бокс-фильтра, sigma в единицах макета
        public float[] Blur(float[] source, float sigma)
        {
            float sigmaPx = sigma * Scale;
            int radius = Mathf.Max(1, Mathf.RoundToInt(Mathf.Sqrt(12f * sigmaPx * sigmaPx / 3f + 1f) * 0.5f));
            var a = (float[])source.Clone();
            var b = new float[a.Length];
            for (int pass = 0; pass < 3; pass++)
            {
                BoxBlur(a, b, radius, true);
                BoxBlur(b, a, radius, false);
            }
            return a;
        }

        void BoxBlur(float[] src, float[] dst, int radius, bool horizontal)
        {
            int lines = horizontal ? Height : Width;
            int length = horizontal ? Width : Height;
            float norm = 1f / (radius * 2 + 1);
            for (int line = 0; line < lines; line++)
            {
                float sum = 0f;
                for (int k = -radius; k <= radius; k++)
                    sum += Sample(src, line, k, length, horizontal);
                for (int k = 0; k < length; k++)
                {
                    dst[Index(line, k, horizontal)] = sum * norm;
                    sum += Sample(src, line, k + radius + 1, length, horizontal) - Sample(src, line, k - radius, length, horizontal);
                }
            }
        }

        float Sample(float[] src, int line, int k, int length, bool horizontal) =>
            k < 0 || k >= length ? 0f : src[Index(line, k, horizontal)];

        int Index(int line, int k, bool horizontal) => horizontal ? line * Width + k : k * Width + line;

        public static Func<Vector2, Color> LinearX(float x0, float x1, params (float t, Color c)[] stops) =>
            p => Gradient(stops, Mathf.InverseLerp(x0, x1, p.x));

        public static Func<Vector2, Color> LinearY(float y0, float y1, params (float t, Color c)[] stops) =>
            p => Gradient(stops, Mathf.InverseLerp(y0, y1, p.y));

        public static Func<Vector2, Color> Radial(Vector2 center, Vector2 radius, params (float t, Color c)[] stops) =>
            p => Gradient(stops, Mathf.Clamp01(new Vector2((p.x - center.x) / radius.x, (p.y - center.y) / radius.y).magnitude));

        public static Color Gradient((float t, Color c)[] stops, float t)
        {
            if (t <= stops[0].t)
                return stops[0].c;
            for (int i = 1; i < stops.Length; i++)
                if (t <= stops[i].t)
                    return Color.Lerp(stops[i - 1].c, stops[i].c, Mathf.InverseLerp(stops[i - 1].t, stops[i].t, t));
            return stops[stops.Length - 1].c;
        }

        public static Color Hex(string hex, float alpha = 1f)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            c.a = alpha;
            return c;
        }

        // Строки идут сверху вниз, а у текстуры Unity первая строка — нижняя
        public void SavePng(string path)
        {
            var bytes = new Color32[Pixels.Length];
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                bytes[(Height - 1 - y) * Width + x] = Pixels[y * Width + x];
            SavePng(path, Width, Height, bytes);
        }

        public static void SavePng(string path, int width, int height, Color32[] pixels)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
