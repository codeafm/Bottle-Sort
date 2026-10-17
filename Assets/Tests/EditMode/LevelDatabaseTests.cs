using System.Collections.Generic;
using System.IO;
using System.Linq;
using BottleSort.Core;
using NUnit.Framework;

namespace BottleSort.Tests
{
    // Готовая база уровней 1–2000 (Assets/_Game/Levels/Resources/LevelDatabase.txt).
    // Быстрые тесты смотрят выборку, длинные (категория Long, Explicit) — все уровни.
    public class LevelDatabaseTests
    {
        const string DatabasePath = "Assets/_Game/Levels/Resources/LevelDatabase.txt";

        private static LevelDatabase _database;

        private static LevelDatabase Database =>
            _database ?? (_database = LevelDatabase.Parse(File.ReadAllText(DatabasePath)));

        private static void AssertPlayable(GeneratedLevel level)
        {
            int number = level.Spec.Number;
            var state = level.State;
            Assert.IsTrue(state.IsWellFormed(), $"L{number}");
            Assert.LessOrEqual(state.BottleCount, LevelParams.MaxBottlesAtStart, $"L{number}");
            Assert.AreEqual(level.Par, level.Solution.Count, $"L{number}");

            var replay = state.Clone();
            foreach (var move in level.Solution)
                replay.Apply(move);
            Assert.IsTrue(replay.IsWon, $"L{number}: stored solution doesn't win");
        }

        [Test]
        public void HasAllLevelsOfCurrentVersion()
        {
            Assert.AreEqual(2000, Database.Count);
            Assert.AreEqual(LevelGenerator.CurrentGenVersion, Database.GenVersion);
            for (int level = 1; level <= 2000; level++)
                Assert.IsTrue(Database.Contains(level), $"L{level}");
        }

        [Test]
        public void SampleLevels_ArePlayable()
        {
            var sample = new HashSet<int> { 1, 2, 3, 4, 25, 45, 78, 101, 151, 201, 251, 351, 401, 501, 601, 701, 2000 };
            for (int level = 7; level <= 2000; level += 37)
                sample.Add(level);

            foreach (int number in sample)
            {
                var level = Database.Get(number);
                AssertPlayable(level);
                var expected = HandmadeLevels.IsTutorial(number) ? LevelType.Normal : LevelSchedule.TypeOf(number);
                Assert.AreEqual(expected, level.Spec.Type, $"L{number}");
            }
        }

        [Test]
        public void SolverAgreesWithStoredPar()
        {
            foreach (int number in new[] { 5, 12, 48, 133, 260 })
            {
                var level = Database.Get(number);
                var solved = Solver.Solve(level.State);
                Assert.IsTrue(solved.IsSolved, $"L{number}");
                if (level.ParExact && solved.IsOptimal)
                    Assert.AreEqual(level.Par, solved.Length, $"L{number}");
            }
        }

        [Test]
        public void EncodeDecode_RoundTrips()
        {
            foreach (int number in new[] { 3, 45, 78, 328, 501, 650, 1500 })
            {
                var level = Database.Get(number);
                var again = LevelDatabase.Decode(LevelDatabase.Encode(level));
                Assert.IsTrue(level.State.StateEquals(again.State), $"L{number}");
                CollectionAssert.AreEqual(level.Solution, again.Solution);
            }
        }

        [Test]
        public void Generator_RebuildsTheSameLevel()
        {
            foreach (int number in new[] { 4, 9 })
                Assert.AreEqual(LevelDatabase.Encode(Database.Get(number)), LevelDatabase.Encode(LevelAudit.BuildOne(number)), $"L{number}");
        }

        [Test]
        public void EndlessLevels_AreBuiltOnTheFly()
        {
            foreach (int number in new[] { 2001, 2010, 2028 })
            {
                Assert.IsNull(Database.Get(number));
                AssertPlayable(LevelGenerator.Generate(LevelParams.SpecFor(number)));
            }
        }

        [Test, Explicit("Все 2000 уровней, несколько минут"), Category("Long")]
        public void AllLevels_PassTheAudit()
        {
            var levels = Database.Levels.OrderBy(n => n).Select(n => Database.Get(n)).ToList();
            foreach (var level in levels)
                AssertPlayable(level);

            var violations = LevelAudit.Validate(levels, determinismEvery: 0).Where(i => !i.Warning).ToList();
            Assert.IsEmpty(violations, string.Join("\n", violations.Take(30)));
        }

        [Test, Explicit("Пересборка каждого 20-го уровня, долго"), Category("Long")]
        public void Generator_RebuildsEveryTwentiethLevel()
        {
            for (int number = 20; number <= 2000; number += 20)
                Assert.AreEqual(LevelDatabase.Encode(Database.Get(number)), LevelDatabase.Encode(LevelAudit.BuildOne(number)), $"L{number}");
        }
    }
}
