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
        public void AfterShowOrFailure_LoadsAgain()
        {
            var slot = new FullscreenAdSlot();
            slot.OnLoadStarted();
            slot.OnLoaded(0);
            slot.OnTaken();
            Assert.IsTrue(slot.NeedsLoad(Minute, out bool stale));
            Assert.IsFalse(stale);

            slot.OnLoadStarted();
            slot.OnFailed();
            Assert.IsTrue(slot.NeedsLoad(Minute, out _));
        }
    }
}
