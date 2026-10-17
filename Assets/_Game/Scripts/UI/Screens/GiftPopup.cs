using BottleSort.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BottleSort.UI
{
    // «Обучение пройдено!» после третьего уровня: стартовый подарок из LEVEL_DESIGN §3.4 и сразу уровень 4.
    public sealed class GiftPopup : UiScreen
    {
        public static readonly int Coins = EconomyRules.TutorialGift.Coins;
        public static readonly int Undo = EconomyRules.TutorialGift.Undo;
        public static readonly int Hints = EconomyRules.TutorialGift.Hints;
        public static readonly int ExtraBottles = EconomyRules.TutorialGift.Bottles;

        [SerializeField] Button claimButton;
        // Монеты, отмены, подсказки, бутылки - в этом порядке.
        [SerializeField] TMP_Text[] counts;

        public override ScreenId Id => ScreenId.Gift;
        public override bool IsPopup => true;

        void Awake() => claimButton.onClick.AddListener(Claim);

        protected override void OnOpen()
        {
            counts[0].text = Loc.Number(Coins);
            counts[1].text = Loc.Format("gift_count", Undo);
            counts[2].text = Loc.Format("gift_count", Hints);
            counts[3].text = Loc.Format("gift_count", ExtraBottles);
        }

        public override bool OnBack()
        {
            Claim();
            return true;
        }

        void Claim()
        {
            if (Economy.TryClaimTutorialGift(PlayerData.Current))
                UiFeedback.Play(UiSound.Reward);
            Router.PlayLevel(4);
        }
    }
}
