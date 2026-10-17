using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

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

        const string MenuPrefab = "Assets/_Game/Prefabs/UI/Screens/MainMenuScreen.prefab";

        [MenuItem("BottleSort/UI/Lift Bottom Buttons Above Banner")]
        public static void Apply()
        {
            AddHeroFit();
            Lift("Assets/_Game/Scenes/Menu.unity", "MainMenuScreen/Content", new[] { "Levels", "Shop", "Play", "RoundButtons" }, "Hero", "Logo");
            Lift("Assets/_Game/Scenes/Game.unity", "GameScreen/Content", new[] { "Boosters" }, "Field", null);
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
