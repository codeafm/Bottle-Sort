using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using static BottleSort.UI.Editor.UiKit;

namespace BottleSort.UI.Editor
{
    // Интро CODEAFM внутри префаба SplashScreen: раскладка, размеры и шрифты как в MahjongVita (BrandIntro.Build).
    // Вызывается из UiSceneBuilder, а в уже собранный префаб — точечно, не трогая ручные правки остального:
    // -executeMethod BottleSort.UI.Editor.BrandIntroSetup.ApplyToPrefab
    public static class BrandIntroSetup
    {
        const string BrandDir = "Assets/_Game/Brand";
        const string LogoPath = BrandDir + "/codeafm-logo.png";
        const string HaloPath = BrandDir + "/codeafm-halo.png";
        const string ShaderPath = BrandDir + "/CodeafmReveal.shader";
        const string SoundPath = "Assets/_Game/Audio/Brand/codeafm-intro.wav";
        const string SplashPrefab = "Assets/_Game/Prefabs/UI/Screens/SplashScreen.prefab";
        const string ArimoBold = "Assets/_Game/Fonts/Arimo-Bold SDF.asset";
        // В MahjongVita надписи — встроенный LegacyRuntime (Arial); Arimo и Liberation Sans повторяют его метрики.
        const string LiberationSans = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";

        const float SourceSize = 1254f;
        static readonly Rect EmblemPixels = new Rect(326, 351, 640, 413);
        static readonly Color Backdrop = new Color(0.018f, 0.012f, 0.035f, 1f);

        public static void ApplyToPrefab()
        {
            var root = PrefabUtility.LoadPrefabContents(SplashPrefab);
            AddTo(root.GetComponent<SplashScreen>());
            PrefabUtility.SaveAsPrefabAsset(root, SplashPrefab);
            PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.SaveAssets();
            Debug.Log("[BrandIntroSetup] CODEAFM intro added to SplashScreen");
        }

        public static void AddTo(SplashScreen screen)
        {
            var old = screen.transform.Find("CodeafmIntro");
            if (old != null)
                Object.DestroyImmediate(old.gameObject);
            SetupHalo();
            var logo = AssetDatabase.LoadAssetAtPath<Texture2D>(LogoPath);

            // Поверх всей заставки; тёмный фон шире safe area, чтобы закрыть и вырез, и полосу жестов.
            var root = Node("CodeafmIntro", screen.transform).Fill();
            root.SetAsLastSibling();
            var overlay = Group(root.gameObject, 1f, true);
            var backdrop = Image(root, "Backdrop", null, Backdrop, true);
            backdrop.rectTransform.anchorMin = Vector2.zero;
            backdrop.rectTransform.anchorMax = Vector2.one;
            backdrop.rectTransform.offsetMin = new Vector2(-400f, -400f);
            backdrop.rectTransform.offsetMax = new Vector2(400f, 400f);

            var stage = Node("Stage", root);
            stage.anchorMin = stage.anchorMax = new Vector2(0.5f, 0.5f);
            stage.sizeDelta = new Vector2(1000f, 920f);
            var artwork = Group(stage.gameObject, 1f, false);

            var halo = Image(stage, "Halo", null, new Color(1f, 1f, 1f, 0.08f));
            halo.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(HaloPath);
            halo.rectTransform.anchoredPosition = new Vector2(0f, 100f);
            halo.rectTransform.sizeDelta = new Vector2(1060f, 850f);

            var left = Part(stage, "EmblemLeft", new Rect(326, 351, 320, 413), -160f, logo);
            var right = Part(stage, "EmblemRight", new Rect(646, 351, 320, 413), 160f, logo);

            var wordmark = Word(stage, "Codeafm", "CODEAFM", ArimoBold, 90f, new Vector2(860f, 110f), -195f);
            var gaming = Word(stage, "Gaming", "G A M I N G", LiberationSans, 34f, new Vector2(760f, 62f), -278f);

            var audio = root.gameObject.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.loop = false;
            audio.spatialBlend = 0f;
            audio.dopplerLevel = 0f;
            audio.priority = 32;
            audio.volume = 0.8f;
            audio.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(SoundPath);

            var intro = root.gameObject.AddComponent<BrandIntro>();
            Set(intro, "overlay", overlay);
            Set(intro, "stage", stage);
            Set(intro, "artwork", artwork);
            Set(intro, "left", left);
            Set(intro, "right", right);
            Set(intro, "wordmark", wordmark);
            Set(intro, "gaming", gaming);
            Set(intro, "halo", halo);
            Set(intro, "signature", audio);
            // В редакторе интро видно целиком: кадр, где эмблема и надписи уже на месте.
            stage.localScale = Vector3.one * 0.975f;
            intro.Present(0.75f);
            Set(screen, "brandIntro", intro);
        }

        static RawImage Part(RectTransform stage, string name, Rect pixels, float x, Texture2D logo)
        {
            var rect = Node(name, stage);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, 102f);
            rect.sizeDelta = pixels.size;
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = logo;
            image.uvRect = UV(pixels);
            image.raycastTarget = false;
            image.material = RevealMaterial(name, image.uvRect);
            return image;
        }

        // Материал-ассет на каждую половину: в редакторе эмблема видна белой и раскрытой, в игре BrandIntro берёт копию.
        static Material RevealMaterial(string name, Rect part)
        {
            string path = $"{BrandDir}/Codeafm{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath));
                AssetDatabase.CreateAsset(material, path);
            }
            var emblem = UV(EmblemPixels);
            material.SetVector("_PartUV", new Vector4(part.x, part.y, part.width, part.height));
            material.SetVector("_EmblemUV", new Vector4(emblem.x, emblem.y, emblem.width, emblem.height));
            material.SetFloat("_Reveal", 1.1f);
            EditorUtility.SetDirty(material);
            return material;
        }

        static TextMeshProUGUI Word(RectTransform stage, string name, string text, string fontPath, float size, Vector2 box, float y)
        {
            var rect = Node(name, stage);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, y);
            rect.sizeDelta = box;
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
            label.fontSize = size;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Overflow;
            label.raycastTarget = false;
            label.text = text;
            return label;
        }

        static void SetupHalo()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(HaloPath);
            if (importer.textureType == TextureImporterType.Sprite)
                return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

        static Rect UV(Rect pixels) =>
            new Rect(pixels.x / SourceSize, 1f - pixels.yMax / SourceSize, pixels.width / SourceSize, pixels.height / SourceSize);
    }
}
