using UnityEngine;

namespace BottleSort.Services
{
    // Unit id Yandex. Лежит в Settings/Resources/AdsConfig.asset, правится в инспекторе.
    // Unity Ads и другие сети медиации настраиваются в кабинете Yandex, здесь их ключей нет.
    [CreateAssetMenu(menuName = "BottleSort/Ads Config", fileName = "AdsConfig")]
    public sealed class AdsConfig : ScriptableObject
    {
        public const string ResourcePath = "AdsConfig";

        [Header("Yandex Mobile Ads, Android (из Flutter-версии)")]
        public string yandexBanner = AdUnits.Banner;
        public string yandexInterstitial = AdUnits.Interstitial;
        public string yandexRewarded = AdUnits.Rewarded;
        [Tooltip("Отдельный блок для баннера в игре, если нужна раздельная статистика. Пусто — тот же, что в меню.")]
        public string yandexGameBanner = "";

        [Header("Yandex Mobile Ads, iOS")]
        public string iosBanner = AdUnits.IosBanner;
        public string iosInterstitial = AdUnits.IosInterstitial;
        public string iosRewarded = AdUnits.IosRewarded;
        [Tooltip("Отдельный блок для баннера в игре на iOS. Пусто — тот же, что в меню.")]
        public string iosGameBanner = "";

        // Всегда настоящие блоки из кабинета, демо-блоки заказчик не хочет даже в dev-сборках.
        // Блоки выбираются по платформе сборки: в iOS-сборку уходят только iOS-блоки.
        public string BannerIdFor(BannerPlacement placement) => BannerIdFor(placement, IsIos);

        public string BannerIdFor(BannerPlacement placement, bool ios)
        {
            string game = ios ? iosGameBanner : yandexGameBanner;
            if (placement == BannerPlacement.Game && !string.IsNullOrEmpty(game))
                return game;
            return ios ? iosBanner : yandexBanner;
        }

        public string BannerId => BannerIdFor(BannerPlacement.Menu);
        public string InterstitialId => InterstitialIdFor(IsIos);
        public string RewardedId => RewardedIdFor(IsIos);
        public string InterstitialIdFor(bool ios) => ios ? iosInterstitial : yandexInterstitial;
        public string RewardedIdFor(bool ios) => ios ? iosRewarded : yandexRewarded;

#if UNITY_IOS
        static bool IsIos => true;
#else
        static bool IsIos => false;
#endif

        public static AdsConfig Load()
        {
            var config = Resources.Load<AdsConfig>(ResourcePath);
            return config != null ? config : CreateInstance<AdsConfig>();
        }
    }
}
