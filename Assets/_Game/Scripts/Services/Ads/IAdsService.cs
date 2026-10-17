using System;

namespace BottleSort.Services
{
    public interface IAdsService
    {
        bool IsRewardedReady { get; }
        bool IsInterstitialReady { get; }

        void Initialize();
        void LoadRewarded();

        // true — досмотрел и заслужил награду. Колбэк приходит в главном потоке.
        void ShowRewarded(Action<bool> done);
        void ShowInterstitial(Action done);

        void ShowBanner();
        void HideBanner();
    }

    // Unit id из Flutter-версии (FLUTTER_SPEC §5).
    public static class AdUnits
    {
        public const string Banner = "R-M-19370824-1";
        public const string Interstitial = "R-M-19370824-2";
        public const string Rewarded = "R-M-19370824-3";
    }
}
