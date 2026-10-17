using System.Collections.Generic;
using System.IO;
using BottleSort.Gameplay;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static BottleSort.UI.Editor.UiKit;

namespace BottleSort.UI.Editor
{
    // Собирает интерфейс сцены Main по размерам из Docs/Mockup. Повторный запуск пересобирает всё,
    // что лежит под Canvas/SafeArea и Canvas/Backdrop; остальную сцену не трогает.
    // Из batchmode: -executeMethod BottleSort.UI.Editor.UiSceneBuilder.BuildFromCommandLine
    public static partial class UiSceneBuilder
    {
        const string MainScenePath = "Assets/_Game/Scenes/Main.unity";
        const string PrefabsDir = "Assets/_Game/Prefabs/UI";
        const string BottleStubPath = PrefabsDir + "/BottleStub.prefab";
        const string BottleAdapterPath = PrefabsDir + "/BottleViewAdapter.prefab";
        const string VfxBottlePath = "Assets/_Game/Art/Bottles/Bottle.prefab";

        static BottleStubView _bottlePrefab;
        // Бутылка для игрового поля: настоящая от vfx-art, если она есть, иначе заглушка.
        static MonoBehaviour _fieldBottlePrefab;

        public static void BuildFromCommandLine()
        {
            UiAssets.SetupAll();
            BuildMainScene();
        }

        [MenuItem("BottleSort/UI/3. Build Main Scene UI")]
        public static void BuildMainScene()
        {
            LoadFonts();
            Directory.CreateDirectory(PrefabsDir);
            Directory.CreateDirectory(PrefabsDir + "/Screens");

            var scene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            var canvas = GameObject.Find("Canvas");
            SetupScaler(canvas.GetComponent<CanvasScaler>());
            var safeArea = canvas.transform.Find("SafeArea");
            ClearOld(canvas.transform, safeArea);

            _bottlePrefab = BuildBottleStub();
            _fieldBottlePrefab = BuildBottleAdapter() ?? (MonoBehaviour)_bottlePrefab;
            var backdrop = BuildBackdrop(canvas.transform);
            backdrop.transform.SetAsFirstSibling();

            var screens = new List<UiScreen>
            {
                BuildSplash(safeArea),
                BuildMenu(safeArea),
                BuildLevels(safeArea),
                BuildGame(safeArea),
                BuildWin(safeArea),
                BuildShop(safeArea),
                BuildSettings(safeArea),
                BuildGift(safeArea),
                BuildRewardPopup(safeArea),
                BuildCalendar(safeArea),
                BuildWheel(safeArea)
            };

            var routerObject = new GameObject("ScreenRouter");
            var router = routerObject.AddComponent<ScreenRouter>();
            Set(router, "screens", screens);
            Set(router, "backdrop", backdrop);

            foreach (var screen in screens)
            {
                screen.gameObject.SetActive(true);
                string path = $"{PrefabsDir}/Screens/{screen.name}.prefab";
                PrefabUtility.SaveAsPrefabAssetAndConnect(screen.gameObject, path, InteractionMode.AutomatedAction);
            }
            // В редакторе видна только заставка, остальные экраны включает ScreenRouter.
            foreach (var screen in screens)
                screen.gameObject.SetActive(screen is SplashScreen);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("[UiSceneBuilder] Main scene UI built");
        }

        // Макет нарисован на 390 pt в ширину, в Canvas это 975 единиц (2.5 на pt). На вытянутых телефонах
        // ширина всегда ровно 390 pt, как в макете; на коротких экранах и планшетах Expand держит высоту
        // не меньше 693 pt, тогда добавляется ширина, а не съёживается шапка.
        static void SetupScaler(CanvasScaler scaler)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(M(390), M(693));
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.referencePixelsPerUnit = 100f;
        }

        static void ClearOld(Transform canvas, Transform safeArea)
        {
            for (int i = safeArea.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(safeArea.GetChild(i).gameObject);
            var oldBackdrop = canvas.Find("Backdrop");
            if (oldBackdrop != null)
                Object.DestroyImmediate(oldBackdrop.gameObject);
            foreach (var name in new[] { "ScreenRouter", "UiSounds" })
            {
                var old = GameObject.Find(name);
                if (old != null)
                    Object.DestroyImmediate(old);
            }
        }

        static ThemeBackdrop BuildBackdrop(Transform canvas)
        {
            var root = Node("Backdrop", canvas).Fill();
            var backdrop = root.gameObject.AddComponent<ThemeBackdrop>();

            var gradientRect = Node("Gradient", root).Fill();
            var gradient = gradientRect.gameObject.AddComponent<RawImage>();
            gradient.raycastTarget = false;

            // Ореол 520 px в точке 50% / 40%.
            var halo = Image(root, "Halo", "glow", White(0.3f));
            halo.rectTransform.anchorMin = halo.rectTransform.anchorMax = new Vector2(0.5f, 0.6f);
            halo.rectTransform.sizeDelta = M(520, 520);
            Motion(halo.gameObject, IdleMotion.Kind.Pulse, 9f, 0.08f);

            var random = new System.Random(7);
            var sparks = new List<Image>();
            for (int i = 0; i < 22; i++)
            {
                float size = 2f + random.Next(3);
                var spark = Image(root, "Spark " + i, "glow", Color.white);
                spark.rectTransform.anchorMin = spark.rectTransform.anchorMax =
                    new Vector2((float)random.NextDouble(), (float)random.NextDouble());
                spark.rectTransform.sizeDelta = M(size * 3f, size * 3f);
                Group(spark.gameObject, 0.4f, false);
                Motion(spark.gameObject, IdleMotion.Kind.Twinkle, 3f + (float)random.NextDouble() * 4f, 0f,
                    (float)random.NextDouble());
                sparks.Add(spark);
            }

            Set(backdrop, "gradient", gradient);
            Set(backdrop, "halo", halo);
            Set(backdrop, "sparks", sparks);

            var world = Object.FindFirstObjectByType<BackgroundView>();
            if (world == null)
            {
                world = new GameObject("Background").AddComponent<BackgroundView>();
                world.TargetCamera = Camera.main;
            }
            Set(backdrop, "worldBackground", world);
            return backdrop;
        }

        static T ScreenRoot<T>(Transform safeArea, string name, out RectTransform content) where T : UiScreen
        {
            var root = Node(name, safeArea).Fill();
            Group(root.gameObject);
            var screen = root.gameObject.AddComponent<T>();
            content = Node("Content", root).Fill();
            Set(screen, "transitionRoot", content);
            return screen;
        }

        // Квадратная кнопка 48×48 (домой, назад, настройки). В макете рамка 1.5 pt снаружи
        // (content-box), поэтому снаружи кнопка 51×51; размещать по IconButtonSize.
        const float IconButtonSize = 51f;

        static Button IconButton(Transform parent, string name, string icon, bool glass)
        {
            var root = Node(name, parent);
            root.sizeDelta = M(IconButtonSize, IconButtonSize);
            var face = Rounded(root, "Face", 16, glass ? Color.white : Hex("#0A1A50", 0.55f), true);
            face.rectTransform.Fill();
            if (glass)
                Gradient(face, White(0.2f), White(0.06f));
            Border(face.transform, 16, glass ? Hex("#DCEBFF", 0.4f) : Hex("#AAD2FF", 0.35f));
            var glyph = Image(face.transform, "Icon", "icon_" + icon, Color.white);
            glyph.rectTransform.sizeDelta = M(24, 24) * (27f / 24f);
            return Button(face.gameObject, root);
        }

        static void IconButtonShadow(Button button) =>
            SoftShadow((RectTransform)button.transform.parent, 6, 14, 0.3f);

        static Button BackButton(Transform parent) => IconButton(parent, "Back", "back", false);

        // Плашка с монетами: монета, число, по желанию зелёный «+».
        static RectTransform CoinPill(Transform parent, string name, float width, float height, bool glass, out TMP_Text label,
            bool plus = false)
        {
            var root = Node(name, parent);
            root.sizeDelta = M(width, height);
            var face = Rounded(root, "Face", height / 2f, glass ? Color.white : Hex("#0A1A50", 0.6f), true);
            face.rectTransform.Fill();
            if (glass)
                Gradient(face, White(0.2f), White(0.06f));
            Border(face.transform, height / 2f, Hex("#FFD66E", 0.6f));

            float coinSize = plus ? 26f : 28f;
            var coinGlow = Image(face.transform, "CoinGlow", "glow", Hex("#FFC83C", 0.5f));
            coinGlow.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(6f - 6f, 0f), new Vector2(coinSize + 12, coinSize + 12),
                new Vector2(0f, 0.5f));
            Coin(face.transform, "Coin", new Vector2(0f, 0.5f), new Vector2((plus ? 8f : 6f) + coinSize / 2f, 0f), coinSize);

            float right = plus ? 44f : 12f;
            label = Text(face.transform, "Amount", DisplayFont, plus ? 20 : 18, Color.white,
                plus ? TextAlignmentOptions.Center : TextAlignmentOptions.MidlineRight);
            label.rectTransform.Fill(plus ? 40 : 36, 0, right, 0);
            // Ширина плашки фиксированная, большие суммы мельчают, а не вылезают.
            label.enableAutoSizing = true;
            label.fontSizeMax = label.fontSize;
            label.fontSizeMin = M(11);
            label.text = "0";

            if (plus)
            {
                var add = Image(face.transform, "Plus", "circle", Color.white);
                add.rectTransform.Place(new Vector2(1f, 0.5f), new Vector2(-6f, 0f), new Vector2(30, 30), new Vector2(1f, 0.5f));
                Gradient(add, Hex("#9BFF4F"), Hex("#17A523"));
                var cross = Image(add.transform, "Icon", "icon_plus", Color.white);
                cross.rectTransform.sizeDelta = M(16, 16);
            }
            return root;
        }

        // Цветная объёмная кнопка из макета: градиент, светлая обводка и «дно» снизу.
        static Button ColorButton(Transform parent, string name, Color top, Color bottom, float radius, Color shadow,
            float depth, out RectTransform face)
        {
            var root = Node(name, parent);
            if (depth > 0f)
            {
                var under = Rounded(root, "Depth", radius, shadow);
                under.rectTransform.Fill(0, depth, 0, -depth);
            }
            var faceImage = Rounded(root, "Face", radius, Color.white, true);
            faceImage.rectTransform.Fill();
            Gradient(faceImage, top, bottom);
            Border(faceImage.transform, radius, White(0.55f));
            face = faceImage.rectTransform;
            var button = Button(root.gameObject);
            return button;
        }

        static TextMeshProUGUI IconLabel(RectTransform face, string icon, float iconSize, string key, float fontSize, float gap,
            out Image iconImage, bool iconFirst = true)
        {
            var row = Node("Content", face).Fill();
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = M(gap);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;

            iconImage = Image(row, "Icon", icon, Color.white);
            var iconElement = iconImage.gameObject.AddComponent<LayoutElement>();
            iconElement.preferredWidth = iconElement.preferredHeight = M(iconSize) * (27f / 24f);
            var label = Text(row, "Label", DisplayFont, fontSize, Color.white, TextAlignmentOptions.Center, key);
            if (!iconFirst)
                iconImage.transform.SetAsLastSibling();
            return label;
        }

        // Стеклянная полка на uGUI (заставка, меню): box-shadow 0 12px 22px rgba(0,0,0,.35)
        // и 0 0 22px rgba(140,200,255,.25) под ней. unit — сколько единиц Canvas в pt.
        static RectTransform Shelf(Transform parent, string name, float width, float unit = K)
        {
            var root = Node(name, parent);
            root.sizeDelta = new Vector2(width, 12f) * unit;
            float blur = 22f * unit;
            // Полка тоньше размытия, поэтому у CSS-тени пик заметно ниже заявленной альфы: берём примерно половину.
            ShelfHalo(root, "Shadow", new Color(0f, 0f, 0f, 0.1f), blur, 12f * unit);
            ShelfHalo(root, "Glow", Hex("#8CC8FF", 0.18f), blur, 0f);
            var bar = Image(root, "Bar", "shelf", Color.white);
            bar.type = UnityEngine.UI.Image.Type.Sliced;
            bar.pixelsPerUnitMultiplier = 16f / (6f * unit);
            bar.rectTransform.anchorMin = Vector2.zero;
            bar.rectTransform.anchorMax = Vector2.one;
            bar.rectTransform.offsetMin = bar.rectTransform.offsetMax = Vector2.zero;
            return root;
        }

        static void ShelfHalo(RectTransform shelf, string name, Color color, float blur, float offsetY)
        {
            var halo = Image(shelf, name, "shadow", color);
            halo.type = UnityEngine.UI.Image.Type.Sliced;
            halo.pixelsPerUnitMultiplier = 64f / blur;
            halo.rectTransform.anchorMin = Vector2.zero;
            halo.rectTransform.anchorMax = Vector2.one;
            halo.rectTransform.offsetMin = new Vector2(-blur, -blur - offsetY);
            halo.rectTransform.offsetMax = new Vector2(blur, blur - offsetY);
        }

        static Toast BuildToast(Transform parent, float top)
        {
            var root = Node("Toast", parent).TopBand(top, 46);
            Group(root.gameObject, 0f, false);
            var toast = root.gameObject.AddComponent<Toast>();

            var body = Rounded(root, "Body", 14, Color.white);
            body.rectTransform.anchorMin = body.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            body.rectTransform.pivot = new Vector2(0.5f, 1f);
            Gradient(body, Hex("#1D64E8"), Hex("#087BDE"));
            Border(body.transform, 14, White(0.5f));
            var layout = body.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset((int)M(16), (int)M(16), (int)M(9), (int)M(9));
            layout.childControlWidth = layout.childControlHeight = true;
            var fitter = body.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var label = Text(body.transform, "Label", HeavyFont, 14, Color.white);
            label.text = "Toast";
            // Обводка не должна участвовать в раскладке.
            body.transform.Find("Border").gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

            Set(toast, "label", label);
            Set(toast, "body", body.rectTransform);
            return toast;
        }

        static Button Dimmer(Transform parent)
        {
            var dim = Image(parent, "Dimmer", null, Hex("#03081E", 0.62f), true);
            dim.rectTransform.anchorMin = Vector2.zero;
            dim.rectTransform.anchorMax = Vector2.one;
            // Затемнение шире safe area, чтобы закрыть и вырез, и полосу жестов.
            dim.rectTransform.offsetMin = new Vector2(-400f, -400f);
            dim.rectTransform.offsetMax = new Vector2(400f, 400f);
            var button = dim.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            return button;
        }

        static Image BluePanel(Transform parent, string name, float radius)
        {
            var panel = Rounded(parent, name, radius, Color.white, true);
            Gradient(panel, Hex("#123C87"), Hex("#061B42"));
            Border(panel.transform, radius, Hex("#78C8FF", 0.6f));
            return panel;
        }

        // Текстуры vfx-art покрывают область viewBox от -30 до 130 по x и от -30 до 270 по y,
        // в долях коробки бутылки 100×240 это якоря ниже.
        static readonly Vector2 ArtMin = new Vector2(-0.3f, -0.125f);
        static readonly Vector2 ArtMax = new Vector2(1.3f, 1.125f);

        static RectTransform FillArtArea(RectTransform rect)
        {
            rect.anchorMin = ArtMin;
            rect.anchorMax = ArtMax;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        static BottleStubView BuildBottleStub()
        {
            var root = Node("BottleStub", null);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0f);
            root.pivot = new Vector2(0.5f, 0f);
            root.sizeDelta = new Vector2(M(70), M(168));
            var group = Group(root.gameObject, 1f, false);
            var view = root.gameObject.AddComponent<BottleStubView>();

            var glow = Image(root, "Glow", null, Color.white);
            FillArtArea(glow.rectTransform);
            glow.gameObject.SetActive(false);
            var back = Node("Back", root);
            FillArtArea(back);
            var backImage = back.gameObject.AddComponent<RawImage>();
            backImage.raycastTarget = false;

            var mask = Image(root, "Liquid", null, Color.white);
            FillArtArea(mask.rectTransform);
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            // Внутри маски координаты снова коробки 100×240 по высоте, а по ширине — вся текстура:
            // слои шире бутылки, лишнее срезает маска (как прямоугольники 400×h в Bottle.dc.html).
            var layers = Node("Layers", mask.transform);
            layers.anchorMin = new Vector2(0f, 0.125f / 1.25f);
            layers.anchorMax = new Vector2(1f, 1.125f / 1.25f);
            layers.offsetMin = layers.offsetMax = Vector2.zero;

            var shade = Image(layers, "Shade", "liquid_shade", Color.white);
            shade.rectTransform.anchorMin = new Vector2((0.3f + 0.16f) / 1.6f, 0f);
            shade.rectTransform.anchorMax = new Vector2((0.3f + 0.84f) / 1.6f, 0f);
            shade.rectTransform.pivot = new Vector2(0.5f, 0f);
            shade.rectTransform.sizeDelta = Vector2.zero;

            var layer = Image(layers, "LayerTemplate", null, Color.white);
            layer.rectTransform.anchorMin = new Vector2(0f, 0f);
            layer.rectTransform.anchorMax = new Vector2(1f, 0f);
            layer.rectTransform.pivot = new Vector2(0.5f, 0f);
            Gradient(layer, Color.white, Color.white);
            layer.gameObject.SetActive(false);

            var hidden = Text(layers, "HiddenTemplate", DisplayFont, 16, White(0.85f));
            hidden.text = "?";
            hidden.rectTransform.anchorMin = hidden.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            hidden.rectTransform.sizeDelta = M(40, 30);
            hidden.gameObject.SetActive(false);

            var front = Node("Front", root);
            FillArtArea(front);
            var frontImage = front.gameObject.AddComponent<RawImage>();
            frontImage.raycastTarget = false;

            var frozenTint = Image(root, "Ice", null, Hex("#D8F4FF", 0.55f));
            FillArtArea(frozenTint.rectTransform);
            frozenTint.gameObject.SetActive(false);

            // Значки льда и замка на высоте 150 из 240, как в Bottle.dc.html.
            var frozenBadge = Image(root, "FrozenBadge", "circle", Color.white);
            frozenBadge.rectTransform.anchorMin = frozenBadge.rectTransform.anchorMax = new Vector2(0.5f, 90f / 240f);
            frozenBadge.rectTransform.sizeDelta = M(26, 26);
            var frozenFill = Image(frozenBadge.transform, "Fill", "circle", Hex("#2FA8E8"));
            frozenFill.rectTransform.anchorMin = new Vector2(0.09f, 0.09f);
            frozenFill.rectTransform.anchorMax = new Vector2(0.91f, 0.91f);
            frozenFill.rectTransform.offsetMin = frozenFill.rectTransform.offsetMax = Vector2.zero;
            var frozenCount = Text(frozenBadge.transform, "Count", DisplayFont, 15, Color.white);
            frozenCount.rectTransform.Fill();
            frozenCount.text = "3";
            frozenBadge.gameObject.SetActive(false);

            var lockBadge = Image(root, "LockBadge", "circle", Hex("#D9A93A"));
            lockBadge.rectTransform.anchorMin = lockBadge.rectTransform.anchorMax = new Vector2(0.5f, 90f / 240f);
            lockBadge.rectTransform.sizeDelta = M(30, 30);
            var lockIcon = Image(lockBadge.transform, "Icon", "icon_lock", Hex("#4A3205"));
            lockIcon.rectTransform.anchorMin = new Vector2(0.2f, 0.2f);
            lockIcon.rectTransform.anchorMax = new Vector2(0.8f, 0.8f);
            lockIcon.rectTransform.offsetMin = lockIcon.rectTransform.offsetMax = Vector2.zero;
            lockBadge.gameObject.SetActive(false);

            FillShapes(view);
            Set(view, "glow", glow);
            Set(view, "back", backImage);
            Set(view, "front", frontImage);
            Set(view, "mask", mask);
            Set(view, "layers", layers);
            Set(view, "shade", shade.rectTransform);
            Set(view, "layerTemplate", layer);
            Set(view, "hiddenTemplate", hidden);
            Set(view, "frozenBadge", frozenBadge.gameObject);
            Set(view, "frozenCount", frozenCount);
            Set(view, "frozenTint", frozenTint);
            Set(view, "lockBadge", lockBadge.gameObject);
            Set(view, "group", group);

            var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, BottleStubPath);
            Object.DestroyImmediate(root.gameObject);
            return prefab.GetComponent<BottleStubView>();
        }

        // Формы берём из VfxArt: те же текстуры стекла и те же границы жидкости, что у BottleView на поле.
        static void FillShapes(BottleStubView view)
        {
            var art = VfxArt.Default;
            var so = new SerializedObject(view);
            var list = so.FindProperty("shapes");
            list.arraySize = art.shapes.Length;
            for (int i = 0; i < art.shapes.Length; i++)
            {
                var shape = art.shapes[i];
                var element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("id").stringValue = shape.id;
                element.FindPropertyRelative("back").objectReferenceValue = shape.back;
                element.FindPropertyRelative("front").objectReferenceValue = shape.front;
                element.FindPropertyRelative("liquid").objectReferenceValue = Sprite("Bottles/" + shape.id + "_liquid");
                element.FindPropertyRelative("glow").objectReferenceValue = Sprite("Bottles/" + shape.id + "_glow");
                element.FindPropertyRelative("liquidTop").floatValue = shape.liquidTop;
                element.FindPropertyRelative("liquidBottom").floatValue = shape.liquidBottom;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static BottleViewAdapter BuildBottleAdapter()
        {
            var bottle = AssetDatabase.LoadAssetAtPath<BottleView>(VfxBottlePath);
            if (bottle == null)
                return null;
            var root = Node("BottleViewAdapter", null);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0f);
            root.pivot = new Vector2(0.5f, 0f);
            root.sizeDelta = new Vector2(70, 168);
            var adapter = root.gameObject.AddComponent<BottleViewAdapter>();
            Set(adapter, "prefab", bottle);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, BottleAdapterPath);
            Object.DestroyImmediate(root.gameObject);
            return prefab.GetComponent<BottleViewAdapter>();
        }

        // Бутылка-заглушка внутри экрана (меню, заставка, магазин): ширина size, высота size × 2.4.
        static BottleStubView PlaceBottle(Transform parent, string name, float size)
        {
            var view = (BottleStubView)PrefabUtility.InstantiatePrefab(_bottlePrefab, parent);
            view.name = name;
            var rect = (RectTransform)view.transform;
            rect.sizeDelta = M(size, size * 2.4f);
            return view;
        }
    }
}
