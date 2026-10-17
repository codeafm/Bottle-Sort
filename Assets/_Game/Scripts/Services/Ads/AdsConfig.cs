using UnityEngine;

namespace BottleSort.Services
{
    // Unit id Yandex. Лежит в Settings/Resources/AdsConfig.asset, правится в инспекторе.
    // Unity Ads и другие сети медиации настраиваются в кабинете Yandex, здесь их ключей нет.
    [CreateAssetMenu(menuName = "BottleSort/Ads Config", fileName = "AdsConfig")]
    public sealed class AdsConfig : ScriptableObject
    {
        public const string ResourcePath = "AdsConfig";

        [Header("Yandex Mobile Ads (из Flutter-версии)")]
        public string yandexBanner = AdUnits.Banner;
        public string yandexInterstitial = AdUnits.Interstitial;
        public string yandexRewarded = AdUnits.Rewarded;

        [Tooltip("Тестовые объявления Yandex. В development-сборках включается всегда.")]
        public bool testMode;

        public bool UseTestMode => testMode || Debug.isDebugBuild;

        // Демо-блоки Yandex всегда отдают тестовую рекламу, в том числе от сетей медиации.
        public string BannerId => UseTestMode ? "demo-banner-yandex" : yandexBanner;
        public string InterstitialId => UseTestMode ? "demo-interstitial-yandex" : yandexInterstitial;
        public string RewardedId => UseTestMode ? "demo-rewarded-yandex" : yandexRewarded;

        public static AdsConfig Load()
        {
            var config = Resources.Load<AdsConfig>(ResourcePath);
            return config != null ? config : CreateInstance<AdsConfig>();
        }
    }
}
