using System;
using BottleSort.UI;
using NUnit.Framework;

namespace BottleSort.Tests
{
    public class RewardedReadyTests
    {
        Func<bool> _isReady;
        Action _prepare;
        int _prepared;

        [SetUp]
        public void SetUp()
        {
            _isReady = RewardedAds.IsReady;
            _prepare = RewardedAds.Prepare;
            _prepared = 0;
            RewardedAds.Prepare = () => _prepared++;
        }

        [TearDown]
        public void TearDown()
        {
            RewardedAds.IsReady = _isReady;
            RewardedAds.Prepare = _prepare;
        }

        [Test]
        public void NotReady_StartsLoadingRightAway()
        {
            RewardedAds.IsReady = () => false;
            Assert.IsFalse(RewardedAds.EnsureReady());
            Assert.AreEqual(1, _prepared);
        }

        [Test]
        public void Ready_DoesNotReload()
        {
            RewardedAds.IsReady = () => true;
            Assert.IsTrue(RewardedAds.EnsureReady());
            Assert.AreEqual(0, _prepared);
        }

        [Test]
        public void RepeatedTapsWhileLoading_EachAsksToLoad()
        {
            // Повторный запрос безопасен: сервис сам не грузит второй ролик, пока идёт первый.
            RewardedAds.IsReady = () => false;
            RewardedAds.EnsureReady();
            RewardedAds.EnsureReady();
            Assert.AreEqual(2, _prepared);
        }
    }
}
