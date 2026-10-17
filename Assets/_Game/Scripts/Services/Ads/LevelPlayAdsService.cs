#if BOTTLESORT_LEVELPLAY && !UNITY_EDITOR
using System;
using Unity.Services.LevelPlay;
using UnityEngine;

namespace BottleSort.Services
{
    // Запасная сеть: Unity Ads через LevelPlay (com.unity.services.levelplay). Прямой SDK Unity Ads
    // (com.unity.ads) с 2026 года для монетизации не поддерживается, поэтому только так.
    // Символ BOTTLESORT_LEVELPLAY ставит Game.Runtime.asmdef, когда пакет установлен.
    public sealed class LevelPlayAdsService : IAdsService
    {
        const float RetrySeconds = 30f;

        readonly MainThread _thread;
        readonly AdsConfig _config;
        LevelPlayInterstitialAd _interstitial;
        LevelPlayRewardedAd _rewarded;
        bool _ready;

        Action _interstitialDone;
        Action<bool> _rewardedDone;
        bool _earned;

        public LevelPlayAdsService(MainThread thread, AdsConfig config)
        {
            _thread = thread;
            _config = config;
        }

        public bool IsInterstitialReady => _ready && _interstitial != null && _interstitial.IsAdReady();
        public bool IsRewardedReady => _ready && _rewarded != null && _rewarded.IsAdReady();

        public void Initialize()
        {
            if (_config.UseTestMode)
                LevelPlay.SetMetaData("is_test_suite", "enable");
            LevelPlay.OnInitSuccess += configuration => _thread.Post(OnInitialized);
            LevelPlay.OnInitFailed += error => _thread.Post(() => Debug.LogWarning($"[Ads] LevelPlay init failed: {error}"));
            LevelPlay.Init(_config.LevelPlayAppKey);
        }

        void OnInitialized()
        {
            _ready = true;
            if (!string.IsNullOrEmpty(_config.LevelPlayInterstitial))
            {
                _interstitial = new LevelPlayInterstitialAd(_config.LevelPlayInterstitial);
                _interstitial.OnAdLoadFailed += error => _thread.Post(() => _thread.After(RetrySeconds, LoadInterstitial));
                _interstitial.OnAdClosed += info => _thread.Post(FinishInterstitial);
                _interstitial.OnAdDisplayFailed += (info, error) => _thread.Post(FinishInterstitial);
                LoadInterstitial();
            }
            if (!string.IsNullOrEmpty(_config.LevelPlayRewarded))
            {
                _rewarded = new LevelPlayRewardedAd(_config.LevelPlayRewarded);
                _rewarded.OnAdLoadFailed += error => _thread.Post(() => _thread.After(RetrySeconds, LoadRewarded));
                _rewarded.OnAdRewarded += (info, reward) => _thread.Post(() => _earned = true);
                _rewarded.OnAdClosed += info => _thread.Post(FinishRewarded);
                _rewarded.OnAdDisplayFailed += (info, error) => _thread.Post(FinishRewarded);
                LoadRewarded();
            }
        }

        void LoadInterstitial() => _interstitial?.LoadAd();

        public void LoadRewarded() => _rewarded?.LoadAd();

        public void ShowInterstitial(Action done)
        {
            if (!IsInterstitialReady)
            {
                done?.Invoke();
                return;
            }
            _interstitialDone = done;
            _interstitial.ShowAd();
        }

        public void ShowRewarded(Action<bool> done)
        {
            if (!IsRewardedReady)
            {
                done?.Invoke(false);
                return;
            }
            _earned = false;
            _rewardedDone = done;
            _rewarded.ShowAd();
        }

        void FinishInterstitial()
        {
            var done = _interstitialDone;
            _interstitialDone = null;
            LoadInterstitial();
            done?.Invoke();
        }

        // OnAdRewarded может прийти и после OnAdClosed, поэтому решаем на следующем кадре.
        void FinishRewarded()
        {
            _thread.After(0.1f, () =>
            {
                var done = _rewardedDone;
                _rewardedDone = null;
                LoadRewarded();
                done?.Invoke(_earned);
            });
        }

        // Баннер в этой игре только у Yandex.
        public void ShowBanner()
        {
        }

        public void HideBanner()
        {
        }
    }
}
#endif
