namespace BottleSort.Services
{
    public enum InterstitialDecision
    {
        Show,
        NotDue,
        Tutorial,
        NoAds
    }

    // Решение заказчика вместо LEVEL_DESIGN §7.5: межстраничная строго после каждой 2-й победы и каждого
    // 2-го проигрыша, победы и проигрыши считаются отдельно, не на обучении 1–3. Ролики за награду
    // на этот счёт не влияют.
    public sealed class InterstitialPolicy
    {
        public const int WinsBetween = 2;
        public const int LossesBetween = 2;
        public const int FirstLevel = 4;

        int _wins;
        int _losses;

        public int WinsSinceAd => _wins;
        public int LossesSinceAd => _losses;

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

        public InterstitialDecision DecideAfterWin(int level, bool noAds) => Decide(_wins >= WinsBetween, level, noAds);

        public InterstitialDecision DecideAfterLoss(int level, bool noAds) => Decide(_losses >= LossesBetween, level, noAds);

        public bool ShouldShowAfterWin(int level, bool noAds) => DecideAfterWin(level, noAds) == InterstitialDecision.Show;

        public bool ShouldShowAfterLoss(int level, bool noAds) => DecideAfterLoss(level, noAds) == InterstitialDecision.Show;

        // Пропущенный показ (ролик не готов) не сбрасывает счётчик: реклама выйдет при следующей возможности.
        public void OnShownAfterWin() => _wins = 0;

        public void OnShownAfterLoss() => _losses = 0;

        static InterstitialDecision Decide(bool due, int level, bool noAds)
        {
            if (noAds)
                return InterstitialDecision.NoAds;
            if (level < FirstLevel)
                return InterstitialDecision.Tutorial;
            return due ? InterstitialDecision.Show : InterstitialDecision.NotDue;
        }
    }
}
