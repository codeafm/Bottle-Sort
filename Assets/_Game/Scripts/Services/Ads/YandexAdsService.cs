#if YANDEX_MOBILE_ADS && !UNITY_EDITOR
using System;
using UnityEngine;
using YandexMobileAds;
using YandexMobileAds.Base;

namespace BottleSort.Services
{
    // Yandex Mobile Ads Unity plugin 7.x. Включается символом YANDEX_MOBILE_ADS в Player Settings
    // после импорта плагина; без него собирается EditorAdsService.
    // Поведение как во Flutter (ad_cache.dart): объявление живёт 30 минут, после ошибки повтор через 30 с.
    public sealed class YandexAdsService : IAdsService
    {
        const float RetrySeconds = 30f;
        const float CacheLifetime = 30f * 60f;

        readonly MainThread _thread;
        readonly AdsConfig _config;

        InterstitialAdLoader _interstitialLoader;
        RewardedAdLoader _rewardedLoader;
        Interstitial _interstitial;
        RewardedAd _rewarded;
        float _interstitialLoadedAt;
        float _rewardedLoadedAt;
        bool _interstitialLoading;
        bool _rewardedLoading;
        Banner _banner;
        bool _bannerWanted;

        public YandexAdsService(MainThread thread, AdsConfig config)
        {
            _thread = thread;
            _config = config;
        }

        public bool IsInterstitialReady => _interstitial != null && Fresh(_interstitialLoadedAt);
        public bool IsRewardedReady => _rewarded != null && Fresh(_rewardedLoadedAt);

        public void Initialize()
        {
            _interstitialLoader = new InterstitialAdLoader();
            _interstitialLoader.OnAdLoaded += (sender, args) => _thread.Post(() =>
            {
                _interstitialLoading = false;
                _interstitial?.Destroy();
                _interstitial = args.Interstitial;
                _interstitialLoadedAt = Time.realtimeSinceStartup;
            });
            _interstitialLoader.OnAdFailedToLoad += (sender, args) => _thread.Post(() =>
            {
                _interstitialLoading = false;
                _thread.After(RetrySeconds, LoadInterstitial);
            });

            _rewardedLoader = new RewardedAdLoader();
            _rewardedLoader.OnAdLoaded += (sender, args) => _thread.Post(() =>
            {
                _rewardedLoading = false;
                _rewarded?.Destroy();
                _rewarded = args.RewardedAd;
                _rewardedLoadedAt = Time.realtimeSinceStartup;
            });
            _rewardedLoader.OnAdFailedToLoad += (sender, args) => _thread.Post(() =>
            {
                _rewardedLoading = false;
                _thread.After(RetrySeconds, LoadRewarded);
            });

            LoadInterstitial();
            LoadRewarded();
        }

        static bool Fresh(float loadedAt) => Time.realtimeSinceStartup - loadedAt < CacheLifetime;

        void LoadInterstitial()
        {
            if (_interstitialLoading || IsInterstitialReady)
                return;
            _interstitialLoading = true;
            _interstitialLoader.LoadAd(new AdRequestConfiguration.Builder(_config.yandexInterstitial).Build());
        }

        public void LoadRewarded()
        {
            if (_rewardedLoading || IsRewardedReady)
                return;
            _rewardedLoading = true;
            _rewardedLoader.LoadAd(new AdRequestConfiguration.Builder(_config.yandexRewarded).Build());
        }

        public void ShowInterstitial(Action done)
        {
            if (!IsInterstitialReady)
            {
                LoadInterstitial();
                done?.Invoke();
                return;
            }

            var ad = _interstitial;
            _interstitial = null;
            bool finished = false;
            void Finish()
            {
                if (finished)
                    return;
                finished = true;
                ad.Destroy();
                LoadInterstitial();
                done?.Invoke();
            }
            ad.OnAdDismissed += (sender, args) => _thread.Post(Finish);
            ad.OnAdFailedToShow += (sender, args) => _thread.Post(Finish);
            ad.Show();
        }

        public void ShowRewarded(Action<bool> done)
        {
            if (!IsRewardedReady)
            {
                LoadRewarded();
                done?.Invoke(false);
                return;
            }

            var ad = _rewarded;
            _rewarded = null;
            bool earned = false;
            bool finished = false;
            void Finish()
            {
                if (finished)
                    return;
                finished = true;
                ad.Destroy();
                LoadRewarded();
                done?.Invoke(earned);
            }
            ad.OnRewarded += (sender, reward) => _thread.Post(() => earned = true);
            ad.OnAdDismissed += (sender, args) => _thread.Post(Finish);
            ad.OnAdFailedToShow += (sender, args) => _thread.Post(Finish);
            ad.Show();
        }

        public void ShowBanner()
        {
            _bannerWanted = true;
            if (_banner != null)
            {
                _banner.Show();
                return;
            }

            float density = Screen.dpi > 0 ? Screen.dpi / 160f : 2f;
            int widthDp = Mathf.RoundToInt(Screen.safeArea.width / density);
            _banner = new Banner(_config.yandexBanner, BannerAdSize.StickySize(widthDp), AdPosition.BottomCenter);
            _banner.OnAdLoaded += (sender, args) => _thread.Post(() =>
            {
                if (_bannerWanted)
                    _banner.Show();
                else
                    _banner.Hide();
            });
            _banner.OnAdFailedToLoad += (sender, args) => _thread.Post(() =>
            {
                _banner?.Destroy();
                _banner = null;
                if (_bannerWanted)
                    _thread.After(RetrySeconds, ShowBanner);
            });
            _banner.LoadAd(new AdRequest.Builder().Build());
        }

        public void HideBanner()
        {
            _bannerWanted = false;
            _banner?.Hide();
        }
    }
}
#endif
