using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace BottleSort.UI.Tests
{
    // Уровни 6 и 24 на разных телефонах с их вырезом и полосой жестов: снимки в Docs/Screens/devices
    // и проверка, что шапка не слипается, а поле не наезжает на бустеры.
    // Запуск: -runTests -testPlatform PlayMode -testFilter DeviceCapture (без -nographics).
    [Explicit, Category("Devices")]
    public sealed class DeviceCapture
    {
        const string OutputDir = "Docs/Screens/devices";
        // Минимальные зазоры в pt: между элементами шапки и между полкой поля и бейджами бустеров.
        const float HeaderGap = 2f;
        const float FieldGap = 8f;

        struct Device
        {
            public string Name;
            public int Width;
            public int Height;
            // Вырез сверху и полоса жестов снизу, в пикселях экрана.
            public int Top;
            public int Bottom;
        }

        static readonly Device[] Devices =
        {
            new Device { Name = "1080x1920_iphone8", Width = 1080, Height = 1920, Top = 58, Bottom = 0 },
            new Device { Name = "1170x2532_iphone15", Width = 1170, Height = 2532, Top = 141, Bottom = 102 },
            new Device { Name = "1080x2400_android", Width = 1080, Height = 2400, Top = 84, Bottom = 48 },
            new Device { Name = "1440x3200_android", Width = 1440, Height = 3200, Top = 112, Bottom = 64 },
            new Device { Name = "1640x2360_ipad10", Width = 1640, Height = 2360, Top = 48, Bottom = 40 }
        };

        string _backup;
        Camera _camera;
        RenderTexture _target;

        [SetUp]
        public void SaveProgress()
        {
            _backup = File.Exists(PlayerData.SavePath) ? File.ReadAllText(PlayerData.SavePath) : null;
            PlayerData.DeleteSave();
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
        public IEnumerator Levels6And24OnPhones()
        {
            var data = PlayerData.Current;
            data.coins = 1250;
            data.highestUnlocked = 25;
            data.tutorialGiftTaken = true;
            data.undoCount = 6;
            data.hintCount = 2;
            data.extraBottleCount = 1;
            data.Save();

            SceneManager.LoadScene("Main");
            yield return null;
            ScreenRouter router = null;
            float deadline = Time.realtimeSinceStartup + 5f;
            while ((router = FindRouter()) == null && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsNotNull(router, "router");
            _camera = Camera.main;
            var canvas = GameObject.Find("Canvas").GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = _camera;
            canvas.planeDistance = 1f;

            var problems = new List<string>();
            foreach (var device in Devices)
            {
                UseDevice(device);
                foreach (int level in new[] { 6, 24 })
                {
                    router.PlayLevel(level);
                    var game = router.Get<GameScreen>();
                    deadline = Time.realtimeSinceStartup + 20f;
                    while ((router.Current != game || game.IsBusy) && Time.realtimeSinceStartup < deadline)
                        yield return null;
                    float settle = Time.realtimeSinceStartup + 0.8f;
                    while (Time.realtimeSinceStartup < settle)
                        yield return null;
                    yield return Shot($"{device.Name}_level{level}", device);
                    Check(device.Name + " L" + level, canvas, game, problems);
                    router.Show(ScreenId.Menu, false);
                    yield return null;
                }
            }
            Assert.IsEmpty(problems, string.Join("\n", problems));
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
            // SafeAreaFitter считает долю от Screen, поэтому вырез переводим в те же доли.
            float bottom = device.Bottom / (float)device.Height;
            float top = device.Top / (float)device.Height;
            SafeAreaFitter.Simulated = new Rect(0f, Screen.height * bottom, Screen.width, Screen.height * (1f - top - bottom));
        }

        // Всё меряем в координатах корня Canvas: там 2.5 единицы на pt.
        static void Check(string label, Canvas canvas, GameScreen game, List<string> problems)
        {
            var root = (RectTransform)canvas.transform;
            var content = game.transform.Find("Content");
            var home = Bounds(root, (RectTransform)content.Find("Home"));
            var plate = Bounds(root, (RectTransform)content.Find("LevelPlate"));
            var coins = Bounds(root, (RectTransform)content.Find("Coins"));
            float left = (plate.xMin - home.xMax) / 2.5f;
            float right = (coins.xMin - plate.xMax) / 2.5f;
            Debug.Log($"[DeviceCapture] {label}: header gaps {left:0.0} / {right:0.0} pt");
            if (left < HeaderGap || right < HeaderGap)
                problems.Add($"{label}: шапка слиплась, зазоры {left:0.0} и {right:0.0} pt");

            float badgesTop = float.MinValue;
            foreach (var badge in content.Find("Boosters").GetComponentsInChildren<TextPill>())
                badgesTop = Mathf.Max(badgesTop, Bounds(root, (RectTransform)badge.transform).yMax);
            float facesTop = Bounds(root, (RectTransform)content.Find("Boosters")).yMax;
            // Спрайт ShelfView шире и выше самой полки из-за свечения, поэтому меряем по ячейке полки:
            // планка начинается на 2.5 pt ниже верха ячейки и толщиной 12 pt (в пикселях макета внутри доски).
            float shelfBottom = float.MaxValue;
            foreach (var slot in content.GetComponentsInChildren<ShelfViewAdapter>())
            {
                var bounds = Bounds(root, (RectTransform)slot.transform);
                float perCss = bounds.height / 12f;
                shelfBottom = Mathf.Min(shelfBottom, bounds.yMax - (2.5f + 12f) * perCss);
            }
            float gap = (shelfBottom - Mathf.Max(badgesTop, facesTop)) / 2.5f;
            Debug.Log($"[DeviceCapture] {label}: shelf to boosters {gap:0.0} pt");
            if (gap < FieldGap)
                problems.Add($"{label}: полка в {gap:0.0} pt от бустеров");
        }

        static Rect Bounds(RectTransform root, RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var min = root.InverseTransformPoint(corners[0]);
            var max = root.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        IEnumerator Shot(string name, Device device)
        {
            yield return null;
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

        static ScreenRouter FindRouter()
        {
            var router = Object.FindFirstObjectByType<ScreenRouter>();
            return router != null && router.gameObject.scene == SceneManager.GetActiveScene() ? router : null;
        }
    }
}
