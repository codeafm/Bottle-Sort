using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BottleSort.UI
{
    public sealed class LevelNode : MonoBehaviour
    {
        public enum State
        {
            Done,
            Current,
            Locked,
            Boss
        }

        [SerializeField] Button button;
        [SerializeField] Image face;
        [SerializeField] RectTransform faceRect;
        [SerializeField] TMP_Text number;
        [SerializeField] GameObject starsRoot;
        [SerializeField] Image[] stars;
        [SerializeField] GameObject lockIcon;
        [SerializeField] GameObject crown;
        [SerializeField] GameObject ring;
        [SerializeField] GameObject playBubble;
        [SerializeField] GameObject tutorialLabel;
        [SerializeField] Sprite doneSprite;
        [SerializeField] Sprite currentSprite;
        [SerializeField] Sprite lockedSprite;
        [SerializeField] Sprite bossSprite;
        [SerializeField] Sprite starOn;
        [SerializeField] Sprite starOff;

        int _level;

        public event Action<int> Pressed;

        void Awake() => button.onClick.AddListener(() => Pressed?.Invoke(_level));

        public void Setup(int level, State state, int earnedStars, bool tutorial)
        {
            _level = level;
            number.text = level.ToString();

            // Размеры из Levels.dc.html: текущий 78, босс 56, остальные 58.
            float size = state == State.Current ? 78f : state == State.Boss ? 64f : 58f;
            faceRect.sizeDelta = new Vector2(size, size) * 2.5f;

            switch (state)
            {
                case State.Done:
                    face.sprite = doneSprite;
                    number.color = UiColors.Hex("#5A2A00");
                    number.fontSize = 55f;
                    break;
                case State.Current:
                    face.sprite = currentSprite;
                    number.color = Color.white;
                    number.fontSize = 75f;
                    break;
                case State.Boss:
                    face.sprite = bossSprite;
                    number.color = Color.white;
                    number.fontSize = 45f;
                    break;
                default:
                    face.sprite = lockedSprite;
                    number.color = UiColors.Hex("#DCE8FF", 0.85f);
                    number.fontSize = 40f;
                    break;
            }

            starsRoot.SetActive(state == State.Done);
            for (int i = 0; i < stars.Length; i++)
                stars[i].sprite = i < earnedStars ? starOn : starOff;
            lockIcon.SetActive(state == State.Locked);
            crown.SetActive(state == State.Boss);
            ring.SetActive(state == State.Current);
            playBubble.SetActive(state == State.Current);
            tutorialLabel.SetActive(tutorial && state == State.Done);
            number.rectTransform.anchoredPosition = state == State.Locked || state == State.Boss
                ? new Vector2(0f, -14f) : Vector2.zero;
        }
    }
}
