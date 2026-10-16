using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BottleSort.UI
{
    // Ячейка поля: зона касания, подъём при выборе, тряска, галочка «собрано».
    // Картинка бутылки (IBottleView) лежит внутри visual и двигается вместе с ним.
    public sealed class BottleSlot : MonoBehaviour, IPointerDownHandler
    {
        const float LiftHeight = 22f;

        [SerializeField] RectTransform visual;
        [SerializeField] RectTransform viewHolder;
        [SerializeField] GameObject doneBadge;
        [SerializeField] RectTransform handAnchor;

        public int Index { get; private set; }
        public IBottleView View { get; private set; }
        public RectTransform HandAnchor => handAnchor;
        public RectTransform Rect => (RectTransform)transform;
        public event Action<BottleSlot> Tapped;

        bool _lifted;

        public void Init(int index, IBottleView view)
        {
            Index = index;
            View = view;
            view.Root.SetParent(viewHolder, false);
            SetLifted(false, false);
            doneBadge.SetActive(false);
        }

        public void OnPointerDown(PointerEventData eventData) => Tapped?.Invoke(this);

        public void SetLifted(bool lifted, bool animated = true)
        {
            if (_lifted == lifted && animated)
                return;
            _lifted = lifted;
            if (View != null && View.MovesItself)
                return;
            var target = new Vector2(0f, lifted ? LiftHeight : 0f);
            if (animated)
                Tweens.Move(visual, target, 0.22f, Ease.OutBack);
            else
                visual.anchoredPosition = target;
        }

        public void SetDone(bool done, bool animated)
        {
            if (doneBadge.activeSelf == done)
                return;
            doneBadge.SetActive(done);
            if (done && animated)
            {
                doneBadge.transform.localScale = Vector3.zero;
                Tweens.Scale(doneBadge.transform, Vector3.one, 0.35f, Ease.OutBack);
            }
        }

        // keyframes shake из макета: -7, 7, -5, 4 px за 0.42 с.
        public void Shake()
        {
            if (View.MovesItself)
            {
                View.Shake();
                return;
            }
            float baseY = _lifted ? LiftHeight : 0f;
            Tweens.Run(visual, "move", 0.42f, t =>
            {
                float x;
                if (t < 0.2f) x = Mathf.Lerp(0f, -7f, t / 0.2f);
                else if (t < 0.4f) x = Mathf.Lerp(-7f, 7f, (t - 0.2f) / 0.2f);
                else if (t < 0.6f) x = Mathf.Lerp(7f, -5f, (t - 0.4f) / 0.2f);
                else if (t < 0.8f) x = Mathf.Lerp(-5f, 4f, (t - 0.6f) / 0.2f);
                else x = Mathf.Lerp(4f, 0f, (t - 0.8f) / 0.2f);
                visual.anchoredPosition = new Vector2(x, baseY);
            }, Ease.Linear);
        }
    }
}
