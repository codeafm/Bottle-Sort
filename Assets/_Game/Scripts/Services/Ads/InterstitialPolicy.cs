using System.Collections.Generic;
using BottleSort.Core;

namespace BottleSort.Services
{
    // LEVEL_DESIGN §7.5: не раньше 21-го, не чаще раза в 3 уровня и раза в 150 секунд; никогда после
    // обучающего уровня фишки, после босса главы и на первом уровне сессии. Во Flutter было просто L % 3 == 0.
    public sealed class InterstitialPolicy
    {
        public const int FirstLevel = 21;
        public const int LevelsBetween = 3;
        public const double SecondsBetween = 150;

        // Первые уровни новых фишек (§5, §10): «?», замок, лёд, тонировка, цветной замок, переворот, пипетка.
        static readonly HashSet<int> FeatureIntroLevels = new HashSet<int> { 101, 151, 201, 251, 401, 601, 701 };

        int _levelsSinceAd;
        double _lastShownAt = double.NegativeInfinity;

        public int LevelsSinceAd => _levelsSinceAd;

        public static bool IsFeatureIntro(int level) => FeatureIntroLevels.Contains(level);

        // Вызывать на каждой победе, до решения о показе.
        public void OnLevelCompleted() => _levelsSinceAd++;

        public bool ShouldShow(int level, LevelType type, bool noAds, double now)
        {
            if (noAds || level < FirstLevel)
                return false;
            if (type == LevelType.ChapterBoss || IsFeatureIntro(level))
                return false;
            // Счётчик живёт только в памяти, поэтому первый уровень сессии сюда не дотягивает сам собой.
            if (_levelsSinceAd < LevelsBetween)
                return false;
            return now - _lastShownAt >= SecondsBetween;
        }

        public void OnShown(double now)
        {
            _levelsSinceAd = 0;
            _lastShownAt = now;
        }
    }
}
