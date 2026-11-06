using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using BottleSort.Core;
using BottleSort.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace BottleSort.UI.Tests
{
    // Кадры для скриншотов магазина: настоящая игра во весь слайд, без баннера, фон темы сочнее обычного.
    // Низ слайда закрывает волна с подписью (Store/Screenshots/template_v2.html), поэтому safe area снизу
    // поднята над ней. В marks.json — где на кадре главные бутылки: туда шаблон ставит руку, стрелку и сияние.
    // Запуск: -runTests -testPlatform PlayMode -testFilter StoreScreenshotCapture (без -nographics).
    [Explicit, Category("Store")]
    public sealed class StoreScreenshotCapture
    {
        const string OutputDir = "Store/Screenshots/source_v2";
        // Уровни в два ряда: в три ряда поле над волной выходит мелким.
        const int PourLevel = 257;
        const int FlaskLevel = 45;
        const int RescueLevel = 208;
        const int ShapesLevel = 280;
        const int LoseLevel = 74;
        // Не конец главы: иначе поверх победы выйдет сундук главы.
        const int WinLevel = 75;
        // Пока идут подготовительные ходы, время ускорено.
        const float FastForward = 4f;

        struct Size
        {
            public string Dir;
            public int Width;
            public int Height;
            public int Top;
            public int Bottom;
        }

        static readonly Size[] Sizes =
        {
            new Size { Dir = "rustore", Width = 1080, Height = 1920, Top = 36, Bottom = 230 },
            new Size { Dir = "appstore_6.7", Width = 1290, Height = 2796, Top = 110, Bottom = 330 }
        };

        static readonly int[] BrightColors =
            { Palette.Red, Palette.Yellow, Palette.Green, Palette.Cyan, Palette.Orange, Palette.Pink, Palette.Lime };

        // Те же темы, что в игре, только светлее: на слайде у каждого кадра свой яркий цвет.
        static readonly (Theme theme, string center, string middle, string edge, string halo, string spark)[] BrightThemes =
        {
            (Theme.Navy, "#2A63D6", "#153A93", "#0B1E5C", "#63A4FF", "#B4D2FF"),
            (Theme.Violet, "#7B3FE4", "#4720A6", "#230F61", "#C38DFF", "#E2C8FF"),
            (Theme.Teal, "#129AB0", "#0A6278", "#053847", "#48E6F5", "#B4F6FF"),
            (Theme.Rose, "#C72374", "#80104A", "#420828", "#FF77B5", "#FFC4DE"),
            (Theme.Emerald, "#1A9A63", "#0D6040", "#053622", "#52FFC4", "#C0FFE2"),
            (Theme.Amber, "#C27416", "#7A440A", "#3A2004", "#FFC765", "#FFE2A8")
        };

        string _backup;
        Array _themes;
        object[] _originalThemes;
        Camera _camera;
        RenderTexture _target;
        Size _size;
        ScreenRouter _router;
        GameScreen _game;
        readonly List<string> _marks = new List<string>();
        readonly List<string> _slideMarks = new List<string>();

        [SetUp]
        public void Prepare()
        {
            _backup = File.Exists(PlayerData.SavePath) ? File.ReadAllText(PlayerData.SavePath) : null;
            PlayerData.DeleteSave();
            var data = PlayerData.Current;
            data.coins = 2480;
            data.highestUnlocked = 120;
            data.tutorialGiftTaken = true;
            data.undoCount = 4;
            data.hintCount = 3;
            data.extraBottleCount = 2;
            data.noAds = true;
            data.Save();
            BrightenThemes();
            Time.captureDeltaTime = 1f / 60f;
        }

        [TearDown]
        public void Restore()
        {
            Time.timeScale = 1f;
            Time.captureDeltaTime = 0f;
            SafeAreaFitter.Simulated = null;
            if (_originalThemes != null)
                for (int i = 0; i < _originalThemes.Length; i++)
                    _themes.SetValue(_originalThemes[i], i);
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
        public IEnumerator Slides()
        {
            yield return OpenGame();
            foreach (var size in Sizes)
            {
                UseSize(size);
                _marks.Clear();
                yield return PourSlide();
                yield return FlaskSlide();
                yield return RescueSlide("03", RescueLevel);
                yield return ShapesSlide("04", ShapesLevel);
                yield return WinSlide();
                yield return LoseSlide();
                File.WriteAllText(Path.Combine(OutputDir, size.Dir, "marks.json"), "{\n" + string.Join(",\n", _marks) + "\n}\n");
            }
        }

        // 1. Обычный уровень в начале партии: бутылка льёт яркий цвет.
        IEnumerator PourSlide()
        {
            yield return OpenLevel(PourLevel, Theme.Violet);
            var solution = Solution(PourLevel);
            // Начало партии, пока бутылки ещё пёстрые. Яркий цвет из открытой бутылки льётся в пустую в нижнем ряду:
            // струя видна на всю высоту, а рука под целью не закрывает соседей.
            float lowerRow = Slot(0).yMax;
            int index = Pick(solution, 2, 10, (state, move) =>
                state[move.To].IsEmpty && state[move.From].HiddenCount == 0 && state.PourAmount(move) >= 2 &&
                Array.IndexOf(BrightColors, state[move.From].TopColor) >= 0 && Slot(move.To).yMin > lowerRow);
            Assert.GreaterOrEqual(index, 0, "no bright pour into an empty bottle on level " + PourLevel);
            yield return PlayTo(solution, index);
            yield return CapturePour(solution[index], "01", 0.45f);
        }

        // 2. Большая колба как запасник: в неё сливают разные цвета, в кадре — налив, после которого в ней 7–8 слоёв.
        IEnumerator FlaskSlide()
        {
            yield return OpenLevel(FlaskLevel, Theme.Navy);
            var state = _game.Session.State;
            int flask = -1;
            for (int i = 0; i < state.BottleCount; i++)
                if (state.IsBigFlask(state[i]))
                    flask = i;
            Assert.GreaterOrEqual(flask, 0, "level " + FlaskLevel + " has no big flask");

            // Сразу сливаем в колбу верхние слои разных цветов: решение уровня залило бы её одним цветом.
            var legal = new List<Move>();
            while (true)
            {
                state = _game.Session.State;
                var store = BestPourInto(state, flask, legal);
                Assert.IsTrue(store.HasValue, "nothing to pour into the flask, it has " + state[flask].Count);
                int after = state[flask].Count + state.PourAmount(store.Value);
                if (after >= state[flask].Capacity - 1)
                {
                    yield return CapturePour(store.Value, "02", 0.85f);
                    yield break;
                }
                yield return Play(store.Value);
            }
        }

        // 3. Спасение: зверёк с табличкой HELP! виден целиком, поэтому без струи — бутылка для фигурки уже выбрана и светится.
        IEnumerator RescueSlide(string name, int level)
        {
            yield return OpenLevel(level, Theme.Teal);
            var state = _game.Session.State;
            int figure = -1;
            for (int i = 0; i < state.BottleCount; i++)
                if (state[i].Recipe != null)
                    figure = i;
            Assert.GreaterOrEqual(figure, 0, "level " + level + " has no figure");
            var solution = Solution(level);
            int index = Pick(solution, 0, solution.Count, (s, move) =>
                move.To == figure && s[figure].Count >= 2 && s[figure].Count + s.PourAmount(move) < s[figure].Capacity);
            if (index < 0)
                index = solution.FindIndex(move => move.To == figure);
            yield return PlayTo(solution, index);
            yield return Idle();
            Time.timeScale = 1f;
            var pick = solution[index];
            Tap(pick.From);
            yield return Frames(0.45f);
            Shot(name, ("source", Slot(pick.From)), ("figure", Slot(figure)));
        }

        // 4. Бутылки разных форм и скрытые слои. Бутылка выбрана и светится, шаблон дорисует стрелку к цели.
        IEnumerator ShapesSlide(string name, int level)
        {
            yield return OpenLevel(level, Theme.Rose);
            var solution = Solution(level);
            for (int i = 0; i < 2; i++)
                yield return Play(solution[i]);
            yield return Idle();
            Time.timeScale = 1f;
            var move = solution[2];
            Tap(move.From);
            yield return Frames(0.45f);
            Shot(name, ("source", Slot(move.From)), ("target", Slot(move.To)));
        }

        // 5. Победа с тремя звёздами и монетами.
        IEnumerator WinSlide()
        {
            yield return OpenLevel(WinLevel, Theme.Amber);
            var win = _router.Get<WinScreen>();
            win.Setup(new WinResult { Level = WinLevel, Stars = 3, Moves = 22, Reward = LevelPlan.Reward(WinLevel, 3), IsBest = true });
            _router.Show(ScreenId.Win, false);
            yield return Frames(2.2f);
            Shot("05");
        }

        // 6. Ходы кончились по-настоящему: на уровне с лимитом сыграны все ходы, карточка вышла сама.
        IEnumerator LoseSlide()
        {
            var level = LevelLoader.Get(LoseLevel);
            yield return Until(() => level.IsCompleted, 20f, "level " + LoseLevel);
            var solution = level.Result.Solution;
            level.Result.MoveLimit = Mathf.Min(14, solution.Count - 3);
            yield return OpenLevel(LoseLevel, Theme.Emerald);
            for (int i = 0; i < level.Result.MoveLimit; i++)
                yield return Play(solution[i]);
            var card = _router.Get<RewardPopup>();
            Time.timeScale = 1f;
            yield return Until(() => card.gameObject.activeInHierarchy && card.GetComponent<CanvasGroup>().blocksRaycasts, 10f, "out of moves card");
            yield return Frames(1.2f);
            Shot("06");
            _router.ClosePopup(card);
            yield return Frames(0.4f);
        }

        IEnumerator OpenGame()
        {
            SceneManager.LoadScene(SceneLoader.GameScene);
            yield return null;
            yield return Until(() => (_router = FindRouter()) != null, 10f, "router");
            _game = _router.Get<GameScreen>();
            yield return Until(() => _router.Current == _game && !_game.IsBusy, 20f, "first level");
            _camera = Camera.main;
            // Overlay-канвас камера не рисует. Ставим его на камеру и поверх всех спрайтов, как overlay в игре.
            var canvas = GameObject.Find("Canvas").GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = _camera;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 30000;
        }

        IEnumerator OpenLevel(int level, Theme theme)
        {
            Time.timeScale = 1f;
            PlayerData.Current.theme = theme.ToString().ToLowerInvariant();
            PlayerData.Current.highestUnlocked = Mathf.Max(PlayerData.Current.highestUnlocked, level);
            _router.PlayLevel(level);
            yield return null;
            yield return Until(() => _router.Current == _game && !_game.IsBusy && _game.Level == level, 30f, "level " + level);
            HideBanner();
            yield return Frames(1.2f);
            Time.timeScale = FastForward;
        }

        static void HideBanner()
        {
            var stub = GameObject.Find("Banner Stub");
            if (stub != null)
                stub.SetActive(false);
        }

        // Первый ход решения между from и to, который подходит для кадра. Проверяем на копии уровня, ничего не играя.
        int Pick(List<Move> solution, int from, int to, Func<LevelState, Move, bool> fits)
        {
            var state = _game.Session.State.Clone();
            for (int i = 0; i < Mathf.Min(to, solution.Count); i++)
            {
                if (i >= from && fits(state, solution[i]))
                    return i;
                state.Apply(solution[i]);
            }
            return -1;
        }

        IEnumerator PlayTo(List<Move> solution, int index)
        {
            for (int i = 0; i < index; i++)
                yield return Play(solution[i]);
        }

        static Move? BestPourInto(LevelState state, int flask, List<Move> legal)
        {
            state.GetLegalMoves(legal);
            Move? best = null;
            int bestScore = int.MinValue;
            foreach (var move in legal)
            {
                if (move.Kind != MoveKind.Pour || move.To != flask || move.From == flask)
                    continue;
                var source = state[move.From];
                if (source.IsCompleted(state.Unit))
                    continue;
                // Колба должна выйти радугой: по слою, и каждый раз новый цвет.
                int score = -state.PourAmount(move);
                for (int layer = 0; layer < state[flask].Count; layer++)
                    if (state[flask][layer] == source.TopColor)
                        score -= 20;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = move;
                }
            }
            return best;
        }

        IEnumerator Play(Move move)
        {
            yield return Idle();
            int made = _game.Session.MovesMade;
            Tap(move.From);
            Tap(move.To);
            Assert.AreEqual(made + 1, _game.Session.MovesMade, $"move {move} on level {_game.Level}");
        }

        // Налив в кадре: бутылка наклонена, струя идёт, в цели поднялась примерно share новых слоёв.
        IEnumerator CapturePour(Move move, string name, float share, params (string key, Rect rect)[] extra)
        {
            yield return Idle();
            Time.timeScale = 1f;
            int layers = _game.Session.State.PourAmount(move);
            var source = View(move.From);
            Tap(move.From);
            yield return Frames(0.3f);
            Tap(move.To);
            // Подъём, перелёт и наклон — 0.69 с, уровень начинает меняться через 0.07 с после струи.
            yield return Frames(0.76f + layers * BottleView.LayerStepSeconds * share);
            Time.timeScale = 0f;
            yield return null;
            var marks = new List<(string, Rect)>
            {
                ("source", Slot(move.From)),
                ("target", Slot(move.To)),
                ("body", BodyRect(source)),
                ("mouth", Point(source.MouthPosition)),
                ("surface", Point(View(move.To).SurfacePosition))
            };
            marks.AddRange(extra);
            Shot(name, marks.ToArray());
            Time.timeScale = 1f;
            yield return Idle();
        }

        IEnumerator Idle()
        {
            yield return Until(() => !_game.IsAnimating, 15f, "pours");
        }

        void Tap(int index) => _game.Field.SlotAt(index).OnPointerDown(new PointerEventData(EventSystem.current));

        BottleView View(int index) => ((BottleViewAdapter)_game.Field.SlotAt(index).View).View;

        Rect Slot(int index)
        {
            var corners = new Vector3[4];
            _game.Field.SlotAt(index).Rect.GetWorldCorners(corners);
            return Pixels(corners[0], corners[2]);
        }

        // Видимое стекло наклонённой бутылки, без ореола выбора.
        Rect BodyRect(BottleView view)
        {
            Bounds? bounds = null;
            foreach (var renderer in view.Body.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || renderer.name.IndexOf("glow", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                if (bounds == null)
                    bounds = renderer.bounds;
                else
                {
                    var b = bounds.Value;
                    b.Encapsulate(renderer.bounds);
                    bounds = b;
                }
            }
            return bounds == null ? new Rect() : Pixels(bounds.Value.min, bounds.Value.max);
        }

        Rect Point(Vector3 world) => Pixels(world, world);

        // В пикселях кадра, отсчёт от левого верхнего угла.
        Rect Pixels(Vector3 worldMin, Vector3 worldMax)
        {
            var a = _camera.WorldToScreenPoint(worldMin);
            var b = _camera.WorldToScreenPoint(worldMax);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), _size.Height - Mathf.Max(a.y, b.y), Mathf.Max(a.x, b.x), _size.Height - Mathf.Min(a.y, b.y));
        }

        void UseSize(Size size)
        {
            _size = size;
            if (_target != null)
            {
                _camera.targetTexture = null;
                _target.Release();
            }
            _target = new RenderTexture(size.Width, size.Height, 24);
            _camera.targetTexture = _target;
            float bottom = size.Bottom / (float)size.Height;
            float top = size.Top / (float)size.Height;
            SafeAreaFitter.Simulated = new Rect(0f, Screen.height * bottom, Screen.width, Screen.height * (1f - top - bottom));
        }

        void Shot(string name, params (string key, Rect rect)[] marks)
        {
            HideBanner();
            // «Цвет собран» и прочие всплывашки закрыли бы поле.
            foreach (var toast in Object.FindObjectsByType<Toast>(FindObjectsSortMode.None))
                toast.HideNow();
            Canvas.ForceUpdateCanvases();
            _camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = _target;
            var texture = new Texture2D(_size.Width, _size.Height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, _size.Width, _size.Height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            string dir = Path.Combine(OutputDir, _size.Dir);
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), texture.EncodeToPNG());
            Object.Destroy(texture);

            _slideMarks.Clear();
            foreach (var (key, rect) in marks)
                _slideMarks.Add(string.Format(CultureInfo.InvariantCulture, "\"{0}\": [{1:0}, {2:0}, {3:0}, {4:0}]",
                    key, rect.xMin, rect.yMin, rect.xMax, rect.yMax));
            _marks.Add($"  \"{name}\": {{ {string.Join(", ", _slideMarks)} }}");
            Debug.Log($"[Store] {_size.Dir}/{name} level {_game.Level}");
        }

        static IEnumerator Frames(float seconds)
        {
            int count = Mathf.CeilToInt(seconds * 60f);
            for (int i = 0; i < count; i++)
                yield return null;
        }

        static IEnumerator Until(Func<bool> condition, float seconds, string what)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > deadline)
                    Assert.Fail("Timed out waiting for " + what);
                yield return null;
            }
        }

        static List<Move> Solution(int level)
        {
            var task = LevelLoader.Get(level);
            Assert.IsTrue(task.IsCompleted, "level is loaded");
            return task.Result.Solution;
        }

        // Цвета тем лежат в закрытом массиве BackgroundView; на время съёмки подменяем их и потом возвращаем.
        void BrightenThemes()
        {
            var field = typeof(BackgroundView).GetField("Themes", BindingFlags.NonPublic | BindingFlags.Static);
            var type = typeof(BackgroundView).GetNestedType("ThemeColors", BindingFlags.NonPublic);
            _themes = (Array)field.GetValue(null);
            _originalThemes = new object[_themes.Length];
            for (int i = 0; i < _themes.Length; i++)
                _originalThemes[i] = _themes.GetValue(i);
            foreach (var (theme, center, middle, edge, halo, spark) in BrightThemes)
            {
                ColorUtility.TryParseHtmlString(spark, out var sparkGlow);
                sparkGlow.a = 0.85f;
                _themes.SetValue(Activator.CreateInstance(type, center, middle, edge, halo, sparkGlow), (int)theme);
            }
        }

        static ScreenRouter FindRouter()
        {
            var router = Object.FindFirstObjectByType<ScreenRouter>();
            return router != null && router.gameObject.scene == SceneManager.GetActiveScene() ? router : null;
        }
    }
}
