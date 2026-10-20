using BottleSort.Services;
using NUnit.Framework;
using UnityEngine;

namespace BottleSort.Tests
{
    // У iOS-приложения в кабинете свои блоки, и номера там в другом порядке — легко перепутать.
    public class AdUnitPlatformTests
    {
        AdsConfig _config;

        [SetUp]
        public void SetUp() => _config = ScriptableObject.CreateInstance<AdsConfig>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_config);

        [Test]
        public void Android_UsesAndroidUnits()
        {
            Assert.AreEqual("R-M-19370824-1", _config.BannerIdFor(BannerPlacement.Menu, false));
            Assert.AreEqual("R-M-19370824-1", _config.BannerIdFor(BannerPlacement.Game, false));
            Assert.AreEqual("R-M-19370824-2", _config.InterstitialIdFor(false));
            Assert.AreEqual("R-M-19370824-3", _config.RewardedIdFor(false));
        }

        [Test]
        public void Ios_UsesIosUnits()
        {
            Assert.AreEqual("R-M-20110699-2", _config.BannerIdFor(BannerPlacement.Menu, true));
            Assert.AreEqual("R-M-20110699-2", _config.BannerIdFor(BannerPlacement.Game, true));
            Assert.AreEqual("R-M-20110699-3", _config.InterstitialIdFor(true));
            Assert.AreEqual("R-M-20110699-1", _config.RewardedIdFor(true));
        }

        [Test]
        public void SavedConfig_HasBothPlatforms()
        {
            var saved = AdsConfig.Load();
            Assert.AreEqual("R-M-19370824-2", saved.InterstitialIdFor(false));
            Assert.AreEqual("R-M-20110699-3", saved.InterstitialIdFor(true));
            Assert.AreEqual("R-M-20110699-1", saved.RewardedIdFor(true));
            Assert.AreEqual("R-M-20110699-2", saved.BannerIdFor(BannerPlacement.Game, true));
        }

        [Test]
        public void GameBanner_OwnUnitPerPlatform()
        {
            _config.yandexGameBanner = "android-game";
            _config.iosGameBanner = "ios-game";
            Assert.AreEqual("android-game", _config.BannerIdFor(BannerPlacement.Game, false));
            Assert.AreEqual("ios-game", _config.BannerIdFor(BannerPlacement.Game, true));
            Assert.AreEqual("R-M-20110699-2", _config.BannerIdFor(BannerPlacement.Menu, true));
        }
    }
}
