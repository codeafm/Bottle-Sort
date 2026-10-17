using System;
using BottleSort.Core;
using BottleSort.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BottleSort.UI
{
    public sealed class MainMenuScreen : UiScreen
    {
        public const int TotalLevels = 2000;

        const float IntroShortSpeed = 1.8f;

        static readonly string[] HeroLayouts = { "CCB", "KPPK", "YORR", "GLG", "BBCB" };

        [SerializeField] Button settingsButton;
        [SerializeField] Button coinsButton;
        [SerializeField] TMP_Text coins;
        [SerializeField] Button giftButton;
        [SerializeField] Button bonusButton;
        [SerializeField] Button luckButton;
        [SerializeField] GameObject giftBadge;
        [SerializeField] GameObject luckBadge;
        [SerializeField] TMP_Text luckCount;
        [SerializeField] Button playButton;
        [SerializeField] LocalizedText playLevel;
        [SerializeField] Button levelsButton;
        [SerializeField] LocalizedText levelsProgress;
        [SerializeField] Button shopButton;
        [SerializeField] BottleStubView[] heroBottles;
        [SerializeField] Toast toast;
        // Появление элементов меню после заставки и после игры; при переходах внутри сцены не играет.
        [SerializeField] ScreenMotion intro;

        bool _introPlayed;

        public override ScreenId Id => ScreenId.Menu;
        // Куда летят монеты из наград (колесо, подарок дня).
        public RectTransform CoinsTarget => (RectTransform)coinsButton.transform;
        public override Theme? BackdropTheme => LevelPlan.PlayerTheme ?? Theme.Navy;

        void Awake()
        {
            settingsButton.onClick.AddListener(() => Router.ShowPopup(ScreenId.Settings));
            // Как во Flutter: тап по монетам или зелёному «+» — монеты за рекламу. В магазин ведёт своя кнопка.
            coinsButton.onClick.AddListener(OfferCoinsForAd);
            shopButton.onClick.AddListener(() => Router.Show(ScreenId.Shop));
            levelsButton.onClick.AddListener(() => Router.Show(ScreenId.Levels));
            playButton.onClick.AddListener(() => Router.PlayLevel(PlayerData.Current.highestUnlocked));
            giftButton.onClick.AddListener(OpenDailyGift);
            bonusButton.onClick.AddListener(() => Router.ShowPopup(ScreenId.Calendar));
            luckButton.onClick.AddListener(() => Router.ShowPopup(ScreenId.Wheel));
        }

        void OnEnable() => PlayerData.Changed += Refresh;
        void OnDisable() => PlayerData.Changed -= Refresh;

        protected override void OnOpen()
        {
            Refresh();
            ShowHeroBottles();
            if (_introPlayed || intro == null)
                return;
            _introPlayed = true;
            // После заставки — полное появление, после игры — короткое. Начинаем, когда кадр прошлой сцены почти растворился.
            float speed = SceneLoader.PreviousScene == SceneLoader.GameScene ? IntroShortSpeed : 1f;
            intro.Play(speed, null, SceneLoader.IsBusy ? SceneLoader.RevealDelay : 0f);
        }

        public override void FillForEditor()
        {
            Refresh();
            ShowHeroBottles();
        }

        void ShowHeroBottles()
        {
            for (int i = 0; i < heroBottles.Length && i < HeroLayouts.Length; i++)
            {
                var bottle = new Bottle(4);
                foreach (char letter in HeroLayouts[i])
                    bottle.Push(Palette.FromLetter(letter));
                heroBottles[i].Show(bottle, 4);
            }
        }

        void Refresh()
        {
            var data = PlayerData.Current;
            coins.text = Loc.Number(data.coins);
            playLevel.SetKey("menu_play_level", data.highestUnlocked);
            levelsProgress.SetKey("menu_levels_progress", data.highestUnlocked, TotalLevels);
            // Красная «1» — подарок дня ещё не забран, жёлтое число — бесплатные вращения колеса.
            giftBadge.SetActive(DailyRewards.GiftAvailable(data, DateTime.Now));
            luckBadge.SetActive(data.wheelSpins > 0);
            luckCount.text = data.wheelSpins.ToString();
        }

        const int FlyingCoins = 8;

        void OfferCoinsForAd()
        {
            switch (CoinAdOffer.StateOf(PlayerData.Current, DateTime.Now, out var wait))
            {
                case CoinAdState.Tutorial:
                    toast.ShowKey("toast_coins_ad_tutorial", 1.8f);
                    return;
                case CoinAdState.DailyLimit:
                    toast.ShowKey("toast_coins_ad_limit", 1.8f);
                    return;
                case CoinAdState.Cooldown:
                    toast.Show(Loc.Format("toast_coins_ad_wait", $"{(int)wait.TotalMinutes}:{wait.Seconds:00}"), 1.8f);
                    return;
            }

            // Ролик ещё не пришёл (или после прошлого показа грузится следующий) — говорим об этом сразу.
            if (!RewardedAds.EnsureReady())
            {
                toast.ShowKey("toast_ad_loading", 2f);
                return;
            }

            var popup = Router.Get<RewardPopup>();
            popup.Setup("coins_ad_title", "coins_ad_text", new RewardBundle(CoinAdOffer.Coins), "coins_ad_watch",
                () => WatchAdForCoins(popup));
            popup.AllowDismiss();
            Router.ShowPopup(ScreenId.Reward);
        }

        void WatchAdForCoins(RewardPopup popup)
        {
            var from = popup.CoinIcon.position;
            var icon = popup.CoinIcon.Find("Icon");
            if (!RewardedAds.EnsureReady())
            {
                toast.ShowKey("toast_ad_loading", 2f);
                return;
            }
            RewardedAds.Show(watched =>
            {
                var data = PlayerData.Current;
                if (!watched)
                {
                    toast.ShowKey("toast_coins_ad_failed", 2.2f);
                    return;
                }
                int before = data.coins;
                if (!CoinAdOffer.TryGrant(data, DateTime.Now))
                    return;
                FlyCoinsIn(from, icon != null ? icon.GetComponent<Image>() : null, before, data.coins);
            });
        }

        // Монеты уже начислены и сохранены, счётчик догоняет их, пока они летят.
        void FlyCoinsIn(Vector3 from, Image template, int before, int after)
        {
            coins.text = Loc.Number(before);
            if (template == null)
            {
                coins.text = Loc.Number(after);
                UiFeedback.Play(UiSound.Coin);
                return;
            }
            CoinFlight.Fly((RectTransform)transform, template, from, CoinsTarget.position, FlyingCoins, index =>
            {
                UiFeedback.Play(UiSound.Coin);
                int shown = before + (after - before) * (index + 1) / FlyingCoins;
                coins.text = Loc.Number(shown);
                if (index == FlyingCoins - 1)
                    Haptics.Light();
            });
        }

        void OpenDailyGift()
        {
            var data = PlayerData.Current;
            if (!DailyRewards.GiftAvailable(data, DateTime.Now))
            {
                toast.ShowKey("toast_gift_taken", 1.8f);
                return;
            }

            var popup = Router.Get<RewardPopup>();
            popup.SetupChoice("daily_gift_title", "daily_gift_text", EconomyRules.DailyGift,
                "reward_double_ad", () => RewardedAds.Show(watched =>
                {
                    // Реклама не досмотрена — подарок не пропадает, его можно забрать ещё раз.
                    if (watched)
                        ClaimGift(2);
                    else
                        toast.ShowKey("toast_coins_ad_failed", 2.2f);
                }),
                "gift_claim", () => ClaimGift(1));
            popup.AllowDismiss();
            Router.ShowPopup(ScreenId.Reward);
        }

        void ClaimGift(int multiplier)
        {
            if (DailyRewards.TryClaimGift(PlayerData.Current, DateTime.Now, out _, multiplier))
                UiFeedback.Play(UiSound.Reward);
        }
    }
}
