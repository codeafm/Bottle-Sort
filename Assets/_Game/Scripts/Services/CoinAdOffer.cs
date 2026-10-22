using System;
using BottleSort.UI;

namespace BottleSort.Services
{
    public enum CoinAdState
    {
        Available,
        Tutorial,
        Cooldown,
        DailyLimit
    }

    // «+100 монет за рекламу» с пилюли монет в меню, как во Flutter, но с лимитом.
    // Уровень приносит ~30 монет (LEVEL_DESIGN §6.3), подарок, календарь и колесо уже дают активному
    // игроку +30–50 %. Пять раз по 100 почти удвоили бы доход, поэтому три раза в день и раз в 3 минуты:
    // до +300 в день — заметно, но магазин не обесценивается.
    public static class CoinAdOffer
    {
        public const int Coins = 100;
        public const int PerDay = 3;
        public static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(3);
        // На обучении рекламы нет (LEVEL_DESIGN §3.4).
        public const int FromLevel = 4;

        public static CoinAdState StateOf(PlayerData data, DateTime now, out TimeSpan wait)
        {
            wait = TimeSpan.Zero;
            if (data.highestUnlocked < FromLevel)
                return CoinAdState.Tutorial;
            if (CountToday(data, now) >= PerDay)
                return CoinAdState.DailyLimit;

            var last = DateTimeOffset.FromUnixTimeSeconds(data.coinAdLastUnix).UtcDateTime;
            var next = last + Cooldown;
            var utcNow = now.ToUniversalTime();
            if (data.coinAdLastUnix > 0 && utcNow < next)
            {
                wait = next - utcNow;
                return CoinAdState.Cooldown;
            }
            return CoinAdState.Available;
        }

        public static int LeftToday(PlayerData data, DateTime now) => Math.Max(0, PerDay - CountToday(data, now));

        // Вызывать только из колбэка досмотренной рекламы.
        public static bool TryGrant(PlayerData data, DateTime now)
        {
            if (StateOf(data, now, out _) != CoinAdState.Available)
                return false;
            string today = DailyRewards.DayKey(now);
            data.coinAdCount = data.coinAdDate == today ? data.coinAdCount + 1 : 1;
            data.coinAdDate = today;
            data.coinAdLastUnix = new DateTimeOffset(now.ToUniversalTime()).ToUnixTimeSeconds();
            Economy.Grant(data, new RewardBundle(Coins), "Монеты за рекламу");
            return true;
        }

        static int CountToday(PlayerData data, DateTime now) =>
            data.coinAdDate == DailyRewards.DayKey(now) ? data.coinAdCount : 0;
    }
}
