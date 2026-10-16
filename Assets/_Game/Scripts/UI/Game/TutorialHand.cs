using UnityEngine;

namespace BottleSort.UI
{
    // Белая рука с кругом нажатия (tapHand и tapRing из макета). Следует за ячейкой бутылки,
    // даже когда та поднята, и переезжает к новой цели.
    public sealed class TutorialHand : MonoBehaviour
    {
        const float GestureTime = 1.1f;
        // Смещения жеста заданы в пикселях макета.
        const float Px = 2.5f;

        [SerializeField] RectTransform hand;
        [SerializeField] RectTransform ring;
        [SerializeField] CanvasGroup ringGroup;
        [SerializeField] CanvasGroup group;

        RectTransform _target;
        RectTransform _parent;
        float _period = 1.6f;
        float _time;
        bool _visible;
        Vector2 _travelFrom;
        float _travel = 1f;

        public bool IsVisible => _visible;
        public RectTransform Target => _target;

        void Awake()
        {
            _parent = (RectTransform)transform.parent;
            group.alpha = 0f;
        }

        public void Point(RectTransform target, bool urgent)
        {
            _period = urgent ? 0.8f : 1.6f;
            if (target == _target && _visible)
                return;

            var position = (RectTransform)transform;
            if (_visible && _target != null)
            {
                _travelFrom = position.anchoredPosition;
                _travel = 0f;
            }
            else
            {
                _travel = 1f;
                _time = 0f;
                position.anchoredPosition = TargetPosition(target);
                Tweens.Fade(group, 1f, 0.2f);
            }
            _target = target;
            _visible = true;
        }

        public void SetUrgent(bool urgent) => _period = urgent ? 0.8f : 1.6f;

        public void Hide()
        {
            if (!_visible)
                return;
            _visible = false;
            _target = null;
            Tweens.Fade(group, 0f, 0.15f);
        }

        public void SetScale(float scale) => transform.localScale = new Vector3(scale, scale, 1f);

        // Рука привязана к центру слоя, поэтому её anchoredPosition — это просто точка в координатах слоя.
        Vector2 TargetPosition(RectTransform target) => _parent.InverseTransformPoint(target.position);

        void LateUpdate()
        {
            if (!_visible || _target == null)
                return;

            var self = (RectTransform)transform;
            var goal = TargetPosition(_target);
            if (_travel < 1f)
            {
                _travel = Mathf.Min(1f, _travel + Time.unscaledDeltaTime / 0.35f);
                self.anchoredPosition = Vector2.Lerp(_travelFrom, goal, Easing.Apply(Ease.OutCubic, _travel));
                return;
            }
            self.anchoredPosition = goal;

            _time += Time.unscaledDeltaTime;
            float t = Mathf.Repeat(_time, _period) / GestureTime;
            AnimateGesture(Mathf.Clamp01(t));
        }

        void AnimateGesture(float t)
        {
            Vector2 offset;
            float scale;
            if (t < 0.45f)
            {
                float k = Easing.Apply(Ease.InOutSine, t / 0.45f);
                offset = Vector2.Lerp(Vector2.zero, new Vector2(-3f, 6f), k);
                scale = Mathf.Lerp(1f, 1.06f, k);
            }
            else if (t < 0.6f)
            {
                float k = (t - 0.45f) / 0.15f;
                offset = Vector2.Lerp(new Vector2(-3f, 6f), new Vector2(0f, -2f), k);
                scale = Mathf.Lerp(1.06f, 0.94f, k);
            }
            else
            {
                float k = Easing.Apply(Ease.InOutSine, (t - 0.6f) / 0.4f);
                offset = Vector2.Lerp(new Vector2(0f, -2f), Vector2.zero, k);
                scale = Mathf.Lerp(0.94f, 1f, k);
            }
            hand.anchoredPosition = offset * Px;
            hand.localScale = new Vector3(scale, scale, 1f);

            float ringScale;
            float ringAlpha;
            if (t < 0.4f)
            {
                ringScale = 0.3f;
                ringAlpha = 0f;
            }
            else if (t < 0.55f)
            {
                float k = (t - 0.4f) / 0.15f;
                ringScale = Mathf.Lerp(0.3f, 0.6f, k);
                ringAlpha = Mathf.Lerp(0f, 0.9f, k);
            }
            else
            {
                float k = (t - 0.55f) / 0.45f;
                ringScale = Mathf.Lerp(0.6f, 1.6f, k);
                ringAlpha = Mathf.Lerp(0.9f, 0f, k);
            }
            ring.localScale = new Vector3(ringScale, ringScale, 1f);
            ringGroup.alpha = ringAlpha;
        }
    }
}
