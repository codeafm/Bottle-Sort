using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BottleSort.UI
{
    // Кадр текущей сцены в текстуру. Overlay-канвасы камера не рисует, поэтому на один рендер переводим
    // канвасы сцены в Screen Space - Camera и потом возвращаем как было.
    public static class ScreenGrab
    {
        struct Saved
        {
            public Canvas Canvas;
            public RenderMode Mode;
            public Camera Camera;
            public float Plane;
            public float Scale;
        }

        // divider — во сколько раз текстура меньше экрана. Освобождать через RenderTexture.ReleaseTemporary.
        public static RenderTexture Render(Camera camera, int divider)
        {
            var target = camera.targetTexture;
            int fullWidth = target != null ? target.width : Screen.width;
            int fullHeight = target != null ? target.height : Screen.height;
            int width = Mathf.Max(2, fullWidth / divider);
            int height = Mathf.Max(2, fullHeight / divider);

            var saved = new List<Saved>();
            var scene = SceneManager.GetActiveScene();
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!canvas.isRootCanvas || canvas.gameObject.scene != scene)
                    continue;
                saved.Add(new Saved
                {
                    Canvas = canvas, Mode = canvas.renderMode, Camera = canvas.worldCamera, Plane = canvas.planeDistance,
                    Scale = canvas.scaleFactor
                });
                // CanvasScaler пересчитает масштаб только в своём Update, а раскладка в маленькой текстуре
                // должна быть той же, поэтому уменьшаем масштаб сами.
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.scaleFactor = canvas.scaleFactor * width / fullWidth;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
            }

            var frame = RenderTexture.GetTemporary(width, height, 24);
            frame.filterMode = FilterMode.Bilinear;
            camera.targetTexture = frame;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            camera.targetTexture = target;

            foreach (var item in saved)
            {
                item.Canvas.renderMode = item.Mode;
                item.Canvas.worldCamera = item.Camera;
                item.Canvas.planeDistance = item.Plane;
                item.Canvas.scaleFactor = item.Scale;
            }
            Canvas.ForceUpdateCanvases();
            return frame;
        }
    }
}
