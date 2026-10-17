using BottleSort.Gameplay;
using UnityEngine;

namespace BottleSort.UI
{
    // Полка поля: раскладку считает UI, а рисует мировой ShelfView от vfx-art. Как в макете, полупрозрачная
    // полка лежит поверх донышек, и сквозь неё видна тень-контакт; полка на Canvas была бы поверх всего мира
    // и закрывала бы тень. Бутылка, которую переливают, поднимается выше (+30) и полку не задевает.
    [RequireComponent(typeof(RectTransform))]
    public sealed class ShelfViewAdapter : MonoBehaviour
    {
        const int InFrontOfBottles = 5;
        const float TopOffset = 6.5f;

        static Transform _worldRoot;
        static readonly Vector3[] Corners = new Vector3[4];

        ShelfView _view;
        SpriteRenderer _sprite;
        Camera _camera;
        Canvas _canvas;
        CanvasGroup[] _groups;

        void Awake()
        {
            if (_worldRoot == null)
                _worldRoot = new GameObject("Field Shelves").transform;
            var go = new GameObject("Shelf", typeof(SpriteRenderer));
            go.transform.SetParent(_worldRoot, false);
            _view = go.AddComponent<ShelfView>();
            _sprite = go.GetComponent<SpriteRenderer>();
            _sprite.sortingOrder = InFrontOfBottles;
            _camera = Camera.main;
        }

        void OnEnable()
        {
            if (_view != null)
                _view.gameObject.SetActive(true);
            _groups = GetComponentsInParent<CanvasGroup>(true);
        }

        void OnDisable()
        {
            if (_view != null)
                _view.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (_view != null)
                Destroy(_view.gameObject);
        }

        void OnTransformParentChanged()
        {
            _groups = GetComponentsInParent<CanvasGroup>(true);
            _canvas = null;
        }

        void LateUpdate()
        {
            if (_view == null || _camera == null)
                return;
            if (_canvas == null)
            {
                _canvas = GetComponentInParent<Canvas>();
                if (_canvas == null)
                    return;
                _canvas = _canvas.rootCanvas;
            }
            var uiCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;

            // Верхний край ячейки — линия, на которой стоят донышки.
            var rect = (RectTransform)transform;
            rect.GetWorldCorners(Corners);
            Vector2 left = RectTransformUtility.WorldToScreenPoint(uiCamera, Corners[1]);
            Vector2 right = RectTransformUtility.WorldToScreenPoint(uiCamera, Corners[2]);
            var worldLeft = _camera.ScreenToWorldPoint(new Vector3(left.x, left.y, 0f));
            var worldRight = _camera.ScreenToWorldPoint(new Vector3(right.x, right.y, 0f));

            // Ширина ячейки задана в пикселях макета: ShelfView считает её при масштабе 1, а масштаб добирает доска.
            float cssWidth = rect.rect.width;
            float scale = (worldRight.x - worldLeft.x) / (cssWidth * ShelfView.CssToWorld);
            var position = (worldLeft + worldRight) * 0.5f;
            // Верх полки у ShelfView выше его точки; в макете верх полки на 2.5 pt ниже дна стекла.
            position.y -= TopOffset * ShelfView.CssToWorld * scale;
            position.z = 0f;
            var shelf = _view.transform;
            shelf.position = position;
            shelf.localScale = new Vector3(scale, scale, 1f);
            float width = cssWidth * ShelfView.CssToWorld;
            if (!Mathf.Approximately(_view.Width, width))
                _view.Width = width;

            float alpha = 1f;
            if (_groups != null)
                foreach (var group in _groups)
                    alpha *= group.alpha;
            var color = _sprite.color;
            if (!Mathf.Approximately(color.a, alpha))
            {
                color.a = alpha;
                _sprite.color = color;
            }
        }
    }
}
