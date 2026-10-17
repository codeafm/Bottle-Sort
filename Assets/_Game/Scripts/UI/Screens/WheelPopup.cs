using System;
using BottleSort.Gameplay;
using BottleSort.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;

namespace BottleSort.UI
{
    // Колесо удачи из Wheel.dc.html. Приз выбирают и начисляют services (DailyRewards.Spin) до анимации,
    // колесо только докручивается до него: 6 оборотов за 4.6 с, щелчки стрелки на каждом разделителе,
    // потом подсветка сектора, монеты из центра, конфетти и карточка выигрыша.
    public sealed class WheelPopup : UiScreen
    {
        const float SpinSeconds = 4.6f;
        const int FullTurns = 6;
        const float SectorAngle = 60f;
        const float RaysIdleTurn = 26f;
        const float RaysSpinTurn = 6f;
        const float BulbsIdle = 0.9f;
        const float BulbsSpin = 0.18f;
        const float TickSeconds = 0.13f;
        const float TickAngle = -14f;
        const float TickSoundInterval = 0.04f;
        const float BurstSeconds = 1.1f;

        // Сектора на колесе идут как в макете (50, отмена, подсказка, 150, бутылка, попытка),
        // а призы — в порядке EconomyRules.WheelPrizes. Здесь — какой приз нарисован в секторе.
        public static readonly int[] PrizeOfSector = { 0, 1, 2, 4, 3, 5 };

        static readonly CubicBezier SpinCurve = new CubicBezier(0.1f, 0.72f, 0.06f, 1f);
        static readonly CubicBezier PopCurve = new CubicBezier(0.34f, 1.56f, 0.64f, 1f);
        static readonly CubicBezier BurstCurve = new CubicBezier(0.2f, 0.8f, 0.3f, 1f);
        // Плитка награды в карточке: цвет сектора сверху вниз и свечение вокруг.
        static readonly string[,] TileColors =
        {
            { "#8C5CF0", "#4A1F9E", "#8C5CF0" }, { "#3FA8FF", "#0B4FB0", "#3FA8FF" }, { "#43C96A", "#11743A", "#43C96A" },
            { "#F0478E", "#A3134F", "#F0478E" }, { "#2CC6D4", "#0A6E79", "#2CC6D4" }, { "#FF9C3A", "#B4520A", "#FF9C3A" }
        };
        // Куда разлетаются 14 монет из центра колеса, pt, и с какой задержкой.
        static readonly Vector3[] Burst =
        {
            new Vector3(150, 40, 0f), new Vector3(135, -25, 0.05f), new Vector3(94, -77, 0.1f), new Vector3(33, -106, 0.15f),
            new Vector3(-33, -106, 0f), new Vector3(-94, -77, 0.05f), new Vector3(-135, -25, 0.1f), new Vector3(-150, 40, 0.15f),
            new Vector3(-135, 105, 0f), new Vector3(-94, 157, 0.05f), new Vector3(-33, 186, 0.1f), new Vector3(33, 186, 0.15f),
            new Vector3(94, 157, 0f), new Vector3(135, 105, 0.05f)
        };

        [SerializeField] BackdropBlur backdropBlur;
        [SerializeField] CanvasGroup contentGroup;
        [SerializeField] Button closeButton;
        [SerializeField] RectTransform ribbon;
        [SerializeField] RectTransform rays;
        [SerializeField] RectTransform wheelFace;
        [SerializeField] TMP_Text[] sectorLabels;
        [SerializeField] Image[] bulbs;
        [SerializeField] GameObject[] bulbGlows;
        [SerializeField] RectTransform pointer;
        [SerializeField] CanvasGroup winHighlight;
        [SerializeField] LocalizedText spinsLeft;
        [SerializeField] Button spinButton;
        [SerializeField] CanvasGroup spinGroup;
        [SerializeField] Button adButton;
        [SerializeField] RectTransform[] burstCoins;
        [SerializeField] RectTransform confetti;
        [SerializeField] CanvasGroup rewardLayer;
        [SerializeField] RectTransform rewardCard;
        [SerializeField] UiGradient rewardTile;
        [SerializeField] Image rewardTileGlow;
        [SerializeField] GameObject rewardCoin;
        [SerializeField] Image rewardIcon;
        [SerializeField] TMP_Text rewardAmount;
        [SerializeField] LocalizedText rewardName;
        [SerializeField] Button collectButton;
        [SerializeField] RectTransform[] flyingCoins;
        [SerializeField] Sprite undoIcon;
        [SerializeField] Sprite hintIcon;
        [SerializeField] Sprite bottleIcon;
        [SerializeField] Sprite spinIcon;

        readonly System.Random _random = new System.Random();
        float _angle;
        float _raysAngle;
        float _bulbTime;
        int _lastDivider;
        float _tickTime = -1f;
        float _lastTickSound = -1f;
        bool _spinning;
        bool _rewardShown;
        int _prize = -1;
        Vector2[] _confettiSpeed;
        float _fall;

        public override ScreenId Id => ScreenId.Wheel;
        public override bool IsPopup => true;
        public bool IsSpinning => _spinning;

        void Awake()
        {
            closeButton.onClick.AddListener(Close);
            spinButton.onClick.AddListener(OnSpinPressed);
            adButton.onClick.AddListener(OnSpinPressed);
            collectButton.onClick.AddListener(Collect);
            for (int sector = 0; sector < sectorLabels.Length; sector++)
                sectorLabels[sector].text = AmountText(EconomyRules.WheelPrizes[PrizeOfSector[sector]]);

            _confettiSpeed = new Vector2[confetti.childCount];
            for (int i = 0; i < _confettiSpeed.Length; i++)
                _confettiSpeed[i] = new Vector2(Random.Range(150f, 260f) * 2.5f, Random.Range(-260f, 260f));
        }

        public override void FillForEditor()
        {
            spinsLeft.SetKey("wheel_spins_left", 1);
            for (int sector = 0; sector < sectorLabels.Length; sector++)
                sectorLabels[sector].text = AmountText(EconomyRules.WheelPrizes[PrizeOfSector[sector]]);
        }

        protected override void OnOpen()
        {
            backdropBlur.Grab(contentGroup);
            DailyRewards.RefreshWheel(PlayerData.Current, DateTime.Now);
            HideReward(false);
            Refresh();

            // Лента с пружинкой: scale 0.6 → 1 за 0.45 с.
            ribbon.localScale = Vector3.one * 0.6f;
            Tweens.Run(ribbon, "in", 0.45f, t => ribbon.localScale = Vector3.one * Mathf.LerpUnclamped(0.6f, 1f, PopCurve.Evaluate(t)),
                Ease.Linear);
        }

        public override bool OnBack()
        {
            if (_rewardShown)
                Collect();
            else
                Close();
            return true;
        }

        void Close()
        {
            if (!_spinning && !_rewardShown)
                Router.ClosePopup(this);
        }

        void Refresh()
        {
            int spins = PlayerData.Current.wheelSpins;
            spinsLeft.SetKey("wheel_spins_left", spins);
            bool free = spins > 0 || _spinning;
            spinButton.gameObject.SetActive(free);
            adButton.gameObject.SetActive(!free);
            spinGroup.alpha = _spinning ? 0.6f : 1f;
            spinGroup.interactable = !_spinning;
            spinGroup.blocksRaycasts = !_spinning;
        }

        void OnSpinPressed()
        {
            if (_spinning || _rewardShown)
                return;
            if (PlayerData.Current.wheelSpins > 0)
            {
                Spin();
                return;
            }

            if (!AdLoadingHint.Check(adButton))
                return;
            adButton.interactable = false;
            RewardedAds.Show(watched =>
            {
                adButton.interactable = true;
                if (watched)
                    DailyRewards.AddSpin(PlayerData.Current);
                Refresh();
            });
        }

        void Spin()
        {
            int prize = DailyRewards.Spin(PlayerData.Current, _random);
            if (prize < 0)
            {
                Refresh();
                return;
            }

            _prize = prize;
            _spinning = true;
            Refresh();
            int sector = Array.IndexOf(PrizeOfSector, prize);
            // Не точно в центр сектора, чтобы каждое вращение выглядело по-своему.
            float jitter = ((float)_random.NextDouble() - 0.5f) * SectorAngle * 0.5f;
            float from = _angle;
            // Колесо крутится по часовой стрелке, то есть угол z уменьшается; сектор i встаёт под стрелку при z = 60·i.
            // Ближайший такой угол не больше текущего, и ещё 6 полных оборотов сверху.
            float target = sector * SectorAngle + jitter;
            float to = from - Mathf.Repeat(from - target, 360f) - 360f * FullTurns;
            _lastDivider = DividerIndex(from);
            Tweens.Run(wheelFace, "spin", SpinSeconds, t =>
            {
                _angle = Mathf.LerpUnclamped(from, to, SpinCurve.Evaluate(t));
                wheelFace.localRotation = Quaternion.Euler(0f, 0f, _angle);
                int divider = DividerIndex(_angle);
                if (divider != _lastDivider)
                {
                    _lastDivider = divider;
                    Tick();
                }
            }, Ease.Linear, () =>
            {
                UiFeedback.Play(UiSound.WheelStop);
                Tweens.Delay(this, "stop", 0.15f, Stop);
            });
        }

        // Разделители стоят на ±30° от центров секторов; считаем, сколько их прошло под стрелкой.
        static int DividerIndex(float angle) => Mathf.FloorToInt((angle + SectorAngle / 2f) / SectorAngle);

        // В начале вращения штырьки пролетают быстрее, чем раз в 40 мс: лишние щелчки пропускаем, стрелка качается всё равно.
        void Tick()
        {
            _tickTime = 0f;
            float now = Time.unscaledTime;
            if (now - _lastTickSound < TickSoundInterval)
                return;
            _lastTickSound = now;
            UiFeedback.Play(UiSound.WheelTick, Random.Range(0.95f, 1.08f));
        }

        void Stop()
        {
            _spinning = false;
            UiFeedback.Play(UiSound.Reward);
            Haptics.Impact(HapticStrength.Medium);
            ShowReward(EconomyRules.WheelPrizes[_prize], Array.IndexOf(PrizeOfSector, _prize));
            Refresh();
        }

        void ShowReward(RewardBundle prize, int sector)
        {
            _rewardShown = true;
            winHighlight.gameObject.SetActive(true);
            rewardTile.SetColors(UiColors.Hex(TileColors[sector, 0]), UiColors.Hex(TileColors[sector, 1]));
            var glow = UiColors.Hex(TileColors[sector, 2]);
            glow.a = 0.8f;
            rewardTileGlow.color = glow;
            rewardCoin.SetActive(prize.Coins > 0);
            rewardIcon.gameObject.SetActive(prize.Coins == 0);
            rewardIcon.sprite = IconOf(prize);
            rewardAmount.text = AmountText(prize);
            rewardName.SetKey(NameKey(prize));

            PlayBurst();
            confetti.gameObject.SetActive(true);
            _fall = ((RectTransform)transform).rect.height + 300f;
            for (int i = 0; i < confetti.childCount; i++)
            {
                var piece = (RectTransform)confetti.GetChild(i);
                piece.anchoredPosition = new Vector2(piece.anchoredPosition.x, Random.Range(-_fall * 0.4f, 300f));
            }

            rewardLayer.gameObject.SetActive(true);
            rewardLayer.alpha = 0f;
            rewardLayer.blocksRaycasts = true;
            Tweens.Fade(rewardLayer, 1f, 0.2f);
            // popIn: 0.4 → 1.08 → 1 за 0.5 с.
            rewardCard.localScale = Vector3.one * 0.4f;
            Tweens.Run(rewardCard, "pop", 0.5f, t => rewardCard.localScale = Vector3.one * Mathf.LerpUnclamped(0.4f, 1f, PopCurve.Evaluate(t)),
                Ease.Linear);
        }

        void PlayBurst()
        {
            for (int i = 0; i < burstCoins.Length && i < Burst.Length; i++)
            {
                var coin = burstCoins[i];
                var group = coin.GetComponent<CanvasGroup>();
                var to = new Vector2(Burst[i].x, Burst[i].y) * 2.5f;
                coin.gameObject.SetActive(true);
                coin.anchoredPosition = Vector2.zero;
                group.alpha = 0f;
                Tweens.Run(coin, "burst", BurstSeconds, t =>
                {
                    float k = BurstCurve.Evaluate(t);
                    coin.anchoredPosition = Vector2.LerpUnclamped(Vector2.zero, to, k);
                    coin.localScale = Vector3.one * Mathf.Lerp(0.3f, 1f, k);
                    coin.localRotation = Quaternion.Euler(0f, 0f, -260f * k);
                    group.alpha = t < 0.15f ? t / 0.15f : 1f - (t - 0.15f) / 0.85f;
                }, Ease.Linear, () => coin.gameObject.SetActive(false), Burst[i].z);
            }
        }

        void Collect()
        {
            if (!_rewardShown)
                return;
            var prize = EconomyRules.WheelPrizes[_prize];
            if (prize.Coins > 0)
                FlyCoinsToCounter();
            HideReward(true);
            Refresh();
        }

        // Монеты из карточки летят в счётчик меню под колесом.
        void FlyCoinsToCounter()
        {
            var menu = Router.Get<MainMenuScreen>();
            if (menu == null)
                return;
            var layer = (RectTransform)flyingCoins[0].parent;
            Vector2 from = layer.InverseTransformPoint(rewardCoin.transform.position);
            Vector2 to = layer.InverseTransformPoint(menu.CoinsTarget.position);
            for (int i = 0; i < flyingCoins.Length; i++)
            {
                var coin = flyingCoins[i];
                coin.gameObject.SetActive(true);
                coin.anchoredPosition = from;
                var bend = new Vector2(Random.Range(-120f, 120f), Random.Range(40f, 160f));
                bool last = i == flyingCoins.Length - 1;
                Tweens.Run(coin, "fly", 0.6f, t =>
                {
                    // Квадратичная кривая с изгибом, чтобы монеты летели веером, а не по одной линии.
                    var mid = (from + to) / 2f + bend;
                    var a = Vector2.Lerp(from, mid, t);
                    var b = Vector2.Lerp(mid, to, t);
                    coin.anchoredPosition = Vector2.Lerp(a, b, t);
                    coin.localScale = Vector3.one * Mathf.Lerp(1f, 0.6f, t);
                }, Ease.InOutSine, () =>
                {
                    coin.gameObject.SetActive(false);
                    UiFeedback.Play(UiSound.Coin);
                    if (last)
                        Haptics.Light();
                }, i * 0.06f);
            }
        }

        void HideReward(bool animated)
        {
            _rewardShown = false;
            winHighlight.gameObject.SetActive(false);
            confetti.gameObject.SetActive(false);
            foreach (var coin in burstCoins)
            {
                Tweens.Kill(coin);
                coin.gameObject.SetActive(false);
            }
            rewardLayer.blocksRaycasts = false;
            if (animated)
                Tweens.Fade(rewardLayer, 0f, 0.2f, Ease.OutCubic, () => rewardLayer.gameObject.SetActive(false));
            else
                rewardLayer.gameObject.SetActive(false);
        }

        void Update()
        {
            if (!IsOpen)
                return;
            float dt = Time.unscaledDeltaTime;

            _raysAngle -= 360f / (_spinning ? RaysSpinTurn : RaysIdleTurn) * dt;
            rays.localRotation = Quaternion.Euler(0f, 0f, _raysAngle);

            // Лампочки мигают через одну: steps(1), первая половина периода горит группа A.
            float period = _spinning ? BulbsSpin : BulbsIdle;
            _bulbTime = Mathf.Repeat(_bulbTime + dt, period);
            bool groupA = _bulbTime < period / 2f;
            for (int i = 0; i < bulbs.Length; i++)
            {
                bool on = (i % 2 == 0) == groupA;
                bulbs[i].color = on ? UiColors.Hex("#FFF6C8") : UiColors.Hex("#8A6A2A");
                bulbGlows[i].SetActive(on);
            }

            // Щелчок стрелки: качнулась на −14° и вернулась за 0.13 с.
            if (_tickTime >= 0f)
            {
                _tickTime += dt;
                float k = Mathf.Clamp01(_tickTime / TickSeconds);
                pointer.localRotation = Quaternion.Euler(0f, 0f, TickAngle * Mathf.Sin(k * Mathf.PI));
                if (k >= 1f)
                    _tickTime = -1f;
            }

            if (winHighlight.gameObject.activeSelf)
                winHighlight.alpha = 0.35f + 0.55f * (1f - Mathf.Cos(Time.unscaledTime / 0.8f * Mathf.PI * 2f)) / 2f;

            if (confetti.gameObject.activeSelf)
            {
                for (int i = 0; i < confetti.childCount; i++)
                {
                    var piece = (RectTransform)confetti.GetChild(i);
                    var position = piece.anchoredPosition;
                    position.y -= _confettiSpeed[i].x * dt;
                    if (position.y < -_fall)
                        position.y += _fall + 150f;
                    piece.anchoredPosition = position;
                    piece.Rotate(0f, 0f, _confettiSpeed[i].y * dt);
                }
            }
        }

        // Для снимков в тестах: колесо в состоянии «крутится» или «выигрыш» без ожидания анимации.
        public void ShowSpinningFrame(float angle)
        {
            _spinning = true;
            _angle = angle;
            wheelFace.localRotation = Quaternion.Euler(0f, 0f, angle);
            Refresh();
        }

        public void ShowRewardFrame(int prize)
        {
            _spinning = false;
            _prize = prize;
            int sector = Array.IndexOf(PrizeOfSector, prize);
            _angle = sector * SectorAngle;
            wheelFace.localRotation = Quaternion.Euler(0f, 0f, _angle);
            Refresh();
            ShowReward(EconomyRules.WheelPrizes[prize], sector);
        }

        static string AmountText(RewardBundle prize)
        {
            if (prize.Coins > 0) return "+" + prize.Coins;
            return "+" + Mathf.Max(1, prize.Undo + prize.Hints + prize.Bottles + prize.Spins);
        }

        static string NameKey(RewardBundle prize)
        {
            if (prize.Coins > 0)
            {
                int top = 0;
                foreach (var other in EconomyRules.WheelPrizes)
                    top = Mathf.Max(top, other.Coins);
                return prize.Coins >= top ? "wheel_prize_jackpot" : "wheel_prize_coins";
            }
            if (prize.Undo > 0) return "wheel_prize_undo";
            if (prize.Hints > 0) return "wheel_prize_hint";
            if (prize.Bottles > 0) return "wheel_prize_bottle";
            return "wheel_prize_spin";
        }

        Sprite IconOf(RewardBundle prize)
        {
            if (prize.Undo > 0) return undoIcon;
            if (prize.Hints > 0) return hintIcon;
            if (prize.Bottles > 0) return bottleIcon;
            return spinIcon;
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
