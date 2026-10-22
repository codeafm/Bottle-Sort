using System;

namespace BottleSort.Services
{
    public enum BannerPlacement
    {
        None,
        Menu,
        Game
    }

    // Когда просить у Yandex новый баннер. Загруженный баннер SDK сам обновляет раз в 60 секунд,
    // а РСЯ не разрешает обновлять чаще раза в 30 секунд, поэтому свой запрос уходит только при первом
    // показе плейсмента и после отказа («рекламы нет» или ошибка). Повтор — через 60 секунд, пока баннер
    // на экране и игра на переднем плане: тот же ритм, что у самого SDK. Запрос без рекламы CPM не портит
    // (CPMV считается по видимым показам), а без повтора место под баннер так и стоит пустым.
    public sealed class BannerScheduler
    {
        public const float RetryAfter = 60f;
        // После сворачивания первый кадр приходит с огромным deltaTime — его не считаем.
        const float MaxStep = 0.5f;

        sealed class Slot
        {
            public bool Requested;
            public bool Loading;
            public bool Loaded;
            public bool Failed;
            public float WaitedForRetry;
        }

        readonly Action<BannerPlacement> _load;
        readonly Slot _menu = new Slot();
        readonly Slot _game = new Slot();

        public BannerScheduler(Action<BannerPlacement> load)
        {
            _load = load;
        }

        public BannerPlacement Shown { get; private set; }

        public bool IsLoading(BannerPlacement placement) => SlotOf(placement)?.Loading ?? false;
        public bool IsLoaded(BannerPlacement placement) => SlotOf(placement)?.Loaded ?? false;

        public void Show(BannerPlacement placement)
        {
            Shown = placement;
            var slot = SlotOf(placement);
            if (slot == null || slot.Requested)
                return;
            slot.Requested = true;
            Request(placement, slot);
        }

        // active — приложение на переднем плане и нет полноэкранной рекламы.
        public void Tick(float deltaTime, bool active)
        {
            var slot = SlotOf(Shown);
            if (slot == null || slot.Loading || !slot.Failed || !active || deltaTime <= 0f)
                return;
            slot.WaitedForRetry += Math.Min(deltaTime, MaxStep);
            if (slot.WaitedForRetry >= RetryAfter)
                Request(Shown, slot);
        }

        public void OnLoaded(BannerPlacement placement, bool loaded)
        {
            var slot = SlotOf(placement);
            if (slot == null)
                return;
            slot.Loading = false;
            slot.WaitedForRetry = 0f;
            if (loaded)
                slot.Loaded = true;
            slot.Failed = !loaded;
        }

        void Request(BannerPlacement placement, Slot slot)
        {
            if (slot.Loading)
                return;
            slot.Loading = true;
            slot.WaitedForRetry = 0f;
            _load(placement);
        }

        Slot SlotOf(BannerPlacement placement) =>
            placement == BannerPlacement.Menu ? _menu : placement == BannerPlacement.Game ? _game : null;
    }
}
