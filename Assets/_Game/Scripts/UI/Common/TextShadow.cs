using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace BottleSort.UI
{
    // text-shadow из макета через underlay. Материалы общие на одинаковые тени, чтобы не ломать батчинг.
    [RequireComponent(typeof(TMP_Text))]
    public sealed class TextShadow : MonoBehaviour
    {
        [SerializeField] Color color = new Color(0f, 0f, 0f, 0.3f);
        [SerializeField] float offsetY = -0.6f;

        static readonly Dictionary<(Material, Color, float), Material> Cache = new Dictionary<(Material, Color, float), Material>();
        static readonly int UnderlayColor = Shader.PropertyToID("_UnderlayColor");
        static readonly int UnderlayOffsetY = Shader.PropertyToID("_UnderlayOffsetY");
        static readonly int UnderlayDilate = Shader.PropertyToID("_UnderlayDilate");

        public void Set(Color shadowColor, float offset)
        {
            color = shadowColor;
            offsetY = offset;
            Apply();
        }

        void Start() => Apply();

        void Apply()
        {
            var text = GetComponent<TMP_Text>();
            if (text.font == null)
                return;
            var source = text.font.material;
            var key = (source, color, offsetY);
            if (!Cache.TryGetValue(key, out var material) || material == null)
            {
                material = new Material(source) { name = source.name + " Shadow" };
                material.EnableKeyword("UNDERLAY_ON");
                material.SetColor(UnderlayColor, color);
                material.SetFloat(UnderlayOffsetY, offsetY);
                material.SetFloat(UnderlayDilate, 0.1f);
                Cache[key] = material;
            }
            text.fontSharedMaterial = material;
        }
    }
}
