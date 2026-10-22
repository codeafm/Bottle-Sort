using System;
using System.Collections.Generic;
using BottleSort.Core;
using BottleSort.Services;
using BottleSort.UI;
using NUnit.Framework;

namespace BottleSort.Tests
{
    public class AnalyticsTests
    {
        sealed class FakeBackend : IAnalyticsBackend
        {
            public Action<bool> Ready;
            public readonly List<string> Events = new List<string>();
            public readonly List<AnalyticsParams> Params = new List<AnalyticsParams>();
            public readonly Dictionary<string, string> UserProperties = new Dictionary<string, string>();

            public void Start(Action<bool> ready) => Ready = ready;

            public void Log(string name, AnalyticsParams parameters)
            {
                Events.Add(name);
                Params.Add(parameters);
            }

            public void SetUserProperty(string name, string value) => UserProperties[name] = value;
        }

        FakeBackend _backend;

        [SetUp]
        public void SetUp()
        {
            _backend = new FakeBackend();
            Analytics.Use(_backend);
        }

        [TearDown]
        public void TearDown() => Analytics.Use(null);

        [Test]
        public void EventsWaitForFirebaseThenGoInOrder()
        {
            Analytics.LevelStarted(5, LevelType.Normal, 1);
            Analytics.LevelLost(5, "Тупик", 12);
            Assert.IsEmpty(_backend.Events);

            _backend.Ready(true);
            CollectionAssert.AreEqual(new[] { "level_start", "level_fail" }, _backend.Events);
            Assert.AreEqual("Тупик", _backend.Params[1]["reason"]);
            Assert.AreEqual(5L, _backend.Params[1]["level"]);
        }

        [Test]
        public void FirebaseUnavailable_EventsAreDropped()
        {
            Analytics.Log("level_start");
            _backend.Ready(false);
            Analytics.Log("level_start");
            Assert.IsEmpty(_backend.Events);
        }

        [Test]
        public void WithoutBackend_NothingHappens()
        {
            Analytics.Use(null);
            Assert.DoesNotThrow(() => Analytics.LevelStarted(1, LevelType.Normal, 1));
        }

        [Test]
        public void FirstLevel_StartsTutorial()
        {
            _backend.Ready(true);
            Analytics.LevelStarted(1, LevelType.Normal, 1);
            Analytics.LevelStarted(1, LevelType.Normal, 2);
            CollectionAssert.AreEqual(new[] { "tutorial_begin", "level_start", "level_start" }, _backend.Events);
            Assert.AreEqual(2L, _backend.Params[2]["attempt"]);
        }

        [Test]
        public void Win_LogsLevelAndCoins_LastTutorialLevelCompletesTutorial()
        {
            _backend.Ready(true);
            Analytics.LevelStarted(HandmadeLevels.TutorialCount, LevelType.Normal, 1);
            Analytics.LevelWon(HandmadeLevels.TutorialCount, 3, 9, 40, true);
            CollectionAssert.AreEqual(
                new[] { "level_start", "level_complete", "earn_virtual_currency", "tutorial_complete" }, _backend.Events);
            Assert.AreEqual(40L, _backend.Params[2]["value"]);
            Assert.AreEqual("Победа на уровне", _backend.Params[2]["source"]);
        }

        [Test]
        public void Rewards_OnlyNonZeroParts()
        {
            _backend.Ready(true);
            Analytics.Rewarded(new RewardBundle(0, hints: 2), "Колесо удачи");
            Analytics.Rewarded(new RewardBundle(0), "Пусто");
            Assert.AreEqual(1, _backend.Events.Count);
            Assert.AreEqual(2L, _backend.Params[0]["hints"]);
            Assert.IsNull(_backend.Params[0]["undo"]);
        }

        [Test]
        public void AdImpression_UsesFirebaseRevenueParameters()
        {
            _backend.Ready(true);
            Analytics.AdImpression(AdImpression.Parse("banner",
                "{\"currency\":\"RUB\",\"revenueUSD\":\"0.00012\",\"precision\":\"estimated\",\"ad_unit_id\":\"R-M-1-1\",\"network\":{\"name\":\"Yandex\"}}"));
            Assert.AreEqual("ad_impression", _backend.Events[0]);
            var p = _backend.Params[0];
            Assert.AreEqual("Yandex", p["ad_platform"]);
            Assert.AreEqual("banner", p["ad_format"]);
            Assert.AreEqual(0.00012, (double)p["value"], 1e-9);
            Assert.AreEqual("USD", p["currency"]);
        }

        [Test]
        public void UserPropertiesAreSentOnceWhenReady()
        {
            _backend.Ready(true);
            Assert.IsTrue(_backend.UserProperties.ContainsKey("max_level"));
            Assert.IsTrue(_backend.UserProperties.ContainsKey("no_ads"));
        }

        [Test]
        public void ScreensHaveRussianNames()
        {
            foreach (ScreenId screen in Enum.GetValues(typeof(ScreenId)))
                StringAssert.DoesNotMatch("^[A-Za-z]+$", Analytics.ScreenName(screen), screen.ToString());
        }
    }

    public class AdImpressionTests
    {
        [Test]
        public void ParsesYandexRawData()
        {
            var impression = AdImpression.Parse("interstitial",
                "{\"currency\":\"RUB\",\"revenueUSD\":\"0.0034\",\"precision\":\"publisher_defined\",\"revenue\":\"0.31\"," +
                "\"adType\":\"interstitial\",\"ad_unit_id\":\"R-M-19370824-2\",\"network\":{\"name\":\"Unity Ads\",\"adapter\":\"UnityAds\"}}");
            Assert.IsTrue(impression.HasRevenue);
            Assert.AreEqual(0.0034, impression.RevenueUsd, 1e-9);
            Assert.AreEqual("Unity Ads", impression.Network);
            Assert.AreEqual("R-M-19370824-2", impression.AdUnit);
            Assert.AreEqual("publisher_defined", impression.Precision);
        }

        [Test]
        public void NumbersWithoutQuotes()
        {
            var impression = AdImpression.Parse("banner", "{\"revenueUSD\":1.5E-4,\"network\":{\"name\":\"Yandex\"}}");
            Assert.AreEqual(0.00015, impression.RevenueUsd, 1e-12);
        }

        [Test]
        public void RevenueInUsdWithoutRevenueUsdField()
        {
            var impression = AdImpression.Parse("banner", "{\"currency\":\"USD\",\"revenue\":\"0.002\"}");
            Assert.IsTrue(impression.HasRevenue);
            Assert.AreEqual(0.002, impression.RevenueUsd, 1e-9);
        }

        [Test]
        public void BrokenOrEmptyData_NoRevenueButStillAnImpression()
        {
            foreach (var raw in new[] { null, "", "not json", "{\"currency\":\"RUB\",\"revenue\":\"0.3\"}" })
            {
                var impression = AdImpression.Parse("banner", raw);
                Assert.IsFalse(impression.HasRevenue, raw);
                Assert.AreEqual("Yandex", impression.Network);
                Assert.AreEqual("banner", impression.Format);
            }
        }

    }

    public class InterstitialPreloadTests
    {
        [Test]
        public void InterstitialIsNotPreloadedWhenItCannotBeShown()
        {
            Assert.IsFalse(InterstitialPolicy.MayEverShow(1, false), "обучение");
            Assert.IsFalse(InterstitialPolicy.MayEverShow(InterstitialPolicy.FirstLevel - 1, false));
            Assert.IsTrue(InterstitialPolicy.MayEverShow(InterstitialPolicy.FirstLevel, false));
            Assert.IsFalse(InterstitialPolicy.MayEverShow(100, true), "куплено «Без рекламы»");
        }
    }
}
