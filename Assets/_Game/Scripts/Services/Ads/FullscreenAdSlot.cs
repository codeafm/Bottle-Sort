using System;

namespace BottleSort.Services
{
    // Когда грузить полноэкранный ролик: держим один готовый, второй параллельно не грузим.
    // Межстраничная выходит раз в минуту, поэтому после ошибки повторяем быстро (5, 15, 30 с, дальше раз в минуту),
    // а за 5 минут до конца срока грузим замену, пока старая ещё годится: к концу уровня ролик должен быть.
    // Ролик за награду нужен, только когда игрок нажал кнопку: после трёх ошибок подряд сам не грузим,
    // ждём нажатия — так у игрока без рекламы в регионе не уходит запрос каждые пять минут.
    // Время — настоящее (UTC, секунды): на iOS приложение в фоне не тикает, а реклама при этом стареет.
    public sealed class FullscreenAdSlot
    {
        // Как во Flutter (ad_cache.dart): дольше 30 минут ролик не держим.
        public const double Lifetime = 30 * 60;
        // Загрузка, от которой за минуту не пришло ни ответа, ни ошибки, считается ошибкой: иначе слот
        // ждал бы её до перезапуска игры и не грузил ничего нового.
        public const double LoadTimeout = 60;
        // Игрок жмёт «за рекламу», а ролика нет: грузим сразу, но не чаще раза в 10 с, сколько бы ни нажимал.
        public const double PlayerRequestGap = 10;

        readonly double[] _retryDelays;
        readonly bool _retryForever;
        readonly double _renewAfter;

        double _loadedAt;
        double _loadStartedAt = double.NegativeInfinity;
        double _retryAt;
        int _failures;

        public static FullscreenAdSlot Interstitial() => new FullscreenAdSlot(new double[] { 5, 15, 30, 60 }, true, 25 * 60);
        public static FullscreenAdSlot Rewarded() => new FullscreenAdSlot(new double[] { 30, 60, 120 }, false, Lifetime);

        FullscreenAdSlot(double[] retryDelays, bool retryForever, double renewAfter)
        {
            _retryDelays = retryDelays;
            _retryForever = retryForever;
            _renewAfter = renewAfter;
        }

        public bool Loaded { get; private set; }
        public bool Loading { get; private set; }
        public int Failures => _failures;

        public bool IsFresh(double now) => Loaded && now - _loadedAt < Lifetime;
        public bool LoadTimedOut(double now) => Loading && now - _loadStartedAt >= LoadTimeout;

        // Пауза после failures ошибок подряд; double.PositiveInfinity — сами больше не пробуем.
        public double RetryDelay(int failures)
        {
            if (failures <= 0)
                return 0;
            if (failures <= _retryDelays.Length)
                return _retryDelays[failures - 1];
            return _retryForever ? _retryDelays[_retryDelays.Length - 1] : double.PositiveInfinity;
        }

        // playerWaiting — игрок нажал кнопку и ждёт ролик: паузу после ошибок не выдерживаем.
        public bool NeedsLoad(double now, bool playerWaiting = false)
        {
            if (Loading)
                return false;
            if (playerWaiting)
                return !IsFresh(now) && now - _loadStartedAt >= PlayerRequestGap;
            bool wanted = !Loaded || now - _loadedAt >= _renewAfter;
            return wanted && now >= _retryAt;
        }

        // Старый ролик, если он есть, остаётся годным до прихода нового.
        public void OnLoadStarted(double now)
        {
            Loading = true;
            _loadStartedAt = now;
        }

        public void OnLoaded(double now)
        {
            Loading = false;
            Loaded = true;
            _loadedAt = now;
            _failures = 0;
            _retryAt = 0;
        }

        public void OnFailed(double now)
        {
            Loading = false;
            _failures++;
            _retryAt = now + RetryDelay(_failures);
        }

        // Ролик ушёл на показ или протух и выброшен — новый грузим сразу, даже если замена до этого не грузилась.
        public void OnTaken()
        {
            Loaded = false;
            _retryAt = 0;
        }
    }
}
