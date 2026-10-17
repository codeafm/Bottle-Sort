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
            Dimmer(content).transform.SetAsFirstSibling();

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
            return screen;
        }

        static UiScreen BuildWheel(Transform safeArea)
        {
            var screen = ScreenRoot<WheelPopup>(safeArea, "WheelPopup", out var content);
            var dimmer = Dimmer(content);
            dimmer.transform.SetAsFirstSibling();

            var panel = BluePanel(content, "Panel", 28);
            panel.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(340, 480));
            PopupTitle(panel.transform, "wheel_title");
            var close = CloseButton(panel.transform);

            const float diameter = 250f;
            var rim = Image(panel.transform, "Rim", "circle", Hex("#FFD957"));
            rim.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, -72), new Vector2(diameter + 16, diameter + 16), new Vector2(0.5f, 1f));
            Gradient(rim, Hex("#FFE58A"), Hex("#FF8A00"));

            var wheel = Node("Wheel", rim.transform);
            wheel.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(diameter, diameter));

            string[] colors = { "#FFC94A", "#35C7FF", "#7BE85A", "#D46BFF", "#FF8A5C", "#4FD8E8" };
            string[] labels = { "50", "+1", "+1", "+1", "150", "+1" };
            string[] icons = { "coin", "icon_undo", "icon_hint", "icon_add_bottle", "coin", "icon_wheel" };
            for (int i = 0; i < 6; i++)
            {
                // Сектор i — по часовой стрелке от верха; WheelPopup поворачивает колесо на i·60°, чтобы привести его к стрелке.
                var sector = Image(wheel, "Sector " + i, "circle", Hex(colors[i]));
                sector.rectTransform.Fill();
                sector.type = UnityEngine.UI.Image.Type.Filled;
                sector.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
                sector.fillOrigin = (int)UnityEngine.UI.Image.Origin360.Top;
                sector.fillClockwise = true;
                sector.fillAmount = 1f / 6f;
                sector.rectTransform.localRotation = Quaternion.Euler(0, 0, 30 - i * 60);

                var spoke = Node("Prize " + i, wheel);
                spoke.Fill();
                spoke.localRotation = Quaternion.Euler(0, 0, -i * 60);
                if (icons[i] == "coin")
                    Coin(spoke, "Icon", new Vector2(0.5f, 0.5f), new Vector2(0, diameter * 0.33f), 36);
                else
                    Image(spoke, "Icon", icons[i], Color.white).rectTransform.Place(new Vector2(0.5f, 0.5f),
                        new Vector2(0, diameter * 0.33f), new Vector2(28, 28));
                var label = Text(spoke, "Label", DisplayFont, 17, Color.white);
                label.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, diameter * 0.19f), new Vector2(60, 24));
                label.text = labels[i];
                Shadow(label, Hex("#061B42", 0.8f), -0.8f);
            }

            var hub = Image(rim.transform, "Hub", "circle", Color.white);
            hub.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56, 56));
            Gradient(hub, Hex("#35C7FF"), Hex("#0548A8"));
            var hubIcon = Image(hub.transform, "Icon", "star_on", Color.white);
            hubIcon.rectTransform.sizeDelta = M(30, 30);

            // Стрелка — ромб, наполовину заходящий на обод сверху.
            var pointer = Rounded(rim.transform, "Pointer", 6, Color.white);
            pointer.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, 2), new Vector2(28, 28));
            pointer.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            Gradient(pointer, Hex("#FF6B8E"), Hex("#E0124A"));
            Border(pointer.transform, 6, White(0.85f));

            var spinsLeft = Text(panel.transform, "SpinsLeft", HeavyFont, 15, Hex("#CFE6FF"), TextAlignmentOptions.Center, "wheel_spins_left");
            spinsLeft.rectTransform.BottomBand(128, 22);
            var result = Text(panel.transform, "Result", DisplayFont, 22, Hex("#FFD957"));
            result.rectTransform.BottomBand(100, 28);
            Shadow(result, Hex("#A84A00"), -0.8f);
            result.text = "";

            var spin = GreenButton(panel.transform, "Spin", "wheel_spin", out var spinLabel);
            ((RectTransform)spin.transform).BottomBand(28, 58, 40, 40);
            spinLabel.rectTransform.Fill(30, 0, 0, 0);
            var adIcon = Image(spinLabel.transform.parent, "AdIcon", "icon_video", Color.white);
            adIcon.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(26, 0), new Vector2(28, 28), new Vector2(0f, 0.5f));

            Set(screen, "closeButton", close);
            Set(screen, "dimmer", dimmer);
            Set(screen, "wheel", wheel);
            Set(screen, "spinButton", spin);
            Set(screen, "spinLabel", spinLabel.GetComponent<LocalizedText>());
            Set(screen, "adIcon", adIcon.gameObject);
            Set(screen, "spinsLeft", spinsLeft.GetComponent<LocalizedText>());
            Set(screen, "result", result);
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
