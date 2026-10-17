using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static BottleSort.UI.Editor.UiKit;

namespace BottleSort.UI.Editor
{
    // Вторая кнопка в карточках наград: синяя обычная под зелёной «за рекламу» и иконка видео на зелёной.
    // Обе выключены, их включает AdChoiceButtons. В собранные префабы — точечно, не трогая ручные правки:
    // -executeMethod BottleSort.UI.Editor.UiSceneBuilder.ApplyAdChoiceToPrefabs
    public static partial class UiSceneBuilder
    {
        static readonly string[] AdChoicePrefabs =
        {
            "Assets/_Game/Prefabs/UI/Screens/RewardPopup.prefab",
            "Assets/_Game/Prefabs/UI/Screens/CalendarPopup.prefab"
        };

        public static void ApplyAdChoiceToPrefabs()
        {
            LoadFonts();
            foreach (string path in AdChoicePrefabs)
            {
                var root = PrefabUtility.LoadPrefabContents(path);
                AddAdChoice(root.GetComponent<UiScreen>());
                PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[UiSceneBuilder] second ad-choice button added to reward cards");
        }

        // Крупная иконка по центру карточки для предложений без ряда наград («+5 ходов», «+1 бутылка»).
        // -executeMethod BottleSort.UI.Editor.UiSceneBuilder.ApplyCardHighlightToPrefab
        public static void ApplyCardHighlightToPrefab()
        {
            LoadFonts();
            string path = AdChoicePrefabs[0];
            var root = PrefabUtility.LoadPrefabContents(path);
            AddCardHighlight(root.GetComponent<RewardPopup>());
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.SaveAssets();
            Debug.Log("[UiSceneBuilder] highlight icon added to RewardPopup");
        }

        static void AddCardHighlight(RewardPopup popup)
        {
            var panel = (RectTransform)popup.transform.Find("Content/Panel");
            var old = panel.Find("Highlight");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);

            // На месте ряда наград: под подзаголовком, плитка 88 pt и плашка с числом на её нижнем крае.
            var root = Node("Highlight", panel);
            // Под заголовками: мягкое свечение плитки не должно ложиться на текст.
            root.SetAsFirstSibling();
            root.Place(new Vector2(0.5f, 1f), new Vector2(0f, -98f), new Vector2(88f, 88f), new Vector2(0.5f, 1f));
            var glow = Image(root, "Glow", "glow", Hex("#35C7FF", 0.45f));
            glow.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(150f, 150f));
            var tile = Rounded(root, "Tile", 26, Color.white);
            tile.rectTransform.Fill();
            Gradient(tile, Hex("#35C7FF"), Hex("#0548A8"));
            Border(tile.transform, 26, White(0.6f));
            InsetBottom(tile.transform, 26, new Color(0f, 0f, 0f, 0.25f));
            var icon = Image(tile.transform, "Icon", "icon_hourglass", Color.white);
            icon.rectTransform.sizeDelta = M(48f, 48f) * (27f / 24f);

            var badge = Rounded(root, "Badge", 15, Color.white);
            badge.rectTransform.Place(new Vector2(0.5f, 0f), Vector2.zero, new Vector2(64f, 30f), new Vector2(0.5f, 0.5f));
            Gradient(badge, Hex("#FFD957"), Hex("#FF9F1A"));
            Border(badge.transform, 15, White(0.7f));
            var caption = Text(badge.transform, "Caption", DisplayFont, 20, Color.white);
            caption.rectTransform.Fill();
            caption.text = "+5";
            Shadow(caption, Hex("#A84A00"), -0.8f);
            root.gameObject.SetActive(false);

            Set(popup, "highlight", root.gameObject);
            Set(popup, "highlightIcon", icon);
            Set(popup, "highlightCaption", caption);
            Set(popup, "highlightTrim", M(16f));
            var so = new SerializedObject(popup);
            var list = so.FindProperty("highlights");
            var entries = new (string key, string icon, string caption)[]
            {
                ("lose_moves_ad", "icon_hourglass", "+5"),
                ("deadend_bottle_ad", "icon_add_bottle", "+1")
            };
            list.arraySize = entries.Length;
            for (int i = 0; i < entries.Length; i++)
            {
                var item = list.GetArrayElementAtIndex(i);
                item.FindPropertyRelative("adKey").stringValue = entries[i].key;
                item.FindPropertyRelative("icon").objectReferenceValue = Sprite(entries[i].icon);
                item.FindPropertyRelative("caption").stringValue = entries[i].caption;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void AddAdChoice(UiScreen popup)
        {
            var panel = (RectTransform)popup.transform.Find("Content/Panel");
            var claim = (RectTransform)panel.Find("Claim");
            foreach (string old in new[] { "SecondaryButton", "Claim/Face/VideoIcon" })
            {
                var found = panel.Find(old);
                if (found != null)
                    Object.DestroyImmediate(found.gameObject);
            }

            // Синяя кнопка встаёт на место зелёной; в паре зелёная поднимается над ней.
            var secondary = ColorButton(panel, "SecondaryButton", Hex("#35C7FF"), Hex("#0870E8"), 20, Hex("#0548A8"), 5, out var face);
            var rect = (RectTransform)secondary.transform;
            rect.anchorMin = claim.anchorMin;
            rect.anchorMax = claim.anchorMax;
            rect.pivot = claim.pivot;
            rect.offsetMin = claim.offsetMin;
            rect.offsetMax = claim.offsetMax;
            rect.SetSiblingIndex(claim.GetSiblingIndex() + 1);
            var label = Text(face, "Label", DisplayFont, 22f * 0.85f, Color.white, TextAlignmentOptions.Center, "gift_claim");
            label.rectTransform.Fill();
            Shadow(label, Hex("#0548A8"), -0.8f);
            secondary.gameObject.SetActive(false);

            var icon = Image(claim.Find("Face"), "VideoIcon", "icon_video", Color.white);
            icon.rectTransform.Place(new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(26f, 26f), new Vector2(0f, 0.5f));
            icon.gameObject.SetActive(false);

            var choice = popup.GetComponent<AdChoiceButtons>();
            if (choice == null)
                choice = popup.gameObject.AddComponent<AdChoiceButtons>();
            Set(choice, "primary", claim.GetComponent<Button>());
            Set(choice, "secondary", secondary);
            Set(choice, "secondaryLabel", label.GetComponent<LocalizedText>());
            Set(choice, "videoIcon", icon.gameObject);
            Set(choice, "panel", panel);
            Set(popup, "adChoice", choice);
        }
    }
}
