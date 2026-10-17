using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using BottleSort.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BottleSort.UI.Tests
{
    // Переливание на настоящем игровом поле: анимация, блокировка занятых бутылок, отмена посреди налива,
    // победа только после последней анимации. Кадры налива — в Logs/PourFrames.
    public sealed class PourAnimationTests
    {
        const string FramesDir = "Logs/PourFrames";

        string _backup;

        [SetUp]
        public void SaveProgress()
        {
            _backup = File.Exists(PlayerData.SavePath) ? File.ReadAllText(PlayerData.SavePath) : null;
            PlayerData.DeleteSave();
            var data = PlayerData.Current;
            data.highestUnlocked = 46;
            data.tutorialGiftTaken = true;
            data.undoCount = 5;
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
        public IEnumerator TutorialPour_AnimatesAndWinsAfterLastPour()
        {
            GameScreen game = null;
            ScreenRouter router = null;
            yield return OpenLevel(1, (r, g) => { router = r; game = g; });
            yield return new WaitForSecondsRealtime(1f);

            // Режим съёмки включаем до хода: он меняет раскладку канваса, а бутылка в полёте её не догоняет.
            var capture = new FrameCapture();
            yield return new WaitForSecondsRealtime(0.4f);

            Tap(game, 0);
            yield return new WaitForSecondsRealtime(0.3f);
            Tap(game, 2);
            Assert.IsTrue(game.IsAnimating, "pour must be animated");
            float start = Time.realtimeSinceStartup;

            // Пока бутылка летит, её нельзя взять снова.
            Tap(game, 0);
            Assert.AreEqual(-1, game.Selected);

            // Фазы по таймингам макета: подъём, перелёт, наклон, струя, возврат.
            var frames = new[] { ("lift", 0.08f), ("move", 0.35f), ("tilt", 0.62f), ("pour", 0.95f), ("pour_late", 1.3f), ("back", 1.75f) };
            foreach (var (name, at) in frames)
            {
                while (Time.realtimeSinceStartup - start < at)
                    yield return null;
                capture.Save("pour_" + name);
            }
            capture.Dispose();

            yield return WaitUntil(() => !game.IsAnimating, 4f, "first pour");
            AssertViewsMatchState(game);

            Tap(game, 1);
            Tap(game, 0);
            yield return WaitUntil(() => !game.IsAnimating, 4f, "second pour");
            Tap(game, 1);
            Tap(game, 2);
            Assert.IsTrue(game.Session.IsWon);

            var win = router.Get<WinScreen>();
            while (game.IsAnimating)
            {
                Assert.AreNotEqual(win, router.Current, "win must wait for the last pour");
                yield return null;
            }
            yield return WaitUntil(() => router.Current == win, 4f, "win screen");
        }

        [UnityTest]
        public IEnumerator ParallelPoursAndUndoMidPour()
        {
            GameScreen game = null;
            yield return OpenLevel(24, (r, g) => game = g);
            yield return new WaitForSecondsRealtime(0.5f);

            var first = FirstPour(game.Session.State, new HashSet<int>());
            Tap(game, first.From);
            Tap(game, first.To);
            Assert.IsTrue(game.IsAnimating);
            Assert.AreEqual(1, game.Session.MovesMade);

            // Другие бутылки можно брать, пока первая льёт.
            var second = FirstPour(game.Session.State, new HashSet<int> { first.From, first.To });
            Tap(game, second.From);
            Tap(game, second.To);
            Assert.AreEqual(2, game.Session.MovesMade, "independent pour must start in parallel");
            yield return new WaitForSecondsRealtime(0.8f);

            var undo = FindBooster(game, Booster.Undo);
            undo.onClick.Invoke();
            yield return null;

            Assert.IsFalse(game.IsAnimating, "undo finishes running pours instantly");
            Assert.AreEqual(1, game.Session.MovesMade);
            AssertViewsMatchState(game);
        }

        static IEnumerator OpenLevel(int level, Action<ScreenRouter, GameScreen> found)
        {
            SceneManager.LoadScene("Main");
            yield return null;
            ScreenRouter router = null;
            yield return WaitUntil(() => (router = FindRouter()) != null, 5f, "router");
            var game = router.Get<GameScreen>();
            router.PlayLevel(level);
            yield return WaitUntil(() => router.Current == game && !game.IsBusy, 20f, "level " + level);
            found(router, game);
        }

        static Move FirstPour(LevelState state, HashSet<int> busy)
        {
            var moves = new List<Move>();
            state.GetLegalMoves(moves);
            foreach (var move in moves)
                if (move.Kind == MoveKind.Pour && !busy.Contains(move.From) && !busy.Contains(move.To))
                    return move;
            Assert.Fail("No free pour in " + state);
            return default;
        }

        static void AssertViewsMatchState(GameScreen game)
        {
            var state = game.Session.State;
            for (int i = 0; i < state.BottleCount; i++)
            {
                var view = ((BottleViewAdapter)game.Field.SlotAt(i).View).View;
                Assert.AreEqual(state[i].Count, view.LayerCount, "layers of bottle " + i);
                Assert.AreEqual(Quaternion.identity, view.Body.localRotation, "bottle " + i + " is back upright");
            }
        }

        static Button FindBooster(GameScreen game, Booster kind)
        {
            foreach (var booster in game.GetComponentsInChildren<BoosterButton>(true))
                if (booster.Kind == kind)
                    return booster.GetComponentInChildren<Button>(true);
            Assert.Fail("No booster " + kind);
            return null;
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

        // Как в UiFlowTests: overlay-канвас камера не рисует, поэтому на время съёмки
        // переводим его в Screen Space - Camera и рендерим камеру в текстуру размера телефона.
        sealed class FrameCapture : IDisposable
        {
            readonly Camera camera = Camera.main;
            readonly Canvas canvas = GameObject.Find("Canvas").GetComponent<Canvas>();
            readonly RenderMode mode;
            readonly RenderTexture target;

            public FrameCapture(int width = 1080, int height = 2340)
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

                Directory.CreateDirectory(FramesDir);
                File.WriteAllBytes(Path.Combine(FramesDir, name + ".png"), texture.EncodeToPNG());
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
