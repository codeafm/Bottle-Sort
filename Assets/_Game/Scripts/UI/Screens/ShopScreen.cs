using System.Collections.Generic;
using BottleSort.Core;
using BottleSort.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BottleSort.UI
{
    // Магазин из Shop.dc.html: модели бутылок и фоны. Цены, открытие и покупка — Services.ShopCatalog и Shop,
    // здесь только картинка карточек.
    public sealed class ShopScreen : UiScreen
    {
        sealed class Item
        {
            public string Id;
            public string NameKey;
            public string Colors;
            public Theme? Theme;
            public ShopItem Data;
        }

        static readonly Item[] Bottles =
        {
            // Порядок и цвета как в Shop.dc.html; классическая и стакан — в конце, их в макете нет.
            new Item { Id = "wine", NameKey = "shape_wine", Colors = "BCKY" },
            new Item { Id = "jug", NameKey = "shape_jug", Colors = "PKOY" },
            new Item { Id = "teapot", NameKey = "shape_teapot", Colors = "GLYO" },
            new Item { Id = "flask", NameKey = "shape_flask", Colors = "CBPK" },
            new Item { Id = "potion", NameKey = "shape_potion", Colors = "PKRO" },
            new Item { Id = "jar", NameKey = "shape_jar", Colors = "NOYL" },
            new Item { Id = "glass", NameKey = "shape_glass", Colors = "ROYL" },
            new Item { Id = "classic", NameKey = "shape_classic", Colors = "RYBG" }
        };

        static readonly Item[] Themes =
        {
            new Item { Id = ShopCatalog.AutoTheme, NameKey = "theme_auto", Colors = "BCB" },
            new Item { Id = "navy", NameKey = "theme_navy", Colors = "BCB", Theme = UI.Theme.Navy },
            new Item { Id = "violet", NameKey = "theme_violet", Colors = "PKP", Theme = UI.Theme.Violet },
            new Item { Id = "teal", NameKey = "theme_teal", Colors = "CGC", Theme = UI.Theme.Teal },
            new Item { Id = "emerald", NameKey = "theme_emerald", Colors = "GLG", Theme = UI.Theme.Emerald },
            new Item { Id = "rose", NameKey = "theme_rose", Colors = "KRK", Theme = UI.Theme.Rose },
            new Item { Id = "amber", NameKey = "theme_amber", Colors = "OYO", Theme = UI.Theme.Amber }
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

        static ShopScreen()
        {
            foreach (var item in Bottles)
                item.Data = ShopCatalog.Find(ShopCategory.Bottle, item.Id);
            foreach (var item in Themes)
                item.Data = ShopCatalog.Find(ShopCategory.Theme, item.Id);
        }

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
                    _cards[i].ShowBottle(item.NameKey, item.Id, item.Colors);
                else
                    _cards[i].ShowTheme(item.NameKey, item.Theme ?? LevelPlan.ThemeOf(PlayerData.Current.highestUnlocked), item.Colors);
                _cards[i].SetState(StateOf(item), item.Data.Price, item.Data.UnlockLevel, IsNew(item));
            }

            var bottle = new Bottle(4, _previewItem.Id);
            foreach (char letter in _previewItem.Colors)
                bottle.Push(Palette.FromLetter(letter));
            preview.Show(bottle, 4);
            previewName.SetKey(_previewItem.NameKey);
            Router.RefreshBackdrop();
        }

        static ShopCard.State StateOf(Item item)
        {
            switch (Shop.StateOf(PlayerData.Current, item.Data))
            {
                case ShopItemState.Selected: return ShopCard.State.Selected;
                case ShopItemState.Owned: return ShopCard.State.Owned;
                case ShopItemState.Affordable: return ShopCard.State.Affordable;
                case ShopItemState.TooExpensive: return ShopCard.State.TooExpensive;
                default: return ShopCard.State.Locked;
            }
        }

        // «Новинка» — модель открылась в последние 30 уровней.
        static bool IsNew(Item item)
        {
            int level = PlayerData.Current.highestUnlocked;
            int unlock = item.Data.UnlockLevel;
            return unlock > 1 && level >= unlock && level < unlock + 30;
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
                    toast.Show(Loc.Format("toast_shop_locked", item.Data.UnlockLevel));
                    break;
                case ShopCard.State.TooExpensive:
                    toast.ShowKey("toast_no_coins");
                    break;
                case ShopCard.State.Affordable:
                    if (Shop.TryBuy(data, item.Data) == PurchaseResult.Bought)
                    {
                        UiFeedback.Play(UiSound.Coin);
                        toast.Show(Loc.Format("toast_bought", Loc.Get(item.NameKey)));
                    }
                    break;
                default:
                    Shop.TrySelect(data, item.Data);
                    break;
            }
            Rebuild();
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
