using System.Linq;
using BottleSort.Core;
using NUnit.Framework;

namespace BottleSort.Tests
{
    // Params(L) без генерации: это быстро, поэтому проверяем все 2000 номеров.
    public class LevelParamsTests
    {
        const int LastLevel = 2000;

        [Test]
        public void Type_FollowsSchedule()
        {
            for (int level = 4; level <= LastLevel; level++)
                Assert.AreEqual(LevelSchedule.TypeOf(level), LevelParams.SpecFor(level).Type, $"L{level}");
        }

        [Test]
        public void NoFeatureBeforeItsIntroduction()
        {
            for (int level = 4; level <= LastLevel; level++)
            {
                foreach (var feature in LevelParams.FeaturesOf(LevelParams.SpecFor(level)))
                    if (LevelParams.IntroLevel.TryGetValue(feature, out int intro))
                        Assert.GreaterOrEqual(level, intro, $"L{level} has {feature}");
            }
        }

        [Test]
        public void IntroLevels_CarryTheirFeature()
        {
            foreach (var pair in LevelParams.IntroLevel)
            {
                var spec = LevelParams.SpecFor(pair.Value);
                Assert.AreEqual(pair.Key, spec.Introduces, $"L{pair.Value}");
                if (pair.Key != LevelParams.Combo)
                    CollectionAssert.Contains(LevelParams.FeaturesOf(spec), pair.Key, $"L{pair.Value}");
            }
        }

        [Test]
        public void EverythingFitsOnScreen()
        {
            for (int level = 4; level <= LastLevel; level++)
            {
                var spec = LevelParams.SpecFor(level);
                int bottles = spec.Colors + spec.EmptyBottles + (spec.BigFlask ? 1 : 0) + (spec.HasRescue ? 1 : 0);
                Assert.LessOrEqual(bottles, LevelParams.MaxBottlesAtStart, $"L{level}");
                Assert.LessOrEqual(spec.Colors, spec.PaletteSize, $"L{level}");
            }
        }

        [Test]
        public void FiveLayers_OnlyWherePlanned()
        {
            for (int level = 4; level <= LastLevel; level++)
            {
                var spec = LevelParams.SpecFor(level);
                if (spec.Unit != 5)
                    continue;
                bool allowed = spec.Introduces == LevelParams.FiveLayers
                    || (spec.Type == LevelType.Special && level >= 351)
                    || (spec.Type == LevelType.BigFlask && level >= 451)
                    || ((spec.Type == LevelType.Boss || spec.Type == LevelType.ChapterBoss) && level >= 500);
                Assert.IsTrue(allowed, $"L{level} {spec.Type}");
            }
        }

        [Test]
        public void MoveLimit_OnlyOnBossesAndItsIntro()
        {
            for (int level = 4; level <= LastLevel; level++)
            {
                var spec = LevelParams.SpecFor(level);
                if (!spec.HasMoveLimit)
                    continue;
                Assert.IsTrue(level == 501 || (spec.Type == LevelType.Boss && level % 100 == 75), $"L{level}");
            }
        }

        [Test]
        public void ColorsGrowAcrossTheGame()
        {
            double early = Enumerable.Range(4, 100).Average(l => LevelParams.SpecFor(l).Colors);
            double late = Enumerable.Range(1801, 199).Average(l => LevelParams.SpecFor(l).Colors);
            Assert.Less(early, late);
        }

        [Test]
        public void SpecIsStable()
        {
            foreach (int level in new[] { 77, 650, 1234, 1999 })
                Assert.AreEqual(Describe(LevelParams.SpecFor(level)), Describe(LevelParams.SpecFor(level)));
        }

        private static string Describe(LevelSpec spec) =>
            $"{spec.Type} {spec.Colors} {spec.Unit} {spec.EmptyBottles} {spec.HiddenLayers} {string.Join(",", LevelParams.FeaturesOf(spec))} " +
            string.Join(",", spec.ShapePool);
    }
}
