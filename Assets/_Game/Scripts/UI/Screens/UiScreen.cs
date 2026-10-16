using UnityEngine;

namespace BottleSort.UI
{
    public enum ScreenId
    {
        Splash,
        Menu,
        Levels,
        Game,
        Win,
        Shop,
        Settings,
        Gift
    }

    // Экран — панель в сцене Main. Переход: fade + лёгкий scale за 0.25 с.
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class UiScreen : MonoBehaviour
    {
        public const float TransitionTime = 0.25f;

        [SerializeField] RectTransform transitionRoot;

        CanvasGroup _group;

        public abstract ScreenId Id { get; }
        // Попап открывается поверх текущего экрана и не прячет его.
        public virtual bool IsPopup => false;
        public virtual Theme? BackdropTheme => null;
        public ScreenRouter Router { get; private set; }
        public bool IsOpen { get; private set; }

        protected CanvasGroup Group => _group != null ? _group : _group = GetComponent<CanvasGroup>();
        RectTransform Content => transitionRoot != null ? transitionRoot : (RectTransform)transform;

        public void Attach(ScreenRouter router)
        {
            Router = router;
            gameObject.SetActive(false);
        }

        public void Open(bool animated)
        {
            IsOpen = true;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Group.blocksRaycasts = true;
            OnOpen();
            if (!animated)
            {
                Group.alpha = 1f;
                Content.localScale = Vector3.one;
                return;
            }

            Group.alpha = 0f;
            Content.localScale = Vector3.one * (IsPopup ? 0.85f : 1.04f);
            Tweens.Fade(Group, 1f, TransitionTime);
            Tweens.Scale(Content, Vector3.one, TransitionTime, IsPopup ? Ease.OutBack : Ease.OutCubic);
        }

        public void Close(bool animated)
        {
            if (!IsOpen)
                return;
            IsOpen = false;
            Group.blocksRaycasts = false;
            OnClose();
            if (!animated)
            {
                gameObject.SetActive(false);
                return;
            }

            Tweens.Fade(Group, 0f, TransitionTime, Ease.OutCubic, () =>
            {
                if (!IsOpen)
                    gameObject.SetActive(false);
            });
            Tweens.Scale(Content, Vector3.one * (IsPopup ? 0.9f : 0.97f), TransitionTime);
        }

        protected virtual void OnOpen() { }
        protected virtual void OnClose() { }

        // Системная «назад» (Android). true — экран обработал сам.
        public virtual bool OnBack() => false;
    }
}
