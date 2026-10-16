using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BottleSort.UI
{
    // linear-gradient из макета по вершинам. У 9-slice вершины есть на линиях нарезки,
    // поэтому промежуточная точка (stop) ложится примерно туда, где у панели край скругления.
    [RequireComponent(typeof(Graphic))]
    public sealed class UiGradient : BaseMeshEffect
    {
        [SerializeField] Color top = Color.white;
        [SerializeField] Color bottom = Color.white;
        [SerializeField] bool horizontal;

        static readonly List<UIVertex> Vertices = new List<UIVertex>();

        public void SetColors(Color first, Color second, bool isHorizontal = false)
        {
            top = first;
            bottom = second;
            horizontal = isHorizontal;
            graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0)
                return;

            Vertices.Clear();
            vh.GetUIVertexStream(Vertices);
            var rect = graphic.rectTransform.rect;
            for (int i = 0; i < Vertices.Count; i++)
            {
                var vertex = Vertices[i];
                float t = horizontal
                    ? Mathf.InverseLerp(rect.xMin, rect.xMax, vertex.position.x)
                    : Mathf.InverseLerp(rect.yMax, rect.yMin, vertex.position.y);
                var tint = Color.Lerp(top, bottom, t);
                vertex.color = (Color32)((Color)vertex.color * tint);
                Vertices[i] = vertex;
            }
            vh.Clear();
            vh.AddUIVertexTriangleStream(Vertices);
        }
    }
}
