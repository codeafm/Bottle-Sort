using System;
using BottleSort.Core;
using BottleSort.Services;
using BottleSort.UI;
using NUnit.Framework;

namespace BottleSort.Tests
{
    public class EconomyTests
    {
        // PlayerData без файла: Save только оповещает, на диск ничего не пишется.
        static PlayerData Fresh() => new PlayerData();

        [TestCase(1, 15, 20, 25)]
        [TestCase(500, 15, 20, 25)]
        [TestCase(1000, 20, 25, 30)]
        [TestCase(2000, 25, 25, 35)]
        public void LevelReward_Normal_MatchesTable(int level, int one, int two, int three)
        {
            Assert.AreEqual(one, EconomyRules.LevelReward(LevelType.Normal, level, 1));
            Assert.AreEqual(two, EconomyRules.LevelReward(LevelType.Normal, level, 2));
            Assert.AreEqual(three, EconomyRules.LevelReward(LevelType.Normal, level, 3));
        }

        [TestCase(LevelType.Special, 1, 25, 30, 40)]
        [TestCase(LevelType.Rescue, 500, 40, 45, 60)]
        [TestCase(LevelType.Boss, 1000, 50, 60, 75)]
        [TestCase(LevelType.ChapterBoss, 2000, 115, 135, 170)]
        public void LevelReward_OtherTypes_MatchTable(LevelType type, int level, int one, int two, int three)
        {
            Assert.AreEqual(one, EconomyRules.LevelReward(type, level, 1));
            Assert.AreEqual(two, EconomyRules.LevelReward(type, level, 2));
            Assert.AreEqual(three, EconomyRules.LevelReward(type, level, 3));
        }

        [Test]
        public void Replay_PaysOnlyForNewStars()
        {
            Assert.AreEqual(25, EconomyRules.ReplayReward(LevelType.Normal, 10, 0, 3));
            Assert.AreEqual(5, EconomyRules.ReplayReward(LevelType.Normal, 10, 2, 3));
            Assert.AreEqual(0, EconomyRules.ReplayReward(LevelType.Normal, 10, 3, 3));
            Assert.AreEqual(0, EconomyRules.ReplayReward(LevelType.Normal, 10, 3, 1));
        }

        [Test]
        public void NewPlayer_StartsWithNothing()
        {
            var data = Fresh();
            Assert.AreEqual(0, data.coins);
            Assert.AreEqual(0, data.undoCount + data.hintCount + data.extraBottleCount);
            Assert.AreEqual(1, data.highestUnlocked);
        }

        [Test]
        public void TutorialGift_IsGivenOnce()
        {
            var data = Fresh();
            Assert.IsTrue(Economy.TryClaimTutorialGift(data));
            Assert.IsFalse(Economy.TryClaimTutorialGift(data));
            Assert.AreEqual(200, data.coins);
            Assert.AreEqual(5, data.undoCount);
            Assert.AreEqual(3, data.hintCount);
            Assert.AreEqual(1, data.extraBottleCount);
        }

        [Test]
        public void BuyBooster_SpendsCoinsAndAddsPack()
        {
            var data = Fresh();
            data.coins = 130;
            Assert.IsTrue(Economy.TryBuyBooster(data, BoosterKind.Undo));
            Assert.AreEqual(70, data.coins);
            Assert.AreEqual(3, data.undoCount);
            Assert.IsFalse(Economy.TryBuyBooster(data, BoosterKind.ExtraBottle));
            Assert.AreEqual(70, data.coins);
            Assert.AreEqual(0, data.extraBottleCount);
        }

        [Test]
        public void UseBooster_FailsWhenEmpty()
        {
            var data = Fresh();
            Assert.IsFalse(Economy.TryUseBooster(data, BoosterKind.Hint));
            data.hintCount = 1;
            Assert.IsTrue(Economy.TryUseBooster(data, BoosterKind.Hint));
            Assert.AreEqual(0, data.hintCount);
        }

        [Test]
        public void ChapterChest_OpensOncePerChapter()
        {
            var data = Fresh();
            data.RecordWin(50, 1);
            var first = Economy.CollectChests(data, 50, LevelType.ChapterBoss);
            Assert.AreEqual(1, first.Count);
            Assert.AreEqual(ChestKind.Chapter, first[0].Kind);
            Assert.AreEqual(110, data.coins);
            Assert.AreEqual(1, data.undoCount);
            Assert.AreEqual(1, data.hintCount);
            Assert.AreEqual(1, data.extraBottleCount);

            data.RecordWin(50, 3);
            Assert.AreEqual(0, Economy.CollectChests(data, 50, LevelType.ChapterBoss).Count);
        }

        [Test]
        public void StarChest_EveryThirtyStars()
        {
            var data = Fresh();
            for (int level = 1; level <= 9; level++)
                data.RecordWin(level, 3);
            Assert.AreEqual(0, Economy.CollectChests(data, 9, LevelType.Normal).Count);

            data.RecordWin(10, 3);
            var chests = Economy.CollectChests(data, 10, LevelType.Normal);
            Assert.AreEqual(1, chests.Count);
            Assert.AreEqual(ChestKind.Stars, chests[0].Kind);
            Assert.AreEqual(60, data.coins);
            Assert.AreEqual(1, data.undoCount + data.hintCount + data.extraBottleCount);

            Assert.AreEqual(0, Economy.CollectChests(data, 10, LevelType.Normal).Count);
        }

        [Test]
        public void StarChest_CatchesUpSeveralAtOnce()
        {
            var data = Fresh();
            for (int level = 1; level <= 20; level++)
                data.RecordWin(level, 3);
            Assert.AreEqual(2, Economy.CollectChests(data, 20, LevelType.Normal).Count);
            Assert.AreEqual(2, data.starChestsOpened);
        }

        [Test]
        public void DailyGift_OncePerDay()
        {
            var data = Fresh();
            var monday = new DateTime(2026, 10, 12, 9, 0, 0);
            Assert.IsTrue(DailyRewards.TryClaimGift(data, monday, out var reward));
            Assert.AreEqual(75, reward.Coins);
            Assert.IsFalse(DailyRewards.TryClaimGift(data, monday.AddHours(10), out _));
            Assert.IsTrue(DailyRewards.TryClaimGift(data, monday.AddDays(1), out _));
            Assert.AreEqual(150, data.coins);
            Assert.AreEqual(2, data.undoCount);
            Assert.AreEqual(2, data.hintCount);
        }

        [Test]
        public void Calendar_GrowsAndWrapsAfterSevenDays()
        {
            var data = Fresh();
            var day = new DateTime(2026, 10, 1);
            int[] expected = { 60, 80, 100, 120, 140, 160, 180, 60 };
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.IsTrue(DailyRewards.TryClaimCalendar(data, day.AddDays(i), out int coins), "day " + i);
                Assert.AreEqual(expected[i], coins);
                Assert.IsFalse(DailyRewards.TryClaimCalendar(data, day.AddDays(i), out _));
            }
            Assert.AreEqual(2, DailyRewards.CalendarDay(data));
        }

        [Test]
        public void Calendar_MissedDayKeepsStreak()
        {
            var data = Fresh();
            DailyRewards.TryClaimCalendar(data, new DateTime(2026, 10, 1), out _);
            DailyRewards.TryClaimCalendar(data, new DateTime(2026, 10, 5), out int coins);
            Assert.AreEqual(80, coins);
        }

        [Test]
        public void Wheel_OneFreeSpinPerDay_DoesNotStack()
        {
            var data = Fresh();
            var today = new DateTime(2026, 10, 17);
            DailyRewards.RefreshWheel(data, today);
            Assert.AreEqual(1, data.wheelSpins);
            DailyRewards.RefreshWheel(data, today.AddDays(1));
            DailyRewards.RefreshWheel(data, today.AddDays(2));
            Assert.AreEqual(1, data.wheelSpins);
        }

        [Test]
        public void Wheel_SpinConsumesAttemptAndGrantsPrize()
        {
            var data = Fresh();
            data.wheelSpins = 1;
            int sector = DailyRewards.Spin(data, new Random(3));
            Assert.That(sector, Is.InRange(0, 5));
            var prize = EconomyRules.WheelPrizes[sector];
            Assert.AreEqual(prize.Spins, data.wheelSpins);
            Assert.AreEqual(prize.Coins, data.coins);
            Assert.AreEqual(prize.Undo + prize.Hints + prize.Bottles, data.undoCount + data.hintCount + data.extraBottleCount);

            data.wheelSpins = 0;
            Assert.AreEqual(-1, DailyRewards.Spin(data, new Random(3)));
        }

        [Test]
        public void Wheel_AllSectorsCanDrop()
        {
            var seen = new bool[EconomyRules.WheelPrizes.Length];
            var random = new Random(11);
            for (int i = 0; i < 200; i++)
            {
                var data = Fresh();
                seen[DailyRewards.Spin(data, random)] = true;
            }
            CollectionAssert.DoesNotContain(seen, false);
        }
    }
}
