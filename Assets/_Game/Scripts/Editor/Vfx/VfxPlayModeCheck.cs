using System.Collections.Generic;
using BottleSort.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BottleSort.Editor.Vfx
{
    // Прогон переливания в play mode без рук: льёт синий из 1.2 в стакан 2.4, снимает кадр посреди налива,
    // проверяет итоговые слои и что бутылка вернулась на место.
    // Из batchmode (без -quit): -executeMethod BottleSort.Editor.Vfx.VfxPlayModeCheck.Run
    [InitializeOnLoad]
    public static class VfxPlayModeCheck
    {
        const string Flag = "BottleSort.VfxPlayModeCheck";
        static double startedAt;
        static int step;
        static BottleView source, target;

        static VfxPlayModeCheck()
        {
            if (SessionState.GetBool(Flag, false))
                EditorApplication.playModeStateChanged += OnPlayMode;
        }

        public static void Run()
        {
            EditorSceneManager.OpenScene(VfxSandboxBuilder.ScenePath);
            SessionState.SetBool(Flag, true);
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.EnterPlaymode();
        }

        static void OnPlayMode(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode)
                return;
            startedAt = EditorApplication.timeSinceStartup;
            step = 0;
            EditorApplication.update += Tick;
        }

        static void Tick()
        {
            double t = EditorApplication.timeSinceStartup - startedAt;
            if (step == 0 && t > 0.5)
            {
                step = 1;
                var views = new List<BottleView>(Object.FindObjectsByType<BottleView>(FindObjectsSortMode.None));
                source = views.Find(v => v.name.StartsWith("Bottle 1.2"));
                target = views.Find(v => v.name.StartsWith("Bottle 2.4"));
                var animator = Object.FindFirstObjectByType<PourAnimator>();
                Debug.Log($"[PlayCheck] before: source={string.Join(",", source.Colors)} target={string.Join(",", target.Colors)}");
                animator.Play(source, target, new[] { 2, 0, 4 }, 0, new[] { 4, 4, 1, 1 }, 0,
                    () => Debug.Log("[PlayCheck] pour finished callback"));
                startedAt = EditorApplication.timeSinceStartup;
            }
            else if (step == 1 && t > 0.95)
            {
                step = 2;
                Camera.main.aspect = 9f / 16f;
                var rt = new RenderTexture(1080, 1920, 24);
                Camera.main.targetTexture = rt;
                Camera.main.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(1080, 1920, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 1080, 1920), 0, 0);
                tex.Apply();
                System.IO.File.WriteAllBytes("Docs/Screens/sandbox_pour_live.png", tex.EncodeToPNG());
                RenderTexture.active = null;
                Camera.main.targetTexture = null;
                Debug.Log($"[PlayCheck] mid-pour frame saved, source body rot={source.Body.eulerAngles.z:0}");
            }
            else if (step == 2 && t > 2.6)
            {
                step = 3;
                bool ok = source.LayerCount == 3 && target.LayerCount == 4 && source.Body.localPosition.sqrMagnitude < 1e-4f
                          && Mathf.Abs(Mathf.DeltaAngle(source.Body.eulerAngles.z, 0f)) < 0.1f && !source.IsAnimating && !target.IsAnimating;
                Debug.Log($"[PlayCheck] after: source={string.Join(",", source.Colors)} target={string.Join(",", target.Colors)} " +
                          $"bodyPos={source.Body.localPosition} ok={ok}");
                EditorApplication.update -= Tick;
                SessionState.SetBool(Flag, false);
                EditorApplication.Exit(ok ? 0 : 1);
            }
        }
    }
}
