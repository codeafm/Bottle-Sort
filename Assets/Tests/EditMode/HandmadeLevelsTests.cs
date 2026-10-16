using System.Collections.Generic;
using BottleSort.Core;
using NUnit.Framework;

namespace BottleSort.Tests
{
    public class HandmadeLevelsTests
    {
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void Tutorial_HasDocumentedPar(int level)
        {
            var state = HandmadeLevels.Tutorial(level);
            Assert.IsTrue(state.IsWellFormed());

            var result = Solver.Solve(state);
            Assert.IsTrue(result.IsOptimal);
            Assert.AreEqual(HandmadeLevels.TutorialPar(level), result.Length);
        }

        [Test]
        public void Tutorial2_DocumentedPathWins()
        {
            // В документе бутылки нумеруются с единицы.
            int[,] path = { { 1, 4 }, { 2, 1 }, { 3, 2 }, { 3, 4 }, { 1, 3 }, { 2, 1 }, { 2, 4 } };
            var state = HandmadeLevels.Tutorial(2);
            for (int i = 0; i < path.GetLength(0); i++)
                state.Apply(Move.Pour(path[i, 0] - 1, path[i, 1] - 1));
            Assert.IsTrue(state.IsWon);
        }

        [Test]
        public void Tutorial3_TrapMoveIsRejected()
        {
            Assert.AreEqual(MoveError.TargetFull, HandmadeLevels.Tutorial(3).Check(Move.Pour(1, 0)));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void Override_TakesPrecedenceOverGenerator(int level)
        {
            var generated = LevelCatalog.Get(level, n => throw new AssertionException("Generator must not run for tutorials"));

            Assert.IsTrue(generated.IsHandmade);
            Assert.IsTrue(generated.IsTutorial);
            Assert.AreEqual(HandmadeLevels.TutorialPar(level), generated.Par);
            Assert.AreEqual(generated.Par, generated.Solution.Count);

            var state = generated.State.Clone();
            foreach (var move in generated.Solution)
                state.Apply(move);
            Assert.IsTrue(state.IsWon);
        }

        [Test]
        public void OtherLevels_ComeFromGenerator()
        {
            var level = LevelCatalog.Get(4, n => new LevelSpec { Number = n, Colors = 4 });
            Assert.IsFalse(level.IsHandmade);
            Assert.IsFalse(level.IsTutorial);
        }

        [Test]
        public void TutorialSession_AlwaysGivesThreeStarsAndNoExtraBottle()
        {
            var level = LevelCatalog.Get(3, null);
            var session = level.CreateSession();
            Assert.IsFalse(session.CanAddBottle);

            // Гоняем красный между пустыми: 31 ход при par 9 — обычно это одна звезда.
            Assert.AreEqual(MoveError.None, session.TryMove(Move.Pour(0, 4), out _));
            for (int i = 0; i < 15; i++)
            {
                Assert.AreEqual(MoveError.None, session.TryMove(Move.Pour(4, 5), out _));
                Assert.AreEqual(MoveError.None, session.TryMove(Move.Pour(5, 4), out _));
            }
            Assert.AreEqual(31, session.MovesMade);
            Assert.AreEqual(3, session.Stars);
        }

        [Test]
        public void Guide_Level1_LeadsStrictlyByScript()
        {
            var level = LevelCatalog.Get(1, null);
            var guide = HandmadeLevels.CreateGuide(level, level.CreateSession());
            Assert.IsTrue(guide.Strict);
            Assert.AreEqual(Move.Pour(0, 2), guide.Recommended);

            Assert.AreEqual(MoveError.NotRecommended, guide.TryMove(Move.Pour(0, 3), out _));
            Assert.IsTrue(guide.LastMoveWasMistake);
            Assert.AreEqual(0, guide.Session.MovesMade);

            Assert.AreEqual(MoveError.None, guide.TryMove(Move.Pour(0, 2), out _));
            Assert.IsFalse(guide.LastMoveWasMistake);
            Assert.IsTrue(guide.LastMoveFollowedHint);
            Assert.AreEqual(Move.Pour(1, 0), guide.Recommended);

            guide.TryMove(Move.Pour(1, 0), out var collected);
            Assert.IsTrue(collected.CompletedBottle);
            guide.TryMove(Move.Pour(1, 2), out var last);
            Assert.IsTrue(last.Won);
            Assert.IsNull(guide.Recommended);
            Assert.AreEqual(1, guide.Mistakes);
        }

        [Test]
        public void Guide_Level2_FollowsScriptThenSolver()
        {
            var level = LevelCatalog.Get(2, null);
            var guide = HandmadeLevels.CreateGuide(level, level.CreateSession());
            Assert.IsFalse(guide.Strict);
            Assert.AreEqual(Move.Pour(0, 3), guide.Recommended);

            guide.TryMove(Move.Pour(0, 4), out _);
            Assert.IsFalse(guide.OnScript);
            Assert.IsFalse(guide.LastMoveFollowedHint);

            var hint = guide.Recommended.Value;
            var after = guide.Session.State.Clone();
            int before = Solver.Solve(after).Length;
            after.Apply(hint);
            Assert.AreEqual(before - 1, Solver.Solve(after).Length, "Off script the hand shows an optimal move");

            guide.Undo();
            Assert.IsTrue(guide.OnScript);
            Assert.AreEqual(Move.Pour(0, 3), guide.Recommended);
        }

        [Test]
        public void Guide_Level3_FlagsTheTrapMove()
        {
            var level = LevelCatalog.Get(3, null);
            var guide = HandmadeLevels.CreateGuide(level, level.CreateSession());

            Assert.AreEqual(MoveError.TargetFull, guide.TryMove(Move.Pour(1, 0), out _));
            Assert.IsTrue(guide.LastMoveWasMistake);
            Assert.AreEqual(1, guide.MistakesInRow);
            Assert.IsTrue(guide.Recommended.HasValue);
            Assert.IsTrue(guide.Session.State.CanApply(guide.Recommended.Value));

            guide.TryMove(guide.Recommended.Value, out _);
            Assert.AreEqual(0, guide.MistakesInRow);
        }

        [Test]
        public void Guide_ReportMistake_CountsTapErrors()
        {
            var level = LevelCatalog.Get(3, null);
            var guide = HandmadeLevels.CreateGuide(level, level.CreateSession());
            guide.ReportMistake(MoveError.SourceEmpty);
            Assert.AreEqual(MoveError.SourceEmpty, guide.LastError);
            Assert.AreEqual(1, guide.Mistakes);
        }

        // В обучении нельзя застрять: из любой достижимой позиции есть выигрыш.
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void Tutorial_HasNoDeadEnds(int level)
        {
            var start = HandmadeLevels.Tutorial(level);
            var seen = new HashSet<string> { start.ToString() };
            var queue = new Queue<LevelState>();
            queue.Enqueue(start);
            var moves = new List<Move>();

            while (queue.Count > 0)
            {
                var state = queue.Dequeue();
                if (state.IsWon)
                    continue;
                Assert.IsFalse(Solver.IsDeadEnd(state), $"Dead end in tutorial {level}: {state}");

                state.GetLegalMoves(moves);
                foreach (var move in moves)
                {
                    var next = state.Clone();
                    next.Apply(move);
                    if (seen.Add(next.ToString()))
                        queue.Enqueue(next);
                }
            }
        }
    }
}
