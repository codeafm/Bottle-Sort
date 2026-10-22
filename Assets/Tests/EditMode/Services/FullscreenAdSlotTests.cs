using BottleSort.Services;
using NUnit.Framework;

namespace BottleSort.Tests
{
    public class FullscreenAdSlotTests
    {
        const double Minute = 60;

        [Test]
        public void Empty_NeedsLoad()
        {
            var slot = new FullscreenAdSlot();
            Assert.IsTrue(slot.NeedsLoad(0, out bool stale));
            Assert.IsFalse(stale);
        }

        [Test]
        public void Loading_NoSecondRequest()
        {
            var slot = new FullscreenAdSlot();
            slot.OnLoadStarted();
            Assert.IsFalse(slot.NeedsLoad(0, out _));
        }

        [Test]
        public void FreshForThirtyMinutes()
        {
            var slot = new FullscreenAdSlot();
            slot.OnLoadStarted();
            slot.OnLoaded(0);
            Assert.IsTrue(slot.IsFresh(29 * Minute));
            Assert.IsFalse(slot.NeedsLoad(29 * Minute, out _));
        }

        [Test]
        public void Expired_IsThrownAwayAndReloaded()
        {
            // Игра открыта больше 30 минут (или висела в фоне): ролик протух, его надо загрузить заново,
            // а не считать «не готов» до бесконечности.
            var slot = new FullscreenAdSlot();
            slot.OnLoadStarted();
            slot.OnLoaded(0);
            Assert.IsFalse(slot.IsFresh(31 * Minute));
            Assert.IsTrue(slot.NeedsLoad(31 * Minute, out bool stale));
            Assert.IsTrue(stale);

            slot.OnLoadStarted();
            slot.OnLoaded(31 * Minute);
            Assert.IsTrue(slot.IsFresh(32 * Minute));
        }

        [Test]
        public void AfterShow_LoadsAgainRightAway()
        {
            var slot = new FullscreenAdSlot();
            slot.OnLoadStarted();
            slot.OnLoaded(0);
            slot.OnTaken();
            Assert.IsTrue(slot.NeedsLoad(Minute, out bool stale));
            Assert.IsFalse(stale);
        }

        [Test]
        public void AfterFailure_WaitsThirtySeconds()
        {
            // Ads проверяет слот раз в 10 секунд; без паузы пустые запросы шли бы шесть раз в минуту.
            var slot = new FullscreenAdSlot();
            slot.OnLoadStarted();
            slot.OnFailed(100);
            Assert.IsFalse(slot.NeedsLoad(110, out _));
            Assert.IsFalse(slot.NeedsLoad(129, out _));
            Assert.IsTrue(slot.NeedsLoad(130, out _));
        }

        [Test]
        public void RepeatedFailures_DoubleThePauseUpToFiveMinutes()
        {
            Assert.AreEqual(30, FullscreenAdSlot.RetryDelay(1));
            Assert.AreEqual(60, FullscreenAdSlot.RetryDelay(2));
            Assert.AreEqual(120, FullscreenAdSlot.RetryDelay(3));
            Assert.AreEqual(300, FullscreenAdSlot.RetryDelay(10));

            var slot = new FullscreenAdSlot();
            slot.OnLoadStarted();
            slot.OnFailed(0);
            slot.OnLoadStarted();
            slot.OnFailed(30);
            Assert.IsFalse(slot.NeedsLoad(89, out _));
            Assert.IsTrue(slot.NeedsLoad(90, out _));
        }

        [Test]
        public void SuccessResetsThePause()
        {
            var slot = new FullscreenAdSlot();
            slot.OnLoadStarted();
            slot.OnFailed(0);
            slot.OnLoadStarted();
            slot.OnFailed(30);
            slot.OnLoadStarted();
            slot.OnLoaded(100);
            slot.OnTaken();
            Assert.IsTrue(slot.NeedsLoad(101, out _), "после показа новый ролик грузится сразу");
            slot.OnLoadStarted();
            slot.OnFailed(101);
            Assert.IsTrue(slot.NeedsLoad(131, out _), "счёт ошибок начался заново: снова 30 с");
        }
    }
}
