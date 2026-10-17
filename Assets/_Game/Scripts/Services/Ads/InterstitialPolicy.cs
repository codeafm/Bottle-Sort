namespace BottleSort.Services
{
    // Решение заказчика вместо LEVEL_DESIGN §7.5: межстраничная после каждой 2-й победы и после каждого
    // 2-го проигрыша, победы и проигрыши считаются отдельно. Не на обучении 1–3 и не сразу после того,
    // как игрок сам смотрел рекламу за награду на этом уровне. Между двумя показами не меньше 30 секунд,
    // чтобы два быстрых проигрыша подряд и победа следом не дали три ролика за минуту.
    public sealed class InterstitialPolicy
    {
        public const int WinsBetween = 2;
        public const int LossesBetween = 2;
        public const double MinSecondsBetween = 30;
        public const int FirstLevel = 4;

        int _wins;
        int _losses;
        double _lastShownAt = double.NegativeInfinity;
        bool _rewardedThisLevel;

        public int WinsSinceAd => _wins;
        public int LossesSinceAd => _losses;

        public void OnLevelStarted() => _rewardedThisLevel = false;
        public void OnRewardedWatched() => _rewardedThisLevel = true;

        public void OnWin(int level)
        {
            if (level >= FirstLevel)
                _wins++;
        }

        public void OnLoss(int level)
        {
            if (level >= FirstLevel)
                _losses++;
        }

        public bool ShouldShowAfterWin(int level, bool noAds, double now) =>
            _wins >= WinsBetween && Allowed(level, noAds, now);

        public bool ShouldShowAfterLoss(int level, bool noAds, double now) =>
            _losses >= LossesBetween && Allowed(level, noAds, now);

        // Пропущенный показ не сбрасывает счётчик: реклама выйдет при следующей возможности.
        public void OnShownAfterWin(double now)
        {
            _wins = 0;
            _lastShownAt = now;
        }

        public void OnShownAfterLoss(double now)
        {
            _losses = 0;
            _lastShownAt = now;
        }

        bool Allowed(int level, bool noAds, double now) =>
            !noAds && level >= FirstLevel && !_rewardedThisLevel && now - _lastShownAt >= MinSecondsBetween;
    }
}
