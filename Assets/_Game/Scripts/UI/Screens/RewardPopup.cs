using System;
using BottleSort.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BottleSort.UI
{
    // Карточка с наградой: подарок дня, сундук главы, сундук за звёзды. Показывает только то, что реально дают.
    public sealed class RewardPopup : UiScreen
    {
        [SerializeField] LocalizedText title;
        [SerializeField] LocalizedText subtitle;
        [SerializeField] Button claimButton;
        [SerializeField] LocalizedText claimLabel;
        // Монеты, отмены, подсказки, бутылки, попытки колеса - в этом порядке.
        [SerializeField] RectTransform[] items;
        [SerializeField] TMP_Text[] counts;
        [SerializeField] float itemSpacing = 175f;

        Action _onClaim;

        public override ScreenId Id => ScreenId.Reward;
        public override bool IsPopup => true;

        void Awake() => claimButton.onClick.AddListener(Claim);

        public void Setup(string titleKey, string subtitleKey, RewardBundle reward, string buttonKey, Action onClaim,
            params object[] titleArgs)
        {
            title.SetKey(titleKey, titleArgs);
            subtitle.SetKey(subtitleKey);
            claimLabel.SetKey(buttonKey);
            _onClaim = onClaim;

            int[] amounts = { reward.Coins, reward.Undo, reward.Hints, reward.Bottles, reward.Spins };
            int shown = 0;
            foreach (int amount in amounts)
                if (amount > 0)
                    shown++;

            int slot = 0;
            for (int i = 0; i < items.Length; i++)
            {
                bool visible = amounts[i] > 0;
                items[i].gameObject.SetActive(visible);
                counts[i].gameObject.SetActive(visible);
                if (!visible)
                    continue;
                float x = (slot - (shown - 1) * 0.5f) * itemSpacing;
                items[i].anchoredPosition = new Vector2(x, items[i].anchoredPosition.y);
                counts[i].rectTransform.anchoredPosition = new Vector2(x, counts[i].rectTransform.anchoredPosition.y);
                counts[i].text = i == 0 ? "+" + Loc.Number(amounts[i]) : Loc.Format("gift_count", amounts[i]);
                slot++;
            }
        }

        public override bool OnBack()
        {
            Claim();
            return true;
        }

        void Claim()
        {
            var callback = _onClaim;
            _onClaim = null;
            Router.ClosePopup(this);
            callback?.Invoke();
        }
    }
}
