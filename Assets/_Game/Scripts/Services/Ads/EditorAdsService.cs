using System;
using UnityEngine;

namespace BottleSort.Services
{
    // Реклама без SDK: в редакторе и в сборках, где Yandex Mobile Ads ещё не подключён.
    // Rewarded «досматривается» за полсекунды, interstitial только пишет в лог.
    public sealed class EditorAdsService : IAdsService
    {
        const float FakeAdSeconds = 0.5f;

        readonly MainThread _runner;

        // Для проверки веток «реклама не загрузилась» и «закрыл, не досмотрев».
        public static bool SimulateNoFill;
        public static bool SimulateSkip;

        public EditorAdsService(MainThread runner)
        {
            _runner = runner;
        }

        public bool IsRewardedReady => !SimulateNoFill;
        public bool IsInterstitialReady => !SimulateNoFill;

        public void Initialize() => Debug.Log("[Ads] Stub ads service, no SDK");

        public void LoadRewarded()
        {
        }

        public void ShowRewarded(Action<bool> done)
        {
            if (SimulateNoFill)
            {
                done?.Invoke(false);
                return;
            }
            Debug.Log("[Ads] Rewarded shown (stub)");
            _runner.After(FakeAdSeconds, () => done?.Invoke(!SimulateSkip));
        }

        public void ShowInterstitial(Action done)
        {
            Debug.Log("[Ads] Interstitial shown (stub)");
            _runner.After(FakeAdSeconds, () => done?.Invoke());
        }

        public void ShowBanner() => Debug.Log("[Ads] Banner on (stub)");
        public void HideBanner() => Debug.Log("[Ads] Banner off (stub)");
    }
}
