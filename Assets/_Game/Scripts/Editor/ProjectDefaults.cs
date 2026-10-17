using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace BottleSort.Editor
{
    // Настройки, которые открытый редактор может перезаписать своей копией ProjectSettings:
    // при каждой загрузке редактора проверяем их и, если что-то слетело, ставим заново.
    [InitializeOnLoad]
    static class ProjectDefaults
    {
        static ProjectDefaults()
        {
            if (Application.isBatchMode)
                return;
            EditorApplication.delayCall += Ensure;
        }

        [MenuItem("BottleSort/Fix Project Defaults")]
        static void Ensure()
        {
            bool changed = false;

            // Сразу интро CODEAFM, без экрана «Made with Unity».
            if (PlayerSettings.SplashScreen.show)
            {
                PlayerSettings.SplashScreen.show = false;
                PlayerSettings.SplashScreen.showUnityLogo = false;
                changed = true;
                Debug.Log("[ProjectDefaults] Unity splash screen turned off");
            }

            var icons = PlayerSettings.GetIcons(NamedBuildTarget.Unknown, IconKind.Any);
            if (icons == null || icons.Length == 0 || icons[0] == null)
            {
                AppIcons.Apply();
                changed = true;
            }

            if (changed)
                AssetDatabase.SaveAssets();
        }
    }
}
