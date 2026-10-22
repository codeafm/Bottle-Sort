using System;

namespace BottleSort.Services
{
    // Состояние одного полноэкранного ролика (межстраничный или за награду). Загруженный ролик живёт
    // 30 минут, как во Flutter (ad_cache.dart), потом его надо выбросить и загрузить новый.
    // После ошибки загрузки следующий запрос не раньше чем через 30 с, дальше пауза удваивается до 5 минут:
    // когда у Yandex нет рекламы, повтор каждые 10 секунд только плодит пустые запросы.
    // Время — настоящее (UTC, секунды): на iOS приложение в фоне не тикает, а реклама при этом стареет.
    public sealed class FullscreenAdSlot
    {
        public const double Lifetime = 30 * 60;
        public const double FirstRetry = 30;
        public const double MaxRetry = 5 * 60;

        double _loadedAt;
        double _retryAt;
        int _failures;

        public bool Loaded { get; private set; }
        public bool Loading { get; private set; }

        public bool IsFresh(double now) => Loaded && now - _loadedAt < Lifetime;

        public static double RetryDelay(int failures) =>
            failures <= 0 ? 0 : Math.Min(MaxRetry, FirstRetry * Math.Pow(2, failures - 1));

        // true — пора загружать. stale — загруженный ролик протух, его нужно уничтожить перед новой загрузкой.
        public bool NeedsLoad(double now, out bool stale)
        {
            stale = Loaded && !IsFresh(now);
            return !Loading && !IsFresh(now) && now >= _retryAt;
        }

        public void OnLoadStarted()
        {
            Loading = true;
            Loaded = false;
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
            Loaded = false;
            _failures++;
            _retryAt = now + RetryDelay(_failures);
        }

        // Ролик ушёл на показ — дальше нужен новый.
        public void OnTaken() => Loaded = false;
    }
}
