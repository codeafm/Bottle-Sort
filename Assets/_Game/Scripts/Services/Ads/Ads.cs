using System;
using System.Collections;
using BottleSort.Core;
using BottleSort.UI;
using UnityEngine;

namespace BottleSort.Services
{
    // Точка входа для экранов: правила LEVEL_DESIGN §7 здесь, SDK — за IAdsService.
    public static class Ads
    {
        // Во Flutter rewarded ждали до 15 с, если он ещё грузился.
        const float RewardedWaitSeconds = 15f;
        static IAdsService _service;
        static MainThread _thread;
        static readonly InterstitialPolicy Policy = new InterstitialPolicy();
        static readonly BannerRefreshTimer BannerTimer = new BannerRefreshTimer();
        static ScreenId _screen = ScreenId.Splash;
        static int _screenLevel;
        static bool _popupOpen;
        static bool _bannerShown;
        static bool _busy;

        public static IAdsService Service => _service;

        // Реализацию Yandex регистрирует Integrations/Yandex: плагин без asmdef лежит в Assembly-CSharp,
        // и из Game.Runtime на него не сослаться.
        public static Func<MainThread, AdsConfig, IAdsService> DeviceServiceFactory;
        public static bool IsShowing => _busy;

        public static void Initialize(MainThread thread)
        {
            _thread = thread;
            _service = CreateService(thread, AdsConfig.Load());
            _service.Initialize();
            RewardedAds.Handler = ShowRewarded;
            ScreenRouter.ScreenShown -= OnScreenShown;
            ScreenRouter.ScreenShown += OnScreenShown;
            ScreenRouter.PopupsChanged -= OnPopupsChanged;
            ScreenRouter.PopupsChanged += OnPopupsChanged;
            PlayerData.Changed -= RefreshBanner;
            PlayerData.Changed += RefreshBanner;
            thread.Ticked -= TickBanner;
            thread.Ticked += TickBanner;
        }

        // Время баннера идёт, только пока он на экране и приложение на переднем плане.
        static void TickBanner(float deltaTime)
        {
            bool visible = _bannerShown && !_busy && Application.isFocused;
            if (BannerTimer.Tick(deltaTime, visible))
                _service.RefreshBanner(BannerTimer.OnRefreshed);
        }

        // В редакторе заглушка. На устройстве только Yandex: другие сети (Unity Ads) подключены
        // адаптерами медиации внутри Yandex и настраиваются в кабинете, коду о них знать не нужно.
        static IAdsService CreateService(MainThread thread, AdsConfig config)
        {
#if !UNITY_EDITOR
            if (DeviceServiceFactory != null)
                return DeviceServiceFactory(thread, config);
#endif
            return new EditorAdsService(thread);
        }

        public static void ShowRewarded(Action<bool> done)
        {
            if (_service == null || _busy)
            {
                done?.Invoke(false);
                return;
            }
            SetBusy(true);
            _thread.StartCoroutine(ShowRewardedWhenReady(watched =>
            {
                SetBusy(false);
                if (watched)
                    Policy.OnRewardedWatched();
                done?.Invoke(watched);
            }));
        }

        static IEnumerator ShowRewardedWhenReady(Action<bool> done)
        {
            if (!_service.IsRewardedReady)
            {
                _service.LoadRewarded();
                float waitUntil = Time.realtimeSinceStartup + RewardedWaitSeconds;
                while (!_service.IsRewardedReady && Time.realtimeSinceStartup < waitUntil)
                    yield return null;
            }
            if (!_service.IsRewardedReady)
            {
                done(false);
                yield break;
            }
            AudioListener.pause = true;
            _service.ShowRewarded(watched =>
            {
                AudioListener.pause = false;
                done(watched);
            });
        }

        public static void LevelStarted() => Policy.OnLevelStarted();
        public static void LevelCompleted(int level) => Policy.OnWin(level);
        public static void LevelLost(int level) => Policy.OnLoss(level);

        // Вызывать на «Дальше» после победы. then вызывается всегда: после рекламы или сразу.
        public static void ShowInterstitialIfDue(int level, LevelType type, Action then)
        {
            double now = Time.realtimeSinceStartupAsDouble;
            bool due = Policy.ShouldShowAfterWin(level, PlayerData.Current.noAds, now);
            ShowInterstitial(due, () => Policy.OnShownAfterWin(now), then);
        }

        // Вызывать, когда после проигрыша игрок выбрал «Заново».
        public static void ShowInterstitialAfterLossIfDue(int level, Action then)
        {
            double now = Time.realtimeSinceStartupAsDouble;
            bool due = Policy.ShouldShowAfterLoss(level, PlayerData.Current.noAds, now);
            ShowInterstitial(due, () => Policy.OnShownAfterLoss(now), then);
        }

        static void ShowInterstitial(bool due, Action shown, Action then)
        {
            if (!due || _service == null || _busy || !_service.IsInterstitialReady)
            {
                then?.Invoke();
                return;
            }

            SetBusy(true);
            shown();
            AudioListener.pause = true;
            _service.ShowInterstitial(() =>
            {
                AudioListener.pause = false;
                SetBusy(false);
                then?.Invoke();
            });
        }

        // Пока идёт полноэкранная реклама, баннер прячем, чтобы он не висел поверх неё.
        static void SetBusy(bool busy)
        {
            _busy = busy;
            RefreshBanner();
        }

        static void OnScreenShown(UiScreen screen, bool animated)
        {
            _screen = screen.Id;
            _screenLevel = screen is GameScreen game ? game.Level : 0;
            // Новая сцена начинает без попапов, старый роутер уже уничтожен.
            _popupOpen = screen.Router != null && screen.Router.HasPopup;
            RefreshBanner();
        }

        static void OnPopupsChanged(ScreenRouter router)
        {
            _popupOpen = router.HasPopup;
            RefreshBanner();
        }

        static void RefreshBanner()
        {
            if (_service == null)
                return;
            var data = PlayerData.Current;
            bool wanted = BannerRules.ShouldShow(new BannerContext
            {
                Screen = _screen,
                Level = _screenLevel,
                PopupOpen = _popupOpen,
                FullscreenAdShowing = _busy,
                HighestUnlocked = data.highestUnlocked,
                NoAds = data.noAds
            });
            // Меняем только при настоящем переходе: смена сцены Menu → Game баннер не дёргает.
            if (wanted == _bannerShown)
                return;
            _bannerShown = wanted;
            if (wanted)
                _service.ShowBanner();
            else
                _service.HideBanner();
        }
    }
}
