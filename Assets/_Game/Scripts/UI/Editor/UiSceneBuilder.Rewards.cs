using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static BottleSort.UI.Editor.UiKit;

namespace BottleSort.UI.Editor
{
    // Попапы наград из меню (подарок дня, бонус недели, колесо удачи) и сундуков после победы.
    // Макетов для них нет, поэтому они в стиле GiftPopup: синяя панель, жёлтый заголовок, зелёная кнопка.
    public static partial class UiSceneBuilder
    {
        static readonly string[] RewardIcons = { "coin", "icon_undo", "icon_hint", "icon_add_bottle", "icon_wheel" };

        static UiScreen BuildRewardPopup(Transform safeArea)
        {
            var screen = ScreenRoot<RewardPopup>(safeArea, "RewardPopup", out var content);
            var dimmer = Dimmer(content);
            dimmer.transform.SetAsFirstSibling();
            Set(screen, "dimmer", dimmer);

            var panel = BluePanel(content, "Panel", 28);
            panel.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320, 330));
            var title = PopupTitle(panel.transform, "daily_gift_title");
            var subtitle = Text(panel.transform, "Text", HeavyFont, 15, Hex("#CFE6FF"), TextAlignmentOptions.Center, "daily_gift_text");
            subtitle.rectTransform.TopBand(66, 22);

            var items = new List<RectTransform>();
            var counts = new List<TMP_Text>();
            for (int i = 0; i < RewardIcons.Length; i++)
            {
                items.Add(RewardTile(panel.transform, "Item " + i, RewardIcons[i], new Vector2((i - 2f) * 70, -104)));
                var count = Text(panel.transform, "Count " + i, DisplayFont, 18, Color.white);
                count.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2((i - 2f) * 70, -170), new Vector2(70, 24), new Vector2(0.5f, 1f));
                counts.Add(count);
            }

            var claim = GreenButton(panel.transform, "Claim", "gift_claim", out var claimLabel);
            ((RectTransform)claim.transform).BottomBand(28, 58, 22, 22);

            Set(screen, "title", title.GetComponent<LocalizedText>());
            Set(screen, "subtitle", subtitle.GetComponent<LocalizedText>());
            Set(screen, "claimButton", claim);
            Set(screen, "claimLabel", claimLabel.GetComponent<LocalizedText>());
            Set(screen, "items", items);
            Set(screen, "counts", counts);
            Set(screen, "itemSpacing", M(70));
            AddAdChoice(screen);
            AddCardHighlight(screen);
            return screen;
        }

        static UiScreen BuildCalendar(Transform safeArea)
        {
            var screen = ScreenRoot<CalendarPopup>(safeArea, "CalendarPopup", out var content);
            var dimmer = Dimmer(content);
            dimmer.transform.SetAsFirstSibling();

            var panel = BluePanel(content, "Panel", 28);
            panel.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(340, 420));
            PopupTitle(panel.transform, "calendar_title");
            var subtitle = Text(panel.transform, "Text", HeavyFont, 15, Hex("#CFE6FF"), TextAlignmentOptions.Center, "calendar_text");
            subtitle.rectTransform.TopBand(66, 22);
            var close = CloseButton(panel.transform);

            var amounts = new List<TMP_Text>();
            var tiles = new List<UiGradient>();
            var checks = new List<GameObject>();
            var glows = new List<GameObject>();
            var dayLabels = new List<LocalizedText>();
            for (int i = 0; i < 7; i++)
            {
                // Четыре дня сверху, три снизу, нижний ряд по центру.
                bool topRow = i < 4;
                float x = topRow ? (i - 1.5f) * 76 : (i - 4 - 1f) * 76;
                float y = topRow ? -104 : -208;

                var glow = Image(panel.transform, "Glow " + i, "glow", Hex("#FFD957", 0.7f));
                glow.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(x, y - 46), new Vector2(110, 130));
                Motion(glow.gameObject, IdleMotion.Kind.Pulse, 1.6f, 0.08f);
                glows.Add(glow.gameObject);

                var tile = Rounded(panel.transform, "Day " + (i + 1), 16, Color.white);
                tile.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(x, y), new Vector2(68, 92), new Vector2(0.5f, 1f));
                tiles.Add(Gradient(tile, Hex("#1A4A9A"), Hex("#0A2560")));
                Border(tile.transform, 16, Hex("#96C8FF", 0.35f));

                var dayLabel = Text(tile.transform, "Label", HeavyFont, 12, Color.white, TextAlignmentOptions.Center, "calendar_day");
                dayLabel.rectTransform.TopBand(6, 18);
                dayLabels.Add(dayLabel.GetComponent<LocalizedText>());

                Coin(tile.transform, "Coin", new Vector2(0.5f, 0.5f), new Vector2(0, 2), 34);
                var amount = Text(tile.transform, "Amount", DisplayFont, 16, Color.white);
                amount.rectTransform.BottomBand(6, 22);
                Shadow(amount, Hex("#061B42"), -0.6f);
                amounts.Add(amount);

                var check = Image(tile.transform, "Check", "icon_check", Color.white);
                check.rectTransform.Place(new Vector2(1f, 1f), new Vector2(4, 4), new Vector2(26, 26), new Vector2(1f, 1f));
                Gradient(check, Hex("#B6FF6E"), Hex("#17A523"));
                checks.Add(check.gameObject);
            }

            var claim = GreenButton(panel.transform, "Claim", "gift_claim", out var claimLabel);
            ((RectTransform)claim.transform).BottomBand(28, 58, 22, 22);

            Set(screen, "closeButton", close);
            Set(screen, "dimmer", dimmer);
            Set(screen, "claimButton", claim);
            Set(screen, "claimLabel", claimLabel.GetComponent<LocalizedText>());
            Set(screen, "claimGroup", Group(claim.gameObject));
            Set(screen, "amounts", amounts);
            Set(screen, "tiles", tiles);
            Set(screen, "checks", checks);
            Set(screen, "todayGlows", glows);
            Set(screen, "dayLabels", dayLabels);
            AddAdChoice(screen);
            return screen;
        }

        static TextMeshProUGUI PopupTitle(Transform panel, string key)
        {
            var title = Text(panel, "Title", DisplayFont, 28, Hex("#FFD957"), TextAlignmentOptions.Center, key);
            title.rectTransform.TopBand(26, 36);
            Shadow(title, Hex("#A84A00"), -0.8f);
            return title;
        }

        static Button CloseButton(Transform panel)
        {
            var close = Node("Close", panel);
            close.Place(new Vector2(1f, 1f), new Vector2(-10, -10), new Vector2(44, 44), new Vector2(1f, 1f));
            var icon = Image(close, "Icon", "icon_close", White(0.85f));
            icon.rectTransform.sizeDelta = M(22, 22);
            return Button(close.gameObject);
        }

        static Button GreenButton(Transform parent, string name, string key, out TextMeshProUGUI label)
        {
            var button = ColorButton(parent, name, Hex("#B6FF6E"), Hex("#17A523"), 20, Hex("#0E6B17"), 5, out var face);
            label = Text(face, "Label", DisplayFont, 22, Color.white, TextAlignmentOptions.Center, key);
            label.rectTransform.Fill();
            Shadow(label, Hex("#0E6B17"), -0.8f);
            return button;
        }

        static RectTransform RewardTile(Transform parent, string name, string icon, Vector2 position)
        {
            bool isCoin = icon == "coin";
            var tile = Rounded(parent, name, 18, isCoin ? Color.clear : Color.white);
            tile.rectTransform.Place(new Vector2(0.5f, 1f), position, new Vector2(60, 60), new Vector2(0.5f, 1f));
            if (!isCoin)
                Gradient(tile, Hex("#35C7FF"), Hex("#0548A8"));
            if (isCoin)
                Coin(tile.transform, "Icon", new Vector2(0.5f, 0.5f), Vector2.zero, 48);
            else
                Image(tile.transform, "Icon", icon, Color.white).rectTransform.sizeDelta = M(28, 28);
            return tile.rectTransform;
        }
    }
}
