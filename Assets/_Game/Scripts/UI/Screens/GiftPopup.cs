using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BottleSort.UI
{
    // «Обучение пройдено!» после третьего уровня: стартовый подарок из LEVEL_DESIGN §3.4 и сразу уровень 4.
    public sealed class GiftPopup : UiScreen
    {
        public const int Coins = 200;
        public const int Undo = 5;
        public const int Hints = 3;
        public const int ExtraBottles = 1;

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
            var data = PlayerData.Current;
            if (!data.tutorialGiftTaken)
            {
                data.tutorialGiftTaken = true;
                data.undoCount += Undo;
                data.hintCount += Hints;
                data.extraBottleCount += ExtraBottles;
                data.AddCoins(Coins);
                UiFeedback.Play(UiSound.Reward);
            }
            Router.PlayLevel(4);
        }
    }
}
