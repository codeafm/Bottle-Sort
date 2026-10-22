using BottleSort.UI;

namespace BottleSort.Services
{
    public struct BannerContext
    {
        public ScreenId Screen;
        public bool FullscreenAdShowing;
        public bool NoAds;
    }

    // Решение заказчика: баннер виден всегда — в меню, на карте, в магазине, в игре на любом уровне
    // (и на обучении), на победе и поверх попапов. Прячем только на заставке, пока идёт полноэкранная
    // реклама и после покупки «Без рекламы». Место под него внизу (320×50 над safe area) держит ui.
    public static class BannerRules
    {
        // Попапы своего экрана не меняют: под ними остаётся баннер той сцены, где они открыты.
        public static BannerPlacement PlacementOf(ScreenId screen)
        {
            switch (screen)
            {
                case ScreenId.Splash:
                    return BannerPlacement.None;
                case ScreenId.Game:
                case ScreenId.Win:
                    return BannerPlacement.Game;
                default:
                    return BannerPlacement.Menu;
            }
        }

        // Если у игры нет своего блока в кабинете, меню и игра показывают один и тот же баннер:
        // один объект и один поток запросов вместо двух, второй не обновляется где-то в фоне.
        public static BannerPlacement SharedWhenSameUnit(BannerPlacement placement, bool sameUnit) =>
            sameUnit && placement == BannerPlacement.Game ? BannerPlacement.Menu : placement;

        public static bool ShouldShow(BannerContext context) =>
            !context.NoAds && !context.FullscreenAdShowing && PlacementOf(context.Screen) != BannerPlacement.None;
    }
}
