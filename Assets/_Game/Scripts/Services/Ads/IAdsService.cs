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

        // Видимым становится баннер этого плейсмента (если уже загружен), другой прячется.
        void ShowBanner(BannerPlacement placement);
        void HideBanner();
        // Новый баннер в плейсмент: первый раз создать, дальше заменить показанный после загрузки.
        // done(false) — не загрузился, на экране остался старый.
        void LoadBanner(BannerPlacement placement, Action<bool> done);
    }

    // Unit id из Flutter-версии (FLUTTER_SPEC §5).
    public static class AdUnits
    {
        public const string Banner = "R-M-19370824-1";
        public const string Interstitial = "R-M-19370824-2";
        public const string Rewarded = "R-M-19370824-3";
    }
}
