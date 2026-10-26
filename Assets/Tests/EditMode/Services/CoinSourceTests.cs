using System;
using BottleSort.Services;
using BottleSort.UI;
using NUnit.Framework;

namespace BottleSort.Tests
{
    // Монеты из игры и из рекламы считаются отдельно, чтобы было видно, откуда они у игрока.
    public class CoinSourceTests
    {
        static PlayerData Player() => new PlayerData { highestUnlocked = 10 };

        [Test]
        public void LevelWin_CountsAsGame()
        {
            var data = Player();
            data.AddCoins(30);
            Assert.AreEqual(30, data.coinsFromGame);
            Assert.AreEqual(0, data.coinsFromAds);
        }

        [Test]
        public void DoubleOnWin_ExtraCountsAsAds()
        {
            var data = Player();
            data.AddCoins(30);
            data.AddCoins(30, fromAd: true);
            Assert.AreEqual(60, data.coins);
            Assert.AreEqual(30, data.coinsFromGame);
            Assert.AreEqual(30, data.coinsFromAds);
        }

        [Test]
        public void CoinAd_CountsAsAds()
        {
            var data = Player();
            CoinAdOffer.Grant(data, new DateTime(2026, 10, 26, 12, 0, 0));
            CoinAdOffer.Grant(data, new DateTime(2026, 10, 26, 12, 0, 5));
            Assert.AreEqual(200, data.coinsFromAds);
            Assert.AreEqual(0, data.coinsFromGame);
        }

        [Test]
        public void DailyGiftDoubled_SplitsBetweenGameAndAds()
        {
            var data = Player();
            Assert.IsTrue(DailyRewards.TryClaimGift(data, new DateTime(2026, 10, 26, 9, 0, 0), out _, 2));
            Assert.AreEqual(EconomyRules.DailyGift.Coins, data.coinsFromGame);
            Assert.AreEqual(EconomyRules.DailyGift.Coins, data.coinsFromAds);
        }

        [Test]
        public void CalendarDoubled_SplitsBetweenGameAndAds()
        {
            var data = Player();
            int day = DailyRewards.CalendarDay(data);
            Assert.IsTrue(DailyRewards.TryClaimCalendar(data, new DateTime(2026, 10, 26, 9, 0, 0), out int coins, 2));
            Assert.AreEqual(EconomyRules.CalendarCoins(day), data.coinsFromGame);
            Assert.AreEqual(coins - EconomyRules.CalendarCoins(day), data.coinsFromAds);
        }

        [Test]
        public void Spending_DoesNotChangeEarnedTotals()
        {
            var data = Player();
            data.AddCoins(500);
            Assert.IsTrue(data.TrySpend(200));
            Assert.AreEqual(300, data.coins);
            Assert.AreEqual(500, data.coinsFromGame);
        }

        [Test]
        public void Totals_SurviveSave()
        {
            var data = Player();
            data.AddCoins(40);
            data.AddCoins(40, fromAd: true);
            var copy = UnityEngine.JsonUtility.FromJson<PlayerData>(UnityEngine.JsonUtility.ToJson(data));
            Assert.AreEqual(40, copy.coinsFromGame);
            Assert.AreEqual(40, copy.coinsFromAds);
        }
    }
}
