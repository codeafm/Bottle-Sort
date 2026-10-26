using System;
using BottleSort.Services;
using BottleSort.UI;
using NUnit.Framework;

namespace BottleSort.Tests
{
    public class BannerAndCoinAdTests
    {
        static BannerContext On(ScreenId screen) => new BannerContext { Screen = screen };

        [TestCase(ScreenId.Menu)]
        [TestCase(ScreenId.Levels)]
        [TestCase(ScreenId.Shop)]
        [TestCase(ScreenId.Game)]
        [TestCase(ScreenId.Win)]
        public void Banner_AlwaysShownOnScreens(ScreenId screen)
        {
            Assert.IsTrue(BannerRules.ShouldShow(On(screen)));
        }

        // Для попапов ScreenShown не приходит, но и если бы пришёл, баннер бы остался.
        [TestCase(ScreenId.Settings)]
        [TestCase(ScreenId.Gift)]
        [TestCase(ScreenId.Reward)]
        [TestCase(ScreenId.Calendar)]
        [TestCase(ScreenId.Wheel)]
        public void Banner_ShownWithPopups(ScreenId popup)
        {
            Assert.IsTrue(BannerRules.ShouldShow(On(popup)));
        }

        [Test]
        public void Banner_HiddenOnSplash()
        {
            Assert.IsFalse(BannerRules.ShouldShow(On(ScreenId.Splash)));
        }

        [Test]
        public void Banner_HiddenUnderFullscreenAdAndWithNoAds()
        {
            var fullscreen = On(ScreenId.Game);
            fullscreen.FullscreenAdShowing = true;
            var noAds = On(ScreenId.Levels);
            noAds.NoAds = true;
            Assert.IsFalse(BannerRules.ShouldShow(fullscreen));
            Assert.IsFalse(BannerRules.ShouldShow(noAds));
        }

        static PlayerData Player() => new PlayerData { highestUnlocked = 10, coins = 50 };

        [Test]
        public void CoinAd_GivesHundredEveryTime()
        {
            var data = Player();
            var now = new DateTime(2026, 10, 17, 12, 0, 0);
            CoinAdOffer.Grant(data, now);
            Assert.AreEqual(150, data.coins);
            // Сразу следующий ролик — снова монеты, без ожидания.
            CoinAdOffer.Grant(data, now.AddSeconds(5));
            Assert.AreEqual(250, data.coins);
        }

        [Test]
        public void CoinAd_NoDailyLimit()
        {
            var data = Player();
            var now = new DateTime(2026, 10, 17, 9, 0, 0);
            for (int i = 0; i < 20; i++)
                CoinAdOffer.Grant(data, now.AddMinutes(i));
            Assert.AreEqual(50 + 20 * 100, data.coins);
            Assert.AreEqual(20, data.coinAdCount);
        }

        [Test]
        public void CoinAd_AlsoDuringTutorial()
        {
            var data = Player();
            data.highestUnlocked = 1;
            CoinAdOffer.Grant(data, new DateTime(2026, 10, 17, 12, 0, 0));
            Assert.AreEqual(150, data.coins);
        }
    }
}
