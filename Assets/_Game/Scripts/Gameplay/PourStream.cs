using UnityEngine;

namespace BottleSort.Gameplay
{
    // Струя от горлышка до поверхности плюс круги и капли там, где она входит в воду.
    // Размеры из макета (ширина 9 px, круги 26x9, капли 3–4 px) при бутылке шириной 70 px.
    public sealed class PourStream : MonoBehaviour
    {
        const float CssToWorld = 2f / 70f;
        const float StreamWidth = 9f * CssToWorld;
        const float StreamShareOfQuad = 0.34f;
        const float GrowSeconds = 0.14f;
        const float CutSeconds = 0.12f;

        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int LengthId = Shader.PropertyToID("_Length");
        static readonly int GrowId = Shader.PropertyToID("_Grow");
        static readonly int CutId = Shader.PropertyToID("_Cut");
        static readonly int WidthId = Shader.PropertyToID("_Width");
        static readonly int PeriodId = Shader.PropertyToID("_FlowPeriod");
        static readonly int SpeedId = Shader.PropertyToID("_FlowSpeed");

        // Капли из макета: смещение в px за цикл, длительность, сдвиг фазы, размер
        static readonly (Vector2 hop, float seconds, float delay, float size, bool white)[] Drops =
        {
            (new Vector2(-11f, 13f), 0.42f, 0f, 4f, false),
            (new Vector2(10f, 11f), 0.38f, 0.15f, 3f, false),
            (new Vector2(2f, 17f), 0.46f, 0.3f, 3f, true),
        };

        VfxArt art;
        Transform streamQuad;
        MeshRenderer streamRenderer;
        Material material;
        SpriteRenderer[] ripples;
        SpriteRenderer[] drops;
        Vector3 top, bottom;
        Color color, splashColor;
        float scale = 1f;
        float time;
        float grow, cut;
        bool active, ending;
        int sortingOrder;

        public bool IsActive => active;

        public static PourStream Create(Transform parent = null)
        {
            var go = new GameObject("Pour Stream");
            if (parent != null)
                go.transform.SetParent(parent, false);
            return go.AddComponent<PourStream>();
        }

        public int SortingOrder
        {
            get => sortingOrder;
            set
            {
                sortingOrder = value;
                if (streamRenderer == null)
                    return;
                streamRenderer.sortingOrder = value;
                foreach (var r in ripples) r.sortingOrder = value + 1;
                foreach (var d in drops) d.sortingOrder = value + 2;
            }
        }

        void Awake() => Build();

        void OnDestroy()
        {
            if (material == null)
                return;
            if (Application.isPlaying) Destroy(material);
            else DestroyImmediate(material);
        }

        // scale — масштаб бутылки, из которой льём: струя и брызги масштабируются вместе с раскладкой
        public void Begin(Color liquid, float bottleScale = 1f)
        {
            Build();
            color = liquid;
            splashColor = Color.Lerp(liquid, Color.white, 0.28f);
            scale = Mathf.Max(0.1f, bottleScale);
            time = 0f;
            grow = Application.isPlaying ? 0f : 1f;
            cut = 0f;
            active = true;
            ending = false;
            gameObject.SetActive(true);
            material.SetColor(ColorId, color);
            Apply();
        }

        public void SetEndpoints(Vector3 mouth, Vector3 surface)
        {
            top = mouth;
            bottom = new Vector3(mouth.x, Mathf.Min(surface.y, mouth.y - 0.05f), mouth.z);
            if (active)
                Apply();
        }

        public void End()
        {
            if (!active)
                return;
            ending = true;
        }

        // Для статичного кадра (скриншоты, превью): положение анимации кругов и капель
        public void SetTime(float seconds)
        {
            time = seconds;
            Apply();
        }

        void Update()
        {
            if (!active)
                return;
            float dt = Time.deltaTime;
            time += dt;
            grow = Mathf.Min(1f, grow + dt / GrowSeconds);
            if (ending)
            {
                cut = Mathf.Min(1f, cut + dt / CutSeconds);
                if (cut >= 1f)
                {
                    active = false;
                    gameObject.SetActive(false);
                    return;
                }
            }
            Apply();
        }

        void Apply()
        {
            float length = Mathf.Max(0.05f, top.y - bottom.y);
            float quadWidth = StreamWidth * scale / StreamShareOfQuad;
            streamQuad.position = top;
            streamQuad.rotation = Quaternion.identity;
            streamQuad.localScale = new Vector3(quadWidth, length, 1f);
            material.SetFloat(LengthId, length);
            material.SetFloat(GrowId, grow);
            material.SetFloat(CutId, cut);
            material.SetFloat(WidthId, StreamShareOfQuad);
            material.SetFloat(PeriodId, 24f * CssToWorld * scale);
            material.SetFloat(SpeedId, 24f * CssToWorld * scale / 0.28f);

            // Брызги появляются, только когда струя долетела до воды
            bool splashing = grow >= 1f && cut <= 0f;
            for (int i = 0; i < ripples.Length; i++)
            {
                float t = Mathf.Repeat(time / 0.5f + i * 0.5f, 1f);
                var r = ripples[i];
                r.enabled = splashing;
                r.transform.position = bottom + new Vector3(0f, 0f, -0.01f);
                r.transform.localScale = Vector3.one * Mathf.Lerp(0.35f, 1.5f, EaseOut(t)) * scale;
                r.color = new Color(splashColor.r, splashColor.g, splashColor.b, Mathf.Lerp(0.95f, 0f, EaseOut(t)));
            }

            for (int i = 0; i < drops.Length; i++)
            {
                var (hop, seconds, delay, _, white) = Drops[i];
                float t = Mathf.Repeat((time + delay) / seconds, 1f);
                float e = EaseOut(t);
                var d = drops[i];
                d.enabled = splashing;
                d.transform.position = bottom + new Vector3(hop.x * e, hop.y * e, 0f) * CssToWorld * scale + new Vector3(0f, 0.04f * scale, -0.02f);
                d.transform.localScale = Vector3.one * (Drops[i].size / 4f) * scale;
                float a = t < 0.2f ? t / 0.2f : 1f - (t - 0.2f) / 0.8f;
                var c = white ? Color.white : splashColor;
                d.color = new Color(c.r, c.g, c.b, a);
            }
        }

        static float EaseOut(float t) => 1f - (1f - t) * (1f - t);

        void Build()
        {
            if (streamRenderer != null)
                return;
            art = VfxArt.Default;

            var quadObject = new GameObject("Stream");
            streamQuad = quadObject.transform;
            streamQuad.SetParent(transform, false);
            quadObject.AddComponent<MeshFilter>().sharedMesh = StreamMesh;
            streamRenderer = quadObject.AddComponent<MeshRenderer>();
            streamRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            material = new Material(art.streamShader) { name = "Pour Stream (instance)", hideFlags = HideFlags.DontSave };
            streamRenderer.sharedMaterial = material;

            ripples = new SpriteRenderer[2];
            for (int i = 0; i < ripples.Length; i++)
                ripples[i] = CreateSprite("Ripple", art.rippleSprite);
            drops = new SpriteRenderer[Drops.Length];
            for (int i = 0; i < drops.Length; i++)
                drops[i] = CreateSprite("Drop", art.dropSprite);

            SortingOrder = sortingOrder;
            gameObject.SetActive(active);
        }

        SpriteRenderer CreateSprite(string objectName, Sprite sprite)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sharedMaterial = art.spriteMaterial;
            return r;
        }

        static Mesh streamMesh;

        // Квад от 0 вниз до -1: верх в позиции горлышка, длину задаёт масштаб по Y
        static Mesh StreamMesh
        {
            get
            {
                if (streamMesh != null)
                    return streamMesh;
                streamMesh = new Mesh { name = "Stream quad", hideFlags = HideFlags.DontSave };
                streamMesh.vertices = new[] { new Vector3(-0.5f, -1f), new Vector3(0.5f, -1f), new Vector3(-0.5f, 0f), new Vector3(0.5f, 0f) };
                streamMesh.uv = new[] { new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 0), new Vector2(1, 0) };
                streamMesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
                streamMesh.RecalculateBounds();
                return streamMesh;
            }
        }
    }
}
