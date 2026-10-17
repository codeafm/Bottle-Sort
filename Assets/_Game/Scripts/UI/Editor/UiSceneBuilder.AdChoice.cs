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
