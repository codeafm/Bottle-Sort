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
        [Tooltip("Отдельный блок для баннера в игре, если нужна раздельная статистика. Пусто — тот же, что в меню.")]
        public string yandexGameBanner = "";

        // Всегда настоящие блоки из кабинета, демо-блоки заказчик не хочет даже в dev-сборках.
        public string BannerId => yandexBanner;
        public string BannerIdFor(BannerPlacement placement) =>
            placement == BannerPlacement.Game && !string.IsNullOrEmpty(yandexGameBanner) ? yandexGameBanner : yandexBanner;
        public string InterstitialId => yandexInterstitial;
        public string RewardedId => yandexRewarded;

        public static AdsConfig Load()
        {
            var config = Resources.Load<AdsConfig>(ResourcePath);
            return config != null ? config : CreateInstance<AdsConfig>();
        }
    }
}
