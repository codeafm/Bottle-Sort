using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BottleSort.UI
{
    public sealed class BoosterButton : MonoBehaviour
    {
        [SerializeField] Booster booster;
        [SerializeField] Button button;
        [SerializeField] GameObject badge;
        [SerializeField] TMP_Text badgeLabel;
        [SerializeField] UiGradient badgeBackground;
        [SerializeField] CanvasGroup group;

        public Booster Kind => booster;
        public event Action<Booster> Pressed;

        void Awake() => button.onClick.AddListener(() => Pressed?.Invoke(booster));

        // Скрытая кнопка не исчезает из сетки, чтобы остальные не переезжали.
        public void SetVisible(bool visible)
        {
            group.alpha = visible ? 1f : 0f;
            group.blocksRaycasts = visible;
            group.interactable = visible;
        }

        // Ещё не открытый бустер виден приглушённым, на бейдже уровень, с которого он появится.
        public void SetLocked(int unlockLevel)
        {
            group.alpha = 0.4f;
            group.blocksRaycasts = false;
            group.interactable = false;
            badge.SetActive(true);
            badgeLabel.text = string.Format(Loc.Get("badge_level"), unlockLevel);
            badgeLabel.color = Color.white;
            badgeBackground.SetColors(UiColors.Hex("#6B7390"), UiColors.Hex("#454C66"));
        }

        // count < 0 — у бустера нет счётчика (Заново). 0 — бейдж «AD»: можно получить за рекламу.
        public void SetCount(int count)
        {
            badge.SetActive(count >= 0);
            if (count < 0)
                return;
            if (count > 0)
            {
                badgeLabel.text = count.ToString();
                badgeLabel.color = UiColors.Hex("#3A1A00");
                badgeBackground.SetColors(UiColors.Hex("#FFC107"), UiColors.Hex("#FF8A00"));
            }
            else
            {
                badgeLabel.text = Loc.Get("badge_ad");
                badgeLabel.color = Color.white;
                badgeBackground.SetColors(UiColors.Hex("#FF3D71"), UiColors.Hex("#FF8A00"));
            }
        }
    }
}
