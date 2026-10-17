using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static BottleSort.UI.Editor.UiKit;

namespace BottleSort.UI.Editor
{
    public static partial class UiSceneBuilder
    {
        static UiScreen BuildShop(Transform safeArea)
        {
            var screen = ScreenRoot<ShopScreen>(safeArea, "ShopScreen", out var content);

            var back = BackButton(content);
            ((RectTransform)back.transform.parent).Place(new Vector2(0f, 1f), new Vector2(16, -10),
                new Vector2(IconButtonSize, IconButtonSize));
            var title = Text(content, "Title", DisplayFont, 28, Color.white, TextAlignmentOptions.Center, "shop_title");
            title.rectTransform.TopBand(10, IconButtonSize, 80, 80);
            Shadow(title, Hex("#001446", 0.7f), -0.8f);
            var coinsRoot = CoinPill(content, "Coins", 96, 47, false, out var coins);
            coinsRoot.Place(new Vector2(1f, 1f), new Vector2(-16, -12), new Vector2(96, 47));

            // Витрина: вращающееся свечение, подсветка подставки, парящая бутылка и название.
            // Отступы из Shop.dc.html: витрина сразу под шапкой, вкладки через 14, подсказка через 8, сетка через 10.
            var showcase = Node("Showcase", content).TopBand(10 + IconButtonSize, 236, 16, 16);
            var spin = Image(showcase, "Spin", "glow", Hex("#35F4FF", 0.4f));
            spin.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, -16), new Vector2(200, 200), new Vector2(0.5f, 1f));
            Motion(spin.gameObject, IdleMotion.Kind.Pulse, 8f, 0.12f);
            var spot = Image(showcase, "Spot", "glow", Hex("#78DCFF", 0.6f));
            spot.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0, 30), new Vector2(170, 28), new Vector2(0.5f, 0.5f));
            var hover = Node("Hover", showcase);
            hover.Place(new Vector2(0.5f, 0f), new Vector2(0, 30), new Vector2(84, 202), new Vector2(0.5f, 0f));
            Motion(hover.gameObject, IdleMotion.Kind.Float, 3f, M(6));
            var preview = PlaceBottle(hover, "Preview", 84);
            var previewName = Text(showcase, "Name", DisplayFont, 20, Color.white, TextAlignmentOptions.Center, "shape_classic");
            previewName.rectTransform.BottomBand(0, 24);

            var tabs = Rounded(content, "Tabs", 18, Hex("#081440", 0.8f));
            tabs.rectTransform.TopBand(10 + IconButtonSize + 236 + 14, 53, 16, 16);
            Border(tabs.transform, 18, Hex("#96C8FF", 0.25f));
            var bottlesTab = Tab(tabs.transform, "Bottles", 0f, "shop_tab_bottles", out var bottlesBackground);
            var themesTab = Tab(tabs.transform, "Themes", 0.5f, "shop_tab_themes", out var themesBackground);
            var hint = Text(content, "Hint", BodyFont, 12, Hex("#A9CCF2"), TextAlignmentOptions.Center, "shop_hint_bottles");
            hint.rectTransform.TopBand(10 + IconButtonSize + 236 + 14 + 53 + 8, 16.4f);

            // Сверху запас 12 pt под бейджи «НОВИНКА», они выступают над карточкой.
            var scrollRect = Node("Scroll", content).Fill(16, 10 + IconButtonSize + 236 + 14 + 53 + 8 + 16.4f + 10 - 12, 16, 0);
            var scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            var viewport = Node("Viewport", scrollRect).Fill();
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = Color.clear;
            var grid = Node("Grid", viewport);
            grid.anchorMin = new Vector2(0f, 1f);
            grid.anchorMax = new Vector2(1f, 1f);
            grid.pivot = new Vector2(0.5f, 1f);
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.cellSize = M((358f - 20f) / 3f, 170);
            layout.spacing = M(10, 10);
            layout.padding = new RectOffset(0, 0, (int)M(12), (int)M(12));
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 3;
            grid.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = grid;
            var cards = new List<ShopCard>();
            for (int i = 0; i < ShopScreen.CardCount; i++)
            {
                var card = BuildShopCard(grid);
                card.name = "Card " + (i + 1);
                card.gameObject.SetActive(true);
                cards.Add(card);
            }

            var toast = BuildToast(content, 380);

            Set(screen, "backButton", back);
            Set(screen, "coins", coins);
            Set(screen, "preview", preview);
            Set(screen, "previewName", previewName.GetComponent<LocalizedText>());
            Set(screen, "bottlesTab", bottlesTab);
            Set(screen, "themesTab", themesTab);
            Set(screen, "bottlesTabBackground", bottlesBackground);
            Set(screen, "themesTabBackground", themesBackground);
            Set(screen, "tabHint", hint.GetComponent<LocalizedText>());
            Set(screen, "grid", grid);
            Set(screen, "cards", cards);
            Set(screen, "toast", toast);
            return screen;
        }

        static Button Tab(Transform parent, string name, float from, string key, out UiGradient background)
        {
            var root = Node(name, parent);
            root.anchorMin = new Vector2(from, 0f);
            root.anchorMax = new Vector2(from + 0.5f, 1f);
            root.offsetMin = new Vector2(M(4), M(4));
            root.offsetMax = new Vector2(-M(4), -M(4));
            var face = Rounded(root, "Face", 14, Color.white, true);
            face.rectTransform.Fill();
            background = Gradient(face, Hex("#35C7FF"), Hex("#0870E8"));
            var label = Text(root, "Label", DisplayFont, 17, Color.white, TextAlignmentOptions.Center, key);
            label.rectTransform.Fill();
            return Button(face.gameObject, root);
        }

        static ShopCard BuildShopCard(Transform grid)
        {
            var root = Node("CardTemplate", grid);
            var card = root.gameObject.AddComponent<ShopCard>();

            var face = Rounded(root, "Face", 20, Color.white, true);
            face.rectTransform.Fill();
            SoftShadow(face.rectTransform, 6, 14, 0.35f);
            var glow = SoftShadow(face.rectTransform, 0, 18, Hex("#9BFF4F", 0.55f));
            glow.name = "Glow";
            Gradient(face, Hex("#163880", 0.92f), Hex("#081842", 0.92f));
            var border = Border(face.transform, 20, Hex("#96C8FF", 0.3f));

            var bottleRoot = Node("Bottle", face.transform);
            bottleRoot.Place(new Vector2(0.5f, 1f), new Vector2(0, -8), new Vector2(60, 98), new Vector2(0.5f, 1f));
            var bottle = PlaceBottle(bottleRoot, "View", 40);

            var themeRoot = Node("Theme", face.transform);
            themeRoot.Place(new Vector2(0.5f, 1f), new Vector2(0, -8), new Vector2(84, 96), new Vector2(0.5f, 1f));
            themeRoot.gameObject.AddComponent<RectMask2D>();
            var thumbRect = Node("Thumb", themeRoot).Fill();
            var thumb = thumbRect.gameObject.AddComponent<RawImage>();
            thumb.raycastTarget = false;
            var themeBottle = PlaceBottle(themeRoot, "View", 24);
            ((RectTransform)themeBottle.transform).anchoredPosition = new Vector2(0f, M(8));
            Border(themeRoot, 14, White(0.4f));

            var name = Text(face.transform, "Name", DisplayFont, 14, Color.white, TextAlignmentOptions.Center, "shape_classic");
            name.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0, 51), new Vector2(104, 16), new Vector2(0.5f, 0.5f));

            var pill = Rounded(face.transform, "Pill", 12, Color.white);
            pill.rectTransform.anchorMin = new Vector2(0f, 0f);
            pill.rectTransform.anchorMax = new Vector2(1f, 0f);
            pill.rectTransform.pivot = new Vector2(0.5f, 0f);
            pill.rectTransform.offsetMin = new Vector2(M(6), M(8));
            pill.rectTransform.offsetMax = new Vector2(-M(6), M(38));
            var pillGradient = Gradient(pill, Hex("#FFD957"), Hex("#FF7A00"));
            var coin = Coin(pill.transform, "Coin", new Vector2(0f, 0.5f), new Vector2(10 + 8, 0), 16);
            var lockIcon = Image(pill.transform, "Lock", "icon_lock", Color.white);
            lockIcon.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(10, 0), new Vector2(14, 14), new Vector2(0f, 0.5f));
            var pillLabel = Text(pill.transform, "Label", DisplayFont, 15, Color.white);
            pillLabel.rectTransform.Fill(14, 0, 4, 0);
            pillLabel.text = "0";

            var badge = Pill(root, "New", 20.6f, 8, 8, 1.5f, 10, Color.white, out var badgeLabel, "shop_new");
            badge.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, 8), new Vector2(74, 20.6f), new Vector2(0.5f, 1f));
            Gradient(badge, Hex("#FF8BCB"), Hex("#D81B60"));
            badgeLabel.characterSpacing = Spacing(0.5f, 10);

            var button = Button(face.gameObject, root);
            Set(card, "button", button);
            Set(card, "border", border);
            Set(card, "selectedGlow", glow.gameObject);
            Set(card, "bottleRoot", bottleRoot.gameObject);
            Set(card, "bottle", bottle);
            Set(card, "themeRoot", themeRoot.gameObject);
            Set(card, "themeThumb", thumb);
            Set(card, "themeBottle", themeBottle);
            Set(card, "nameLabel", name.GetComponent<LocalizedText>());
            Set(card, "pill", pillGradient);
            Set(card, "pillCoin", coin.gameObject);
            Set(card, "pillLock", lockIcon.gameObject);
            Set(card, "pillLabel", pillLabel);
            Set(card, "newBadge", badge.gameObject);
            root.gameObject.SetActive(false);
            return card;
        }

        static UiScreen BuildSettings(Transform safeArea)
        {
            var screen = ScreenRoot<SettingsPopup>(safeArea, "SettingsPopup", out var content);
            var dimmer = Dimmer(content);
            dimmer.transform.SetAsFirstSibling();

            var panel = BluePanel(content, "Panel", 28);
            panel.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(330, 420));
            var title = Text(panel.transform, "Title", DisplayFont, 28, Hex("#FFD957"), TextAlignmentOptions.Center, "settings_title");
            title.rectTransform.TopBand(22, 36);
            Shadow(title, Hex("#A84A00"), -0.8f);

            var close = Node("Close", panel.transform);
            close.Place(new Vector2(1f, 1f), new Vector2(-10, -10), new Vector2(44, 44));
            var closeIcon = Image(close, "Icon", "icon_close", White(0.85f));
            closeIcon.rectTransform.sizeDelta = M(22, 22);
            var closeButton = Button(close.gameObject);

            var music = SettingRow(panel.transform, 0, "music", "settings_music");
            var sounds = SettingRow(panel.transform, 1, "sound", "settings_sounds");
            var vibration = SettingRow(panel.transform, 2, "vibration", "settings_vibration");
            var notifications = SettingRow(panel.transform, 3, "bell", "settings_notifications");

            float languageTop = 76 + 4 * 62 + 6;
            var languageIcon = IconTile(panel.transform, "globe", languageTop);
            var languageLabel = Text(panel.transform, "LanguageLabel", BodyFont, 16, Color.white, TextAlignmentOptions.MidlineLeft, "settings_language");
            languageLabel.rectTransform.Place(new Vector2(0f, 1f), new Vector2(76, -languageTop - 22), new Vector2(100, 24), new Vector2(0f, 0.5f));
            var segment = Rounded(panel.transform, "Language", 14, Hex("#081440", 0.8f));
            segment.rectTransform.Place(new Vector2(1f, 1f), new Vector2(-20, -languageTop), new Vector2(150, 44), new Vector2(1f, 1f));
            Border(segment.transform, 14, Hex("#96C8FF", 0.25f));
            var russian = Tab(segment.transform, "Ru", 0f, "lang_ru", out var russianBackground);
            var english = Tab(segment.transform, "En", 0.5f, "lang_en", out var englishBackground);
            foreach (var label in segment.GetComponentsInChildren<TMP_Text>())
                label.fontSize = M(13);

            Set(screen, "closeButton", closeButton);
            Set(screen, "dimmer", dimmer);
            Set(screen, "music", music);
            Set(screen, "sounds", sounds);
            Set(screen, "vibration", vibration);
            Set(screen, "notifications", notifications);
            Set(screen, "russian", russian);
            Set(screen, "english", english);
            Set(screen, "russianBackground", russianBackground);
            Set(screen, "englishBackground", englishBackground);
            return screen;
        }

        static Image IconTile(Transform parent, string icon, float top)
        {
            var tile = Rounded(parent, "Icon " + icon, 14, Color.white);
            tile.rectTransform.Place(new Vector2(0f, 1f), new Vector2(20, -top), new Vector2(44, 44), new Vector2(0f, 1f));
            Gradient(tile, Hex("#35C7FF"), Hex("#0548A8"));
            var glyph = Image(tile.transform, "Glyph", "icon_" + icon, Color.white);
            glyph.rectTransform.sizeDelta = M(24, 24);
            return tile;
        }

        static ToggleSwitch SettingRow(Transform panel, int index, string icon, string key)
        {
            float top = 76 + index * 62;
            IconTile(panel, icon, top);
            var label = Text(panel, "Label " + key, BodyFont, 16, Color.white, TextAlignmentOptions.MidlineLeft, key);
            label.rectTransform.Place(new Vector2(0f, 1f), new Vector2(76, -top - 22), new Vector2(160, 24), new Vector2(0f, 0.5f));

            var root = Node("Toggle " + key, panel);
            root.Place(new Vector2(1f, 1f), new Vector2(-20, -top - 5), new Vector2(64, 34), new Vector2(1f, 1f));
            var track = Rounded(root, "Track", 17, Color.white, true);
            track.rectTransform.Fill();
            var trackGradient = Gradient(track, Hex("#9BFF4F"), Hex("#17A523"));
            Border(track.transform, 17, White(0.4f));
            var knob = Image(root, "Knob", "circle", Color.white);
            knob.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(15, 0), new Vector2(28, 28));
            var button = Button(track.gameObject, root);
            var toggle = root.gameObject.AddComponent<ToggleSwitch>();
            Set(toggle, "button", button);
            Set(toggle, "track", trackGradient);
            Set(toggle, "knob", knob.rectTransform);
            Set(toggle, "travel", M(15));
            return toggle;
        }
    }
}
