using System;

namespace BottleSort.Core
{
    // Обучающие уровни 1–3 из LEVEL_DESIGN.md §3.4. Генератор их не трогает.
    public static class HandmadeLevels
    {
        public const int TutorialCount = 3;

        private static readonly string[] TutorialLayouts =
        {
            "RRBB BBRR - -",
            "GBRR RGBB BRGG - -",
            "YYYR RRBY BGGB GBRG - -"
        };

        private static readonly int[] TutorialPars = { 3, 7, 9 };

        public static bool IsTutorial(int level) => level >= 1 && level <= TutorialCount;

        public static LevelState Tutorial(int level)
        {
            if (!IsTutorial(level))
                throw new ArgumentOutOfRangeException(nameof(level));
            return LevelState.FromLayout(4, TutorialLayouts[level - 1]);
        }

        public static int TutorialPar(int level)
        {
            if (!IsTutorial(level))
                throw new ArgumentOutOfRangeException(nameof(level));
            return TutorialPars[level - 1];
        }
    }
}
