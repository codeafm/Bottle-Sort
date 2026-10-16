using UnityEngine;

namespace BottleSort.UI
{
    // Бесконечные «живые» анимации из макета: floaty, halo, twinkle, shine, вращение лучей.
    public sealed class IdleMotion : MonoBehaviour
    {
        public enum Kind
        {
            Float,
            Pulse,
            Spin,
            Twinkle,
            Sweep
        }

        [SerializeField] Kind kind = Kind.Float;
        [SerializeField] float period = 3.2f;
        [SerializeField] float amount = 20f;
        [SerializeField] float phase;
        // Для Sweep: откуда и докуда бежит блик по X, и какую долю периода он стоит на месте.
        [SerializeField] Vector2 sweepRange = new Vector2(-200f, 800f);
        [SerializeField, Range(0f, 0.9f)] float sweepPause = 0.4f;

        RectTransform _rect;
        CanvasGroup _group;
        Vector2 _basePosition;
        Vector3 _baseScale;
        float _time;

        public void Configure(Kind newKind, float newPeriod, float newAmount, float newPhase = 0f)
        {
            kind = newKind;
            period = newPeriod;
            amount = newAmount;
            phase = newPhase;
        }

        public void SetSweep(Vector2 range, float pause)
        {
            sweepRange = range;
            sweepPause = pause;
        }

        void OnEnable()
        {
            _rect = transform as RectTransform;
            _group = GetComponent<CanvasGroup>();
            if (_rect != null)
                _basePosition = _rect.anchoredPosition;
            _baseScale = transform.localScale;
            _time = phase * period;
        }

        void OnDisable()
        {
            if (_rect != null && (kind == Kind.Float || kind == Kind.Sweep))
                _rect.anchoredPosition = _basePosition;
            if (kind == Kind.Pulse || kind == Kind.Twinkle)
                transform.localScale = _baseScale;
        }

        void Update()
        {
            _time += Time.unscaledDeltaTime;
            float cycle = Mathf.Repeat(_time / period, 1f);
            float wave = (1f - Mathf.Cos(cycle * Mathf.PI * 2f)) / 2f;

            switch (kind)
            {
                case Kind.Float:
                    _rect.anchoredPosition = _basePosition + new Vector2(0f, wave * amount);
                    break;
                case Kind.Pulse:
                    transform.localScale = _baseScale * (1f + wave * amount);
                    if (_group != null)
                        _group.alpha = 0.65f + wave * 0.35f;
                    break;
                case Kind.Spin:
                    transform.localRotation = Quaternion.Euler(0f, 0f, -_time * amount);
                    break;
                case Kind.Twinkle:
                    transform.localScale = _baseScale * (0.6f + 0.4f * wave);
                    if (_group != null)
                        _group.alpha = 0.1f + 0.65f * wave;
                    break;
                case Kind.Sweep:
                    float moving = 1f - sweepPause;
                    float t = Mathf.Clamp01(cycle / moving);
                    float eased = Easing.Apply(Ease.InOutSine, t);
                    _rect.anchoredPosition = new Vector2(Mathf.Lerp(sweepRange.x, sweepRange.y, eased), _basePosition.y);
                    break;
            }
        }
    }
}
