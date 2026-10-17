namespace BottleSort.Services
{
    // Новый баннер — раз в 30 секунд, пока его реально видно. Скрыт, приложение свёрнуто или идёт
    // полноэкранная реклама — время не идёт. Пока грузится следующий, второй запрос не шлём.
    // Не загрузился — старый остаётся на месте, новая попытка через 10 секунд видимого времени.
    public sealed class BannerRefreshTimer
    {
        public const float Interval = 30f;
        public const float RetryAfter = 10f;
        // После сворачивания первый кадр приходит с огромным deltaTime — его не считаем.
        const float MaxStep = 0.5f;

        float _visibleTime;
        bool _loading;

        public bool IsLoading => _loading;
        public float VisibleTime => _visibleTime;

        // true — пора грузить новый баннер.
        public bool Tick(float deltaTime, bool visible)
        {
            if (!visible || _loading || deltaTime <= 0f)
                return false;
            _visibleTime += deltaTime < MaxStep ? deltaTime : MaxStep;
            if (_visibleTime < Interval)
                return false;
            _loading = true;
            return true;
        }

        public void OnRefreshed(bool loaded)
        {
            _loading = false;
            _visibleTime = loaded ? 0f : Interval - RetryAfter;
        }

        // Баннер создан заново (первый показ) — отсчёт с нуля.
        public void Reset()
        {
            _loading = false;
            _visibleTime = 0f;
        }
    }
}
