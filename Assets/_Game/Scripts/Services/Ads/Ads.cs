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
        // Реклама показывается только после обучения (§3.4): ни баннера, ни interstitial на 1–3.
        const int FirstLevelWithBanner = 4;

        static IAdsService _service;
        static MainThread _thread;
        static readonly InterstitialPolicy Policy = new InterstitialPolicy();
        static bool _bannerScreen;
        static bool _bannerShown;
        static bool _busy;

        public static IAdsService Service => _service;
        public static bool IsShowing => _busy;

        public static void Initialize(MainThread thread)
        {
            _thread = thread;
            _service = CreateService(thread, AdsConfig.Load());
            _service.Initialize();
            RewardedAds.Handler = ShowRewarded;
            ScreenRouter.ScreenShown -= OnScreenShown;
            ScreenRouter.ScreenShown += OnScreenShown;
            PlayerData.Changed -= RefreshBanner;
            PlayerData.Changed += RefreshBanner;
        }

        // В редакторе всегда заглушка. На устройстве Yandex основной, Unity Ads (LevelPlay) — запасной,
        // если пакет стоит и в AdsConfig вписаны ключи.
        static IAdsService CreateService(MainThread thread, AdsConfig config)
        {
#if UNITY_EDITOR
            return new EditorAdsService(thread);
#else
            IAdsService primary;
#if YANDEX_MOBILE_ADS
            primary = new YandexAdsService(thread, config);
#else
            primary = new EditorAdsService(thread);
#endif
            IAdsService fallback = null;
#if BOTTLESORT_LEVELPLAY
            if (config.HasLevelPlayKeys)
                fallback = new LevelPlayAdsService(thread, config);
#endif
            return new FallbackAdsService(primary, fallback);
#endif
        }

        public static void ShowRewarded(Action<bool> done)
        {
            if (_service == null || _busy)
            {
                done?.Invoke(false);
                return;
            }
            _busy = true;
            _thread.StartCoroutine(ShowRewardedWhenReady(watched =>
            {
                _busy = false;
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

        public static void LevelCompleted() => Policy.OnLevelCompleted();

        // Вызывать на «Дальше» после победы. then вызывается всегда: после рекламы или сразу.
        public static void ShowInterstitialIfDue(int level, LevelType type, Action then)
        {
            var data = PlayerData.Current;
            double now = Time.realtimeSinceStartupAsDouble;
            if (_service == null || _busy || !_service.IsInterstitialReady || !Policy.ShouldShow(level, type, data.noAds, now))
            {
                then?.Invoke();
                return;
            }

            _busy = true;
            Policy.OnShown(now);
            AudioListener.pause = true;
            _service.ShowInterstitial(() =>
            {
                AudioListener.pause = false;
                _busy = false;
                then?.Invoke();
            });
        }

        static void OnScreenShown(UiScreen screen, bool animated)
        {
            // Баннер только на карте уровней: в игре и в меню низ экрана занят кнопками.
            _bannerScreen = screen.Id == ScreenId.Levels;
            RefreshBanner();
        }

        static void RefreshBanner()
        {
            if (_service == null)
                return;
            var data = PlayerData.Current;
            bool wanted = _bannerScreen && !data.noAds && data.highestUnlocked >= FirstLevelWithBanner;
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
