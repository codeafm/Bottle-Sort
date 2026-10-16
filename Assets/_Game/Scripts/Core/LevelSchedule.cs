using System;

namespace BottleSort.Core
{
    public enum LevelType
    {
        Normal,
        Special,
        BigFlask,
        Rescue,
        Boss,
        ChapterBoss
    }

    public enum LevelTier
    {
        Easy,
        Medium,
        Hard,
        Boss
    }

    // Ритм уровней из LEVEL_DESIGN.md §1–2: главы, тип уровня и место на «пиле» сложности.
    public static class LevelSchedule
    {
        public const int ShortChapterLength = 50;
        public const int LongChapterLength = 100;
        public const int ShortChapters = 10;
        private const int ShortChaptersEnd = ShortChapters * ShortChapterLength;

        // Позиции 1..50 внутри блока из 50 уровней.
        private const string TierPattern =
            "EMMHMEMHEM" +
            "EMHEMMMHEM" +
            "EMMEBEEEMM" +
            "EMHEMMMHEM" +
            "EMHEMMHEEB";

        public static int ChapterOf(int level)
        {
            CheckLevel(level);
            if (level <= ShortChaptersEnd)
                return (level - 1) / ShortChapterLength + 1;
            return ShortChapters + (level - ShortChaptersEnd - 1) / LongChapterLength + 1;
        }

        public static int ChapterStart(int chapter)
        {
            if (chapter < 1)
                throw new ArgumentOutOfRangeException(nameof(chapter));
            if (chapter <= ShortChapters)
                return (chapter - 1) * ShortChapterLength + 1;
            return ShortChaptersEnd + (chapter - ShortChapters - 1) * LongChapterLength + 1;
        }

        public static bool IsChapterEnd(int level) => ChapterOf(level) != ChapterOf(level + 1);

        public static LevelType TypeOf(int level)
        {
            CheckLevel(level);
            if (level % 25 == 0)
                return IsChapterEnd(level) ? LevelType.ChapterBoss : LevelType.Boss;
            if (level % 10 == 0 && level >= 30)
                return LevelType.Special;
            if (level % 10 == 5 && level >= 45)
                return LevelType.BigFlask;
            if (level % 50 == 28 && level >= 78)
                return LevelType.Rescue;
            return LevelType.Normal;
        }

        public static LevelTier TierOf(int level)
        {
            CheckLevel(level);
            int position = (level - 1) % 50;
            switch (TierPattern[position])
            {
                case 'E': return LevelTier.Easy;
                case 'H': return LevelTier.Hard;
                case 'B': return LevelTier.Boss;
                default: return LevelTier.Medium;
            }
        }

        private static void CheckLevel(int level)
        {
            if (level < 1)
                throw new ArgumentOutOfRangeException(nameof(level), level, "Levels start at 1");
        }
    }
}
