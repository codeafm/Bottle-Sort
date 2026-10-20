namespace BottleSort.Services
{
    // Состояние одного полноэкранного ролика (межстраничный или за награду). Загруженный ролик живёт
    // 30 минут, как во Flutter (ad_cache.dart), потом его надо выбросить и загрузить новый.
    // Время — настоящее (UTC, секунды): на iOS приложение в фоне не тикает, а реклама при этом стареет.
    public sealed class FullscreenAdSlot
    {
        public const double Lifetime = 30 * 60;

        double _loadedAt;

        public bool Loaded { get; private set; }
        public bool Loading { get; private set; }

        public bool IsFresh(double now) => Loaded && now - _loadedAt < Lifetime;

        // true — пора загружать. stale — загруженный ролик протух, его нужно уничтожить перед новой загрузкой.
        public bool NeedsLoad(double now, out bool stale)
        {
            stale = Loaded && !IsFresh(now);
            return !Loading && !IsFresh(now);
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
        }

        public void OnFailed()
        {
            Loading = false;
            Loaded = false;
        }

        // Ролик ушёл на показ — дальше нужен новый.
        public void OnTaken() => Loaded = false;
    }
}
