using System;
using System.Collections;
using System.Globalization;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BottleSort.UI.Editor
{
    // Мелкие помощники для сборки uGUI из кода. Размеры передаются в пикселях макета (ширина 390),
    // в единицы Canvas 1080×1920 их переводит M().
    internal static class UiKit
    {
        public const float K = 2.5f;

        public static TMP_FontAsset DisplayFont;
        public static TMP_FontAsset BodyFont;
        public static TMP_FontAsset HeavyFont;

        public static float M(float mockup) => mockup * K;
        public static Vector2 M(float x, float y) => new Vector2(x * K, y * K);

        public static void LoadFonts()
        {
            DisplayFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UiAssets.FredokaAsset);
            BodyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UiAssets.NunitoAsset);
            HeavyFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UiAssets.NunitoBlackAsset);
            if (DisplayFont == null || BodyFont == null || HeavyFont == null)
                throw new InvalidOperationException("Font assets are missing, run BottleSort/UI/2. Setup Sprites And Fonts");
        }

        public static Sprite Sprite(string name)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{UiAssets.ArtDir}/{name}.png");
            if (sprite == null)
                throw new InvalidOperationException($"Sprite {name} not found");
            return sprite;
        }

        public static Color Hex(string hex, float alpha = 1f) => UiColors.Hex(hex, alpha);
        public static Color White(float alpha) => new Color(1f, 1f, 1f, alpha);

        public static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        // Растянуть по родителю с отступами (в пикселях макета).
        public static RectTransform Fill(this RectTransform rect, float left = 0, float top = 0, float right = 0, float bottom = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(M(left), M(bottom));
            rect.offsetMax = new Vector2(-M(right), -M(top));
            return rect;
        }

        // Якорь и pivot в одной точке, позиция и размер в пикселях макета.
        public static RectTransform Place(this RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size, Vector2? pivot = null)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot ?? anchor;
            rect.anchoredPosition = position * K;
            rect.sizeDelta = size * K;
            return rect;
        }

        // Полоса на всю ширину, прибитая к верху: top — отступ сверху, height — высота.
        public static RectTransform TopBand(this RectTransform rect, float top, float height, float left = 0, float right = 0)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(M(left), -M(top + height));
            rect.offsetMax = new Vector2(-M(right), -M(top));
            return rect;
        }

        public static RectTransform BottomBand(this RectTransform rect, float bottom, float height, float left = 0, float right = 0)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(M(left), M(bottom));
            rect.offsetMax = new Vector2(-M(right), M(bottom + height));
            return rect;
        }

        public static Image Image(Transform parent, string name, string sprite, Color color, bool raycast = false)
        {
            var rect = Node(name, parent);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite != null ? Sprite(sprite) : null;
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        // Скруглённый прямоугольник: радиус в пикселях макета через Pixels Per Unit Multiplier.
        public static Image Rounded(Transform parent, string name, float radius, Color color, bool raycast = false)
        {
            var image = Image(parent, name, "rounded", color, raycast);
            image.type = UnityEngine.UI.Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 64f / M(radius);
            return image;
        }

        // Обводка поверх панели, растягивается по родителю.
        public static Image Border(Transform parent, float radius, Color color)
        {
            var image = Image(parent, "Border", "rounded_line", color);
            image.type = UnityEngine.UI.Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 64f / M(radius);
            image.rectTransform.Fill();
            return image;
        }

        public static UiGradient Gradient(Graphic graphic, Color top, Color bottom, bool horizontal = false)
        {
            var gradient = graphic.gameObject.AddComponent<UiGradient>();
            Set(gradient, "top", top);
            Set(gradient, "bottom", bottom);
            Set(gradient, "horizontal", horizontal);
            return gradient;
        }

        // box-shadow из макета: мягкая тень за элементом, со смещением вниз и размытием в pt.
        // Если у target нет своей картинки, тень ложится внутрь него первым ребёнком и двигается вместе с ним;
        // иначе — соседом перед ним (target должен быть уже размещён).
        public static Image SoftShadow(RectTransform target, float offsetY, float blur, Color color)
        {
            bool inside = target.GetComponent<Graphic>() == null;
            var shadow = Image(inside ? target : target.parent, inside ? "Shadow" : target.name + " Shadow", "shadow", color);
            shadow.type = UnityEngine.UI.Image.Type.Sliced;
            float extent = M(blur);
            shadow.pixelsPerUnitMultiplier = 64f / extent;
            var rect = shadow.rectTransform;
            var grow = new Vector2(-extent, -extent - M(offsetY));
            var shrink = new Vector2(extent, extent - M(offsetY));
            if (inside)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = grow;
                rect.offsetMax = shrink;
                rect.SetAsFirstSibling();
                shadow.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                return shadow;
            }
            rect.anchorMin = target.anchorMin;
            rect.anchorMax = target.anchorMax;
            rect.pivot = target.pivot;
            rect.offsetMin = target.offsetMin + grow;
            rect.offsetMax = target.offsetMax + shrink;
            rect.SetSiblingIndex(target.GetSiblingIndex());
            return shadow;
        }

        public static Image SoftShadow(RectTransform target, float offsetY, float blur, float alpha) =>
            SoftShadow(target, offsetY, blur, new Color(0f, 0f, 0f, alpha));

        // inset 0 -4px 0 из макета: тёмный серп по нижнему краю кнопки.
        public static Image InsetBottom(Transform face, float radius, Color color)
        {
            var inset = Image(face, "Inset", "inset_bottom", color);
            inset.type = UnityEngine.UI.Image.Type.Sliced;
            inset.pixelsPerUnitMultiplier = 64f / M(radius);
            inset.rectTransform.Fill();
            return inset;
        }

        // Монета из Docs/Art/coin.svg (квадрат 120): лицевой круг r = 50 с центром (60, 56), под ним толщина и тень.
        // Старая плоская монета занимала 92 из 96 px своего квадрата. Ставим новую так, чтобы лицо было
        // того же диаметра и на том же месте: size и center — размер и центр прежней монеты в pt.
        const float NewCoinFace = 100f / 120f;
        const float OldCoinFace = 92f / 96f;
        const float CoinFaceLift = 4f / 120f;

        public static Image Coin(Transform parent, string name, Vector2 anchor, Vector2 center, float size)
        {
            float drawn = size * OldCoinFace / NewCoinFace;
            var coin = Image(parent, name, "coin", Color.white);
            coin.preserveAspect = true;
            coin.rectTransform.Place(anchor, center - new Vector2(0f, drawn * CoinFaceLift), new Vector2(drawn, drawn),
                new Vector2(0.5f, 0.5f));
            return coin;
        }

        // Межбуквенный интервал из CSS (px при данном кегле) в единицы TMP (сотые доли em).
        public static float Spacing(float letterSpacing, float fontSize) => letterSpacing / fontSize * 100f;

        public static TextMeshProUGUI Text(Transform parent, string name, TMP_FontAsset font, float size, Color color,
            TextAlignmentOptions align = TextAlignmentOptions.Center, string key = null, bool upper = false)
        {
            var rect = Node(name, parent);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font;
            text.fontSize = M(size);
            text.color = color;
            text.alignment = align;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Overflow;
            if (key != null)
            {
                var localized = rect.gameObject.AddComponent<LocalizedText>();
                Set(localized, "key", key);
                Set(localized, "upperCase", upper);
                string value = Loc.Get(key);
                text.text = upper ? value.ToUpperInvariant() : value;
            }
            return text;
        }

        // Тени и обводки текста — материалы-ассеты в Fonts/Effects, по одному на шрифт и параметры:
        // так их видно в редакторе, их можно править, а одинаковые тексты батчатся.
        const string TextEffectsDir = "Assets/_Game/Fonts/Effects";

        // text-shadow из макета: underlay шрифта.
        public static void Shadow(TMP_Text text, Color color, float offset = -0.6f)
        {
            text.fontSharedMaterial = TextEffect(text, $"Shadow {ColorUtility.ToHtmlStringRGBA(color)} {Number(offset)}", material =>
            {
                material.EnableKeyword("UNDERLAY_ON");
                material.SetColor("_UnderlayColor", color);
                material.SetFloat("_UnderlayOffsetY", offset);
                material.SetFloat("_UnderlayDilate", 0.1f);
            });
        }

        public static void Outline(TMP_Text text, Color color, float width)
        {
            text.fontSharedMaterial = TextEffect(text, $"Outline {ColorUtility.ToHtmlStringRGBA(color)} {Number(width)}", material =>
            {
                material.EnableKeyword("OUTLINE_ON");
                material.SetColor("_OutlineColor", color);
                material.SetFloat("_OutlineWidth", width);
            });
        }

        static Material TextEffect(TMP_Text text, string effect, Action<Material> setup)
        {
            var source = text.font.material;
            string name = $"{source.name} {effect}";
            string path = $"{TextEffectsDir}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;
            System.IO.Directory.CreateDirectory(TextEffectsDir);
            material = new Material(source) { name = name };
            setup(material);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static string Number(float value) => value.ToString("0.##", CultureInfo.InvariantCulture);

        // Кнопка с нажатием scale 0.95. Зона касания — сам объект, минимум 44 pt выдерживается размерами из макета.
        public static Button Button(GameObject target, Transform visual = null)
        {
            var graphic = target.GetComponent<Graphic>();
            if (graphic == null)
            {
                graphic = target.AddComponent<Image>();
                ((Image)graphic).color = Color.clear;
            }
            graphic.raycastTarget = true;
            var button = target.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.targetGraphic = graphic;
            var press = target.AddComponent<PressScale>();
            if (visual != null)
                Set(press, "visual", visual);
            return button;
        }

        public static IdleMotion Motion(GameObject target, IdleMotion.Kind kind, float period, float amount, float phase = 0f)
        {
            var motion = target.AddComponent<IdleMotion>();
            Set(motion, "kind", kind);
            Set(motion, "period", period);
            Set(motion, "amount", amount);
            Set(motion, "phase", phase);
            return motion;
        }

        public static CanvasGroup Group(GameObject target, float alpha = 1f, bool raycasts = true)
        {
            var group = target.GetComponent<CanvasGroup>();
            if (group == null)
                group = target.AddComponent<CanvasGroup>();
            group.alpha = alpha;
            group.blocksRaycasts = raycasts;
            group.interactable = raycasts;
            return group;
        }

        public static void Set(Object target, string field, object value)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            if (property == null)
                throw new ArgumentException($"{target.GetType().Name}.{field} not found");

            switch (value)
            {
                case null:
                    property.objectReferenceValue = null;
                    break;
                case Object reference:
                    property.objectReferenceValue = reference;
                    break;
                case bool flag:
                    property.boolValue = flag;
                    break;
                case Enum enumValue:
                    property.intValue = Convert.ToInt32(enumValue);
                    break;
                case int integer:
                    property.intValue = integer;
                    break;
                case float number:
                    property.floatValue = number;
                    break;
                case string text:
                    property.stringValue = text;
                    break;
                case Color color:
                    property.colorValue = color;
                    break;
                case Vector2 vector:
                    property.vector2Value = vector;
                    break;
                case IList list:
                    property.arraySize = list.Count;
                    for (int i = 0; i < list.Count; i++)
                        property.GetArrayElementAtIndex(i).objectReferenceValue = (Object)list[i];
                    break;
                default:
                    throw new ArgumentException($"Unsupported value type {value.GetType().Name} for {field}");
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
