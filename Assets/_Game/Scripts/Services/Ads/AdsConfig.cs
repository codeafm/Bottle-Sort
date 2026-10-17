using UnityEngine;

namespace BottleSort.Services
{
    // Ключи рекламных сетей. Лежит в Settings/Resources/AdsConfig.asset, правится в инспекторе.
    // Пока ключи LevelPlay пустые, запасная сеть не включается и работает один Yandex.
    [CreateAssetMenu(menuName = "BottleSort/Ads Config", fileName = "AdsConfig")]
    public sealed class AdsConfig : ScriptableObject
    {
        public const string ResourcePath = "AdsConfig";

        [Header("Yandex Mobile Ads (из Flutter-версии)")]
        public string yandexBanner = AdUnits.Banner;
        public string yandexInterstitial = AdUnits.Interstitial;
        public string yandexRewarded = AdUnits.Rewarded;

        [Header("Unity Ads через LevelPlay: App Key и Ad Unit ID из кабинета LevelPlay")]
        public string levelPlayAppKeyAndroid = "";
        public string levelPlayAppKeyIos = "";
        public string levelPlayInterstitialAndroid = "";
        public string levelPlayInterstitialIos = "";
        public string levelPlayRewardedAndroid = "";
        public string levelPlayRewardedIos = "";

        [Tooltip("Тестовая реклама и проверка интеграции. В development-сборках включается всегда.")]
        public bool testMode;

        public bool UseTestMode => testMode || Debug.isDebugBuild;

        public string LevelPlayAppKey => ForPlatform(levelPlayAppKeyAndroid, levelPlayAppKeyIos);
        public string LevelPlayInterstitial => ForPlatform(levelPlayInterstitialAndroid, levelPlayInterstitialIos);
        public string LevelPlayRewarded => ForPlatform(levelPlayRewardedAndroid, levelPlayRewardedIos);

        public bool HasLevelPlayKeys => !string.IsNullOrEmpty(LevelPlayAppKey) &&
            (!string.IsNullOrEmpty(LevelPlayInterstitial) || !string.IsNullOrEmpty(LevelPlayRewarded));

        public static AdsConfig Load()
        {
            var config = Resources.Load<AdsConfig>(ResourcePath);
            return config != null ? config : CreateInstance<AdsConfig>();
        }

        static string ForPlatform(string android, string ios)
        {
#if UNITY_IOS
            return ios;
#else
            return android;
#endif
        }
    }
}
