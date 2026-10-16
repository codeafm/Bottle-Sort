using TMPro;
using UnityEngine;

namespace BottleSort.UI
{
    // Синяя плашка-уведомление: «Цвет собран», «Лёд растаял», «Не хватает монет».
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class Toast : MonoBehaviour
    {
        [SerializeField] TMP_Text label;
        [SerializeField] RectTransform body;

        CanvasGroup _group;
        Vector2 _restPosition;

        void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            _restPosition = body.anchoredPosition;
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
        }

        public void Show(string text, float seconds = 1.4f)
        {
            label.text = text;
            Tweens.Kill(this, "hide");
            body.anchoredPosition = _restPosition + new Vector2(0f, 20f);
            Tweens.Move(body, _restPosition, 0.25f);
            Tweens.Fade(_group, 1f, 0.25f);
            Tweens.Delay(this, "hide", seconds, () => Tweens.Fade(_group, 0f, 0.25f));
        }

        public void ShowKey(string key, float seconds = 1.4f) => Show(Loc.Get(key), seconds);

        public void HideNow()
        {
            Tweens.Kill(this);
            Tweens.Kill(_group);
            _group.alpha = 0f;
        }
    }
}
