using System;
using BottleSort.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BottleSort.UI
{
    // Карточка с наградой: подарок дня, сундук главы, сундук за звёзды, «+100 за рекламу».
    // Показывает только то, что реально дают.
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
        // Затемнение закрывает карточку только у предложений (реклама за монеты), у наград — нет.
        [SerializeField] Button dimmer;
        // Вторая (синяя) кнопка и иконка видео для «за рекламу или обычно».
        [SerializeField] AdChoiceButtons adChoice;
        // Крупная иконка по центру вместо ряда наград: для предложений «+5 ходов», «+1 бутылка» и т. п.
        // Какая иконка к какому предложению — в списке highlights (ключ зелёной кнопки → иконка и подпись).
        [SerializeField] GameObject highlight;
        [SerializeField] Image highlightIcon;
        [SerializeField] TMP_Text highlightCaption;
        [SerializeField] Highlight[] highlights;
        // Насколько карточка с крупной иконкой ниже обычной, единицы Canvas.
        [SerializeField] float highlightTrim = 40f;

        [Serializable]
        public struct Highlight
        {
            public string adKey;
            public Sprite icon;
            public string caption;
        }

        Action _onClaim;
        Action _onSecondary;
        bool _dismissible;

        public override ScreenId Id => ScreenId.Reward;
        public override bool IsPopup => true;

        // Откуда летят монеты в счётчик.
        public RectTransform CoinIcon => items[0];

        void Awake()
        {
            claimButton.onClick.AddListener(Claim);
            // Подзаголовок в одну строку: длинный перевод лучше чуть уменьшить, чем вылезти за карточку.
            if (subtitle.TryGetComponent(out TMP_Text subtitleText) && !subtitleText.enableAutoSizing)
            {
                subtitleText.fontSizeMax = subtitleText.fontSize;
                subtitleText.fontSizeMin = subtitleText.fontSize * 0.7f;
                subtitleText.enableAutoSizing = true;
            }
            // В сценах, собранных до этого поля, ссылки нет: берём затемнение по имени, как его создаёт билдер.
            if (dimmer == null)
            {
                var found = transform.Find("Content/Dimmer");
                dimmer = found != null ? found.GetComponent<Button>() : null;
            }
            if (dimmer != null)
                dimmer.onClick.AddListener(Dismiss);
        }

        public void Setup(string titleKey, string subtitleKey, RewardBundle reward, string buttonKey, Action onClaim,
            params object[] titleArgs)
        {
            title.SetKey(titleKey, titleArgs);
            subtitle.SetKey(subtitleKey);
            claimLabel.SetKey(buttonKey);
            _onClaim = onClaim;
            _onSecondary = null;
            _dismissible = false;
            if (adChoice != null)
                adChoice.ShowSingle();
            if (highlight != null)
                highlight.SetActive(false);

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

        // Две кнопки: зелёная «за рекламу» (adKey, onWatchAd) и под ней обычная (secondaryKey, onSecondary).
        // Карточка закрывается после любой из них, колбэк вызывается уже после закрытия.
        public void SetupChoice(string titleKey, string subtitleKey, RewardBundle reward, string adKey, Action onWatchAd,
            string secondaryKey, Action onSecondary, params object[] titleArgs)
        {
            Setup(titleKey, subtitleKey, reward, adKey, onWatchAd, titleArgs);
            _onSecondary = onSecondary;
            bool highlighted = ShowHighlight(adKey);
            if (adChoice != null)
                adChoice.ShowPair(secondaryKey, Secondary, highlighted ? highlightTrim : 0f);
            else
                Debug.LogWarning("[RewardPopup] No AdChoiceButtons in the card, only the ad button is shown");
        }

        bool ShowHighlight(string adKey)
        {
            if (highlight == null || highlights == null)
                return false;
            foreach (var entry in highlights)
            {
                if (entry.adKey != adKey)
                    continue;
                foreach (var item in items)
                    item.gameObject.SetActive(false);
                foreach (var count in counts)
                    count.gameObject.SetActive(false);
                highlightIcon.sprite = entry.icon;
                highlightCaption.text = entry.caption;
                highlightCaption.gameObject.SetActive(!string.IsNullOrEmpty(entry.caption));
                highlight.SetActive(true);
                return true;
            }
            return false;
        }

        void Secondary()
        {
            var callback = _onSecondary;
            _onSecondary = null;
            _onClaim = null;
            Router.ClosePopup(this);
            callback?.Invoke();
        }

        // Для предложений: можно закрыть тапом мимо карточки или «назад», ничего не получив.
        public void AllowDismiss() => _dismissible = true;

        public override bool OnBack()
        {
            if (_dismissible)
                Dismiss();
            else
                Claim();
            return true;
        }

        void Dismiss()
        {
            if (!_dismissible)
                return;
            _onClaim = null;
            Router.ClosePopup(this);
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
