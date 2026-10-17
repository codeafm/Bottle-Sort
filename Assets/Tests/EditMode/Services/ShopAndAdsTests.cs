using BottleSort.Core;
using BottleSort.Services;
using BottleSort.UI;
using NUnit.Framework;

namespace BottleSort.Tests
{
    public class ShopAndAdsTests
    {
        static ShopItem Bottle(string id) => ShopCatalog.Find(ShopCategory.Bottle, id);
        static ShopItem Theme(string id) => ShopCatalog.Find(ShopCategory.Theme, id);

        [Test]
        public void Buy_SpendsCoinsOwnsAndSelects()
        {
            var data = new PlayerData { highestUnlocked = 40, coins = 450 };
            Assert.AreEqual(ShopItemState.Affordable, Shop.StateOf(data, Bottle("glass")));
            Assert.AreEqual(PurchaseResult.Bought, Shop.TryBuy(data, Bottle("glass")));
            Assert.AreEqual(150, data.coins);
            Assert.IsTrue(data.Owns("glass"));
            Assert.AreEqual("glass", data.bottleSkin);
            Assert.AreEqual(ShopItemState.Selected, Shop.StateOf(data, Bottle("glass")));
            Assert.AreEqual(PurchaseResult.AlreadyOwned, Shop.TryBuy(data, Bottle("glass")));
            Assert.AreEqual(150, data.coins);
        }

        [Test]
        public void Buy_RespectsLevelAndCoins()
        {
            var data = new PlayerData { highestUnlocked = 40, coins = 5000 };
            Assert.AreEqual(ShopItemState.Locked, Shop.StateOf(data, Bottle("flask")));
            Assert.AreEqual(PurchaseResult.Locked, Shop.TryBuy(data, Bottle("flask")));

            data.highestUnlocked = 60;
            data.coins = 100;
            Assert.AreEqual(ShopItemState.TooExpensive, Shop.StateOf(data, Bottle("flask")));
            Assert.AreEqual(PurchaseResult.NotEnoughCoins, Shop.TryBuy(data, Bottle("flask")));
            Assert.AreEqual(100, data.coins);
        }

        [Test]
        public void FreeItems_OpenByLevel()
        {
            var data = new PlayerData { highestUnlocked = 10 };
            Assert.AreEqual(ShopItemState.Locked, Shop.StateOf(data, Bottle("wine")));
            data.highestUnlocked = 30;
            Assert.AreEqual(ShopItemState.Owned, Shop.StateOf(data, Bottle("wine")));
            Assert.IsTrue(Shop.TrySelect(data, Bottle("wine")));
            Assert.AreEqual("wine", data.bottleSkin);
            Assert.AreEqual(0, data.coins);
        }

        [Test]
        public void Select_NotOwned_Fails()
        {
            var data = new PlayerData { highestUnlocked = 300, coins = 0 };
            Assert.IsFalse(Shop.TrySelect(data, Theme("amber")));
            Assert.AreEqual("auto", data.theme);
            Assert.IsTrue(Shop.TrySelect(data, Theme("navy")));
            Assert.AreEqual("navy", data.theme);
        }

        [Test]
        public void Catalog_PricesFromDocuments()
        {
            Assert.AreEqual(300, Bottle("glass").Price);
            Assert.AreEqual(1300, Bottle("potion").Price);
            Assert.AreEqual(140, Bottle("potion").UnlockLevel);
            Assert.AreEqual(500, Theme("violet").Price);
            Assert.AreEqual(1000, Theme("amber").Price);
            Assert.AreEqual(251, Theme("amber").UnlockLevel);
        }

        [Test]
        public void SelectedSkin_ReplacesOnlyClassicBottles()
        {
            Assert.AreEqual("jug", ShopCatalog.BottleShapeFor("classic", 4, "jug"));
            Assert.AreEqual("cylinder", ShopCatalog.BottleShapeFor("cylinder", 8, "jug"));
            Assert.AreEqual("flask", ShopCatalog.BottleShapeFor("flask", 4, "jug"));
            Assert.AreEqual("classic", ShopCatalog.BottleShapeFor("classic", 5, "potion"));
            Assert.AreEqual("jar", ShopCatalog.BottleShapeFor("classic", 5, "jar"));
            Assert.AreEqual("classic", ShopCatalog.BottleShapeFor("classic", 4, "missing"));
        }

        [Test]
        public void Interstitial_NeverBeforeLevel21()
        {
            var policy = new InterstitialPolicy();
            for (int i = 0; i < 5; i++)
                policy.OnLevelCompleted();
            Assert.IsFalse(policy.ShouldShow(20, LevelType.Normal, false, 1000));
            Assert.IsTrue(policy.ShouldShow(21, LevelType.Normal, false, 1000));
        }

        [Test]
        public void Interstitial_EveryThreeLevelsAnd150Seconds()
        {
            var policy = new InterstitialPolicy();
            policy.OnLevelCompleted();
            Assert.IsFalse(policy.ShouldShow(40, LevelType.Normal, false, 0), "first level of the session");
            policy.OnLevelCompleted();
            policy.OnLevelCompleted();
            Assert.IsTrue(policy.ShouldShow(42, LevelType.Normal, false, 0));
            policy.OnShown(0);

            for (int i = 0; i < 3; i++)
                policy.OnLevelCompleted();
            Assert.IsFalse(policy.ShouldShow(45, LevelType.Normal, false, 100), "too soon");
            Assert.IsTrue(policy.ShouldShow(45, LevelType.Normal, false, 150));
        }

        [Test]
        public void Interstitial_SkipsChapterBossFeatureIntroAndNoAds()
        {
            var policy = new InterstitialPolicy();
            for (int i = 0; i < 5; i++)
                policy.OnLevelCompleted();
            Assert.IsFalse(policy.ShouldShow(50, LevelType.ChapterBoss, false, 1000));
            Assert.IsFalse(policy.ShouldShow(101, LevelType.Normal, false, 1000));
            Assert.IsFalse(policy.ShouldShow(60, LevelType.Normal, true, 1000));
            Assert.IsTrue(policy.ShouldShow(25, LevelType.Boss, false, 1000));
        }

        [Test]
        public void Reminders_StartTomorrowEvening()
        {
            var times = NotificationService.ReminderTimes(new System.DateTime(2026, 10, 17, 21, 30, 0));
            Assert.AreEqual(7, times.Length);
            Assert.AreEqual(new System.DateTime(2026, 10, 18, 19, 0, 0), times[0]);
            Assert.AreEqual(new System.DateTime(2026, 10, 24, 19, 0, 0), times[6]);
        }
    }
}
