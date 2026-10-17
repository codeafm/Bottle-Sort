using System;
using System.Collections.Generic;
using BottleSort.Core;
using BottleSort.UI;

namespace BottleSort.Services
{
    public enum ChestKind
    {
        Chapter,
        Stars
    }

    public readonly struct ChestReward
    {
        public readonly ChestKind Kind;
        public readonly RewardBundle Reward;
        // Номер главы или порядковый номер звёздного сундука.
        public readonly int Number;

        public ChestReward(ChestKind kind, int number, RewardBundle reward)
        {
            Kind = kind;
            Number = number;
            Reward = reward;
        }
    }

    // Начисления и траты. Всё через PlayerData, чтобы экраны узнали об изменениях из PlayerData.Changed.
    public static class Economy
    {
        public static event Action<RewardBundle> Granted;

        public static void Grant(PlayerData data, RewardBundle reward)
        {
            if (reward.IsEmpty)
                return;
            data.coins = Math.Max(0, data.coins + reward.Coins);
            data.undoCount += reward.Undo;
            data.hintCount += reward.Hints;
            data.extraBottleCount += reward.Bottles;
            data.wheelSpins += reward.Spins;
            data.Save();
            Granted?.Invoke(reward);
        }

        public static int BoosterCount(PlayerData data, BoosterKind kind)
        {
            switch (kind)
            {
                case BoosterKind.Undo: return data.undoCount;
                case BoosterKind.Hint: return data.hintCount;
                default: return data.extraBottleCount;
            }
        }

        public static bool TryUseBooster(PlayerData data, BoosterKind kind)
        {
            if (BoosterCount(data, kind) <= 0)
                return false;
            switch (kind)
            {
                case BoosterKind.Undo: data.undoCount--; break;
                case BoosterKind.Hint: data.hintCount--; break;
                default: data.extraBottleCount--; break;
            }
            data.Save();
            return true;
        }

        public static bool TryBuyBooster(PlayerData data, BoosterKind kind)
        {
            var offer = EconomyRules.OfferFor(kind);
            if (data.coins < offer.Price)
                return false;
            data.coins -= offer.Price;
            Grant(data, RewardBundle.Booster(kind, offer.Count));
            return true;
        }

        // Стартовый подарок после третьего уровня, только один раз.
        public static bool TryClaimTutorialGift(PlayerData data)
        {
            if (data.tutorialGiftTaken)
                return false;
            data.tutorialGiftTaken = true;
            Grant(data, EconomyRules.TutorialGift);
            return true;
        }

        // Сундуки, которые открылись после победы: глава пройдена впервые, набралось ещё 30 звёзд.
        // Вызывать после RecordWin, звёзды уже должны быть посчитаны.
        public static List<ChestReward> CollectChests(PlayerData data, int level, LevelType type)
        {
            var chests = new List<ChestReward>();
            if (type == LevelType.ChapterBoss)
            {
                int chapter = LevelSchedule.ChapterOf(level);
                if (!data.chapterChests.Contains(chapter))
                {
                    data.chapterChests.Add(chapter);
                    chests.Add(new ChestReward(ChestKind.Chapter, chapter, EconomyRules.ChapterChest(chapter)));
                }
            }

            int due = data.TotalStars / EconomyRules.StarsPerChest;
            while (data.starChestsOpened < due)
            {
                data.starChestsOpened++;
                var booster = StarChestBooster(data.starChestsOpened);
                var reward = new RewardBundle(EconomyRules.StarChestCoins) + RewardBundle.Booster(booster);
                chests.Add(new ChestReward(ChestKind.Stars, data.starChestsOpened, reward));
            }

            var total = new RewardBundle();
            foreach (var chest in chests)
                total += chest.Reward;
            if (chests.Count > 0)
                Grant(data, total);
            return chests;
        }

        // «Случайный бустер», но одинаковый для одного и того же сундука: переустановка ничего не перекрутит.
        static BoosterKind StarChestBooster(int chestNumber) =>
            (BoosterKind)new Random(chestNumber * 7919 + 13).Next(3);
    }
}
