using System;
using UnityEngine;
using UnityEngine.UI;

namespace BottleSort.UI
{
    public sealed class ToggleSwitch : MonoBehaviour
    {
        [SerializeField] Button button;
        [SerializeField] UiGradient track;
        [SerializeField] RectTransform knob;
        [SerializeField] float travel = 40f;

        public bool Value { get; private set; }
        public event Action<bool> Changed;

        void Awake() => button.onClick.AddListener(() =>
        {
            SetValue(!Value, true);
            Changed?.Invoke(Value);
        });

        public void SetValue(bool value, bool animated)
        {
            Value = value;
            if (value)
                track.SetColors(UiColors.Hex("#9BFF4F"), UiColors.Hex("#17A523"));
            else
                track.SetColors(new Color(0f, 0f, 0f, 0.35f), new Color(0f, 0f, 0f, 0.35f));
            var target = new Vector2(value ? travel : -travel, 0f);
            if (animated)
                Tweens.Move(knob, target, 0.18f, Ease.OutBack);
            else
                knob.anchoredPosition = target;
        }
    }
}
