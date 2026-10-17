using System;
using BottleSort.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BottleSort.UI
{
    // 7-дневный бонус: каждый день одна награда, 60…180 монет. Пропуск дня серию не сбрасывает.
    public sealed class CalendarPopup : UiScreen
    {
        [SerializeField] Button closeButton;
        [SerializeField] Button dimmer;
        [SerializeField] Button claimButton;
        [SerializeField] LocalizedText claimLabel;
        [SerializeField] CanvasGroup claimGroup;
        // Вторая (синяя) кнопка и иконка видео для «×2 за рекламу или обычно».
        [SerializeField] AdChoiceButtons adChoice;
        // По одному на день, с первого по седьмой.
        [SerializeField] LocalizedText[] dayLabels;
        [SerializeField] TMP_Text[] amounts;
        [SerializeField] UiGradient[] tiles;
        [SerializeField] GameObject[] checks;
        [SerializeField] GameObject[] todayGlows;

        bool _adOffer;

        public override ScreenId Id => ScreenId.Calendar;
        public override bool IsPopup => true;

        void Awake()
        {
            closeButton.onClick.AddListener(Close);
            dimmer.onClick.AddListener(Close);
            claimButton.onClick.AddListener(() =>
            {
                if (_adOffer)
                    ClaimDoubleForAd();
                else
                    Claim(1);
            });
            if (adChoice == null)
                Debug.LogWarning("[CalendarPopup] No AdChoiceButtons in the card, the ad x2 offer is off");
        }

        protected override void OnOpen() => Refresh();

        public override void FillForEditor() => Refresh();

        void Close() => Router.ClosePopup(this);

        void Refresh()
        {
            var data = PlayerData.Current;
            var today = DateTime.Now;
            bool claimedToday = DailyRewards.CalendarClaimedToday(data, today);
            int next = DailyRewards.CalendarDay(data);
            // Сегодня забрали седьмой день — счётчик уже ушёл на первый, а на экране вся неделя собрана.
            int claimedUpTo = claimedToday && next == 1 ? EconomyRules.CalendarDays : next - 1;
            int todayDay = claimedToday ? claimedUpTo : next;

            for (int i = 0; i < amounts.Length; i++)
            {
                int day = i + 1;
                bool claimed = day <= claimedUpTo;
                bool isToday = day == todayDay;
                dayLabels[i].SetKey("calendar_day", day);
                amounts[i].text = Loc.Number(EconomyRules.CalendarCoins(day));
                checks[i].SetActive(claimed);
                todayGlows[i].SetActive(isToday && !claimed);
                if (isToday && !claimed)
                    tiles[i].SetColors(UiColors.Hex("#FFD957"), UiColors.Hex("#FF7A00"));
                else if (claimed)
                    tiles[i].SetColors(UiColors.Hex("#17A523", 0.55f), UiColors.Hex("#0E6B17", 0.55f));
                else
                    tiles[i].SetColors(UiColors.Hex("#1A4A9A"), UiColors.Hex("#0A2560"));
            }

            claimButton.interactable = !claimedToday;
            claimGroup.alpha = claimedToday ? 0.55f : 1f;
            // Зелёная — «×2 за рекламу», под ней обычное «Забрать». Без второй кнопки — просто «Забрать».
            if (claimedToday || adChoice == null)
            {
                if (adChoice != null)
                    adChoice.ShowSingle();
                claimLabel.SetKey(claimedToday ? "calendar_tomorrow" : "gift_claim");
                _adOffer = false;
            }
            else
            {
                claimLabel.SetKey("reward_double_ad");
                adChoice.ShowPair("gift_claim", () => Claim(1));
                _adOffer = true;
            }
        }

        void ClaimDoubleForAd()
        {
            claimButton.interactable = false;
            RewardedAds.Show(watched =>
            {
                if (watched)
                    Claim(2);
                else
                    Refresh();
            });
        }

        void Claim(int multiplier)
        {
            if (!DailyRewards.TryClaimCalendar(PlayerData.Current, DateTime.Now, out _, multiplier))
                return;
            UiFeedback.Play(UiSound.Coin);
            Refresh();
        }
    }
}
