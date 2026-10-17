using BottleSort.UI;

namespace BottleSort.Services
{
    public struct BannerContext
    {
        public ScreenId Screen;
        // Уровень на игровом экране; для остальных экранов не важен.
        public int Level;
        public bool PopupOpen;
        public bool FullscreenAdShowing;
        public int HighestUnlocked;
        public bool NoAds;
    }

    // Баннер Yandex — в меню, на карте уровней и в игре, как во Flutter. Место под него внизу (320×50 над
    // нижним краем safe area) ui оставил в Main.dc.html и Game.dc.html. Не показываем на обучении,
    // заставке, победе, поверх попапов и пока идёт полноэкранная реклама.
    public static class BannerRules
    {
        public const int FromLevel = 4;

        public static BannerPlacement PlacementOf(ScreenId screen) =>
            screen == ScreenId.Game ? BannerPlacement.Game
            : screen == ScreenId.Menu || screen == ScreenId.Levels ? BannerPlacement.Menu
            : BannerPlacement.None;

        // Есть ли смысл грузить игровой баннер для этого уровня.
        public static bool AllowedOnLevel(int level, int highestUnlocked, bool noAds) =>
            !noAds && level >= FromLevel && highestUnlocked >= FromLevel;

        public static bool ShouldShow(BannerContext context)
        {
            if (context.NoAds || context.FullscreenAdShowing || context.PopupOpen)
                return false;
            if (context.HighestUnlocked < FromLevel)
                return false;
            switch (context.Screen)
            {
                case ScreenId.Menu:
                case ScreenId.Levels:
                    return true;
                case ScreenId.Game:
                    return context.Level >= FromLevel;
                default:
                    return false;
            }
        }
    }
}
