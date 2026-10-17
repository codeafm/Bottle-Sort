using System;
using BottleSort.Core;

namespace BottleSort.Services
{
    public enum BoosterKind
    {
        Undo,
        Hint,
        ExtraBottle
    }

    [Serializable]
    public struct RewardBundle
    {
        public int Coins;
        public int Undo;
        public int Hints;
        public int Bottles;
        public int Spins;

        public RewardBundle(int coins, int undo = 0, int hints = 0, int bottles = 0, int spins = 0)
        {
            Coins = coins;
            Undo = undo;
            Hints = hints;
            Bottles = bottles;
            Spins = spins;
        }

        public bool IsEmpty => Coins == 0 && Undo == 0 && Hints == 0 && Bottles == 0 && Spins == 0;

        public static RewardBundle Booster(BoosterKind kind, int count = 1)
        {
            switch (kind)
            {
                case BoosterKind.Undo: return new RewardBundle(0, undo: count);
                case BoosterKind.Hint: return new RewardBundle(0, hints: count);
                default: return new RewardBundle(0, bottles: count);
            }
        }

        public static RewardBundle operator +(RewardBundle a, RewardBundle b) =>
            new RewardBundle(a.Coins + b.Coins, a.Undo + b.Undo, a.Hints + b.Hints, a.Bottles + b.Bottles, a.Spins + b.Spins);
    }

    public readonly struct BoosterOffer
    {
        public readonly BoosterKind Kind;
        public readonly int Count;
        public readonly int Price;

        public BoosterOffer(BoosterKind kind, int count, int price)
        {
            Kind = kind;
            Count = count;
            Price = price;
        }
    }

    // Числа из LEVEL_DESIGN §4, §3.4, §6 и подарки из Flutter-версии (FLUTTER_SPEC §2).
    public static class EconomyRules
    {
        // Во Flutter было то 0, то 1250. По §6.2 при установке 0, всё стартовое приходит подарком после обучения.
        public const int StartCoins = 0;

        public static readonly RewardBundle TutorialGift = new RewardBundle(200, undo: 5, hints: 3, bottles: 1);

        public const int StarsPerChest = 30;
        public const int StarChestCoins = 60;

        public static readonly RewardBundle DailyGift = new RewardBundle(75, undo: 1, hints: 1);
        public const int CalendarDays = 7;

        public static readonly BoosterOffer[] BoosterOffers =
        {
            new BoosterOffer(BoosterKind.Undo, 3, 60),
            new BoosterOffer(BoosterKind.Hint, 1, 60),
            new BoosterOffer(BoosterKind.ExtraBottle, 1, 120)
        };

        // Шесть равных секторов колеса, по кругу по часовой стрелке.
        public static readonly RewardBundle[] WheelPrizes =
        {
            new RewardBundle(50),
            new RewardBundle(0, undo: 1),
            new RewardBundle(0, hints: 1),
            new RewardBundle(0, bottles: 1),
            new RewardBundle(150),
            new RewardBundle(0, spins: 1)
        };

        public static int BaseReward(LevelType type)
        {
            switch (type)
            {
                case LevelType.Special:
                case LevelType.BigFlask: return 25;
                case LevelType.Rescue: return 35;
                case LevelType.Boss: return 40;
                case LevelType.ChapterBoss: return 75;
                default: return 15;
            }
        }

        public static double StarMultiplier(int stars) => stars >= 3 ? 1.5 : stars == 2 ? 1.2 : 1.0;

        public static int LevelReward(LevelType type, int level, int stars)
        {
            if (stars <= 0)
                return 0;
            double raw = BaseReward(type) * (1 + level / 4000.0) * StarMultiplier(stars);
            return RoundToFive(raw);
        }

        // Переигровка доплачивает только разницу за новые звёзды, иначе ничего.
        public static int ReplayReward(LevelType type, int level, int previousStars, int stars)
        {
            if (previousStars <= 0)
                return LevelReward(type, level, stars);
            if (stars <= previousStars)
                return 0;
            return LevelReward(type, level, stars) - LevelReward(type, level, previousStars);
        }

        public static RewardBundle ChapterChest(int chapter) => new RewardBundle(100 + 10 * chapter, 1, 1, 1);

        public static int CalendarCoins(int day) => 40 + Math.Max(1, Math.Min(CalendarDays, day)) * 20;

        public static BoosterOffer OfferFor(BoosterKind kind)
        {
            foreach (var offer in BoosterOffers)
                if (offer.Kind == kind)
                    return offer;
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        // Половина округляется вверх: 22.5 → 25.
        static int RoundToFive(double value) => (int)Math.Floor(value / 5 + 0.5) * 5;
    }
}
