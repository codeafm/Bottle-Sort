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
        public void LoadsOnceWhenFirstShown()
        {
            _banners.Show(BannerPlacement.Menu);
            _banners.OnLoaded(BannerPlacement.Menu, true);
            _banners.Show(BannerPlacement.None);
            _banners.Show(BannerPlacement.Menu);
            Assert.AreEqual(1, LoadsOf(BannerPlacement.Menu));
        }

        [Test]
        public void LoadedBanner_IsNeverReloadedByUs()
        {
            // Рекламу в загруженном баннере раз в 60 секунд меняет сам SDK — наш запрос был бы лишним.
            _banners.Show(BannerPlacement.Menu);
            _banners.OnLoaded(BannerPlacement.Menu, true);
            Tick(3600f);
            Assert.AreEqual(1, LoadsOf(BannerPlacement.Menu));
            Assert.IsTrue(_banners.IsLoaded(BannerPlacement.Menu));
        }

        [Test]
        public void GameBanner_LoadsOnFirstShow_NotPerLevel()
        {
            _banners.Show(BannerPlacement.Game);
            _banners.OnLoaded(BannerPlacement.Game, true);
            _banners.Show(BannerPlacement.Menu);
            _banners.Show(BannerPlacement.Game);
            Assert.AreEqual(1, LoadsOf(BannerPlacement.Game));
        }

        [Test]
        public void NoSecondRequestWhileLoading()
        {
            _banners.Show(BannerPlacement.Menu);
            _banners.Show(BannerPlacement.None);
            _banners.Show(BannerPlacement.Menu);
            Tick(120f);
            Assert.AreEqual(1, LoadsOf(BannerPlacement.Menu));
            Assert.IsTrue(_banners.IsLoading(BannerPlacement.Menu));
        }

        [Test]
        public void FailedLoad_RetriesAfterAMinuteOfVisibleTime()
        {
            _banners.Show(BannerPlacement.Menu);
            _banners.OnLoaded(BannerPlacement.Menu, false);
            Tick(59f);
            Assert.AreEqual(1, LoadsOf(BannerPlacement.Menu), "раньше минуты — не больше одного запроса в 30 с, как требует РСЯ");
            Tick(1.5f);
            Assert.AreEqual(2, LoadsOf(BannerPlacement.Menu));
        }

        [Test]
        public void NoAds_RetryEveryMinuteWhileVisible()
        {
            // Отказ «рекламы нет» — обычное дело, пауза не растёт: SDK сам обновляет баннер так же раз в минуту.
            _banners.Show(BannerPlacement.Menu);
            for (int i = 1; i <= 5; i++)
            {
                _banners.OnLoaded(BannerPlacement.Menu, false);
                Tick(59f);
                Assert.AreEqual(i, LoadsOf(BannerPlacement.Menu));
                Tick(1.5f);
                Assert.AreEqual(i + 1, LoadsOf(BannerPlacement.Menu));
            }
        }

        [Test]
        public void SuccessResetsThePause()
        {
            _banners.Show(BannerPlacement.Menu);
            _banners.OnLoaded(BannerPlacement.Menu, false);
            Tick(61f);
            _banners.OnLoaded(BannerPlacement.Menu, true);
            Tick(3600f);
            Assert.AreEqual(2, LoadsOf(BannerPlacement.Menu), "после удачной загрузки своих повторов нет, обновляет SDK");
        }

        [Test]
        public void RetryWaitsOnlyWhileVisibleAndInForeground()
        {
            _banners.Show(BannerPlacement.Menu);
            _banners.OnLoaded(BannerPlacement.Menu, false);
            Tick(30f);
            _banners.Show(BannerPlacement.None);
            Tick(600f);
            _banners.Show(BannerPlacement.Menu);
            Tick(600f, active: false);
            Assert.AreEqual(1, LoadsOf(BannerPlacement.Menu));
            Tick(29f);
            Assert.AreEqual(1, LoadsOf(BannerPlacement.Menu));
            Tick(1.5f);
            Assert.AreEqual(2, LoadsOf(BannerPlacement.Menu));
        }

        [Test]
        public void HugeFrameAfterResumeDoesNotCount()
        {
            _banners.Show(BannerPlacement.Menu);
            _banners.OnLoaded(BannerPlacement.Menu, false);
            _banners.Tick(3600f, true);
            Assert.AreEqual(1, LoadsOf(BannerPlacement.Menu));
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
        public void SameUnit_MenuAndGameShareOneBanner()
        {
            Assert.AreEqual(BannerPlacement.Menu, BannerRules.SharedWhenSameUnit(BannerPlacement.Game, true));
            Assert.AreEqual(BannerPlacement.Menu, BannerRules.SharedWhenSameUnit(BannerPlacement.Menu, true));
            Assert.AreEqual(BannerPlacement.Game, BannerRules.SharedWhenSameUnit(BannerPlacement.Game, false));
            Assert.AreEqual(BannerPlacement.None, BannerRules.SharedWhenSameUnit(BannerPlacement.None, true));
        }

        [Test]
        public void DefaultConfig_GameBannerUsesMenuUnit()
        {
            // В кабинете один баннерный блок, поэтому по умолчанию меню и игра делят один баннер.
            var config = UnityEngine.ScriptableObject.CreateInstance<AdsConfig>();
            Assert.AreEqual(config.BannerIdFor(BannerPlacement.Menu, false), config.BannerIdFor(BannerPlacement.Game, false));
            Assert.AreEqual(config.BannerIdFor(BannerPlacement.Menu, true), config.BannerIdFor(BannerPlacement.Game, true));
            UnityEngine.Object.DestroyImmediate(config);
        }
    }
}
