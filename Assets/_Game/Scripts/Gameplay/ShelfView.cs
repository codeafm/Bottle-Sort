using UnityEngine;

namespace BottleSort.Gameplay
{
    // Стеклянная полка под рядом бутылок. Позиция — линия, на которой стоят донышки,
    // ширина в мировых единицах (раскладка обычно берёт ширину ряда + 16 px макета с каждой стороны).
    [ExecuteAlways]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class ShelfView : MonoBehaviour
    {
        public const int DefaultSortingOrder = -10;
        public const float CssToWorld = 2f / 70f;

        [SerializeField] float width = 8f;

        SpriteRenderer spriteRenderer;
        bool dirty;

        public float Width
        {
            get => width;
            set
            {
                width = Mathf.Max(0.5f, value);
                Apply();
            }
        }

        void OnEnable() => Apply();

        // Размер спрайта нельзя менять прямо в OnValidate, Unity ругается — откладываем до Update
        void OnValidate() => dirty = true;

        void Update()
        {
            if (!dirty)
                return;
            dirty = false;
            Apply();
        }

        void Apply()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
                var art = VfxArt.Default;
                if (art == null)
                    return;
                spriteRenderer.sprite = art.shelfSprite;
                spriteRenderer.sharedMaterial = art.spriteMaterial;
                spriteRenderer.drawMode = SpriteDrawMode.Sliced;
                if (spriteRenderer.sortingOrder == 0)
                    spriteRenderer.sortingOrder = DefaultSortingOrder;
            }
            if (spriteRenderer.sprite == null)
                return;

            // Спрайт шире самой полки на свечение по краям, его и добавляем
            var sprite = spriteRenderer.sprite;
            float glow = (sprite.border.x + sprite.border.z) / sprite.pixelsPerUnit - ShelfCapsWidth;
            spriteRenderer.size = new Vector2(width + glow, sprite.rect.height / sprite.pixelsPerUnit);
        }

        // Скругления самой полки внутри левого и правого бордюров
        const float ShelfCapsWidth = 2f * 8f * CssToWorld;
    }
}
