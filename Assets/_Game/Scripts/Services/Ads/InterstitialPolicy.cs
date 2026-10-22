using System;

namespace BottleSort.Services
{
    public enum InterstitialDecision
    {
        Show,
        NotDue,
        Tutorial,
        NoAds
    }

    // Решение заказчика вместо LEVEL_DESIGN §7.5: межстраничная после каждого уровня — и на «Дальше» после победы,
    // и на «Заново» после проигрыша, — но не чаще раза в минуту и не в первую минуту после запуска игры.
    // Минута считается от закрытия прошлой межстраничной. Не на обучении 1–3. Ролики за награду на это не влияют.
    // Время — секунды с запуска игры (Time.realtimeSinceStartup), передаётся снаружи, чтобы правило было проверяемым.
    public sealed class InterstitialPolicy
    {
        public const float MinSecondsBetween = 60f;
        public const int FirstLevel = 4;

        float _lastShownAt;

        // startedAt — время запуска: первая межстраничная не раньше чем через минуту после него.
        public InterstitialPolicy(float startedAt = 0f)
        {
            _lastShownAt = startedAt;
        }

        public float SecondsUntilDue(float now) => Math.Max(0f, _lastShownAt + MinSecondsBetween - now);

        public InterstitialDecision Decide(int level, bool noAds, float now)
        {
            if (noAds)
                return InterstitialDecision.NoAds;
            if (level < FirstLevel)
                return InterstitialDecision.Tutorial;
            return now - _lastShownAt >= MinSecondsBetween ? InterstitialDecision.Show : InterstitialDecision.NotDue;
        }

        public bool ShouldShow(int level, bool noAds, float now) => Decide(level, noAds, now) == InterstitialDecision.Show;

        // Вызывать, когда игрок закрыл межстраничную. Пропущенный показ (ролик не готов) ничего не сбрасывает:
        // реклама выйдет после следующего уровня.
        public void OnShown(float now) => _lastShownAt = now;

        // Грузить ролик заранее есть смысл, только если его вообще могут показать: без «Без рекламы»
        // и когда игрок дошёл до уровней с рекламой. Иначе это загрузка впустую и лишний запрос.
        public static bool MayEverShow(int highestUnlocked, bool noAds) => !noAds && highestUnlocked >= FirstLevel;
    }
}
