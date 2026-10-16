using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BottleSort.UI
{
    public enum Ease
    {
        Linear,
        OutCubic,
        InCubic,
        InOutSine,
        OutBack
    }

    public static class Easing
    {
        public static float Apply(Ease ease, float t)
        {
            switch (ease)
            {
                case Ease.OutCubic: return 1f - Mathf.Pow(1f - t, 3f);
                case Ease.InCubic: return t * t * t;
                case Ease.InOutSine: return -(Mathf.Cos(Mathf.PI * t) - 1f) / 2f;
                case Ease.OutBack:
                    // Как cubic-bezier(0.34, 1.56, 0.64, 1) в макете: небольшой перелёт и возврат.
                    const float c1 = 1.70158f;
                    const float c3 = c1 + 1f;
                    return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
                default: return t;
            }
        }
    }

    // Минимальная замена DOTween: один раннер на сцену, твины привязаны к владельцу и каналу.
    // Новый твин на том же канале заменяет старый, поэтому повторные нажатия не копят анимации.
    public sealed class Tweens : MonoBehaviour
    {
        sealed class Tween
        {
            public Object Owner;
            public string Channel;
            public float Delay;
            public float Duration;
            public float Elapsed;
            public Ease Ease;
            public Action<float> Step;
            public Action Done;
        }

        static Tweens _runner;
        readonly List<Tween> _active = new List<Tween>();

        static Tweens Runner
        {
            get
            {
                if (_runner == null)
                {
                    var go = new GameObject("Tweens");
                    DontDestroyOnLoad(go);
                    _runner = go.AddComponent<Tweens>();
                }
                return _runner;
            }
        }

        public static void Run(Object owner, string channel, float duration, Action<float> step,
            Ease ease = Ease.OutCubic, Action done = null, float delay = 0f)
        {
            if (!Application.isPlaying)
            {
                step(1f);
                done?.Invoke();
                return;
            }

            Kill(owner, channel);
            Runner._active.Add(new Tween
            {
                Owner = owner, Channel = channel, Delay = delay, Duration = Mathf.Max(duration, 0.0001f),
                Ease = ease, Step = step, Done = done
            });
        }

        public static void Delay(Object owner, string channel, float seconds, Action done) =>
            Run(owner, channel, seconds, _ => { }, Ease.Linear, done);

        public static void Kill(Object owner, string channel = null)
        {
            if (_runner == null)
                return;
            _runner._active.RemoveAll(t => t.Owner == owner && (channel == null || t.Channel == channel));
        }

        public static bool IsRunning(Object owner, string channel)
        {
            if (_runner == null)
                return false;
            foreach (var tween in _runner._active)
                if (tween.Owner == owner && tween.Channel == channel)
                    return true;
            return false;
        }

        public static void Fade(CanvasGroup group, float to, float duration, Ease ease = Ease.OutCubic, Action done = null)
        {
            float from = group.alpha;
            Run(group, "fade", duration, t => group.alpha = Mathf.LerpUnclamped(from, to, t), ease, done);
        }

        public static void Fade(TMP_Text text, float to, float duration, Action done = null)
        {
            float from = text.alpha;
            Run(text, "fade", duration, t => text.alpha = Mathf.LerpUnclamped(from, to, t), Ease.Linear, done);
        }

        public static void Scale(Transform target, Vector3 to, float duration, Ease ease = Ease.OutCubic, Action done = null,
            float delay = 0f)
        {
            Vector3 from = target.localScale;
            Run(target, "scale", duration, t => target.localScale = Vector3.LerpUnclamped(from, to, t), ease, done, delay);
        }

        public static void Move(RectTransform target, Vector2 to, float duration, Ease ease = Ease.OutCubic, Action done = null)
        {
            Vector2 from = target.anchoredPosition;
            Run(target, "move", duration, t => target.anchoredPosition = Vector2.LerpUnclamped(from, to, t), ease, done);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < _active.Count; i++)
            {
                var tween = _active[i];
                if (tween.Owner == null)
                {
                    _active.RemoveAt(i--);
                    continue;
                }

                if (tween.Delay > 0f)
                {
                    tween.Delay -= dt;
                    continue;
                }

                tween.Elapsed += dt;
                float t = Mathf.Clamp01(tween.Elapsed / tween.Duration);
                tween.Step(Easing.Apply(tween.Ease, t));
                if (t < 1f)
                    continue;

                // Done может запустить новый твин на этом же канале, поэтому убираем старый до вызова.
                int index = _active.IndexOf(tween);
                if (index >= 0)
                    _active.RemoveAt(index);
                if (index >= 0 && index <= i)
                    i--;
                tween.Done?.Invoke();
            }
        }
    }
}
