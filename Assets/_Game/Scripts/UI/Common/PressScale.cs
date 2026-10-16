using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BottleSort.UI
{
    // Нажатие: scale 0.95, звук и лёгкая вибрация. Масштабируется визуальная часть, а не зона касания.
    public sealed class PressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        const float PressedScale = 0.95f;

        [SerializeField] Transform visual;
        [SerializeField] bool silent;

        Selectable _selectable;
        bool _pressed;

        void Awake()
        {
            _selectable = GetComponent<Selectable>();
            if (visual == null)
                visual = transform;
        }

        void OnDisable()
        {
            _pressed = false;
            Tweens.Kill(visual, "scale");
            visual.localScale = Vector3.one;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_selectable != null && !_selectable.IsInteractable())
                return;
            _pressed = true;
            Tweens.Scale(visual, Vector3.one * PressedScale, 0.08f);
            if (!silent)
                UiFeedback.Tap();
        }

        public void OnPointerUp(PointerEventData eventData) => Release();

        public void OnPointerExit(PointerEventData eventData) => Release();

        void Release()
        {
            if (!_pressed)
                return;
            _pressed = false;
            Tweens.Scale(visual, Vector3.one, 0.18f, Ease.OutBack);
        }
    }
}
