using System;
using System.Globalization;
using BottleSort.UI;

namespace BottleSort.Services
{
    // Подарок дня, 7-дневный календарь и колесо удачи — как во Flutter (main_menu_screen.dart).
    // Дата передаётся снаружи, чтобы тесты могли «перелистывать» дни.
    public static class DailyRewards
    {
        public static string DayKey(DateTime day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        public static bool GiftAvailable(PlayerData data, DateTime today) => data.giftDate != DayKey(today);

        public static bool TryClaimGift(PlayerData data, DateTime today, out RewardBundle reward)
        {
            reward = EconomyRules.DailyGift;
            if (!GiftAvailable(data, today))
                return false;
            data.giftDate = DayKey(today);
            Economy.Grant(data, reward);
            return true;
        }

        public static bool CalendarClaimedToday(PlayerData data, DateTime today) => data.calendarDate == DayKey(today);

        // День, награду которого заберут следующей. Пропуск дня серию не сбрасывает — как во Flutter.
        public static int CalendarDay(PlayerData data) => Math.Max(1, Math.Min(EconomyRules.CalendarDays, data.calendarDay));

        public static bool TryClaimCalendar(PlayerData data, DateTime today, out int coins)
        {
            int day = CalendarDay(data);
            coins = EconomyRules.CalendarCoins(day);
            if (CalendarClaimedToday(data, today))
                return false;
            data.calendarDate = DayKey(today);
            data.calendarDay = day >= EconomyRules.CalendarDays ? 1 : day + 1;
            Economy.Grant(data, new RewardBundle(coins));
            return true;
        }

        // Во Flutter бесплатная попытка была одна на всю жизнь. Здесь каждый новый день добавляет одну,
        // если попыток не осталось; накопить больше одной бесплатной нельзя.
        public static void RefreshWheel(PlayerData data, DateTime today)
        {
            string key = DayKey(today);
            if (data.wheelDate == key)
                return;
            data.wheelDate = key;
            data.wheelSpins = Math.Max(data.wheelSpins, 1);
            data.Save();
        }

        public static bool HasAnythingToday(PlayerData data, DateTime today)
        {
            RefreshWheel(data, today);
            return GiftAvailable(data, today) || !CalendarClaimedToday(data, today) || data.wheelSpins > 0;
        }

        // Возвращает номер сектора или -1, если попыток нет. Приз начисляется сразу,
        // колесо на экране просто докручивается до этого сектора.
        public static int Spin(PlayerData data, Random random)
        {
            if (data.wheelSpins <= 0)
                return -1;
            data.wheelSpins--;
            int sector = random.Next(EconomyRules.WheelPrizes.Length);
            Economy.Grant(data, EconomyRules.WheelPrizes[sector]);
            return sector;
        }

        public static void AddSpin(PlayerData data)
        {
            Economy.Grant(data, new RewardBundle(0, spins: 1));
        }
    }
}
