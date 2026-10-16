using System.Collections.Generic;
using System.Linq;
using BottleSort.Core;
using NUnit.Framework;

namespace BottleSort.Tests
{
    public class GeneratorTests
    {
        // Примерно как в первых главах по LEVEL_DESIGN.md: 4–9 цветов, фишки через раз.
        private static LevelSpec SpecFor(int level)
        {
            var spec = new LevelSpec { Number = level, Colors = 4 + level % 6 };
            switch (level % 7)
            {
                case 1:
                    spec.HiddenLayers = 1 + level % 2;
                    break;
                case 2:
                    spec.Ice.Add(new IceFeature { Placement = FeaturePlacement.Full, Turns = 3 });
                    break;
                case 3:
                    spec.Locks.Add(new LockFeature { Placement = FeaturePlacement.Full });
                    break;
                case 4:
                    spec.Tints.Add(new TintFeature { Placement = FeaturePlacement.Empty });
                    spec.EmptyBottles = 3;
                    break;
                case 5:
                    spec.BigFlask = true;
                    spec.EmptyBottles = 1;
                    break;
                case 6:
                    spec.Unit = 5;
                    spec.Colors = 4 + level % 4;
                    break;
            }
            return spec;
        }

        [Test]
        public void TwoHundredLevelsInARow_AreSolvable()
        {
            for (int level = 1; level <= 200; level++)
            {
                var generated = LevelGenerator.Generate(SpecFor(level));
                var state = generated.State;

                Assert.IsTrue(state.IsWellFormed(), $"L{level}: {state}");
                Assert.AreEqual(generated.Par, generated.Solution.Count, $"L{level}");
                Assert.Greater(generated.Par, 0, $"L{level}");

                var replay = state.Clone();
                foreach (var move in generated.Solution)
                    replay.Apply(move);
                Assert.IsTrue(replay.IsWon, $"L{level}: solution doesn't win {state}");
            }
        }

        [Test]
        public void SameSeed_GivesSameLevel()
        {
            for (int level = 1; level <= 30; level++)
            {
                var first = LevelGenerator.Generate(SpecFor(level));
                var second = LevelGenerator.Generate(SpecFor(level));

                Assert.IsTrue(first.State.StateEquals(second.State), $"L{level}");
                Assert.AreEqual(first.Candidate, second.Candidate);
                CollectionAssert.AreEqual(first.Solution, second.Solution);
            }
        }

        [Test]
        public void DifferentLevels_GetDifferentLayouts()
        {
            var layouts = new HashSet<string>();
            for (int level = 1; level <= 50; level++)
                layouts.Add(LevelGenerator.Generate(new LevelSpec { Number = level, Colors = 6 }).State.ToString());
            Assert.AreEqual(50, layouts.Count);
        }

        [Test]
        public void GenVersion_ChangesLayouts()
        {
            var v1 = LevelGenerator.Generate(new LevelSpec { Number = 10, Colors = 6, GenVersion = 1 });
            var v2 = LevelGenerator.Generate(new LevelSpec { Number = 10, Colors = 6, GenVersion = 2 });
            Assert.IsFalse(v1.State.StateEquals(v2.State));
        }

        [Test]
        public void StartPosition_HasNoCollectedBottles()
        {
            for (int level = 1; level <= 40; level++)
            {
                var generated = LevelGenerator.Generate(SpecFor(level));
                Assert.IsFalse(generated.State.Bottles.Any(b => b.IsCompleted(generated.State.Unit)), $"L{level}");
            }
        }

        [Test]
        public void SmallLevels_AvoidLookalikeColors()
        {
            for (int level = 1; level <= 40; level++)
            {
                var state = LevelGenerator.Generate(new LevelSpec { Number = level, Colors = 5 }).State;
                var colors = state.Bottles.SelectMany(b => Enumerable.Range(0, b.Count).Select(i => (int)b[i])).Distinct().ToList();

                Assert.AreEqual(5, colors.Count);
                foreach (int a in colors)
                    foreach (int b in colors)
                        Assert.IsFalse(Palette.AreLookalikes(a, b), $"L{level}: {state}");
            }
        }

        [Test]
        public void BigFlask_IsSecondAndHoldsTwoUnits()
        {
            var state = LevelGenerator.Generate(new LevelSpec { Number = 45, Colors = 6, EmptyBottles = 1, BigFlask = true }).State;

            Assert.AreEqual(8, state[LevelGenerator.BigFlaskIndex].Capacity);
            Assert.AreEqual(LevelGenerator.FlaskShape, state[LevelGenerator.BigFlaskIndex].Shape);
            Assert.IsTrue(state[LevelGenerator.BigFlaskIndex].IsEmpty);
            Assert.AreEqual(8, state.BottleCount);
        }

        [Test]
        public void BigFlaskLevel_DisablesExtraBottle()
        {
            var level = LevelGenerator.Generate(new LevelSpec { Number = 45, Colors = 6, EmptyBottles = 1, BigFlask = true });
            Assert.IsFalse(level.CreateSession().CanAddBottle);
        }

        [Test]
        public void Features_LandWhereAsked()
        {
            var spec = new LevelSpec
            {
                Number = 1500,
                Colors = 8,
                Ice = { new IceFeature { Placement = FeaturePlacement.Empty, Turns = 4 } },
                Locks = { new LockFeature { Placement = FeaturePlacement.Full, Colored = true } },
                HiddenLayers = 2,
                HiddenBottles = 3
            };
            var state = LevelGenerator.Generate(spec).State;

            var frozen = state.Bottles.Single(b => b.IsFrozen);
            Assert.AreEqual(4, frozen.FrozenTurns);
            Assert.IsTrue(frozen.IsEmpty);

            var locked = state.Bottles.Single(b => b.IsLocked);
            Assert.IsFalse(locked.IsEmpty);
            Assert.AreNotEqual(Palette.AnyColor, locked.LockColor);

            Assert.AreEqual(3, state.Bottles.Count(b => b.HiddenCount == 2));
        }

        [Test]
        public void Tint_OnFullBottleTakesBottomColor()
        {
            var spec = new LevelSpec { Number = 900, Colors = 6, Tints = { new TintFeature { Placement = FeaturePlacement.Full } } };
            var state = LevelGenerator.Generate(spec).State;

            var tinted = state.Bottles.Single(b => b.AcceptColor != Palette.AnyColor);
            Assert.AreEqual(tinted[0], tinted.AcceptColor);
        }

        [Test]
        public void MoveLimit_FollowsFormula()
        {
            var spec = new LevelSpec { Number = 501, Colors = 6, MoveLimitSlack = 0.6, Analyzer = new AnalyzerOptions { Runs = 30 } };
            var level = LevelGenerator.Generate(spec);

            int expected = level.Par + System.Math.Max(8, (int)System.Math.Ceiling(0.6 * level.Par));
            Assert.AreEqual(expected, level.MoveLimit);
            Assert.AreEqual(level.MoveLimit, level.CreateSession().MoveLimit);
        }

        [Test]
        public void TargetDifficulty_PicksClosestCandidate()
        {
            var spec = new LevelSpec
            {
                Number = 300,
                Colors = 7,
                Candidates = 6,
                TargetDifficulty = 50,
                Analyzer = new AnalyzerOptions { Runs = 30, DeadSamples = 10 }
            };
            var level = LevelGenerator.Generate(spec);

            Assert.IsNotNull(level.Metrics);
            Assert.That(level.Metrics.Difficulty, Is.InRange(0.0, 100.0));
        }

        [Test]
        public void TooManyFeatures_AreRejected()
        {
            var spec = new LevelSpec
            {
                Colors = 4,
                EmptyBottles = 1,
                Ice = { new IceFeature { Placement = FeaturePlacement.Empty } },
                Locks = { new LockFeature { Placement = FeaturePlacement.Empty } }
            };
            Assert.Throws<System.ArgumentException>(() => LevelGenerator.Generate(spec));
        }
    }

    public class RngTests
    {
        // Эталон снят один раз. Если тест упал — поменялся генератор, а с ним и все уровни.
        [Test]
        public void Sequence_IsStable()
        {
            var rng = new Rng(42);
            Assert.AreEqual(0x15780B2E0C2EC716UL, rng.NextULong());
            Assert.AreEqual(0x6104D9866D113A7EUL, rng.NextULong());
            Assert.AreEqual(0xAE17533239E499A1UL, rng.NextULong());
        }

        [Test]
        public void Seeds_AreStable()
        {
            Assert.AreEqual(0x2B150A1713F91D43UL, Seeds.FromText("bs"));
            Assert.AreEqual(0xB9E53388FA776B16UL, Seeds.ForLevel(1, 1));
            Assert.AreNotEqual(Seeds.ForLevel(1, 1), Seeds.ForLevel(1, 2));
            Assert.AreNotEqual(Seeds.ForLevel(1, 1), Seeds.ForLevel(2, 1));
        }

        [Test]
        public void NextInt_StaysInRangeAndCoversIt()
        {
            var rng = new Rng(7);
            var hits = new int[6];
            for (int i = 0; i < 6000; i++)
                hits[rng.NextInt(6)]++;
            foreach (int count in hits)
                Assert.That(count, Is.InRange(850, 1150));
        }

        [Test]
        public void Shuffle_KeepsElements()
        {
            var list = Enumerable.Range(0, 20).ToList();
            new Rng(3).Shuffle(list);
            CollectionAssert.AreEquivalent(Enumerable.Range(0, 20), list);
            CollectionAssert.AreNotEqual(Enumerable.Range(0, 20), list);
        }
    }

    public class MetricsTests
    {
        [Test]
        public void Spread_IsZeroForSortedAndOneForFullyMixed()
        {
            Assert.AreEqual(0.0, DifficultyAnalyzer.Spread(LevelState.FromLayout(4, "RRRR BBBB -")));
            Assert.AreEqual(1.0, DifficultyAnalyzer.Spread(LevelState.FromLayout(2, "RB BR")));
        }

        [Test]
        public void EasyLevel_IsWonByEveryAgent()
        {
            var state = LevelState.FromLayout(4, "RRRB BBBR - -");
            var metrics = DifficultyAnalyzer.Analyze(state, Solver.Solve(state), 0, new AnalyzerOptions { Runs = 20 });

            Assert.AreEqual(1.0, metrics.RandomWinRate);
            Assert.AreEqual(1.0, metrics.GreedyWinRate);
            Assert.AreEqual(3, metrics.Opt);
        }

        [Test]
        public void HarderLevel_ScoresHigher()
        {
            var options = new AnalyzerOptions { Runs = 40, DeadSamples = 10 };
            var easy = LevelGenerator.Generate(new LevelSpec { Number = 5, Colors = 3, Candidates = 1 }).State;
            var hard = LevelGenerator.Generate(new LevelSpec { Number = 5, Colors = 10, Candidates = 1 }).State;

            var easyScore = DifficultyAnalyzer.Analyze(easy, Solver.Solve(easy), 0, options).Difficulty;
            var hardScore = DifficultyAnalyzer.Analyze(hard, Solver.Solve(hard), 0, options).Difficulty;
            Assert.Less(easyScore, hardScore);
        }

        [Test]
        public void Analysis_IsDeterministic()
        {
            var state = LevelGenerator.Generate(new LevelSpec { Number = 77, Colors = 7 }).State;
            var options = new AnalyzerOptions { Runs = 30, DeadSamples = 10 };
            var a = DifficultyAnalyzer.Analyze(state, Solver.Solve(state), 0, options);
            var b = DifficultyAnalyzer.Analyze(state, Solver.Solve(state), 0, options);
            Assert.AreEqual(a.Difficulty, b.Difficulty);
        }
    }

    public class LevelScheduleTests
    {
        [TestCase(1, LevelType.Normal)]
        [TestCase(20, LevelType.Normal)]
        [TestCase(25, LevelType.Boss)]
        [TestCase(28, LevelType.Normal)]
        [TestCase(30, LevelType.Special)]
        [TestCase(35, LevelType.Normal)]
        [TestCase(45, LevelType.BigFlask)]
        [TestCase(50, LevelType.ChapterBoss)]
        [TestCase(78, LevelType.Rescue)]
        [TestCase(100, LevelType.ChapterBoss)]
        [TestCase(500, LevelType.ChapterBoss)]
        [TestCase(550, LevelType.Boss)]
        [TestCase(600, LevelType.ChapterBoss)]
        [TestCase(2000, LevelType.ChapterBoss)]
        public void TypeOf_FollowsRhythm(int level, LevelType expected)
        {
            Assert.AreEqual(expected, LevelSchedule.TypeOf(level));
        }

        [Test]
        public void TypeCounts_MatchLevelDesign()
        {
            var counts = Enumerable.Range(1, 2000).GroupBy(LevelSchedule.TypeOf).ToDictionary(g => g.Key, g => g.Count());
            Assert.AreEqual(1566, counts[LevelType.Normal]);
            Assert.AreEqual(158, counts[LevelType.Special]);
            Assert.AreEqual(157, counts[LevelType.BigFlask]);
            Assert.AreEqual(39, counts[LevelType.Rescue]);
            Assert.AreEqual(55, counts[LevelType.Boss]);
            Assert.AreEqual(25, counts[LevelType.ChapterBoss]);
        }

        [Test]
        public void EveryBlockOfFifty_HasEightHardAndTwoBosses()
        {
            for (int start = 1; start <= 2000; start += 50)
            {
                var tiers = Enumerable.Range(start, 50).Select(LevelSchedule.TierOf).ToList();
                Assert.AreEqual(8, tiers.Count(t => t == LevelTier.Hard));
                Assert.AreEqual(2, tiers.Count(t => t == LevelTier.Boss));
            }
        }

        [TestCase(1, 1)]
        [TestCase(50, 1)]
        [TestCase(51, 2)]
        [TestCase(500, 10)]
        [TestCase(501, 11)]
        [TestCase(2000, 25)]
        [TestCase(2001, 26)]
        public void ChapterOf_UsesShortThenLongChapters(int level, int chapter)
        {
            Assert.AreEqual(chapter, LevelSchedule.ChapterOf(level));
            Assert.LessOrEqual(LevelSchedule.ChapterStart(chapter), level);
        }
    }
}
