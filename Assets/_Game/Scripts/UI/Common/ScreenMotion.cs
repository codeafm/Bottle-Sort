using System;
using System.Collections.Generic;
using UnityEngine;

namespace BottleSort.UI
{
    // Элементы экрана появляются или уходят по очереди: сдвиг, масштаб, прозрачность.
    // Шаги правятся в Inspector; сдвиг в pt макета, время в секундах.
    public sealed class ScreenMotion : MonoBehaviour
    {
        const float Pt = 2.5f;

        [Serializable]
        public struct Step
        {
            public RectTransform target;
            [Tooltip("Откуда элемент приезжает (появление) или куда уезжает (уход), pt")]
            public Vector2 offset;
            [Tooltip("Масштаб в начале появления или в конце ухода")]
            public float scale;
            public float delay;
            public float duration;
            [Tooltip("Лёгкая пружинка в конце появления")]
            public bool spring;
        }

        [SerializeField] bool appear = true;
        [SerializeField] Step[] steps = new Step[0];

        struct Rest
        {
            public Vector2 Position;
            public Vector3 Scale;
            public float Alpha;
            public bool Raycasts;
        }

        readonly Dictionary<RectTransform, Rest> _rest = new Dictionary<RectTransform, Rest>();

        public float Duration(float speed = 1f)
        {
            float total = 0f;
            foreach (var step in steps)
                total = Mathf.Max(total, step.delay + step.duration);
            return total / speed;
        }

        // speed > 1 — короткая версия: всё быстрее и плотнее. startDelay — пауза перед всеми шагами.
        public void Play(float speed = 1f, Action done = null, float startDelay = 0f)
        {
            foreach (var step in steps)
            {
                if (step.target == null)
                    continue;
                var target = step.target;
                var rest = RestOf(target);
                var group = GroupOf(target);
                float duration = Mathf.Max(0.01f, step.duration / speed);
                float delay = startDelay + step.delay / speed;
                float scale = step.scale > 0f ? step.scale : 1f;
                var offset = step.offset * Pt;

                if (appear)
                {
                    Apply(target, rest, offset, scale, 0f);
                    // Кнопка нажимается, как только начала появляться.
                    group.blocksRaycasts = false;
                    Tweens.Run(target, "motion", duration, t =>
                    {
                        group.blocksRaycasts = rest.Raycasts;
                        float k = 1f - t;
                        Apply(target, rest, offset * k, Mathf.LerpUnclamped(scale, 1f, t), Mathf.Clamp01(t * 1.6f));
                    }, step.spring ? Ease.OutBack : Ease.OutCubic, null, delay);
                }
                else
                {
                    group.blocksRaycasts = false;
                    Tweens.Run(target, "motion", duration,
                        t => Apply(target, rest, offset * t, Mathf.LerpUnclamped(1f, scale, t), 1f - t),
                        Ease.InOutSine, null, delay);
                }
            }
            if (done != null)
                Tweens.Delay(this, "done", startDelay + Duration(speed), done);
        }

        // Сразу в покой: экран виден целиком (например, для снимков).
        public void Finish()
        {
            Tweens.Kill(this);
            foreach (var step in steps)
            {
                if (step.target == null)
                    continue;
                Tweens.Kill(step.target, "motion");
                var rest = RestOf(step.target);
                Apply(step.target, rest, Vector2.zero, 1f, 1f);
                GroupOf(step.target).blocksRaycasts = rest.Raycasts;
            }
        }

        Rest RestOf(RectTransform target)
        {
            if (_rest.TryGetValue(target, out var rest))
                return rest;
            var group = GroupOf(target);
            rest = new Rest
            {
                Position = target.anchoredPosition,
                Scale = target.localScale,
                Alpha = group.alpha,
                Raycasts = group.blocksRaycasts
            };
            _rest[target] = rest;
            return rest;
        }

        static CanvasGroup GroupOf(RectTransform target)
        {
            var group = target.GetComponent<CanvasGroup>();
            return group != null ? group : target.gameObject.AddComponent<CanvasGroup>();
        }

        // Покачивающиеся элементы (IdleMotion) двигаем через их сдвиг, иначе они перетрут позицию.
        static void Apply(RectTransform target, Rest rest, Vector2 offset, float scale, float visibility)
        {
            var idle = target.GetComponent<IdleMotion>();
            if (idle != null && idle.isActiveAndEnabled && idle.MotionKind == IdleMotion.Kind.Float)
                idle.Offset = offset;
            else
                target.anchoredPosition = rest.Position + offset;
            target.localScale = rest.Scale * scale;
            target.GetComponent<CanvasGroup>().alpha = rest.Alpha * visibility;
        }
    }
}
