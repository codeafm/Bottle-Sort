using UnityEngine;

namespace BottleSort.Gameplay
{
    // Котик в фигурке спасения (BOTTLE_MODELS §5.1). Сидит в спасательном круге на поверхности жидкости и машет
    // табличкой HELP!, а когда рецепт собран — выпрыгивает из горлышка с конфетти. Круг остаётся плавать.
    // Живёт внутри тела бутылки, поэтому трясётся и поднимается вместе с ним; позицию и прозрачность задаёт BottleView.
    public sealed class RescueCat : MonoBehaviour
    {
        public const float JumpSeconds = 1.2f;

        const float SignWaveDegrees = 9f;
        const float SignWaveSeconds = 1.7f;
        // Лапа с табличкой: в единицах макета вправо и вверх от низа круга
        static readonly Vector2 SignPaw = new Vector2(18f, 27f);

        static readonly Color32[] ConfettiColors =
        {
            new Color32(0xC9, 0x28, 0x3A, 0xFF), new Color32(0x2D, 0x5F, 0xC4, 0xFF), new Color32(0xE3, 0xB2, 0x1E, 0xFF),
            new Color32(0x2A, 0x9E, 0x4A, 0xFF), new Color32(0x6E, 0x3B, 0xB0, 0xFF), new Color32(0x14, 0x95, 0xAA, 0xFF),
            new Color32(0xD9, 0x77, 0x2A, 0xFF), new Color32(0xC7, 0x32, 0x78, 0xFF),
        };

        VfxArt art;
        Transform ring, kitten, sign;
        SpriteRenderer ringBack, ringFront, cat, paw, help, flash;
        ParticleSystem confetti;
        static Material confettiMaterial;

        float surfaceY;
        float mouthY;
        bool full;
        bool kittenGone;
        float jumpTime = -1f;
        float idleTime;
        float alpha = 1f;
        int sortingOrder;

        public bool IsJumping => jumpTime >= 0f;
        public bool IsSitting => !kittenGone && !IsJumping;
        public float JumpTimeLeft => IsJumping ? Mathf.Max(0f, JumpSeconds - jumpTime) : 0f;

        public static RescueCat Create(Transform parent, VfxArt art, HideFlags hide)
        {
            var go = new GameObject("Rescue Cat") { hideFlags = hide };
            go.transform.SetParent(parent, false);
            var rescue = go.AddComponent<RescueCat>();
            rescue.Build(art, hide);
            return rescue;
        }

        void Build(VfxArt source, HideFlags hide)
        {
            art = source;
            ring = NewNode("Ring", transform, hide);
            ringBack = NewSprite("Ring Back", ring, art.ringBackSprite, hide);
            ringFront = NewSprite("Ring Front", ring, art.ringFrontSprite, hide);
            kitten = NewNode("Kitten", transform, hide);
            cat = NewSprite("Cat", kitten, art.catSprite, hide);
            paw = NewSprite("Paw", kitten, art.catPawSprite, hide);
            sign = NewNode("Sign", kitten, hide);
            sign.localPosition = SignPaw * BottleGeometry.UnitSize;
            help = NewSprite("Help", sign, art.helpSignSprite, hide);
            flash = NewSprite("Flash", transform, art.flashSprite, hide);
            flash.sharedMaterial = art.additiveMaterial;
            flash.enabled = false;
            ApplySorting();
        }

        static Transform NewNode(string name, Transform parent, HideFlags hide)
        {
            var go = new GameObject(name) { hideFlags = hide };
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        SpriteRenderer NewSprite(string name, Transform parent, Sprite sprite, HideFlags hide)
        {
            var go = new GameObject(name) { hideFlags = hide };
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = art.spriteMaterial;
            return renderer;
        }

        public int SortingOrder
        {
            get => sortingOrder;
            set
            {
                sortingOrder = value;
                ApplySorting();
            }
        }

        // Стекло спереди у фигурки рисуется на SortingOrder + 6, всё остальное — под ним
        void ApplySorting()
        {
            if (ringBack == null)
                return;
            ringBack.sortingOrder = sortingOrder + 1;
            cat.sortingOrder = sortingOrder + 2;
            ringFront.sortingOrder = sortingOrder + 3;
            paw.sortingOrder = sortingOrder + 4;
            help.sortingOrder = sortingOrder + 5;
            flash.sortingOrder = sortingOrder + 7;
            if (confetti != null)
                confetti.GetComponent<ParticleSystemRenderer>().sortingOrder = sortingOrder + 8;
        }

        // Поверхность жидкости и верх горлышка — в единицах макета формы
        public void SetLevel(float surfaceUnits, float mouthUnits)
        {
            surfaceY = surfaceUnits;
            mouthY = mouthUnits;
            Pose();
        }

        // Полная фигурка без прыжка — котик уже спасён, в ней плавает только круг.
        // Если фигурка снова неполная (отмена), котик возвращается в круг.
        public void SetFull(bool isFull)
        {
            full = isFull;
            if (!full)
            {
                jumpTime = -1f;
                kittenGone = false;
            }
            else if (!IsJumping)
                kittenGone = true;
            Pose();
        }

        public void SetAlpha(float value)
        {
            alpha = value;
            Pose();
        }

        // Повторный вызов во время прыжка или после него ничего не делает
        public float Jump()
        {
            if (IsJumping || kittenGone)
                return JumpTimeLeft;
            if (!Application.isPlaying)
            {
                kittenGone = true;
                Pose();
                return 0f;
            }
            jumpTime = 0f;
            Pose();
            return JumpSeconds;
        }

        void Update()
        {
            if (!Application.isPlaying)
                return;
            idleTime += Time.deltaTime;
            if (jumpTime >= 0f)
            {
                float before = jumpTime;
                jumpTime += Time.deltaTime;
                if (before < 0.42f && jumpTime >= 0.42f)
                    Burst();
                if (jumpTime >= JumpSeconds)
                {
                    jumpTime = -1f;
                    kittenGone = true;
                }
            }
            Pose();
        }

        void Pose()
        {
            if (ring == null)
                return;
            var surface = new Vector3(0f, BottleGeometry.ToLocalY(surfaceY), 0f);
            ring.localPosition = surface;
            SetColor(ringBack, 1f);
            SetColor(ringFront, 1f);

            bool showKitten = !kittenGone && (!full || IsJumping);
            cat.enabled = paw.enabled = showKitten;
            help.enabled = showKitten;
            flash.enabled = false;
            if (!showKitten)
                return;

            if (!IsJumping)
            {
                cat.sprite = art.catSprite;
                kitten.localPosition = surface;
                kitten.localRotation = Quaternion.identity;
                kitten.localScale = Vector3.one;
                float wave = Application.isPlaying ? Mathf.Sin(idleTime * Mathf.PI * 2f / SignWaveSeconds) : 0f;
                sign.localRotation = Quaternion.Euler(0f, 0f, -12f + wave * SignWaveDegrees);
                sign.localScale = Vector3.one;
                SetColor(cat, 1f);
                SetColor(paw, 1f);
                SetColor(help, 1f);
                return;
            }
            PoseJump(surface);
        }

        // Присел → вылетел из горлышка → покачался наверху → исчез вспышкой. Всего 1.2 с.
        void PoseJump(Vector3 surface)
        {
            float t = jumpTime;
            cat.sprite = art.catHappySprite;
            float apex = BottleGeometry.ToLocalY(mouthY - 50f);

            Vector3 scale = Vector3.one;
            float y = surface.y;
            float spin = 0f;
            float fade = 1f;
            if (t < 0.16f)
            {
                float k = Ease.Out.Evaluate(t / 0.16f);
                scale = new Vector3(1f + 0.1f * k, 1f - 0.16f * k, 1f);
            }
            else if (t < 0.62f)
            {
                float k = (t - 0.16f) / 0.46f;
                y = Mathf.LerpUnclamped(surface.y, apex, Ease.Out.Evaluate(k));
                float stretch = Mathf.Sin(Mathf.Clamp01(k * 1.4f) * Mathf.PI) * 0.14f;
                scale = new Vector3(1f - stretch * 0.6f, 1f + stretch, 1f);
            }
            else if (t < 0.88f)
            {
                float k = (t - 0.62f) / 0.26f;
                y = apex + Mathf.Sin(k * Mathf.PI) * 0.12f;
                spin = Mathf.Sin(k * Mathf.PI * 2f) * 10f;
            }
            else
            {
                float k = Mathf.Clamp01((t - 0.88f) / 0.32f);
                y = apex + k * 0.3f;
                float shrink = 1f - k * k;
                scale = Vector3.one * Mathf.Max(0.001f, shrink);
                fade = 1f - k * k;
                flash.enabled = true;
                flash.transform.localPosition = new Vector3(0f, apex + 0.6f, 0f);
                flash.transform.localScale = Vector3.one * Mathf.Lerp(0.3f, 0.75f, k);
                flash.color = new Color(1f, 0.93f, 0.7f, Mathf.Sin(k * Mathf.PI) * 0.9f * alpha);
            }

            kitten.localPosition = new Vector3(0f, y, 0f);
            kitten.localRotation = Quaternion.Euler(0f, 0f, spin);
            kitten.localScale = scale;
            // Табличку котик бросает сразу, как обрадовался
            sign.localScale = Vector3.one * Mathf.Clamp01(1f - t / 0.14f);
            SetColor(cat, fade);
            SetColor(paw, fade);
            SetColor(help, fade);
        }

        void SetColor(SpriteRenderer renderer, float opacity)
        {
            renderer.color = new Color(1f, 1f, 1f, opacity * alpha);
        }

        void Burst()
        {
            if (confetti == null)
                confetti = CreateConfetti();
            confetti.transform.localPosition = new Vector3(0f, BottleGeometry.ToLocalY(mouthY), 0f);
            // Частицы не наследуют масштаб фигурки, поэтому размеры и скорость считаем от него сами
            float scale = Mathf.Abs(transform.lossyScale.x);
            var main = confetti.main;
            main.startSpeed = new ParticleSystem.MinMaxCurve(3.6f * scale, 6.8f * scale);
            main.startSizeX = new ParticleSystem.MinMaxCurve(0.06f * scale, 0.1f * scale);
            main.startSizeY = new ParticleSystem.MinMaxCurve(0.1f * scale, 0.16f * scale);
            main.gravityModifier = 0.8f * scale;
            confetti.Clear();
            confetti.Play();
        }

        ParticleSystem CreateConfetti()
        {
            var go = new GameObject("Rescue Confetti") { hideFlags = gameObject.hideFlags };
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.3f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 60;
            main.startSize3D = true;
            main.startSizeZ = 1f;
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var gradient = new Gradient { mode = GradientMode.Fixed };
            var keys = new GradientColorKey[ConfettiColors.Length];
            for (int i = 0; i < keys.Length; i++)
                keys[i] = new GradientColorKey(ConfettiColors[i], (i + 1f) / keys.Length);
            gradient.SetKeys(keys, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            main.startColor = new ParticleSystem.MinMaxGradient(gradient) { mode = ParticleSystemGradientMode.RandomColor };

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 40) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 52f;
            shape.radius = 0.1f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.separateAxes = true;
            rotation.x = new ParticleSystem.MinMaxCurve(2f, 6f);
            rotation.z = new ParticleSystem.MinMaxCurve(1.5f, 3f);

            var drag = ps.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.limit = 1000f;
            drag.dampen = 0f;
            drag.multiplyDragByParticleVelocity = false;
            drag.drag = 2.4f;

            var color = ps.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.55f), new GradientAlphaKey(0f, 1f) });
            color.color = fade;

            if (confettiMaterial == null)
            {
                confettiMaterial = new Material(art.spriteMaterial) { name = "Rescue Confetti (instance)", hideFlags = HideFlags.DontSave };
                confettiMaterial.mainTexture = art.confettiSprite.texture;
            }
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = confettiMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.World;
            renderer.sortingOrder = sortingOrder + 8;
            return ps;
        }
    }
}
