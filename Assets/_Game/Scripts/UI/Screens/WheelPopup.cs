using System;
using BottleSort.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BottleSort.UI
{
    // Колесо удачи из Flutter: 6 равных секторов, вращение 2.6 с с замедлением.
    // Приз выбирается и начисляется до анимации, колесо только докручивается до него.
    public sealed class WheelPopup : UiScreen
    {
        const float SpinSeconds = 2.6f;
        const int FullTurns = 5;

        [SerializeField] Button closeButton;
        [SerializeField] Button dimmer;
        [SerializeField] RectTransform wheel;
        [SerializeField] Button spinButton;
        [SerializeField] LocalizedText spinLabel;
        [SerializeField] GameObject adIcon;
        [SerializeField] LocalizedText spinsLeft;
        [SerializeField] TMP_Text result;

        readonly System.Random _random = new System.Random();
        bool _spinning;
        float _angle;

        public override ScreenId Id => ScreenId.Wheel;
        public override bool IsPopup => true;

        void Awake()
        {
            closeButton.onClick.AddListener(Close);
            dimmer.onClick.AddListener(Close);
            spinButton.onClick.AddListener(OnSpinPressed);
        }

        protected override void OnOpen()
        {
            DailyRewards.RefreshWheel(PlayerData.Current, DateTime.Now);
            result.text = "";
            Refresh();
        }

        public override bool OnBack()
        {
            Close();
            return true;
        }

        void Close()
        {
            if (!_spinning)
                Router.ClosePopup(this);
        }

        void Refresh()
        {
            int spins = PlayerData.Current.wheelSpins;
            bool free = spins > 0;
            spinLabel.SetKey(free ? "wheel_spin" : "wheel_spin_ad");
            adIcon.SetActive(!free);
            spinsLeft.SetKey("wheel_spins_left", spins);
            spinButton.interactable = !_spinning;
        }

        void OnSpinPressed()
        {
            if (_spinning)
                return;
            if (PlayerData.Current.wheelSpins > 0)
            {
                Spin();
                return;
            }

            spinButton.interactable = false;
            RewardedAds.Show(watched =>
            {
                if (watched)
                {
                    DailyRewards.AddSpin(PlayerData.Current);
                    Spin();
                }
                else
                {
                    Refresh();
                }
            });
        }

        void Spin()
        {
            int sector = DailyRewards.Spin(PlayerData.Current, _random);
            if (sector < 0)
            {
                Refresh();
                return;
            }

            _spinning = true;
            result.text = "";
            Refresh();
            float sectorAngle = 360f / EconomyRules.WheelPrizes.Length;
            // Не точно в центр сектора, чтобы каждое вращение выглядело по-своему.
            float jitter = ((float)_random.NextDouble() - 0.5f) * sectorAngle * 0.6f;
            float from = _angle % 360f;
            float to = 360f * FullTurns + sector * sectorAngle + jitter;
            Tweens.Run(wheel, "spin", SpinSeconds, t =>
            {
                _angle = Mathf.LerpUnclamped(from, to, t);
                wheel.localRotation = Quaternion.Euler(0f, 0f, _angle);
            }, Ease.OutCubic, () => Finish(sector));
        }

        void Finish(int sector)
        {
            _spinning = false;
            var prize = EconomyRules.WheelPrizes[sector];
            result.text = PrizeText(prize);
            Tweens.Scale(result.transform, Vector3.one * 1.15f, 0.15f, Ease.OutBack,
                () => Tweens.Scale(result.transform, Vector3.one, 0.2f));
            UiFeedback.Play(prize.Coins > 0 ? UiSound.Coin : UiSound.Reward);
            Haptics.Impact(HapticStrength.Medium);
            Refresh();
        }

        public static string PrizeText(RewardBundle prize)
        {
            if (prize.Coins > 0) return Loc.Format("wheel_won_coins", prize.Coins);
            if (prize.Undo > 0) return Loc.Get("wheel_won_undo");
            if (prize.Hints > 0) return Loc.Get("wheel_won_hint");
            if (prize.Bottles > 0) return Loc.Get("wheel_won_bottle");
            return Loc.Get("wheel_won_spin");
        }
    }
}
