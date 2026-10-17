using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using Object = UnityEngine.Object;

namespace BottleSort.UI.Editor
{
    // Подготовка ассетов UI: TMP Essentials, настройки импорта спрайтов, SDF-шрифты с кириллицей.
    public static class UiAssets
    {
        public const string ArtDir = "Assets/_Game/Art/UI";
        const string BottleArtDir = "Assets/_Game/Art/Bottles";
        public const string FontsDir = "Assets/_Game/Fonts";
        public const string FredokaAsset = FontsDir + "/Fredoka-Bold SDF.asset";
        public const string NunitoAsset = FontsDir + "/Nunito-ExtraBold SDF.asset";
        public const string NunitoBlackAsset = FontsDir + "/Nunito-Black SDF.asset";
        public const string ArimoAsset = FontsDir + "/Arimo-Bold SDF.asset";

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
            BakeBottleMasks();
            foreach (string path in Directory.GetFiles(ArtDir, "*.png", SearchOption.AllDirectories))
            {
                string assetPath = path.Replace('\\', '/');
                var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
                if (importer == null)
                    continue;

                string name = Path.GetFileNameWithoutExtension(assetPath);
                var border = Vector4.zero;
                if (name == "rounded" || name == "rounded_line" || name == "inset_bottom")
                    border = new Vector4(64, 64, 64, 64);
                else if (name == "shelf")
                    border = new Vector4(16, 0, 16, 0);
                else if (name == "shadow")
                    border = new Vector4(128, 128, 128, 128);

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

        // Бутылки в меню, на заставке и в магазине рисуются на uGUI, но из арта vfx-art: стекло берём как есть,
        // а из маски (R — жидкость, G — свечение, B — тело) делаем спрайты, у которых нужное лежит в альфе.
        static void BakeBottleMasks()
        {
            string output = ArtDir + "/Bottles";
            Directory.CreateDirectory(output);
            foreach (string path in Directory.GetFiles(BottleArtDir, "*_mask.png"))
            {
                string id = Path.GetFileNameWithoutExtension(path).Replace("_mask", "");
                string liquidPath = $"{output}/{id}_liquid.png";
                string glowPath = $"{output}/{id}_glow.png";
                if (File.Exists(liquidPath) && File.GetLastWriteTimeUtc(liquidPath) >= File.GetLastWriteTimeUtc(path))
                    continue;

                var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                source.LoadImage(File.ReadAllBytes(path));
                var pixels = source.GetPixels32();
                var liquid = new Color32[pixels.Length];
                var glow = new Color32[pixels.Length];
                for (int i = 0; i < pixels.Length; i++)
                {
                    var p = pixels[i];
                    liquid[i] = new Color32(255, 255, 255, p.r);
                    glow[i] = new Color32(255, 255, 255, (byte)(p.g * (1f - 0.96f * p.b / 255f)));
                }
                WritePng(liquidPath, source.width, source.height, liquid);
                WritePng(glowPath, source.width, source.height, glow);
                Object.DestroyImmediate(source);
            }
            AssetDatabase.Refresh();
        }

        static void WritePng(string path, int width, int height, Color32[] pixels)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        public static void SetupFonts()
        {
            var black = CreateFont(FontsDir + "/Nunito-Black.ttf", NunitoBlackAsset, 2048);
            var body = CreateFont(FontsDir + "/Nunito-ExtraBold.ttf", NunitoAsset, 2048);
            var display = CreateFont(FontsDir + "/Fredoka-Bold.ttf", FredokaAsset, 1024);
            var cyrillic = CreateFont(FontsDir + "/Arimo-Bold.ttf", ArimoAsset, 2048);

            // У Fredoka нет кириллицы. В макете браузер дорисовывает её системным sans-serif, это Arial Bold,
            // поэтому берём Arimo — шрифт с метриками и рисунком Arial.
            display.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset> { cyrillic };
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
