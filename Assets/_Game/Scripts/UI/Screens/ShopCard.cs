using System;
using BottleSort.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BottleSort.UI
{
    public sealed class ShopCard : MonoBehaviour
    {
        public enum State
        {
            Selected,
            Owned,
            Affordable,
            TooExpensive,
            Locked
        }

        [SerializeField] Button button;
        [SerializeField] Image border;
        [SerializeField] GameObject selectedGlow;
        [SerializeField] GameObject bottleRoot;
        [SerializeField] BottleStubView bottle;
        [SerializeField] GameObject themeRoot;
        [SerializeField] RawImage themeThumb;
        [SerializeField] BottleStubView themeBottle;
        [SerializeField] LocalizedText nameLabel;
        [SerializeField] UiGradient pill;
        [SerializeField] GameObject pillCoin;
        [SerializeField] GameObject pillLock;
        [SerializeField] TMP_Text pillLabel;
        [SerializeField] GameObject newBadge;

        public event Action Pressed;

        void Awake() => button.onClick.AddListener(() => Pressed?.Invoke());

        public void ShowBottle(string nameKey, string colors)
        {
            bottleRoot.SetActive(true);
            themeRoot.SetActive(false);
            nameLabel.SetKey(nameKey);
            bottle.Show(MakeBottle(colors), 4);
        }

        public void ShowTheme(string nameKey, Theme theme, string colors)
        {
            bottleRoot.SetActive(false);
            themeRoot.SetActive(true);
            nameLabel.SetKey(nameKey);
            themeThumb.texture = ThemeBackdrop.TextureFor(theme);
            themeBottle.Show(MakeBottle(colors), 4);
        }

        public void SetState(State state, int price, int unlockLevel, bool isNew)
        {
            bool selected = state == State.Selected;
            border.color = selected ? UiColors.Hex("#9BFF4F") : UiColors.Hex("#96C8FF", 0.3f);
            selectedGlow.SetActive(selected);
            newBadge.SetActive(isNew && state != State.Owned && state != State.Selected && state != State.Locked);
            pillCoin.SetActive(state == State.Affordable || state == State.TooExpensive);
            pillLock.SetActive(state == State.Locked);
            pillLabel.color = state == State.Affordable ? UiColors.Hex("#4A1E00") : Color.white;

            switch (state)
            {
                case State.Selected:
                    pillLabel.text = Loc.Get("shop_selected");
                    pill.SetColors(UiColors.Hex("#9BFF4F"), UiColors.Hex("#17A523"));
                    break;
                case State.Owned:
                    pillLabel.text = Loc.Get("shop_select");
                    pill.SetColors(UiColors.Hex("#35C7FF"), UiColors.Hex("#0870E8"));
                    break;
                case State.Affordable:
                    pillLabel.text = Loc.Number(price);
                    pill.SetColors(UiColors.Hex("#FFD957"), UiColors.Hex("#FF7A00"));
                    break;
                case State.TooExpensive:
                    pillLabel.text = Loc.Number(price);
                    pill.SetColors(new Color(1f, 1f, 1f, 0.12f), new Color(1f, 1f, 1f, 0.12f));
                    break;
                default:
                    pillLabel.text = Loc.Format("shop_from_level", unlockLevel);
                    pill.SetColors(new Color(1f, 1f, 1f, 0.12f), new Color(1f, 1f, 1f, 0.12f));
                    break;
            }
        }

        static Bottle MakeBottle(string colors)
        {
            var result = new Bottle(4);
            foreach (char letter in colors)
                result.Push(Palette.FromLetter(letter));
            return result;
        }
    }
}
