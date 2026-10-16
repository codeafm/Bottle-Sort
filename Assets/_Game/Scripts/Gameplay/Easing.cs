using UnityEngine;

namespace BottleSort.Gameplay
{
    // Кривые из макета записаны как CSS cubic-bezier, здесь они же
    public readonly struct CubicBezier
    {
        readonly float x1, y1, x2, y2;

        public CubicBezier(float x1, float y1, float x2, float y2)
        {
            this.x1 = x1;
            this.y1 = y1;
            this.x2 = x2;
            this.y2 = y2;
        }

        public float Evaluate(float x)
        {
            if (x <= 0f) return 0f;
            if (x >= 1f) return 1f;

            float t = x;
            for (int i = 0; i < 8; i++)
            {
                float error = Sample(x1, x2, t) - x;
                float slope = SampleDerivative(x1, x2, t);
                if (Mathf.Abs(error) < 1e-5f)
                    break;
                if (Mathf.Abs(slope) < 1e-6f)
                    break;
                t = Mathf.Clamp01(t - error / slope);
            }
            return Sample(y1, y2, t);
        }

        static float Sample(float a, float b, float t) =>
            ((1f - 3f * b + 3f * a) * t + (3f * b - 6f * a)) * t * t + 3f * a * t;

        static float SampleDerivative(float a, float b, float t) =>
            3f * (1f - 3f * b + 3f * a) * t * t + 2f * (3f * b - 6f * a) * t + 3f * a;
    }

    public static class Ease
    {
        public static readonly CubicBezier Standard = new CubicBezier(0.25f, 0.1f, 0.25f, 1f);
        public static readonly CubicBezier Out = new CubicBezier(0f, 0f, 0.58f, 1f);
        public static readonly CubicBezier InOut = new CubicBezier(0.42f, 0f, 0.58f, 1f);
        public static readonly CubicBezier Overshoot = new CubicBezier(0.34f, 1.56f, 0.64f, 1f);
        public static readonly CubicBezier Glide = new CubicBezier(0.25f, 0.9f, 0.35f, 1f);
        public static readonly CubicBezier Tilt = new CubicBezier(0.3f, 0.7f, 0.4f, 1f);
    }
}
