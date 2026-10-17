using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
    // Уровни-спасения на настоящем поле: фигурка своей формой стоит посередине на два ряда, рецепт виден на стекле,
    // котик выпрыгивает, когда рецепт собран, а победа ждёт его прыжка. Кадры — в Logs/RescueScreens.
    public sealed class RescueLevelTests
    {
        const string ShotsDir = "Logs/RescueScreens";

        string _backup;

        [SetUp]
        public void SaveProgress()
        {
            _backup = File.Exists(PlayerData.SavePath) ? File.ReadAllText(PlayerData.SavePath) : null;
            PlayerData.DeleteSave();
            var data = PlayerData.Current;
            data.highestUnlocked = 78;
            data.tutorialGiftTaken = true;
            data.undoCount = 3;
            data.Save();
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
        public IEnumerator Level78_HippoInTheMiddle_CatJumpsOutAndTheLevelGoesOn()
        {
            ScreenRouter router = null;
            GameScreen game = null;
            yield return OpenLevel(78, (r, g) => { router = r; game = g; });
            using (var capture = new FrameCapture())
            {
                int figure = FigureIndex(game.Session.State);
                var view = ViewAt(game, figure);
                Assert.IsTrue(view.IsFigure, "the figure is drawn with its own art, not as a classic bottle");
                Assert.AreEqual("hippo", view.Shape);
                Assert.AreEqual(200f, view.BoxWidth);
                Assert.IsTrue(view.IsCatInside);
                AssertFigureInTheMiddle(game, figure);

                yield return new WaitForSecondsRealtime(0.8f);
                capture.Save("rescue_78_start");

                var solution = Solution(78);
                bool rescued = false;
                for (int i = 0; i < solution.Count; i++)
                {
                    yield return Play(game, solution[i]);
                    if (rescued || !game.Session.State[figure].IsFull)
                        continue;

                    rescued = true;
                    yield return WaitUntil(() => view.IsRescuing, 5f, "the cat jumps out when the recipe is complete");
                    yield return new WaitForSecondsRealtime(0.62f);
                    capture.Save("rescue_78_jump");
                    yield return WaitUntil(() => !view.IsRescuing, 3f, "end of the jump");
                    Assert.IsFalse(game.Session.IsWon, "the level goes on after the rescue");
                    Assert.IsFalse(view.IsCatInside, "the cat is gone, only the ring floats");
                    Assert.AreEqual(game, router.Current);
                    capture.Save("rescue_78_saved");

                    // Отмена возвращает котика в круг, а тот же ход снова его спасает
                    yield return WaitUntil(() => !game.IsAnimating, 5f, "pours");
                    FindBooster(game, Booster.Undo).onClick.Invoke();
                    yield return null;
                    Assert.IsFalse(game.Session.State[figure].IsFull);
                    Assert.IsTrue(view.IsCatInside, "undo puts the cat back");
                    yield return Play(game, solution[i]);
                    yield return WaitUntil(() => view.IsRescuing, 5f, "the second rescue");
                }
                Assert.IsTrue(rescued, "the solution fills the figure");
                Assert.IsTrue(game.Session.IsWon);
                yield return WaitUntil(() => router.Current is WinScreen, 5f, "win screen");
            }
        }

        [UnityTest]
        public IEnumerator Level328_TwoColorRecipe_CountsBothColorsAndWinsAfterTheJump()
        {
            ScreenRouter router = null;
            GameScreen game = null;
            yield return OpenLevel(328, (r, g) => { router = r; game = g; });
            using (var capture = new FrameCapture())
            {
                var state = game.Session.State;
                int figure = FigureIndex(state);
                var view = ViewAt(game, figure);
                Assert.IsTrue(view.IsFigure);
                Assert.AreEqual("whale", view.Shape);
                Assert.AreEqual(8, view.Recipe.Count, "the whole recipe is shown on the glass");
                AssertFigureInTheMiddle(game, figure);

                yield return new WaitForSecondsRealtime(0.8f);
                capture.Save("rescue_328_start");

                var solution = Solution(328);
                bool halfShot = false;
                int collected = GameScreen.CollectedColors(state);
                for (int i = 0; i < solution.Count; i++)
                {
                    yield return Play(game, solution[i]);
                    int now = GameScreen.CollectedColors(state);
                    Assert.GreaterOrEqual(now, collected, "progress never drops when the second recipe color goes in");
                    collected = now;
                    if (!halfShot && state[figure].Count >= 4 && state[figure].Count < 8)
                    {
                        halfShot = true;
                        yield return WaitUntil(() => !game.IsAnimating, 5f, "pour");
                        Assert.IsFalse(view.IsRescuing, "half a recipe is not a rescue");
                        yield return new WaitForSecondsRealtime(0.3f);
                        capture.Save("rescue_328_half");
                    }
                }
                Assert.IsTrue(halfShot);
                Assert.IsTrue(game.Session.IsWon);
                Assert.AreEqual(state.CountColors(), GameScreen.CollectedColors(state), "both recipe colors count as collected");
                yield return WaitUntil(() => router.Current is WinScreen, 8f, "win screen");
            }
        }

        // Собранную бутылку можно взять, раз её цвет ждёт фигурка, и если этот ход последний,
        // экран победы появляется только после прыжка котика.
        [UnityTest]
        public IEnumerator LastPourIntoTheFigure_WinWaitsForTheJump()
        {
            const int number = 4028;
            var figure = new Bottle(4, "fox", canPourOut: false, recipe: new byte[] { Palette.Purple, Palette.Purple, Palette.Purple, Palette.Purple });
            var full = new Bottle(4);
            for (int i = 0; i < 4; i++)
                full.Push(Palette.Purple);
            var state = new LevelState(4, new[] { full, figure, new Bottle(4) });
            LevelLoader.Put(number, new GeneratedLevel
            {
                Spec = new LevelSpec { Number = number, Unit = 4, Colors = 1, RescueRecipeColors = 1, RescueShape = "fox" },
                State = state,
                Par = 1,
                ParExact = true,
                Solution = new List<Move> { Move.Pour(0, 1) },
                PassedAllFilters = true
            });

            ScreenRouter router = null;
            GameScreen game = null;
            yield return OpenLevel(number, (r, g) => { router = r; game = g; });
            var view = ViewAt(game, 1);
            Assert.AreEqual("fox", view.Shape);

            Tap(game, 0);
            Assert.AreEqual(0, game.Selected, "a completed bottle is picked up for the figure");
            Tap(game, 1);
            Assert.IsTrue(game.Session.IsWon);

            yield return WaitUntil(() => view.IsRescuing, 5f, "jump");
            while (view.IsRescuing)
            {
                Assert.IsFalse(router.Current is WinScreen, "win waits for the cat");
                yield return null;
            }
            yield return WaitUntil(() => router.Current is WinScreen, 3f, "win screen");
        }

        [UnityTest]
        public IEnumerator Level2028_GeneratedOnTheFly_StillGetsAFigure()
        {
            GameScreen game = null;
            yield return OpenLevel(2028, (r, g) => game = g, 90f);
            using (var capture = new FrameCapture())
            {
                int figure = FigureIndex(game.Session.State);
                var view = ViewAt(game, figure);
                Assert.IsTrue(view.IsFigure, "level 2028 shows its figure: " + game.Session.State[figure].Shape);
                AssertFigureInTheMiddle(game, figure);
                yield return new WaitForSecondsRealtime(0.8f);
                capture.Save("rescue_2028_start");

                // Несколько ходов из решения, чтобы на кадре было видно игру, а не только начало
                var solution = Solution(2028);
                for (int i = 0; i < Mathf.Min(6, solution.Count); i++)
                    yield return Play(game, solution[i]);
                yield return WaitUntil(() => !game.IsAnimating, 5f, "pours");
                yield return new WaitForSecondsRealtime(0.3f);
                capture.Save("rescue_2028_play");
            }
        }

        // Фигурка стоит посередине поля и выше двух рядов бутылок по бокам
        static void AssertFigureInTheMiddle(GameScreen game, int figure)
        {
            var slots = game.Field.Slots;
            var figureRect = slots[figure].Rect;
            float bottleHeight = 0f;
            foreach (var slot in slots)
                if (slot.Index != figure)
                    bottleHeight = Mathf.Max(bottleHeight, slot.Rect.sizeDelta.y);
            Assert.Less(Mathf.Abs(figureRect.anchoredPosition.x), 1f, "figure column is centred");
            Assert.Greater(figureRect.sizeDelta.y, bottleHeight * 2f, "figure spans two rows");
            Assert.Greater(figureRect.sizeDelta.x, figureRect.sizeDelta.y * 0.8f, "figure keeps its 200×240 box");

            int left = 0, right = 0;
            foreach (var slot in slots)
            {
                if (slot.Index == figure || slot.Rect.anchoredPosition.y < figureRect.anchoredPosition.y - 1f)
                    continue;
                if (slot.Rect.anchoredPosition.x < 0f) left++;
                else right++;
            }
            Assert.AreEqual(left, right, "bottles stand on both sides of the figure");
        }

        static UnityEngine.UI.Button FindBooster(GameScreen game, Booster kind)
        {
            foreach (var booster in game.GetComponentsInChildren<BoosterButton>(true))
                if (booster.Kind == kind)
                    return booster.GetComponentInChildren<UnityEngine.UI.Button>(true);
            Assert.Fail("No booster " + kind);
            return null;
        }

        static int FigureIndex(LevelState state)
        {
            for (int i = 0; i < state.BottleCount; i++)
                if (state[i].Recipe != null)
                    return i;
            Assert.Fail("No rescue figure in " + state);
            return -1;
        }

        static BottleView ViewAt(GameScreen game, int index) => ((BottleViewAdapter)game.Field.SlotAt(index).View).View;

        static List<Move> Solution(int level)
        {
            var task = LevelLoader.Get(level);
            Assert.IsTrue(task.IsCompleted, "level is loaded");
            return task.Result.Solution;
        }

        static IEnumerator Play(GameScreen game, Move move)
        {
            yield return WaitUntil(() => !game.IsAnimating, 5f, "previous pour");
            int moves = game.Session.MovesMade;
            Tap(game, move.From);
            Tap(game, move.To);
            Assert.AreEqual(moves + 1, game.Session.MovesMade, $"move {move} is accepted");
        }

        static IEnumerator OpenLevel(int level, Action<ScreenRouter, GameScreen> found, float loadSeconds = 20f)
        {
            SceneManager.LoadScene(SceneLoader.GameScene);
            yield return null;
            ScreenRouter router = null;
            yield return WaitUntil(() => (router = FindRouter()) != null, 5f, "router");
            var game = router.Get<GameScreen>();
            yield return WaitUntil(() => router.Current == game && !game.IsBusy, 20f, "start level");
            router.PlayLevel(level);
            yield return null;
            yield return WaitUntil(() => router.Current == game && !game.IsBusy && game.Level == level, loadSeconds, "level " + level);
            found(router, game);
        }

        static ScreenRouter FindRouter()
        {
            var router = Object.FindFirstObjectByType<ScreenRouter>();
            return router != null && router.gameObject.scene == SceneManager.GetActiveScene() ? router : null;
        }

        static void Tap(GameScreen game, int index)
        {
            game.Field.SlotAt(index).OnPointerDown(new PointerEventData(EventSystem.current));
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

        // Overlay-канвас камера не рисует, поэтому на время съёмки он в Screen Space - Camera,
        // а камера рисует в текстуру размера телефона, как в PourAnimationTests.
        sealed class FrameCapture : IDisposable
        {
            readonly Camera camera = Camera.main;
            readonly Canvas canvas = GameObject.Find("Canvas").GetComponent<Canvas>();
            readonly RenderMode mode;
            readonly RenderTexture target;

            public FrameCapture(int width = 1080, int height = 2337)
            {
                mode = canvas.renderMode;
                target = new RenderTexture(width, height, 24);
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                camera.targetTexture = target;
            }

            public void Save(string name)
            {
                camera.Render();
                var previous = RenderTexture.active;
                RenderTexture.active = target;
                var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                texture.Apply();
                RenderTexture.active = previous;

                Directory.CreateDirectory(ShotsDir);
                File.WriteAllBytes(Path.Combine(ShotsDir, name + ".png"), texture.EncodeToPNG());
                Object.Destroy(texture);
            }

            public void Dispose()
            {
                camera.targetTexture = null;
                canvas.renderMode = mode;
                target.Release();
                Object.Destroy(target);
            }
        }
    }
}
