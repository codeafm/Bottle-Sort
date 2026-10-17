using System.Threading.Tasks;
using BottleSort.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BottleSort.UI
{
    // Заставка из Splash.dc.html: логотип влетает, наклонённая бутылка наливает в бутылку на полке,
    // слой за слоем, потом золотое свечение и звёздочки. Держим минимум 2 с и пока грузится первый уровень.
    public sealed class SplashScreen : UiScreen
    {
        const float MinTime = 2f;
        const float StepTime = 0.75f;
        const float StreamTop = 330f;

        static readonly byte[] Order =
        {
            Palette.Red, Palette.Orange, Palette.Yellow, Palette.Green, Palette.Cyan, Palette.Blue, Palette.Purple,
            Palette.Pink
        };

        [SerializeField] RectTransform logo;
        [SerializeField] CanvasGroup logoGroup;
        [SerializeField] BottleStubView pourer;
        [SerializeField] BottleStubView target;
        [SerializeField] RectTransform stream;
        [SerializeField] UiGradient streamColors;
        [SerializeField] RectTransform ripple;
        [SerializeField] RectTransform sparks;
        [SerializeField] RectTransform progressFill;
        [SerializeField] LocalizedText loadingLabel;
        // Сколько макетных пикселей в одной единице Canvas по вертикали внутри сцены заставки.
        [SerializeField] float mockupScale = 2.5f;

        float _elapsed;
        float _stepTimer;
        int _step;
        float _shownProgress;
        Task _loading;
        bool _leaving;
        bool _frozen;

        public override ScreenId Id => ScreenId.Splash;
        public override Theme? BackdropTheme => Theme.Violet;

        protected override void OnOpen()
        {
            _elapsed = 0f;
            _step = 0;
            _stepTimer = 0f;
            _shownProgress = 0.06f;
            _leaving = false;
            AnimateLogo();
            ShowStep();

            // Первый уровень игрока строим заранее: на поздних уровнях генератор думает заметное время.
            int level = PlayerData.Current.highestUnlocked;
            _loading = LevelLoader.Get(level);
        }

        void AnimateLogo()
        {
            logoGroup.alpha = 0f;
            Tweens.Run(logo, "logo", 0.9f, t =>
            {
                float scale = t < 0.6f ? Mathf.Lerp(0.55f, 1.06f, Easing.Apply(Ease.OutCubic, t / 0.6f))
                    : Mathf.Lerp(1.06f, 1f, (t - 0.6f) / 0.4f);
                logo.localScale = new Vector3(scale, scale, 1f);
                logoGroup.alpha = Mathf.Clamp01(t / 0.6f);
            }, Ease.Linear);
        }

        void Update()
        {
            if (!IsOpen || _frozen)
                return;
            float dt = Time.unscaledDeltaTime;
            _elapsed += dt;

            _stepTimer += dt;
            if (_stepTimer >= StepTime)
            {
                _stepTimer -= StepTime;
                _step++;
                ShowStep();
            }

            float timeProgress = Mathf.Clamp01(_elapsed / MinTime);
            float loadProgress = _loading == null || _loading.IsCompleted ? 1f : 0.85f;
            _shownProgress = Mathf.MoveTowards(_shownProgress, Mathf.Min(timeProgress, loadProgress), dt * 1.5f);
            progressFill.anchorMax = new Vector2(Mathf.Max(_shownProgress, 0.06f), 1f);
            loadingLabel.SetArgs(Mathf.RoundToInt(_shownProgress * 100f));

            if (!_leaving && _shownProgress >= 0.999f)
                Leave();
        }

        // Остановить заставку на нужном шаге налива и проценте загрузки: для сравнения с макетом.
        public void Freeze(int step, float progress)
        {
            _frozen = true;
            _step = step;
            foreach (var motion in GetComponentsInChildren<IdleMotion>())
                motion.enabled = false;
            ShowStep();
            Tweens.Kill(logo, "logo");
            logo.localScale = Vector3.one;
            logoGroup.alpha = 1f;
            progressFill.anchorMax = new Vector2(progress, 1f);
            loadingLabel.SetArgs(Mathf.RoundToInt(progress * 100f));
        }

        void Leave()
        {
            _leaving = true;
            // Первый запуск — сразу в обучение, меню игрок увидит по кнопке «домой» (LEVEL_DESIGN §3.4).
            if (PlayerData.Current.IsFirstLaunch)
                Router.PlayLevel(1);
            else
                Router.Show(ScreenId.Menu);
        }

        void ShowStep()
        {
            int k = _step % 6;
            int filled = Mathf.Min(k, 4);
            bool full = k >= 4;
            byte color = Order[(_step / 6) % Order.Length];

            var bottle = new Bottle(4);
            for (int i = 0; i < filled; i++)
                bottle.Push(color);
            target.Show(bottle, 4);

            var source = new Bottle(4);
            if (!full)
            {
                source.Push(color);
                source.Push(color);
            }
            pourer.Show(source, 4);

            // Поверхность жидкости в целевой бутылке, в пикселях макета от верха экрана.
            float surface = 404f + 216f * ((226f - filled * 40f) / 240f);
            stream.gameObject.SetActive(!full);
            ripple.gameObject.SetActive(!full);
            streamColors.SetColors(UiColors.Liquid(color, 1), UiColors.Liquid(color, 0), true);
            float height = Mathf.Max(surface - StreamTop, 10f) * mockupScale;
            float from = stream.sizeDelta.y;
            Tweens.Run(stream, "height", 0.5f, t => stream.sizeDelta = new Vector2(stream.sizeDelta.x, Mathf.Lerp(from, height, t)));
            ripple.anchoredPosition = new Vector2(ripple.anchoredPosition.x, -(surface - 4f) * mockupScale);
            ripple.GetComponent<Image>().color = UiColors.Liquid(color, 1);

            target.SetCompleted(full, false);
            if (k == 4)
                Burst();
        }

        void Burst()
        {
            int count = sparks.childCount;
            for (int i = 0; i < count; i++)
            {
                var spark = (RectTransform)sparks.GetChild(i);
                float angle = i / (float)count * Mathf.PI * 2f;
                var to = new Vector2(Mathf.Cos(angle) * 80f, Mathf.Sin(angle) * 90f) * mockupScale;
                var group = spark.GetComponent<CanvasGroup>();
                spark.anchoredPosition = Vector2.zero;
                spark.localScale = Vector3.one * 0.3f;
                group.alpha = 0f;
                Tweens.Run(spark, "burst", 0.9f, t =>
                {
                    spark.anchoredPosition = Vector2.Lerp(Vector2.zero, to, t);
                    spark.localScale = Vector3.one * Mathf.Lerp(0.3f, 1f, t);
                    group.alpha = t < 0.25f ? t / 0.25f : 1f - (t - 0.25f) / 0.75f;
                }, Ease.OutCubic, null, (i % 3) * 0.06f);
            }
        }
    }
}
