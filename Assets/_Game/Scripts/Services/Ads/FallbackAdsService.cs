using System;

namespace BottleSort.Services
{
    // Две сети: основная (Yandex) и запасная (Unity Ads через LevelPlay). Полноэкранную рекламу показывает
    // основная, если у неё что-то загружено, иначе запасная. Баннер только у основной.
    // Награду решает колбэк той сети, которая показала. Частоту показов считает Ads один раз на обе сети.
    public sealed class FallbackAdsService : IAdsService
    {
        readonly IAdsService _primary;
        readonly IAdsService _fallback;

        public FallbackAdsService(IAdsService primary, IAdsService fallback)
        {
            _primary = primary ?? throw new ArgumentNullException(nameof(primary));
            _fallback = fallback;
        }

        public bool IsRewardedReady => _primary.IsRewardedReady || (_fallback?.IsRewardedReady ?? false);
        public bool IsInterstitialReady => _primary.IsInterstitialReady || (_fallback?.IsInterstitialReady ?? false);

        public void Initialize()
        {
            _primary.Initialize();
            _fallback?.Initialize();
        }

        public void LoadRewarded()
        {
            _primary.LoadRewarded();
            _fallback?.LoadRewarded();
        }

        public void ShowRewarded(Action<bool> done)
        {
            if (_primary.IsRewardedReady)
                _primary.ShowRewarded(done);
            else if (_fallback != null && _fallback.IsRewardedReady)
                _fallback.ShowRewarded(done);
            else
                done?.Invoke(false);
        }

        public void ShowInterstitial(Action done)
        {
            if (_primary.IsInterstitialReady)
                _primary.ShowInterstitial(done);
            else if (_fallback != null && _fallback.IsInterstitialReady)
                _fallback.ShowInterstitial(done);
            else
                done?.Invoke();
        }

        public void ShowBanner() => _primary.ShowBanner();
        public void HideBanner() => _primary.HideBanner();
    }
}
