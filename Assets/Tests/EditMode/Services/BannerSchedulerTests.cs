using System.Collections.Generic;
using BottleSort.Services;
using BottleSort.UI;
using NUnit.Framework;

namespace BottleSort.Tests
{
    public class BannerSchedulerTests
    {
        readonly List<BannerPlacement> _loads = new List<BannerPlacement>();
        BannerScheduler _banners;

        [SetUp]
        public void SetUp()
        {
            _loads.Clear();
            _banners = new BannerScheduler(_loads.Add);
        }

        void Tick(float seconds, bool active = true)
        {
            for (float t = 0; t < seconds - 0.001f; t += 0.1f)
                _banners.Tick(0.1f, active);
        }

        int LoadsOf(BannerPlacement placement) => _loads.FindAll(p => p == placement).Count;

        [Test]
        public void Menu_LoadsOnceWhenFirstShown()
        {
            _banners.Show(BannerPlacement.Menu);
            _banners.OnLoaded(BannerPlacement.Menu, true);
            _banners.Show(BannerPlacement.None);
            _banners.Show(BannerPlacement.Menu);
            Assert.AreEqual(1, LoadsOf(BannerPlacement.Menu));
        }

        [Test]
        public void Game_SeparateRequestOnEveryLevel()
        {
            _banners.Show(BannerPlacement.Menu);
            _banners.OnLoaded(BannerPlacement.Menu, true);

            _banners.Show(BannerPlacement.Game);
            Assert.AreEqual(0, LoadsOf(BannerPlacement.Game), "игровой баннер ждёт старта уровня");
            _banners.LevelStarted();
            _banners.OnLoaded(BannerPlacement.Game, true);
            _banners.LevelStarted();
            _banners.OnLoaded(BannerPlacement.Game, true);
            _banners.LevelStarted();

            Assert.AreEqual(3, LoadsOf(BannerPlacement.Game));
            Assert.AreEqual(1, LoadsOf(BannerPlacement.Menu), "меню своим запросом не перегружается");
        }

        [Test]
        public void LevelStartedBeforeScreenShown_StillOneRequest()
        {
            _banners.LevelStarted();
            _banners.Show(BannerPlacement.Game);
            Assert.AreEqual(1, LoadsOf(BannerPlacement.Game));
        }

        [Test]
        public void LevelRequestWaitsForLoadInFlight()
        {
            _banners.Show(BannerPlacement.Game);
            _banners.LevelStarted();
            _banners.LevelStarted();
            _banners.LevelStarted();
            Assert.AreEqual(1, LoadsOf(BannerPlacement.Game), "пока грузится, второй запрос не уходит");
            Assert.IsTrue(_banners.IsLoading(BannerPlacement.Game));

            _banners.OnLoaded(BannerPlacement.Game, true);
            Assert.AreEqual(2, LoadsOf(BannerPlacement.Game), "отложенный запрос уровня уходит после загрузки");
            _banners.OnLoaded(BannerPlacement.Game, true);
            Assert.AreEqual(2, LoadsOf(BannerPlacement.Game));
        }

        [Test]
        public void RefreshesEveryThirtyVisibleSeconds()
        {
            _banners.Show(BannerPlacement.Menu);
            _banners.OnLoaded(BannerPlacement.Menu, true);
            Tick(29f);
            Assert.AreEqual(1, LoadsOf(BannerPlacement.Menu));
            Tick(1.5f);
            Assert.AreEqual(2, LoadsOf(BannerPlacement.Menu));
        }

        [Test]
        public void TimerPausesWhileHiddenOrInBackground()
        {
            _banners.Show(BannerPlacement.Menu);
            _banners.OnLoaded(BannerPlacement.Menu, true);
            Tick(20f);
            _banners.Show(BannerPlacement.None);
            Tick(600f);
            _banners.Show(BannerPlacement.Menu);
            Tick(600f, active: false);
            Assert.AreEqual(20f, _banners.VisibleTime(BannerPlacement.Menu), 0.05f);
            Tick(9f);
            Assert.AreEqual(1, LoadsOf(BannerPlacement.Menu));
            Tick(1.5f);
            Assert.AreEqual(2, LoadsOf(BannerPlacement.Menu));
        }

        [Test]
        public void EachPlacementHasItsOwnTimer()
        {
            _banners.Show(BannerPlacement.Menu);
            _banners.OnLoaded(BannerPlacement.Menu, true);
            Tick(20f);
            _banners.Show(BannerPlacement.Game);
            _banners.LevelStarted();
            _banners.OnLoaded(BannerPlacement.Game, true);
            Tick(25f);
            Assert.AreEqual(1, LoadsOf(BannerPlacement.Game));
            Assert.AreEqual(20f, _banners.VisibleTime(BannerPlacement.Menu), 0.05f);
        }

        [Test]
        public void NewLevelResetsTimer()
        {
            _banners.Show(BannerPlacement.Game);
            _banners.LevelStarted();
            _banners.OnLoaded(BannerPlacement.Game, true);
            Tick(25f);
            _banners.LevelStarted();
            _banners.OnLoaded(BannerPlacement.Game, true);
            Tick(25f);
            Assert.AreEqual(2, LoadsOf(BannerPlacement.Game), "таймер начался заново с новым уровнем");
        }

        [Test]
        public void FailedLoad_RetriesInTenSeconds()
        {
            _banners.Show(BannerPlacement.Menu);
            _banners.OnLoaded(BannerPlacement.Menu, false);
            Tick(9f);
            Assert.AreEqual(1, LoadsOf(BannerPlacement.Menu));
            Tick(1.5f);
            Assert.AreEqual(2, LoadsOf(BannerPlacement.Menu));
        }

        [Test]
        public void HugeFrameAfterResumeDoesNotCount()
        {
            _banners.Show(BannerPlacement.Menu);
            _banners.OnLoaded(BannerPlacement.Menu, true);
            _banners.Tick(3600f, true);
            Assert.Less(_banners.VisibleTime(BannerPlacement.Menu), 1f);
        }

        [TestCase(ScreenId.Menu, BannerPlacement.Menu)]
        [TestCase(ScreenId.Levels, BannerPlacement.Menu)]
        [TestCase(ScreenId.Game, BannerPlacement.Game)]
        [TestCase(ScreenId.Shop, BannerPlacement.Menu)]
        [TestCase(ScreenId.Win, BannerPlacement.Game)]
        [TestCase(ScreenId.Splash, BannerPlacement.None)]
        public void ScreensMapToPlacements(ScreenId screen, BannerPlacement placement)
        {
            Assert.AreEqual(placement, BannerRules.PlacementOf(screen));
        }

        [Test]
        public void GameBannerRequestedOnEveryLevelUnlessNoAds()
        {
            Assert.IsTrue(BannerRules.AllowedOnLevel(false));
            Assert.IsFalse(BannerRules.AllowedOnLevel(true));
        }
    }
}
