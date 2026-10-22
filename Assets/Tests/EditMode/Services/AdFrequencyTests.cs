using System;
using BottleSort.Core;
using BottleSort.Services;
using BottleSort.UI;
using NUnit.Framework;

namespace BottleSort.Tests
{
    public class AdFrequencyTests
    {
        [Test]
        public void Interstitial_AfterEveryLevel_OnceAMinutePassed()
        {
            var policy = new InterstitialPolicy();
            Assert.IsTrue(policy.ShouldShow(10, false, 61f));
            policy.OnShown(61f);
            Assert.IsTrue(policy.ShouldShow(11, false, 121f), "минута с прошлой — снова реклама");
        }

        [Test]
        public void Interstitial_NotMoreOftenThanOnceAMinute()
        {
            var policy = new InterstitialPolicy();
            policy.OnShown(100f);
            Assert.AreEqual(InterstitialDecision.NotDue, policy.Decide(12, false, 130f), "уровень за 30 секунд — без рекламы");
            Assert.AreEqual(InterstitialDecision.NotDue, policy.Decide(13, false, 159f));
            Assert.AreEqual(30f, policy.SecondsUntilDue(130f), 0.001f);
            Assert.IsTrue(policy.ShouldShow(14, false, 160f));
        }

        [Test]
        public void Interstitial_NotInFirstMinuteAfterLaunch()
        {
            var policy = new InterstitialPolicy(startedAt: 5f);
            Assert.IsFalse(policy.ShouldShow(20, false, 40f));
            Assert.IsTrue(policy.ShouldShow(20, false, 65f));
        }

        [Test]
        public void Interstitial_WinAndLossShareOneMinute()
        {
            // Реклама после победы, потом быстрый проигрыш — на «Заново» рекламы нет, минута общая.
            var policy = new InterstitialPolicy();
            policy.OnShown(200f);
            Assert.IsFalse(policy.ShouldShow(30, false, 215f));
            Assert.IsTrue(policy.ShouldShow(30, false, 260f));
        }

        [Test]
        public void Interstitial_NotReady_TriesAfterNextLevel()
        {
            var policy = new InterstitialPolicy();
            Assert.IsTrue(policy.ShouldShow(20, false, 90f));
            // ролик не загрузился, OnShown не вызывали — после следующего уровня пробуем снова
            Assert.IsTrue(policy.ShouldShow(21, false, 110f));
        }

        [Test]
        public void Interstitial_NotOnTutorialLevels()
        {
            var policy = new InterstitialPolicy();
            for (int level = 1; level < InterstitialPolicy.FirstLevel; level++)
                Assert.AreEqual(InterstitialDecision.Tutorial, policy.Decide(level, false, 500f));
            Assert.IsTrue(policy.ShouldShow(InterstitialPolicy.FirstLevel, false, 500f));
        }

        [Test]
        public void Interstitial_NeverWithNoAds()
        {
            var policy = new InterstitialPolicy();
            Assert.AreEqual(InterstitialDecision.NoAds, policy.Decide(41, true, 1000f));
            Assert.AreEqual(InterstitialDecision.NoAds, policy.Decide(2, true, 1000f), "«Без рекламы» важнее обучения");
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
