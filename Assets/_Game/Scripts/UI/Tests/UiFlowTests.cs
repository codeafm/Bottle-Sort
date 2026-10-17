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
        public IEnumerator SplashMenuTutorialWinAcrossScenes()
        {
            SceneManager.LoadScene(SceneLoader.SplashScene);
            // Сцена сменится только в конце кадра; без этого найдётся роутер сцены от прошлого теста.
            yield return null;
            yield return WaitScreen(ScreenId.Splash, 5f, "splash");
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Capture("ui_splash");

            // После заставки всегда меню, и в первый запуск тоже; обучение — по кнопке «Играть».
            yield return WaitScreen(ScreenId.Menu, 12f, "menu after splash");
            Assert.AreEqual(SceneLoader.MenuScene, SceneManager.GetActiveScene().name);
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Capture("ui_menu");

            Click(FindRouter().Current.transform, "Content/Play");
            yield return WaitLevel(1, 5f, "level 1 from menu");
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Capture("ui_level1");

            var game = Game;
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

            yield return WaitScreen(ScreenId.Win, 6f, "win screen");
            var win = FindRouter().Get<WinScreen>();
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Capture("ui_win");
            Assert.AreEqual(2, PlayerData.Current.highestUnlocked);
            Assert.AreEqual(3, PlayerData.Current.StarsOf(1));
            Assert.Greater(PlayerData.Current.coins, 0);

            // Следующий уровень открывается в той же сцене, без перезагрузки.
            var scene = SceneManager.GetActiveScene();
            Click(win.transform, "Content/Next");
            yield return WaitLevel(2, 5f, "level 2");
            Assert.AreEqual(scene, SceneManager.GetActiveScene(), "next level must not reload the scene");
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Capture("ui_level2");

            Click(Game.transform, "Content/Home/Face");
            yield return WaitScreen(ScreenId.Menu, 5f, "menu again");
            Click(FindRouter().Current.transform, "Content/Levels");
            yield return WaitScreen(ScreenId.Levels, 3f, "levels");
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Capture("ui_levels");

            FindRouter().Show(ScreenId.Shop);
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Capture("ui_shop");

            FindRouter().Show(ScreenId.Menu);
            Click(FindRouter().Current.transform, "Content/Settings/Face");
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Capture("ui_settings");
        }

        // Сцену игры можно открыть напрямую (из редактора): уровень берётся текущий у игрока.
        [UnityTest]
        public IEnumerator GameSceneStartsOnPlayersLevel()
        {
            var data = PlayerData.Current;
            data.highestUnlocked = 7;
            data.tutorialGiftTaken = true;
            data.Save();

            SceneManager.LoadScene(SceneLoader.GameScene);
            yield return null;
            yield return WaitLevel(7, 20f, "level 7");
        }

        static GameScreen Game => FindRouter().Get<GameScreen>();

        static IEnumerator WaitScreen(ScreenId id, float seconds, string what)
        {
            yield return WaitUntil(() =>
            {
                var router = FindRouter();
                return !SceneLoader.IsBusy && router != null && router.Current != null && router.Current.Id == id;
            }, seconds, what);
        }

        static IEnumerator WaitLevel(int level, float seconds, string what)
        {
            yield return WaitScreen(ScreenId.Game, seconds, what);
            var game = Game;
            yield return WaitUntil(() => !game.IsBusy, seconds, what);
            Assert.AreEqual(level, game.Level, what);
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

            SceneManager.LoadScene(SceneLoader.GameScene);
            yield return null;
            yield return WaitLevel(46, 20f, "level 46 on start");
            var router = FindRouter();
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

            SceneManager.LoadScene(SceneLoader.MenuScene);
            yield return null;
            yield return WaitScreen(ScreenId.Menu, 5f, "menu");
            var router = FindRouter();

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
