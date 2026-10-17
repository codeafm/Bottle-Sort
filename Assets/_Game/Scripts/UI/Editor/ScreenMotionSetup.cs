using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static BottleSort.UI.Editor.UiKit;

namespace BottleSort.UI.Editor
{
    // Уход заставки, появление меню и растворение между сценами. Вызывается из UiSceneBuilder при сборке,
    // а для уже собранных (и, возможно, поправленных руками) префабов — точечно через ApplyToPrefabs,
    // без пересборки сцен. Из batchmode: -executeMethod BottleSort.UI.Editor.ScreenMotionSetup.ApplyToPrefabs
    public static class ScreenMotionSetup
    {
        const string ScreensDir = "Assets/_Game/Prefabs/UI/Screens";
        const string LoaderPath = "Assets/_Game/Prefabs/UI/Resources/SceneLoader.prefab";

        // Подпись студии внизу заставки убрана по просьбе заказчика (интро CODEAFM осталось):
        // -executeMethod BottleSort.UI.Editor.ScreenMotionSetup.RemoveSplashStudioLabel
        public static void RemoveSplashStudioLabel()
        {
            string path = $"{ScreensDir}/SplashScreen.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            var studio = root.transform.Find("Content/Studio");
            if (studio != null)
                Object.DestroyImmediate(studio.gameObject);
            // Шаг ухода заставки, который гасил эту подпись, остался бы с пустой ссылкой.
            var outro = root.transform.Find("Outro").GetComponent<ScreenMotion>();
            var so = new SerializedObject(outro);
            var steps = so.FindProperty("steps");
            for (int i = steps.arraySize - 1; i >= 0; i--)
                if (steps.GetArrayElementAtIndex(i).FindPropertyRelative("target").objectReferenceValue == null)
                    steps.DeleteArrayElementAtIndex(i);
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
            Debug.Log("[ScreenMotionSetup] studio label removed from the splash");
        }

        public static void ApplyToPrefabs()
        {
            Patch<SplashScreen>($"{ScreensDir}/SplashScreen.prefab", SplashOutro);
            Patch<MainMenuScreen>($"{ScreensDir}/MainMenuScreen.prefab", MenuIntro);
            Patch<SceneLoader>(LoaderPath, Dissolve);
            AssetDatabase.SaveAssets();
            Debug.Log("[ScreenMotionSetup] prefabs updated");
        }

        static void Patch<T>(string path, System.Action<T> apply) where T : Component
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            apply(root.GetComponent<T>());
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        // Бутылки и полка уходят вниз и гаснут, логотип поднимается, растёт до 1.08 и растворяется,
        // подписи и полоса гаснут; фон остаётся. Всё вместе около 0.7 с.
        public static void SplashOutro(SplashScreen screen)
        {
            var steps = new List<ScreenMotion.Step>();
            var stage = screen.transform.Find("Content/Stage");
            Add(steps, stage, "Pourer", new Vector2(0, -46), 0.96f, 0f, 0.6f);
            Add(steps, stage, "Target", new Vector2(0, -46), 0.96f, 0.04f, 0.6f);
            Add(steps, stage, "Sparks", new Vector2(0, -46), 1f, 0.04f, 0.6f);
            Add(steps, stage, "Shelf", new Vector2(0, -36), 1f, 0.08f, 0.6f);
            Add(steps, stage, "LogoRoot", new Vector2(0, 18), 1.08f, 0.1f, 0.6f);
            Add(steps, stage, "Caption", Vector2.zero, 1f, 0f, 0.45f);
            Add(steps, stage, "Progress", Vector2.zero, 1f, 0.05f, 0.45f);
            Add(steps, stage, "Loading", Vector2.zero, 1f, 0.05f, 0.45f);

            var motion = MotionOn(screen.gameObject, "Outro", false, steps);
            Set(screen, "outro", motion);
            Set(screen, "barShine", stage.Find("Progress/Shine").GetComponent<IdleMotion>());
        }

        // Логотип опускается с пружинкой, бутылки поднимаются по очереди, кнопки въезжают снизу одна за другой,
        // шапка — сверху. Около 0.8 с; после игры то же вдвое быстрее.
        public static void MenuIntro(MainMenuScreen screen)
        {
            var steps = new List<ScreenMotion.Step>();
            var content = screen.transform.Find("Content");
            Add(steps, content, "Settings", new Vector2(0, 34), 1f, 0.05f, 0.4f);
            Add(steps, content, "Coins", new Vector2(0, 34), 1f, 0.08f, 0.4f);
            Add(steps, content, "Logo", new Vector2(0, 46), 0.94f, 0f, 0.6f, true);
            var row = content.Find("Hero/Row");
            for (int i = 1; i <= 5; i++)
                Add(steps, row, "Bottle " + i, new Vector2(0, -34), 1f, 0.12f + 0.05f * (i - 1), 0.4f, true);
            Add(steps, row, "Shelf", Vector2.zero, 1f, 0.1f, 0.35f);
            var round = content.Find("RoundButtons");
            Add(steps, round, "Gift", new Vector2(0, -44), 1f, 0.24f, 0.4f, true);
            Add(steps, round, "Bonus", new Vector2(0, -44), 1f, 0.28f, 0.4f, true);
            Add(steps, round, "Luck", new Vector2(0, -44), 1f, 0.32f, 0.4f, true);
            Add(steps, content, "Play", new Vector2(0, -56), 1f, 0.34f, 0.42f, true);
            Add(steps, content, "Levels", new Vector2(0, -56), 1f, 0.4f, 0.4f);
            Add(steps, content, "Shop", new Vector2(0, -56), 1f, 0.44f, 0.4f);

            var motion = MotionOn(screen.gameObject, "Intro", true, steps);
            Set(screen, "intro", motion);
        }

        // Последний кадр старой сцены поверх всего, под ним грузится новая; растворение 0.5 с.
        public static void Dissolve(SceneLoader loader)
        {
            var curtain = loader.transform.Find("Curtain");
            var snapshot = curtain.Find("Snapshot");
            if (snapshot == null)
            {
                snapshot = Node("Snapshot", curtain).Fill();
                var image = snapshot.gameObject.AddComponent<RawImage>();
                image.raycastTarget = false;
                image.enabled = false;
            }
            Set(loader, "snapshot", snapshot.GetComponent<RawImage>());
            Set(loader, "fadeTime", 0.5f);
        }

        // Тень у кнопки с картинкой на корне лежит соседом «<Имя> Shadow» — она едет вместе с кнопкой.
        static void Add(List<ScreenMotion.Step> steps, Transform parent, string name, Vector2 offset, float scale, float delay,
            float duration, bool spring = false)
        {
            foreach (string part in new[] { name + " Shadow", name })
            {
                var target = parent.Find(part) as RectTransform;
                if (target == null)
                    continue;
                steps.Add(new ScreenMotion.Step
                {
                    target = target, offset = offset, scale = scale, delay = delay, duration = duration, spring = spring
                });
            }
        }

        static ScreenMotion MotionOn(GameObject screen, string name, bool appear, List<ScreenMotion.Step> steps)
        {
            var holder = screen.transform.Find(name);
            if (holder == null)
                holder = Node(name, screen.transform);
            var motion = holder.GetComponent<ScreenMotion>();
            if (motion == null)
                motion = holder.gameObject.AddComponent<ScreenMotion>();
            Set(motion, "appear", appear);
            var so = new SerializedObject(motion);
            var list = so.FindProperty("steps");
            list.arraySize = steps.Count;
            for (int i = 0; i < steps.Count; i++)
            {
                var item = list.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("target").objectReferenceValue = steps[i].target;
                item.FindPropertyRelative("offset").vector2Value = steps[i].offset;
                item.FindPropertyRelative("scale").floatValue = steps[i].scale;
                item.FindPropertyRelative("delay").floatValue = steps[i].delay;
                item.FindPropertyRelative("duration").floatValue = steps[i].duration;
                item.FindPropertyRelative("spring").boolValue = steps[i].spring;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            return motion;
        }
    }
}
