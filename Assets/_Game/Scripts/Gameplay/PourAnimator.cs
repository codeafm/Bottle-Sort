using System;
using System.Collections;
using System.Collections.Generic;
using BottleSort.Core;
using UnityEngine;

namespace BottleSort.Gameplay
{
    // Переливание по фазам макета: подъём 0.15 → перелёт 0.32 → наклон 66° за 0.22 → струя,
    // по слою за 0.32 → возврат наклона 0.2 → назад 0.34. Правила хода сюда не входят:
    // вызывающий код уже применил ход и передаёт, какими бутылки стали после него.
    public sealed class PourAnimator : MonoBehaviour
    {
        public const float TiltDegrees = 66f;
        const float CssToWorld = 2f / 70f;
        const int SortingBoost = 30;

        PourStream stream;
        int playing;

        public bool IsPlaying => playing > 0;

        public static float DurationFor(int layers) => 0.76f + layers * BottleView.LayerStepSeconds + 0.04f + 0.56f;

        public Coroutine Play(BottleView source, BottleView target, Bottle sourceAfter, Bottle targetAfter, Action onFinished = null)
        {
            return Play(source, target, LayersOf(sourceAfter), sourceAfter.HiddenCount, LayersOf(targetAfter), targetAfter.HiddenCount, onFinished);
        }

        public Coroutine Play(BottleView source, BottleView target, IReadOnlyList<int> sourceAfter, int sourceHiddenAfter,
            IReadOnlyList<int> targetAfter, int targetHiddenAfter, Action onFinished = null)
        {
            return StartCoroutine(Run(source, target, sourceAfter, sourceHiddenAfter, targetAfter, targetHiddenAfter, onFinished));
        }

        IEnumerator Run(BottleView source, BottleView target, IReadOnlyList<int> sourceAfter, int sourceHiddenAfter,
            IReadOnlyList<int> targetAfter, int targetHiddenAfter, Action onFinished)
        {
            playing++;
            int layers = Mathf.Max(1, targetAfter.Count - target.LayerCount);
            int color = targetAfter.Count > 0 ? targetAfter[targetAfter.Count - 1] : 0;

            var body = source.Body;
            int baseOrder = source.SortingOrder;
            source.SetState(BottleVisualState.Normal, true);
            source.BeginExternalMotion();
            source.SortingOrder = baseOrder + SortingBoost;

            float scale = body.lossyScale.x;
            Vector3 startPos = body.position;
            Quaternion startRot = body.rotation;
            Vector3 restPos = source.transform.position;
            Vector3 liftedPos = restPos + Vector3.up * (40f * CssToWorld * scale);

            var pose = PourPose(source, target);

            yield return Move(body, startPos, liftedPos, startRot, Quaternion.identity, 0.15f, Ease.Out);
            yield return Move(body, liftedPos, pose.uprightPosition, Quaternion.identity, Quaternion.identity, 0.32f, Ease.Glide);
            yield return Tilt(body, pose.center, pose.pivotOffset, 0f, pose.angle, 0.22f, Ease.Tilt);

            var pourStream = Stream;
            pourStream.SortingOrder = Mathf.Max(baseOrder, target.SortingOrder) + SortingBoost - 5;
            pourStream.SetEndpoints(source.MouthPosition, target.SurfacePosition);
            pourStream.Begin(LiquidPalette.Get(color), scale);

            float pourTime = 0f;
            bool levelsStarted = false;
            float levelSeconds = layers * BottleView.LayerStepSeconds + 0.04f;
            while (pourTime < 0.07f + levelSeconds)
            {
                pourTime += Time.deltaTime;
                if (!levelsStarted && pourTime >= 0.07f)
                {
                    levelsStarted = true;
                    source.AnimateLevelChange(sourceAfter, sourceHiddenAfter);
                    target.AnimateLevelChange(targetAfter, targetHiddenAfter);
                }
                pourStream.SetEndpoints(source.MouthPosition, target.SurfacePosition);
                yield return null;
            }

            pourStream.End();
            yield return Tilt(body, pose.center, pose.pivotOffset, pose.angle, 0f, 0.2f, Ease.InOut);
            yield return Wait(0.01f);
            yield return Move(body, body.position, restPos, Quaternion.identity, Quaternion.identity, 0.34f, Ease.Glide);

            source.SortingOrder = baseOrder;
            source.EndExternalMotion();
            playing--;
            onFinished?.Invoke();
        }

        // Ставим бутылку так, чтобы после наклона горлышко оказалось чуть выше горлышка цели
        public static (Vector3 uprightPosition, Vector3 center, Vector3 pivotOffset, float angle) PourPose(BottleView source, BottleView target)
        {
            var body = source.Body;
            float scale = body.lossyScale.x;
            float angle = target.transform.position.x >= source.transform.position.x ? -TiltDegrees : TiltDegrees;
            var rotation = Quaternion.Euler(0f, 0f, angle);

            Vector3 mouthLocal = source.Body.InverseTransformPoint(source.MouthPosition);
            Vector3 pivotLocal = source.LocalPivot;
            Vector3 landing = target.MouthPosition + Vector3.up * (6f * CssToWorld * target.transform.lossyScale.y + 0.1f * scale);

            Vector3 bodyPosition = landing - rotation * (mouthLocal * scale);
            Vector3 pivotOffset = pivotLocal * scale;
            Vector3 center = bodyPosition + rotation * pivotOffset;
            Vector3 upright = center - pivotOffset;
            return (upright, center, pivotOffset, angle);
        }

        // Статичный кадр «льёт» для превью и скриншотов, без корутин
        public static void PoseForPreview(BottleView source, BottleView target, PourStream stream, Color color)
        {
            var pose = PourPose(source, target);
            source.BeginExternalMotion();
            var rotation = Quaternion.Euler(0f, 0f, pose.angle);
            source.Body.rotation = rotation;
            source.Body.position = pose.center - rotation * pose.pivotOffset;
            stream.SortingOrder = Mathf.Max(source.SortingOrder, target.SortingOrder) + 5;
            source.SortingOrder += 10;
            stream.Begin(color, source.Body.lossyScale.x);
            stream.SetEndpoints(source.MouthPosition, target.SurfacePosition);
        }

        PourStream Stream
        {
            get
            {
                if (stream == null || stream.IsActive)
                    stream = PourStream.Create(transform);
                return stream;
            }
        }

        static IEnumerator Move(Transform body, Vector3 from, Vector3 to, Quaternion rotFrom, Quaternion rotTo, float seconds, CubicBezier ease)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                float k = ease.Evaluate(Mathf.Clamp01(t / seconds));
                body.SetPositionAndRotation(Vector3.LerpUnclamped(from, to, k), Quaternion.SlerpUnclamped(rotFrom, rotTo, k));
                yield return null;
            }
            body.SetPositionAndRotation(to, rotTo);
        }

        static IEnumerator Tilt(Transform body, Vector3 center, Vector3 pivotOffset, float fromDeg, float toDeg, float seconds, CubicBezier ease)
        {
            float t = 0f;
            while (true)
            {
                t += Time.deltaTime;
                float k = ease.Evaluate(Mathf.Clamp01(t / seconds));
                var rotation = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(fromDeg, toDeg, k));
                body.SetPositionAndRotation(center - rotation * pivotOffset, rotation);
                if (t >= seconds)
                    yield break;
                yield return null;
            }
        }

        static IEnumerator Wait(float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                yield return null;
            }
        }

        static int[] LayersOf(Bottle bottle)
        {
            var layers = new int[bottle.Count];
            for (int i = 0; i < bottle.Count; i++)
                layers[i] = bottle[i];
            return layers;
        }
    }
}
