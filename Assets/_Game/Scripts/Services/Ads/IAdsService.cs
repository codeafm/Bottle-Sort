using System;

namespace BottleSort.Services
{
    public interface IAdsService
    {
        bool IsRewardedReady { get; }
        bool IsInterstitialReady { get; }

        void Initialize();
        // Загрузить, если ролика нет или он протух; свежий или грузящийся не трогать.
        void LoadInterstitial();
        // playerWaiting — игрок нажал кнопку «за рекламу», а ролика нет: грузить, не дожидаясь паузы после ошибок.
        void LoadRewarded(bool playerWaiting);

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

    // Unit id из кабинета Yandex. Android — те же, что во Flutter-версии (FLUTTER_SPEC §5).
    // У iOS-приложения свои блоки, и номера там идут в другом порядке: -1 у него награда, а не баннер.
    public static class AdUnits
    {
        public const string Banner = "R-M-19370824-1";
        public const string Interstitial = "R-M-19370824-2";
        public const string Rewarded = "R-M-19370824-3";

        public const string IosBanner = "R-M-20110699-2";
        public const string IosInterstitial = "R-M-20110699-3";
        public const string IosRewarded = "R-M-20110699-1";
    }
}
