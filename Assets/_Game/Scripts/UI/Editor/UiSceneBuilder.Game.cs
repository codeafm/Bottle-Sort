using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static BottleSort.UI.Editor.UiKit;

namespace BottleSort.UI.Editor
{
    public static partial class UiSceneBuilder
    {
        // Вертикальная сетка игрового экрана в пикселях макета от верха safe area.
        const float HeaderTop = 10f;
        const float HeaderHeight = 74f;
        const float ProgressTop = HeaderTop + HeaderHeight + 6f;
        const float ProgressHeight = 54f;
        const float InfoTop = ProgressTop + ProgressHeight + 8f;
        const float InfoHeight = 28f;
        const float FieldTop = InfoTop + InfoHeight;
        // Снизу: отступ 18, место под баннер 50 + рамка 3, зазор 14, бустеры, зазор 8.
        const float BoostersBottom = 18f + 53f + 14f;
        // Кнопка 62 + рамка 4, зазор 6, подпись 13 px с line-height 1.36.
        const float BoostersHeight = 66f + 6f + 17.7f;
        const float FieldBottom = BoostersBottom + BoostersHeight + 8f;

        static UiScreen BuildGame(Transform safeArea)
        {
            var screen = ScreenRoot<GameScreen>(safeArea, "GameScreen", out var content);
            var hud = content.gameObject.AddComponent<GameHud>();

            // Шапка: сетка 112 | центр | 112, все блоки фиксированного размера.
            var home = IconButton(content, "Home", "home", true);
            ((RectTransform)home.transform.parent).Place(new Vector2(0f, 1f), new Vector2(16, -(HeaderTop + 4)),
                new Vector2(IconButtonSize, IconButtonSize));
            IconButtonShadow(home);

            // Плашка 150×58 плюс рамка 1.5 снаружи.
            var plate = Rounded(content, "LevelPlate", 20, Color.white);
            plate.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, -HeaderTop), new Vector2(153, 61), new Vector2(0.5f, 1f));
            var plateShadow = SoftShadow(plate.rectTransform, 8, 18, 0.35f);
            Gradient(plate, White(0.24f), White(0.08f));
            Border(plate.transform, 20, Hex("#E6F0FF", 0.55f));
            var levelCaption = Text(plate.transform, "Caption", HeavyFont, 11, Hex("#EBF3FF", 0.85f), TextAlignmentOptions.Center, "hud_level");
            levelCaption.characterSpacing = Spacing(2.5f, 11);
            levelCaption.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, -8.5f), new Vector2(150, 14), new Vector2(0.5f, 1f));
            var levelNumber = Text(plate.transform, "Number", DisplayFont, 28, Color.white);
            levelNumber.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0, 7), new Vector2(150, 32), new Vector2(0.5f, 0f));
            levelNumber.text = "1";
            Shadow(levelNumber, new Color(0f, 0f, 0f, 0.3f), -0.7f);

            var tag = Rounded(plate.transform, "Tag", 10, Color.white);
            tag.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0, -10.5f), new Vector2(110, 20), new Vector2(0.5f, 0f));
            var tagGradient = Gradient(tag, Hex("#7BE84A"), Hex("#1E9A2C"));
            Border(tag.transform, 10, White(0.9f)).gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var tagLayout = tag.gameObject.AddComponent<HorizontalLayoutGroup>();
            tagLayout.padding = new RectOffset((int)M(12), (int)M(12), 0, 0);
            tagLayout.childAlignment = TextAnchor.MiddleCenter;
            tagLayout.childControlWidth = tagLayout.childControlHeight = true;
            tag.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var tagLabel = Text(tag.transform, "Label", HeavyFont, 10.5f, Color.white, TextAlignmentOptions.Center, "tag_normal");
            tagLabel.characterSpacing = Spacing(0.6f, 10.5f);
            Shadow(tagLabel, new Color(0f, 0f, 0f, 0.3f), -0.5f);

            var coinsRoot = CoinPill(content, "Coins", 104, 44, true, out var coins);
            coinsRoot.Place(new Vector2(1f, 1f), new Vector2(-16, -(HeaderTop + 6)), new Vector2(104, 44));
            SoftShadow(coinsRoot, 6, 14, 0.3f);

            // В макете плашка 153 pt чуть шире своей колонки и вплотную к монетам; держим зазор 4 pt
            // от монет и от «домой», на узком экране плашка ужимается.
            var header = content.gameObject.AddComponent<HeaderLayout>();
            Set(header, "plate", plate.rectTransform);
            Set(header, "plateShadow", plateShadow.rectTransform);
            Set(header, "plateWidth", M(153));
            Set(header, "sideColumn", M(16 + 104));
            Set(header, "minGap", M(4));

            // Панель прогресса: собрано · ходы · звёзды.
            var progress = Rounded(content, "Progress", 18, Color.white);
            progress.rectTransform.TopBand(ProgressTop, ProgressHeight, 16, 16);
            Gradient(progress, White(0.16f), White(0.05f));
            Border(progress.transform, 18, Hex("#DCEBFF", 0.3f));

            var collected = Text(progress.transform, "Collected", HeavyFont, 12, Hex("#EBF3FF", 0.9f), TextAlignmentOptions.MidlineLeft, "hud_collected");
            collected.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            collected.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            collected.rectTransform.offsetMin = new Vector2(M(16), M(-0.5f));
            collected.rectTransform.offsetMax = new Vector2(-M(16 + 74 + 68 + 24), M(16));
            var bar = Rounded(progress.transform, "Bar", 5, Hex("#000000", 0.35f));
            bar.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            bar.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            bar.rectTransform.offsetMin = new Vector2(M(16), -M(15.7f));
            bar.rectTransform.offsetMax = new Vector2(-M(16 + 74 + 68 + 24), -M(6.7f));
            var fill = Rounded(bar.transform, "Fill", 5, Color.white);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(0f, 1f);
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            Gradient(fill, Hex("#9BFF4F"), Hex("#17A523"), true);

            var moves = Node("Moves", progress.transform);
            moves.anchorMin = new Vector2(1f, 0f);
            moves.anchorMax = new Vector2(1f, 1f);
            moves.pivot = new Vector2(1f, 0.5f);
            moves.anchoredPosition = new Vector2(-M(16 + 68 + 12), 0f);
            // Колонка высотой по содержимому (33 pt), разделители такой же высоты.
            moves.anchorMin = moves.anchorMax = new Vector2(1f, 0.5f);
            moves.sizeDelta = new Vector2(M(74), M(33));
            var movesGroup = Group(moves.gameObject, 1f, false);
            var dividerLeft = Image(moves, "Left", null, White(0.18f));
            dividerLeft.rectTransform.anchorMin = new Vector2(0f, 0f);
            dividerLeft.rectTransform.anchorMax = new Vector2(0f, 1f);
            dividerLeft.rectTransform.sizeDelta = new Vector2(M(1), 0f);
            var dividerRight = Image(moves, "Right", null, White(0.18f));
            dividerRight.rectTransform.anchorMin = new Vector2(1f, 0f);
            dividerRight.rectTransform.anchorMax = new Vector2(1f, 1f);
            dividerRight.rectTransform.sizeDelta = new Vector2(M(1), 0f);
            var movesCaption = Text(moves, "Caption", HeavyFont, 10, Hex("#EBF3FF", 0.75f), TextAlignmentOptions.Center, "hud_moves");
            movesCaption.characterSpacing = Spacing(1f, 10);
            movesCaption.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 11.5f), new Vector2(74, 14));
            var movesValue = Text(moves, "Value", DisplayFont, 20, Color.white);
            movesValue.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, -5.5f), new Vector2(74, 24));
            movesValue.text = "0";

            var stars = new List<Image>();
            for (int i = 0; i < 3; i++)
            {
                var star = Image(progress.transform, "Star " + i, "star_on", Color.white);
                star.rectTransform.Place(new Vector2(1f, 0.5f), new Vector2(-16 - (2 - i) * 22, 0), new Vector2(21, 21),
                    new Vector2(1f, 0.5f));
                stars.Add(star);
            }

            // Строка подсказки: всегда на месте, до двух строк текста.
            var infoBand = Node("Info", content).TopBand(InfoTop, InfoHeight, 16, 16);
            var infoPill = Rounded(infoBand, "Pill", 12, new Color(0f, 0f, 0f, 0.25f));
            infoPill.rectTransform.anchorMin = infoPill.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            infoPill.rectTransform.sizeDelta = M(300, 28);
            var info = Text(infoBand, "Text", BodyFont, 12, Hex("#DCEBFF"), TextAlignmentOptions.Center);
            info.rectTransform.Fill(14, 0, 14, 0);
            info.textWrappingMode = TextWrappingModes.Normal;
            info.overflowMode = TextOverflowModes.Overflow;
            info.lineSpacing = -8f;
            info.text = Loc.Get("info_normal");

            // Поле.
            var fieldRect = Node("Field", content);
            fieldRect.anchorMin = Vector2.zero;
            fieldRect.anchorMax = Vector2.one;
            fieldRect.offsetMin = new Vector2(M(16), M(FieldBottom));
            fieldRect.offsetMax = new Vector2(-M(16), -M(FieldTop));
            var field = fieldRect.gameObject.AddComponent<BottleField>();
            var board = Node("Board", fieldRect);
            board.anchorMin = board.anchorMax = new Vector2(0.5f, 0.5f);
            board.sizeDelta = new Vector2(358, 420);
            var slot = BuildSlotTemplate(fieldRect);
            // Полки поля рисует мировой ShelfView позади бутылок, здесь только место и ширина
            // (в пикселях макета внутри доски, которая уже масштабирована).
            var shelf = Node("ShelfTemplate", fieldRect);
            shelf.sizeDelta = new Vector2(200, 12);
            shelf.gameObject.AddComponent<ShelfViewAdapter>();
            shelf.anchorMin = shelf.anchorMax = new Vector2(0.5f, 0.5f);
            shelf.pivot = new Vector2(0.5f, 0f);
            shelf.gameObject.SetActive(false);
            // Для превью в редакторе — полка на uGUI той же формы, что в меню; в игре не используется.
            var previewShelf = Shelf(fieldRect, "ShelfPreviewTemplate", 200, 1f);
            previewShelf.anchorMin = previewShelf.anchorMax = new Vector2(0.5f, 0.5f);
            previewShelf.pivot = new Vector2(0.5f, 0f);
            previewShelf.gameObject.SetActive(false);

            var toast = BuildToast(fieldRect, 0);

            // Бустеры: четыре колонки, скрытая кнопка не сдвигает остальные.
            var boosterRow = Node("Boosters", content).BottomBand(BoostersBottom, BoostersHeight, 16, 16);
            var boosters = new List<BoosterButton>
            {
                BuildBooster(boosterRow, 0, Booster.Undo, "undo", "booster_undo"),
                BuildBooster(boosterRow, 1, Booster.Restart, "restart", "booster_restart"),
                BuildBooster(boosterRow, 2, Booster.Hint, "hint", "booster_hint"),
                BuildBooster(boosterRow, 3, Booster.ExtraBottle, "add_bottle", "booster_bottle")
            };

            // Рука-подсказка поверх поля, касания не перехватывает.
            var handLayer = Node("HandLayer", content).Fill();
            var director = handLayer.gameObject.AddComponent<TutorialDirector>();
            var hand = BuildHand(handLayer);
            Set(director, "hand", hand);

            Set(hud, "homeButton", home);
            Set(hud, "levelNumber", levelNumber);
            Set(hud, "tagLabel", tagLabel.GetComponent<LocalizedText>());
            Set(hud, "tagBackground", tagGradient);
            Set(hud, "coins", coins);
            Set(hud, "collected", collected.GetComponent<LocalizedText>());
            Set(hud, "progressFill", fill.rectTransform);
            Set(hud, "movesGroup", movesGroup);
            Set(hud, "moves", movesValue);
            Set(hud, "stars", stars);
            Set(hud, "starOn", Sprite("star_on"));
            Set(hud, "starOff", Sprite("star_off"));
            Set(hud, "info", info);
            Set(hud, "infoPill", infoPill.rectTransform);

            Set(field, "board", board);
            Set(field, "slotTemplate", slot);
            Set(field, "shelfTemplate", shelf);
            Set(field, "bottleTemplate", _fieldBottlePrefab);
            var preview = fieldRect.gameObject.AddComponent<FieldPreview>();
            Set(preview, "bottle", _bottlePrefab);
            Set(preview, "shelf", previewShelf);

            Set(screen, "hud", hud);
            Set(screen, "field", field);
            Set(screen, "boosters", boosters);
            Set(screen, "tutorial", director);
            Set(screen, "toast", toast);
            return screen;
        }

        static BottleSlot BuildSlotTemplate(Transform parent)
        {
            var root = Node("SlotTemplate", parent);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0f);
            root.sizeDelta = new Vector2(72, 172);
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = Color.clear;
            var slot = root.gameObject.AddComponent<BottleSlot>();

            var visual = Node("Visual", root);
            visual.anchorMin = Vector2.zero;
            visual.anchorMax = Vector2.one;
            visual.offsetMin = visual.offsetMax = Vector2.zero;
            var holder = Node("View", visual);
            holder.anchorMin = Vector2.zero;
            holder.anchorMax = Vector2.one;
            holder.offsetMin = holder.offsetMax = Vector2.zero;

            // Всё ниже — в пикселях макета: доска с ячейками масштабируется целиком.
            var badge = Image(visual, "Done", "circle", Color.white);
            badge.rectTransform.anchorMin = badge.rectTransform.anchorMax = new Vector2(1f, 1f);
            badge.rectTransform.pivot = new Vector2(1f, 1f);
            badge.rectTransform.anchoredPosition = new Vector2(4f, 8f);
            badge.rectTransform.sizeDelta = new Vector2(24f, 24f);
            var badgeFill = Image(badge.transform, "Fill", "circle", Color.white);
            badgeFill.rectTransform.anchorMin = Vector2.zero;
            badgeFill.rectTransform.anchorMax = Vector2.one;
            badgeFill.rectTransform.offsetMin = new Vector2(2f, 2f);
            badgeFill.rectTransform.offsetMax = new Vector2(-2f, -2f);
            Gradient(badgeFill, Hex("#9BFF4F"), Hex("#17A523"));
            var check = Image(badge.transform, "Check", "icon_check", Color.white);
            check.rectTransform.sizeDelta = new Vector2(13f, 13f);

            // Точка руки из макета: left 50% − 6 px, bottom 38%.
            var anchor = Node("HandAnchor", visual);
            anchor.anchorMin = anchor.anchorMax = new Vector2(0.5f, 0.38f);
            anchor.anchoredPosition = new Vector2(-6f, 0f);
            anchor.sizeDelta = Vector2.zero;

            Set(slot, "visual", visual);
            Set(slot, "viewHolder", holder);
            Set(slot, "doneBadge", badge.gameObject);
            Set(slot, "handAnchor", anchor);
            root.gameObject.SetActive(false);
            return slot;
        }

        // Бустеры: сетка в 4 колонки с зазором 8 pt по ширине поля (358 pt), как grid в Game.dc.html.
        static BoosterButton BuildBooster(Transform row, int column, Booster kind, string icon, string key)
        {
            const float columnWidth = (358f - 3 * 8f) / 4f;
            var root = Node(kind.ToString(), row);
            root.Place(new Vector2(0.5f, 1f), new Vector2(column * (columnWidth + 8f) + columnWidth / 2f - 179f, 0f),
                new Vector2(columnWidth, BoostersHeight), new Vector2(0.5f, 1f));
            var group = Group(root.gameObject);
            var button = Button(root.gameObject);
            var booster = root.gameObject.AddComponent<BoosterButton>();

            // 62 pt плюс рамка 2 pt снаружи.
            var faceRoot = Node("FaceRoot", root);
            faceRoot.Place(new Vector2(0.5f, 1f), Vector2.zero, new Vector2(66, 66), new Vector2(0.5f, 1f));
            SoftShadow(faceRoot, 8, 16, 0.4f);
            var face = Rounded(faceRoot, "Face", 22, Color.white);
            face.rectTransform.Fill();
            Gradient(face, Hex("#35C7FF"), Hex("#0548A8")).SetColors(Hex("#35C7FF"), Hex("#0870E8"), 0.6f, Hex("#0548A8"));
            InsetBottom(face.transform, 22, Hex("#001E5A", 0.4f));
            Border(face.transform, 22, White(0.5f));
            var glyph = Image(face.transform, "Icon", "icon_" + icon, Color.white);
            glyph.rectTransform.sizeDelta = M(28, 28) * (27f / 24f);

            var badge = Pill(face.transform, "Badge", 24, 5, 12, 2, 12, Hex("#3A1A00"), out var badgeLabel);
            badge.rectTransform.Place(new Vector2(1f, 1f), new Vector2(5, 5), new Vector2(24, 24), new Vector2(1f, 1f));
            var badgeGradient = Gradient(badge, Hex("#FFC107"), Hex("#FF8A00"));
            badgeLabel.text = "3";

            var label = Text(root, "Label", BodyFont, 13, Color.white, TextAlignmentOptions.Center, key);
            label.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, -72), new Vector2(100, 18), new Vector2(0.5f, 1f));

            Set(booster, "booster", kind);
            Set(booster, "button", button);
            Set(booster, "badge", badge.gameObject);
            Set(booster, "badgeLabel", badgeLabel);
            Set(booster, "badgeBackground", badgeGradient);
            Set(booster, "group", group);
            return booster;
        }

        // Бейдж-«таблетка» с белой рамкой: ширина по тексту, но не меньше высоты (min-width из макета).
        static Image Pill(Transform parent, string name, float height, float paddingX, float radius, float border,
            float fontSize, Color ink, out TextMeshProUGUI label, string key = null)
        {
            var pill = Rounded(parent, name, radius, Color.white);
            Border(pill.transform, radius, Color.white);
            label = Text(pill.transform, "Label", HeavyFont, fontSize, ink, TextAlignmentOptions.Center, key);
            label.rectTransform.Fill();
            var fit = pill.gameObject.AddComponent<TextPill>();
            Set(fit, "label", label);
            Set(fit, "padding", M(paddingX + border));
            Set(fit, "minWidth", M(height));
            return pill;
        }

        static TutorialHand BuildHand(Transform layer)
        {
            var root = Node("Hand", layer);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = Vector2.zero;
            root.sizeDelta = M(46, 46);
            var group = Group(root.gameObject, 0f, false);
            var hand = root.gameObject.AddComponent<TutorialHand>();

            var ring = Image(root, "Ring", "ring", White(0.85f));
            ring.rectTransform.anchorMin = ring.rectTransform.anchorMax = Vector2.zero;
            ring.rectTransform.anchoredPosition = M(4, 42);
            ring.rectTransform.sizeDelta = M(28, 28);
            var ringGroup = Group(ring.gameObject, 0f, false);

            // Жест качает руку вокруг центра коробки 46x46, как transform-origin в CSS.
            var gesture = Node("Gesture", root);
            gesture.anchorMin = gesture.anchorMax = new Vector2(0.5f, 0.5f);
            gesture.sizeDelta = M(46, 46);
            // Спрайт руки запечён с полями (viewBox от -3 до 27 вместо 0..24), поэтому он чуть больше коробки.
            var image = Image(gesture, "Image", "hand", Color.white);
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = Vector2.zero;
            image.rectTransform.pivot = Vector2.zero;
            image.rectTransform.anchoredPosition = M(-5.75f, -5.75f);
            image.rectTransform.sizeDelta = M(57.5f, 57.5f);

            Set(hand, "hand", gesture);
            Set(hand, "ring", ring.rectTransform);
            Set(hand, "ringGroup", ringGroup);
            Set(hand, "group", group);
            return hand;
        }

        static UiScreen BuildWin(Transform safeArea)
        {
            var screen = ScreenRoot<WinScreen>(safeArea, "WinScreen", out var content);

            var rays = Image(content, "Rays", "rays", Color.white);
            rays.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, -256), new Vector2(760, 760), new Vector2(0.5f, 0.5f));
            Motion(rays.gameObject, IdleMotion.Kind.Spin, 24f, 15f);
            var glow = Image(content, "Glow", "glow", Hex("#FFD66E", 0.45f));
            glow.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, -256), new Vector2(420, 420), new Vector2(0.5f, 0.5f));

            var confetti = Node("Confetti", content);
            confetti.anchorMin = new Vector2(0f, 1f);
            confetti.anchorMax = new Vector2(1f, 1f);
            confetti.pivot = new Vector2(0.5f, 1f);
            confetti.sizeDelta = Vector2.zero;
            string[] colors = { "#C9283A", "#2D5FC4", "#E3B21E", "#2A9E4A", "#6E3BB0", "#1495AA", "#D9772A", "#C73278", "#85B524" };
            var random = new System.Random(11);
            for (int i = 0; i < 34; i++)
            {
                var piece = Rounded(confetti, "Piece " + i, 2, Hex(colors[i % colors.Length]));
                piece.rectTransform.anchorMin = piece.rectTransform.anchorMax = new Vector2((float)random.NextDouble(), 1f);
                piece.rectTransform.sizeDelta = M(6 + random.Next(7), 10 + random.Next(9));
                piece.rectTransform.localRotation = Quaternion.Euler(0f, 0f, random.Next(360));
            }

            // Отступы ниже — из Win.dc.html, в pt от верха экрана минус 44 pt выреза (safe area).
            var ribbonRoot = Node("Ribbon", content);
            ribbonRoot.Place(new Vector2(0.5f, 1f), new Vector2(0, -52), new Vector2(330, 76), new Vector2(0.5f, 1f));
            var tailLeft = Image(ribbonRoot, "TailLeft", "ribbon_tail", Hex("#B8105A"));
            tailLeft.rectTransform.Place(new Vector2(0f, 1f), new Vector2(0, -18), new Vector2(50, 50), new Vector2(0f, 1f));
            var tailRight = Image(ribbonRoot, "TailRight", "ribbon_tail", Hex("#B8105A"));
            tailRight.rectTransform.Place(new Vector2(1f, 1f), new Vector2(0, -18), new Vector2(50, 50), new Vector2(0f, 1f));
            tailRight.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
            var ribbon = Rounded(ribbonRoot, "Body", 14, Color.white);
            ribbon.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(276, 70));
            SoftShadow(ribbon.rectTransform, 8, 20, 0.45f);
            Gradient(ribbon, Color.white, Color.white).SetColors(Hex("#FF8BCB"), Hex("#FF3D8D"), 0.5f, Hex("#D81B60"));
            Border(ribbon.transform, 14, Hex("#FFE1F0"));
            var ribbonText = Text(ribbon.transform, "Label", DisplayFont, 36, Color.white, TextAlignmentOptions.Center, "win_title");
            ribbonText.characterSpacing = Spacing(2f, 36);
            ribbonText.rectTransform.Fill();
            Shadow(ribbonText, Hex("#8A0A3E"), -0.9f);

            var starsRow = Node("Stars", content);
            starsRow.Place(new Vector2(0.5f, 1f), new Vector2(0, -162), new Vector2(272, 126), new Vector2(0.5f, 1f));
            var stars = new List<Image>();
            var starSpecs = new[] { (-97f, 78f, 0f), (0f, 104f, 22f), (97f, 78f, 0f) };
            for (int i = 0; i < 3; i++)
            {
                var (x, size, lift) = starSpecs[i];
                // Спрайт со свечением: звезда занимает 24 из 32 единиц.
                var star = Image(starsRow, "Star " + i, "star_big", Color.white);
                star.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(x, lift + size / 2f), new Vector2(size, size) * (32f / 24f),
                    new Vector2(0.5f, 0.5f));
                stars.Add(star);
            }

            var levelDone = Text(content, "LevelDone", DisplayFont, 26, Color.white, TextAlignmentOptions.Center, "win_level_done");
            levelDone.rectTransform.TopBand(306, 32);
            Shadow(levelDone, new Color(0f, 0f, 0f, 0.5f), -0.7f);

            var chips = Node("Chips", content).TopBand(347, 34);
            var chipsLayout = chips.gameObject.AddComponent<HorizontalLayoutGroup>();
            chipsLayout.childAlignment = TextAnchor.MiddleCenter;
            chipsLayout.spacing = M(10);
            chipsLayout.childControlWidth = chipsLayout.childControlHeight = true;
            chipsLayout.childForceExpandWidth = false;
            var movesChip = Chip(chips, "Moves", "win_moves");
            var bestChip = Chip(chips, "Best", "win_best");

            var rewardPanel = Rounded(content, "Reward", 24, Color.white);
            rewardPanel.rectTransform.TopBand(407, 108, 24, 24);
            Gradient(rewardPanel, Hex("#123C87", 0.92f), Hex("#061B42", 0.92f));
            Border(rewardPanel.transform, 24, Hex("#78C8FF", 0.5f));
            var rewardCaption = Text(rewardPanel.transform, "Caption", HeavyFont, 13, Hex("#9FDDFF"), TextAlignmentOptions.MidlineLeft, "win_reward");
            rewardCaption.characterSpacing = Spacing(1f, 13);
            rewardCaption.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(18, 25), new Vector2(160, 18), new Vector2(0f, 0.5f));
            var coinGlow = Image(rewardPanel.transform, "CoinGlow", "glow", Hex("#FFC83C", 0.7f));
            coinGlow.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(18 - 10, -10), new Vector2(58, 58), new Vector2(0f, 0.5f));
            Coin(rewardPanel.transform, "Coin", new Vector2(0f, 0.5f), new Vector2(18 + 19, -10), 38);
            var reward = Text(rewardPanel.transform, "Amount", DisplayFont, 40, Hex("#FFD957"), TextAlignmentOptions.MidlineLeft);
            reward.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(66, -10), new Vector2(140, 48), new Vector2(0f, 0.5f));
            reward.text = "+25";
            Shadow(reward, Hex("#A84A00"), -0.8f);
            var doubleButton = ColorButton(rewardPanel.transform, "Double", Hex("#E474FF"), Hex("#7E25D4"), 20, Hex("#4A0E8A"), 5,
                out var doubleFace);
            ((RectTransform)doubleButton.transform).Place(new Vector2(1f, 0.5f), new Vector2(-18, 2.5f), new Vector2(151, 64),
                new Vector2(1f, 0.5f));
            var doubleLabel = IconLabel(doubleFace, "icon_video", 22, "win_double", 20, 8, out _);

            var next = ColorButton(content, "Next", Hex("#B6FF6E"), Hex("#17A523"), 26, Hex("#0E6B17"), 7, out var nextFace);
            ((RectTransform)next.transform).BottomBand(84, 81, 24, 24);
            nextFace.GetComponent<UiGradient>().SetColors(Hex("#B6FF6E"), Hex("#5BDB33"), 0.45f, Hex("#17A523"));
            SoftShadow((RectTransform)next.transform, 16 + 7, 28, 0.45f);
            var nextLabel = IconLabel(nextFace, "icon_arrow_right", 24, "win_next", 26, 10, out _, iconFirst: false);
            Shadow(nextLabel, Hex("#0E6B17"), -0.8f);

            var menuRoot = Node("Menu", content);
            menuRoot.Place(new Vector2(0.5f, 0f), new Vector2(0, 30), new Vector2(220, 38), new Vector2(0.5f, 0f));
            var menuLabel = Text(menuRoot, "Label", HeavyFont, 16, Hex("#CFE6FF"), TextAlignmentOptions.Center, "win_menu");
            menuLabel.rectTransform.Fill();
            var menu = Button(menuRoot.gameObject);

            Set(screen, "stars", stars);
            Set(screen, "levelDone", levelDone.GetComponent<LocalizedText>());
            Set(screen, "movesChip", movesChip.gameObject);
            Set(screen, "movesLabel", movesChip.GetComponentInChildren<LocalizedText>());
            Set(screen, "bestChip", bestChip.gameObject);
            Set(screen, "reward", reward);
            Set(screen, "doubleButton", doubleButton);
            Set(screen, "doubleLabel", doubleLabel.GetComponent<LocalizedText>());
            Set(screen, "nextButton", next);
            Set(screen, "menuButton", menu);
            Set(screen, "confetti", confetti);
            return screen;
        }

        static RectTransform Chip(Transform parent, string name, string key)
        {
            var chip = Rounded(parent, name, 14, Hex("#0A1A50", 0.7f));
            Border(chip.transform, 14, Hex("#96C8FF", 0.35f)).gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var layout = chip.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset((int)M(15.5f), (int)M(15.5f), (int)M(7.5f), (int)M(7.5f));
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var label = Text(chip.transform, "Label", HeavyFont, 14, Color.white, TextAlignmentOptions.Center, key);
            return chip.rectTransform;
        }

        static UiScreen BuildGift(Transform safeArea)
        {
            var screen = ScreenRoot<GiftPopup>(safeArea, "GiftPopup", out var content);
            Dimmer(content).transform.SetAsFirstSibling();

            var panel = BluePanel(content, "Panel", 28);
            panel.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320, 330));
            var title = Text(panel.transform, "Title", DisplayFont, 28, Hex("#FFD957"), TextAlignmentOptions.Center, "gift_title");
            title.rectTransform.TopBand(26, 36);
            Shadow(title, Hex("#A84A00"), -0.8f);
            var subtitle = Text(panel.transform, "Text", HeavyFont, 15, Hex("#CFE6FF"), TextAlignmentOptions.Center, "gift_text");
            subtitle.rectTransform.TopBand(66, 22);

            string[] icons = { "coin", "icon_undo", "icon_hint", "icon_add_bottle" };
            var counts = new List<TMP_Text>();
            for (int i = 0; i < icons.Length; i++)
            {
                bool isCoin = icons[i] == "coin";
                var tile = Rounded(panel.transform, "Item " + i, 18, isCoin ? Color.clear : Color.white);
                tile.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2((i - 1.5f) * 70, -104), new Vector2(60, 60), new Vector2(0.5f, 1f));
                if (!isCoin)
                    Gradient(tile, Hex("#35C7FF"), Hex("#0548A8"));
                if (isCoin)
                    Coin(tile.transform, "Icon", new Vector2(0.5f, 0.5f), Vector2.zero, 48);
                else
                    Image(tile.transform, "Icon", icons[i], Color.white).rectTransform.sizeDelta = M(28, 28);
                var count = Text(panel.transform, "Count " + i, DisplayFont, 18, Color.white);
                count.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2((i - 1.5f) * 70, -170), new Vector2(70, 24), new Vector2(0.5f, 1f));
                counts.Add(count);
            }

            var claim = ColorButton(panel.transform, "Claim", Hex("#B6FF6E"), Hex("#17A523"), 20, Hex("#0E6B17"), 5, out var claimFace);
            ((RectTransform)claim.transform).BottomBand(28, 58, 22, 22);
            var claimLabel = Text(claimFace, "Label", DisplayFont, 22, Color.white, TextAlignmentOptions.Center, "gift_claim");
            claimLabel.rectTransform.Fill();
            Shadow(claimLabel, Hex("#0E6B17"), -0.8f);

            Set(screen, "claimButton", claim);
            Set(screen, "counts", counts);
            return screen;
        }
    }
}
