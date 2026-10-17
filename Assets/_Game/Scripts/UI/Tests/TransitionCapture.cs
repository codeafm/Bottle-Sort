using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace BottleSort.UI.Tests
{
    // Кадры запуска (интро CODEAFM → заставка → меню) и перехода игра → меню в Docs/Screens/transition.
    // Запуск: -runTests -testPlatform PlayMode -testFilter TransitionCapture (без -nographics).
    [Explicit, Category("MockupCompare")]
    public sealed class TransitionCapture
    {
        const int Width = 1080;
        const int Height = 2337;
        const float TopInsetPoints = 44f;
        const float Step = 0.1f;
        const string OutputDir = "Docs/Screens/transition";

        string _backup;
        RenderTexture _target;
        RenderTexture _small;
        Camera _camera;

        [SetUp]
        public void SaveProgress()
        {
            _backup = File.Exists(PlayerData.SavePath) ? File.ReadAllText(PlayerData.SavePath) : null;
            PlayerData.DeleteSave();
            var data = PlayerData.Current;
            data.coins = 1250;
            data.highestUnlocked = 24;
            data.tutorialGiftTaken = true;
            data.giftDate = "";
            data.wheelSpins = 2;
            data.stars = new List<int> { 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 2, 3, 1 };
            data.Save();
            float top = TopInsetPoints * (Width / 390f) / Height;
            SafeAreaFitter.Simulated = new Rect(0f, 0f, Screen.width, Screen.height * (1f - top));
            _target = new RenderTexture(Width, Height, 24);
            _small = new RenderTexture(Width / 2, Height / 2, 0);
            Directory.CreateDirectory(OutputDir);
        }

        [TearDown]
        public void RestoreProgress()
        {
            SafeAreaFitter.Simulated = null;
            if (_camera != null)
                _camera.targetTexture = null;
            _target.Release();
            _small.Release();
            PlayerData.DeleteSave();
            if (_backup != null)
                File.WriteAllText(PlayerData.SavePath, _backup);
            PlayerData.Reload();
        }

        [UnityTest]
        public IEnumerator SplashToMenu()
        {
            foreach (string old in Directory.GetFiles(OutputDir, "launch_*.jpg"))
                File.Delete(old);
            SceneManager.LoadScene(SceneLoader.SplashScene);
            yield return null;
            float start = Time.realtimeSinceStartup;
            float next = 0f;
            float menuSince = -1f;
            float outroFrom = -1f;
            while (Time.realtimeSinceStartup - start < 20f)
            {
                float now = Time.realtimeSinceStartup - start;
                var splash = FindRouter() != null ? FindRouter().Get<SplashScreen>() : null;
                if (outroFrom < 0f && splash != null && splash.IsReady)
                    outroFrom = now;
                if (now >= next)
                {
                    Shot("launch", now);
                    // Интро — каждые 0.2 с, сама заставка — раз в полсекунды, уход и появление меню — каждые 0.1 с.
                    next += now < BrandIntro.Duration + 0.6f ? 0.2f : outroFrom < 0f ? 0.5f : Step;
                }
                var router = FindRouter();
                bool inMenu = router != null && router.Current != null && router.Current.Id == ScreenId.Menu;
                if (inMenu && menuSince < 0f)
                    menuSince = now;
                if (menuSince >= 0f && now - menuSince > 1.3f)
                    yield break;
                yield return null;
            }
            Assert.Fail("menu did not appear after the splash");
        }

        [UnityTest]
        public IEnumerator GameToMenu()
        {
            foreach (string old in Directory.GetFiles(OutputDir, "back_*.jpg"))
                File.Delete(old);
            SceneManager.LoadScene(SceneLoader.GameScene);
            yield return null;
            float deadline = Time.realtimeSinceStartup + 20f;
            GameScreen game = null;
            while ((FindRouter() == null || (game = FindRouter().Get<GameScreen>()) == null || game.IsBusy)
                   && Time.realtimeSinceStartup < deadline)
            {
                // Поле раскладывается под размер экрана: сразу переводим вывод на телефон.
                if (FindRouter() != null)
                    PointToPhone();
                yield return null;
            }
            PointToPhone();
            game.Field.Refit();
            yield return new WaitForSecondsRealtime(0.8f);

            float start = Time.realtimeSinceStartup;
            FindRouter().Show(ScreenId.Menu);
            Debug.Log($"[TransitionCapture] tap to first frame of the switch: {(Time.realtimeSinceStartup - start) * 1000f:0} ms");
            float next = 0f;
            while (Time.realtimeSinceStartup - start < 1.8f)
            {
                float now = Time.realtimeSinceStartup - start;
                if (now >= next)
                {
                    Shot("back", now);
                    next += Step;
                }
                yield return null;
            }
            Assert.AreEqual(ScreenId.Menu, FindRouter().Current.Id);
        }

        // Каждый кадр заново: после смены сцены свои камера и канвас; шторку SceneLoader рисуем той же камерой поверх.
        void Shot(string prefix, float time)
        {
            PointToPhone();
            Canvas.ForceUpdateCanvases();
            _camera.Render();

            Graphics.Blit(_target, _small);
            var previous = RenderTexture.active;
            RenderTexture.active = _small;
            var texture = new Texture2D(_small.width, _small.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, _small.width, _small.height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            string name = $"{prefix}_{time.ToString("0.00", CultureInfo.InvariantCulture)}s.jpg";
            File.WriteAllBytes(Path.Combine(OutputDir, name), texture.EncodeToJPG(82));
            Object.Destroy(texture);
        }

        void PointToPhone()
        {
            _camera = Camera.main;
            _camera.targetTexture = _target;
            var canvas = GameObject.Find("Canvas").GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = _camera;
            canvas.planeDistance = 1f;
            var loader = GameObject.Find("SceneLoader");
            if (loader != null)
            {
                var overlay = loader.GetComponent<Canvas>();
                overlay.renderMode = RenderMode.ScreenSpaceCamera;
                overlay.worldCamera = _camera;
                overlay.planeDistance = 0.5f;
            }
        }

        static ScreenRouter FindRouter()
        {
            var router = Object.FindFirstObjectByType<ScreenRouter>();
            return router != null && router.gameObject.scene == SceneManager.GetActiveScene() ? router : null;
        }
    }
}
