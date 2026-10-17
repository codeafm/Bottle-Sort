using BottleSort.Core;
using BottleSort.Gameplay;
using BottleSort.Services;
using UnityEngine;

namespace BottleSort.UI
{
    // Мост между ячейкой поля в Canvas и BottleView, который живёт в мире камеры.
    // Раскладку и касания считает UI, бутылка каждый кадр встаёт на место ячейки и повторяет её прозрачность.
    [RequireComponent(typeof(RectTransform))]
    public sealed class BottleViewAdapter : MonoBehaviour, IBottleView
    {
        // Масштаб 1 у BottleView — ширина 2 мировых единицы (viewBox 100 из макета).
        const float WorldWidthAtScaleOne = 2f;
        const float DimmedAlpha = 0.6f;

        [SerializeField] BottleView prefab;

        static Transform _worldRoot;
        static readonly Vector3[] Corners = new Vector3[4];

        BottleView _view;
        Camera _camera;
        Canvas _canvas;
        CanvasGroup[] _groups;
        BottleGlow _glow;
        bool _completed;
        bool _dimmed;

        public RectTransform Root => (RectTransform)transform;
        public bool MovesItself => true;
        public BottleView View => _view;

        void Awake()
        {
            if (_worldRoot == null)
                _worldRoot = new GameObject("Field Bottles").transform;
            _view = Instantiate(prefab, _worldRoot);
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

        public void Show(Bottle bottle, int unit)
        {
            _view.Show(bottle);
            // Модель из магазина подменяет классическую бутылку только на картинке, правила не меняются.
            string skin = ShopCatalog.BottleShapeFor(bottle.Shape, bottle.Capacity, PlayerData.Current.bottleSkin);
            if (skin != bottle.Shape)
                _view.SetShape(skin);
        }

        public void SetGlow(BottleGlow glow)
        {
            _glow = glow;
            ApplyState();
        }

        public void SetCompleted(bool completed, bool animated)
        {
            if (completed == _completed)
                return;
            _completed = completed;
            if (completed && animated)
                _view.PlayCompleted();
            else
                ApplyState();
        }

        public void SetDimmed(bool dimmed) => _dimmed = dimmed;

        public void Shake() => _view.Shake();

        void ApplyState()
        {
            switch (_glow)
            {
                case BottleGlow.Selected: _view.SetState(BottleVisualState.Selected); break;
                case BottleGlow.Hint: _view.SetState(BottleVisualState.Hint); break;
                default: _view.SetState(_completed ? BottleVisualState.Completed : BottleVisualState.Normal); break;
            }
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

            Root.GetWorldCorners(Corners);
            Vector2 left = RectTransformUtility.WorldToScreenPoint(uiCamera, Corners[0]);
            Vector2 right = RectTransformUtility.WorldToScreenPoint(uiCamera, Corners[3]);
            var bottom = (left + right) * 0.5f;
            float widthPixels = right.x - left.x;
            // Ячейка — коробка viewBox 100×240, а BottleView ставится точкой дна стекла (y = 230), на 10 единиц выше.
            bottom.y += widthPixels * 0.1f;
            float unitsPerPixel = 2f * _camera.orthographicSize / _camera.pixelHeight;

            var world = _camera.ScreenToWorldPoint(new Vector3(bottom.x, bottom.y, 0f));
            world.z = 0f;
            var bottleTransform = _view.transform;
            bottleTransform.position = world;
            float scale = widthPixels * unitsPerPixel / WorldWidthAtScaleOne;
            bottleTransform.localScale = new Vector3(scale, scale, 1f);

            float alpha = _dimmed ? DimmedAlpha : 1f;
            if (_groups != null)
                foreach (var group in _groups)
                    alpha *= group.alpha;
            if (!Mathf.Approximately(_view.Alpha, alpha))
                _view.Alpha = alpha;
        }
    }
}
