using System.Collections.Generic;
using BottleSort.UI;

namespace BottleSort.Services
{
    public enum ShopCategory
    {
        Bottle,
        Theme
    }

    public enum ShopItemState
    {
        Selected,
        Owned,
        Affordable,
        TooExpensive,
        Locked
    }

    public enum PurchaseResult
    {
        Bought,
        AlreadyOwned,
        Locked,
        NotEnoughCoins,
        Unknown
    }

    public sealed class ShopItem
    {
        public readonly string Id;
        public readonly ShopCategory Category;
        public readonly int Price;
        public readonly int UnlockLevel;

        public ShopItem(string id, ShopCategory category, int price, int unlockLevel)
        {
            Id = id;
            Category = category;
            Price = price;
            UnlockLevel = unlockLevel;
        }

        // Бесплатное (classic, ночь, подарочная «Длинная» на 30-м) открывается само, когда дошёл до уровня.
        public bool IsFreeByLevel => Price == 0;
    }

    // Цены и уровни открытия: BOTTLE_MODELS.md §4 и LEVEL_DESIGN.md §6.4.
    // heart, tube, amphora, crystal, lamp появятся здесь, когда vfx-art нарисует их формы.
    public static class ShopCatalog
    {
        public const string AutoTheme = "auto";

        public static readonly ShopItem[] Bottles =
        {
            new ShopItem("classic", ShopCategory.Bottle, 0, 1),
            new ShopItem("wine", ShopCategory.Bottle, 0, 30),
            new ShopItem("glass", ShopCategory.Bottle, 300, 30),
            new ShopItem("flask", ShopCategory.Bottle, 500, 45),
            new ShopItem("jug", ShopCategory.Bottle, 700, 60),
            new ShopItem("jar", ShopCategory.Bottle, 900, 80),
            new ShopItem("teapot", ShopCategory.Bottle, 1100, 110),
            new ShopItem("potion", ShopCategory.Bottle, 1300, 140)
        };

        public static readonly ShopItem[] Themes =
        {
            new ShopItem(AutoTheme, ShopCategory.Theme, 0, 1),
            new ShopItem("navy", ShopCategory.Theme, 0, 1),
            new ShopItem("violet", ShopCategory.Theme, 500, 51),
            new ShopItem("teal", ShopCategory.Theme, 600, 101),
            new ShopItem("emerald", ShopCategory.Theme, 700, 151),
            new ShopItem("rose", ShopCategory.Theme, 800, 201),
            new ShopItem("amber", ShopCategory.Theme, 1000, 251)
        };

        // Формы, которые не держат пять слоёв (BOTTLE_MODELS §4): на таких уровнях остаётся классика.
        static readonly HashSet<string> FourLayersOnly = new HashSet<string> { "potion", "teapot" };

        public static ShopItem[] Of(ShopCategory category) => category == ShopCategory.Bottle ? Bottles : Themes;

        public static ShopItem Find(ShopCategory category, string id)
        {
            foreach (var item in Of(category))
                if (item.Id == id)
                    return item;
            return null;
        }

        // Выбранная в магазине модель заменяет только классические бутылки: особые формы,
        // большая колба и песочные часы — часть уровня, их не трогаем.
        public static string BottleShapeFor(string levelShape, int capacity, string selectedSkin)
        {
            if (levelShape != "classic" || string.IsNullOrEmpty(selectedSkin) || selectedSkin == "classic")
                return levelShape;
            if (capacity > 4 && FourLayersOnly.Contains(selectedSkin))
                return levelShape;
            return Find(ShopCategory.Bottle, selectedSkin) != null ? selectedSkin : levelShape;
        }
    }

    public static class Shop
    {
        public static bool Owns(PlayerData data, ShopItem item)
        {
            if (item.Id == ShopCatalog.AutoTheme || data.Owns(item.Id))
                return true;
            return item.IsFreeByLevel && item.UnlockLevel <= data.highestUnlocked;
        }

        public static string SelectedId(PlayerData data, ShopCategory category) =>
            category == ShopCategory.Bottle ? data.bottleSkin : data.theme;

        public static ShopItemState StateOf(PlayerData data, ShopItem item)
        {
            if (item.Id == SelectedId(data, item.Category))
                return ShopItemState.Selected;
            if (Owns(data, item))
                return ShopItemState.Owned;
            if (data.highestUnlocked < item.UnlockLevel)
                return ShopItemState.Locked;
            return data.coins >= item.Price ? ShopItemState.Affordable : ShopItemState.TooExpensive;
        }

        // Купленное сразу выбирается: так было во Flutter, и так ждёт игрок.
        public static PurchaseResult TryBuy(PlayerData data, ShopItem item)
        {
            if (item == null)
                return PurchaseResult.Unknown;
            if (Owns(data, item))
                return PurchaseResult.AlreadyOwned;
            if (data.highestUnlocked < item.UnlockLevel)
                return PurchaseResult.Locked;
            if (data.coins < item.Price)
                return PurchaseResult.NotEnoughCoins;

            data.coins -= item.Price;
            Analytics.CoinsSpent(item.Price, (item.Category == ShopCategory.Bottle ? "Бутылка: " : "Фон: ") + item.Id);
            data.owned.Add(item.Id);
            ApplySelection(data, item);
            data.Save();
            return PurchaseResult.Bought;
        }

        public static bool TrySelect(PlayerData data, ShopItem item)
        {
            if (item == null || !Owns(data, item))
                return false;
            ApplySelection(data, item);
            data.Save();
            return true;
        }

        static void ApplySelection(PlayerData data, ShopItem item)
        {
            if (item.Category == ShopCategory.Bottle)
                data.bottleSkin = item.Id;
            else
                data.theme = item.Id;
        }
    }
}
