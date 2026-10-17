using TMPro;
using UnityEngine;

namespace BottleSort.UI
{
    // Плашка по ширине текста — бейджи «3», «AD», «ур. 12», «НОВИНКА»: inline-flex с padding и min-width из макета.
    // Растёт от своего pivot, поэтому бейдж в углу кнопки с pivot справа растёт влево.
    public sealed class TextPill : MonoBehaviour
    {
        [SerializeField] TMP_Text label;
        [SerializeField] float padding;
        [SerializeField] float minWidth;

        string _fitted;

        void OnEnable() => Fit();

        void LateUpdate()
        {
            if (label.text != _fitted)
                Fit();
        }

        public void Fit()
        {
            _fitted = label.text;
            var rect = (RectTransform)transform;
            float width = Mathf.Max(minWidth, label.GetPreferredValues(label.text).x + padding * 2f);
            rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);
        }
    }
}
