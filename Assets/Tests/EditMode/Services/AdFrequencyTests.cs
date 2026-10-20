using System;
using BottleSort.Core;
using BottleSort.Services;
using BottleSort.UI;
using NUnit.Framework;

namespace BottleSort.Tests
{
    public class AdFrequencyTests
    {
        // Время в секундах; между событиями заведомо больше 30 с, если тест не про интервал.
        const double Later = 1000;

        [Test]
        public void Interstitial_EverySecondWin()
        {
            var policy = new InterstitialPolicy();
            policy.OnWin(10);
            Assert.IsFalse(policy.ShouldShowAfterWin(10, false, 0));
            policy.OnWin(11);
            Assert.IsTrue(policy.ShouldShowAfterWin(11, false, 0));
            policy.OnShownAfterWin(0);

            policy.OnWin(12);
            Assert.IsFalse(policy.ShouldShowAfterWin(12, false, Later));
            policy.OnWin(13);
            Assert.IsTrue(policy.ShouldShowAfterWin(13, false, Later));
        }

        [Test]
        public void Interstitial_EverySecondLoss_CountedSeparately()
        {
            var policy = new InterstitialPolicy();
            policy.OnWin(10);
            policy.OnLoss(11);
            Assert.IsFalse(policy.ShouldShowAfterLoss(11, false, 0), "одна победа и один проигрыш — ещё не два проигрыша");
            Assert.IsFalse(policy.ShouldShowAfterWin(11, false, 0));
            policy.OnLoss(11);
            Assert.IsTrue(policy.ShouldShowAfterLoss(11, false, 0));
            policy.OnShownAfterLoss(0);

            Assert.AreEqual(0, policy.LossesSinceAd);
            Assert.AreEqual(1, policy.WinsSinceAd, "показ после проигрышей не сбрасывает счёт побед");
        }

        [Test]
        public void Interstitial_NotOnTutorialLevels()
        {
            var policy = new InterstitialPolicy();
            for (int level = 1; level <= 3; level++)
                policy.OnWin(level);
            Assert.AreEqual(0, policy.WinsSinceAd);
            policy.OnWin(4);
            policy.OnWin(5);
            Assert.IsFalse(policy.ShouldShowAfterWin(3, false, 0));
            Assert.IsTrue(policy.ShouldShowAfterWin(5, false, 0));
        }

        [Test]
        public void Interstitial_AtLeastThirtySecondsApart()
        {
            var policy = new InterstitialPolicy();
            policy.OnLoss(20);
            policy.OnLoss(20);
            Assert.IsTrue(policy.ShouldShowAfterLoss(20, false, 100));
            policy.OnShownAfterLoss(100);

            policy.OnWin(20);
            policy.OnWin(21);
            Assert.IsFalse(policy.ShouldShowAfterWin(21, false, 110));
            Assert.AreEqual(2, policy.WinsSinceAd, "пропуск не съедает счёт — реклама выйдет при следующей возможности");
            Assert.IsTrue(policy.ShouldShowAfterWin(22, false, 130));
        }

        [Test]
        public void Interstitial_DoubleRewardOnEveryWin_DoesNotBlockIt()
        {
            // Игрок на каждой победе жмёт ×2: ролик за награду, потом «Дальше».
            var policy = new InterstitialPolicy();
            double now = 0;
            for (int level = 10; level < 16; level++)
            {
                policy.OnWin(level);
                now += 40;
                policy.OnRewardedWatched(now);
                now += 35;
                if (policy.ShouldShowAfterWin(level, false, now))
                    policy.OnShownAfterWin(now);
            }
            Assert.AreEqual(0, policy.WinsSinceAd, "межстраничная выходила каждые 2 уровня");
        }

        [Test]
        public void Interstitial_RewardedOnlyShiftsThirtySeconds()
        {
            var policy = new InterstitialPolicy();
            policy.OnWin(30);
            policy.OnWin(31);
            policy.OnRewardedWatched(Later);
            Assert.AreEqual(InterstitialDecision.Spacing, policy.DecideAfterWin(31, false, Later + 10));
            Assert.AreEqual(InterstitialDecision.Show, policy.DecideAfterWin(31, false, Later + 30));
        }

        [Test]
        public void Interstitial_ReportsWhySkipped()
        {
            var policy = new InterstitialPolicy();
            Assert.AreEqual(InterstitialDecision.Tutorial, policy.DecideAfterWin(2, false, Later));
            Assert.AreEqual(InterstitialDecision.NoAds, policy.DecideAfterWin(20, true, Later));
            Assert.AreEqual(InterstitialDecision.NotDue, policy.DecideAfterWin(20, false, Later));
        }

        [Test]
        public void Interstitial_NeverWithNoAds()
        {
            var policy = new InterstitialPolicy();
            policy.OnWin(40);
            policy.OnWin(41);
            Assert.IsFalse(policy.ShouldShowAfterWin(41, true, Later));
        }

        [Test]
        public void ExtraMoves_ContinueLevelUntilRestart()
        {
            var session = new GameSession(LevelState.FromLayout(4, "RRGG GGRR - -"), 4, moveLimit: 2);
            Assert.AreEqual(MoveError.None, session.TryMove(Move.Pour(0, 2), out _));
            Assert.AreEqual(MoveError.None, session.TryMove(Move.Pour(1, 3), out _));
            Assert.IsTrue(session.IsOutOfMoves);

            Assert.IsTrue(session.AddMoves(5));
            Assert.IsFalse(session.IsOutOfMoves);
            Assert.AreEqual(5, session.MovesLeft);

            session.Restart();
            Assert.AreEqual(2, session.MoveLimit);
        }

        [Test]
        public void ExtraMoves_OnlyForLevelsWithLimit()
        {
            var session = new GameSession(LevelState.FromLayout(4, "RRGG GGRR - -"), 4);
            Assert.IsFalse(session.AddMoves(5));
            Assert.IsFalse(session.HasMoveLimit);
        }

        [TestCase(LevelType.Normal, 2)]
        [TestCase(LevelType.Special, 2)]
        [TestCase(LevelType.Rescue, 3)]
        [TestCase(LevelType.Boss, 3)]
        [TestCase(LevelType.ChapterBoss, 3)]
        public void WinReward_TripleOnRescueAndBosses(LevelType type, int multiplier)
        {
            Assert.AreEqual(multiplier, EconomyRules.RewardedWinMultiplier(type));
        }

        [Test]
        public void DailyGift_DoubleForAd()
        {
            var data = new PlayerData();
            Assert.IsTrue(DailyRewards.TryClaimGift(data, new DateTime(2026, 10, 18), out var reward, 2));
            Assert.AreEqual(150, reward.Coins);
            Assert.AreEqual(150, data.coins);
            Assert.AreEqual(2, data.undoCount);
            Assert.AreEqual(2, data.hintCount);
            Assert.IsFalse(DailyRewards.TryClaimGift(data, new DateTime(2026, 10, 18), out _, 2));
        }

        [Test]
        public void Calendar_DoubleForAd_KeepsStreak()
        {
            var data = new PlayerData();
            Assert.IsTrue(DailyRewards.TryClaimCalendar(data, new DateTime(2026, 10, 18), out int coins, 2));
            Assert.AreEqual(120, coins);
            Assert.AreEqual(120, data.coins);
            Assert.AreEqual(2, DailyRewards.CalendarDay(data));
        }
    }
}
