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
        public void Interstitial_SkippedAfterRewardedOnThisLevel()
        {
            var policy = new InterstitialPolicy();
            policy.OnLevelStarted();
            policy.OnWin(30);
            policy.OnWin(31);
            policy.OnRewardedWatched();
            Assert.IsFalse(policy.ShouldShowAfterWin(31, false, Later));

            policy.OnLevelStarted();
            Assert.IsTrue(policy.ShouldShowAfterWin(32, false, Later));
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
        public void BannerTimer_RefreshesAfterThirtyVisibleSeconds()
        {
            var timer = new BannerRefreshTimer();
            for (int i = 0; i < 29 * 10; i++)
                Assert.IsFalse(timer.Tick(0.1f, true));
            bool due = false;
            for (int i = 0; i < 20 && !due; i++)
                due = timer.Tick(0.1f, true);
            Assert.IsTrue(due);
        }

        [Test]
        public void BannerTimer_PausesWhenHidden()
        {
            var timer = new BannerRefreshTimer();
            Tick(timer, 20f, true);
            Tick(timer, 600f, false);
            Assert.AreEqual(20f, timer.VisibleTime, 0.01f);
            Assert.IsFalse(Tick(timer, 9f, true));
            Assert.IsTrue(Tick(timer, 2f, true));
        }

        [Test]
        public void BannerTimer_HugeFrameAfterResumeDoesNotCount()
        {
            var timer = new BannerRefreshTimer();
            Assert.IsFalse(timer.Tick(3600f, true));
            Assert.Less(timer.VisibleTime, 1f);
        }

        [Test]
        public void BannerTimer_NoOverlappingLoads_KeepsOldOnFailure()
        {
            var timer = new BannerRefreshTimer();
            Assert.IsTrue(Tick(timer, 31f, true));
            Assert.IsTrue(timer.IsLoading);
            Assert.IsFalse(Tick(timer, 60f, true), "пока грузится, второй запрос не шлём");

            timer.OnRefreshed(false);
            Assert.IsFalse(Tick(timer, 9f, true));
            Assert.IsTrue(Tick(timer, 2f, true), "после ошибки — новая попытка через 10 с");

            timer.OnRefreshed(true);
            Assert.AreEqual(0f, timer.VisibleTime);
        }

        static bool Tick(BannerRefreshTimer timer, float seconds, bool visible)
        {
            bool due = false;
            for (float t = 0; t < seconds - 0.001f; t += 0.1f)
                due |= timer.Tick(0.1f, visible);
            return due;
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
