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
