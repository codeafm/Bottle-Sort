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
            studio.characterSpacing = 18f;
            studio.rectTransform.BottomBand(16, 20);

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
            return screen;
        }

        static UiScreen BuildMenu(Transform safeArea)
        {
            var screen = ScreenRoot<MainMenuScreen>(safeArea, "MainMenuScreen", out var content);

            var settings = IconButton(content, "Settings", "settings", false);
            ((RectTransform)settings.transform.parent).Place(new Vector2(0f, 1f), new Vector2(20, -10), new Vector2(48, 48));
            var coinsRoot = CoinPill(content, "Coins", 130, 44, false, out var coins, true);
            coinsRoot.Place(new Vector2(1f, 1f), new Vector2(-20, -12), new Vector2(130, 44));
            var coinsButton = Button(coinsRoot.Find("Face").gameObject, coinsRoot);

            var logo = Logo(content, 270);
            logo.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, -72), new Vector2(270, 270 * LogoAspect));
            Motion(logo.gameObject, IdleMotion.Kind.Float, 3.2f, M(8));

            // Полка с бутылками занимает всё между логотипом и круглыми кнопками.
            var heroArea = Node("Hero", content);
            heroArea.anchorMin = new Vector2(0f, 0f);
            heroArea.anchorMax = new Vector2(1f, 1f);
            heroArea.offsetMin = new Vector2(0f, M(382));
            heroArea.offsetMax = new Vector2(0f, -M(72 + 270 * LogoAspect));
            var heroRow = Node("Row", heroArea);
            heroRow.anchorMin = heroRow.anchorMax = new Vector2(0.5f, 0.5f);
            heroRow.sizeDelta = M(320, 140);
            var sizes = new[] { 44f, 52f, 56f, 52f, 44f };
            var periods = new[] { 3.4f, 3.0f, 3.6f, 3.2f, 3.8f };
            var heroes = new List<BottleStubView>();
            float x = -(sizes[0] + sizes[1] + sizes[2] + sizes[3] + sizes[4] + 40f) / 2f;
            for (int i = 0; i < sizes.Length; i++)
            {
                var holder = Node("Bottle " + (i + 1), heroRow);
                holder.Place(new Vector2(0.5f, 0f), new Vector2(x + sizes[i] / 2f, 6), new Vector2(sizes[i], sizes[i] * 2.4f),
                    new Vector2(0.5f, 0f));
                Motion(holder.gameObject, IdleMotion.Kind.Float, periods[i], M(8), i * 0.23f);
                var bottle = PlaceBottle(holder, "View", sizes[i]);
                heroes.Add(bottle);
                x += sizes[i] + 10f;
            }
            var shelf = Shelf(heroRow, "Shelf", 320);
            shelf.anchorMin = shelf.anchorMax = new Vector2(0.5f, 0f);
            shelf.pivot = new Vector2(0.5f, 1f);
            shelf.anchoredPosition = new Vector2(0f, M(12));

            var round = Node("RoundButtons", content);
            round.Place(new Vector2(0.5f, 0f), new Vector2(0, 278), new Vector2(330, 86), new Vector2(0.5f, 0f));
            var gift = RoundButton(round, "Gift", -84, "gift", "menu_gift", Hex("#FF8BCB"), Hex("#D81B60"));
            var bonus = RoundButton(round, "Bonus", 0, "calendar", "menu_bonus", Hex("#66E9FF"), Hex("#0870D5"));
            var luck = RoundButton(round, "Luck", 84, "wheel", "menu_luck", Hex("#E474FF"), Hex("#7E25D4"));

            var play = ColorButton(content, "Play", Hex("#B6FF6E"), Hex("#17A523"), 30, Hex("#0E6B17"), 8, out var playFace);
            ((RectTransform)play.transform).BottomBand(176, 88, 20, 20);
            playFace.gameObject.AddComponent<RectMask2D>();
            var playShine = Image(playFace, "Shine", "shine", White(0.45f));
            playShine.rectTransform.anchorMin = new Vector2(0f, 0f);
            playShine.rectTransform.anchorMax = new Vector2(0f, 1f);
            playShine.rectTransform.sizeDelta = new Vector2(M(70), 0f);
            playShine.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -20f);
            var playSweep = Motion(playShine.gameObject, IdleMotion.Kind.Sweep, 3.4f, 0f);
            Set(playSweep, "sweepRange", new Vector2(M(-60), M(420)));
            Set(playSweep, "sweepPause", 0.4f);
            var playIcon = Image(playFace, "Icon", "icon_play", Color.white);
            playIcon.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(-78, 10), new Vector2(28, 28));
            var playLabel = Text(playFace, "Label", DisplayFont, 36, Color.white, TextAlignmentOptions.Center, "menu_play");
            playLabel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(14, 10), new Vector2(200, 44));
            Shadow(playLabel, Hex("#0E6B17"), -0.8f);
            var playLevel = Text(playFace, "Level", HeavyFont, 15, Hex("#08450F"), TextAlignmentOptions.Center, "menu_play_level");
            playLevel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, -22), new Vector2(200, 20));
            Motion(play.gameObject, IdleMotion.Kind.Pulse, 2.2f, 0.02f);

            var levels = ColorButton(content, "Levels", Hex("#66E9FF"), Hex("#0870D5"), 22, Hex("#04478C"), 6, out var levelsFace);
            var levelsRect = (RectTransform)levels.transform;
            levelsRect.anchorMin = new Vector2(0f, 0f);
            levelsRect.anchorMax = new Vector2(0.5f, 0f);
            levelsRect.pivot = new Vector2(0.5f, 0f);
            levelsRect.offsetMin = new Vector2(M(20), M(86));
            levelsRect.offsetMax = new Vector2(-M(7), M(150));
            var mapIcon = Image(levelsFace, "Icon", "icon_map", Color.white);
            mapIcon.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(-48, 0), new Vector2(24, 24));
            var levelsLabel = Text(levelsFace, "Label", DisplayFont, 20, Color.white, TextAlignmentOptions.MidlineLeft, "menu_levels");
            levelsLabel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(-30, 8), new Vector2(110, 26), new Vector2(0f, 0.5f));
            Shadow(levelsLabel, Hex("#00286E", 0.6f));
            var levelsProgress = Text(levelsFace, "Progress", BodyFont, 12, Hex("#E6F7FF"), TextAlignmentOptions.MidlineLeft, "menu_levels_progress");
            levelsProgress.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(-30, -12), new Vector2(110, 16), new Vector2(0f, 0.5f));

            var shop = ColorButton(content, "Shop", Hex("#FFD957"), Hex("#FF7A00"), 22, Hex("#A84A00"), 6, out var shopFace);
            var shopRect = (RectTransform)shop.transform;
            shopRect.anchorMin = new Vector2(0.5f, 0f);
            shopRect.anchorMax = new Vector2(1f, 0f);
            shopRect.pivot = new Vector2(0.5f, 0f);
            shopRect.offsetMin = new Vector2(M(7), M(86));
            shopRect.offsetMax = new Vector2(-M(20), M(150));
            var bagIcon = Image(shopFace, "Icon", "icon_bag", Color.white);
            bagIcon.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(-44, 0), new Vector2(24, 24));
            var shopLabel = Text(shopFace, "Label", DisplayFont, 20, Color.white, TextAlignmentOptions.MidlineLeft, "menu_shop");
            shopLabel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(-26, 0), new Vector2(110, 26), new Vector2(0f, 0.5f));
            Shadow(shopLabel, Hex("#783200", 0.6f));

            var toast = BuildToast(content, 120);

            Set(screen, "settingsButton", settings);
            Set(screen, "coinsButton", coinsButton);
            Set(screen, "coins", coins);
            Set(screen, "giftButton", gift);
            Set(screen, "bonusButton", bonus);
            Set(screen, "luckButton", luck);
            Set(screen, "playButton", play);
            Set(screen, "playLevel", playLevel.GetComponent<LocalizedText>());
            Set(screen, "levelsButton", levels);
            Set(screen, "levelsProgress", levelsProgress.GetComponent<LocalizedText>());
            Set(screen, "shopButton", shop);
            Set(screen, "heroBottles", heroes);
            Set(screen, "toast", toast);
            return screen;
        }

        static Button RoundButton(Transform parent, string name, float x, string icon, string key, Color top, Color bottom)
        {
            var root = Node(name, parent);
            root.Place(new Vector2(0.5f, 1f), new Vector2(x, 0), new Vector2(72, 86), new Vector2(0.5f, 1f));
            var face = Rounded(root, "Face", 22, Color.white);
            face.rectTransform.Place(new Vector2(0.5f, 1f), Vector2.zero, new Vector2(62, 62), new Vector2(0.5f, 1f));
            Gradient(face, top, bottom);
            Border(face.transform, 22, White(0.45f));
            var glyph = Image(face.transform, "Icon", "icon_" + icon, Color.white);
            glyph.rectTransform.sizeDelta = M(30, 30);
            var label = Text(root, "Label", BodyFont, 13, Color.white, TextAlignmentOptions.Center, key);
            label.rectTransform.Place(new Vector2(0.5f, 0f), Vector2.zero, new Vector2(90, 18), new Vector2(0.5f, 0f));
            return Button(root.gameObject);
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

            var dot = Image(mapContent, "DotTemplate", "circle", Color.white);
            dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            dot.gameObject.SetActive(false);
            var node = BuildLevelNode(mapContent);

            var back = BackButton(content);
            ((RectTransform)back.transform.parent).Place(new Vector2(0f, 1f), new Vector2(20, -10), new Vector2(48, 48));
            var title = Text(content, "Title", DisplayFont, 28, Color.white, TextAlignmentOptions.Center, "map_title");
            title.rectTransform.TopBand(10, 48, 80, 80);
            Shadow(title, Hex("#001446", 0.7f), -0.8f);

            var starsPill = Rounded(content, "Stars", 20, Hex("#0A1A50", 0.6f));
            starsPill.rectTransform.Place(new Vector2(1f, 1f), new Vector2(-20, -14), new Vector2(84, 40));
            Border(starsPill.transform, 20, Hex("#FFD66E", 0.55f));
            var starIcon = Image(starsPill.transform, "Icon", "star_on", Color.white);
            starIcon.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(10, 0), new Vector2(20, 20), new Vector2(0f, 0.5f));
            var totalStars = Text(starsPill.transform, "Count", DisplayFont, 18, Color.white, TextAlignmentOptions.MidlineRight);
            totalStars.rectTransform.Fill(34, 0, 12, 0);
            totalStars.text = "0";

            var card = Rounded(content, "Chapter", 20, Color.white);
            card.rectTransform.TopBand(70, 70, 20, 20);
            Gradient(card, Hex("#123C87", 0.85f), Hex("#061B42", 0.85f));
            Border(card.transform, 20, Hex("#78C8FF", 0.45f));
            var previous = ChapterArrow(card.transform, "Previous", true);
            var next = ChapterArrow(card.transform, "Next", false);
            var chapterTitle = Text(card.transform, "Title", HeavyFont, 12, Hex("#7FE3FF"), TextAlignmentOptions.MidlineLeft, "map_chapter");
            chapterTitle.characterSpacing = 4f;
            chapterTitle.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(52, 12), new Vector2(150, 18), new Vector2(0f, 0.5f));
            chapterTitle.enableAutoSizing = true;
            chapterTitle.fontSizeMin = M(8);
            chapterTitle.fontSizeMax = M(12);
            var chapterName = Text(card.transform, "Name", DisplayFont, 22, Color.white, TextAlignmentOptions.MidlineLeft, "chapter_1");
            chapterName.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(52, -10), new Vector2(136, 28), new Vector2(0f, 0.5f));
            chapterName.enableAutoSizing = true;
            chapterName.fontSizeMin = M(14);
            chapterName.fontSizeMax = M(22);
            var count = Text(card.transform, "Count", HeavyFont, 13, Color.white, TextAlignmentOptions.MidlineRight);
            count.rectTransform.Place(new Vector2(1f, 0.5f), new Vector2(-52, 10), new Vector2(84, 18), new Vector2(1f, 0.5f));
            count.text = "0 / 50";
            var bar = Rounded(card.transform, "Bar", 5, Hex("#000000", 0.4f));
            bar.rectTransform.Place(new Vector2(1f, 0.5f), new Vector2(-52, -10), new Vector2(84, 10), new Vector2(1f, 0.5f));
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
            Set(screen, "nodeTemplate", node);
            Set(screen, "dotTemplate", dot);
            Set(screen, "toast", toast);
            return screen;
        }

        static Button ChapterArrow(Transform card, string name, bool left)
        {
            var root = Node(name, card);
            root.Place(new Vector2(left ? 0f : 1f, 0.5f), new Vector2(left ? 4 : -4, 0), new Vector2(44, 44),
                new Vector2(left ? 0f : 1f, 0.5f));
            var icon = Image(root, "Icon", left ? "icon_back" : "icon_arrow_right", White(0.85f));
            icon.rectTransform.sizeDelta = M(20, 20);
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
            Motion(ring.gameObject, IdleMotion.Kind.Pulse, 1.6f, 0.55f);

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
            bubble.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0, 6), new Vector2(76, 26), new Vector2(0.5f, 0f));
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
