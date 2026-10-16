using System.Collections.Generic;
using UnityEngine;

namespace BottleSort.Gameplay
{
    // Собранная бутылка: тёплая вспышка за бутылкой и короткий фонтан искр из горлышка.
    // Экземпляры переиспользуются, одновременно их нужно не больше пары.
    public sealed class BottleBurst : MonoBehaviour
    {
        const float Seconds = 0.9f;
        static readonly List<BottleBurst> Pool = new List<BottleBurst>();

        SpriteRenderer flash;
        ParticleSystem sparks;
        float time = -1f;
        float scale = 1f;

        public static BottleBurst Play(BottleView bottle, Color liquid)
        {
            var burst = Take();
            float scale = Mathf.Abs(bottle.transform.lossyScale.x);
            var center = bottle.Body.TransformPoint(bottle.LocalPivot);
            burst.Fire(center, bottle.MouthPosition, liquid, scale, bottle.SortingOrder);
            return burst;
        }

        static BottleBurst Take()
        {
            Pool.RemoveAll(b => b == null);
            foreach (var b in Pool)
                if (b.time < 0f)
                    return b;
            var go = new GameObject("Bottle Burst");
            var burst = go.AddComponent<BottleBurst>();
            burst.Build();
            Pool.Add(burst);
            return burst;
        }

        void Fire(Vector3 center, Vector3 mouth, Color liquid, float bottleScale, int sortingOrder)
        {
            scale = bottleScale;
            time = 0f;
            gameObject.SetActive(true);

            flash.transform.position = center + Vector3.forward * 0.01f;
            flash.sortingOrder = sortingOrder - 1;

            sparks.transform.position = mouth;
            var main = sparks.main;
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white, Color.Lerp(liquid, Color.white, 0.45f));
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f * scale, 0.14f * scale);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.2f * scale, 4.2f * scale);
            main.gravityModifier = 0.55f * scale;
            sparks.GetComponent<ParticleSystemRenderer>().sortingOrder = sortingOrder + 5;
            sparks.Clear();
            sparks.Play();
        }

        void Update()
        {
            if (time < 0f)
                return;
            time += Time.deltaTime;
            float t = Mathf.Clamp01(time / 0.55f);
            flash.transform.localScale = Vector3.one * Mathf.Lerp(0.7f, 1.6f, 1f - (1f - t) * (1f - t)) * scale;
            flash.color = new Color(1f, 0.92f, 0.62f, Mathf.Sin(t * Mathf.PI) * 0.85f);
            if (time >= Seconds)
            {
                time = -1f;
                gameObject.SetActive(false);
            }
        }

        void Build()
        {
            var art = VfxArt.Default;

            var flashObject = new GameObject("Flash");
            flashObject.transform.SetParent(transform, false);
            flash = flashObject.AddComponent<SpriteRenderer>();
            flash.sprite = art.flashSprite;
            flash.sharedMaterial = art.additiveMaterial;

            var sparkObject = new GameObject("Sparks");
            sparkObject.transform.SetParent(transform, false);
            sparks = sparkObject.AddComponent<ParticleSystem>();
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = sparks.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.3f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 40;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            var emission = sparks.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });

            var shape = sparks.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 32f;
            shape.radius = 0.08f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            var size = sparks.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

            var drag = sparks.limitVelocityOverLifetime;
            drag.enabled = true;
            // По умолчанию модуль ещё и режет скорость до 1, нам нужно только сопротивление воздуха
            drag.limit = 1000f;
            drag.dampen = 0f;
            drag.multiplyDragByParticleVelocity = false;
            drag.drag = 2.5f;

            var renderer = sparks.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = SparkMaterial(art);
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            gameObject.SetActive(false);
        }

        static Material sparkMaterial;

        static Material SparkMaterial(VfxArt art)
        {
            if (sparkMaterial == null)
            {
                sparkMaterial = new Material(art.additiveMaterial) { name = "Sparks (instance)", hideFlags = HideFlags.DontSave };
                sparkMaterial.mainTexture = art.sparkSprite.texture;
            }
            return sparkMaterial;
        }
    }
}
