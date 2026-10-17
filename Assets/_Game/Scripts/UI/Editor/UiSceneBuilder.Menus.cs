using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static BottleSort.UI.Editor.UiKit;

namespace BottleSort.UI.Editor
{
    public static partial class UiSceneBuilder
    {
        const string LogoPath = "Assets/_Game/Art/sort_bottle_logo.png";
        const float LogoAspect = 698f / 1024f;

        static RawImage Logo(Transform parent, float width)
        {
            var rect = Node("Logo", parent);
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = AssetDatabase.LoadAssetAtPath<Texture2D>(LogoPath);
            image.raycastTarget = false;
            rect.sizeDelta = M(width, width * LogoAspect);
            return image;
        }

        // Заставка собрана на «сцене» 390×844 по центру safe area: так координаты совпадают с Splash.dc.html.
        static UiScreen BuildSplash(Transform safeArea)
        {
            var screen = ScreenRoot<SplashScreen>(safeArea, "SplashScreen", out var content);
            var stage = Node("Stage", content);
            stage.anchorMin = stage.anchorMax = new Vector2(0.5f, 0.5f);
            stage.sizeDelta = M(390, 844);
            // Макет считает от верха экрана, а мы стоим в safe area: центр экрана выше её центра на половину выреза.
            stage.anchoredPosition = new Vector2(0f, M(22));

            RectTransform At(RectTransform rect, float x, float y, float w, float h)
            {
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = new Vector2(M(x + w / 2f), -M(y + h / 2f));
                rect.sizeDelta = M(w, h);
                return rect;
            }

            var halo = Image(stage, "Halo", "glow", Hex("#9B78FF", 0.45f));
            At(halo.rectTransform, 195 - 180, 470 - 180, 360, 360);
            Motion(halo.gameObject, IdleMotion.Kind.Pulse, 4f, 0.1f);

            var logoRoot = At(Node("LogoRoot", stage), 45, 96, 300, 300 * LogoAspect);
            var logoGroup = Group(logoRoot.gameObject, 1f, false);
            var logo = Logo(logoRoot, 300);
            logo.rectTransform.Fill();
            Motion(logo.gameObject, IdleMotion.Kind.Float, 3.4f, M(7));

            var pourerRoot = At(Node("Pourer", stage), 114, 289, 58, 138);
            pourerRoot.localRotation = Quaternion.Euler(0f, 0f, -62f);
            var pourer = PlaceBottle(pourerRoot, "Bottle", 56);
            ((RectTransform)pourer.transform).anchorMin = ((RectTransform)pourer.transform).anchorMax = new Vector2(0.5f, 0f);

            var stream = Image(stage, "Stream", null, Color.white);
            var streamRect = stream.rectTransform;
            streamRect.anchorMin = streamRect.anchorMax = new Vector2(0f, 1f);
            streamRect.pivot = new Vector2(0.5f, 1f);
            streamRect.anchoredPosition = new Vector2(M(195), -M(330));
            streamRect.sizeDelta = new Vector2(M(9), M(100));
            var streamColors = Gradient(stream, Color.white, Color.white, true);
            var streamShine = Image(stream.transform, "Shine", "shine", White(0.35f));
            streamShine.rectTransform.Fill();

            var ripple = Image(stage, "Ripple", "ring", Color.white);
            ripple.rectTransform.anchorMin = ripple.rectTransform.anchorMax = new Vector2(0f, 1f);
            ripple.rectTransform.pivot = new Vector2(0.5f, 1f);
            ripple.rectTransform.anchoredPosition = new Vector2(M(195), -M(500));
            ripple.rectTransform.sizeDelta = M(26, 9);
            Motion(ripple.gameObject, IdleMotion.Kind.Pulse, 0.5f, 0.4f);
            Group(ripple.gameObject, 1f, false);

            var targetRoot = At(Node("Target", stage), 149, 400, 92, 220);
            var target = PlaceBottle(targetRoot, "Bottle", 90);

            var sparks = At(Node("Sparks", stage), 191, 500, 9, 9);
            for (int i = 0; i < 10; i++)
            {
                var spark = Image(sparks, "Spark " + i, "glow", i % 2 == 0 ? Color.white : Hex("#FFE07A"));
                spark.rectTransform.sizeDelta = M(16, 16);
                Group(spark.gameObject, 0f, false);
            }

            var shelf = Shelf(stage, "Shelf", 250);
            shelf.anchorMin = shelf.anchorMax = new Vector2(0f, 1f);
            shelf.pivot = new Vector2(0f, 1f);
            shelf.anchoredPosition = new Vector2(M(70), -M(612));

            var caption = Text(stage, "Caption", DisplayFont, 19, Hex("#F0ECFF", 0.92f), TextAlignmentOptions.Center, "splash_caption");
            At(caption.rectTransform, 0, 664, 390, 26);

            var bar = Rounded(stage, "Progress", 8, Hex("#000000", 0.35f));
            At(bar.rectTransform, 65, 704, 260, 16);
            bar.gameObject.AddComponent<RectMask2D>();
            var fill = Rounded(bar.transform, "Fill", 8, Color.white);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(0.06f, 1f);
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            Gradient(fill, Hex("#8EE07A"), Hex("#2A9E4A"));
            var shine = Image(bar.transform, "Shine", "shine", White(0.45f));
            shine.rectTransform.anchorMin = new Vector2(0f, 0f);
            shine.rectTransform.anchorMax = new Vector2(0f, 1f);
            shine.rectTransform.sizeDelta = new Vector2(M(40), 0f);
            var sweep = Motion(shine.gameObject, IdleMotion.Kind.Sweep, 1.8f, 0f);
            Set(sweep, "sweepRange", new Vector2(M(-30), M(290)));
            Set(sweep, "sweepPause", 0f);
            Border(bar.transform, 8, Hex("#DCD2FF", 0.4f));

            var loading = Text(stage, "Loading", BodyFont, 13, Hex("#E1DCFF", 0.8f), TextAlignmentOptions.Center, "splash_loading");
            At(loading.rectTransform, 0, 734, 390, 20);

            var studio = Text(content, "Studio", HeavyFont, 12, Hex("#DCD7FF", 0.55f), TextAlignmentOptions.Center, "studio");
            studio.characterSpacing = Spacing(2f, 12);
            studio.rectTransform.BottomBand(34, 16);

            Set(screen, "logo", logoRoot);
            Set(screen, "logoGroup", logoGroup);
            Set(screen, "pourer", pourer);
            Set(screen, "target", target);
            Set(screen, "stream", streamRect);
            Set(screen, "streamColors", streamColors);
            Set(screen, "ripple", ripple.rectTransform);
            Set(screen, "sparks", sparks);
            Set(screen, "progressFill", fill.rectTransform);
            Set(screen, "loadingLabel", loading.GetComponent<LocalizedText>());
            Set(screen, "mockupScale", K);
            ScreenMotionSetup.SplashOutro(screen);
            BrandIntroSetup.AddTo(screen);
            return screen;
        }

        // Место под баннер внизу: отступ 18 и плашка 50 с рамкой 1.5 (в макете она content-box).
        const float Banner = 18f + 53f;

        // Main.dc.html: сверху вниз от safe area (в макете 54 pt = 44 выреза + 10), снизу вверх от края.
        // У кнопок рамка снаружи (content-box), поэтому высоты больше на две рамки: 93, 68, 66.
        static UiScreen BuildMenu(Transform safeArea)
        {
            var screen = ScreenRoot<MainMenuScreen>(safeArea, "MainMenuScreen", out var content);

            var settings = IconButton(content, "Settings", "settings", false);
            ((RectTransform)settings.transform.parent).Place(new Vector2(0f, 1f), new Vector2(20, -10),
                new Vector2(IconButtonSize, IconButtonSize));
            var coinsRoot = CoinPill(content, "Coins", 130, 47, false, out var coins, true);
            coinsRoot.Place(new Vector2(1f, 1f), new Vector2(-20, -12), new Vector2(130, 47));
            var coinsButton = Button(coinsRoot.Find("Face").gameObject, coinsRoot);

            const float logoTop = 10f + IconButtonSize + 14f;
            var logo = Logo(content, 270);
            logo.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, -logoTop), new Vector2(270, 270 * LogoAspect));
            Motion(logo.gameObject, IdleMotion.Kind.Float, 3.2f, M(8));

            // Полка с бутылками занимает всё между логотипом и круглыми кнопками, содержимое по центру.
            const float roundBottom = Banner + 18f + 68f + 18f + 93f + 22f;
            const float roundHeight = 66f + 6f + 18f;
            var heroArea = Node("Hero", content);
            heroArea.anchorMin = new Vector2(0f, 0f);
            heroArea.anchorMax = new Vector2(1f, 1f);
            heroArea.offsetMin = new Vector2(0f, M(roundBottom + roundHeight + 18f));
            heroArea.offsetMax = new Vector2(0f, -M(logoTop + 270 * LogoAspect));
            var heroRow = Node("Row", heroArea);
            heroRow.anchorMin = heroRow.anchorMax = new Vector2(0.5f, 0.5f);
            heroRow.sizeDelta = M(320, 134 + 6);
            var sizes = new[] { 44f, 52f, 56f, 52f, 44f };
            var periods = new[] { 3.4f, 3.0f, 3.6f, 3.2f, 3.8f };
            var phases = new[] { 0.4f, 1.2f, 2.0f, 0.8f, 1.6f };
            var heroes = new List<BottleStubView>();
            float x = -(sizes[0] + sizes[1] + sizes[2] + sizes[3] + sizes[4] + 40f) / 2f;
            for (int i = 0; i < sizes.Length; i++)
            {
                var holder = Node("Bottle " + (i + 1), heroRow);
                holder.Place(new Vector2(0.5f, 0f), new Vector2(x + sizes[i] / 2f, 6), new Vector2(sizes[i], sizes[i] * 2.4f),
                    new Vector2(0.5f, 0f));
                Motion(holder.gameObject, IdleMotion.Kind.Float, periods[i], M(8), phases[i] / periods[i]);
                var bottle = PlaceBottle(holder, "View", sizes[i]);
                heroes.Add(bottle);
                x += sizes[i] + 10f;
            }
            var shelf = Shelf(heroRow, "Shelf", 320);
            shelf.anchorMin = shelf.anchorMax = new Vector2(0.5f, 0f);
            shelf.pivot = new Vector2(0.5f, 1f);
            shelf.anchoredPosition = new Vector2(0f, M(12));

            var round = Node("RoundButtons", content);
            round.Place(new Vector2(0.5f, 0f), new Vector2(0, roundBottom), new Vector2(330, roundHeight), new Vector2(0.5f, 0f));
            var gift = RoundButton(round, "Gift", -88, "gift", "menu_gift", Hex("#FF8BCB"), Hex("#D81B60"), Hex("#6E0028", 0.35f));
            var bonus = RoundButton(round, "Bonus", 0, "calendar", "menu_bonus", Hex("#66E9FF"), Hex("#0870D5"), Hex("#00286E", 0.35f));
            var luck = RoundButton(round, "Luck", 88, "wheel", "menu_luck", Hex("#E474FF"), Hex("#7E25D4"), Hex("#320064", 0.35f));
            var giftBadge = RoundBadge(gift, Hex("#FF3D3D"), Hex("#FF3D3D"), Color.white, "1");
            var luckBadge = RoundBadge(luck, Hex("#FFC107"), Hex("#FF8A00"), Hex("#3A1A00"), "2");

            const float playBottom = Banner + 18f + 68f + 18f;
            var play = ColorButton(content, "Play", Hex("#B6FF6E"), Hex("#17A523"), 30, Hex("#0E6B17"), 8, out var playFace);
            var playRect = (RectTransform)play.transform;
            playRect.BottomBand(playBottom, 93, 20, 20);
            playFace.GetComponent<UiGradient>().SetColors(Hex("#B6FF6E"), Hex("#5BDB33"), 0.45f, Hex("#17A523"));
            SoftShadow(playRect, 18 + 8, 30, 0.45f);
            var pulse = Rounded(play.transform, "Pulse", 30 + 12, Hex("#9BFF4F", 0.55f));
            pulse.rectTransform.Fill();
            pulse.transform.SetAsFirstSibling();
            Group(pulse.gameObject, 0.55f, false);
            Motion(pulse.gameObject, IdleMotion.Kind.Ripple, 2.2f, M(12));
            playFace.gameObject.AddComponent<RectMask2D>();
            var playShine = Image(playFace, "Shine", "shine", White(0.45f));
            playShine.rectTransform.anchorMin = new Vector2(0f, 0f);
            playShine.rectTransform.anchorMax = new Vector2(0f, 1f);
            playShine.rectTransform.sizeDelta = new Vector2(M(70), 0f);
            playShine.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -20f);
            var playSweep = Motion(playShine.gameObject, IdleMotion.Kind.Sweep, 3.4f, 0f);
            Set(playSweep, "sweepRange", new Vector2(M(-60), M(420)));
            Set(playSweep, "sweepPause", 0.4f);
            var playTitle = Node("Title", playFace);
            playTitle.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 9.2f), new Vector2(300, 44));
            var playLabel = IconLabel(playTitle, "icon_play", 28, "menu_play", 36, 10, out _);
            playLabel.characterSpacing = Spacing(1f, 36);
            Shadow(playLabel, Hex("#0E6B17"), -0.8f);
            var playLevel = Text(playFace, "Level", HeavyFont, 15, Hex("#08450F"), TextAlignmentOptions.Center, "menu_play_level");
            playLevel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, -20.8f), new Vector2(200, 20));

            var levels = MenuButton(content, "Levels", true, Hex("#66E9FF"), Hex("#0870D5"), Hex("#04478C"), "icon_map", "menu_levels",
                Hex("#00286E", 0.6f), out var levelsProgress);
            var shop = MenuButton(content, "Shop", false, Hex("#FFD957"), Hex("#FF7A00"), Hex("#A84A00"), "icon_bag", "menu_shop",
                Hex("#783200", 0.6f), out _);

            var toast = BuildToast(content, 120);

            Set(screen, "settingsButton", settings);
            Set(screen, "coinsButton", coinsButton);
            Set(screen, "coins", coins);
            Set(screen, "giftButton", gift);
            Set(screen, "bonusButton", bonus);
            Set(screen, "luckButton", luck);
            Set(screen, "giftBadge", giftBadge.gameObject);
            Set(screen, "luckBadge", luckBadge.gameObject);
            Set(screen, "luckCount", luckBadge.GetComponentInChildren<TMP_Text>());
            Set(screen, "playButton", play);
            Set(screen, "playLevel", playLevel.GetComponent<LocalizedText>());
            Set(screen, "levelsButton", levels);
            Set(screen, "levelsProgress", levelsProgress.GetComponent<LocalizedText>());
            Set(screen, "shopButton", shop);
            Set(screen, "heroBottles", heroes);
            Set(screen, "toast", toast);
            ScreenMotionSetup.MenuIntro(screen);
            return screen;
        }

        // Кнопки «Уровни» и «Магазин»: две колонки с зазором 14, высота 64 + рамка, «дно» 6 pt.
        static Button MenuButton(Transform content, string name, bool left, Color top, Color bottom, Color depth, string icon,
            string key, Color textShadow, out TMP_Text subtitle)
        {
            var button = ColorButton(content, name, top, bottom, 22, depth, 6, out var face);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = new Vector2(left ? 0f : 0.5f, 0f);
            rect.anchorMax = new Vector2(left ? 0.5f : 1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(M(left ? 20 : 7), M(Banner + 18));
            rect.offsetMax = new Vector2(-M(left ? 7 : 20), M(Banner + 18 + 68));
            SoftShadow(rect, 14 + 6, 24, 0.4f);

            var label = IconLabel(face, icon, 24, key, 20, 10, out _);
            Shadow(label, textShadow, -0.7f);
            subtitle = null;
            if (!left)
                return button;

            // Подпись «24 / 1000» под названием: колонка с line-height 1.05.
            var column = Node("Column", label.transform.parent);
            column.SetSiblingIndex(label.transform.GetSiblingIndex());
            var vertical = column.gameObject.AddComponent<VerticalLayoutGroup>();
            vertical.childAlignment = TextAnchor.MiddleLeft;
            vertical.childControlWidth = vertical.childControlHeight = true;
            vertical.childForceExpandWidth = vertical.childForceExpandHeight = false;
            vertical.spacing = M(-4);
            label.transform.SetParent(column, false);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            subtitle = Text(column, "Progress", BodyFont, 12, Hex("#E6F7FF"), TextAlignmentOptions.MidlineLeft, "menu_levels_progress");
            return button;
        }

        static Button RoundButton(Transform parent, string name, float x, string icon, string key, Color top, Color bottom, Color inset)
        {
            var root = Node(name, parent);
            root.Place(new Vector2(0.5f, 1f), new Vector2(x, 0), new Vector2(76, 90), new Vector2(0.5f, 1f));
            var faceRoot = Node("FaceRoot", root);
            faceRoot.Place(new Vector2(0.5f, 1f), Vector2.zero, new Vector2(66, 66), new Vector2(0.5f, 1f));
            SoftShadow(faceRoot, 8, 18, 0.4f);
            var face = Rounded(faceRoot, "Face", 22, Color.white);
            face.rectTransform.Fill();
            Gradient(face, top, bottom);
            InsetBottom(face.transform, 22, inset);
            Border(face.transform, 22, White(0.45f));
            var glyph = Image(face.transform, "Icon", "icon_" + icon, Color.white);
            glyph.rectTransform.sizeDelta = M(30, 30) * (27f / 24f);
            var label = Text(root, "Label", BodyFont, 13, Color.white, TextAlignmentOptions.Center, key);
            label.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, -72), new Vector2(90, 18), new Vector2(0.5f, 1f));
            return Button(root.gameObject);
        }

        // Бейдж на круглой кнопке: 22 pt плюс белая рамка 2 снаружи, угол −6/−6.
        static Image RoundBadge(Button button, Color top, Color bottom, Color ink, string sample)
        {
            var face = button.transform.Find("FaceRoot/Face");
            var badge = Pill(face, "Badge", 26, 3, 13, 2, 13, ink, out var label);
            badge.rectTransform.Place(new Vector2(1f, 1f), new Vector2(6, 6), new Vector2(26, 26), new Vector2(1f, 1f));
            Gradient(badge, top, bottom);
            label.text = sample;
            return badge;
        }

        static UiScreen BuildLevels(Transform safeArea)
        {
            var screen = ScreenRoot<LevelMapScreen>(safeArea, "LevelMapScreen", out var content);

            var scrollRect = Node("Scroll", content).Fill(0, 150, 0, 0);
            var scroll = scrollRect.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            var viewport = Node("Viewport", scrollRect).Fill();
            viewport.gameObject.AddComponent<RectMask2D>();
            var viewportImage = viewport.gameObject.AddComponent<Image>();
            viewportImage.color = Color.clear;
            var mapContent = Node("Content", viewport);
            mapContent.anchorMin = new Vector2(0f, 1f);
            mapContent.anchorMax = new Vector2(1f, 1f);
            mapContent.pivot = new Vector2(0.5f, 1f);
            mapContent.sizeDelta = new Vector2(0f, M(2000));
            scroll.viewport = viewport;
            scroll.content = mapContent;

            var dot = Image(mapContent, "Path Dot 1", "circle", Color.white);
            dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            dot.gameObject.SetActive(false);
            var node = BuildLevelNode(mapContent);
            node.name = "Level Node 1";

            var back = BackButton(content);
            ((RectTransform)back.transform.parent).Place(new Vector2(0f, 1f), new Vector2(20, -10),
                new Vector2(IconButtonSize, IconButtonSize));
            var title = Text(content, "Title", DisplayFont, 28, Color.white, TextAlignmentOptions.Center, "map_title");
            title.rectTransform.TopBand(10, IconButtonSize, 80, 80);
            Shadow(title, Hex("#001446", 0.7f), -0.8f);

            var starsPill = Rounded(content, "Stars", 20, Hex("#0A1A50", 0.6f));
            starsPill.rectTransform.Place(new Vector2(1f, 1f), new Vector2(-20, -14), new Vector2(76, 43));
            Border(starsPill.transform, 20, Hex("#FFD66E", 0.55f));
            var starIcon = Image(starsPill.transform, "Icon", "star_on", Color.white);
            starIcon.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(13.5f, 0), new Vector2(20, 20), new Vector2(0f, 0.5f));
            var totalStars = Text(starsPill.transform, "Count", DisplayFont, 18, Color.white, TextAlignmentOptions.MidlineLeft);
            totalStars.rectTransform.Fill(39.5f, 0, 8, 0);
            totalStars.text = "0";

            var card = Rounded(content, "Chapter", 20, Color.white);
            card.rectTransform.TopBand(10 + IconButtonSize + 12, 66, 20, 20);
            Gradient(card, Hex("#123C87", 0.85f), Hex("#061B42", 0.85f));
            Border(card.transform, 20, Hex("#78C8FF", 0.45f));
            var previous = ChapterArrow(card.transform, "Previous", true);
            var next = ChapterArrow(card.transform, "Next", false);
            var chapterTitle = Text(card.transform, "Title", HeavyFont, 12, Hex("#7FE3FF"), TextAlignmentOptions.MidlineLeft, "map_chapter");
            chapterTitle.characterSpacing = Spacing(1f, 12);
            chapterTitle.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(17.5f, 12.6f), new Vector2(200, 18), new Vector2(0f, 0.5f));
            chapterTitle.enableAutoSizing = true;
            chapterTitle.fontSizeMin = M(8);
            chapterTitle.fontSizeMax = M(12);
            var chapterName = Text(card.transform, "Name", DisplayFont, 22, Color.white, TextAlignmentOptions.MidlineLeft, "chapter_1");
            chapterName.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(17.5f, -7f), new Vector2(200, 28), new Vector2(0f, 0.5f));
            chapterName.enableAutoSizing = true;
            chapterName.fontSizeMin = M(14);
            chapterName.fontSizeMax = M(22);
            var count = Text(card.transform, "Count", HeavyFont, 13, Color.white, TextAlignmentOptions.MidlineRight);
            count.rectTransform.Place(new Vector2(1f, 0.5f), new Vector2(-17.5f, 7.5f), new Vector2(96, 18), new Vector2(1f, 0.5f));
            count.text = "0 / 50";
            var bar = Rounded(card.transform, "Bar", 5, Hex("#000000", 0.4f));
            bar.rectTransform.Place(new Vector2(1f, 0.5f), new Vector2(-17.5f, -11.4f), new Vector2(96, 10), new Vector2(1f, 0.5f));
            var fill = Rounded(bar.transform, "Fill", 5, Color.white);
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = new Vector2(0.15f, 1f);
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;
            Gradient(fill, Hex("#66E9FF"), Hex("#35F4FF"), true);

            var toast = BuildToast(content, 150);

            Set(screen, "backButton", back);
            Set(screen, "totalStars", totalStars);
            Set(screen, "chapterTitle", chapterTitle.GetComponent<LocalizedText>());
            Set(screen, "chapterName", chapterName.GetComponent<LocalizedText>());
            Set(screen, "chapterCount", count);
            Set(screen, "chapterFill", fill.rectTransform);
            Set(screen, "previousChapter", previous);
            Set(screen, "nextChapter", next);
            Set(screen, "scroll", scroll);
            Set(screen, "content", mapContent);
            Set(screen, "nodes", new List<LevelNode> { node });
            Set(screen, "dots", new List<Image> { dot });
            Set(screen, "toast", toast);
            return screen;
        }

        // Стрелки глав — в макете их нет, поэтому это маленькие шевроны в полях карточки,
        // а зона касания 44 pt лежит поверх края текста.
        static Button ChapterArrow(Transform card, string name, bool left)
        {
            var root = Node(name, card);
            root.Place(new Vector2(left ? 0f : 1f, 0.5f), new Vector2(left ? 8 : -8, 0), new Vector2(44, 66), new Vector2(left ? 0.3f : 0.7f, 0.5f));
            var icon = Image(root, "Icon", left ? "icon_back" : "icon_arrow_right", White(0.6f));
            icon.rectTransform.Place(new Vector2(left ? 0.3f : 0.7f, 0.5f), Vector2.zero, new Vector2(12, 12));
            return Button(root.gameObject);
        }

        static LevelNode BuildLevelNode(Transform parent)
        {
            var root = Node("NodeTemplate", parent);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 1f);
            root.sizeDelta = M(78, 78);
            var node = root.gameObject.AddComponent<LevelNode>();

            var ring = Image(root, "Ring", "ring", Hex("#9BFF4F"));
            ring.rectTransform.sizeDelta = M(78, 78);
            Group(ring.gameObject, 0.85f, false);
            Motion(ring.gameObject, IdleMotion.Kind.RingOut, 1.6f, 0.55f);

            // Свечение текущего: box-shadow 0 0 28px rgba(155,255,79,.75) вокруг круга 78.
            var glow = Image(root, "Glow", "glow", Hex("#9BFF4F", 0.9f));
            glow.rectTransform.sizeDelta = M(78 + 70, 78 + 70);
            var face = Image(root, "Face", "node_done", Color.white, true);
            face.rectTransform.sizeDelta = M(58, 58);
            var lockIcon = Image(face.transform, "Lock", "icon_lock", Hex("#DCE8FF", 0.85f));
            lockIcon.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 9), new Vector2(16, 16));
            var crown = Image(face.transform, "Crown", "crown", Color.white);
            crown.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 9), new Vector2(24, 20));
            var number = Text(face.transform, "Number", DisplayFont, 22, Hex("#5A2A00"));
            number.rectTransform.Fill();
            number.text = "1";

            var starsRoot = Node("Stars", root);
            starsRoot.Place(new Vector2(0.5f, 1f), new Vector2(0, -2), new Vector2(50, 16), new Vector2(0.5f, 0f));
            var stars = new List<Image>();
            for (int i = 0; i < 3; i++)
            {
                var star = Image(starsRoot, "Star " + i, "star_on", Color.white);
                star.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 17, 0), new Vector2(16, 16));
                stars.Add(star);
            }

            var bubble = Rounded(root, "PlayBubble", 12, Color.white);
            // В макете translateX(-50%) у пузыря перебит анимацией bob, поэтому он начинается от центра узла.
            bubble.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, 42 - 26), new Vector2(67, 26), new Vector2(0f, 0f));
            var bubbleText = Text(bubble.transform, "Label", HeavyFont, 13, Hex("#0E6B17"), TextAlignmentOptions.Center, "map_play");
            bubbleText.rectTransform.Fill();
            Motion(bubble.gameObject, IdleMotion.Kind.Float, 1.8f, M(5));

            var tutorial = Text(root, "Tutorial", HeavyFont, 11, Hex("#CFE6FF"), TextAlignmentOptions.Center, "map_tutorial");
            tutorial.rectTransform.Place(new Vector2(0.5f, 0f), new Vector2(0, -2), new Vector2(90, 16), new Vector2(0.5f, 1f));

            var button = Button(face.gameObject, root);
            Set(node, "button", button);
            Set(node, "face", face);
            Set(node, "faceRect", face.rectTransform);
            Set(node, "number", number);
            Set(node, "starsRoot", starsRoot.gameObject);
            Set(node, "stars", stars);
            Set(node, "lockIcon", lockIcon.gameObject);
            Set(node, "crown", crown.gameObject);
            Set(node, "ring", ring.gameObject);
            Set(node, "currentGlow", glow.gameObject);
            Set(node, "playBubble", bubble.gameObject);
            Set(node, "tutorialLabel", tutorial.gameObject);
            Set(node, "doneSprite", Sprite("node_done"));
            Set(node, "currentSprite", Sprite("node_current"));
            Set(node, "lockedSprite", Sprite("node_locked"));
            Set(node, "bossSprite", Sprite("node_boss"));
            Set(node, "starOn", Sprite("star_on"));
            Set(node, "starOff", Sprite("star_off"));
            root.gameObject.SetActive(false);
            return node;
        }
    }
}
