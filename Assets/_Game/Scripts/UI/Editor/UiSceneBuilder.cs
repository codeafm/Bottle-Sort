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

        // Стеклянная квадратная кнопка 48×48 (домой, назад, настройки).
        static Button IconButton(Transform parent, string name, string icon, bool glass)
        {
            var root = Node(name, parent);
            root.sizeDelta = M(48, 48);
            var face = Rounded(root, "Face", 16, glass ? Color.white : Hex("#0A1A50", 0.55f), true);
            face.rectTransform.Fill();
            if (glass)
                Gradient(face, White(0.2f), White(0.06f));
            Border(face.transform, 16, glass ? Hex("#DCEBFF", 0.4f) : Hex("#AAD2FF", 0.35f));
            var glyph = Image(face.transform, "Icon", "icon_" + icon, Color.white);
            glyph.rectTransform.sizeDelta = M(26, 26);
            return Button(face.gameObject, root);
        }

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

            var coin = Image(face.transform, "Coin", "coin", Color.white);
            coin.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(28, 28), new Vector2(0f, 0.5f));

            float right = plus ? 40f : 12f;
            label = Text(face.transform, "Amount", DisplayFont, height >= 44 ? 20 : 18, Color.white, TextAlignmentOptions.MidlineRight);
            label.rectTransform.Fill(36, 0, right, 0);
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

        static RectTransform Shelf(Transform parent, string name, float width)
        {
            var shelf = Image(parent, name, "shelf", Color.white);
            shelf.type = UnityEngine.UI.Image.Type.Sliced;
            shelf.pixelsPerUnitMultiplier = 16f / M(6f);
            shelf.rectTransform.sizeDelta = M(width, 12);
            var glow = Image(shelf.transform, "Glow", "glow", Hex("#8CC8FF", 0.25f));
            glow.rectTransform.Fill(-20, -10, -20, -26);
            glow.transform.SetAsFirstSibling();
            return shelf.rectTransform;
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

        static BottleStubView BuildBottleStub()
        {
            var root = Node("BottleStub", null);
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0f);
            root.pivot = new Vector2(0.5f, 0f);
            root.sizeDelta = new Vector2(M(70), M(168));
            var group = Group(root.gameObject, 1f, false);
            var view = root.gameObject.AddComponent<BottleStubView>();

            var glow = Image(root, "Glow", "bottle_classic_glow", Color.white);
            glow.rectTransform.Fill();
            glow.gameObject.SetActive(false);
            var back = Image(root, "Back", "bottle_classic_back", Color.white);
            back.rectTransform.Fill();

            var mask = Image(root, "Liquid", "bottle_classic_mask", Color.white);
            mask.rectTransform.Fill();
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var layers = Node("Layers", mask.transform).Fill();

            var shade = Image(layers, "Shade", "liquid_shade", Color.white);
            shade.rectTransform.anchorMin = new Vector2(0f, 0f);
            shade.rectTransform.anchorMax = new Vector2(1f, 0f);
            shade.rectTransform.pivot = new Vector2(0.5f, 0f);

            var layer = Image(layers, "LayerTemplate", null, Color.white);
            layer.rectTransform.anchorMin = new Vector2(0f, 0f);
            layer.rectTransform.anchorMax = new Vector2(1f, 0f);
            layer.rectTransform.pivot = new Vector2(0.5f, 0f);
            layer.gameObject.SetActive(false);

            var hidden = Text(layers, "HiddenTemplate", DisplayFont, 16, White(0.85f));
            hidden.text = "?";
            hidden.rectTransform.anchorMin = hidden.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            hidden.rectTransform.sizeDelta = M(40, 30);
            hidden.gameObject.SetActive(false);

            var front = Image(root, "Front", "bottle_classic_front", Color.white);
            front.rectTransform.Fill();

            var frozenTint = Image(root, "Ice", "bottle_classic_mask", Hex("#D8F4FF", 0.55f));
            frozenTint.rectTransform.Fill();
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

            Set(view, "classic.back", Sprite("bottle_classic_back"));
            Set(view, "classic.front", Sprite("bottle_classic_front"));
            Set(view, "classic.mask", Sprite("bottle_classic_mask"));
            Set(view, "classic.glow", Sprite("bottle_classic_glow"));
            Set(view, "classic.liquidTop", 66f);
            Set(view, "classic.liquidBottom", 226f);
            Set(view, "cylinder.back", Sprite("bottle_cylinder_back"));
            Set(view, "cylinder.front", Sprite("bottle_cylinder_front"));
            Set(view, "cylinder.mask", Sprite("bottle_cylinder_mask"));
            Set(view, "cylinder.glow", Sprite("bottle_cylinder_glow"));
            Set(view, "cylinder.liquidTop", 36f);
            Set(view, "cylinder.liquidBottom", 212f);
            Set(view, "glow", glow);
            Set(view, "back", back);
            Set(view, "front", front);
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
