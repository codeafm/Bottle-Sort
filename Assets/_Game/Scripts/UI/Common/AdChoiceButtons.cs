using System;
using UnityEngine;
using UnityEngine.UI;

namespace BottleSort.UI
{
    // Две кнопки в карточке: сверху зелёная «за рекламу» с иконкой видео, под ней синяя обычная («Забрать», «Заново»).
    // Обе лежат в префабе карточки; в паре карточка вытягивается вниз на высоту кнопки и промежуток,
    // а зелёная поднимается над синей. В одиночном режиме всё как в префабе.
    public sealed class AdChoiceButtons : MonoBehaviour
    {
        [SerializeField] Button primary;
        [SerializeField] Button secondary;
        [SerializeField] LocalizedText secondaryLabel;
        [SerializeField] GameObject videoIcon;
        [SerializeField] RectTransform panel;
        // Промежуток между кнопками, единицы Canvas (12 pt).
        [SerializeField] float gap = 30f;

        Action _onSecondary;
        bool _ready;
        Vector2 _primaryMin;
        Vector2 _primaryMax;
        Vector2 _panelSize;

        // Карточка может получить Setup раньше своего Awake (попап ещё ни разу не открывался), поэтому всё лениво.
        void Prepare()
        {
            if (_ready)
                return;
            _ready = true;
            var rect = (RectTransform)primary.transform;
            _primaryMin = rect.offsetMin;
            _primaryMax = rect.offsetMax;
            _panelSize = panel.sizeDelta;
            secondary.onClick.AddListener(() => _onSecondary?.Invoke());
        }

        public void ShowSingle()
        {
            Prepare();
            _onSecondary = null;
            secondary.gameObject.SetActive(false);
            videoIcon.SetActive(false);
            Place(0f);
        }

        public void ShowPair(string secondaryKey, Action onSecondary)
        {
            Prepare();
            _onSecondary = onSecondary;
            secondaryLabel.SetKey(secondaryKey);
            secondary.gameObject.SetActive(true);
            secondary.interactable = true;
            videoIcon.SetActive(true);
            Place(_primaryMax.y - _primaryMin.y + gap);
        }

        void Place(float lift)
        {
            panel.sizeDelta = _panelSize + new Vector2(0f, lift);
            var rect = (RectTransform)primary.transform;
            rect.offsetMin = _primaryMin + new Vector2(0f, lift);
            rect.offsetMax = _primaryMax + new Vector2(0f, lift);
        }
    }
}
