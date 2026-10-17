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

        static readonly string[] HeroLayouts = { "CCB", "KPPK", "YORR", "GLG", "BBCB" };

        [SerializeField] Button settingsButton;
        [SerializeField] Button coinsButton;
        [SerializeField] TMP_Text coins;
        [SerializeField] Button giftButton;
        [SerializeField] Button bonusButton;
        [SerializeField] Button luckButton;
        [SerializeField] Button playButton;
        [SerializeField] LocalizedText playLevel;
        [SerializeField] Button levelsButton;
        [SerializeField] LocalizedText levelsProgress;
        [SerializeField] Button shopButton;
        [SerializeField] BottleStubView[] heroBottles;
        [SerializeField] Toast toast;

        public override ScreenId Id => ScreenId.Menu;
        public override Theme? BackdropTheme => LevelPlan.PlayerTheme ?? Theme.Navy;

        void Awake()
        {
            settingsButton.onClick.AddListener(() => Router.ShowPopup(ScreenId.Settings));
            coinsButton.onClick.AddListener(() => Router.Show(ScreenId.Shop));
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
            popup.Setup("daily_gift_title", "daily_gift_text", EconomyRules.DailyGift, "gift_claim", () =>
            {
                if (DailyRewards.TryClaimGift(PlayerData.Current, DateTime.Now, out _))
                    UiFeedback.Play(UiSound.Reward);
            });
            Router.ShowPopup(ScreenId.Reward);
        }
    }
}
