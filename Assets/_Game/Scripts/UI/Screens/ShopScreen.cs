using System.Collections.Generic;
using BottleSort.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BottleSort.UI
{
    // Магазин из Shop.dc.html: модели бутылок и фоны. Цены и уровни открытия — BOTTLE_MODELS.md §4 и LEVEL_DESIGN.md §6.4.
    // Покупки пока в PlayerData; когда появится экономика в services, логика pick переедет туда.
    public sealed class ShopScreen : UiScreen
    {
        sealed class Item
        {
            public string Id;
            public string NameKey;
            public int Price;
            public int UnlockLevel;
            public string Colors;
            public Theme? Theme;
        }

        const string AutoTheme = "auto";

        static readonly Item[] Bottles =
        {
            new Item { Id = "classic", NameKey = "shape_classic", Price = 0, UnlockLevel = 1, Colors = "RYBG" },
            new Item { Id = "wine", NameKey = "shape_wine", Price = 0, UnlockLevel = 30, Colors = "BCKY" },
            new Item { Id = "glass", NameKey = "shape_glass", Price = 300, UnlockLevel = 30, Colors = "ROYL" },
            new Item { Id = "flask", NameKey = "shape_flask", Price = 500, UnlockLevel = 45, Colors = "CBPK" },
            new Item { Id = "jug", NameKey = "shape_jug", Price = 700, UnlockLevel = 60, Colors = "PKOY" },
            new Item { Id = "jar", NameKey = "shape_jar", Price = 900, UnlockLevel = 80, Colors = "NOYL" },
            new Item { Id = "teapot", NameKey = "shape_teapot", Price = 1100, UnlockLevel = 110, Colors = "GLYO" },
            new Item { Id = "potion", NameKey = "shape_potion", Price = 1300, UnlockLevel = 140, Colors = "PKRO" }
        };

        static readonly Item[] Themes =
        {
            new Item { Id = AutoTheme, NameKey = "theme_auto", Price = 0, UnlockLevel = 1, Colors = "BCB" },
            new Item { Id = "navy", NameKey = "theme_navy", Price = 0, UnlockLevel = 1, Colors = "BCB", Theme = UI.Theme.Navy },
            new Item { Id = "violet", NameKey = "theme_violet", Price = 500, UnlockLevel = 51, Colors = "PKP", Theme = UI.Theme.Violet },
            new Item { Id = "teal", NameKey = "theme_teal", Price = 600, UnlockLevel = 101, Colors = "CGC", Theme = UI.Theme.Teal },
            new Item { Id = "emerald", NameKey = "theme_emerald", Price = 700, UnlockLevel = 151, Colors = "GLG", Theme = UI.Theme.Emerald },
            new Item { Id = "rose", NameKey = "theme_rose", Price = 800, UnlockLevel = 201, Colors = "KRK", Theme = UI.Theme.Rose },
            new Item { Id = "amber", NameKey = "theme_amber", Price = 1000, UnlockLevel = 251, Colors = "OYO", Theme = UI.Theme.Amber }
        };

        [SerializeField] Button backButton;
        [SerializeField] TMP_Text coins;
        [SerializeField] BottleStubView preview;
        [SerializeField] LocalizedText previewName;
        [SerializeField] Button bottlesTab;
        [SerializeField] Button themesTab;
        [SerializeField] UiGradient bottlesTabBackground;
        [SerializeField] UiGradient themesTabBackground;
        [SerializeField] LocalizedText tabHint;
        [SerializeField] RectTransform grid;
        [SerializeField] ShopCard cardTemplate;
        [SerializeField] Toast toast;

        readonly List<ShopCard> _cards = new List<ShopCard>();
        bool _bottlesTab = true;
        Item _previewItem;

        public override ScreenId Id => ScreenId.Shop;
        public override Theme? BackdropTheme => LevelPlan.PlayerTheme ?? Theme.Navy;

        void Awake()
        {
            backButton.onClick.AddListener(() => Router.Show(ScreenId.Menu));
            bottlesTab.onClick.AddListener(() => SwitchTab(true));
            themesTab.onClick.AddListener(() => SwitchTab(false));
        }

        void OnEnable() => PlayerData.Changed += RefreshCoins;
        void OnDisable() => PlayerData.Changed -= RefreshCoins;

        public override bool OnBack()
        {
            Router.Show(ScreenId.Menu);
            return true;
        }

        protected override void OnOpen()
        {
            RefreshCoins();
            _previewItem = Find(Bottles, PlayerData.Current.bottleSkin) ?? Bottles[0];
            SwitchTab(true);
        }

        void RefreshCoins() => coins.text = Loc.Number(PlayerData.Current.coins);

        void SwitchTab(bool bottles)
        {
            _bottlesTab = bottles;
            var on = (UiColors.Hex("#35C7FF"), UiColors.Hex("#0870E8"));
            var off = (Color.clear, Color.clear);
            bottlesTabBackground.SetColors(bottles ? on.Item1 : off.Item1, bottles ? on.Item2 : off.Item2);
            themesTabBackground.SetColors(bottles ? off.Item1 : on.Item1, bottles ? off.Item2 : on.Item2);
            tabHint.SetKey(bottles ? "shop_hint_bottles" : "shop_hint_themes");
            Rebuild();
        }

        void Rebuild()
        {
            var items = _bottlesTab ? Bottles : Themes;
            while (_cards.Count < items.Length)
            {
                var card = Instantiate(cardTemplate, grid);
                int index = _cards.Count;
                card.Pressed += () => Pick(index);
                _cards.Add(card);
            }

            for (int i = 0; i < _cards.Count; i++)
            {
                bool used = i < items.Length;
                _cards[i].gameObject.SetActive(used);
                if (!used)
                    continue;
                var item = items[i];
                if (_bottlesTab)
                    _cards[i].ShowBottle(item.NameKey, item.Colors);
                else
                    _cards[i].ShowTheme(item.NameKey, item.Theme ?? LevelPlan.ThemeOf(PlayerData.Current.highestUnlocked), item.Colors);
                _cards[i].SetState(StateOf(item), item.Price, item.UnlockLevel, IsNew(item));
            }

            var bottle = new Bottle(4);
            foreach (char letter in _previewItem.Colors)
                bottle.Push(Palette.FromLetter(letter));
            preview.Show(bottle, 4);
            previewName.SetKey(_previewItem.NameKey);
            Router.RefreshBackdrop();
        }

        ShopCard.State StateOf(Item item)
        {
            var data = PlayerData.Current;
            string selected = _bottlesTab ? data.bottleSkin : data.theme;
            if (item.Id == selected)
                return ShopCard.State.Selected;
            if (Owns(item))
                return ShopCard.State.Owned;
            if (data.highestUnlocked < item.UnlockLevel)
                return ShopCard.State.Locked;
            return data.coins >= item.Price ? ShopCard.State.Affordable : ShopCard.State.TooExpensive;
        }

        static bool Owns(Item item)
        {
            var data = PlayerData.Current;
            if (item.Id == AutoTheme || data.Owns(item.Id))
                return true;
            // Бесплатное (classic, ночь, подарочная «Длинная») открывается само по уровню.
            return item.Price == 0 && item.UnlockLevel <= data.highestUnlocked;
        }

        // «Новинка» — модель открылась в последние 30 уровней.
        static bool IsNew(Item item)
        {
            int level = PlayerData.Current.highestUnlocked;
            return item.UnlockLevel > 1 && level >= item.UnlockLevel && level < item.UnlockLevel + 30;
        }

        void Pick(int index)
        {
            var items = _bottlesTab ? Bottles : Themes;
            var item = items[index];
            var data = PlayerData.Current;
            if (_bottlesTab)
                _previewItem = item;

            switch (StateOf(item))
            {
                case ShopCard.State.Locked:
                    toast.Show(Loc.Format("toast_shop_locked", item.UnlockLevel));
                    break;
                case ShopCard.State.TooExpensive:
                    toast.ShowKey("toast_no_coins");
                    break;
                case ShopCard.State.Affordable:
                    if (data.TrySpend(item.Price))
                    {
                        data.owned.Add(item.Id);
                        Select(item);
                        UiFeedback.Play(UiSound.Reward);
                        toast.Show(Loc.Format("toast_bought", Loc.Get(item.NameKey)));
                    }
                    break;
                default:
                    Select(item);
                    break;
            }
            Rebuild();
        }

        void Select(Item item)
        {
            var data = PlayerData.Current;
            if (_bottlesTab)
                data.bottleSkin = item.Id;
            else
                data.theme = item.Id;
            data.Save();
        }

        static Item Find(Item[] items, string id)
        {
            foreach (var item in items)
                if (item.Id == id)
                    return item;
            return null;
        }
    }
}
