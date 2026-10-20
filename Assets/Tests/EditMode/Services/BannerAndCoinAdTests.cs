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
        public void CoinAd_GivesHundredAndStartsCooldown()
        {
            var data = Player();
            var now = new DateTime(2026, 10, 17, 12, 0, 0);
            Assert.AreEqual(CoinAdState.Available, CoinAdOffer.StateOf(data, now, out _));
            Assert.IsTrue(CoinAdOffer.TryGrant(data, now));
            Assert.AreEqual(150, data.coins);

            Assert.AreEqual(CoinAdState.Cooldown, CoinAdOffer.StateOf(data, now.AddMinutes(1), out var wait));
            Assert.AreEqual(TimeSpan.FromMinutes(2), wait);
            Assert.IsFalse(CoinAdOffer.TryGrant(data, now.AddMinutes(1)));
            Assert.AreEqual(150, data.coins);
            Assert.AreEqual(CoinAdState.Available, CoinAdOffer.StateOf(data, now.AddMinutes(3), out _));
        }

        [Test]
        public void CoinAd_ThreeTimesADay()
        {
            var data = Player();
            var now = new DateTime(2026, 10, 17, 9, 0, 0);
            for (int i = 0; i < CoinAdOffer.PerDay; i++)
                Assert.IsTrue(CoinAdOffer.TryGrant(data, now.AddMinutes(10 * i)), "watch " + i);
            Assert.AreEqual(CoinAdState.DailyLimit, CoinAdOffer.StateOf(data, now.AddHours(5), out _));
            Assert.AreEqual(0, CoinAdOffer.LeftToday(data, now.AddHours(5)));
            Assert.AreEqual(50 + 300, data.coins);

            var tomorrow = now.Date.AddDays(1).AddHours(8);
            Assert.AreEqual(CoinAdState.Available, CoinAdOffer.StateOf(data, tomorrow, out _));
            Assert.AreEqual(CoinAdOffer.PerDay, CoinAdOffer.LeftToday(data, tomorrow));
        }

        [Test]
        public void CoinAd_NotDuringTutorial()
        {
            var data = Player();
            data.highestUnlocked = 3;
            var now = new DateTime(2026, 10, 17, 12, 0, 0);
            Assert.AreEqual(CoinAdState.Tutorial, CoinAdOffer.StateOf(data, now, out _));
            Assert.IsFalse(CoinAdOffer.TryGrant(data, now));
            Assert.AreEqual(50, data.coins);
        }

        [Test]
        public void CoinAd_CooldownSurvivesSave()
        {
            var data = Player();
            var now = new DateTime(2026, 10, 17, 12, 0, 0);
            CoinAdOffer.TryGrant(data, now);
            var copy = UnityEngine.JsonUtility.FromJson<PlayerData>(UnityEngine.JsonUtility.ToJson(data));
            Assert.AreEqual(CoinAdState.Cooldown, CoinAdOffer.StateOf(copy, now.AddSeconds(30), out _));
            Assert.AreEqual(CoinAdOffer.PerDay - 1, CoinAdOffer.LeftToday(copy, now));
        }
    }
}
