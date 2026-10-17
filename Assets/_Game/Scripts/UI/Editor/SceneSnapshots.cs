using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BottleSort.UI.Editor
{
    // Снимки сцен так, как их видно в редакторе без Play: каждая сцена и каждый её экран по очереди.
    // Сцены не сохраняются. Из batchmode: -executeMethod BottleSort.UI.Editor.SceneSnapshots.CaptureAll
    public static class SceneSnapshots
    {
        const int Width = 1080;
        const int Height = 2337;
        const string OutputDir = "Logs/EditModeShots";
        static readonly string[] Scenes = { SceneLoader.SplashScene, SceneLoader.MenuScene, SceneLoader.GameScene };

        [MenuItem("BottleSort/UI/Snapshots of Scenes (edit mode)")]
        public static void CaptureAll()
        {
            Directory.CreateDirectory(OutputDir);
            foreach (string name in Scenes)
            {
                EditorSceneManager.OpenScene($"Assets/_Game/Scenes/{name}.unity", OpenSceneMode.Single);
                var screens = Object.FindFirstObjectByType<ScreenRouter>().Screens;
                var visible = new HashSet<UiScreen>();
                foreach (var screen in screens)
                    if (screen.gameObject.activeSelf)
                        visible.Add(screen);
                // Попап снимаем поверх экрана, который открыт в сцене по умолчанию.
                foreach (var screen in screens)
                {
                    foreach (var other in screens)
                        other.gameObject.SetActive(other == screen || (screen.IsPopup && visible.Contains(other)));
                    foreach (var preview in Object.FindObjectsByType<FieldPreview>(FindObjectsSortMode.None))
                        preview.Rebuild();
                    Capture($"{name}_{screen.name}");
                }
            }
            Debug.Log("[SceneSnapshots] saved to " + OutputDir);
        }

        static void Capture(string fileName)
        {
            var camera = Camera.main;
            var canvas = GameObject.Find("Canvas").GetComponent<Canvas>();
            var target = new RenderTexture(Width, Height, 24);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            camera.targetTexture = target;
            Canvas.ForceUpdateCanvases();
            camera.Render();

            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(Path.Combine(OutputDir, fileName + ".png"), texture.EncodeToPNG());

            camera.targetTexture = null;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Object.DestroyImmediate(texture);
            target.Release();
        }
    }
}
