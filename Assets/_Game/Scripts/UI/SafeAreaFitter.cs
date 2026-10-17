using UnityEngine;

namespace BottleSort.UI
{
    // Вешать на корневую панель, растянутую на весь Canvas.
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        // Подменная safe area в пикселях экрана: в batchmode её нет, а сравнение с макетом идёт с вырезом iPhone.
        public static Rect? Simulated;

        RectTransform _rect;
        Rect _lastSafeArea;
        Vector2Int _lastScreenSize;

        void OnEnable()
        {
            _rect = (RectTransform)transform;
            Apply();
        }

        void Update()
        {
            if (SafeArea != _lastSafeArea ||
                Screen.width != _lastScreenSize.x || Screen.height != _lastScreenSize.y)
                Apply();
        }

        static Rect SafeArea => Simulated ?? Screen.safeArea;

        void Apply()
        {
            _lastSafeArea = SafeArea;
            _lastScreenSize = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0)
                return;

            var min = _lastSafeArea.position;
            var max = _lastSafeArea.position + _lastSafeArea.size;
            min.x /= Screen.width;
            min.y /= Screen.height;
            max.x /= Screen.width;
            max.y /= Screen.height;

            _rect.anchorMin = min;
            _rect.anchorMax = max;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }
    }
}
