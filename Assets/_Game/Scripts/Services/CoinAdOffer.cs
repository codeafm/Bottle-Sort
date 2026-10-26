using System;
using BottleSort.UI;

namespace BottleSort.Services
{
    // «+100 монет за рекламу» с пилюли монет в меню, как во Flutter. Без лимитов и с первого запуска,
    // даже до конца обучения: сколько роликов игрок готов посмотреть, столько раз и получит монеты
    // (решение заказчика, раньше было 3 в день, раз в 3 минуты и только после уровня 3).
    public static class CoinAdOffer
    {
        public const int Coins = 100;

        // Вызывать только из колбэка досмотренной рекламы. Счётчик за день остаётся для статистики.
        public static void Grant(PlayerData data, DateTime now)
        {
            string today = DailyRewards.DayKey(now);
            data.coinAdCount = data.coinAdDate == today ? data.coinAdCount + 1 : 1;
            data.coinAdDate = today;
            data.coinAdLastUnix = new DateTimeOffset(now.ToUniversalTime()).ToUnixTimeSeconds();
            Economy.Grant(data, new RewardBundle(Coins), "Монеты за рекламу", Coins);
        }
    }
}
