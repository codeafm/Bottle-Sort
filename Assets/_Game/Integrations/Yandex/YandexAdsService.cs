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
    // Как во Flutter (ad_cache.dart): объявление живёт 30 минут. Когда грузить и когда повторять после
    // ошибки, решает FullscreenAdSlot, а звать Load* раз в 10 секунд — Ads; своих таймеров здесь нет.
    public sealed class YandexAdsService : IAdsService
    {
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

        // Первые загрузки запускает Ads: межстраничную — только если её вообще могут показать.
        public void Initialize()
        {
            _interstitialLoader = new InterstitialAdLoader();
            _rewardedLoader = new RewardedAdLoader();
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
                    _interstitialSlot.OnFailed(Now);
                    Debug.Log($"[Ads] Interstitial failed to load: {error.Message}");
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
                    _rewardedSlot.OnFailed(Now);
                    Debug.Log($"[Ads] Rewarded failed to load: {error.Message}");
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
            ad.OnAdImpression += (sender, data) => _thread.Post(() => Ads.ReportImpression("interstitial", data?.rawData));
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
            ad.OnAdImpression += (sender, data) => _thread.Post(() => Ads.ReportImpression("rewarded", data?.rawData));
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

        // Баннер плейсмента создаётся один раз, дальше SDK сам меняет в нём рекламу раз в 60 секунд.
        // Новый объект нужен только после ошибки (BannerScheduler): он грузится скрытым и встаёт
        // на место старого после загрузки, так что пустого места нет. Повторные OnAdLoaded того же
        // баннера при автообновлении сюда уже не доходят — проверка slot.Next != next их отсекает.
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
            // Классический 320×50: под него больше всего рекламы. У Last Tower фиксированный баннер даёт
            // fill rate 94 %, у адаптивного Inline во Flutter-версии Bottle Sort было 35 %. Ровно занимает
            // место под баннер в макете: Yandex прижимает его к низу safe area, кнопки ui на 71 pt выше.
            // Плагин 8.6.0 звал на Android fixedSize вместо fixed — исправлено в BannerAdSizeUtils.
            var banner = new Banner(BannerAdSize.Fixed(BannerWidth, BannerHeight), AdPosition.BottomCenter);
            banner.Hide();
            banner.OnImpression += (sender, data) => _thread.Post(() => Ads.ReportImpression("banner", data?.rawData));
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
