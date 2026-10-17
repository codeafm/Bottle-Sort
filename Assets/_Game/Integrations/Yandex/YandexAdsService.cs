using System;
using BottleSort.Services;
using UnityEngine;
using YandexMobileAds;
using YandexMobileAds.Base;

namespace BottleSort.Integrations
{
    // Yandex Mobile Ads 8.6 (Assets/YandexMobileAds). Плагин без asmdef и живёт в Assembly-CSharp,
    // поэтому эта папка тоже вне Game.Runtime и подключается к Ads через DeviceServiceFactory.
    // Unity Ads показывается через адаптер медиации Yandex, отдельного кода для него нет.
    // Как во Flutter (ad_cache.dart): объявление живёт 30 минут, после ошибки повтор через 30 с.
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
        bool _bannerLoaded;
        bool _bannerWanted;

#if !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => Ads.DeviceServiceFactory = (thread, config) => new YandexAdsService(thread, config);
#endif

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
            _rewardedLoader = new RewardedAdLoader();
            LoadInterstitial();
            LoadRewarded();
        }

        static bool Fresh(float loadedAt) => Time.realtimeSinceStartup - loadedAt < CacheLifetime;

        // Колбэки загрузчиков плагин сам отдаёт в главном потоке.
        void LoadInterstitial()
        {
            if (_interstitialLoading || IsInterstitialReady)
                return;
            _interstitialLoading = true;
            _interstitialLoader.LoadAd(new AdRequest(_config.InterstitialId),
                ad =>
                {
                    _interstitialLoading = false;
                    _interstitial?.Destroy();
                    _interstitial = ad;
                    _interstitialLoadedAt = Time.realtimeSinceStartup;
                },
                error =>
                {
                    _interstitialLoading = false;
                    _thread.After(RetrySeconds, LoadInterstitial);
                });
        }

        public void LoadRewarded()
        {
            if (_rewardedLoading || IsRewardedReady)
                return;
            _rewardedLoading = true;
            _rewardedLoader.LoadAd(new AdRequest(_config.RewardedId),
                ad =>
                {
                    _rewardedLoading = false;
                    _rewarded?.Destroy();
                    _rewarded = ad;
                    _rewardedLoadedAt = Time.realtimeSinceStartup;
                },
                error =>
                {
                    _rewardedLoading = false;
                    _thread.After(RetrySeconds, LoadRewarded);
                });
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

        // Награда — только если сеть прислала OnRewarded; закрыл раньше — ничего.
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
            // OnRewarded может прийти чуть позже OnAdDismissed, поэтому итог подводим через кадр.
            ad.OnAdDismissed += (sender, args) => _thread.Post(() => _thread.After(0.1f, Finish));
            ad.OnAdFailedToShow += (sender, args) => _thread.Post(Finish);
            ad.Show();
        }

        public void ShowBanner()
        {
            _bannerWanted = true;
            if (_banner == null)
            {
                CreateBanner();
                return;
            }
            if (_bannerLoaded)
                _banner.Show();
        }

        public void HideBanner()
        {
            _bannerWanted = false;
            _banner?.Hide();
        }

        void CreateBanner()
        {
            int widthDp = ScreenUtils.ConvertPixelsToDp((int)Screen.safeArea.width);
            _banner = new Banner(BannerAdSize.Sticky(widthDp), AdPosition.BottomCenter);
            _banner.OnAdLoaded += (sender, args) => _thread.Post(() =>
            {
                _bannerLoaded = true;
                if (_bannerWanted)
                    _banner.Show();
                else
                    _banner.Hide();
            });
            _banner.OnAdFailedToLoad += (sender, args) => _thread.Post(() =>
            {
                _banner?.Destroy();
                _banner = null;
                _bannerLoaded = false;
                if (_bannerWanted)
                    _thread.After(RetrySeconds, () =>
                    {
                        if (_bannerWanted && _banner == null)
                            CreateBanner();
                    });
            });
            _banner.LoadAd(new AdRequest(_config.BannerId));
        }
    }
}
