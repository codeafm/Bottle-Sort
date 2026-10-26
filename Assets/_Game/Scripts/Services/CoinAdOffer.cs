using System;
using BottleSort.UI;

namespace BottleSort.Services
{
    public enum CoinAdState
    {
        Available,
        Tutorial
    }

    // «+100 монет за рекламу» с пилюли монет в меню, как во Flutter. Без лимитов: сколько роликов игрок
    // готов посмотреть, столько раз и получит монеты (решение заказчика, раньше было 3 в день и раз в 3 минуты).
    public static class CoinAdOffer
    {
        public const int Coins = 100;
        // На обучении рекламы нет (LEVEL_DESIGN §3.4).
        public const int FromLevel = 4;

        public static CoinAdState StateOf(PlayerData data) =>
            data.highestUnlocked < FromLevel ? CoinAdState.Tutorial : CoinAdState.Available;

        // Вызывать только из колбэка досмотренной рекламы. Счётчик за день остаётся для статистики.
        public static bool TryGrant(PlayerData data, DateTime now)
        {
            if (StateOf(data) != CoinAdState.Available)
                return false;
            string today = DailyRewards.DayKey(now);
            data.coinAdCount = data.coinAdDate == today ? data.coinAdCount + 1 : 1;
            data.coinAdDate = today;
            data.coinAdLastUnix = new DateTimeOffset(now.ToUniversalTime()).ToUnixTimeSeconds();
            Economy.Grant(data, new RewardBundle(Coins), "Монеты за рекламу");
            return true;
        }
    }
}
