using BottleSort.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BottleSort.UI
{
    // Шапка игрового экрана. Все блоки фиксированного размера: на любом уровне шапка одна и та же,
    // меняется только содержимое, поэтому поле под ней не сдвигается.
    public sealed class GameHud : MonoBehaviour
    {
        [SerializeField] Button homeButton;
        [SerializeField] TMP_Text levelNumber;
        [SerializeField] LocalizedText tagLabel;
        [SerializeField] UiGradient tagBackground;
        [SerializeField] TMP_Text coins;
        [SerializeField] LocalizedText collected;
        [SerializeField] RectTransform progressFill;
        [SerializeField] CanvasGroup movesGroup;
        [SerializeField] TMP_Text moves;
        [SerializeField] Image[] stars;
        [SerializeField] Sprite starOn;
        [SerializeField] Sprite starOff;
        [SerializeField] TMP_Text info;
        [SerializeField] RectTransform infoPill;

        float _progress;

        public Button Home => homeButton;

        void OnEnable()
        {
            PlayerData.Changed += RefreshCoins;
            RefreshCoins();
        }

        void OnDisable() => PlayerData.Changed -= RefreshCoins;

        void RefreshCoins() => coins.text = Loc.Number(PlayerData.Current.coins);

        public void SetLevel(int level)
        {
            levelNumber.text = level.ToString();
            var (top, bottom) = UiColors.TagOf(level);
            tagBackground.SetColors(top, bottom);
            if (HandmadeLevels.IsTutorial(level))
            {
                tagLabel.SetKey("tag_tutorial", level, HandmadeLevels.TutorialCount);
                return;
            }

            switch (LevelPlan.TypeOf(level))
            {
                case LevelType.Special: tagLabel.SetKey("tag_special"); break;
                case LevelType.BigFlask: tagLabel.SetKey("tag_flask"); break;
                case LevelType.Rescue: tagLabel.SetKey("tag_rescue"); break;
                case LevelType.Boss: tagLabel.SetKey("tag_boss"); break;
                case LevelType.ChapterBoss: tagLabel.SetKey("tag_chapter_boss"); break;
                default: tagLabel.SetKey("tag_normal"); break;
            }
        }

        public void SetProgress(int done, int total, bool animated)
        {
            collected.SetKey("hud_collected", done, total);
            float target = total > 0 ? Mathf.Clamp01(done / (float)total) : 0f;
            if (!animated)
            {
                _progress = target;
                ApplyProgress(target);
                return;
            }
            float from = _progress;
            _progress = target;
            Tweens.Run(progressFill, "progress", 0.4f, t => ApplyProgress(Mathf.Lerp(from, target, t)));
        }

        void ApplyProgress(float value)
        {
            progressFill.anchorMax = new Vector2(value, 1f);
            progressFill.gameObject.SetActive(value > 0.001f);
        }

        // На обучении ходы не считаются: колонка остаётся на месте, но пустая.
        public void SetMoves(int count, bool visible)
        {
            movesGroup.alpha = visible ? 1f : 0f;
            moves.text = count.ToString();
        }

        public void SetStars(int count)
        {
            for (int i = 0; i < stars.Length; i++)
                stars[i].sprite = i < count ? starOn : starOff;
        }

        public void SetInfo(string text, bool animated = true)
        {
            if (animated && info.text == text)
                return;
            if (!animated)
            {
                Tweens.Kill(info);
                ApplyInfo(text);
                info.alpha = 1f;
                return;
            }
            Tweens.Fade(info, 0f, 0.1f, () =>
            {
                ApplyInfo(text);
                Tweens.Fade(info, 1f, 0.1f);
            });
        }

        // Плашка облегает текст, но не шире строки.
        void ApplyInfo(string text)
        {
            info.text = text;
            // padding: 5px 12px из макета, в единицах Canvas (2.5 на pt).
            const float paddingX = 30f;
            const float paddingY = 12.5f;
            float maxWidth = info.rectTransform.rect.width;
            var size = info.GetPreferredValues(text, maxWidth, 0f);
            infoPill.sizeDelta = new Vector2(Mathf.Min(size.x, maxWidth) + paddingX * 2f, size.y + paddingY * 2f);
            infoPill.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }
    }
}
