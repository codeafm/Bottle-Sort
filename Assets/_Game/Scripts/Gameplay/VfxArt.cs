using UnityEngine;

namespace BottleSort.Gameplay
{
    // Всё, что генерирует BottleSort/VFX/Generate Art. Лежит в Resources, поэтому визуальные компоненты
    // работают и без ручной настройки ссылок.
    [CreateAssetMenu(menuName = "BottleSort/VFX Art", fileName = "VfxArt")]
    public sealed class VfxArt : ScriptableObject
    {
        public const string ResourcePath = "VfxArt";

        public BottleShapeArt[] shapes;
        public Texture2D glyphs;

        public Shader bottleShader;
        public Shader streamShader;
        public Shader backgroundShader;
        public Material spriteMaterial;
        public Material additiveMaterial;

        public Sprite lockSprite;
        public Sprite gemSprite;
        public Sprite shelfSprite;
        public Sprite rippleSprite;
        public Sprite dropSprite;
        public Sprite flashSprite;
        public Sprite sparkSprite;
        public Sprite confettiSprite;

        static VfxArt loaded;

        public static VfxArt Default
        {
            get
            {
                if (loaded == null)
                    loaded = Resources.Load<VfxArt>(ResourcePath);
                return loaded;
            }
        }

        public BottleShapeArt GetShape(string id)
        {
            BottleShapeArt fallback = null;
            foreach (var shape in shapes)
            {
                if (shape.id == id)
                    return shape;
                if (shape.id == BottleShapeIds.Classic)
                    fallback = shape;
            }
            return fallback;
        }
    }
}
