using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace BottleSort.Editor
{
    // Иконка как во Flutter-версии (flutter_launcher_icons): app_icon.jpg везде,
    // на Android adaptive - фон #061B46 и та же картинка с отступом 16% по краям.
    public static class AppIcons
    {
        public const string SourcePath = "Assets/_Game/Art/app_icon.jpg";
        public const string LayersFolder = "Assets/_Game/Art/Icons/";
        const string BackgroundPath = LayersFolder + "app_icon_bg.png";
        const string ForegroundPath = LayersFolder + "app_icon_fg.png";

        static readonly Color32 BackgroundColor = new Color32(0x06, 0x1B, 0x46, 0xFF);
        const float ForegroundInset = 0.16f;
        const int BackgroundSize = 432;

        [MenuItem("BottleSort/Apply App Icons")]
        public static void Apply()
        {
            if (!File.Exists(SourcePath))
            {
                Debug.LogError($"[AppIcons] Missing {SourcePath}");
                return;
            }

            GenerateLayers();
            var icon = LoadTexture(SourcePath);
            var background = LoadTexture(BackgroundPath);
            var foreground = LoadTexture(ForegroundPath);

            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);

            // У adaptive два слоя: 0 - фон, 1 - передний план. Round и Legacy однослойные.
            foreach (var kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android))
            {
                var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
                foreach (var platformIcon in icons)
                {
                    if (platformIcon.maxLayerCount >= 2)
                        platformIcon.SetTextures(background, foreground);
                    else
                        platformIcon.SetTexture(icon);
                }
                PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
            }

            // Без iOS-модуля список пуст, тогда iOS возьмёт Default Icon. В jpg альфы нет.
            foreach (var kind in PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.iOS))
            {
                var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.iOS, kind);
                foreach (var platformIcon in icons)
                    platformIcon.SetTexture(icon);
                PlayerSettings.SetPlatformIcons(NamedBuildTarget.iOS, kind, icons);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[AppIcons] Applied");
        }

        // Во Flutter отступ задан через <inset> в xml, Unity так не умеет, поэтому запекаем его в png.
        // Холст подбираем под исходник, чтобы картинка легла пиксель в пиксель, без пересэмплирования.
        static void GenerateLayers()
        {
            var source = new Texture2D(2, 2);
            source.LoadImage(File.ReadAllBytes(SourcePath));

            int margin = Mathf.RoundToInt(source.width * ForegroundInset / (1f - 2f * ForegroundInset));
            int canvas = source.width + 2 * margin;
            var foreground = new Texture2D(canvas, canvas, TextureFormat.RGBA32, false);
            foreground.SetPixels32(new Color32[canvas * canvas]);
            foreground.SetPixels32(margin, margin, source.width, source.height, source.GetPixels32());

            var background = new Texture2D(BackgroundSize, BackgroundSize, TextureFormat.RGBA32, false);
            var fill = new Color32[BackgroundSize * BackgroundSize];
            for (int i = 0; i < fill.Length; i++)
                fill[i] = BackgroundColor;
            background.SetPixels32(fill);

            Directory.CreateDirectory(LayersFolder);
            WriteIfChanged(ForegroundPath, foreground.EncodeToPNG());
            WriteIfChanged(BackgroundPath, background.EncodeToPNG());

            Object.DestroyImmediate(source);
            Object.DestroyImmediate(foreground);
            Object.DestroyImmediate(background);
        }

        static void WriteIfChanged(string path, byte[] png)
        {
            if (File.Exists(path) && File.ReadAllBytes(path).SequenceEqual(png))
                return;
            File.WriteAllBytes(path, png);
            AssetDatabase.ImportAsset(path);
        }

        static Texture2D LoadTexture(string path)
        {
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }

    public sealed class AppIconImportRules : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            bool isLayer = assetPath.StartsWith(AppIcons.LayersFolder);
            if (!isLayer && assetPath != AppIcons.SourcePath)
                return;

            var importer = (TextureImporter)assetImporter;
            bool hasAlpha = assetPath.EndsWith("_fg.png");

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
