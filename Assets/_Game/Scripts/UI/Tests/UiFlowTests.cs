using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BottleSort.UI.Tests
{
    // Сквозной путь из сцены Boot: заставка → обучение 1 → меню → уровень 1 → победа → уровень 2,
    // плюс карта, магазин и настройки. Скриншоты кладём в Logs/UiScreens, сводка для документации — Docs/Screens/ui_overview.jpg.
    public sealed class UiFlowTests
    {
        const string ScreensDir = "Logs/UiScreens";

        string _backup;

        [SetUp]
        public void SaveProgress()
        {
            _backup = File.Exists(PlayerData.SavePath) ? File.ReadAllText(PlayerData.SavePath) : null;
            PlayerData.DeleteSave();
        }

        [TearDown]
        public void RestoreProgress()
        {
            PlayerData.DeleteSave();
            if (_backup != null)
                File.WriteAllText(PlayerData.SavePath, _backup);
            PlayerData.Reload();
        }

        [UnityTest]
        public IEnumerator BootSplashMenuTutorialWinNextLevel()
        {
            SceneManager.LoadScene("Boot");
            // Сцена сменится только в конце кадра; без этого найдётся роутер сцены от прошлого теста.
            yield return null;
            ScreenRouter router = null;
            yield return WaitUntil(() => (router = FindRouter()) != null, 5f, "router");
            yield return WaitUntil(() => router.Current != null, 2f, "first screen");
            Assert.AreEqual(ScreenId.Splash, router.Current.Id);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Capture("ui_splash");

            // Первый запуск: после заставки сразу обучение, меню — по кнопке «домой».
            var game = router.Get<GameScreen>();
            yield return WaitUntil(() => router.Current == game && !game.IsBusy, 10f, "level 1 after splash");
            Assert.AreEqual(1, game.Level);

            Click(game.transform, "Content/Home/Face");
            yield return WaitUntil(() => router.Current.Id == ScreenId.Menu, 3f, "menu");
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Capture("ui_menu");

            Click(router.Current.transform, "Content/Play");
            yield return WaitUntil(() => router.Current == game && !game.IsBusy, 5f, "level 1 from menu");
            Assert.AreEqual(1, game.Level);
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Capture("ui_level1");

            // Уровень 1 строгий: бутылку не по руке взять нельзя.
            Tap(game, 1);
            yield return null;
            Assert.AreEqual(-1, game.Selected, "strict tutorial must ignore a wrong bottle");

            Tap(game, 0);
            yield return null;
            Assert.AreEqual(0, game.Selected);
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Capture("ui_level1_selected");
            Tap(game, 2);
            // Ходы теперь с анимацией переливания: следующий ход — когда бутылки вернулись на место.
            yield return WaitUntil(() => !game.IsAnimating, 4f, "first pour");
            Tap(game, 1);
            Tap(game, 0);
            yield return WaitUntil(() => !game.IsAnimating, 4f, "second pour");
            yield return new WaitForSecondsRealtime(1.6f);
            Tap(game, 1);
            Tap(game, 2);

            var win = router.Get<WinScreen>();
            yield return WaitUntil(() => router.Current == win, 6f, "win screen");
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Capture("ui_win");
            Assert.AreEqual(2, PlayerData.Current.highestUnlocked);
            Assert.AreEqual(3, PlayerData.Current.StarsOf(1));
            Assert.Greater(PlayerData.Current.coins, 0);

            Click(win.transform, "Content/Next");
            yield return WaitUntil(() => router.Current == game && !game.IsBusy, 5f, "level 2");
            Assert.AreEqual(2, game.Level);
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Capture("ui_level2");

            Click(game.transform, "Content/Home/Face");
            yield return WaitUntil(() => router.Current.Id == ScreenId.Menu, 3f, "menu again");
            Click(router.Current.transform, "Content/Levels");
            yield return WaitUntil(() => router.Current.Id == ScreenId.Levels, 3f, "levels");
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Capture("ui_levels");

            router.Show(ScreenId.Shop);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Capture("ui_shop");

            router.Show(ScreenId.Menu);
            Click(router.Current.transform, "Content/Settings/Face");
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Capture("ui_settings");
        }

        [UnityTest]
        public IEnumerator LaterLevelsBuildWithHeaderInPlace()
        {
            var data = PlayerData.Current;
            data.highestUnlocked = 46;
            data.tutorialGiftTaken = true;
            data.undoCount = 3;
            data.hintCount = 2;
            data.extraBottleCount = 1;
            data.Save();

            SceneManager.LoadScene("Main");
            yield return null;
            ScreenRouter router = null;
            yield return WaitUntil(() => (router = FindRouter()) != null, 5f, "router");
            var game = router.Get<GameScreen>();
            Vector3[] header = null;

            foreach (int level in new[] { 24, 30, 45 })
            {
                router.PlayLevel(level);
                yield return WaitUntil(() => router.Current == game && !game.IsBusy, 20f, "level " + level);
                yield return new WaitForSecondsRealtime(0.8f);
                yield return Capture("ui_level" + level);

                // Шапка одна и та же на всех уровнях: плашка уровня стоит на месте и не меняет размер.
                var plate = (RectTransform)game.transform.Find("Content/LevelPlate");
                var corners = new Vector3[4];
                plate.GetWorldCorners(corners);
                if (header != null)
                    for (int i = 0; i < 4; i++)
                        Assert.That(Vector3.Distance(header[i], corners[i]), Is.LessThan(0.5f), "level plate moved on level " + level);
                header = corners;
            }
        }

        // Размытый фон колеса снимается перерисовкой канваса через камеру; после этого канвас должен
        // вернуться в Overlay с прежним масштабом, иначе поедет весь интерфейс.
        [UnityTest]
        public IEnumerator WheelBlurLeavesCanvasAsItWas()
        {
            var data = PlayerData.Current;
            data.tutorialGiftTaken = true;
            data.Save();

            SceneManager.LoadScene("Main");
            yield return null;
            ScreenRouter router = null;
            yield return WaitUntil(() => (router = FindRouter()) != null, 5f, "router");
            router.Show(ScreenId.Menu, false);
            yield return null;

            var canvas = GameObject.Find("Canvas").GetComponent<Canvas>();
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
            float scale = canvas.scaleFactor;
            router.ShowPopup(ScreenId.Wheel);
            yield return null;

            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
            Assert.AreEqual(scale, canvas.scaleFactor, 1e-4f);
            var blur = router.Get<WheelPopup>().GetComponentInChildren<RawImage>();
            Assert.IsTrue(blur.enabled, "blur shown");
            Assert.IsNotNull(blur.texture, "blur texture");
            var content = router.Get<WheelPopup>().transform.Find("Content").GetComponent<CanvasGroup>();
            Assert.AreEqual(1f, content.alpha);

            router.ClosePopup(router.Get<WheelPopup>());
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.IsNull(blur.texture, "blur released");
        }

        // Сразу после LoadScene ещё жив роутер прошлой сцены, берём только из активной.
        static ScreenRouter FindRouter()
        {
            var router = Object.FindFirstObjectByType<ScreenRouter>();
            return router != null && router.gameObject.scene == SceneManager.GetActiveScene() ? router : null;
        }

        static void Tap(GameScreen game, int index)
        {
            game.Field.SlotAt(index).OnPointerDown(new PointerEventData(EventSystem.current));
        }

        static void Click(Transform root, string path)
        {
            var target = root.Find(path);
            Assert.IsNotNull(target, "No UI element at " + path);
            var button = target.GetComponent<Button>();
            Assert.IsNotNull(button, "No button at " + path);
            button.onClick.Invoke();
        }

        static IEnumerator WaitUntil(Func<bool> condition, float seconds, string what)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline)
                    Assert.Fail("Timed out waiting for " + what);
                yield return null;
            }
        }

        // Overlay-канвас камера не рисует, поэтому на время снимка переводим его в Screen Space - Camera
        // и рендерим камеру в текстуру размера телефона.
        static IEnumerator Capture(string name, int width = 1080, int height = 2340)
        {
            var camera = Camera.main;
            var canvas = GameObject.Find("Canvas").GetComponent<Canvas>();
            var mode = canvas.renderMode;
            var target = new RenderTexture(width, height, 24);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            camera.targetTexture = target;
            yield return null;
            yield return null;

            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;

            Directory.CreateDirectory(ScreensDir);
            File.WriteAllBytes(Path.Combine(ScreensDir, name + ".png"), texture.EncodeToPNG());

            camera.targetTexture = null;
            canvas.renderMode = mode;
            Object.Destroy(texture);
            target.Release();
            Object.Destroy(target);
            yield return null;
        }
    }
}
