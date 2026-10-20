using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BottleSort.Services;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace BottleSort.UI.Tests
{
    // Баннер теперь виден везде, кроме заставки: поверх попапов, на победе, в магазине и на обучении.
    // Снимки с плашкой баннера в Docs/Screens/banner и проверка, что ни одна кнопка не уходит под неё.
    // Запуск: -runTests -testPlatform PlayMode -testFilter BannerCoverageCapture (без -nographics).
    [Explicit, Category("Devices")]
    public sealed class BannerCoverageCapture
    {
        const string OutputDir = "Docs/Screens/banner";
        const float PerPt = 2.5f;
        const float MinGap = 4f;

        struct Device
        {
            public string Name;
            public int Width;
            public int Height;
            public int Top;
            public int Bottom;
        }

        static readonly Device[] Devices =
        {
            new Device { Name = "iphone8", Width = 1080, Height = 1920, Top = 58, Bottom = 0 },
            new Device { Name = "iphone15", Width = 1170, Height = 2532, Top = 141, Bottom = 102 },
            new Device { Name = "android", Width = 1080, Height = 2400, Top = 84, Bottom = 48 }
        };

        string _backup;
        Camera _camera;
        RenderTexture _target;
        Canvas _canvas;
        readonly List<string> _problems = new List<string>();

        [SetUp]
        public void SaveProgress()
        {
            _backup = File.Exists(PlayerData.SavePath) ? File.ReadAllText(PlayerData.SavePath) : null;
            PlayerData.DeleteSave();
            _problems.Clear();
        }

        [TearDown]
        public void RestoreProgress()
        {
            SafeAreaFitter.Simulated = null;
            if (_camera != null)
                _camera.targetTexture = null;
            if (_target != null)
                _target.Release();
            PlayerData.DeleteSave();
            if (_backup != null)
                File.WriteAllText(PlayerData.SavePath, _backup);
            PlayerData.Reload();
        }

        [UnityTest]
        public IEnumerator MenuScreensAndPopups()
        {
            Progress(24);
            yield return Open(SceneLoader.MenuScene);
            var router = FindRouter();

            foreach (var device in Devices)
            {
                UseDevice(device);
                foreach (var screen in new[] { ScreenId.Menu, ScreenId.Levels, ScreenId.Shop })
                {
                    router.Show(screen, false);
                    yield return Measure(device, screen.ToString(), router);
                }
                router.Show(ScreenId.Menu, false);

                foreach (var popup in new[] { ScreenId.Calendar, ScreenId.Settings, ScreenId.Wheel })
                {
                    router.ShowPopup(popup);
                    yield return Measure(device, popup.ToString(), router);
                    CloseAll(router);
                }

                var card = router.Get<RewardPopup>();
                card.SetupChoice("daily_gift_title", "daily_gift_text", EconomyRules.DailyGift, "reward_double_ad", null, "gift_claim", null);
                router.ShowPopup(ScreenId.Reward);
                yield return Measure(device, "daily_gift", router);
                CloseAll(router);
            }
            Assert.IsEmpty(_problems, string.Join("\n", _problems));
        }

        [UnityTest]
        public IEnumerator GameTutorialLossCardsAndWin()
        {
            Progress(24);
            yield return Open(SceneLoader.GameScene);
            var router = FindRouter();
            var shelves = GameObject.Find("Field Shelves");

            foreach (var device in Devices)
            {
                UseDevice(device);
                foreach (int level in new[] { 1, 3, 24 })
                {
                    yield return Play(router, level);
                    yield return Measure(device, "game_level" + level, router);
                }

                // Мировые полки поля рисуются той же камерой и легли бы поверх карточек.
                if (shelves != null)
                    shelves.SetActive(false);
                var card = router.Get<RewardPopup>();
                card.SetupChoice("lose_moves_title", "lose_moves_text", default, "lose_moves_ad", null, "lose_restart", null);
                router.ShowPopup(ScreenId.Reward);
                yield return Measure(device, "lose_moves", router);
                CloseAll(router);

                card.SetupChoice("deadend_title", "deadend_text", new RewardBundle(0, bottles: 1), "deadend_bottle_ad", null,
                    "lose_restart", null);
                router.ShowPopup(ScreenId.Reward);
                yield return Measure(device, "deadend", router);
                CloseAll(router);

                if (Has(router, ScreenId.Settings))
                {
                    router.ShowPopup(ScreenId.Settings);
                    yield return Measure(device, "game_settings", router);
                    CloseAll(router);
                }

                var win = router.Get<WinScreen>();
                win.Setup(new WinResult { Level = 24, Stars = 3, Moves = 18, Reward = 170, IsBest = true });
                router.Show(ScreenId.Win, false);
                yield return Measure(device, "win", router);
                win.Setup(new WinResult { Level = 3, Stars = 3, Moves = 6, Reward = 50, IsTutorial = true });
                router.Show(ScreenId.Win, false);
                yield return Measure(device, "win_tutorial", router);

                router.ShowPopup(ScreenId.Gift);
                yield return Measure(device, "tutorial_gift", router);
                CloseAll(router);
                if (shelves != null)
                    shelves.SetActive(true);
            }
            Assert.IsEmpty(_problems, string.Join("\n", _problems));
        }

        static void Progress(int highest)
        {
            var data = PlayerData.Current;
            data.coins = 1250;
            data.highestUnlocked = highest;
            data.tutorialGiftTaken = true;
            data.undoCount = 6;
            data.hintCount = 2;
            data.extraBottleCount = 1;
            data.Save();
        }

        IEnumerator Open(string scene)
        {
            SceneManager.LoadScene(scene);
            yield return null;
            float deadline = Time.realtimeSinceStartup + 20f;
            while ((SceneLoader.IsBusy || FindRouter() == null || FindRouter().Current == null || Busy(FindRouter())) &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsNotNull(FindRouter(), "router");
            _camera = Camera.main;
            _canvas = GameObject.Find("Canvas").GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceCamera;
            _canvas.worldCamera = _camera;
            _canvas.planeDistance = 1f;
            yield return Wait(1.6f);
        }

        static bool Busy(ScreenRouter router) => router.Current is GameScreen game && game.IsBusy;

        static IEnumerator Play(ScreenRouter router, int level)
        {
            router.PlayLevel(level);
            var game = router.Get<GameScreen>();
            float deadline = Time.realtimeSinceStartup + 20f;
            while ((router.Current != game || game.IsBusy) && Time.realtimeSinceStartup < deadline)
                yield return null;
        }

        static bool Has(ScreenRouter router, ScreenId id) => router.Screens.Any(screen => screen.Id == id);

        static void CloseAll(ScreenRouter router)
        {
            foreach (var screen in router.Screens)
                router.ClosePopup(screen);
        }

        void UseDevice(Device device)
        {
            if (_target != null)
            {
                _camera.targetTexture = null;
                _target.Release();
            }
            _target = new RenderTexture(device.Width, device.Height, 24);
            _camera.targetTexture = _target;
            float bottom = device.Bottom / (float)device.Height;
            float top = device.Top / (float)device.Height;
            SafeAreaFitter.Simulated = new Rect(0f, Screen.height * bottom, Screen.width, Screen.height * (1f - top - bottom));
        }

        IEnumerator Measure(Device device, string name, ScreenRouter router)
        {
            yield return Wait(0.9f);
            var stub = GameObject.Find("Banner Stub");
            string label = device.Name + " " + name;
            if (stub == null)
            {
                _problems.Add(label + ": баннера нет на экране");
                yield break;
            }
            var stubCanvas = stub.GetComponent<Canvas>();
            stubCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            stubCanvas.worldCamera = _camera;
            stubCanvas.planeDistance = 0.5f;
            Canvas.ForceUpdateCanvases();
            yield return null;
            Shot(device.Name + "_" + name, device);

            var root = (RectTransform)_canvas.transform;
            var banner = Bounds(root, (RectTransform)stub.transform.Find("SafeArea/Banner 320x50"));
            var screens = router.Screens.Where(screen => screen == router.Current || IsOpenPopup(screen));
            foreach (var screen in screens)
                foreach (var button in screen.GetComponentsInChildren<Button>())
                {
                    if (button.GetComponentInParent<ScrollRect>() != null)
                        continue;
                    var face = FaceOf(root, button);
                    // Подложка попапа «тап мимо — закрыть» растянута на весь экран, это не кнопка.
                    if (face.height > root.rect.height * 0.8f)
                        continue;
                    bool besideBanner = face.xMax < banner.xMin || face.xMin > banner.xMax;
                    float gap = (face.yMin - banner.yMax) / PerPt;
                    if (!besideBanner && face.yMax > banner.yMin && gap < MinGap)
                        _problems.Add($"{label}: {NameOf(button.transform, screen.transform)} в {gap:0.0} pt от верха баннера");
                }
            Debug.Log($"[BannerCoverage] {label}: checked");
        }

        static bool IsOpenPopup(UiScreen screen) =>
            screen.gameObject.activeInHierarchy && screen.GetComponent<CanvasGroup>().alpha > 0.5f &&
            screen.GetComponent<CanvasGroup>().blocksRaycasts;

        // Низ кнопки по видимой графике, без мягкой тени и ореола.
        static Rect FaceOf(RectTransform root, Button button)
        {
            Rect? face = null;
            foreach (var graphic in button.GetComponentsInChildren<Graphic>())
            {
                if (graphic.name.EndsWith("Shadow") || graphic.name == "Pulse" || !graphic.enabled || graphic.color.a <= 0.01f)
                    continue;
                var bounds = Bounds(root, graphic.rectTransform);
                face = face == null ? bounds : Rect.MinMaxRect(Mathf.Min(face.Value.xMin, bounds.xMin), Mathf.Min(face.Value.yMin, bounds.yMin),
                    Mathf.Max(face.Value.xMax, bounds.xMax), Mathf.Max(face.Value.yMax, bounds.yMax));
            }
            return face ?? Bounds(root, (RectTransform)button.transform);
        }

        static string NameOf(Transform item, Transform root)
        {
            string path = item.name;
            for (var parent = item.parent; parent != null && parent != root; parent = parent.parent)
                path = parent.name + "/" + path;
            return root.name + "/" + path;
        }

        static Rect Bounds(RectTransform root, RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var min = root.InverseTransformPoint(corners[0]);
            var max = root.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        void Shot(string name, Device device)
        {
            _camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = _target;
            var texture = new Texture2D(device.Width, device.Height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, device.Width, device.Height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            Directory.CreateDirectory(OutputDir);
            File.WriteAllBytes(Path.Combine(OutputDir, name + ".jpg"), texture.EncodeToJPG(85));
            Object.Destroy(texture);
        }

        static IEnumerator Wait(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until)
                yield return null;
        }

        static ScreenRouter FindRouter()
        {
            var router = Object.FindFirstObjectByType<ScreenRouter>();
            return router != null && router.gameObject.scene == SceneManager.GetActiveScene() ? router : null;
        }
    }
}
