using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static BottleSort.UI.Editor.UiKit;

namespace BottleSort.UI.Editor
{
    // Колесо удачи по Wheel.dc.html: всё стоит на «сцене» 390×844 pt, как в макете, координаты — от её верхнего левого угла.
    public static partial class UiSceneBuilder
    {
        const float WheelSize = 340f;
        static readonly Vector2 WheelCenter = new Vector2(195f, 400f);

        static RectTransform AtStage(RectTransform rect, Vector2 center, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(M(center.x), -M(center.y));
            rect.sizeDelta = M(size.x, size.y);
            return rect;
        }

        static Sprite WheelSprite(string name) => Sprite("Wheel/" + name);

        // Тень кнопки внутри неё самой: кнопки «Крутить» и «Ещё попытка» стоят в одном месте и прячутся по очереди.
        static void ShadowInside(Button button, float alpha)
        {
            var shadow = SoftShadow((RectTransform)button.transform, 16 + 7, 28, alpha);
            shadow.transform.SetParent(button.transform, true);
            shadow.transform.SetAsFirstSibling();
        }

        static UiScreen BuildWheel(Transform safeArea)
        {
            var screen = ScreenRoot<WheelPopup>(safeArea, "WheelPopup", out var content);

            var stage = Node("Stage", content);
            stage.anchorMin = stage.anchorMax = new Vector2(0.5f, 0.5f);
            stage.sizeDelta = M(390, 844);
            stage.anchoredPosition = new Vector2(0f, M(22));

            // Затемнение ловит касания, чтобы меню под ним не нажималось. Картинка с запасом вокруг экрана макета.
            var dim = Image(stage, "Dimmer", null, Color.white, true);
            dim.sprite = WheelSprite("wheel_dim");
            AtStage(dim.rectTransform, new Vector2(195, 422), new Vector2(390 + 400, 844 + 800));

            var rays = Image(stage, "Rays", null, Color.white);
            rays.sprite = WheelSprite("wheel_rays");
            AtStage(rays.rectTransform, WheelCenter, new Vector2(720, 720));
            var warm = Image(stage, "Warm", "glow", Hex("#FFC850", 0.5f));
            AtStage(warm.rectTransform, WheelCenter, new Vector2(330, 330));

            var ribbon = BuildWheelRibbon(stage);

            var closeRoot = Node("Close", stage);
            AtStage(closeRoot, new Vector2(390 - 18 - 22, 60 + 22), new Vector2(44, 44));
            var closeFace = Rounded(closeRoot, "Face", 15, White(0.12f), true);
            closeFace.rectTransform.Fill();
            Border(closeFace.transform, 15, Hex("#DCEBFF", 0.45f));
            var closeIcon = Image(closeFace.transform, "Icon", "icon_close", Color.white);
            closeIcon.rectTransform.sizeDelta = M(20, 20) * (27f / 24f);
            var close = Button(closeFace.gameObject, closeRoot);

            var wheel = Node("Wheel", stage);
            AtStage(wheel, WheelCenter, new Vector2(WheelSize, WheelSize));
            Image(wheel, "Base", null, Color.white).sprite = WheelSprite("wheel_base");
            wheel.Find("Base").GetComponent<RectTransform>().Fill();

            var face = Node("Face", wheel).Fill();
            Image(face, "Sectors", null, Color.white).sprite = WheelSprite("wheel_face");
            face.Find("Sectors").GetComponent<RectTransform>().Fill();
            var labels = new List<TMP_Text>();
            string[] iconOfPrize = { "coin", "icon_undo", "icon_hint", "icon_add_bottle", "coin", "icon_wheel" };
            for (int sector = 0; sector < 6; sector++)
            {
                var spoke = Node("Sector " + sector, face).Fill();
                spoke.localRotation = Quaternion.Euler(0f, 0f, -60f * sector);
                // Надпись и иконка в координатах макета: центр (170, 170), надпись на y 66, иконка на y 104.
                var label = Text(spoke, "Amount", DisplayFont, 32, Color.white);
                label.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 170 - 55), new Vector2(110, 40));
                label.outlineWidth = 0.18f;
                label.outlineColor = new Color32(0, 0, 0, 85);
                label.text = "+1";
                labels.Add(label);
                string icon = iconOfPrize[WheelPopup.PrizeOfSector[sector]];
                if (icon == "coin")
                    Coin(spoke, "Icon", new Vector2(0.5f, 0.5f), new Vector2(0, 170 - 104), 33);
                else
                    Image(spoke, "Icon", icon, Color.white).rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, 170 - 104),
                        new Vector2(29, 29) * (27f / 24f));
            }

            var win = Image(wheel, "Win", null, Color.white);
            win.sprite = WheelSprite("wheel_win");
            win.rectTransform.Fill();
            var winGroup = Group(win.gameObject, 0.6f, false);
            win.gameObject.SetActive(false);

            Image(wheel, "Overlay", null, Color.white).sprite = WheelSprite("wheel_overlay");
            wheel.Find("Overlay").GetComponent<RectTransform>().Fill();

            // 18 лампочек по ободу на радиусе 158, через каждые 20° начиная с 10°.
            var bulbs = new List<Image>();
            var glows = new List<GameObject>();
            var bulbRoot = Node("Bulbs", wheel).Fill();
            for (int i = 0; i < 18; i++)
            {
                float angle = (10f + 20f * i) * Mathf.Deg2Rad;
                var position = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * 158f;
                var glow = Image(bulbRoot, "Glow " + i, "glow", Hex("#FFE27A", 0.9f));
                glow.rectTransform.Place(new Vector2(0.5f, 0.5f), position, new Vector2(22, 22));
                var bulb = Image(bulbRoot, "Bulb " + i, "circle", Hex("#FFF6C8"));
                bulb.rectTransform.Place(new Vector2(0.5f, 0.5f), position, new Vector2(9.2f, 9.2f));
                bulbs.Add(bulb);
                glows.Add(glow.gameObject);
            }

            // Стрелка: коробка 46×60 из макета, вращается вокруг точки (23, 10); у спрайта поля под тень.
            var pointer = Node("Pointer", stage);
            pointer.anchorMin = pointer.anchorMax = new Vector2(0f, 1f);
            pointer.pivot = new Vector2(0.5f, 1f - 10f / 60f);
            pointer.anchoredPosition = new Vector2(M(172 + 23), -M(206 + 10));
            pointer.sizeDelta = M(46, 60);
            var pointerImage = Image(pointer, "Image", null, Color.white);
            pointerImage.sprite = WheelSprite("wheel_pointer");
            pointerImage.rectTransform.anchorMin = Vector2.zero;
            pointerImage.rectTransform.anchorMax = Vector2.one;
            pointerImage.rectTransform.offsetMin = new Vector2(-M(6), -M(12));
            pointerImage.rectTransform.offsetMax = new Vector2(M(12), M(4));

            // Под колесом: «Попыток: N» и кнопка. Пилюля 37 pt (рамка снаружи), зазор 16, кнопка 300 + рамка.
            var pill = Rounded(stage, "Spins", 17.5f, White(0.12f));
            AtStage(pill.rectTransform, new Vector2(195, 592 + 18.7f), new Vector2(150, 37.4f));
            Border(pill.transform, 17.5f, Hex("#FFD66E", 0.5f));
            var spinsLabel = IconLabel(pill.rectTransform, "icon_restart", 18, "wheel_spins_left", 15, 8, out var spinsIcon);
            spinsLabel.font = HeavyFont;
            spinsIcon.color = Hex("#FFD957");

            const float buttonTop = 592 + 37.4f + 16;
            var spinRoot = Node("SpinButton", stage);
            AtStage(spinRoot, new Vector2(195, buttonTop + 40.5f), new Vector2(305, 81));
            var spinGroup = Group(spinRoot.gameObject);
            var spin = ColorButton(spinRoot, "Spin", Hex("#B6FF6E"), Hex("#17A523"), 26, Hex("#0E6B17"), 7, out var spinFace);
            ((RectTransform)spin.transform).Fill();
            spinFace.GetComponent<UiGradient>().SetColors(Hex("#B6FF6E"), Hex("#5BDB33"), 0.45f, Hex("#17A523"));
            ShadowInside(spin, 0.45f);
            var pulse = Rounded(spin.transform, "Pulse", 26 + 12, Hex("#9BFF4F", 0.55f));
            pulse.rectTransform.Fill();
            pulse.transform.SetAsFirstSibling();
            Group(pulse.gameObject, 1f, false);
            Motion(pulse.gameObject, IdleMotion.Kind.Ripple, 2.2f, M(12));
            spinFace.gameObject.AddComponent<RectMask2D>();
            var shine = Image(spinFace, "Shine", "shine", White(0.45f));
            shine.rectTransform.anchorMin = new Vector2(0f, 0f);
            shine.rectTransform.anchorMax = new Vector2(0f, 1f);
            shine.rectTransform.sizeDelta = new Vector2(M(60), 0f);
            shine.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -20f);
            var sweep = Motion(shine.gameObject, IdleMotion.Kind.Sweep, 3f, 0f);
            Set(sweep, "sweepRange", new Vector2(M(-60), M(360)));
            Set(sweep, "sweepPause", 0.4f);
            var spinLabel = Text(spinFace, "Label", DisplayFont, 32, Color.white, TextAlignmentOptions.Center, "wheel_spin", true);
            spinLabel.characterSpacing = Spacing(1f, 32);
            spinLabel.rectTransform.Fill();
            Shadow(spinLabel, Hex("#0E6B17"), -0.8f);

            var adRoot = Node("AdButton", stage);
            AtStage(adRoot, new Vector2(195, buttonTop + 40.5f), new Vector2(305, 81));
            var ad = ColorButton(adRoot, "Ad", Hex("#E474FF"), Hex("#7E25D4"), 26, Hex("#4A0E8A"), 7, out var adFace);
            ((RectTransform)ad.transform).Fill();
            ShadowInside(ad, 0.45f);
            IconLabel(adFace, "icon_video", 26, "wheel_spin_ad", 24, 10, out _);
            ad.gameObject.SetActive(false);

            // Конфетти и монеты из центра лежат под затемнением карточки, как z-index в макете.
            var confetti = Node("Confetti", stage);
            confetti.anchorMin = new Vector2(0f, 1f);
            confetti.anchorMax = new Vector2(1f, 1f);
            confetti.pivot = new Vector2(0.5f, 1f);
            confetti.sizeDelta = Vector2.zero;
            string[] colors = { "#FFD957", "#FF6FD8", "#5CC8FF", "#7BE36A", "#B48CFF", "#FF9C3A" };
            var random = new System.Random(17);
            for (int i = 0; i < 30; i++)
            {
                var piece = Rounded(confetti, "Piece " + i, 2, Hex(colors[i % colors.Length]));
                piece.rectTransform.anchorMin = piece.rectTransform.anchorMax = new Vector2((float)random.NextDouble(), 1f);
                piece.rectTransform.sizeDelta = M(6 + random.Next(7), 10 + random.Next(9));
                piece.rectTransform.localRotation = Quaternion.Euler(0f, 0f, random.Next(360));
            }
            confetti.gameObject.SetActive(false);

            var burstRoot = Node("Burst", stage);
            AtStage(burstRoot, WheelCenter, Vector2.zero);
            var burst = new List<RectTransform>();
            for (int i = 0; i < 14; i++)
            {
                var coinRoot = Node("Coin " + i, burstRoot);
                coinRoot.sizeDelta = M(30, 30);
                Group(coinRoot.gameObject, 0f, false);
                Coin(coinRoot, "Image", new Vector2(0.5f, 0.5f), Vector2.zero, 30);
                coinRoot.gameObject.SetActive(false);
                burst.Add(coinRoot);
            }

            var reward = BuildWheelReward(content, stage, out var card, out var tile, out var tileGlow, out var rewardCoin,
                out var rewardIcon, out var amount, out var rewardName, out var collect);

            // Монеты к счётчику летят поверх карточки, поэтому отдельный слой после неё.
            var flyStage = Node("FlyingCoins", content);
            flyStage.anchorMin = flyStage.anchorMax = stage.anchorMin;
            flyStage.sizeDelta = stage.sizeDelta;
            flyStage.anchoredPosition = stage.anchoredPosition;
            var flyRoot = Node("Center", flyStage);
            AtStage(flyRoot, WheelCenter, Vector2.zero);
            var flying = new List<RectTransform>();
            for (int i = 0; i < 8; i++)
            {
                var coinRoot = Node("Coin " + i, flyRoot);
                coinRoot.sizeDelta = M(28, 28);
                Coin(coinRoot, "Image", new Vector2(0.5f, 0.5f), Vector2.zero, 28);
                coinRoot.gameObject.SetActive(false);
                flying.Add(coinRoot);
            }

            Set(screen, "closeButton", close);
            Set(screen, "ribbon", ribbon);
            Set(screen, "rays", rays.rectTransform);
            Set(screen, "wheelFace", face);
            Set(screen, "sectorLabels", labels);
            Set(screen, "bulbs", bulbs);
            Set(screen, "bulbGlows", glows);
            Set(screen, "pointer", pointer);
            Set(screen, "winHighlight", winGroup);
            Set(screen, "spinsLeft", spinsLabel.GetComponent<LocalizedText>());
            Set(screen, "spinButton", spin);
            Set(screen, "spinGroup", spinGroup);
            Set(screen, "adButton", ad);
            Set(screen, "burstCoins", burst);
            Set(screen, "confetti", confetti);
            Set(screen, "rewardLayer", reward);
            Set(screen, "rewardCard", card);
            Set(screen, "rewardTile", tile);
            Set(screen, "rewardTileGlow", tileGlow);
            Set(screen, "rewardCoin", rewardCoin);
            Set(screen, "rewardIcon", rewardIcon);
            Set(screen, "rewardAmount", amount);
            Set(screen, "rewardName", rewardName);
            Set(screen, "collectButton", collect);
            Set(screen, "flyingCoins", flying);
            Set(screen, "undoIcon", Sprite("icon_undo"));
            Set(screen, "hintIcon", Sprite("icon_hint"));
            Set(screen, "bottleIcon", Sprite("icon_add_bottle"));
            Set(screen, "spinIcon", Sprite("icon_wheel"));
            return screen;
        }

        // Лента «КОЛЕСО УДАЧИ»: фиолетовая с золотой рамкой 3 pt и хвостами с вырезом.
        static RectTransform BuildWheelRibbon(RectTransform stage)
        {
            var ribbon = Node("Ribbon", stage);
            AtStage(ribbon, new Vector2(195, 104 + 35), new Vector2(300, 70));
            var tailLeft = Image(ribbon, "TailLeft", "ribbon_tail", Hex("#3A1490"));
            tailLeft.rectTransform.Place(new Vector2(0f, 1f), new Vector2(-6, -22), new Vector2(52, 46), new Vector2(0f, 1f));
            var tailRight = Image(ribbon, "TailRight", "ribbon_tail", Hex("#3A1490"));
            tailRight.rectTransform.Place(new Vector2(1f, 1f), new Vector2(6, -22), new Vector2(52, 46), new Vector2(0f, 1f));
            tailRight.rectTransform.localScale = new Vector3(-1f, 1f, 1f);

            var gold = Rounded(ribbon, "Gold", 14, Hex("#FFD957"));
            gold.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(256, 64));
            SoftShadow(gold.rectTransform, 8, 20, 0.45f);
            var body = Rounded(gold.transform, "Body", 11, Color.white);
            body.rectTransform.Fill(3, 3, 3, 3);
            Gradient(body, Color.white, Color.white).SetColors(Hex("#B07BFF"), Hex("#7B3FE4"), 0.5f, Hex("#4F1FB0"));
            var label = Text(gold.transform, "Label", DisplayFont, 27, Color.white, TextAlignmentOptions.Center, "wheel_title", true);
            label.characterSpacing = Spacing(1.5f, 27);
            label.rectTransform.Fill();
            Shadow(label, Hex("#2A0A70"), -0.8f);
            return ribbon;
        }

        // Карточка «ВЫИГРЫШ!» 290 pt по центру (195, 420) поверх затемнения.
        static CanvasGroup BuildWheelReward(RectTransform content, RectTransform stage, out RectTransform card, out UiGradient tile,
            out Image tileGlow, out GameObject coin, out Image icon, out TMP_Text amount, out LocalizedText name, out Button collect)
        {
            var layer = Node("Reward", content).Fill();
            var group = Group(layer.gameObject, 1f, true);
            var dim = Image(layer, "Dim", null, Hex("#030516", 0.45f), true);
            dim.rectTransform.anchorMin = Vector2.zero;
            dim.rectTransform.anchorMax = Vector2.one;
            dim.rectTransform.offsetMin = new Vector2(-400f, -400f);
            dim.rectTransform.offsetMax = new Vector2(400f, 400f);

            var cardStage = Node("Stage", layer);
            cardStage.anchorMin = cardStage.anchorMax = new Vector2(0.5f, 0.5f);
            cardStage.sizeDelta = stage.sizeDelta;
            cardStage.anchoredPosition = stage.anchoredPosition;

            const float height = 350f;
            var outer = Rounded(cardStage, "Card", 28, Hex("#FFD957"));
            AtStage(outer.rectTransform, new Vector2(195, 420), new Vector2(290, height));
            SoftShadow(outer.rectTransform, 20, 40, 0.5f);
            SoftShadow(outer.rectTransform, 0, 40, Hex("#FFC83D", 0.85f));
            card = outer.rectTransform;
            var inner = Rounded(outer.transform, "Body", 25, Color.white, true);
            inner.rectTransform.Fill(3, 3, 3, 3);
            Gradient(inner, Hex("#2A4FA8"), Hex("#0F2462"));

            RectTransform Row(string rowName, float centerFromTop, float rowHeight)
            {
                var row = Node(rowName, outer.transform);
                row.Place(new Vector2(0.5f, 1f), new Vector2(0, -centerFromTop), new Vector2(250, rowHeight), new Vector2(0.5f, 0.5f));
                return row;
            }

            var title = Text(Row("Title", 41, 24), "Label", DisplayFont, 20, Hex("#FFD957"), TextAlignmentOptions.Center, "wheel_win_title");
            title.characterSpacing = Spacing(2f, 20);
            title.rectTransform.Fill();

            var tileRoot = Row("Tile", 114, 102);
            tileRoot.sizeDelta = M(102, 102);
            tileGlow = Image(tileRoot, "Glow", "shadow", Hex("#F0478E", 0.8f));
            tileGlow.type = UnityEngine.UI.Image.Type.Sliced;
            tileGlow.pixelsPerUnitMultiplier = 64f / M(26);
            tileGlow.rectTransform.Fill(-26, -26, -26, -26);
            var tileBorder = Rounded(tileRoot, "Border", 30, White(0.6f));
            tileBorder.rectTransform.Fill();
            var tileFace = Rounded(tileBorder.transform, "Face", 27, Color.white);
            tileFace.rectTransform.Fill(3, 3, 3, 3);
            tile = Gradient(tileFace, Hex("#F0478E"), Hex("#A3134F"));
            InsetBottom(tileFace.transform, 27, new Color(0f, 0f, 0f, 0.25f));
            coin = Coin(tileRoot, "Coin", new Vector2(0.5f, 0.5f), Vector2.zero, 56).gameObject;
            icon = Image(tileRoot, "Icon", "icon_undo", Color.white);
            icon.rectTransform.sizeDelta = M(42, 42) * (27f / 24f);

            amount = Text(Row("Amount", 197, 48), "Label", DisplayFont, 44, Color.white);
            amount.rectTransform.Fill();
            amount.text = "+150";
            Shadow(amount, new Color(0f, 0f, 0f, 0.35f), -0.8f);

            var nameText = Text(Row("Name", 240, 22), "Label", HeavyFont, 16, Hex("#CFE6FF"), TextAlignmentOptions.Center, "wheel_prize_jackpot");
            nameText.rectTransform.Fill();
            name = nameText.GetComponent<LocalizedText>();

            var buttonRow = Row("Collect", 297, 56);
            buttonRow.sizeDelta = M(240, 56);
            collect = ColorButton(buttonRow, "Button", Hex("#B6FF6E"), Hex("#17A523"), 20, Hex("#0E6B17"), 5, out var collectFace);
            ((RectTransform)collect.transform).Fill();
            var collectLabel = Text(collectFace, "Label", DisplayFont, 22, Color.white, TextAlignmentOptions.Center, "wheel_collect");
            collectLabel.rectTransform.Fill();
            Shadow(collectLabel, Hex("#0E6B17"), -0.6f);

            layer.gameObject.SetActive(false);
            return group;
        }
    }
}
