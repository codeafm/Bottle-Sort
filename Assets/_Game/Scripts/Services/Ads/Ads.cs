using System;
using BottleSort.Core;
using BottleSort.UI;
using UnityEngine;

namespace BottleSort.Services
{
    // Точка входа для экранов: правила LEVEL_DESIGN §7 здесь, SDK — за IAdsService.
    public static class Ads
    {
        static IAdsService _service;
        static MainThread _thread;
        static readonly InterstitialPolicy Policy = new InterstitialPolicy();
        static BannerScheduler _banners;
        static ScreenId _screen = ScreenId.Splash;
        static int _screenLevel;
        static bool _popupOpen;
        static BannerPlacement _bannerShown;
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
            _bannerShown = BannerPlacement.None;
            _banners = new BannerScheduler(placement =>
                _service.LoadBanner(placement, loaded =>
                {
                    Debug.Log($"[Ads] Banner {placement}: {(loaded ? "new one shown" : "failed, keeping the old one")}");
                    _banners.OnLoaded(placement, loaded);
                }));
            RewardedAds.Handler = ShowRewarded;
            RewardedAds.IsReady = () => _service != null && _service.IsRewardedReady;
            RewardedAds.Prepare = () => _service?.LoadRewarded();
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
        static void TickBanner(float deltaTime) => _banners?.Tick(deltaTime, !_busy && Application.isFocused);

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

        // Не ждём, пока ролик догрузится: кнопки сами проверяют RewardedAds.EnsureReady и говорят игроку,
        // а сюда без готового ролика приходит только редкий случай — тогда просто «не досмотрел».
        public static void ShowRewarded(Action<bool> done)
        {
            if (_service == null || _busy)
            {
                done?.Invoke(false);
                return;
            }
            if (!_service.IsRewardedReady)
            {
                _service.LoadRewarded();
                done?.Invoke(false);
                return;
            }
            SetBusy(true);
            AudioListener.pause = true;
            _service.ShowRewarded(watched =>
            {
                AudioListener.pause = false;
                SetBusy(false);
                if (watched)
                    Policy.OnRewardedWatched();
                done?.Invoke(watched);
            });
        }

        // Новый уровень, «Следующий» или «Заново»: игровому баннеру — новый запрос.
        public static void LevelStarted(int level)
        {
            Policy.OnLevelStarted();
            var data = PlayerData.Current;
            if (_banners != null && BannerRules.AllowedOnLevel(level, data.highestUnlocked, data.noAds))
                _banners.LevelStarted();
        }
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
            var placement = wanted ? BannerRules.PlacementOf(_screen) : BannerPlacement.None;
            // Меняем только при настоящем переходе: Меню ↔ Карта — один и тот же баннер меню.
            if (placement == _bannerShown)
                return;
            _bannerShown = placement;
            _banners.Show(placement);
            if (placement == BannerPlacement.None)
                _service.HideBanner();
            else
                _service.ShowBanner(placement);
        }
    }
}
