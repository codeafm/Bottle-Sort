using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BottleSort.UI
{
    // Две кнопки в карточке: сверху зелёная «за рекламу» с иконкой видео, под ней синяя обычная
    // («Забрать», «Заново»). Вторую кнопку делаем копией первой прямо в игре, а карточку вытягиваем
    // вниз на её высоту, поэтому сцены и префабы трогать не нужно. В одиночном режиме всё как было.
    public sealed class AdChoiceButtons
    {
        const float Gap = 12f;

        readonly Button _primary;
        readonly RectTransform _panel;
        readonly Vector2 _primaryOffsetMin;
        readonly Vector2 _primaryOffsetMax;
        readonly Vector2 _panelSize;
        readonly float _step;

        Button _secondary;
        LocalizedText _secondaryLabel;
        Image _videoIcon;
        Action _onSecondary;

        public AdChoiceButtons(Button primary)
        {
            _primary = primary;
            _panel = (RectTransform)primary.transform.parent;
            var rect = (RectTransform)primary.transform;
            _primaryOffsetMin = rect.offsetMin;
            _primaryOffsetMax = rect.offsetMax;
            _panelSize = _panel.sizeDelta;
            // Кнопка прижата к низу карточки, её высота — разница отступов (rect у скрытого попапа ещё не посчитан).
            _step = (_primaryOffsetMax.y - _primaryOffsetMin.y) + Gap * UiScale;
        }

        // Мокап 390 pt растянут на 975 единиц Canvas.
        static float UiScale => 2.5f;

        public void ShowSingle()
        {
            if (_secondary != null)
                _secondary.gameObject.SetActive(false);
            if (_videoIcon != null)
                _videoIcon.gameObject.SetActive(false);
            Restore();
        }

        public void ShowPair(string secondaryKey, Action onSecondary)
        {
            EnsureSecondary();
            _onSecondary = onSecondary;
            _secondaryLabel.SetKey(secondaryKey);
            _secondary.gameObject.SetActive(true);
            _secondary.interactable = true;

            Restore();
            _panel.sizeDelta = _panelSize + new Vector2(0f, _step);
            var primaryRect = (RectTransform)_primary.transform;
            primaryRect.offsetMin = _primaryOffsetMin + new Vector2(0f, _step);
            primaryRect.offsetMax = _primaryOffsetMax + new Vector2(0f, _step);
            ShowVideoIcon();
        }

        void Restore()
        {
            _panel.sizeDelta = _panelSize;
            var primaryRect = (RectTransform)_primary.transform;
            primaryRect.offsetMin = _primaryOffsetMin;
            primaryRect.offsetMax = _primaryOffsetMax;
        }

        void EnsureSecondary()
        {
            if (_secondary != null)
                return;
            var copy = UnityEngine.Object.Instantiate(_primary.gameObject, _panel);
            copy.name = _primary.gameObject.name + " Secondary";
            _secondary = copy.GetComponent<Button>();
            _secondary.onClick.RemoveAllListeners();
            _secondary.onClick.AddListener(() => _onSecondary?.Invoke());
            _secondaryLabel = copy.GetComponentInChildren<LocalizedText>(true);

            var face = copy.transform.Find("Face");
            var gradient = face != null ? face.GetComponent<UiGradient>() : null;
            if (gradient != null)
                gradient.SetColors(UiColors.Hex("#35C7FF"), UiColors.Hex("#0870E8"));
            var depth = copy.transform.Find("Depth");
            if (depth != null && depth.TryGetComponent(out Image depthImage))
                depthImage.color = UiColors.Hex("#0548A8");
            if (_secondaryLabel != null && _secondaryLabel.TryGetComponent(out TMP_Text text))
                text.fontSize *= 0.85f;
        }

        // Иконка видео — та же, что у «×2» на победе и «Ещё попытки» колеса; в каждой сцене она уже загружена.
        void ShowVideoIcon()
        {
            if (_videoIcon == null)
            {
                var sprite = FindSprite("icon_video");
                var label = _primary.GetComponentInChildren<TMP_Text>(true);
                if (sprite == null || label == null)
                    return;
                var go = new GameObject("VideoIcon", typeof(RectTransform), typeof(Image));
                var rect = (RectTransform)go.transform;
                rect.SetParent(label.transform.parent, false);
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
                rect.pivot = new Vector2(0f, 0.5f);
                rect.sizeDelta = new Vector2(26f, 26f) * UiScale;
                rect.anchoredPosition = new Vector2(18f * UiScale, 0f);
                _videoIcon = go.GetComponent<Image>();
                _videoIcon.sprite = sprite;
                _videoIcon.raycastTarget = false;
            }
            _videoIcon.gameObject.SetActive(true);
        }

        static Sprite FindSprite(string name)
        {
            foreach (var sprite in Resources.FindObjectsOfTypeAll<Sprite>())
                if (sprite.name == name)
                    return sprite;
            return null;
        }
    }
}
