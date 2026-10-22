using System;

namespace BottleSort.Services
{
    public enum BannerPlacement
    {
        None,
        Menu,
        Game
    }

    // Когда просить у Yandex новый баннер. Фиксированный 320×50 SDK сам не обновляет (на телефоне 22.10 одна
    // реклама висела 7 минут), поэтому меняем её сами: раз в 60 секунд, пока баннер на экране и игра на переднем
    // плане. После отказа («рекламы нет» или ошибка) — тоже через 60 секунд. РСЯ разрешает не чаще раза в 30 с.
    public sealed class BannerScheduler
    {
        public const float RefreshEvery = 60f;
        // После сворачивания первый кадр приходит с огромным deltaTime — его не считаем.
        const float MaxStep = 0.5f;

        sealed class Slot
        {
            public bool Requested;
            public bool Loading;
            public bool Loaded;
            public float ShownSinceRequest;
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
            if (slot == null || slot.Loading || !active || deltaTime <= 0f)
                return;
            slot.ShownSinceRequest += Math.Min(deltaTime, MaxStep);
            if (slot.ShownSinceRequest >= RefreshEvery)
                Request(Shown, slot);
        }

        // Не загрузилось — на экране остаётся прежняя реклама, следующая попытка через минуту показа.
        public void OnLoaded(BannerPlacement placement, bool loaded)
        {
            var slot = SlotOf(placement);
            if (slot == null)
                return;
            slot.Loading = false;
            slot.ShownSinceRequest = 0f;
            if (loaded)
                slot.Loaded = true;
        }

        void Request(BannerPlacement placement, Slot slot)
        {
            if (slot.Loading)
                return;
            slot.Loading = true;
            slot.ShownSinceRequest = 0f;
            _load(placement);
        }

        Slot SlotOf(BannerPlacement placement) =>
            placement == BannerPlacement.Menu ? _menu : placement == BannerPlacement.Game ? _game : null;
    }
}
