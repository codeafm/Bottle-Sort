using System.Collections.Generic;
using System.IO;
using BottleSort.Core;
using BottleSort.Gameplay;
using BottleSort.Gameplay.Sandbox;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace BottleSort.Editor.Vfx
{
    // Собирает Sandbox_VFX.unity и снимает с неё кадры в Docs/Screens.
    // Всё сразу из batchmode: -executeMethod BottleSort.Editor.Vfx.VfxSandboxBuilder.RunAll
    public static class VfxSandboxBuilder
    {
        public const string ScenePath = "Assets/_Game/Scenes/Sandbox_VFX.unity";
        const string ScreensDir = "Docs/Screens";
        const float BottleScale = 0.78f;
        const float Spacing = 2.05f;

        sealed class Spec
        {
            public string Shape = BottleShapeIds.Classic;
            public string Colors = "";
            public int Capacity = 4;
            public int Hidden;
            public int Frozen;
            public bool Locked;
            public int LockColor = Palette.AnyColor;
            public int Tint = Palette.AnyColor;
            public BottleVisualState State;
        }

        // Ряды песочницы: состояния, особые формы, колба и скрытые слои, лёд и замки
        static readonly Spec[][] Rows =
        {
            new[]
            {
                new Spec { Colors = "RBGY" },
                new Spec { Colors = "YRPB", State = BottleVisualState.Selected },
                new Spec { Colors = "CC", State = BottleVisualState.Hint },
                new Spec { Colors = "GGGG", State = BottleVisualState.Completed },
                new Spec { Colors = "" },
            },
            new[]
            {
                new Spec { Shape = BottleShapeIds.Wine, Colors = "OKO" },
                new Spec { Shape = BottleShapeIds.Jug, Colors = "LNLN" },
                new Spec { Shape = BottleShapeIds.Teapot, Colors = "WDC" },
                new Spec { Shape = BottleShapeIds.Glass, Colors = "PPB" },
                new Spec { Shape = BottleShapeIds.Flask, Colors = "KYK" },
            },
            new[]
            {
                new Spec { Shape = BottleShapeIds.Potion, Colors = "BRBG" },
                new Spec { Shape = BottleShapeIds.Jar, Colors = "YOC" },
                new Spec { Shape = BottleShapeIds.Cylinder, Colors = "RBYGPCOK", Capacity = 8 },
                new Spec { Colors = "NDRR", Hidden = 2 },
                new Spec { Colors = "", Tint = Palette.Cyan },
            },
            new[]
            {
                new Spec { Colors = "BGRY", Frozen = 3 },
                new Spec { Colors = "RYBG", Locked = true },
                new Spec { Colors = "PK", Locked = true, LockColor = Palette.Yellow },
                new Spec { Shape = BottleShapeIds.Jug, Colors = "OCOW", Frozen = 12 },
                new Spec { Shape = BottleShapeIds.Flask, Colors = "DWGC", Hidden = 3 },
            },
        };

        static readonly float[] RowBaseY = { 5.0f, 0.6f, -3.8f, -8.2f };

        [MenuItem("BottleSort/VFX/Build Sandbox Scene")]
        public static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = cameraObject.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 9.6f;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.016f, 0.039f, 0.133f);
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;

            var background = new GameObject("Background").AddComponent<BackgroundView>();
            background.TargetCamera = cam;

            var bottlesRoot = new GameObject("Bottles").transform;
            var views = new List<BottleView>();
            for (int row = 0; row < Rows.Length; row++)
            {
                var specs = Rows[row];
                float startX = -(specs.Length - 1) * Spacing * 0.5f;

                var shelf = new GameObject($"Shelf {row + 1}", typeof(SpriteRenderer)).AddComponent<ShelfView>();
                shelf.transform.position = new Vector3(0f, RowBaseY[row], 0f);
                shelf.Width = (specs.Length - 1) * Spacing + 2.3f;

                for (int i = 0; i < specs.Length; i++)
                {
                    var spec = specs[i];
                    var go = new GameObject($"Bottle {row + 1}.{i + 1} {spec.Shape}");
                    go.transform.SetParent(bottlesRoot, false);
                    go.transform.position = new Vector3(startX + i * Spacing, RowBaseY[row], 0f);
                    go.transform.localScale = Vector3.one * BottleScale;
                    var view = go.AddComponent<BottleView>();
                    view.SetShape(spec.Shape);
                    view.SetLayers(ParseColors(spec.Colors), spec.Capacity, spec.Hidden);
                    view.SetFrozen(spec.Frozen);
                    view.SetLocked(spec.Locked, spec.LockColor);
                    view.SetTint(spec.Tint);
                    view.SetState(spec.State, true);
                    views.Add(view);
                }
            }

            var pour = new GameObject("Pour Animator").AddComponent<PourAnimator>();
            var confetti = new GameObject("Confetti").AddComponent<ConfettiEffect>();
            confetti.TargetCamera = cam;

            var sandbox = new GameObject("VFX Sandbox").AddComponent<VfxSandbox>();
            var serialized = new SerializedObject(sandbox);
            serialized.FindProperty("background").objectReferenceValue = background;
            serialized.FindProperty("pourAnimator").objectReferenceValue = pour;
            serialized.FindProperty("confetti").objectReferenceValue = confetti;
            var list = serialized.FindProperty("bottles");
            list.arraySize = views.Count;
            for (int i = 0; i < views.Count; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = views[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[VfxSandboxBuilder] Scene saved: " + ScenePath);
        }

        [MenuItem("BottleSort/VFX/Render Sandbox Screens")]
        public static void RenderScreens()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Directory.CreateDirectory(ScreensDir);
            var cam = Camera.main;
            // В batchmode у камеры аспект окна по умолчанию, а эффекты считают края кадра заранее
            cam.aspect = 9f / 16f;
            var background = Object.FindFirstObjectByType<BackgroundView>();
            var views = new List<BottleView>(Object.FindObjectsByType<BottleView>(FindObjectsSortMode.None));
            views.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

            background.SetTheme(BackgroundTheme.Navy, true);
            Capture(cam, 1080, 1920, ScreensDir + "/sandbox_vfx.png");

            // Кадр переливания: классическая из первого ряда льёт синий в стакан второго
            var source = views.Find(v => v.name.StartsWith("Bottle 1.2"));
            var target = views.Find(v => v.name.StartsWith("Bottle 2.4"));
            source.SetState(BottleVisualState.Normal, true);
            var stream = PourStream.Create();
            PourAnimator.PoseForPreview(source, target, stream, LiquidPalette.Get(Palette.Blue));
            stream.SetTime(0.18f);
            background.SetTheme(BackgroundTheme.Violet, true);
            Capture(cam, 1080, 1920, ScreensDir + "/sandbox_pour.png");
            Object.DestroyImmediate(stream.gameObject);
            source.EndExternalMotion();

            var themes = (BackgroundTheme[])System.Enum.GetValues(typeof(BackgroundTheme));
            const int w = 360, h = 640;
            var strip = new Color32[w * themes.Length * h];
            for (int t = 0; t < themes.Length; t++)
            {
                background.SetTheme(themes[t], true);
                var pixels = Render(cam, w, h);
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    strip[y * w * themes.Length + t * w + x] = pixels[y * w + x];
            }
            Raster.SavePng(ScreensDir + "/sandbox_themes.png", w * themes.Length, h, strip);

            var confetti = Object.FindFirstObjectByType<ConfettiEffect>();
            background.SetTheme(BackgroundTheme.Amber, true);
            confetti.Burst();
            foreach (var ps in confetti.GetComponentsInChildren<ParticleSystem>())
                if (!ps.main.loop)
                    ps.Simulate(0.7f, true, true, true);
            Capture(cam, 1080, 1920, ScreensDir + "/sandbox_confetti.png");
            confetti.Clear();

            Debug.Log("[VfxSandboxBuilder] Screens saved to " + ScreensDir);
        }

        public static void RunAll()
        {
            BottleArtGenerator.Run();
            BuildScene();
            RenderScreens();
        }

        static void Capture(Camera cam, int width, int height, string path)
        {
            Raster.SavePng(path, width, height, Render(cam, width, height));
        }

        static Color32[] Render(Camera cam, int width, int height)
        {
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var previous = cam.targetTexture;
            cam.targetTexture = rt;
            // Фон подгоняется под камеру в LateUpdate, в редакторе его надо дёрнуть вручную
            foreach (var bg in Object.FindObjectsByType<BackgroundView>(FindObjectsSortMode.None))
                bg.FitToCamera();

            var request = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, request))
                RenderPipeline.SubmitRenderRequest(cam, request);
            else
                cam.Render();

            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            RenderTexture.active = active;
            cam.targetTexture = previous;

            var pixels = texture.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
                pixels[i].a = 255;
            Object.DestroyImmediate(texture);
            rt.Release();
            Object.DestroyImmediate(rt);
            return pixels;
        }

        static int[] ParseColors(string letters)
        {
            var colors = new int[letters.Length];
            for (int i = 0; i < letters.Length; i++)
                colors[i] = Palette.FromLetter(letters[i]);
            return colors;
        }
    }
}
