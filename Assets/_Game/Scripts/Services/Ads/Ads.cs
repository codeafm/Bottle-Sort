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
            thread.Ticked -= Tick;
            thread.Ticked += Tick;
        }

        // Как часто проверять, что полноэкранные ролики загружены и не протухли.
        const float KeepFreshEvery = 10f;
        static float _keepFreshIn;
        static bool _wasFocused = true;

        static void Tick(float deltaTime)
        {
            bool focused = Application.isFocused;
            // Время баннера идёт, только пока он на экране и приложение на переднем плане.
            _banners?.Tick(deltaTime, !_busy && focused);

            // Вернулись из фона (там ролик мог протухнуть) — проверяем сразу, иначе раз в 10 секунд.
            _keepFreshIn -= deltaTime;
            if (focused && (!_wasFocused || _keepFreshIn <= 0f))
            {
                _keepFreshIn = KeepFreshEvery;
                KeepFullscreenAdsLoaded();
            }
            _wasFocused = focused;
        }

        static void KeepFullscreenAdsLoaded()
        {
            if (_service == null || _busy)
                return;
            _service.LoadInterstitial();
            _service.LoadRewarded();
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
                // Ролик за награду не отменяет межстраничную, только сдвигает 30 секунд между ними.
                Policy.OnRewardedWatched(Time.realtimeSinceStartupAsDouble);
                done?.Invoke(watched);
            });
        }

        // Новый уровень, «Следующий» или «Заново»: игровому баннеру — новый запрос.
        public static void LevelStarted(int level)
        {
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
            var decision = Policy.DecideAfterWin(level, PlayerData.Current.noAds, now);
            ShowInterstitial("win", decision, () => Policy.OnShownAfterWin(now), then);
        }

        // Вызывать, когда после проигрыша игрок выбрал «Заново».
        public static void ShowInterstitialAfterLossIfDue(int level, Action then)
        {
            double now = Time.realtimeSinceStartupAsDouble;
            var decision = Policy.DecideAfterLoss(level, PlayerData.Current.noAds, now);
            ShowInterstitial("loss", decision, () => Policy.OnShownAfterLoss(now), then);
        }

        static void ShowInterstitial(string after, InterstitialDecision decision, Action shown, Action then)
        {
            string skip = decision == InterstitialDecision.NotDue ? "not due"
                : decision == InterstitialDecision.Spacing ? "spacing"
                : decision == InterstitialDecision.Tutorial ? "tutorial"
                : decision == InterstitialDecision.NoAds ? "no ads"
                : _service == null ? "no service"
                : _busy ? "busy"
                : !_service.IsInterstitialReady ? "not ready"
                : null;
            if (skip != null)
            {
                Debug.Log($"[Ads] Interstitial after {after} skipped: {skip}");
                // Должна была выйти, но ролика нет — грузим сразу, к следующей возможности будет готов.
                if (skip == "not ready")
                    _service.LoadInterstitial();
                then?.Invoke();
                return;
            }

            Debug.Log($"[Ads] Interstitial after {after}: showing");
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
