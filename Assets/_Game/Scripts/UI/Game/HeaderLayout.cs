using UnityEngine;

namespace BottleSort.UI
{
    // Шапка игры — сетка 112 | центр | 112 pt. Боковые колонки (домой, монеты) не трогаем,
    // а плашка уровня в центре ужимается, если экран уже 390 pt, чтобы ни на что не наезжать.
    [RequireComponent(typeof(RectTransform))]
    public sealed class HeaderLayout : MonoBehaviour
    {
        [SerializeField] RectTransform plate;
        [SerializeField] RectTransform plateShadow;
        [SerializeField] float plateWidth;
        [SerializeField] float sideColumn;
        [SerializeField] float minGap;

        void OnEnable() => Apply();

        void OnRectTransformDimensionsChange() => Apply();

        void Apply()
        {
            if (plate == null)
                return;
            float width = ((RectTransform)transform).rect.width;
            float room = width - 2f * (sideColumn + minGap);
            float plateNow = Mathf.Clamp(room, 0f, plateWidth);
            // Тень шире плашки на размытие с обеих сторон, эту разницу и сохраняем.
            if (plateShadow != null)
                plateShadow.sizeDelta = new Vector2(plateShadow.sizeDelta.x - plate.sizeDelta.x + plateNow, plateShadow.sizeDelta.y);
            plate.sizeDelta = new Vector2(plateNow, plate.sizeDelta.y);
        }
    }
}
