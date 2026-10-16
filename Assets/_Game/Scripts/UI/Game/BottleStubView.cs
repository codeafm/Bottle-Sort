using System.Collections.Generic;
using BottleSort.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BottleSort.UI
{
    // Простая бутылка на uGUI: стекло, слои под маской, «?», лёд и замок значками.
    // Временная, до BottleView от vfx-art; форм только две — classic и мерная колба.
    public sealed class BottleStubView : MonoBehaviour, IBottleView
    {
        [System.Serializable]
        public struct ShapeArt
        {
            public Sprite back;
            public Sprite front;
            public Sprite mask;
            public Sprite glow;
            // Где в viewBox 100×240 лежит жидкость.
            public float liquidTop;
            public float liquidBottom;
        }

        const float ViewBoxHeight = 240f;

        [SerializeField] ShapeArt classic;
        [SerializeField] ShapeArt cylinder;
        [SerializeField] Image glow;
        [SerializeField] Image back;
        [SerializeField] Image front;
        [SerializeField] Image mask;
        [SerializeField] RectTransform layers;
        [SerializeField] RectTransform shade;
        [SerializeField] Image layerTemplate;
        [SerializeField] TMP_Text hiddenTemplate;
        [SerializeField] GameObject frozenBadge;
        [SerializeField] TMP_Text frozenCount;
        [SerializeField] Image frozenTint;
        [SerializeField] GameObject lockBadge;
        [SerializeField] CanvasGroup group;

        readonly List<Image> _layerImages = new List<Image>();
        readonly List<TMP_Text> _hiddenMarks = new List<TMP_Text>();
        string _shape;
        BottleGlow _glow;
        bool _completed;

        public RectTransform Root => (RectTransform)transform;
        public bool MovesItself => false;

        public void Show(Bottle bottle, int unit)
        {
            ApplyShape(bottle.Shape);
            var art = bottle.Shape == LevelGenerator.FlaskShape ? cylinder : classic;
            float height = Root.rect.height;
            float top = art.liquidTop / ViewBoxHeight * height;
            float bottom = art.liquidBottom / ViewBoxHeight * height;
            float layerHeight = (bottom - top) / bottle.Capacity;
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
                image.color = hidden ? UiColors.Hex("#3E4A68") : UiColors.Liquid(bottle[i]);
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
            shade.sizeDelta = new Vector2(0f, layerHeight * bottle.Count);

            // Значки в долях ширины: одна и та же бутылка бывает и в поле, и крупно на заставке.
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

        // Трясёт ячейка поля, у заглушки своей анимации нет.
        public void Shake() { }

        void ApplyGlow()
        {
            glow.gameObject.SetActive(_glow != BottleGlow.None || _completed);
            switch (_glow)
            {
                case BottleGlow.Selected: glow.color = UiColors.Selected; break;
                case BottleGlow.Hint: glow.color = UiColors.HintGlow; break;
                default: glow.color = UiColors.DoneGlow * new Color(1f, 1f, 1f, 0.85f); break;
            }
        }

        public void SetDimmed(bool dimmed)
        {
            group.alpha = dimmed ? 0.6f : 1f;
        }

        void ApplyShape(string shape)
        {
            if (shape == _shape)
                return;
            _shape = shape;
            var art = shape == LevelGenerator.FlaskShape ? cylinder : classic;
            back.sprite = art.back;
            front.sprite = art.front;
            mask.sprite = art.mask;
            glow.sprite = art.glow;
        }

        void EnsureLayers(int count)
        {
            while (_layerImages.Count < count)
            {
                var image = Instantiate(layerTemplate, layers);
                image.gameObject.name = "Layer " + _layerImages.Count;
                image.gameObject.SetActive(true);
                _layerImages.Add(image);
                var mark = Instantiate(hiddenTemplate, layers);
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
