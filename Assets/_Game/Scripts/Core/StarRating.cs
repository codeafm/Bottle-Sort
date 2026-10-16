using System;

namespace BottleSort.Core
{
    // Звёзды считаются от par (длины оптимального решения), LEVEL_DESIGN.md §4.
    // Формула Flutter-версии на поздних уровнях давала 3 звезды всегда, поэтому от неё отказались.
    public static class StarRating
    {
        public static int ThreeStarLimit(int par) => par + Math.Max(3, CeilPercent(par, 25));
        public static int TwoStarLimit(int par) => par + Math.Max(8, CeilPercent(par, 60));

        public static int Calculate(int moves, int par, bool usedExtraBottle)
        {
            int stars;
            if (moves <= ThreeStarLimit(par))
                stars = 3;
            else if (moves <= TwoStarLimit(par))
                stars = 2;
            else
                stars = 1;

            // +Бутылка меняет саму головоломку, поэтому выше двух звёзд с ней не дают.
            return usedExtraBottle ? Math.Min(stars, 2) : stars;
        }

        private static int CeilPercent(int value, int percent) => (value * percent + 99) / 100;
    }
}
