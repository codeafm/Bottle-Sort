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
    // Параметры генератора по номеру уровня — в Core (LevelParams), сами уровни 1–2000 — в базе (LevelLoader).
    public static class LevelPlan
    {
        static readonly Theme[] ChapterThemes =
        {
            Theme.Navy, Theme.Teal, Theme.Violet, Theme.Emerald, Theme.Rose, Theme.Amber, Theme.Navy, Theme.Teal,
            Theme.Violet, Theme.Rose, Theme.Emerald, Theme.Amber, Theme.Navy, Theme.Teal, Theme.Amber, Theme.Violet,
            Theme.Teal, Theme.Rose, Theme.Emerald, Theme.Violet, Theme.Amber, Theme.Navy, Theme.Emerald, Theme.Rose,
            Theme.Navy
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
