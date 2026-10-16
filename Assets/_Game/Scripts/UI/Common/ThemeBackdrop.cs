using System.Collections.Generic;
using BottleSort.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace BottleSort.UI
{
    // Фон экранов. Если в сцене есть BackgroundView от vfx-art (шейдер в мире камеры), переключаем тему у него,
    // а свой UI-фон из Bg.dc.html (градиент, ореол, искорки) выключаем: он перекрыл бы бутылки на поле.
    public sealed class ThemeBackdrop : MonoBehaviour
    {
        const int Width = 108;
        const int Height = 234;

        [SerializeField] RawImage gradient;
        [SerializeField] Image halo;
        [SerializeField] Image[] sparks;
        [SerializeField] BackgroundView worldBackground;

        static readonly Dictionary<Theme, Texture2D> Textures = new Dictionary<Theme, Texture2D>();

        public Theme Current { get; private set; } = (Theme)(-1);

        public void SetTheme(Theme theme)
        {
            if (theme == Current)
                return;
            bool first = Current == (Theme)(-1);
            Current = theme;
            if (worldBackground != null)
            {
                // Порядок тем в BackgroundTheme тот же, что в Theme.
                worldBackground.SetTheme((BackgroundTheme)theme, first);
                return;
            }
            var colors = UiColors.Of(theme);
            gradient.texture = TextureFor(theme);
            var haloColor = colors.Halo;
            haloColor.a = 0.3f;
            halo.color = haloColor;
            foreach (var spark in sparks)
                spark.color = colors.Spark;
        }

        void Awake()
        {
            if (worldBackground == null)
                return;
            gradient.gameObject.SetActive(false);
            halo.gameObject.SetActive(false);
            foreach (var spark in sparks)
                spark.gameObject.SetActive(false);
        }

        public static Texture2D TextureFor(Theme theme)
        {
            if (Textures.TryGetValue(theme, out var cached) && cached != null)
                return cached;

            var colors = UiColors.Of(theme);
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "Backdrop " + theme
            };
            var pixels = new Color32[Width * Height];
            for (int y = 0; y < Height; y++)
            {
                // В CSS y идёт сверху вниз, в текстуре — снизу вверх.
                float v = 1f - (y + 0.5f) / Height;
                for (int x = 0; x < Width; x++)
                {
                    float u = (x + 0.5f) / Width;
                    // ellipse at 50% 35%, farthest-corner.
                    float du = (u - 0.5f) / 1.054f;
                    float dv = (v - 0.35f) / 0.738f;
                    float d = Mathf.Sqrt(du * du + dv * dv);
                    var color = d < 0.55f
                        ? Color.Lerp(colors.Center, colors.Middle, d / 0.55f)
                        : Color.Lerp(colors.Middle, colors.Edge, (d - 0.55f) / 0.45f);

                    float vu = (u - 0.5f) / 0.5f;
                    float vv = (v - 0.4f) / 0.6f;
                    float vignette = Mathf.InverseLerp(0.55f, 1f, Mathf.Sqrt(vu * vu + vv * vv) / 1.2f);
                    color = Color.Lerp(color, Color.black, vignette * 0.35f);
                    color.a = 1f;
                    pixels[y * Width + x] = color;
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Textures[theme] = texture;
            return texture;
        }
    }
}
