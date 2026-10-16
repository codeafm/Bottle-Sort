using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BottleSort.UI
{
    public struct WinResult
    {
        public int Level;
        public int Stars;
        public int Moves;
        public int Reward;
        public bool IsTutorial;
        public bool IsBest;
    }

    public sealed class WinScreen : UiScreen
    {
        [SerializeField] Image[] stars;
        [SerializeField] LocalizedText levelDone;
        [SerializeField] GameObject movesChip;
        [SerializeField] LocalizedText movesLabel;
        [SerializeField] GameObject bestChip;
        [SerializeField] TMP_Text reward;
        [SerializeField] Button doubleButton;
        [SerializeField] LocalizedText doubleLabel;
        [SerializeField] Button nextButton;
        [SerializeField] Button menuButton;
        [SerializeField] RectTransform confetti;

        WinResult _result;
        Vector2[] _confettiSpeed;
        float _fallHeight;

        public override ScreenId Id => ScreenId.Win;
        public override Theme? BackdropTheme => Theme.Amber;

        void Awake()
        {
            doubleButton.onClick.AddListener(DoubleReward);
            nextButton.onClick.AddListener(Next);
            menuButton.onClick.AddListener(() => Router.Show(ScreenId.Menu));

            _confettiSpeed = new Vector2[confetti.childCount];
            for (int i = 0; i < _confettiSpeed.Length; i++)
                _confettiSpeed[i] = new Vector2(Random.Range(220f, 420f), Random.Range(-260f, 260f));
        }

        public void Setup(WinResult result)
        {
            _result = result;
        }

        protected override void OnOpen()
        {
            levelDone.SetKey("win_level_done", _result.Level);
            movesChip.SetActive(!_result.IsTutorial);
            movesLabel.SetKey("win_moves", _result.Moves);
            bestChip.SetActive(_result.IsBest);
            reward.text = "+" + Loc.Number(_result.Reward);
            // На обучении рекламы нет совсем (LEVEL_DESIGN §3.4).
            doubleButton.gameObject.SetActive(!_result.IsTutorial && _result.Reward > 0);
            doubleButton.interactable = true;
            doubleLabel.SetKey("win_double", Loc.Number(_result.Reward * 2));

            for (int i = 0; i < stars.Length; i++)
            {
                var star = stars[i];
                bool earned = i < _result.Stars;
                star.color = earned ? Color.white : new Color(0.35f, 0.3f, 0.45f, 0.6f);
                star.transform.localScale = Vector3.zero;
                star.transform.localRotation = Quaternion.Euler(0f, 0f, 40f);
                var t = star.transform;
                Tweens.Run(t, "star", 0.5f, k =>
                {
                    t.localScale = Vector3.one * k;
                    t.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(40f, 0f, k));
                }, Ease.OutBack, null, 0.1f + 0.25f * i);
            }

            _fallHeight = ((RectTransform)transform).rect.height + 300f;
            for (int i = 0; i < confetti.childCount; i++)
            {
                var piece = (RectTransform)confetti.GetChild(i);
                piece.anchoredPosition = new Vector2(piece.anchoredPosition.x, Random.Range(-_fallHeight * 0.5f, 200f));
            }
        }

        void Update()
        {
            if (!IsOpen)
                return;
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < confetti.childCount; i++)
            {
                var piece = (RectTransform)confetti.GetChild(i);
                var position = piece.anchoredPosition;
                position.y -= _confettiSpeed[i].x * dt;
                if (position.y < -_fallHeight)
                    position.y += _fallHeight + 150f;
                piece.anchoredPosition = position;
                piece.Rotate(0f, 0f, _confettiSpeed[i].y * dt);
            }
        }

        void DoubleReward()
        {
            doubleButton.interactable = false;
            RewardedAds.Show(watched =>
            {
                if (!watched)
                {
                    doubleButton.interactable = true;
                    return;
                }
                PlayerData.Current.AddCoins(_result.Reward);
                reward.text = "+" + Loc.Number(_result.Reward * 2);
                UiFeedback.Play(UiSound.Reward);
                doubleButton.gameObject.SetActive(false);
            });
        }

        void Next()
        {
            var data = PlayerData.Current;
            if (_result.Level == 3 && !data.tutorialGiftTaken)
            {
                Router.ShowPopup(ScreenId.Gift);
                return;
            }
            Router.PlayLevel(_result.Level + 1);
        }
    }
}
