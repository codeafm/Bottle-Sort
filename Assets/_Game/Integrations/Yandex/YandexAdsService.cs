using System;
using BottleSort.Services;
using UnityEngine;
using YandexMobileAds;
using YandexMobileAds.Base;

namespace BottleSort.Integrations
{
    // Yandex Mobile Ads 8.6 (Assets/YandexMobileAds). Плагин без asmdef и живёт в Assembly-CSharp,
    // поэтому эта папка тоже вне Game.Runtime и подключается к Ads через DeviceServiceFactory.
    // Unity Ads показывается через адаптер медиации Yandex, отдельного кода для него нет.
    // Как во Flutter (ad_cache.dart): объявление живёт 30 минут (FullscreenAdSlot), после ошибки повтор через 30 с.
    public sealed class YandexAdsService : IAdsService
    {
        const float RetrySeconds = 30f;
        const int BannerWidth = 320;
        const int BannerHeight = 50;

        readonly MainThread _thread;
        readonly AdsConfig _config;

        InterstitialAdLoader _interstitialLoader;
        RewardedAdLoader _rewardedLoader;
        Interstitial _interstitial;
        RewardedAd _rewarded;
        readonly FullscreenAdSlot _interstitialSlot = new FullscreenAdSlot();
        readonly FullscreenAdSlot _rewardedSlot = new FullscreenAdSlot();
        // Свой баннер у меню и у игры. Next — новый, который грузится скрытым, пока на экране старый.
        sealed class BannerSlot
        {
            public Banner Current;
            public Banner Next;
        }

        readonly BannerSlot _menuBanner = new BannerSlot();
        readonly BannerSlot _gameBanner = new BannerSlot();
        BannerPlacement _visible;

#if !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => Ads.DeviceServiceFactory = (thread, config) => new YandexAdsService(thread, config);
#endif

        public YandexAdsService(MainThread thread, AdsConfig config)
        {
            _thread = thread;
            _config = config;
        }

        public bool IsInterstitialReady => _interstitial != null && _interstitialSlot.IsFresh(Now);
        public bool IsRewardedReady => _rewarded != null && _rewardedSlot.IsFresh(Now);

        public void Initialize()
        {
            _interstitialLoader = new InterstitialAdLoader();
            _rewardedLoader = new RewardedAdLoader();
            LoadInterstitial();
            LoadRewarded();
        }

        static double Now => (DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds;

        // Зовётся при каждой проверке и раз в 10 секунд из Ads: протухший ролик выбрасываем и грузим новый,
        // свежий или уже грузящийся не трогаем. Колбэки загрузчиков плагин сам отдаёт в главном потоке.
        public void LoadInterstitial()
        {
            if (!_interstitialSlot.NeedsLoad(Now, out bool stale))
                return;
            if (stale || _interstitial != null)
            {
                Debug.Log("[Ads] Interstitial expired, loading a new one");
                _interstitial?.Destroy();
                _interstitial = null;
            }
            _interstitialSlot.OnLoadStarted();
            _interstitialLoader.LoadAd(new AdRequest(_config.InterstitialId),
                ad =>
                {
                    _interstitial?.Destroy();
                    _interstitial = ad;
                    _interstitialSlot.OnLoaded(Now);
                    Debug.Log("[Ads] Interstitial loaded");
                },
                error =>
                {
                    Debug.Log($"[Ads] Interstitial failed to load: {error.Message}");
                    _interstitialSlot.OnFailed();
                    _thread.After(RetrySeconds, LoadInterstitial);
                });
        }

        public void LoadRewarded()
        {
            if (!_rewardedSlot.NeedsLoad(Now, out bool stale))
                return;
            if (stale || _rewarded != null)
            {
                Debug.Log("[Ads] Rewarded expired, loading a new one");
                _rewarded?.Destroy();
                _rewarded = null;
            }
            _rewardedSlot.OnLoadStarted();
            _rewardedLoader.LoadAd(new AdRequest(_config.RewardedId),
                ad =>
                {
                    _rewarded?.Destroy();
                    _rewarded = ad;
                    _rewardedSlot.OnLoaded(Now);
                    Debug.Log("[Ads] Rewarded loaded");
                },
                error =>
                {
                    Debug.Log($"[Ads] Rewarded failed to load: {error.Message}");
                    _rewardedSlot.OnFailed();
                    _thread.After(RetrySeconds, LoadRewarded);
                });
        }

        public void ShowInterstitial(Action done)
        {
            if (!IsInterstitialReady)
            {
                LoadInterstitial();
                done?.Invoke();
                return;
            }

            var ad = _interstitial;
            _interstitial = null;
            _interstitialSlot.OnTaken();
            bool finished = false;
            void Finish()
            {
                if (finished)
                    return;
                finished = true;
                ad.Destroy();
                LoadInterstitial();
                done?.Invoke();
            }
            ad.OnAdDismissed += (sender, args) => _thread.Post(Finish);
            ad.OnAdFailedToShow += (sender, args) => _thread.Post(Finish);
            ad.Show();
        }

        // Награда — только если сеть прислала OnRewarded; закрыл раньше — ничего.
        public void ShowRewarded(Action<bool> done)
        {
            if (!IsRewardedReady)
            {
                LoadRewarded();
                done?.Invoke(false);
                return;
            }

            var ad = _rewarded;
            _rewarded = null;
            _rewardedSlot.OnTaken();
            bool earned = false;
            bool finished = false;
            void Finish()
            {
                if (finished)
                    return;
                finished = true;
                ad.Destroy();
                LoadRewarded();
                done?.Invoke(earned);
            }
            ad.OnRewarded += (sender, reward) => _thread.Post(() => earned = true);
            // OnRewarded может прийти чуть позже OnAdDismissed, поэтому итог подводим через кадр.
            ad.OnAdDismissed += (sender, args) => _thread.Post(() => _thread.After(0.1f, Finish));
            ad.OnAdFailedToShow += (sender, args) => _thread.Post(Finish);
            ad.Show();
        }

        public void ShowBanner(BannerPlacement placement)
        {
            _visible = placement;
            ApplyBannerVisibility();
        }

        public void HideBanner()
        {
            _visible = BannerPlacement.None;
            ApplyBannerVisibility();
        }

        // Новый баннер грузится скрытым и встаёт на место старого только после загрузки:
        // нет пустого места между баннерами, а при ошибке остаётся старый.
        public void LoadBanner(BannerPlacement placement, Action<bool> done)
        {
            var slot = SlotOf(placement);
            if (slot == null || slot.Next != null)
            {
                done?.Invoke(false);
                return;
            }
            Banner next = null;
            next = CreateBanner(placement, () =>
            {
                if (slot.Next != next)
                    return;
                slot.Next = null;
                var old = slot.Current;
                slot.Current = next;
                ApplyBannerVisibility();
                old?.Destroy();
                done?.Invoke(true);
            }, () =>
            {
                if (slot.Next != next)
                    return;
                slot.Next = null;
                next.Destroy();
                done?.Invoke(false);
            });
            slot.Next = next;
        }

        void ApplyBannerVisibility()
        {
            foreach (var (placement, slot) in new[] { (BannerPlacement.Menu, _menuBanner), (BannerPlacement.Game, _gameBanner) })
            {
                if (slot.Current == null)
                    continue;
                if (placement == _visible)
                    slot.Current.Show();
                else
                    slot.Current.Hide();
            }
        }

        BannerSlot SlotOf(BannerPlacement placement) =>
            placement == BannerPlacement.Menu ? _menuBanner : placement == BannerPlacement.Game ? _gameBanner : null;

        Banner CreateBanner(BannerPlacement placement, Action loaded, Action failed)
        {
            // Не выше 50 при ширине 320, как место под баннер в макете меню и игры. Yandex прижимает его
            // к низу safe area, а кнопки ui начинаются на 71 pt выше неё, так что баннер их не закрывает.
            // Fixed не годится: плагин 8.6.0 зовёт на Android BannerAdSize.fixedSize, а в SDK 8.6.0 метод
            // уже называется fixed, и баннер падает с NoSuchMethodError. Inline в SDK есть под тем же именем.
            var banner = new Banner(BannerAdSize.Inline(BannerWidth, BannerHeight), AdPosition.BottomCenter);
            banner.Hide();
            banner.OnAdLoaded += (sender, args) => _thread.Post(loaded);
            banner.OnAdFailedToLoad += (sender, args) => _thread.Post(() =>
            {
                Debug.Log($"[Ads] Banner {placement} failed to load: {args.Message}");
                failed();
            });
            banner.LoadAd(new AdRequest(_config.BannerIdFor(placement)));
            return banner;
        }
    }
}
