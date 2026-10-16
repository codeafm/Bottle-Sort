using System;
using UnityEngine;

namespace BottleSort.Gameplay
{
    public enum BackgroundTheme
    {
        Navy,
        Violet,
        Teal,
        Rose,
        Emerald,
        Amber
    }

    // Фон-шейдер на весь кадр камеры. Ставится один раз в сцену, тему можно менять с плавным переходом.
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class BackgroundView : MonoBehaviour
    {
        public const int SortingOrderBehindAll = -1000;

        readonly struct ThemeColors
        {
            public readonly Color Center, Middle, Edge, Halo, SparkGlow;

            public ThemeColors(string center, string middle, string edge, string halo, Color sparkGlow)
            {
                Center = Hex(center);
                Middle = Hex(middle);
                Edge = Hex(edge);
                Halo = Hex(halo);
                SparkGlow = sparkGlow;
            }

            public static ThemeColors Lerp(ThemeColors a, ThemeColors b, float t) => new ThemeColors(
                Color.Lerp(a.Center, b.Center, t), Color.Lerp(a.Middle, b.Middle, t), Color.Lerp(a.Edge, b.Edge, t),
                Color.Lerp(a.Halo, b.Halo, t), Color.Lerp(a.SparkGlow, b.SparkGlow, t));

            ThemeColors(Color center, Color middle, Color edge, Color halo, Color sparkGlow)
            {
                Center = center;
                Middle = middle;
                Edge = edge;
                Halo = halo;
                SparkGlow = sparkGlow;
            }
        }

        static readonly ThemeColors[] Themes =
        {
            new ThemeColors("#12306E", "#071338", "#040A22", "#3A7BFF", new Color(150 / 255f, 190 / 255f, 1f, 0.8f)),
            new ThemeColors("#3A1E78", "#1A0F45", "#0B0624", "#9A5CFF", new Color(210 / 255f, 170 / 255f, 1f, 0.8f)),
            new ThemeColors("#0E5266", "#062C3A", "#021219", "#1BA9C0", new Color(150 / 255f, 240 / 255f, 1f, 0.7f)),
            new ThemeColors("#6E1240", "#3A0A26", "#1A0412", "#FF3D8D", new Color(1f, 170 / 255f, 210 / 255f, 0.8f)),
            new ThemeColors("#0F5A3E", "#06301F", "#02150D", "#2DFFB0", new Color(170 / 255f, 1f, 210 / 255f, 0.8f)),
            new ThemeColors("#6B4310", "#2E1C06", "#120A02", "#FFB547", new Color(1f, 220 / 255f, 150 / 255f, 0.85f)),
        };

        static readonly int CenterId = Shader.PropertyToID("_Center");
        static readonly int MiddleId = Shader.PropertyToID("_Middle");
        static readonly int EdgeId = Shader.PropertyToID("_Edge");
        static readonly int HaloId = Shader.PropertyToID("_Halo");
        static readonly int SparkGlowId = Shader.PropertyToID("_SparkGlow");
        static readonly int AspectId = Shader.PropertyToID("_Aspect");

        [SerializeField] BackgroundTheme theme;
        [SerializeField] Camera targetCamera;
        [SerializeField] float crossfadeSeconds = 0.6f;

        MeshRenderer meshRenderer;
        Material material;
        ThemeColors from, shown;
        float fade = 1f;

        public BackgroundTheme Theme
        {
            get => theme;
            set => SetTheme(value);
        }

        public Camera TargetCamera
        {
            get => targetCamera;
            set => targetCamera = value;
        }

        public static bool TryParse(string name, out BackgroundTheme result) =>
            Enum.TryParse(name, true, out result);

        public void SetTheme(BackgroundTheme value, bool instant = false)
        {
            from = shown;
            theme = value;
            fade = instant || !Application.isPlaying ? 1f : 0f;
            Apply();
        }

        public void NextTheme() => SetTheme((BackgroundTheme)(((int)theme + 1) % Themes.Length));

        void OnEnable()
        {
            Build();
            shown = Themes[(int)theme];
            from = shown;
            fade = 1f;
            Apply();
            FitToCamera();
        }

        void OnValidate()
        {
            if (material != null)
                SetTheme(theme, true);
        }

        void OnDestroy()
        {
            if (material == null)
                return;
            if (Application.isPlaying) Destroy(material);
            else DestroyImmediate(material);
        }

        void LateUpdate()
        {
            if (fade < 1f)
            {
                fade = Mathf.Min(1f, fade + Time.deltaTime / Mathf.Max(0.01f, crossfadeSeconds));
                Apply();
            }
            FitToCamera();
        }

        void Build()
        {
            if (meshRenderer != null)
                return;
            var art = VfxArt.Default;
            var filter = GetComponent<MeshFilter>();
            if (filter == null)
                filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = UnitQuad;
            meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null)
                meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.sortingOrder = SortingOrderBehindAll;
            material = new Material(art.backgroundShader) { name = "Background (instance)", hideFlags = HideFlags.DontSave };
            meshRenderer.sharedMaterial = material;
        }

        void Apply()
        {
            if (material == null)
                return;
            shown = ThemeColors.Lerp(from, Themes[(int)theme], Mathf.SmoothStep(0f, 1f, fade));
            material.SetColor(CenterId, shown.Center);
            material.SetColor(MiddleId, shown.Middle);
            material.SetColor(EdgeId, shown.Edge);
            material.SetColor(HaloId, shown.Halo);
            material.SetColor(SparkGlowId, shown.SparkGlow);
        }

        // Квад всегда ровно по кадру ортографической камеры
        public void FitToCamera()
        {
            var cam = targetCamera != null ? targetCamera : Camera.main;
            if (cam == null || material == null)
                return;
            float height = cam.orthographic ? cam.orthographicSize * 2f : 20f;
            float width = height * cam.aspect;
            var t = transform;
            var camPos = cam.transform.position;
            t.position = new Vector3(camPos.x, camPos.y, camPos.z + cam.farClipPlane * 0.9f);
            t.rotation = cam.transform.rotation;
            var parentScale = t.parent != null ? t.parent.lossyScale : Vector3.one;
            t.localScale = new Vector3(width / parentScale.x, height / parentScale.y, 1f);
            material.SetFloat(AspectId, cam.aspect);
        }

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        static Mesh unitQuad;

        static Mesh UnitQuad
        {
            get
            {
                if (unitQuad != null)
                    return unitQuad;
                unitQuad = new Mesh { name = "Background quad", hideFlags = HideFlags.DontSave };
                unitQuad.vertices = new[] { new Vector3(-0.5f, -0.5f), new Vector3(0.5f, -0.5f), new Vector3(-0.5f, 0.5f), new Vector3(0.5f, 0.5f) };
                unitQuad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
                unitQuad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
                unitQuad.RecalculateBounds();
                return unitQuad;
            }
        }
    }
}
