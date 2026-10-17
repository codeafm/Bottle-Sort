using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BottleSort.UI
{
    // linear-gradient из макета по вершинам. Если задан средний стоп (например, 0% / 60% / 100%),
    // треугольники режутся по линии стопа, иначе цвет между вершинами 9-slice лёг бы не туда.
    [RequireComponent(typeof(Graphic))]
    public sealed class UiGradient : BaseMeshEffect
    {
        [SerializeField] Color top = Color.white;
        [SerializeField] Color bottom = Color.white;
        [SerializeField] bool horizontal;
        [SerializeField] bool hasMiddle;
        [SerializeField] Color middle = Color.white;
        [SerializeField, Range(0.01f, 0.99f)] float middleAt = 0.5f;

        static readonly List<UIVertex> Vertices = new List<UIVertex>();
        static readonly List<UIVertex> Split = new List<UIVertex>();

        public void SetColors(Color first, Color second, bool isHorizontal = false)
        {
            top = first;
            bottom = second;
            horizontal = isHorizontal;
            hasMiddle = false;
            graphic.SetVerticesDirty();
        }

        public void SetColors(Color first, Color mid, float midAt, Color second)
        {
            top = first;
            middle = mid;
            middleAt = midAt;
            bottom = second;
            hasMiddle = true;
            graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0)
                return;

            Vertices.Clear();
            vh.GetUIVertexStream(Vertices);
            var rect = graphic.rectTransform.rect;
            if (hasMiddle)
            {
                float cut = horizontal ? Mathf.Lerp(rect.xMin, rect.xMax, middleAt) : Mathf.Lerp(rect.yMax, rect.yMin, middleAt);
                SplitTriangles(cut);
            }

            for (int i = 0; i < Vertices.Count; i++)
            {
                var vertex = Vertices[i];
                float t = horizontal
                    ? Mathf.InverseLerp(rect.xMin, rect.xMax, vertex.position.x)
                    : Mathf.InverseLerp(rect.yMax, rect.yMin, vertex.position.y);
                vertex.color = (Color32)((Color)vertex.color * Evaluate(t));
                Vertices[i] = vertex;
            }
            vh.Clear();
            vh.AddUIVertexTriangleStream(Vertices);
        }

        Color Evaluate(float t)
        {
            if (!hasMiddle)
                return Color.Lerp(top, bottom, t);
            return t < middleAt
                ? Color.Lerp(top, middle, t / middleAt)
                : Color.Lerp(middle, bottom, (t - middleAt) / (1f - middleAt));
        }

        float Axis(UIVertex v) => horizontal ? v.position.x : v.position.y;

        // Каждый треугольник, который пересекает линию стопа, превращается в три.
        void SplitTriangles(float cut)
        {
            Split.Clear();
            for (int i = 0; i + 2 < Vertices.Count; i += 3)
            {
                var a = Vertices[i];
                var b = Vertices[i + 1];
                var c = Vertices[i + 2];
                bool sa = Axis(a) > cut, sb = Axis(b) > cut, sc = Axis(c) > cut;
                if (sa == sb && sb == sc)
                {
                    Split.Add(a);
                    Split.Add(b);
                    Split.Add(c);
                    continue;
                }

                // Повернём так, чтобы одиночная вершина по свою сторону линии была первой, сохраняя обход.
                if (sb != sa && sb != sc)
                    (a, b, c) = (b, c, a);
                else if (sc != sa && sc != sb)
                    (a, b, c) = (c, a, b);

                var ab = Lerp(a, b, (cut - Axis(a)) / (Axis(b) - Axis(a)));
                var ac = Lerp(a, c, (cut - Axis(a)) / (Axis(c) - Axis(a)));
                Split.Add(a);
                Split.Add(ab);
                Split.Add(ac);
                Split.Add(ab);
                Split.Add(b);
                Split.Add(c);
                Split.Add(ab);
                Split.Add(c);
                Split.Add(ac);
            }
            Vertices.Clear();
            Vertices.AddRange(Split);
        }

        static UIVertex Lerp(UIVertex a, UIVertex b, float t)
        {
            var v = a;
            v.position = Vector3.Lerp(a.position, b.position, t);
            v.color = Color32.Lerp(a.color, b.color, t);
            v.uv0 = Vector4.Lerp(a.uv0, b.uv0, t);
            v.uv1 = Vector4.Lerp(a.uv1, b.uv1, t);
            return v;
        }
    }
}
