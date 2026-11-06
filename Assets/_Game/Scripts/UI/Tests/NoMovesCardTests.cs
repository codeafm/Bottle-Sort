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
    // Карточка «Ходов нет» выходит только когда перелить действительно нечего (решение заказчика),
    // а не когда солвер доказал, что уровень уже не решить.
    public sealed class NoMovesCardTests
    {
        string _backup;

        [SetUp]
        public void SaveProgress()
        {
            _backup = File.Exists(PlayerData.SavePath) ? File.ReadAllText(PlayerData.SavePath) : null;
            PlayerData.DeleteSave();
            var data = PlayerData.Current;
            data.highestUnlocked = 100;
            data.tutorialGiftTaken = true;
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

        // Единственный ход 0 → 2 оставляет жёлтый сверху, а все остальные бутылки полные: переливать некуда.
        static LevelState OneMoveToStuck()
        {
            Bottle Full(string layers)
            {
                var bottle = new Bottle(2);
                foreach (char c in layers)
                    bottle.Push(Palette.FromLetter(c));
                return bottle;
            }
            return new LevelState(2, new[] { Full("YG"), Full("RB"), Full("G") });
        }

        static void PutLevel(int number, LevelState state, bool bigFlask = false)
        {
            LevelLoader.Put(number, new GeneratedLevel
            {
                Spec = new LevelSpec { Number = number, Unit = state.Unit, Colors = state.CountColors(), BigFlask = bigFlask },
                State = state,
                Par = 1,
                ParExact = true,
                Solution = new List<Move>(),
                PassedAllFilters = true
            });
        }

        [UnityTest]
        public IEnumerator NoPours_CardAddsABottleFromStock()
        {
            const int number = 4101;
            var data = PlayerData.Current;
            data.extraBottleCount = 1;
            data.Save();
            PutLevel(number, OneMoveToStuck());

            ScreenRouter router = null;
            GameScreen game = null;
            yield return OpenLevel(number, (r, g) => { router = r; game = g; });

            Tap(game, 0);
            Tap(game, 2);
            Assert.AreEqual(1, game.Session.MovesMade);
            var card = router.Get<RewardPopup>();
            yield return WaitUntil(() => card.IsOpen, 8f, "no-moves card");
            Assert.IsFalse(VideoIcon(card).activeSelf, "a bottle from stock needs no ad");

            Button(card, "Claim").onClick.Invoke();
            yield return null;
            Assert.AreEqual(4, game.Session.State.BottleCount, "the stored bottle is on the field");
            Assert.AreEqual(0, PlayerData.Current.extraBottleCount);
            Assert.IsTrue(game.Session.State.CanApply(Move.Pour(0, 3)), "the game goes on");
        }

        [UnityTest]
        public IEnumerator NoPours_WithoutExtraBottle_CardUndoesTheMove()
        {
            const int number = 4102;
            var data = PlayerData.Current;
            data.undoCount = 1;
            data.Save();
            PutLevel(number, OneMoveToStuck(), bigFlask: true);

            ScreenRouter router = null;
            GameScreen game = null;
            yield return OpenLevel(number, (r, g) => { router = r; game = g; });

            Tap(game, 0);
            Tap(game, 2);
            var card = router.Get<RewardPopup>();
            yield return WaitUntil(() => card.IsOpen, 8f, "no-moves card");
            Assert.IsFalse(VideoIcon(card).activeSelf, "the undo comes from stock");

            Button(card, "Claim").onClick.Invoke();
            yield return null;
            Assert.AreEqual(0, game.Session.MovesMade, "the move is undone");
            Assert.AreEqual(0, PlayerData.Current.undoCount);
        }

        // Позиция уже не решается (доказано солвером), но переливать есть куда: карточки нет,
        // подсказка не пишет «Ходов нет», а показывает какой-нибудь разумный ход.
        [UnityTest]
        public IEnumerator UnsolvableButMovable_NoCardAndHintStillShowsAMove()
        {
            const int number = 4103;
            var data = PlayerData.Current;
            data.hintCount = 1;
            data.Save();
            var state = LevelState.FromLayout(4, "RRRY YGBY GGBB GRBY -");
            Assert.IsTrue(Solver.IsDeadEnd(state));
            PutLevel(number, state);

            ScreenRouter router = null;
            GameScreen game = null;
            yield return OpenLevel(number, (r, g) => { router = r; game = g; });

            Tap(game, 0);
            Tap(game, 4);
            Assert.AreEqual(1, game.Session.MovesMade);
            yield return WaitUntil(() => !game.IsAnimating, 6f, "pour");
            yield return new WaitForSecondsRealtime(1.5f);
            var card = router.Get<RewardPopup>();
            Assert.IsFalse(card.IsOpen, "moves are left, so no card");

            FindBooster(game, Booster.Hint).onClick.Invoke();
            yield return WaitUntil(() => game.Selected >= 0, 10f, "hint");
            Assert.AreEqual(0, PlayerData.Current.hintCount);
            Assert.IsFalse(card.IsOpen);
        }

        static GameObject VideoIcon(RewardPopup card)
        {
            var icon = Button(card, "Claim").transform.Find("Face/VideoIcon");
            Assert.IsNotNull(icon, "Claim/Face/VideoIcon");
            return icon.gameObject;
        }

        static Button Button(RewardPopup card, string name)
        {
            foreach (var button in card.GetComponentsInChildren<Button>(true))
                if (button.name == name)
                    return button;
            Assert.Fail("No button " + name);
            return null;
        }

        static Button FindBooster(GameScreen game, Booster kind)
        {
            foreach (var booster in game.GetComponentsInChildren<BoosterButton>(true))
                if (booster.Kind == kind)
                    return booster.GetComponentInChildren<Button>(true);
            Assert.Fail("No booster " + kind);
            return null;
        }

        static IEnumerator OpenLevel(int level, Action<ScreenRouter, GameScreen> found)
        {
            SceneManager.LoadScene(SceneLoader.GameScene);
            yield return null;
            ScreenRouter router = null;
            yield return WaitUntil(() => (router = FindRouter()) != null, 5f, "router");
            var game = router.Get<GameScreen>();
            yield return WaitUntil(() => router.Current == game && !game.IsBusy, 20f, "start level");
            router.PlayLevel(level);
            yield return null;
            yield return WaitUntil(() => router.Current == game && !game.IsBusy && game.Level == level, 20f, "level " + level);
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
    }
}
