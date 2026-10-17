using System.Collections;
using System.Collections.Generic;
using System.IO;
using BottleSort.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace BottleSort.UI.Tests
{
    // Снимки экранов игры в тех же состояниях, что Docs/Mockup/png: 1080×2337, монеты 1 250, уровень 24,
    // раскладки уровней из Game.dc.html. Сравнение и склейки делает Tools/UiArt/compare_mockup.py.
    // Запуск: -runTests -testPlatform PlayMode -testFilter MockupCapture (без -nographics).
    [Explicit, Category("MockupCompare")]
    public sealed class MockupCapture
    {
        const int Width = 1080;
        const int Height = 2337;
        // Вырез iPhone: в макете под статус-бар 54 pt, из них 44 pt — safe area, 10 — отступ экрана.
        const float TopInsetPoints = 44f;
        const float PointsToPixels = Width / 390f;
        const string OutputDir = "Logs/MockupCompare";

        string _backup;
        RenderTexture _target;
        Camera _camera;

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
        public IEnumerator CaptureAllScreens()
        {
            var data = PlayerData.Current;
            data.coins = 1250;
            data.highestUnlocked = 24;
            data.tutorialGiftTaken = true;
            data.undoCount = 3;
            data.hintCount = 2;
            data.extraBottleCount = 1;
            // Бейджи меню как в макете: подарок дня не забран, у колеса два вращения.
            data.giftDate = "";
            data.wheelSpins = 2;
            int[] stars = { 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 2, 3, 1 };
            data.stars = new List<int>(stars);
            data.Save();
            SimulateNotch();

            yield return Open(SceneLoader.SplashScene);
            Router.Get<SplashScreen>().Freeze(4, 0.41f);
            yield return Settle(0.4f);
            yield return Shot("00_splash");

            // Заставка уже грузит меню, переход идёт через неё.
            Router.Show(ScreenId.Menu, false);
            yield return Arrive();
            yield return Settle(0.6f);
            yield return Shot("01_main");

            Router.Show(ScreenId.Levels, false);
            yield return Settle(0.6f);
            yield return Shot("02_levels");

            yield return Level(MockupLevels.Normal(), "03a_game_normal");
            yield return Level(MockupLevels.Special(), "03b_game_special");
            yield return Level(MockupLevels.Big(), "03c_game_big");

            Router.Get<WinScreen>().Setup(new WinResult { Level = 24, Stars = 3, Moves = 18, Reward = 170, IsBest = true });
            Router.Show(ScreenId.Win, false);
            yield return Settle(1.4f);
            yield return Shot("04_win");

            data.owned.Add("wine");
            data.owned.Add("flask");
            data.owned.Add("violet");
            data.bottleSkin = "wine";
            data.theme = "violet";
            data.Save();
            Router.Show(ScreenId.Shop, false);
            yield return Arrive();
            yield return Settle(0.6f);
            yield return Shot("05_shop");
            data.bottleSkin = "classic";
            data.theme = "auto";
            data.Save();

            for (int level = 1; level <= 3; level++)
            {
                Router.PlayLevel(level);
                yield return WaitReady();
                yield return Settle(1.2f);
                yield return Shot($"1{level - 1}_tutorial_{level}");
            }

            string[] names = { "20_level_100", "21_level_200", "22_level_500", "23_level_1000", "24_level_1500", "25_level_2000" };
            var generated = MockupLevels.Late();
            for (int i = 0; i < generated.Count; i++)
                yield return Level(generated[i], names[i]);
        }

        // Три кадра Wheel.dc.html: покой (1 попытка), вращение (rotate 1290°) и выигрыш джекпота (сектор +150 под стрелкой).
        [UnityTest]
        public IEnumerator CaptureWheel()
        {
            var data = PlayerData.Current;
            data.coins = 1250;
            data.highestUnlocked = 24;
            data.tutorialGiftTaken = true;
            data.wheelSpins = 1;
            data.wheelDate = "";
            data.Save();
            SimulateNotch();

            yield return Open(SceneLoader.MenuScene);
            yield return Settle(0.6f);
            data.wheelSpins = 1;
            Router.ShowPopup(ScreenId.Wheel);
            yield return Settle(1f);
            yield return Shot("06_wheel_live");

            var wheel = Router.Get<WheelPopup>();
            data.wheelSpins = 0;
            wheel.ShowSpinningFrame(-1290f);
            yield return Settle(0.3f);
            yield return Shot("06_wheel_spin");

            wheel.ShowRewardFrame(4);
            yield return Settle(0.9f);
            yield return Shot("06_wheel_reward");
        }

        static ScreenRouter Router => FindRouter();

        static void SimulateNotch()
        {
            float top = TopInsetPoints * PointsToPixels / Height;
            SafeAreaFitter.Simulated = new Rect(0f, 0f, Screen.width, Screen.height * (1f - top));
        }

        IEnumerator Open(string scene)
        {
            SceneManager.LoadScene(scene);
            yield return null;
            yield return Arrive();
        }

        // Дождаться конца перехода между сценами; в новой сцене свои камера и канвас, их снова переводим на телефон.
        IEnumerator Arrive()
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while ((SceneLoader.IsBusy || Router == null || Router.Current == null) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsNotNull(Router, "router");
            Assert.IsNotNull(Router.Current, "first screen");
            if (_camera != Camera.main)
            {
                RenderToPhone();
                yield return null;
            }
        }

        IEnumerator Level(GeneratedLevel level, string name)
        {
            LevelLoader.Put(level.Spec.Number, level);
            Router.PlayLevel(level.Spec.Number);
            yield return WaitReady();
            yield return Settle(0.8f);
            yield return Shot(name);
        }

        IEnumerator WaitReady()
        {
            // Из меню уровень открывается в сцене Game: сначала ждём сам переход.
            yield return null;
            yield return Arrive();
            var game = Router.Get<GameScreen>();
            Assert.IsNotNull(game, "game scene");
            float deadline = Time.realtimeSinceStartup + 20f;
            while ((Router.Current != game || game.IsBusy) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsFalse(game.IsBusy, "level did not start");
        }

        static IEnumerator Settle(float seconds)
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

        // Overlay-канвас камера не рисует, поэтому на всё время съёмки переводим его в Screen Space - Camera,
        // а камера рисует в текстуру размера телефона: раскладка сразу считается под 1080×2337.
        void RenderToPhone()
        {
            if (_target != null)
                _target.Release();
            _camera = Camera.main;
            _target = new RenderTexture(Width, Height, 24);
            _camera.targetTexture = _target;
            var canvas = GameObject.Find("Canvas").GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = _camera;
            canvas.planeDistance = 1f;
        }

        IEnumerator Shot(string name)
        {
            yield return null;
            _camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = _target;
            var texture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            Directory.CreateDirectory(OutputDir);
            File.WriteAllBytes(Path.Combine(OutputDir, name + ".png"), texture.EncodeToPNG());
            Object.Destroy(texture);
        }
    }

    // Раскладки уровней из Game.dc.html (LEVELS и build()), включая его генератор на LCG.
    static class MockupLevels
    {
        static readonly string[] Mixed = { "wine", "jug", "teapot", "flask", "potion", "glass", "jar" };

        public static GeneratedLevel Normal() => Make(24, 4, false, new[] { "RBGY", "YRPB", "GCRP", "BPCC", "PGYR", "CYBG", "", "" });

        public static GeneratedLevel Special() => Make(30, 4, true, new[] { "KBGY", "YKPB", "GCKP", "BPCC", "PGYK", "CYBG", "", "" });

        public static GeneratedLevel Big()
        {
            var layout = new[] { "RBYG", "PCOR", "BYGP", "CORB", "YGPC", "ORBY", "GPCO", "", "" };
            return Make(45, 4, false, layout, bigIndex: 8);
        }

        public static List<GeneratedLevel> Late() => new List<GeneratedLevel>
        {
            Generated(100, 4, false, "RBYGPCOK", 2, 2, -1, 0, false, false, 100),
            Generated(200, 4, false, "RBYGPCOKL", 2, 1, 2, 3, false, false, 200),
            Generated(500, 5, true, "RBYGPCOKL", 2, 2, -1, 0, false, false, 500),
            Generated(1000, 4, true, "RBYGPCOKLN", 2, 2, -1, 0, true, false, 1000),
            Generated(1500, 4, false, "RBYGPCOKLN", 1, 2, 6, 4, false, true, 1500),
            Generated(2000, 5, true, "RBYGPCOKLNWD", 2, 3, 4, 5, true, false, 2000)
        };

        static GeneratedLevel Generated(int number, int unit, bool mixed, string colors, int empty, int hidden,
            int frozenAt, int frozenTurns, bool lockLast, bool big, int seed)
        {
            // Тот же генератор, что в макете: double-арифметика как в JS, иначе раскладка разъедется.
            double state = seed * 7919.0 + 13.0;
            double Next()
            {
                state = (state * 1103515245.0 + 12345.0) % 2147483648.0;
                return state / 2147483648.0;
            }

            var pool = new List<char>();
            foreach (char c in colors)
                for (int k = 0; k < unit; k++)
                    pool.Add(c);
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = (int)System.Math.Floor(Next() * (i + 1));
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }

            var bottles = new List<List<char>>();
            for (int i = 0; i < pool.Count; i += unit)
                bottles.Add(pool.GetRange(i, unit));
            for (int i = 0; i < bottles.Count; i++)
            {
                var b = bottles[i];
                if (b.TrueForAll(c => c == b[0]))
                {
                    var next = bottles[(i + 1) % bottles.Count];
                    (b[0], next[0]) = (next[0], b[0]);
                }
            }
            for (int e = 0; e < empty; e++)
                bottles.Add(new List<char>());

            var layout = new List<string>();
            foreach (var b in bottles)
                layout.Add(new string(b.ToArray()));
            int bigIndex = -1;
            if (big)
            {
                bigIndex = 1;
                layout.Insert(1, "");
            }

            var level = Make(number, unit, mixed, layout.ToArray(), bigIndex);
            var levelState = level.State;
            for (int i = 0; i < levelState.BottleCount; i++)
            {
                var bottle = levelState[i];
                bottle.HiddenCount = System.Math.Min(hidden, System.Math.Max(bottle.Count - 1, 0));
                if (i == frozenAt)
                    bottle.FrozenTurns = frozenTurns;
                if (lockLast && i == levelState.BottleCount - 1)
                    bottle.IsLocked = true;
            }
            return level;
        }

        static GeneratedLevel Make(int number, int unit, bool mixed, string[] layout, int bigIndex = -1)
        {
            var bottles = new List<Bottle>();
            for (int i = 0; i < layout.Length; i++)
            {
                bool flask = i == bigIndex;
                string shape = flask ? LevelGenerator.FlaskShape : mixed ? Mixed[i % Mixed.Length] : Bottle.DefaultShape;
                var bottle = new Bottle(flask ? unit * 2 : unit, shape);
                foreach (char letter in layout[i])
                    bottle.Push(Palette.FromLetter(letter));
                bottles.Add(bottle);
            }
            var state = new LevelState(unit, bottles);
            return new GeneratedLevel
            {
                Spec = new LevelSpec { Number = number, Unit = unit, Colors = state.CountColors(), BigFlask = bigIndex >= 0 },
                State = state,
                Par = 20,
                ParExact = true,
                Solution = new List<Move>(),
                PassedAllFilters = true
            };
        }
    }
}
