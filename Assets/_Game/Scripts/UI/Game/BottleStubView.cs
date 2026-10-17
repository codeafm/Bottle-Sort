using System.Collections.Generic;
using BottleSort.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BottleSort.UI
{
    // Бутылка на uGUI для меню, заставки и магазина: там нужны маски и прокрутка, а мировой BottleView
    // в Canvas не встанет. Стекло — текстуры vfx-art, жидкость — слои под маской той же формы.
    public sealed class BottleStubView : MonoBehaviour, IBottleView
    {
        [System.Serializable]
        public struct ShapeArt
        {
            public string id;
            public Texture back;
            public Texture front;
            public Sprite liquid;
            public Sprite glow;
            // Где в viewBox 100×240 лежит жидкость.
            public float liquidTop;
            public float liquidBottom;
        }

        const float ViewBoxHeight = 240f;

        [SerializeField] ShapeArt[] shapes;
        [SerializeField] Image glow;
        [SerializeField] RawImage back;
        [SerializeField] RawImage front;
        [SerializeField] Image mask;
        [SerializeField] RectTransform layers;
        [SerializeField] RectTransform shade;
        // Слои жидкости и знаки «?» лежат в префабе заранее, по одному на место в бутылке (колба 5×2 — до 10).
        [SerializeField] Image[] layerSlots;
        [SerializeField] TMP_Text[] hiddenSlots;
        [SerializeField] GameObject frozenBadge;
        [SerializeField] TMP_Text frozenCount;
        [SerializeField] Image frozenTint;
        [SerializeField] GameObject lockBadge;
        [SerializeField] CanvasGroup group;

        readonly List<Image> _layerImages = new List<Image>();
        readonly List<UiGradient> _layerShades = new List<UiGradient>();
        readonly List<TMP_Text> _hiddenMarks = new List<TMP_Text>();
        int _shape = -1;
        float _layerHeight;
        BottleGlow _glow;
        bool _completed;

        public RectTransform Root => (RectTransform)transform;
        public bool MovesItself => false;

        public void Show(Bottle bottle, int unit)
        {
            var art = ApplyShape(bottle.Shape);
            // Слои считаются в коробке viewBox, она совпадает с размером самой бутылки.
            float height = layers.rect.height;
            float top = art.liquidTop / ViewBoxHeight * height;
            float bottom = art.liquidBottom / ViewBoxHeight * height;
            float layerHeight = (bottom - top) / bottle.Capacity;
            _layerHeight = layerHeight;
            float bottomFromBase = height - bottom;

            EnsureLayers(bottle.Count);
            for (int i = 0; i < _layerImages.Count; i++)
            {
                var image = _layerImages[i];
                bool visible = i < bottle.Count;
                image.gameObject.SetActive(visible);
                _hiddenMarks[i].gameObject.SetActive(visible && bottle.IsLayerHidden(i));
                if (!visible)
                    continue;

                bool hidden = bottle.IsLayerHidden(i);
                var color = hidden ? UiColors.Hex("#3E4A68") : UiColors.Liquid(bottle[i]);
                // gLayer из Bottle.dc.html: светлее у верхней границы слоя, чуть темнее у нижней.
                _layerShades[i].SetColors(Color.Lerp(color, Color.white, 0.12f), color, 0.14f, Color.Lerp(color, Color.black, 0.07f));
                var rect = image.rectTransform;
                // Чуть больше слоя, чтобы между слоями не просвечивало стекло.
                rect.anchoredPosition = new Vector2(0f, bottomFromBase + layerHeight * i);
                rect.sizeDelta = new Vector2(0f, layerHeight + 1f);
                var mark = _hiddenMarks[i];
                mark.rectTransform.anchoredPosition = new Vector2(0f, bottomFromBase + layerHeight * (i + 0.5f));
                mark.fontSize = Mathf.Min(layerHeight * 0.8f, Root.rect.width * 0.5f);
            }

            shade.gameObject.SetActive(bottle.Count > 0);
            shade.anchoredPosition = new Vector2(0f, bottomFromBase);
            shade.sizeDelta = new Vector2(shade.sizeDelta.x, layerHeight * bottle.Count);

            // Значки в долях ширины: одна и та же бутылка бывает и маленькой в карточке, и крупно на заставке.
            float width = Root.rect.width;
            ((RectTransform)frozenBadge.transform).sizeDelta = Vector2.one * width * 0.38f;
            ((RectTransform)lockBadge.transform).sizeDelta = Vector2.one * width * 0.44f;
            frozenCount.fontSize = width * 0.22f;

            frozenBadge.SetActive(bottle.IsFrozen);
            frozenTint.gameObject.SetActive(bottle.IsFrozen);
            if (bottle.IsFrozen)
                frozenCount.text = bottle.FrozenTurns.ToString();
            lockBadge.SetActive(bottle.IsLocked);
        }

        // Непрерывный уровень одного цвета (заставка): level в слоях, верхний слой может быть неполным;
        // alpha — для мягкой смены цвета.
        public void ShowLiquid(byte color, int capacity, float level, float alpha = 1f)
        {
            int whole = Mathf.Clamp(Mathf.CeilToInt(level - 0.001f), 0, capacity);
            var bottle = new Bottle(capacity);
            for (int i = 0; i < whole; i++)
                bottle.Push(color);
            Show(bottle, capacity);
            if (whole == 0)
                return;

            var top = _layerImages[whole - 1].rectTransform;
            float fraction = Mathf.Clamp01(level - (whole - 1));
            top.sizeDelta = new Vector2(0f, _layerHeight * fraction + 1f);
            shade.sizeDelta = new Vector2(shade.sizeDelta.x, _layerHeight * Mathf.Min(level, capacity));
            var shadeImage = shade.GetComponent<Image>();
            var shadeColor = shadeImage.color;
            shadeColor.a = alpha;
            shadeImage.color = shadeColor;
            if (alpha >= 1f)
                return;
            var tint = UiColors.Liquid(color);
            tint.a = alpha;
            for (int i = 0; i < whole; i++)
                _layerShades[i].SetColors(Color.Lerp(tint, new Color(1f, 1f, 1f, alpha), 0.12f), tint, 0.14f,
                    Color.Lerp(tint, new Color(0f, 0f, 0f, alpha), 0.07f));
        }

        // Свечение «бутылка собрана» с плавным появлением и угасанием (0 — нет, 1 — полностью).
        public void SetCompletedGlow(float amount)
        {
            _completed = amount > 0.001f;
            ApplyGlow();
            if (_completed && _glow == BottleGlow.None)
                glow.color = UiColors.DoneGlow * new Color(1f, 1f, 1f, 0.9f * Mathf.Clamp01(amount));
        }

        public void SetGlow(BottleGlow state)
        {
            _glow = state;
            ApplyGlow();
        }

        public void SetCompleted(bool completed, bool animated)
        {
            _completed = completed;
            ApplyGlow();
        }

        // Трясёт ячейка поля, у этой бутылки своей анимации нет.
        public void Shake() { }

        public void SetDimmed(bool dimmed)
        {
            group.alpha = dimmed ? 0.6f : 1f;
        }

        void ApplyGlow()
        {
            glow.gameObject.SetActive(_glow != BottleGlow.None || _completed);
            switch (_glow)
            {
                case BottleGlow.Selected: glow.color = UiColors.Selected; break;
                case BottleGlow.Hint: glow.color = UiColors.HintGlow; break;
                default: glow.color = UiColors.DoneGlow * new Color(1f, 1f, 1f, 0.9f); break;
            }
        }

        ShapeArt ApplyShape(string shape)
        {
            int index = IndexOf(shape);
            if (index < 0)
                index = Mathf.Max(0, IndexOf(Bottle.DefaultShape));
            var art = shapes[index];
            if (index == _shape)
                return art;
            _shape = index;
            back.texture = art.back;
            front.texture = art.front;
            mask.sprite = art.liquid;
            frozenTint.sprite = art.liquid;
            glow.sprite = art.glow;
            return art;
        }

        int IndexOf(string id)
        {
            for (int i = 0; i < shapes.Length; i++)
                if (shapes[i].id == id)
                    return i;
            return -1;
        }

        void EnsureLayers(int count)
        {
            if (_layerImages.Count == 0)
                for (int i = 0; i < layerSlots.Length; i++)
                {
                    _layerImages.Add(layerSlots[i]);
                    _layerShades.Add(layerSlots[i].GetComponent<UiGradient>());
                    _hiddenMarks.Add(hiddenSlots[i]);
                }
            // Запас на случай бутылки больше, чем мест в префабе.
            while (_layerImages.Count < count)
            {
                var image = Instantiate(layerSlots[0], layers);
                image.gameObject.name = "Layer " + _layerImages.Count;
                image.gameObject.SetActive(true);
                image.color = Color.white;
                _layerImages.Add(image);
                _layerShades.Add(image.GetComponent<UiGradient>());
                var mark = Instantiate(hiddenSlots[0], layers);
                mark.gameObject.name = "Hidden " + _hiddenMarks.Count;
                _hiddenMarks.Add(mark);
            }
            // Знаки «?» поверх всех слоёв, блик — поверх всего.
            foreach (var mark in _hiddenMarks)
                mark.transform.SetAsLastSibling();
            shade.SetAsLastSibling();
        }
    }
}
