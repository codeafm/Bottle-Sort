using BottleSort.Core;
using NUnit.Framework;

namespace BottleSort.Tests
{
    public class SessionTests
    {
        private static LevelState WithFeatures()
        {
            var state = LevelState.FromLayout(4, "RGBB GRRB BGRG - -");
            state[0].HiddenCount = 2;
            state[2].FrozenTurns = 2;
            state[4].IsLocked = true;
            return state;
        }

        [Test]
        public void Undo_RestoresExactStateAfterEveryMove()
        {
            var session = new GameSession(WithFeatures(), par: 10);
            var snapshots = new System.Collections.Generic.List<LevelState> { session.State.Clone() };

            foreach (var move in new[] { Move.Pour(0, 3), Move.Pour(1, 3), Move.Pour(2, 0), Move.Pour(2, 1) })
            {
                Assert.AreEqual(MoveError.None, session.TryMove(move, out _), move.ToString());
                snapshots.Add(session.State.Clone());
            }

            for (int i = snapshots.Count - 1; i > 0; i--)
            {
                Assert.IsTrue(session.State.StateEquals(snapshots[i]));
                Assert.IsTrue(session.Undo());
                Assert.IsTrue(session.State.StateEquals(snapshots[i - 1]), $"Undo to step {i - 1}");
                Assert.AreEqual(i - 1, session.MovesMade);
            }

            Assert.IsFalse(session.Undo());
            Assert.AreEqual(2, session.State[0].HiddenCount);
            Assert.AreEqual(2, session.State[2].FrozenTurns);
            Assert.IsTrue(session.State[4].IsLocked);
        }

        [Test]
        public void Undo_BringsLockBack()
        {
            var state = LevelState.FromLayout(4, "RRR BGBG R -");
            state[1].IsLocked = true;
            var session = new GameSession(state, par: 5);

            session.TryMove(Move.Pour(2, 0), out var outcome);
            Assert.IsTrue(outcome.UnlockedAny);

            session.Undo();
            Assert.IsTrue(session.State[1].IsLocked);
        }

        [Test]
        public void IllegalMove_DoesNotTouchHistory()
        {
            var session = new GameSession(LevelState.FromLayout(4, "RGBY RGBY - -"), par: 5);
            Assert.AreEqual(MoveError.TargetFull, session.TryMove(Move.Pour(0, 1), out _));
            Assert.AreEqual(0, session.MovesMade);
            Assert.IsFalse(session.CanUndo);
        }

        [Test]
        public void ExtraBottle_SurvivesUndoButNotRestart()
        {
            var session = new GameSession(LevelState.FromLayout(4, "RGBB GRRB BGRG -"), par: 10);
            session.TryMove(Move.Pour(0, 3), out _);

            Assert.IsTrue(session.AddBottle());
            Assert.IsFalse(session.AddBottle(), "Only one extra bottle per attempt");
            session.TryMove(Move.Pour(1, 4), out _);

            session.Undo();
            Assert.AreEqual(5, session.State.BottleCount);
            Assert.IsTrue(session.State[4].IsEmpty);

            session.Undo();
            Assert.AreEqual(5, session.State.BottleCount);
            Assert.IsTrue(session.ExtraBottleUsed);

            session.Restart();
            Assert.AreEqual(4, session.State.BottleCount);
            Assert.IsFalse(session.ExtraBottleUsed);
            Assert.AreEqual(0, session.MovesMade);
        }

        [Test]
        public void ExtraBottle_IsOffOnBigFlaskLevels()
        {
            var session = new GameSession(LevelState.FromLayout(4, "RB BR -"), par: 3, extraBottleAllowed: false);
            Assert.IsFalse(session.AddBottle());
        }

        [Test]
        public void Restart_ReturnsToTheStart()
        {
            var start = WithFeatures();
            var session = new GameSession(start, par: 10);
            session.TryMove(Move.Pour(0, 3), out _);
            session.TryMove(Move.Pour(1, 3), out _);

            session.Restart();

            Assert.IsTrue(session.State.StateEquals(start));
            Assert.IsFalse(session.CanUndo);
        }

        [Test]
        public void MoveLimit_EndsTheAttempt()
        {
            var session = new GameSession(LevelState.FromLayout(4, "RRRB BBBR - -"), par: 3, moveLimit: 1);
            session.TryMove(Move.Pour(0, 2), out _);

            Assert.IsTrue(session.IsOutOfMoves);
            Assert.AreEqual(0, session.MovesLeft);
            Assert.AreEqual(MoveError.LevelOver, session.TryMove(Move.Pour(1, 3), out _));

            session.Undo();
            Assert.IsFalse(session.IsOutOfMoves);
        }

        [Test]
        public void Hint_IsFirstMoveOfOptimalSolution()
        {
            var state = LevelState.FromLayout(4, "RGBB GRRB BGRG - -");
            var session = new GameSession(state, par: 10);

            var hint = session.Hint();
            Assert.IsTrue(hint.HasValue);
            Assert.IsTrue(session.State.CanApply(hint.Value));

            var optimal = Solver.Solve(state).Length;
            var after = state.Clone();
            after.Apply(hint.Value);
            Assert.AreEqual(optimal - 1, Solver.Solve(after).Length);
        }

        [Test]
        public void Hint_IsEmptyWhenWon()
        {
            var session = new GameSession(LevelState.FromLayout(4, "RRRR - -"), par: 0);
            Assert.IsNull(session.Hint());
        }
    }

    public class StarRatingTests
    {
        [TestCase(8, 11, 16)]
        [TestCase(18, 23, 29)]
        [TestCase(25, 32, 40)]
        [TestCase(31, 39, 50)]
        [TestCase(50, 63, 80)]
        public void Limits_MatchLevelDesignTable(int par, int threeStars, int twoStars)
        {
            Assert.AreEqual(threeStars, StarRating.ThreeStarLimit(par));
            Assert.AreEqual(twoStars, StarRating.TwoStarLimit(par));

            Assert.AreEqual(3, StarRating.Calculate(threeStars, par, false));
            Assert.AreEqual(2, StarRating.Calculate(threeStars + 1, par, false));
            Assert.AreEqual(2, StarRating.Calculate(twoStars, par, false));
            Assert.AreEqual(1, StarRating.Calculate(twoStars + 1, par, false));
        }

        [Test]
        public void ExtraBottle_CapsAtTwoStars()
        {
            Assert.AreEqual(2, StarRating.Calculate(10, 10, usedExtraBottle: true));
            Assert.AreEqual(1, StarRating.Calculate(100, 10, usedExtraBottle: true));
        }
    }
}
