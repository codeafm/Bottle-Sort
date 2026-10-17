using System;

namespace BottleSort.Services
{
    public enum BannerPlacement
    {
        None,
        Menu,
        Game
    }

    // Когда грузить новый баннер. У меню (с картой уровней) и у игры свой баннер со своими запросами.
    // Меню грузит баннер при первом показе, игра — на старте каждого уровня, включая «Следующий» и «Заново»
    // (так не важно, что раньше: экран или уровень). Оба обновляются раз в 30 секунд, пока их видно.
    // Пока баннер грузится, второй запрос в тот же слот не уходит: запрос по уровню ждёт,
    // а запрос по таймеру просто не начинается. Старый баннер висит, пока новый не загрузился.
    public sealed class BannerScheduler
    {
        public const float Interval = 30f;
        public const float RetryAfter = 10f;
        // После сворачивания первый кадр приходит с огромным deltaTime — его не считаем.
        const float MaxStep = 0.5f;

        sealed class Slot
        {
            public bool Requested;
            public bool Loading;
            public bool Pending;
            public float VisibleTime;
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
        public float VisibleTime(BannerPlacement placement) => SlotOf(placement)?.VisibleTime ?? 0f;

        public void Show(BannerPlacement placement)
        {
            Shown = placement;
            if (placement != BannerPlacement.Menu || _menu.Requested)
                return;
            _menu.Requested = true;
            Request(BannerPlacement.Menu, _menu);
        }

        public void LevelStarted()
        {
            _game.Requested = true;
            _game.VisibleTime = 0f;
            Request(BannerPlacement.Game, _game);
        }

        // active — приложение на переднем плане и нет полноэкранной рекламы.
        public void Tick(float deltaTime, bool active)
        {
            var slot = SlotOf(Shown);
            if (slot == null || !slot.Requested || !active || slot.Loading || deltaTime <= 0f)
                return;
            slot.VisibleTime += Math.Min(deltaTime, MaxStep);
            if (slot.VisibleTime >= Interval)
                Request(Shown, slot);
        }

        public void OnLoaded(BannerPlacement placement, bool loaded)
        {
            var slot = SlotOf(placement);
            if (slot == null)
                return;
            slot.Loading = false;
            slot.VisibleTime = loaded ? 0f : Interval - RetryAfter;
            if (!slot.Pending)
                return;
            slot.Pending = false;
            Request(placement, slot);
        }

        void Request(BannerPlacement placement, Slot slot)
        {
            if (slot.Loading)
            {
                slot.Pending = true;
                return;
            }
            slot.Loading = true;
            slot.VisibleTime = 0f;
            _load(placement);
        }

        Slot SlotOf(BannerPlacement placement) =>
            placement == BannerPlacement.Menu ? _menu : placement == BannerPlacement.Game ? _game : null;
    }
}
