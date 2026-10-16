using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace BottleSort.Editor.Vfx
{
    // Разбор атрибута d из макета. Кривые сразу превращаются в ломаные с шагом около step единиц.
    public sealed class SvgPath
    {
        public sealed class Contour
        {
            public readonly List<Vector2> Points = new List<Vector2>();
            public bool Closed;
        }

        public readonly List<Contour> Contours = new List<Contour>();

        public bool IsEmpty => Contours.Count == 0;

        public Rect Bounds
        {
            get
            {
                float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
                foreach (var contour in Contours)
                foreach (var p in contour.Points)
                {
                    minX = Mathf.Min(minX, p.x);
                    minY = Mathf.Min(minY, p.y);
                    maxX = Mathf.Max(maxX, p.x);
                    maxY = Mathf.Max(maxY, p.y);
                }
                return IsEmpty ? Rect.zero : Rect.MinMaxRect(minX, minY, maxX, maxY);
            }
        }

        public static SvgPath Parse(string d, float step = 0.4f)
        {
            var path = new SvgPath();
            if (string.IsNullOrWhiteSpace(d))
                return path;

            var reader = new Reader(d);
            Contour contour = null;
            Vector2 current = Vector2.zero, start = Vector2.zero, lastControl = Vector2.zero;
            char command = ' ', previous = ' ';

            while (reader.HasMore)
            {
                if (reader.PeekCommand(out char c))
                    command = c;
                else if (command == ' ')
                    throw new FormatException("Path must start with a command: " + d);

                bool relative = char.IsLower(command);
                Vector2 origin = relative ? current : Vector2.zero;

                switch (char.ToUpperInvariant(command))
                {
                    case 'M':
                        current = origin + reader.Point();
                        start = current;
                        contour = new Contour();
                        contour.Points.Add(current);
                        path.Contours.Add(contour);
                        // Следующие пары после M — это уже L
                        command = relative ? 'l' : 'L';
                        previous = 'M';
                        continue;
                    case 'L':
                        current = origin + reader.Point();
                        contour.Points.Add(current);
                        break;
                    case 'H':
                        current = new Vector2((relative ? current.x : 0f) + reader.Number(), current.y);
                        contour.Points.Add(current);
                        break;
                    case 'V':
                        current = new Vector2(current.x, (relative ? current.y : 0f) + reader.Number());
                        contour.Points.Add(current);
                        break;
                    case 'Q':
                    {
                        var control = origin + reader.Point();
                        var end = origin + reader.Point();
                        AddQuad(contour, current, control, end, step);
                        lastControl = control;
                        current = end;
                        break;
                    }
                    case 'T':
                    {
                        bool chained = "QqTt".IndexOf(previous) >= 0;
                        var control = chained ? 2f * current - lastControl : current;
                        var end = origin + reader.Point();
                        AddQuad(contour, current, control, end, step);
                        lastControl = control;
                        current = end;
                        break;
                    }
                    case 'C':
                    {
                        var c1 = origin + reader.Point();
                        var c2 = origin + reader.Point();
                        var end = origin + reader.Point();
                        AddCubic(contour, current, c1, c2, end, step);
                        lastControl = c2;
                        current = end;
                        break;
                    }
                    case 'S':
                    {
                        bool chained = "CcSs".IndexOf(previous) >= 0;
                        var c1 = chained ? 2f * current - lastControl : current;
                        var c2 = origin + reader.Point();
                        var end = origin + reader.Point();
                        AddCubic(contour, current, c1, c2, end, step);
                        lastControl = c2;
                        current = end;
                        break;
                    }
                    case 'A':
                    {
                        float rx = reader.Number(), ry = reader.Number(), rotation = reader.Number();
                        bool large = reader.Flag(), sweep = reader.Flag();
                        var end = origin + reader.Point();
                        AddArc(contour, current, end, rx, ry, rotation, large, sweep, step);
                        current = end;
                        break;
                    }
                    case 'Z':
                        contour.Closed = true;
                        current = start;
                        previous = 'Z';
                        command = ' ';
                        continue;
                    default:
                        throw new FormatException($"Unsupported path command '{command}' in {d}");
                }

                previous = command;
            }

            return path;
        }

        public static SvgPath Ellipse(float cx, float cy, float rx, float ry, int segments = 96)
        {
            var path = new SvgPath();
            var contour = new Contour { Closed = true };
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                contour.Points.Add(new Vector2(cx + Mathf.Cos(a) * rx, cy + Mathf.Sin(a) * ry));
            }
            path.Contours.Add(contour);
            return path;
        }

        public static SvgPath RoundedRect(float x, float y, float w, float h, float r)
        {
            r = Mathf.Min(r, w * 0.5f, h * 0.5f);
            string F(float v) => v.ToString(CultureInfo.InvariantCulture);
            return Parse($"M{F(x + r)} {F(y)} H{F(x + w - r)} A{F(r)} {F(r)} 0 0 1 {F(x + w)} {F(y + r)} V{F(y + h - r)} " +
                         $"A{F(r)} {F(r)} 0 0 1 {F(x + w - r)} {F(y + h)} H{F(x + r)} A{F(r)} {F(r)} 0 0 1 {F(x)} {F(y + h - r)} " +
                         $"V{F(y + r)} A{F(r)} {F(r)} 0 0 1 {F(x + r)} {F(y)} Z");
        }

        public SvgPath Merge(SvgPath other)
        {
            var merged = new SvgPath();
            merged.Contours.AddRange(Contours);
            if (other != null)
                merged.Contours.AddRange(other.Contours);
            return merged;
        }

        public SvgPath Translate(Vector2 offset)
        {
            var moved = new SvgPath();
            foreach (var contour in Contours)
            {
                var copy = new Contour { Closed = contour.Closed };
                foreach (var p in contour.Points)
                    copy.Points.Add(p + offset);
                moved.Contours.Add(copy);
            }
            return moved;
        }

        static int Segments(float length, float step) => Mathf.Clamp(Mathf.CeilToInt(length / step), 2, 400);

        static void AddQuad(Contour contour, Vector2 p0, Vector2 p1, Vector2 p2, float step)
        {
            int n = Segments(Vector2.Distance(p0, p1) + Vector2.Distance(p1, p2), step);
            for (int i = 1; i <= n; i++)
            {
                float t = i / (float)n, u = 1f - t;
                contour.Points.Add(u * u * p0 + 2f * u * t * p1 + t * t * p2);
            }
        }

        static void AddCubic(Contour contour, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float step)
        {
            int n = Segments(Vector2.Distance(p0, p1) + Vector2.Distance(p1, p2) + Vector2.Distance(p2, p3), step);
            for (int i = 1; i <= n; i++)
            {
                float t = i / (float)n, u = 1f - t;
                contour.Points.Add(u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3);
            }
        }

        // Перевод из конечных точек в центр по SVG 1.1, приложение F.6.5
        static void AddArc(Contour contour, Vector2 from, Vector2 to, float rx, float ry, float rotationDeg,
            bool large, bool sweep, float step)
        {
            rx = Mathf.Abs(rx);
            ry = Mathf.Abs(ry);
            if (rx < 1e-4f || ry < 1e-4f || (from - to).sqrMagnitude < 1e-8f)
            {
                contour.Points.Add(to);
                return;
            }

            float phi = rotationDeg * Mathf.Deg2Rad, cos = Mathf.Cos(phi), sin = Mathf.Sin(phi);
            var half = (from - to) * 0.5f;
            float x1 = cos * half.x + sin * half.y;
            float y1 = -sin * half.x + cos * half.y;

            float lambda = x1 * x1 / (rx * rx) + y1 * y1 / (ry * ry);
            if (lambda > 1f)
            {
                float s = Mathf.Sqrt(lambda);
                rx *= s;
                ry *= s;
            }

            float num = rx * rx * ry * ry - rx * rx * y1 * y1 - ry * ry * x1 * x1;
            float den = rx * rx * y1 * y1 + ry * ry * x1 * x1;
            float coef = Mathf.Sqrt(Mathf.Max(0f, num / den)) * (large == sweep ? -1f : 1f);
            float cx1 = coef * rx * y1 / ry;
            float cy1 = -coef * ry * x1 / rx;
            var mid = (from + to) * 0.5f;
            var center = new Vector2(cos * cx1 - sin * cy1 + mid.x, sin * cx1 + cos * cy1 + mid.y);

            float startAngle = Angle(new Vector2(1, 0), new Vector2((x1 - cx1) / rx, (y1 - cy1) / ry));
            float delta = Angle(new Vector2((x1 - cx1) / rx, (y1 - cy1) / ry), new Vector2((-x1 - cx1) / rx, (-y1 - cy1) / ry));
            if (!sweep && delta > 0f) delta -= Mathf.PI * 2f;
            if (sweep && delta < 0f) delta += Mathf.PI * 2f;

            int n = Segments(Mathf.Abs(delta) * Mathf.Max(rx, ry), step);
            for (int i = 1; i <= n; i++)
            {
                float a = startAngle + delta * i / n;
                float ex = rx * Mathf.Cos(a), ey = ry * Mathf.Sin(a);
                contour.Points.Add(new Vector2(cos * ex - sin * ey + center.x, sin * ex + cos * ey + center.y));
            }
        }

        static float Angle(Vector2 u, Vector2 v)
        {
            float sign = u.x * v.y - u.y * v.x < 0f ? -1f : 1f;
            float dot = Mathf.Clamp(Vector2.Dot(u, v) / (u.magnitude * v.magnitude), -1f, 1f);
            return sign * Mathf.Acos(dot);
        }

        sealed class Reader
        {
            readonly string text;
            int pos;

            public Reader(string text) => this.text = text;

            public bool HasMore
            {
                get
                {
                    SkipSeparators();
                    return pos < text.Length;
                }
            }

            public bool PeekCommand(out char command)
            {
                SkipSeparators();
                command = text[pos];
                if (char.IsLetter(command) && command != 'e' && command != 'E')
                {
                    pos++;
                    return true;
                }
                return false;
            }

            public Vector2 Point() => new Vector2(Number(), Number());

            public bool Flag()
            {
                SkipSeparators();
                return text[pos++] == '1';
            }

            public float Number()
            {
                SkipSeparators();
                int begin = pos;
                if (pos < text.Length && (text[pos] == '-' || text[pos] == '+'))
                    pos++;
                bool dot = false;
                while (pos < text.Length)
                {
                    char c = text[pos];
                    if (char.IsDigit(c)) pos++;
                    else if (c == '.' && !dot) { dot = true; pos++; }
                    else if ((c == 'e' || c == 'E') && pos + 1 < text.Length)
                    {
                        pos++;
                        if (text[pos] == '-' || text[pos] == '+') pos++;
                    }
                    else break;
                }
                if (begin == pos)
                    throw new FormatException($"Number expected at {pos} in {text}");
                return float.Parse(text.Substring(begin, pos - begin), NumberStyles.Float, CultureInfo.InvariantCulture);
            }

            void SkipSeparators()
            {
                while (pos < text.Length && (char.IsWhiteSpace(text[pos]) || text[pos] == ','))
                    pos++;
            }
        }
    }
}
