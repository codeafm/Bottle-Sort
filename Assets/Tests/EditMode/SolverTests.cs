using System.Collections.Generic;
using System.Linq;
using BottleSort.Core;
using NUnit.Framework;

namespace BottleSort.Tests
{
    public class SolverTests
    {
        private static void AssertSolutionWins(LevelState start, SolveResult result)
        {
            var state = start.Clone();
            foreach (var move in result.Moves)
                state.Apply(move);
            Assert.IsTrue(state.IsWon, $"Solution {string.Join(", ", result.Moves)} doesn't win {start}");
        }

        // Обычный BFS без канонизации и эвристики — эталон для маленьких уровней.
        private static int BruteForceLength(LevelState start)
        {
            if (start.IsWon)
                return 0;
            var seen = new HashSet<string> { Key(start) };
            var frontier = new List<LevelState> { start };
            var moves = new List<Move>();
            for (int depth = 1; frontier.Count > 0; depth++)
            {
                var next = new List<LevelState>();
                foreach (var state in frontier)
                {
                    state.GetLegalMoves(moves);
                    foreach (var move in moves)
                    {
                        var child = state.Clone();
                        child.Apply(move);
                        if (child.IsWon)
                            return depth;
                        if (seen.Add(Key(child)))
                            next.Add(child);
                    }
                }
                frontier = next;
            }
            return -1;
        }

        private static string Key(LevelState state) =>
            string.Join("|", state.Bottles.Select(b => $"{b}:{b.FrozenTurns}:{b.IsLocked}:{b.FlipsLeft}"));

        [Test]
        public void WonPosition_NeedsNoMoves()
        {
            var result = Solver.Solve(LevelState.FromLayout(4, "RRRR BBBB -"));
            Assert.AreEqual(SolveStatus.Solved, result.Status);
            Assert.AreEqual(0, result.Length);
        }

        [Test]
        public void SimpleLevel_HasKnownOptimum()
        {
            var result = Solver.Solve(LevelState.FromLayout(4, "RRRB BBBR -"));
            Assert.IsTrue(result.IsSolved);
            Assert.IsTrue(result.IsOptimal);
            Assert.AreEqual(3, result.Length);
        }

        [Test]
        public void NoMovesAtAll_IsUnsolvable()
        {
            var result = Solver.Solve(LevelState.FromLayout(2, "RB BR"));
            Assert.AreEqual(SolveStatus.Unsolvable, result.Status);
            Assert.AreEqual(1, result.DeadEnds);
        }

        [Test]
        public void ClosedSearchSpace_IsUnsolvable()
        {
            // Одна пустая на четыре цвета: можно немного походить, но не выиграть.
            var state = LevelState.FromLayout(4, "RRRY YGBY GGBB GRBY -");
            Assert.AreEqual(-1, BruteForceLength(state));
            Assert.AreEqual(SolveStatus.Unsolvable, Solver.Solve(state).Status);
            Assert.IsTrue(Solver.IsDeadEnd(state));
        }

        [Test]
        public void MatchesBruteForceOnSmallLevels()
        {
            for (int i = 0; i < 25; i++)
            {
                int colors = 3 + i % 2;
                var spec = new LevelSpec { Number = 9000 + i, Colors = colors, Unit = 7 - colors, Candidates = 1 };
                var state = LevelGenerator.Generate(spec).State;

                var result = Solver.Solve(state);
                Assert.IsTrue(result.IsOptimal, state.ToString());
                Assert.AreEqual(BruteForceLength(state), result.Length, state.ToString());
                AssertSolutionWins(state, result);
            }
        }

        [Test]
        public void MatchesBruteForceWithIceAndLocks()
        {
            var state = LevelState.FromLayout(3, "RGB GBR BRG - -");
            state[3].FrozenTurns = 3;
            state[1].IsLocked = true;

            var result = Solver.Solve(state);
            Assert.AreEqual(BruteForceLength(state), result.IsSolved ? result.Length : -1);
            if (result.IsSolved)
                AssertSolutionWins(state, result);
        }

        [Test]
        public void IceCanForceWaitingMoves()
        {
            // Единственная общая пустая заморожена на 2 хода. Зелёные бутылки принимают только зелёный,
            // так что первые два хода — переливать зелёный туда-обратно, пока лёд не растает.
            var mixedA = Bottle2("RB");
            var mixedB = Bottle2("BR");
            var frozen = new Bottle(2) { FrozenTurns = 2 };
            var greenA = new Bottle(2, acceptColor: Palette.Green);
            greenA.Push(Palette.Green);
            var greenB = new Bottle(2, acceptColor: Palette.Green);
            greenB.Push(Palette.Green);
            var state = new LevelState(2, new[] { mixedA, mixedB, frozen, greenA, greenB });

            var result = Solver.Solve(state);
            Assert.IsTrue(result.IsSolved);
            Assert.AreEqual(5, result.Length);
            Assert.AreEqual(BruteForceLength(state), result.Length);
            AssertSolutionWins(state, result);
        }

        private static Bottle Bottle2(string layers)
        {
            var bottle = new Bottle(2);
            foreach (char c in layers)
                bottle.Push(Palette.FromLetter(c));
            return bottle;
        }

        [Test]
        public void BottleOrderDoesNotChangeTheAnswer()
        {
            var a = Solver.Solve(LevelState.FromLayout(4, "RGBB GRRB BGRG - -"));
            var b = Solver.Solve(LevelState.FromLayout(4, "- BGRG - GRRB RGBB"));
            Assert.AreEqual(a.Length, b.Length);
        }

        [Test]
        public void SmallBudget_GivesUnknownInsteadOfWrongAnswer()
        {
            var state = LevelGenerator.Generate(new LevelSpec { Number = 42, Colors = 8, Candidates = 1 }).State;
            var options = new SolverOptions { MaxStates = 50, AllowWeighted = false };

            var result = Solver.Solve(state, options);
            Assert.AreEqual(SolveStatus.Unknown, result.Status);
            Assert.IsFalse(Solver.IsDeadEnd(state, 50));
        }

        [Test]
        public void WeightedFallback_FindsSomeSolution()
        {
            var state = LevelGenerator.Generate(new LevelSpec { Number = 43, Colors = 8, Candidates = 1 }).State;
            var options = new SolverOptions { MaxStates = 200, WeightedMaxStates = 200_000 };

            var result = Solver.Solve(state, options);
            Assert.IsTrue(result.IsSolved);
            Assert.IsFalse(result.IsOptimal);
            Assert.GreaterOrEqual(result.Length, Solver.Solve(state).Length);
            AssertSolutionWins(state, result);
        }

        [Test]
        public void LowerBound_NeverExceedsOptimum()
        {
            for (int i = 0; i < 20; i++)
            {
                var state = LevelGenerator.Generate(new LevelSpec { Number = 700 + i, Colors = 6, Candidates = 1 }).State;
                var result = Solver.Solve(state);
                Assert.LessOrEqual(Solver.LowerBound(state), result.Length);
                Assert.AreEqual(Solver.LowerBound(state), result.LowerBound);
            }
        }

        [Test]
        public void HiddenLayersDoNotChangeTheSolution()
        {
            var open = LevelState.FromLayout(4, "RGBB GRRB BGRG - -");
            var hidden = open.Clone();
            foreach (var bottle in hidden.Bottles)
                bottle.HiddenCount = System.Math.Max(bottle.Count - 1, 0);

            Assert.AreEqual(Solver.Solve(open).Length, Solver.Solve(hidden).Length);
        }

        [Test]
        public void HourglassFlip_SavesLevelsWithOneEmptyBottle()
        {
            int rescued = 0;
            for (int i = 0; i < 20; i++)
            {
                var spec = new LevelSpec { Number = 601 + i, Colors = 4, EmptyBottles = 1, HourglassFlips = 1, Candidates = 16 };
                var level = LevelGenerator.Generate(spec);

                var noFlip = level.State.Clone();
                foreach (var bottle in noFlip.Bottles)
                    bottle.FlipsLeft = 0;
                if (Solver.Solve(noFlip).Status != SolveStatus.Unsolvable)
                    continue;

                rescued++;
                Assert.IsTrue(level.Solution.Any(m => m.Kind == MoveKind.Flip), level.State.ToString());
                AssertSolutionWins(level.State, Solver.Solve(level.State));
            }
            Assert.Greater(rescued, 0);
        }

        [Test]
        public void Hint_ReturnsNothingInDeadEnd()
        {
            Assert.IsNull(Solver.Hint(LevelState.FromLayout(4, "RRRY YGBY GGBB GRBY -")));
        }
    }
}
