namespace BottleSort.Services
{
    public enum InterstitialDecision
    {
        Show,
        NotDue,
        Spacing,
        Tutorial,
        NoAds
    }

    // Решение заказчика вместо LEVEL_DESIGN §7.5: межстраничная после каждой 2-й победы и после каждого
    // 2-го проигрыша, победы и проигрыши считаются отдельно, не на обучении 1–3. Между любыми двумя
    // полноэкранными роликами не меньше 30 секунд. Ролик за награду (×2 на победе и т. п.) только сдвигает
    // этот отсчёт: заказчик хочет межстраничную каждые 2 уровня, даже если игрок каждый раз жмёт ×2.
    public sealed class InterstitialPolicy
    {
        public const int WinsBetween = 2;
        public const int LossesBetween = 2;
        public const double MinSecondsBetween = 30;
        public const int FirstLevel = 4;

        int _wins;
        int _losses;
        double _lastFullscreenAt = double.NegativeInfinity;

        public int WinsSinceAd => _wins;
        public int LossesSinceAd => _losses;

        public void OnRewardedWatched(double now)
        {
            if (now > _lastFullscreenAt)
                _lastFullscreenAt = now;
        }

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

        public InterstitialDecision DecideAfterWin(int level, bool noAds, double now) =>
            Decide(_wins >= WinsBetween, level, noAds, now);

        public InterstitialDecision DecideAfterLoss(int level, bool noAds, double now) =>
            Decide(_losses >= LossesBetween, level, noAds, now);

        public bool ShouldShowAfterWin(int level, bool noAds, double now) =>
            DecideAfterWin(level, noAds, now) == InterstitialDecision.Show;

        public bool ShouldShowAfterLoss(int level, bool noAds, double now) =>
            DecideAfterLoss(level, noAds, now) == InterstitialDecision.Show;

        // Пропущенный показ не сбрасывает счётчик: реклама выйдет при следующей возможности.
        public void OnShownAfterWin(double now)
        {
            _wins = 0;
            _lastFullscreenAt = now;
        }

        public void OnShownAfterLoss(double now)
        {
            _losses = 0;
            _lastFullscreenAt = now;
        }

        InterstitialDecision Decide(bool due, int level, bool noAds, double now)
        {
            if (noAds)
                return InterstitialDecision.NoAds;
            if (level < FirstLevel)
                return InterstitialDecision.Tutorial;
            if (!due)
                return InterstitialDecision.NotDue;
            return now - _lastFullscreenAt >= MinSecondsBetween ? InterstitialDecision.Show : InterstitialDecision.Spacing;
        }
    }
}
