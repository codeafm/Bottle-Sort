using System.Collections.Generic;
using BottleSort.Core;
using UnityEngine;

namespace BottleSort.Gameplay
{
    public enum BottleVisualState
    {
        Normal,
        Selected,
        Hint,
        Completed
    }

    // Вид одной бутылки. Позиция объекта — точка, где дно стоит на полке; масштаб задаёт раскладка.
    // Всё рисуется одним квадом с шейдером BottleSort/Bottle, замок — отдельным спрайтом поверх.
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class BottleView : MonoBehaviour
    {
        public const int MaxLayers = 10;
        public const float LayerStepSeconds = 0.32f;
        const float LayerEaseSeconds = 0.28f;
        const float SelectedLift = 0.63f;

        static readonly Color SelectedGlow = new Color(53 / 255f, 244 / 255f, 1f, 1f);
        static readonly Color HintGlow = new Color(1f, 200 / 255f, 61 / 255f, 1f);
        static readonly Color CompletedGlow = new Color(1f, 236 / 255f, 150 / 255f, 0.7f);

        static readonly int BackTexId = Shader.PropertyToID("_BackTex");
        static readonly int FrontTexId = Shader.PropertyToID("_FrontTex");
        static readonly int MaskTexId = Shader.PropertyToID("_MaskTex");
        static readonly int GlyphTexId = Shader.PropertyToID("_GlyphTex");
        static readonly int FillId = Shader.PropertyToID("_Fill");
        static readonly int CapacityId = Shader.PropertyToID("_Capacity");
        static readonly int HiddenId = Shader.PropertyToID("_Hidden");
        static readonly int LiquidTopId = Shader.PropertyToID("_LiquidTop");
        static readonly int LiquidBottomId = Shader.PropertyToID("_LiquidBottom");
        static readonly int GlowId = Shader.PropertyToID("_GlowColor");
        static readonly int TintId = Shader.PropertyToID("_Tint");
        static readonly int IceId = Shader.PropertyToID("_Ice");
        static readonly int IceTurnsId = Shader.PropertyToID("_IceTurns");
        static readonly int AlphaId = Shader.PropertyToID("_Alpha");
        static readonly int[] ColorIds =
        {
            Shader.PropertyToID("_Color0"), Shader.PropertyToID("_Color1"), Shader.PropertyToID("_Color2"),
            Shader.PropertyToID("_Color3"), Shader.PropertyToID("_Color4"), Shader.PropertyToID("_Color5"),
            Shader.PropertyToID("_Color6"), Shader.PropertyToID("_Color7"), Shader.PropertyToID("_Color8"),
            Shader.PropertyToID("_Color9")
        };

        static Mesh quad;

        [SerializeField] VfxArt art;
        [SerializeField] string shape = BottleShapeIds.Classic;
        [SerializeField] int capacity = 4;
        [SerializeField] List<int> colors = new List<int>();
        [SerializeField] int hidden;
        [SerializeField] int frozen;
        [SerializeField] bool locked;
        [SerializeField] int lockColor = Palette.AnyColor;
        [SerializeField] int tint = Palette.AnyColor;
        [SerializeField] BottleVisualState state;
        [SerializeField] int sortingOrder;

        Transform body;
        MeshRenderer glassRenderer;
        SpriteRenderer lockRenderer;
        SpriteRenderer gemRenderer;
        Material material;
        BottleShapeArt shapeArt;

        float fill;
        readonly Queue<float> fillSteps = new Queue<float>();
        float stepFrom, stepTo, stepTime = -1f;
        float stepSeconds = LayerStepSeconds;
        List<int> pendingColors;

        float hiddenShown;
        float iceShown;
        float lockShown;
        Color glowShown;
        float lift;
        float liftFrom, liftTime = -1f;
        float shakeTime = -1f;
        float alpha = 1f;
        bool externalMotion;

        public Transform Body
        {
            get
            {
                EnsureBuilt();
                return body;
            }
        }

        public string Shape => shape;
        public int Capacity => capacity;
        public int LayerCount => colors.Count;
        public IReadOnlyList<int> Colors => colors;
        public BottleVisualState State => state;
        public int HiddenCount => hidden;
        public int FrozenTurns => frozen;
        public bool IsLocked => locked;
        public int LockColor => lockColor;
        public bool IsAnimating => stepTime >= 0f || fillSteps.Count > 0 || liftTime >= 0f || shakeTime >= 0f;

        // Контур стекла с ручками и носиками, в локальных координатах при масштабе 1 — для раскладки
        public Rect LocalOutline
        {
            get
            {
                EnsureBuilt();
                return BottleGeometry.ToLocal(shapeArt.outline);
            }
        }

        public Vector2 Size
        {
            get
            {
                var outline = LocalOutline;
                var scale = transform.lossyScale;
                return new Vector2(outline.width * Mathf.Abs(scale.x), outline.height * Mathf.Abs(scale.y));
            }
        }

        public float LayerHeight
        {
            get
            {
                EnsureBuilt();
                return (shapeArt.liquidBottom - shapeArt.liquidTop) / Mathf.Max(1, capacity) * BottleGeometry.UnitSize * Mathf.Abs(transform.lossyScale.y);
            }
        }

        // Откуда течёт струя: верх горлышка, следует за наклоном
        public Vector3 MouthPosition
        {
            get
            {
                EnsureBuilt();
                return body.TransformPoint(BottleGeometry.ToLocal(shapeArt.mouth));
            }
        }

        // Центр текущей поверхности жидкости — куда бьёт струя
        public Vector3 SurfacePosition
        {
            get
            {
                EnsureBuilt();
                float layer = (shapeArt.liquidBottom - shapeArt.liquidTop) / Mathf.Max(1, capacity);
                return body.TransformPoint(new Vector2(0f, BottleGeometry.ToLocalY(shapeArt.liquidBottom - fill * layer)));
            }
        }

        // Точка вращения при наклоне: середина бутылки
        public Vector3 LocalPivot
        {
            get
            {
                EnsureBuilt();
                return BottleGeometry.ToLocal(shapeArt.outline.center);
            }
        }

        public int SortingOrder
        {
            get => sortingOrder;
            set
            {
                sortingOrder = value;
                if (glassRenderer == null)
                    return;
                glassRenderer.sortingOrder = value;
                lockRenderer.sortingOrder = value + 1;
                gemRenderer.sortingOrder = value + 2;
            }
        }

        public float Alpha
        {
            get => alpha;
            set
            {
                alpha = Mathf.Clamp01(value);
                ApplyLook();
            }
        }

        void OnEnable()
        {
            EnsureBuilt();
            SnapAll();
        }

        void OnDisable()
        {
            if (!Application.isPlaying && body != null)
            {
                DestroyImmediate(body.gameObject);
                body = null;
            }
        }

        void OnDestroy()
        {
            if (material != null)
            {
                if (Application.isPlaying) Destroy(material);
                else DestroyImmediate(material);
            }
        }

        void OnValidate()
        {
            capacity = Mathf.Clamp(capacity, 1, MaxLayers);
            if (body != null)
            {
                ApplyShape();
                SnapAll();
            }
        }

        public void SetShape(string shapeId)
        {
            shape = string.IsNullOrEmpty(shapeId) ? BottleShapeIds.Classic : shapeId;
            EnsureBuilt();
            ApplyShape();
            ApplyLook();
        }

        // Цвета — индексы Palette снизу вверх. Без анимации: для начала уровня, отмены, «Заново».
        public void SetLayers(IReadOnlyList<int> layerColors, int layerCapacity, int hiddenCount = 0)
        {
            capacity = Mathf.Clamp(layerCapacity, 1, MaxLayers);
            CopyColors(layerColors, colors);
            hidden = Mathf.Clamp(hiddenCount, 0, colors.Count);
            fillSteps.Clear();
            stepTime = -1f;
            pendingColors = null;
            fill = colors.Count;
            hiddenShown = hidden;
            EnsureBuilt();
            ApplyShape();
            ApplyLook();
        }

        // Плавно доводит уровень до нового набора слоёв, по слою за LayerStepSeconds, как в макете.
        // Возвращает длительность. Скрытые слои, которые открылись, проявляются в конце.
        public float AnimateLevelChange(IReadOnlyList<int> layerColors, int hiddenCount, float secondsPerLayer = LayerStepSeconds)
        {
            EnsureBuilt();
            var next = new List<int>();
            CopyColors(layerColors, next);
            float target = next.Count;
            float from = stepTime >= 0f ? stepTo : fill;
            fillSteps.Clear();

            if (next.Count >= colors.Count)
            {
                colors.Clear();
                colors.AddRange(next);
                pendingColors = null;
            }
            else
                pendingColors = next;

            int steps = Mathf.CeilToInt(Mathf.Abs(target - from) - 0.001f);
            float direction = Mathf.Sign(target - from);
            for (int i = 1; i <= steps; i++)
                fillSteps.Enqueue(i == steps ? target : from + direction * i);

            stepSeconds = Mathf.Max(0.05f, secondsPerLayer);
            hidden = Mathf.Clamp(hiddenCount, 0, next.Count);
            if (hidden > hiddenShown)
                hiddenShown = hidden;

            if (!Application.isPlaying)
            {
                SnapAll();
                return 0f;
            }
            return steps * stepSeconds;
        }

        public void SetHidden(int hiddenCount)
        {
            hidden = Mathf.Clamp(hiddenCount, 0, colors.Count);
            if (hidden > hiddenShown || !Application.isPlaying)
                hiddenShown = hidden;
            ApplyLook();
        }

        // 0 — льда нет. Уменьшение до нуля тает плавно.
        public void SetFrozen(int turns)
        {
            frozen = Mathf.Max(0, turns);
            if (frozen > 0 || !Application.isPlaying)
                iceShown = frozen > 0 ? 1f : 0f;
            ApplyLook();
        }

        public void SetLocked(bool isLocked, int color = Palette.AnyColor)
        {
            locked = isLocked;
            lockColor = color;
            if (locked || !Application.isPlaying)
                lockShown = locked ? 1f : 0f;
            ApplyLook();
        }

        // Тонированная бутылка: стекло цвета, который она принимает. Palette.AnyColor — обычное стекло.
        public void SetTint(int acceptColor)
        {
            tint = acceptColor;
            ApplyLook();
        }

        public void SetState(BottleVisualState visualState, bool instant = false)
        {
            if (state == visualState && !instant)
                return;
            state = visualState;
            liftFrom = lift;
            liftTime = instant || !Application.isPlaying ? -1f : 0f;
            if (liftTime < 0f)
            {
                lift = TargetLift;
                glowShown = TargetGlow;
            }
            ApplyLook();
            ApplyBodyPose();
        }

        public void Show(Bottle bottle)
        {
            shape = bottle.Shape;
            var layerColors = new int[bottle.Count];
            for (int i = 0; i < bottle.Count; i++)
                layerColors[i] = bottle[i];
            SetLayers(layerColors, bottle.Capacity, bottle.HiddenCount);
            SetFrozen(bottle.FrozenTurns);
            SetLocked(bottle.IsLocked, bottle.LockColor);
            SetTint(bottle.AcceptColor);
        }

        // Тряска при ошибочном ходе, 0.42 с
        public void Shake()
        {
            shakeTime = Application.isPlaying ? 0f : -1f;
        }

        public void PlayCompleted()
        {
            SetState(BottleVisualState.Completed);
            if (Application.isPlaying && colors.Count > 0)
                BottleBurst.Play(this, LiquidPalette.Get(colors[colors.Count - 1]));
        }

        // Пока идёт переливание, телом бутылки управляет PourAnimator
        public void BeginExternalMotion()
        {
            externalMotion = true;
            shakeTime = -1f;
        }

        public void EndExternalMotion()
        {
            externalMotion = false;
            ApplyBodyPose();
        }

        void Update()
        {
            if (body == null)
                return;
            if (!Application.isPlaying)
            {
                SnapAll();
                return;
            }

            float dt = Time.deltaTime;
            UpdateLevel(dt);

            if (hiddenShown > hidden && stepTime < 0f && fillSteps.Count == 0)
                hiddenShown = Mathf.MoveTowards(hiddenShown, hidden, dt / 0.3f);

            iceShown = Mathf.MoveTowards(iceShown, frozen > 0 ? 1f : 0f, dt / 0.35f);
            lockShown = Mathf.MoveTowards(lockShown, locked ? 1f : 0f, dt / 0.3f);
            glowShown = Color.Lerp(glowShown, TargetGlow, 1f - Mathf.Exp(-dt * 18f));

            if (liftTime >= 0f)
            {
                liftTime += dt;
                float t = Mathf.Clamp01(liftTime / 0.22f);
                lift = Mathf.LerpUnclamped(liftFrom, TargetLift, Ease.Overshoot.Evaluate(t));
                if (t >= 1f)
                    liftTime = -1f;
            }

            if (shakeTime >= 0f)
            {
                shakeTime += dt;
                if (shakeTime >= 0.42f)
                    shakeTime = -1f;
            }

            ApplyLook();
            ApplyBodyPose();
        }

        void UpdateLevel(float dt)
        {
            if (stepTime < 0f && fillSteps.Count > 0)
            {
                stepFrom = fill;
                stepTo = fillSteps.Dequeue();
                stepTime = 0f;
            }
            if (stepTime < 0f)
                return;

            stepTime += dt;
            float ease = Mathf.Min(LayerEaseSeconds, stepSeconds);
            fill = Mathf.Lerp(stepFrom, stepTo, Ease.Standard.Evaluate(Mathf.Clamp01(stepTime / ease)));
            if (stepTime >= stepSeconds)
            {
                fill = stepTo;
                stepTime = -1f;
                if (fillSteps.Count == 0 && pendingColors != null)
                {
                    colors.Clear();
                    colors.AddRange(pendingColors);
                    pendingColors = null;
                }
            }
        }

        float TargetLift => state == BottleVisualState.Selected ? SelectedLift : 0f;

        Color TargetGlow
        {
            get
            {
                switch (state)
                {
                    case BottleVisualState.Selected: return SelectedGlow;
                    case BottleVisualState.Hint: return HintGlow;
                    case BottleVisualState.Completed: return CompletedGlow;
                    default: return new Color(SelectedGlow.r, SelectedGlow.g, SelectedGlow.b, 0f);
                }
            }
        }

        void SnapAll()
        {
            if (pendingColors != null)
            {
                colors.Clear();
                colors.AddRange(pendingColors);
                pendingColors = null;
            }
            fillSteps.Clear();
            stepTime = -1f;
            capacity = Mathf.Clamp(capacity, 1, MaxLayers);
            fill = Mathf.Min(colors.Count, capacity);
            hidden = Mathf.Clamp(hidden, 0, colors.Count);
            hiddenShown = hidden;
            iceShown = frozen > 0 ? 1f : 0f;
            lockShown = locked ? 1f : 0f;
            glowShown = TargetGlow;
            lift = TargetLift;
            liftTime = -1f;
            shakeTime = -1f;
            if (body != null)
            {
                ApplyShape();
                ApplyLook();
                ApplyBodyPose();
            }
        }

        void ApplyBodyPose()
        {
            if (body == null || externalMotion)
                return;
            float shake = 0f;
            if (shakeTime >= 0f)
                shake = ShakeOffset(shakeTime / 0.42f);
            body.localPosition = new Vector3(shake, lift, 0f);
            body.localRotation = Quaternion.identity;
        }

        // Ключевые кадры shake из макета: 0, -7, 7, -5, 4, 0 px при ширине бутылки 70
        static float ShakeOffset(float t)
        {
            float[] keys = { 0f, -0.2f, 0.2f, -0.143f, 0.114f, 0f };
            float scaled = Mathf.Clamp01(t) * (keys.Length - 1);
            int i = Mathf.Min(Mathf.FloorToInt(scaled), keys.Length - 2);
            float local = scaled - i;
            return Mathf.Lerp(keys[i], keys[i + 1], local * local * (3f - 2f * local));
        }

        void EnsureBuilt()
        {
            if (body != null)
                return;
            if (art == null)
                art = VfxArt.Default;
            if (art == null)
            {
                Debug.LogError("[BottleView] VfxArt not found, run BottleSort/VFX/Generate Art", this);
                enabled = false;
                return;
            }

            var hide = Application.isPlaying ? HideFlags.None : HideFlags.DontSave;
            var bodyObject = new GameObject("Body") { hideFlags = hide };
            body = bodyObject.transform;
            body.SetParent(transform, false);

            var glassObject = new GameObject("Glass") { hideFlags = hide };
            glassObject.transform.SetParent(body, false);
            glassObject.AddComponent<MeshFilter>().sharedMesh = Quad;
            glassRenderer = glassObject.AddComponent<MeshRenderer>();
            glassRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            glassRenderer.receiveShadows = false;

            if (material == null)
                material = new Material(art.bottleShader) { name = "Bottle (instance)", hideFlags = HideFlags.DontSave };
            material.SetTexture(GlyphTexId, art.glyphs);
            glassRenderer.sharedMaterial = material;

            lockRenderer = CreateSprite("Lock", art.lockSprite, hide);
            gemRenderer = CreateSprite("Gem", art.gemSprite, hide);
            gemRenderer.transform.SetParent(lockRenderer.transform, false);
            gemRenderer.transform.localPosition = BottleGeometry.ToLocal(new Vector2(50f, 155f)) - BottleGeometry.ToLocal(new Vector2(50f, 150f));

            SortingOrder = sortingOrder;
            ApplyShape();
        }

        SpriteRenderer CreateSprite(string objectName, Sprite sprite, HideFlags hide)
        {
            var go = new GameObject(objectName) { hideFlags = hide };
            go.transform.SetParent(body, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = art.spriteMaterial;
            renderer.enabled = false;
            return renderer;
        }

        void ApplyShape()
        {
            if (art == null || material == null)
                return;
            shapeArt = art.GetShape(shape);
            material.SetTexture(BackTexId, shapeArt.back);
            material.SetTexture(FrontTexId, shapeArt.front);
            material.SetTexture(MaskTexId, shapeArt.mask);
            material.SetFloat(LiquidTopId, shapeArt.liquidTop);
            material.SetFloat(LiquidBottomId, shapeArt.liquidBottom);

            // Замок и лёд — по центру жидкости, а не на фиксированной высоте (BOTTLE_MODELS §1.5)
            float centerY = (shapeArt.liquidTop + shapeArt.liquidBottom) * 0.5f;
            lockRenderer.transform.localPosition = new Vector3(0f, BottleGeometry.ToLocalY(centerY), 0f);
        }

        void ApplyLook()
        {
            if (material == null)
                return;

            for (int i = 0; i < MaxLayers; i++)
            {
                int color = i < colors.Count ? colors[i] : (colors.Count > 0 ? colors[colors.Count - 1] : 0);
                material.SetColor(ColorIds[i], LiquidPalette.Get(color));
            }
            material.SetFloat(FillId, fill);
            material.SetFloat(CapacityId, capacity);
            material.SetFloat(HiddenId, hiddenShown);
            material.SetColor(GlowId, glowShown);
            material.SetFloat(IceId, iceShown);
            material.SetFloat(IceTurnsId, Mathf.Max(frozen, 1));
            material.SetFloat(AlphaId, alpha);

            if (tint >= 0)
            {
                // Светлее самого цвета, чтобы тонированное стекло не путали с жидкостью
                var c = Color.Lerp(LiquidPalette.Get(tint), Color.white, 0.35f);
                c.a = 0.2f;
                material.SetColor(TintId, c);
            }
            else
                material.SetColor(TintId, Color.clear);

            lockRenderer.enabled = lockShown > 0.001f;
            lockRenderer.color = new Color(1f, 1f, 1f, lockShown * alpha);
            lockRenderer.transform.localScale = Vector3.one * Mathf.Lerp(1.25f, 1f, lockShown);
            gemRenderer.enabled = lockRenderer.enabled && lockColor >= 0;
            if (lockColor >= 0)
            {
                var gem = LiquidPalette.Get(lockColor);
                gem.a = lockShown * alpha;
                gemRenderer.color = gem;
            }
        }

        static void CopyColors(IReadOnlyList<int> source, List<int> target)
        {
            target.Clear();
            if (source == null)
                return;
            for (int i = 0; i < source.Count && i < MaxLayers; i++)
                target.Add(source[i]);
        }

        static Mesh Quad
        {
            get
            {
                if (quad != null)
                    return quad;
                var area = BottleGeometry.TextureArea;
                var min = BottleGeometry.ToLocal(new Vector2(area.xMin, area.yMax));
                var max = BottleGeometry.ToLocal(new Vector2(area.xMax, area.yMin));
                quad = new Mesh { name = "Bottle quad", hideFlags = HideFlags.DontSave };
                quad.vertices = new[]
                {
                    new Vector3(min.x, min.y), new Vector3(max.x, min.y), new Vector3(min.x, max.y), new Vector3(max.x, max.y)
                };
                quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
                quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
                quad.RecalculateBounds();
                return quad;
            }
        }
    }
}
