using System;
using System.Collections.Generic;
using BottleSort.Core;
using BottleSort.Services;
using UnityEngine;

namespace BottleSort.UI
{
    public enum Theme
    {
        Navy,
        Violet,
        Teal,
        Rose,
        Emerald,
        Amber
    }

    // Что UI нужно знать об уровне, кроме раскладки: тема главы, тег в шапке, награда.
    // Параметры генератора по номеру уровня (Params(L) из LEVEL_DESIGN §3.1) в Core пока нет,
    // поэтому здесь упрощённая таблица — заменить, когда gameplay добавит свою.
    public static class LevelPlan
    {
        static readonly Theme[] ChapterThemes =
        {
            Theme.Navy, Theme.Teal, Theme.Violet, Theme.Emerald, Theme.Rose, Theme.Amber, Theme.Navy, Theme.Teal,
            Theme.Violet, Theme.Rose, Theme.Emerald, Theme.Amber, Theme.Navy, Theme.Teal, Theme.Amber, Theme.Violet,
            Theme.Teal, Theme.Rose, Theme.Emerald, Theme.Violet, Theme.Amber, Theme.Navy, Theme.Emerald, Theme.Rose,
            Theme.Navy
        };

        // Первый уровень диапазона и цвета для тиров E / M / H.
        static readonly (int from, int easy, int medium, int hard, int hiddenShare)[] Ranges =
        {
            (4, 4, 4, 5, 0), (11, 4, 5, 5, 0), (21, 5, 5, 6, 0), (31, 5, 6, 6, 0), (51, 6, 6, 7, 0), (76, 6, 7, 7, 0),
            (101, 6, 7, 8, 40), (151, 7, 7, 8, 30), (201, 7, 8, 8, 30), (301, 7, 8, 9, 30), (351, 8, 8, 9, 30),
            (451, 8, 9, 9, 35), (501, 8, 9, 10, 35), (701, 9, 9, 10, 40), (1001, 9, 10, 10, 40), (1101, 9, 10, 11, 40),
            (1501, 10, 10, 11, 45), (1801, 10, 11, 12, 45)
        };

        static readonly (int from, string shape)[] SpecialShapes =
        {
            (30, "wine"), (30, "glass"), (30, "flask"), (60, "jug"), (80, "jar"), (110, "teapot"), (140, "potion")
        };

        public static int ChapterOf(int level) => LevelSchedule.ChapterOf(level);

        public static Theme ThemeOf(int level)
        {
            int chapter = ChapterOf(level);
            return ChapterThemes[(chapter - 1) % ChapterThemes.Length];
        }

        // Купленный в магазине фон, закреплённый игроком; null — «Авто», тема главы.
        public static Theme? PlayerTheme =>
            Enum.TryParse(PlayerData.Current.theme, true, out Theme theme) ? theme : (Theme?)null;

        public static string ChapterNameKey(int chapter) =>
            "chapter_" + ((chapter - 1) % ChapterThemes.Length + 1);

        public static int ChapterEnd(int chapter) => LevelSchedule.ChapterStart(chapter + 1) - 1;

        public static LevelType TypeOf(int level) =>
            HandmadeLevels.IsTutorial(level) ? LevelType.Normal : LevelSchedule.TypeOf(level);

        public static bool IsBossNode(int level)
        {
            var type = TypeOf(level);
            return type == LevelType.Boss || type == LevelType.ChapterBoss;
        }

        public static LevelSpec SpecFor(int level)
        {
            var type = LevelSchedule.TypeOf(level);
            var tier = LevelSchedule.TierOf(level);
            var range = Ranges[0];
            foreach (var r in Ranges)
                if (level >= r.from)
                    range = r;

            int colors;
            switch (tier)
            {
                case LevelTier.Easy: colors = range.easy; break;
                case LevelTier.Medium: colors = range.medium; break;
                default: colors = range.hard; break;
            }

            var spec = new LevelSpec
            {
                Number = level,
                PaletteSize = level < 1101 ? 10 : level < 1601 ? 11 : 12
            };

            if (type == LevelType.BigFlask)
            {
                colors = range.medium + 1;
                spec.BigFlask = true;
                spec.EmptyBottles = 1;
            }
            else if (type == LevelType.Special)
            {
                colors = range.medium;
                spec.ShapePool = ShapesFor(level);
            }

            spec.Colors = Mathf.Min(colors, spec.PaletteSize);
            if (range.hiddenShare > 0 && StableRoll(level) < range.hiddenShare)
                spec.HiddenLayers = level < 151 ? 1 : 2;
            return spec;
        }

        static List<string> ShapesFor(int level)
        {
            var shapes = new List<string>();
            foreach (var (from, shape) in SpecialShapes)
                if (level >= from)
                    shapes.Add(shape);
            return shapes;
        }

        // Детерминированная «монетка» по номеру: один и тот же уровень всегда со скрытыми слоями или без.
        static int StableRoll(int level)
        {
            unchecked
            {
                uint x = (uint)level * 2654435761u;
                x ^= x >> 15;
                return (int)(x % 100);
            }
        }

        // LEVEL_DESIGN §6.2, формула в EconomyRules.
        public static int Reward(int level, int stars) => EconomyRules.LevelReward(TypeOf(level), level, stars);

        public static bool BoosterUnlocked(Booster booster, int level) => level >= BoosterUnlockLevel(booster);

        public static int BoosterUnlockLevel(Booster booster)
        {
            switch (booster)
            {
                case Booster.Undo: return 4;
                case Booster.Restart: return 6;
                case Booster.Hint: return 8;
                default: return 12;
            }
        }
    }

    public enum Booster
    {
        Undo,
        Restart,
        Hint,
        ExtraBottle
    }
}
