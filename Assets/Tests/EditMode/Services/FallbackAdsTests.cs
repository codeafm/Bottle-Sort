using System;
using BottleSort.Services;
using NUnit.Framework;

namespace BottleSort.Tests
{
    public class FallbackAdsTests
    {
        sealed class FakeNetwork : IAdsService
        {
            public bool RewardedLoaded;
            public bool InterstitialLoaded;
            // Что вернёт колбэк rewarded: досмотрел или закрыл раньше.
            public bool WatchesToEnd = true;
            public int RewardedShown;
            public int InterstitialShown;
            public int Loads;
            public bool BannerVisible;

            public bool IsRewardedReady => RewardedLoaded;
            public bool IsInterstitialReady => InterstitialLoaded;

            public void Initialize()
            {
            }

            public void LoadRewarded() => Loads++;

            public void ShowRewarded(Action<bool> done)
            {
                RewardedShown++;
                RewardedLoaded = false;
                done(WatchesToEnd);
            }

            public void ShowInterstitial(Action done)
            {
                InterstitialShown++;
                InterstitialLoaded = false;
                done();
            }

            public void ShowBanner() => BannerVisible = true;
            public void HideBanner() => BannerVisible = false;
        }

        FakeNetwork _yandex;
        FakeNetwork _unity;
        FallbackAdsService _ads;

        [SetUp]
        public void SetUp()
        {
            _yandex = new FakeNetwork();
            _unity = new FakeNetwork();
            _ads = new FallbackAdsService(_yandex, _unity);
        }

        [Test]
        public void Rewarded_PrimaryFirst()
        {
            _yandex.RewardedLoaded = true;
            _unity.RewardedLoaded = true;
            bool? watched = null;
            _ads.ShowRewarded(result => watched = result);
            Assert.AreEqual(true, watched);
            Assert.AreEqual(1, _yandex.RewardedShown);
            Assert.AreEqual(0, _unity.RewardedShown);
        }

        [Test]
        public void Rewarded_FallsBackWhenPrimaryEmpty()
        {
            _unity.RewardedLoaded = true;
            Assert.IsTrue(_ads.IsRewardedReady);
            bool? watched = null;
            _ads.ShowRewarded(result => watched = result);
            Assert.AreEqual(true, watched);
            Assert.AreEqual(0, _yandex.RewardedShown);
            Assert.AreEqual(1, _unity.RewardedShown);
        }

        [Test]
        public void Rewarded_RewardComesFromNetworkThatShowed()
        {
            _unity.RewardedLoaded = true;
            _unity.WatchesToEnd = false;
            _yandex.WatchesToEnd = true;
            bool? watched = null;
            _ads.ShowRewarded(result => watched = result);
            Assert.AreEqual(false, watched);
        }

        [Test]
        public void Rewarded_NothingLoaded_ReportsNoReward()
        {
            Assert.IsFalse(_ads.IsRewardedReady);
            bool? watched = null;
            _ads.ShowRewarded(result => watched = result);
            Assert.AreEqual(false, watched);
            Assert.AreEqual(0, _yandex.RewardedShown + _unity.RewardedShown);
        }

        [Test]
        public void Interstitial_FallsBackAndAlwaysContinues()
        {
            _unity.InterstitialLoaded = true;
            int continued = 0;
            _ads.ShowInterstitial(() => continued++);
            Assert.AreEqual(1, _unity.InterstitialShown);

            _ads.ShowInterstitial(() => continued++);
            Assert.AreEqual(2, continued, "игра идёт дальше, даже если показывать нечего");
            Assert.AreEqual(1, _unity.InterstitialShown);
        }

        [Test]
        public void Interstitial_ShowsOnlyOneNetwork()
        {
            _yandex.InterstitialLoaded = true;
            _unity.InterstitialLoaded = true;
            _ads.ShowInterstitial(() => { });
            Assert.AreEqual(1, _yandex.InterstitialShown);
            Assert.AreEqual(0, _unity.InterstitialShown);
            Assert.IsTrue(_unity.InterstitialLoaded);
        }

        [Test]
        public void Banner_OnlyPrimary()
        {
            _ads.ShowBanner();
            Assert.IsTrue(_yandex.BannerVisible);
            Assert.IsFalse(_unity.BannerVisible);
        }

        [Test]
        public void Load_AsksBothNetworks()
        {
            _ads.LoadRewarded();
            Assert.AreEqual(1, _yandex.Loads);
            Assert.AreEqual(1, _unity.Loads);
        }

        [Test]
        public void WithoutFallback_WorksAsPrimary()
        {
            var single = new FallbackAdsService(_yandex, null);
            Assert.IsFalse(single.IsRewardedReady);
            bool? watched = null;
            single.ShowRewarded(result => watched = result);
            Assert.AreEqual(false, watched);
            int continued = 0;
            single.ShowInterstitial(() => continued++);
            Assert.AreEqual(1, continued);
        }
    }
}
