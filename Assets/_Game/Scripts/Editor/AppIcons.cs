using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace BottleSort.Editor
{
    public static class AppIcons
    {
        public const string Folder = "Assets/_Game/Art/Icons/";
        const string FullPath = Folder + "icon_full.png";
        const string BackgroundPath = Folder + "icon_bg.png";
        const string ForegroundPath = Folder + "icon_fg.png";

        [MenuItem("BottleSort/Apply App Icons")]
        public static void Apply()
        {
            var full = Load(FullPath);
            var background = Load(BackgroundPath);
            var foreground = Load(ForegroundPath);
            if (full == null || background == null || foreground == null)
                return;

            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { full }, IconKind.Any);

            // У adaptive-иконки два слоя: 0 - фон, 1 - передний план. Round и Legacy однослойные.
            foreach (var kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android))
            {
                var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
                foreach (var icon in icons)
                {
                    if (icon.maxLayerCount >= 2)
                        icon.SetTextures(background, foreground);
                    else
                        icon.SetTexture(full);
                }
                PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
            }

            // Без iOS-модуля список пуст, тогда iOS возьмёт Default Icon.
            foreach (var kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.iOS))
            {
                var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.iOS, kind);
                foreach (var icon in icons)
                    icon.SetTexture(full);
                PlayerSettings.SetPlatformIcons(NamedBuildTarget.iOS, kind, icons);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[AppIcons] Applied");
        }

        static Texture2D Load(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogError($"[AppIcons] Missing {path}");
                return null;
            }
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }

    public sealed class AppIconImportRules : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(AppIcons.Folder))
                return;

            var importer = (TextureImporter)assetImporter;
            bool hasAlpha = Path.GetFileNameWithoutExtension(assetPath) == "icon_fg";

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaSource = hasAlpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = hasAlpha;
            importer.ClearPlatformTextureSettings("Android");
            importer.ClearPlatformTextureSettings("iPhone");
        }
    }
}
