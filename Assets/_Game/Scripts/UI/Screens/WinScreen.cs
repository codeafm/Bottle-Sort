using BottleSort.Services;
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

        public override void FillForEditor()
        {
            _result = new WinResult { Level = 24, Stars = 3, Moves = 18, Reward = 170, IsBest = true };
            ShowResult();
        }

        void ShowResult()
        {
            levelDone.SetKey("win_level_done", _result.Level);
            movesChip.SetActive(!_result.IsTutorial);
            movesLabel.SetKey("win_moves", _result.Moves);
            bestChip.SetActive(_result.IsBest);
            reward.text = "+" + Loc.Number(_result.Reward);
            // На обучении рекламы нет совсем (LEVEL_DESIGN §3.4).
            doubleButton.gameObject.SetActive(!_result.IsTutorial && _result.Reward > 0);
            doubleButton.interactable = true;
            int multiplier = EconomyRules.RewardedWinMultiplier(LevelPlan.TypeOf(_result.Level));
            doubleLabel.SetKey(multiplier == 3 ? "win_triple" : "win_double", Loc.Number(_result.Reward * multiplier));
        }

        protected override void OnOpen()
        {
            ShowResult();

            PlayStarSounds();
            ScheduleChests();

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

        float StarDelay(int index) => 0.1f + 0.25f * index;

        // Звук звезды — в момент, когда она «приземлилась», примерно на середине OutBack.
        void PlayStarSounds()
        {
            for (int i = 0; i < _result.Stars && i < stars.Length; i++)
            {
                var sound = i == 0 ? UiSound.Star1 : i == 1 ? UiSound.Star2 : UiSound.Star3;
                Tweens.Delay(stars[i].transform, "sound", StarDelay(i) + 0.2f, () => UiFeedback.Play(sound));
            }
            if (_result.Reward > 0)
                Tweens.Delay(reward, "sound", StarDelay(_result.Stars) + 0.35f, () => UiFeedback.Play(UiSound.Coin));
        }

        // Сундук начисляем сразу, а карточку показываем после звёзд. Если игрок успел уйти — монеты всё равно у него.
        void ScheduleChests()
        {
            if (_result.IsTutorial)
                return;
            var chests = Economy.CollectChests(PlayerData.Current, _result.Level, LevelPlan.TypeOf(_result.Level));
            if (chests.Count == 0)
                return;

            var total = new RewardBundle();
            int chapter = 0;
            foreach (var chest in chests)
            {
                total += chest.Reward;
                if (chest.Kind == ChestKind.Chapter)
                    chapter = chest.Number;
            }

            Tweens.Delay(this, "chest", StarDelay(stars.Length) + 0.9f, () =>
            {
                if (!IsOpen)
                    return;
                var popup = Router.Get<RewardPopup>();
                if (chapter > 0)
                    popup.Setup("chest_chapter_title", "chest_chapter_text", total, "chest_ok", null, chapter);
                else
                    popup.Setup("chest_stars_title", "chest_stars_text", total, "chest_ok", null);
                Router.ShowPopup(ScreenId.Reward);
                UiFeedback.Play(UiSound.Reward);
            });
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
            if (!AdLoadingHint.Check(doubleButton))
                return;
            doubleButton.interactable = false;
            RewardedAds.Show("Награда за победу ×2", watched =>
            {
                if (!watched)
                {
                    doubleButton.interactable = true;
                    return;
                }
                int multiplier = EconomyRules.RewardedWinMultiplier(LevelPlan.TypeOf(_result.Level));
                PlayerData.Current.AddCoins(_result.Reward * (multiplier - 1), fromAd: true);
                Analytics.CoinsEarned(_result.Reward * (multiplier - 1), "Награда за победу ×2");
                reward.text = "+" + Loc.Number(_result.Reward * multiplier);
                UiFeedback.Play(UiSound.Coin);
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
            int next = _result.Level + 1;
            nextButton.interactable = false;
            Ads.ShowInterstitialIfDue(_result.Level, LevelPlan.TypeOf(_result.Level), () =>
            {
                nextButton.interactable = true;
                Router.PlayLevel(next);
            });
        }
    }
}
