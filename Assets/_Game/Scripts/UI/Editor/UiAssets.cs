using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace BottleSort.UI.Editor
{
    // Подготовка ассетов UI: TMP Essentials, настройки импорта спрайтов, SDF-шрифты с кириллицей.
    public static class UiAssets
    {
        public const string ArtDir = "Assets/_Game/Art/UI";
        public const string FontsDir = "Assets/_Game/Fonts";
        public const string FredokaAsset = FontsDir + "/Fredoka-Bold SDF.asset";
        public const string NunitoAsset = FontsDir + "/Nunito-ExtraBold SDF.asset";
        public const string NunitoBlackAsset = FontsDir + "/Nunito-Black SDF.asset";

        const string TmpEssentials = "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage";
        const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        // Латиница, кириллица и знаки, которые встречаются в loc_strings.txt.
        static readonly string Characters = BuildCharacterSet();

        // В batchmode ImportPackage ничего не делает, там нужен ключ
        // -importPackage "Library/PackageCache/com.unity.ugui@.../Package Resources/TMP Essential Resources.unitypackage".
        [MenuItem("BottleSort/UI/1. Import TMP Essentials")]
        public static void ImportTmpEssentials()
        {
            if (File.Exists(TmpSettingsPath))
                return;
            AssetDatabase.ImportPackage(TmpEssentials, false);
            AssetDatabase.Refresh();
        }

        [MenuItem("BottleSort/UI/2. Setup Sprites And Fonts")]
        public static void SetupAll()
        {
            SetupSprites();
            SetupFonts();
            AssetDatabase.SaveAssets();
        }

        public static void SetupSprites()
        {
            foreach (string path in Directory.GetFiles(ArtDir, "*.png"))
            {
                string assetPath = path.Replace('\\', '/');
                var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
                if (importer == null)
                    continue;

                string name = Path.GetFileNameWithoutExtension(assetPath);
                var border = Vector4.zero;
                if (name == "rounded" || name == "rounded_line")
                    border = new Vector4(64, 64, 64, 64);
                else if (name == "shelf")
                    border = new Vector4(16, 0, 16, 0);

                bool changed = importer.textureType != TextureImporterType.Sprite
                               || importer.spriteBorder != border || importer.mipmapEnabled;
                if (!changed)
                    continue;

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spriteBorder = border;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.SaveAndReimport();
            }
        }

        public static void SetupFonts()
        {
            var black = CreateFont(FontsDir + "/Nunito-Black.ttf", NunitoBlackAsset, 2048);
            var body = CreateFont(FontsDir + "/Nunito-ExtraBold.ttf", NunitoAsset, 2048);
            var display = CreateFont(FontsDir + "/Fredoka-Bold.ttf", FredokaAsset, 1024);

            // У Fredoka нет кириллицы: русские буквы в заголовках берутся из Nunito Black.
            display.fallbackFontAssetTable ??= new System.Collections.Generic.List<TMP_FontAsset>();
            if (!display.fallbackFontAssetTable.Contains(black))
                display.fallbackFontAssetTable.Add(black);
            EditorUtility.SetDirty(display);

            var settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);
            if (settings != null)
            {
                var so = new SerializedObject(settings);
                so.FindProperty("m_defaultFontAsset").objectReferenceValue = body;
                var fallbacks = so.FindProperty("m_fallbackFontAssets");
                fallbacks.arraySize = 1;
                fallbacks.GetArrayElementAtIndex(0).objectReferenceValue = black;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static TMP_FontAsset CreateFont(string fontPath, string assetPath, int atlasWidth)
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null)
                return existing;

            var font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
            var asset = TMP_FontAsset.CreateFontAsset(font, 64, 8, GlyphRenderMode.SDFAA, atlasWidth, 1024,
                AtlasPopulationMode.Dynamic, false);
            asset.name = Path.GetFileNameWithoutExtension(assetPath);
            AssetDatabase.CreateAsset(asset, assetPath);

            asset.atlasTexture.name = asset.name + " Atlas";
            asset.material.name = asset.name + " Material";
            AssetDatabase.AddObjectToAsset(asset.atlasTexture, asset);
            AssetDatabase.AddObjectToAsset(asset.material, asset);

            asset.TryAddCharacters(Characters, out string missing);
            if (!string.IsNullOrEmpty(missing))
                Debug.Log($"[UiAssets] {asset.name}: no glyphs for {missing.Length} chars (fallback will cover them)");
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        static string BuildCharacterSet()
        {
            var builder = new System.Text.StringBuilder();
            for (char c = ' '; c <= '~'; c++)
                builder.Append(c);
            for (char c = 'А'; c <= 'я'; c++)
                builder.Append(c);
            builder.Append("Ёё«»·×–—“”…№\u00A0");
            return builder.ToString();
        }
    }
}
