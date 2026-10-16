using UnityEngine;

namespace BottleSort.Gameplay
{
    // Конфетти в мире камеры. Burst — два залпа снизу по углам в момент победы,
    // Rain — непрерывный дождь сверху, как на экране победы в макете (34 полоски, падение 4–8 с, 540°).
    // Рисуется камерой, поэтому поверх Canvas в режиме Overlay его не видно: для окна победы нужен
    // Canvas в режиме Screen Space - Camera с sortingOrder ниже конфетти, либо конфетти позади попапа.
    public sealed class ConfettiEffect : MonoBehaviour
    {
        public const int DefaultSortingOrder = 200;

        // У Gradient не больше 8 ключей, поэтому лайм из набора макета не взят — он рядом с зелёным
        static readonly Color32[] Colors =
        {
            new Color32(0xC9, 0x28, 0x3A, 0xFF), new Color32(0x2D, 0x5F, 0xC4, 0xFF), new Color32(0xE3, 0xB2, 0x1E, 0xFF),
            new Color32(0x2A, 0x9E, 0x4A, 0xFF), new Color32(0x6E, 0x3B, 0xB0, 0xFF), new Color32(0x14, 0x95, 0xAA, 0xFF),
            new Color32(0xD9, 0x77, 0x2A, 0xFF), new Color32(0xC7, 0x32, 0x78, 0xFF),
        };

        [SerializeField] Camera targetCamera;
        [SerializeField] int sortingOrder = DefaultSortingOrder;

        ParticleSystem burstLeft, burstRight, rain;
        static Material material;

        public Camera TargetCamera
        {
            get => targetCamera;
            set => targetCamera = value;
        }

        public void Burst()
        {
            Build();
            var cam = Cam;
            float h = cam.orthographicSize, w = h * cam.aspect;
            var c = cam.transform.position;
            Place(burstLeft, new Vector3(c.x - w * 0.95f, c.y - h * 0.75f, 0f), -28f);
            Place(burstRight, new Vector3(c.x + w * 0.95f, c.y - h * 0.75f, 0f), 28f);
            burstLeft.Play();
            burstRight.Play();
        }

        public void StartRain()
        {
            Build();
            var cam = Cam;
            float h = cam.orthographicSize, w = h * cam.aspect;
            var c = cam.transform.position;
            rain.transform.position = new Vector3(c.x, c.y + h + 0.6f, 0f);
            var shape = rain.shape;
            shape.scale = new Vector3(w * 2f, 0.1f, 1f);
            if (!rain.isPlaying)
            {
                rain.Play();
                // Сразу заполнить экран, а не ждать, пока первые долетят сверху
                rain.Simulate(6f, true, false, false);
                rain.Play();
            }
        }

        public void StopRain()
        {
            if (rain != null)
                rain.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        public void Clear()
        {
            foreach (var ps in new[] { burstLeft, burstRight, rain })
                if (ps != null)
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        Camera Cam => targetCamera != null ? targetCamera : Camera.main;

        static void Place(ParticleSystem ps, Vector3 position, float tilt)
        {
            ps.transform.position = position;
            ps.transform.rotation = Quaternion.Euler(0f, 0f, tilt);
        }

        void Build()
        {
            if (rain != null)
                return;
            var art = VfxArt.Default;
            if (material == null)
            {
                material = new Material(art.spriteMaterial) { name = "Confetti (instance)", hideFlags = HideFlags.DontSave };
                material.mainTexture = art.confettiSprite.texture;
            }
            burstLeft = CreateBurst("Confetti Left");
            burstRight = CreateBurst("Confetti Right");
            rain = CreateRain();
        }

        ParticleSystem CreateBurst(string objectName)
        {
            var ps = CreateSystem(objectName, 160);
            var main = ps.main;
            main.loop = false;
            main.duration = 0.25f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 3.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(9f, 15f);
            main.gravityModifier = 1.1f;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 70) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f;
            shape.radius = 0.2f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            var drag = ps.limitVelocityOverLifetime;
            drag.enabled = true;
            // По умолчанию модуль ещё и режет скорость до 1, нам нужно только сопротивление воздуха
            drag.limit = 1000f;
            drag.dampen = 0f;
            drag.multiplyDragByParticleVelocity = false;
            drag.drag = 1.6f;
            drag.multiplyDragByParticleSize = false;

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 1.2f;
            noise.frequency = 0.6f;
            noise.quality = ParticleSystemNoiseQuality.Low;

            var color = ps.colorOverLifetime;
            color.enabled = true;
            color.color = FadeOutAtEnd();
            return ps;
        }

        ParticleSystem CreateRain()
        {
            var ps = CreateSystem("Confetti Rain", 60);
            var main = ps.main;
            main.loop = true;
            main.duration = 8f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 8f);
            main.startSpeed = 0f;
            main.gravityModifier = 0f;

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
            velocity.y = new ParticleSystem.MinMaxCurve(-3.4f, -6.2f);
            velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

            var emission = ps.emission;
            emission.rateOverTime = 34f / 6f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(10f, 0.1f, 1f);
            return ps;
        }

        ParticleSystem CreateSystem(string objectName, int maxParticles)
        {
            var go = new GameObject(objectName);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = maxParticles;
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(0.17f, 0.33f);
            main.startSizeY = new ParticleSystem.MinMaxCurve(0.28f, 0.5f);
            main.startSizeZ = 1f;
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = RandomPaletteColor();

            // Переворачиваются в полёте: вращение по X даёт «мигание» плоской полоски
            var rotation = ps.rotationOverLifetime;
            rotation.enabled = true;
            rotation.separateAxes = true;
            rotation.x = new ParticleSystem.MinMaxCurve(2f, 6f);
            rotation.y = new ParticleSystem.MinMaxCurve(0f, 1.5f);
            rotation.z = new ParticleSystem.MinMaxCurve(1.5f, 2.5f);

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.World;
            renderer.sortingOrder = sortingOrder;
            return ps;
        }

        static ParticleSystem.MinMaxGradient RandomPaletteColor()
        {
            var gradient = new Gradient { mode = GradientMode.Fixed };
            var keys = new GradientColorKey[Colors.Length];
            for (int i = 0; i < Colors.Length; i++)
                keys[i] = new GradientColorKey(Colors[i], (i + 1f) / Colors.Length);
            gradient.SetKeys(keys, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            return new ParticleSystem.MinMaxGradient(gradient) { mode = ParticleSystemGradientMode.RandomColor };
        }

        static ParticleSystem.MinMaxGradient FadeOutAtEnd()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            return gradient;
        }
    }
}
