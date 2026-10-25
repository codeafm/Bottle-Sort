using System;
using BottleSort.Services;
using UnityEngine;
using YandexMobileAds;
using YandexMobileAds.Base;

namespace BottleSort.Integrations
{
    // Yandex Mobile Ads 8.6 (Assets/YandexMobileAds). Плагин без asmdef и живёт в Assembly-CSharp,
    // поэтому эта папка тоже вне Game.Runtime и подключается к Ads через DeviceServiceFactory.
    // Unity Ads и VK показываются через адаптеры медиации Yandex, отдельного кода для них нет.
    // Когда грузить полноэкранные ролики и когда повторять после ошибки, решает FullscreenAdSlot,
    // а звать Load* раз в секунду — Ads; своих таймеров здесь нет.
    public sealed class YandexAdsService : IAdsService
    {
        const int BannerWidth = 320;
        const int BannerHeight = 50;

        readonly MainThread _thread;
        readonly AdsConfig _config;

        FullscreenAd<Interstitial> _interstitial;
        FullscreenAd<RewardedAd> _rewarded;
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

        public bool IsInterstitialReady => _interstitial != null && _interstitial.IsReady;
        public bool IsRewardedReady => _rewarded != null && _rewarded.IsReady;

        // Первые загрузки запускает Ads: межстраничную — только если её вообще могут показать.
        // Загрузчик на каждый формат один на всю игру, как советует Yandex.
        public void Initialize()
        {
            var interstitialLoader = new InterstitialAdLoader();
            var rewardedLoader = new RewardedAdLoader();
            _interstitial = new FullscreenAd<Interstitial>("Interstitial", _config.InterstitialId, FullscreenAdSlot.Interstitial(),
                interstitialLoader.LoadAd, interstitialLoader.CancelLoading, ad => ad.Destroy());
            _rewarded = new FullscreenAd<RewardedAd>("Rewarded", _config.RewardedId, FullscreenAdSlot.Rewarded(),
                rewardedLoader.LoadAd, rewardedLoader.CancelLoading, ad => ad.Destroy());
        }

        static double Now => (DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds;

        public void LoadInterstitial() => _interstitial?.Load(false);

        public void LoadRewarded(bool playerWaiting) => _rewarded?.Load(playerWaiting);

        public void ShowInterstitial(Action done)
        {
            var ad = _interstitial?.Take();
            if (ad == null)
            {
                LoadInterstitial();
                done?.Invoke();
                return;
            }

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
            ad.OnAdFailedToShow += (sender, args) => _thread.Post(() =>
            {
                Debug.Log($"[Ads] Interstitial failed to show: {args.Message}");
                Finish();
            });
            ad.OnAdImpression += (sender, data) => _thread.Post(() => Ads.ReportImpression("interstitial", data?.rawData));
            ad.Show();
        }

        // Награда — только если сеть прислала OnRewarded; закрыл раньше — ничего.
        public void ShowRewarded(Action<bool> done)
        {
            var ad = _rewarded?.Take();
            if (ad == null)
            {
                LoadRewarded(true);
                done?.Invoke(false);
                return;
            }

            bool earned = false;
            bool finished = false;
            void Finish()
            {
                if (finished)
                    return;
                finished = true;
                ad.Destroy();
                LoadRewarded(false);
                done?.Invoke(earned);
            }
            ad.OnRewarded += (sender, reward) => _thread.Post(() => earned = true);
            // OnRewarded может прийти чуть позже OnAdDismissed, поэтому итог подводим через кадр.
            ad.OnAdDismissed += (sender, args) => _thread.Post(() => _thread.After(0.1f, Finish));
            ad.OnAdFailedToShow += (sender, args) => _thread.Post(() =>
            {
                Debug.Log($"[Ads] Rewarded failed to show: {args.Message}");
                Finish();
            });
            ad.OnAdImpression += (sender, data) => _thread.Post(() => Ads.ReportImpression("rewarded", data?.rawData));
            ad.Show();
        }

        // Один полноэкранный ролик: что загружено и какая загрузка сейчас идёт. Ответ на загрузку,
        // отменённую по таймауту, приходит со старым номером и выбрасывается, чтобы не встать поверх свежего.
        // Колбэки загрузчика плагин сам отдаёт в главном потоке.
        sealed class FullscreenAd<TAd> where TAd : class
        {
            readonly string _name;
            readonly string _unitId;
            readonly FullscreenAdSlot _slot;
            readonly Action<AdRequest, Action<TAd>, Action<AdFailedToLoadEventArgs>> _load;
            readonly Action _cancelLoading;
            readonly Action<TAd> _destroy;
            TAd _ad;
            int _request;

            public FullscreenAd(string name, string unitId, FullscreenAdSlot slot,
                Action<AdRequest, Action<TAd>, Action<AdFailedToLoadEventArgs>> load, Action cancelLoading, Action<TAd> destroy)
            {
                _name = name;
                _unitId = unitId;
                _slot = slot;
                _load = load;
                _cancelLoading = cancelLoading;
                _destroy = destroy;
            }

            public bool IsReady => _ad != null && _slot.IsFresh(Now);

            // Зовётся раз в секунду и после каждого показа: свежий ролик и идущую загрузку не трогает.
            public void Load(bool playerWaiting)
            {
                double now = Now;
                if (_slot.LoadTimedOut(now))
                {
                    _cancelLoading();
                    _request++;
                    _slot.OnFailed(now);
                    Debug.Log($"[Ads] {_name} got no answer in {FullscreenAdSlot.LoadTimeout:0} s, {NextTry()}");
                }
                if (_ad != null && !_slot.IsFresh(now))
                {
                    Debug.Log($"[Ads] {_name} expired, loading a new one");
                    _destroy(_ad);
                    _ad = null;
                    _slot.OnTaken();
                }
                if (!_slot.NeedsLoad(now, playerWaiting))
                    return;
                if (_ad != null)
                    Debug.Log($"[Ads] {_name} is getting old, loading a replacement");

                int request = ++_request;
                _slot.OnLoadStarted(now);
                _load(new AdRequest(_unitId),
                    ad =>
                    {
                        if (request != _request)
                        {
                            _destroy(ad);
                            return;
                        }
                        if (_ad != null)
                            _destroy(_ad);
                        _ad = ad;
                        _slot.OnLoaded(Now);
                        Debug.Log($"[Ads] {_name} loaded");
                    },
                    error =>
                    {
                        if (request != _request)
                            return;
                        _slot.OnFailed(Now);
                        Debug.Log($"[Ads] {_name} failed to load: {error.Message}, {NextTry()}");
                    });
            }

            // Ролик уходит на показ; следующий начнёт грузиться, когда показ закончится.
            public TAd Take()
            {
                if (!IsReady)
                    return null;
                var ad = _ad;
                _ad = null;
                _slot.OnTaken();
                return ad;
            }

            string NextTry()
            {
                double delay = _slot.RetryDelay(_slot.Failures);
                return double.IsPositiveInfinity(delay)
                    ? $"{_slot.Failures} errors in a row, waiting for the player"
                    : $"retry in {delay:0} s";
            }
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

        // Новая реклама — новый объект баннера (раз в минуту, BannerScheduler): он грузится скрытым и встаёт
        // на место старого только после загрузки, так что баннер не мигает и не пустеет. Не загрузился —
        // на экране остаётся старый.
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
