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
        static bool _sharedBanner;
        // SDK стартует после ответа на вопрос iOS о трекинге (TrackingConsent); до этого ничего не грузим.
        static bool _started;
        static ScreenId _screen = ScreenId.Splash;
        static BannerPlacement _bannerShown;
        static bool _busy;

        public static IAdsService Service => _service;

        // Реализацию Yandex регистрирует Integrations/Yandex: плагин без asmdef лежит в Assembly-CSharp,
        // и из Game.Runtime на него не сослаться.
        public static Func<MainThread, AdsConfig, IAdsService> DeviceServiceFactory;
        public static bool IsShowing => _busy;

        // Сервис, у которого уже можно грузить и показывать; до старта SDK — null.
        static IAdsService Started => _started ? _service : null;

        // На iOS вопрос о трекинге задаём через секунду после запуска, поверх заставки: меню с баннером
        // и первые уровни идут после неё, так что к ним реклама уже грузится с ответом игрока.
        const float TrackingQuestionDelay = 1f;

        public static void Initialize(MainThread thread)
        {
            _thread = thread;
            var config = AdsConfig.Load();
            _sharedBanner = config.BannerIdFor(BannerPlacement.Game) == config.BannerIdFor(BannerPlacement.Menu);
            _service = CreateService(thread, config);
            _started = false;
            _bannerShown = BannerPlacement.None;
            _banners = new BannerScheduler(placement =>
                _service.LoadBanner(placement, loaded =>
                {
                    Debug.Log($"[Ads] Banner {placement}: {(loaded ? "loaded" : "failed, keeping the old one")}");
                    _banners.OnLoaded(placement, loaded);
                }));
            RewardedAds.Handler = ShowRewarded;
            // Пока идёт межстраничная, ролик за награду показать нельзя — кнопка скажет об этом, а не промолчит.
            RewardedAds.IsReady = () => _service != null && !_busy && _service.IsRewardedReady;
            RewardedAds.Prepare = () => Started?.LoadRewarded();
            ScreenRouter.ScreenShown -= OnScreenShown;
            ScreenRouter.ScreenShown += OnScreenShown;
            PlayerData.Changed -= RefreshBanner;
            PlayerData.Changed += RefreshBanner;
            thread.Ticked -= Tick;
            thread.Ticked += Tick;
            TrackingConsent.Request(thread, TrackingQuestionDelay, () =>
            {
                _service.Initialize();
                _started = true;
                KeepFullscreenAdsLoaded();
                RefreshBanner();
            });
        }

        // Как часто проверять, что полноэкранные ролики загружены и не протухли.
        // Повтор после ошибки решает FullscreenAdSlot: чаще его паузы запрос всё равно не уйдёт.
        const float KeepFreshEvery = 10f;
        static float _keepFreshIn;
        static bool _wasFocused = true;

        static void Tick(float deltaTime)
        {
            bool focused = Application.isFocused;
            // Повтор баннера после ошибки ждёт, только пока баннер на экране и приложение на переднем плане.
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
            var service = Started;
            if (service == null || _busy)
                return;
            if (InterstitialPolicy.MayEverShow(PlayerData.Current.highestUnlocked, PlayerData.Current.noAds))
                service.LoadInterstitial();
            service.LoadRewarded();
        }

        // В редакторе заглушка. На устройстве только Yandex: другие сети подключены адаптерами
        // медиации внутри Yandex и настраиваются в кабинете, коду о них знать не нужно.
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
        // placement — какая кнопка предложила ролик, по-русски: так он и попадёт в аналитику.
        public static void ShowRewarded(string placement, Action<bool> done)
        {
            var service = Started;
            if (service == null || _busy || !service.IsRewardedReady)
            {
                if (service != null && !_busy)
                    service.LoadRewarded();
                Analytics.RewardedAd(placement, "Не загрузилась");
                done?.Invoke(false);
                return;
            }
            SetBusy(true);
            AudioListener.pause = true;
            service.ShowRewarded(watched =>
            {
                AudioListener.pause = false;
                SetBusy(false);
                Analytics.RewardedAd(placement, watched ? "Досмотрел" : "Закрыл раньше");
                done?.Invoke(watched);
            });
        }

        // Вызывать на «Дальше» после победы. then вызывается всегда: после рекламы или сразу.
        public static void ShowInterstitialIfDue(int level, LevelType type, Action then) => ShowInterstitial("Победа", level, then);

        // Вызывать, когда после проигрыша игрок выбрал «Заново».
        public static void ShowInterstitialAfterLossIfDue(int level, Action then) => ShowInterstitial("Проигрыш", level, then);

        // Межстраничная после любого уровня, но не чаще раза в минуту (InterstitialPolicy).
        static void ShowInterstitial(string after, int level, Action then)
        {
            var service = Started;
            float now = Time.realtimeSinceStartup;
            var decision = Policy.Decide(level, PlayerData.Current.noAds, now);
            string skip = decision == InterstitialDecision.NotDue ? $"not due, {Policy.SecondsUntilDue(now):0} s left"
                : decision == InterstitialDecision.Tutorial ? "tutorial"
                : decision == InterstitialDecision.NoAds ? "no ads"
                : service == null ? "no service"
                : _busy ? "busy"
                : !service.IsInterstitialReady ? "not ready"
                : null;
            if (skip != null)
            {
                Debug.Log($"[Ads] Interstitial after {after} skipped: {skip}");
                // Должна была выйти, но ролика нет — это упущенный показ, его видно в аналитике.
                if (skip == "not ready")
                {
                    Analytics.InterstitialAd(after, "Не загрузилась", level);
                    service.LoadInterstitial();
                }
                then?.Invoke();
                return;
            }

            Debug.Log($"[Ads] Interstitial after {after}: showing");
            Analytics.InterstitialAd(after, "Показана", level);
            SetBusy(true);
            AudioListener.pause = true;
            service.ShowInterstitial(() =>
            {
                // Минута до следующей — от закрытия рекламы: у игрока точно будет минута игры.
                Policy.OnShown(Time.realtimeSinceStartup);
                AudioListener.pause = false;
                SetBusy(false);
                then?.Invoke();
            });
        }

        // Доход с показа — его присылает SDK, сюда приходит сырой JSON Yandex.
        public static void ReportImpression(string format, string rawData)
        {
            var impression = AdImpression.Parse(format, rawData);
            Analytics.AdImpression(impression);
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
            RefreshBanner();
        }

        static void RefreshBanner()
        {
            var service = Started;
            if (service == null)
                return;
            bool wanted = BannerRules.ShouldShow(new BannerContext
            {
                Screen = _screen,
                FullscreenAdShowing = _busy,
                NoAds = PlayerData.Current.noAds
            });
            var placement = wanted
                ? BannerRules.SharedWhenSameUnit(BannerRules.PlacementOf(_screen), _sharedBanner)
                : BannerPlacement.None;
            // Меняем только при настоящем переходе: Меню ↔ Карта ↔ Магазин — один и тот же баннер меню.
            if (placement == _bannerShown)
                return;
            _bannerShown = placement;
            _banners.Show(placement);
            if (placement == BannerPlacement.None)
                service.HideBanner();
            else
                service.ShowBanner(placement);
        }
    }
}
