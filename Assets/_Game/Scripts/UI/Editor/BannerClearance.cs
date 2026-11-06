using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace BottleSort.UI.Editor
{
    // Нижний блок кнопок меню и бустеры в игре — над местом под баннер (71 pt от низа safe area, как в макете).
    // Сдвигает весь блок целиком: масштабы и расстояния между кнопками, выставленные руками, не меняются;
    // тени кнопок, что лежат соседями «<Имя> Shadow», едут вместе с кнопками. Если блок наезжает на поле, оно
    // ужимается снизу; полка с бутылками в меню встаёт ровно между логотипом и кнопками и сама уменьшается
    // (FitInParent), если там тесно. Повторный запуск ничего не сдвигает, если всё уже на месте.
    // Из batchmode: -executeMethod BottleSort.UI.Editor.BannerClearance.Apply
    public static class BannerClearance
    {
        const float Pt = 2.5f;
        const float BannerSpace = 71f;
        const float AreaGap = 8f;

        const string ScreensDir = "Assets/_Game/Prefabs/UI/Screens";
        const string MenuPrefab = ScreensDir + "/MainMenuScreen.prefab";

        // Победа: ссылка «В главное меню» стоит так, чтобы её текст был на высоте нижних кнопок меню (71 pt).
        const float WinMenuBottom = 60f;
        const float WinHeadGap = 10f;
        static readonly string[] WinLifted = { "Menu", "Next", "Next Shadow" };
        static readonly string[] WinHead = { "Rays", "Glow", "Confetti", "Ribbon", "Stars", "LevelDone", "Chips", "Reward" };
        static readonly string[] WinHeadBounds = { "Ribbon", "Stars", "LevelDone", "Chips", "Reward" };

        // Колесо: всё от крестика до кнопки «Крутить» — между верхом safe area и баннером.
        const float WheelBannerSpace = 56f;
        const float WheelMargin = 6f;
        static readonly string[] WheelBounds = { "Close", "Ribbon", "Wheel", "Spins", "SpinButton", "AdButton" };

        const float BannerHeight = 50f;

        [MenuItem("BottleSort/UI/Lift Bottom Buttons Above Banner")]
        public static void Apply()
        {
            AddHeroFit();
            Lift("Assets/_Game/Scenes/Menu.unity", "MainMenuScreen/Content", new[] { "Levels", "Shop", "Play", "RoundButtons" }, "Hero", "Logo");
            Lift("Assets/_Game/Scenes/Game.unity", "GameScreen/Content", new[] { "Boosters" }, "Field", null);
        }

        // Баннер виден и поверх победы, колеса и магазина. Точечно по префабам, повторный запуск ничего не меняет.
        // Из batchmode: -executeMethod BottleSort.UI.Editor.BannerClearance.ApplyToScreens
        [MenuItem("BottleSort/UI/Fit Win, Wheel and Shop Above Banner")]
        public static void ApplyToScreens()
        {
            PatchPrefab("WinScreen", FitWin);
            PatchPrefab("WheelPopup", FitWheel);
            PatchPrefab("ShopScreen", PadShop);
            AssetDatabase.SaveAssets();
        }

        static void PatchPrefab(string name, System.Action<Transform> apply)
        {
            string path = $"{ScreensDir}/{name}.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            apply(root.transform);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        // «Дальше» и «В главное меню» поднимаются над баннером. Всё, что выше (лента, звёзды, панель награды),
        // переезжает в Head/Block: на коротких экранах вроде iPhone 8 блок равномерно уменьшается, чтобы панель
        // награды не легла на «Дальше»; на длинных он остаётся как был.
        public static void FitWin(Transform screen)
        {
            var content = (RectTransform)screen.Find("Content");
            if (content.Find("Head") != null)
                return;
            var menu = (RectTransform)content.Find("Menu");
            float lift = WinMenuBottom * Pt - Bottom(menu);
            foreach (string name in WinLifted)
                if (content.Find(name) is RectTransform item)
                    item.anchoredPosition += new Vector2(0f, lift);

            float headBottom = Top((RectTransform)content.Find("Next")) + WinHeadGap * Pt;
            var head = UiKit.Node("Head", content);
            head.anchorMin = Vector2.zero;
            head.anchorMax = Vector2.one;
            head.offsetMin = new Vector2(0f, headBottom);
            head.offsetMax = Vector2.zero;
            head.SetSiblingIndex(content.Find(WinHead[0]).GetSiblingIndex());

            // Блок — та же верхняя полоса экрана, поэтому привязанные к верху элементы встают на прежние места.
            var block = UiKit.Node("Block", head);
            block.anchorMin = new Vector2(0f, 1f);
            block.anchorMax = Vector2.one;
            block.pivot = new Vector2(0.5f, 1f);
            block.anchoredPosition = Vector2.zero;
            float height = 0f;
            foreach (string name in WinHeadBounds)
            {
                var item = (RectTransform)content.Find(name);
                height = Mathf.Max(height, item.rect.height * item.pivot.y * item.localScale.y - item.anchoredPosition.y);
            }
            block.sizeDelta = new Vector2(0f, height);
            foreach (string name in WinHead)
                content.Find(name).SetParent(block, false);
            AddFit(head, block);
            Debug.Log($"[BannerClearance] win: buttons lifted by {lift / Pt:0.0} pt, head {height / Pt:0.0} pt above {headBottom / Pt:0.0} pt");
        }

        // Сцена колеса 390×844 pt из макета не помещается на короткий экран: её часть от крестика до кнопки «Крутить»
        // ставится по центру между верхом safe area и баннером и уменьшается, если там тесно (Fit/Frame).
        public static void FitWheel(Transform screen)
        {
            var content = (RectTransform)screen.Find("Content");
            if (content.Find("Fit") != null)
                return;
            var stage = (RectTransform)content.Find("Stage");
            var band = VisibleBounds(stage, WheelBounds);
            float top = band.yMax + WheelMargin * Pt;
            float bottom = band.yMin - WheelMargin * Pt;

            var fit = UiKit.Node("Fit", content);
            fit.anchorMin = Vector2.zero;
            fit.anchorMax = Vector2.one;
            fit.offsetMin = new Vector2(0f, WheelBannerSpace * Pt);
            fit.offsetMax = Vector2.zero;
            fit.SetSiblingIndex(stage.GetSiblingIndex());
            var frame = UiKit.Node("Frame", fit);
            frame.anchorMin = frame.anchorMax = new Vector2(0.5f, 0.5f);
            frame.sizeDelta = new Vector2(stage.rect.width, top - bottom);
            frame.anchoredPosition = Vector2.zero;
            stage.SetParent(frame, false);
            stage.anchorMin = stage.anchorMax = new Vector2(0.5f, 0.5f);
            stage.anchoredPosition = new Vector2(0f, -(top + bottom) / 2f);
            AddFit(fit, frame);
            Debug.Log($"[BannerClearance] wheel: {(top - bottom) / Pt:0.0} pt of the stage fitted above {WheelBannerSpace} pt");
        }

        // Последний ряд карточек докручивается выше баннера.
        public static void PadShop(Transform screen)
        {
            var grid = screen.GetComponentInChildren<GridLayoutGroup>(true);
            int bottom = grid.padding.top + Mathf.RoundToInt(BannerHeight * Pt);
            if (grid.padding.bottom < bottom)
                grid.padding.bottom = bottom;
            EditorUtility.SetDirty(grid);
        }

        static void AddFit(RectTransform area, RectTransform content)
        {
            var fit = area.gameObject.AddComponent<FitInParent>();
            var so = new SerializedObject(fit);
            so.FindProperty("content").objectReferenceValue = content;
            so.ApplyModifiedPropertiesWithoutUndo();
            content.localScale = Vector3.one;
        }

        // Границы видимой графики в координатах parent, без мягких теней и ореолов (как в BannerCoverageCapture).
        static Rect VisibleBounds(RectTransform parent, string[] names)
        {
            var corners = new Vector3[4];
            var bounds = new Rect();
            bool any = false;
            foreach (string name in names)
            {
                var item = parent.Find(name);
                if (item == null)
                    continue;
                foreach (var graphic in item.GetComponentsInChildren<Graphic>(true))
                {
                    if (graphic.name.EndsWith("Shadow") || graphic.name == "Pulse" || graphic.name.StartsWith("Glow"))
                        continue;
                    graphic.rectTransform.GetWorldCorners(corners);
                    Vector2 min = parent.InverseTransformPoint(corners[0]);
                    Vector2 max = parent.InverseTransformPoint(corners[2]);
                    var rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
                    bounds = any ? Rect.MinMaxRect(Mathf.Min(bounds.xMin, rect.xMin), Mathf.Min(bounds.yMin, rect.yMin),
                        Mathf.Max(bounds.xMax, rect.xMax), Mathf.Max(bounds.yMax, rect.yMax)) : rect;
                    any = true;
                }
            }
            return bounds;
        }

        static void AddHeroFit()
        {
            var root = PrefabUtility.LoadPrefabContents(MenuPrefab);
            var hero = root.transform.Find("Content/Hero");
            if (hero.GetComponent<FitInParent>() == null)
            {
                var fit = hero.gameObject.AddComponent<FitInParent>();
                var so = new SerializedObject(fit);
                so.FindProperty("content").objectReferenceValue = hero.Find("Row");
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, MenuPrefab);
            }
            PrefabUtility.UnloadPrefabContents(root);
        }

        // topElement — элемент, привязанный к верху, под которым начинается область (логотип над полкой в меню).
        static void Lift(string scenePath, string contentPath, string[] block, string areaAbove, string topElement)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var content = FindInScene(scene, contentPath);
            var items = new List<RectTransform>();
            foreach (string name in block)
                items.Add((RectTransform)content.Find(name));

            float lowest = float.MaxValue;
            foreach (var item in items)
                lowest = Mathf.Min(lowest, VisibleBottom(item));
            float delta = BannerSpace * Pt - lowest;
            Debug.Log($"[BannerClearance] {scene.name}: lowest button at {lowest / Pt:0.0} pt, lifting by {delta / Pt:0.0} pt");

            float top = float.MinValue;
            foreach (var item in items)
            {
                var shadow = content.Find(item.name + " Shadow") as RectTransform;
                MoveWithShadow(item, shadow, delta);
                top = Mathf.Max(top, Top(item));
            }

            // Поле растянуто по экрану: если блок до него дорос, поднимаем его нижний край.
            // Полка в меню занимает всё между логотипом и кнопками.
            var area = (RectTransform)content.Find(areaAbove);
            float need = top + AreaGap * Pt;
            Undo.RecordObject(area, "Lift above banner");
            if (topElement != null)
            {
                var logo = (RectTransform)content.Find(topElement);
                // Низ логотипа, считая от верха: он привязан к верху, масштаб — вокруг pivot.
                float logoBottom = -logo.anchoredPosition.y + logo.rect.height * logo.pivot.y * logo.localScale.y;
                area.offsetMin = new Vector2(area.offsetMin.x, need);
                area.offsetMax = new Vector2(area.offsetMax.x, -(logoBottom + AreaGap * Pt));
                Debug.Log($"[BannerClearance] {scene.name}: {areaAbove} from {need / Pt:0.0} pt above the bottom to {(logoBottom + AreaGap * Pt) / Pt:0.0} pt below the top");
            }
            else if (area.offsetMin.y < need)
            {
                Debug.Log($"[BannerClearance] {scene.name}: {areaAbove} bottom {area.offsetMin.y / Pt:0.0} -> {need / Pt:0.0} pt");
                area.offsetMin = new Vector2(area.offsetMin.x, need);
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(area);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static Transform FindInScene(UnityEngine.SceneManagement.Scene scene, string path)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = root.transform.Find("SafeArea/" + path);
                if (found != null)
                    return found;
            }
            throw new System.InvalidOperationException($"{path} not found in {scene.name}");
        }

        // Низ элемента вместе с тем, что торчит из него вниз («дно» цветной кнопки), в единицах от низа родителя.
        static float VisibleBottom(RectTransform item)
        {
            float bottom = Bottom(item);
            float below = 0f;
            foreach (RectTransform child in item)
                if (!child.name.EndsWith("Shadow") && child.gameObject.activeSelf)
                    below = Mathf.Min(below, child.anchorMin.y * item.rect.height + child.offsetMin.y);
            return bottom + below * item.localScale.y;
        }

        // Все элементы блока привязаны к низу (anchor y = 0), масштаб — вокруг pivot.
        static float Bottom(RectTransform item) =>
            item.offsetMin.y + item.pivot.y * item.rect.height * (1f - item.localScale.y);

        static float Top(RectTransform item) => Bottom(item) + item.rect.height * item.localScale.y;

        static void MoveWithShadow(RectTransform item, RectTransform shadow, float delta)
        {
            Undo.RecordObject(item, "Lift above banner");
            item.anchoredPosition += new Vector2(0f, delta);
            PrefabUtility.RecordPrefabInstancePropertyModifications(item);
            if (shadow == null)
                return;

            // Тень строилась вокруг кнопки в префабе; повторяем её положение относительно кнопки с тем же масштабом.
            var itemSource = PrefabUtility.GetCorrespondingObjectFromSource(item);
            var shadowSource = PrefabUtility.GetCorrespondingObjectFromSource(shadow);
            if (itemSource == null || shadowSource == null)
                return;
            float scale = item.localScale.y;
            Undo.RecordObject(shadow, "Lift above banner");
            shadow.localScale = item.localScale;
            shadow.anchoredPosition = item.anchoredPosition + (shadowSource.anchoredPosition - itemSource.anchoredPosition) * scale;
            PrefabUtility.RecordPrefabInstancePropertyModifications(shadow);
        }
    }
}
