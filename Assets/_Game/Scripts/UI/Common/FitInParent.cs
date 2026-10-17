using UnityEngine;

namespace BottleSort.UI
{
    // Держит content целиком внутри этого прямоугольника: если места меньше, чем его размер, равномерно уменьшает,
    // но никогда не растягивает. Полка с бутылками в меню на коротких экранах не лезет на кнопки.
    [ExecuteAlways, RequireComponent(typeof(RectTransform))]
    public sealed class FitInParent : MonoBehaviour
    {
        [SerializeField] RectTransform content;

        void OnEnable() => Fit();

        void OnRectTransformDimensionsChange() => Fit();

        void Fit()
        {
            if (content == null)
                return;
            var area = ((RectTransform)transform).rect.size;
            var size = content.rect.size;
            if (size.x <= 0f || size.y <= 0f)
                return;
            float scale = Mathf.Min(1f, area.x / size.x, area.y / size.y);
            content.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
