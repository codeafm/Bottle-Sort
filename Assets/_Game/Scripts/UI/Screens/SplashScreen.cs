using System.Threading.Tasks;
using BottleSort.Core;
using BottleSort.Services;
using UnityEngine;
using UnityEngine.UI;

namespace BottleSort.UI
{
    // Заставка из Splash.dc.html: логотип влетает, бутылка наклоняется и непрерывно наливает в бутылку на полке,
    // потом золотое свечение и звёздочки. При запуске игры сначала идёт интро студии (BrandIntro), потом заставка
    // не меньше одного полного круга налива (4.5 с), пока грузятся меню и уровень игрока; полоса идёт в такт
    // этому времени, на полной бутылке — «Готово!», и заставка спокойно уходит (outro), а не обрывается.
    public sealed class SplashScreen : UiScreen
    {
        // Круг налива: новый цвет проявляется, бутылка наклоняется, струя бежит вниз, уровень растёт,
        // струя отрывается, бутылка выпрямляется и вспыхивает, короткая пауза на полной бутылке. Секунды от начала круга.
        const float CycleTime = 4.5f;
        const float RefillEnd = 0.35f;
        const float TiltStart = 0.35f;
        const float TiltEnd = 0.75f;
        const float StreamStart = 0.6f;
        const float StreamGrow = 0.25f;
        const float FillStart = 0.75f;
        const float FillEnd = 3.45f;
        const float StreamDrop = 0.25f;
        const float UntiltEnd = 3.85f;
        const float GlowIn = 0.3f;
        // Круг начинается чуть раньше заставки, чтобы к концу минимального показа бутылка стояла полной.
        const float StartOffset = -0.3f;
        // На сколько градусов бутылка в покое прямее, чем при наливе (угол налива — тот, что в префабе).
        const float RestTilt = 34f;
        const int Capacity = 4;
        // Звук налива: клип короче самого налива, поэтому он звучит трижды внахлёст, и тон чуть растёт,
        // как при доливании. Тише, чем на поле.
        static readonly float[] PourCues = { StreamStart, 1.6f, 2.6f };
        static readonly float[] PourPitch = { 0.95f, 1.02f, 1.1f };
        const float PourShare = 0.45f;
        const float CompleteShare = 0.5f;

        const float MinTime = CycleTime;
        // Подвисание при загрузке не должно съедать время показа: шаг кадра не больше 1/20 с.
        const float MaxFrameStep = 0.05f;
        const float ReadyPause = 0.35f;
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
        [SerializeField] IdleMotion barShine;
        [SerializeField] ScreenMotion outro;
        [SerializeField] BrandIntro brandIntro;

        // Интро студии — один раз за запуск игры.
        static bool _introShown;
        // Сколько макетных пикселей в одной единице Canvas по вертикали внутри сцены заставки.
        [SerializeField] float mockupScale = 2.5f;

        float _elapsed;
        float _pourTime;
        int _burstCycle = -1;
        int _soundCycle = -1;
        int _soundCue;
        float _pourAngle = float.NaN;
        float _streamY = float.NaN;
        float _shownProgress;
        Task _loading;
        bool _leaving;
        bool _frozen;
        bool _waitingIntro;

        public bool IsReady => _leaving;
        // Для снимков налива: идёт ли ещё интро студии и сколько секунд прошло от начала круга налива.
        public bool IsPlayingIntro => _waitingIntro;
        public float PourTime => _pourTime;

        // Для снимков: кадр налива на любой момент, в том числе смена цвета на втором круге.
        public void ShowPourFrame(float time) => Pose(time, false);

        public override ScreenId Id => ScreenId.Splash;
        public override Theme? BackdropTheme => Theme.Violet;

        protected override void OnOpen()
        {
            _elapsed = 0f;
            _shownProgress = 0.06f;
            _leaving = false;

            // Первый уровень игрока строим заранее: на поздних уровнях генератор думает заметное время.
            int level = PlayerData.Current.highestUnlocked;
            _loading = LevelLoader.Get(level);
            SceneLoader.Preload(SceneLoader.MenuScene);

            if (brandIntro != null && !_introShown)
            {
                _introShown = true;
                _waitingIntro = true;
                logoGroup.alpha = 0f;
                brandIntro.Play(BeginSplash);
                return;
            }
            if (brandIntro != null)
                brandIntro.Skip();
            BeginSplash();
        }

        // Заставка начинается, когда интро студии погасло, и тёмный фон растворяется уже поверх неё.
        void BeginSplash()
        {
            _waitingIntro = false;
            _elapsed = 0f;
            _pourTime = StartOffset;
            _burstCycle = -1;
            _soundCycle = -1;
            AnimateLogo();
            Pose(_pourTime, false);
            if (AudioService.Instance != null)
                AudioService.Instance.PlayMusic(MusicTrack.Menu);
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
            if (!IsOpen || _frozen || _waitingIntro)
                return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, MaxFrameStep);
            _elapsed += dt;
            // На «Готово!» налив останавливается на полной бутылке.
            if (_leaving)
                return;

            _pourTime += dt;
            Pose(_pourTime, true);

            // Полоса идёт в такт показу и лишь к концу мягко замедляется; быстрая загрузка её не подгоняет.
            float time = Mathf.Clamp01(_elapsed / MinTime);
            float timeProgress = 1f - Mathf.Pow(1f - time, 1.6f);
            float loadProgress = Mathf.Min(_loading == null || _loading.IsCompleted ? 1f : 0.85f, SceneLoader.PreloadProgress);
            float target = Mathf.Min(timeProgress, loadProgress);
            _shownProgress = Mathf.Lerp(_shownProgress, target, 1f - Mathf.Exp(-dt * 10f));
            if (target >= 1f && _shownProgress > 0.995f)
                _shownProgress = 1f;
            progressFill.anchorMax = new Vector2(Mathf.Max(_shownProgress, 0.06f), 1f);
            loadingLabel.SetArgs(Mathf.RoundToInt(_shownProgress * 100f));

            // «Готово!» — когда и полоса дошла, и бутылка стоит полной (обычно это совпадает: круг = минимальный показ).
            if (_shownProgress >= 1f && _pourTime >= 0f && Phase(_pourTime) >= UntiltEnd)
                Ready();
        }

        public override void FillForEditor()
        {
            Pose((FillStart + FillEnd) / 2f, false);
            logoGroup.alpha = 1f;
            logo.localScale = Vector3.one;
            progressFill.anchorMax = new Vector2(0.41f, 1f);
            loadingLabel.SetArgs(41);
        }

        // Остановить заставку на нужном шаге налива и проценте загрузки: для сравнения с макетом.
        public void Freeze(int step, float progress)
        {
            _frozen = true;
            _waitingIntro = false;
            if (brandIntro != null)
                brandIntro.Skip();
            foreach (var motion in GetComponentsInChildren<IdleMotion>())
                motion.enabled = false;
            // Шаги старой заставки: 0–3 — налито столько слоёв и льётся, 4–5 — полная.
            int k = step % 6;
            float phase = k >= 4 ? UntiltEnd + 0.1f : Mathf.Lerp(FillStart, FillEnd, k / (float)Capacity);
            Pose(step / 6 * CycleTime + phase, false);
            Tweens.Kill(logo, "logo");
            logo.localScale = Vector3.one;
            logoGroup.alpha = 1f;
            progressFill.anchorMax = new Vector2(progress, 1f);
            loadingLabel.SetArgs(Mathf.RoundToInt(progress * 100f));
        }

        // 100 %: «Готово!», блик по полосе, бутылка долита. Через паузу заставка уходит, потом меню.
        void Ready()
        {
            _leaving = true;
            loadingLabel.SetKey("splash_ready");
            SweepBar();
            Tweens.Delay(this, "leave", ReadyPause, () => outro.Play(1f, Leave));
        }

        void SweepBar()
        {
            barShine.enabled = false;
            var shine = (RectTransform)barShine.transform;
            var range = barShine.SweepRange;
            Tweens.Run(shine, "ready", ReadyPause, t => shine.anchoredPosition = new Vector2(Mathf.Lerp(range.x, range.y, t),
                shine.anchoredPosition.y), Ease.InOutSine);
        }

        // После заставки всегда меню, и в первый запуск тоже (решение заказчика).
        void Leave() => Router.Show(ScreenId.Menu);

        static float Phase(float time) => time < 0f ? 0f : time - Mathf.Floor(time / CycleTime) * CycleTime;

        static float Smooth(float from, float to, float value) => Easing.Apply(Ease.InOutSine, Mathf.InverseLerp(from, to, value));

        // Поверхность жидкости в целевой бутылке, в пикселях макета от верха экрана; level — в слоях.
        static float SurfaceAt(float level) => 404f + 216f * ((226f - level * 40f) / 240f);

        // Кадр налива на момент time от начала первого круга. live — идёт сама заставка (тогда вспыхивают звёздочки).
        void Pose(float time, bool live)
        {
            var pourerRoot = pourer.transform.parent;
            if (float.IsNaN(_pourAngle))
            {
                _pourAngle = Mathf.DeltaAngle(0f, pourerRoot.localEulerAngles.z);
                _streamY = stream.anchoredPosition.y;
            }

            int cycle = time < 0f ? 0 : Mathf.FloorToInt(time / CycleTime);
            float p = Phase(time);
            byte color = Order[cycle % Order.Length];
            // Уровень растёт почти равномерно, мягко только в самом начале и в конце: струя уже льётся — вода прибывает.
            float linear = Mathf.InverseLerp(FillStart, FillEnd, p);
            float fill = Mathf.Lerp(linear, Smooth(FillStart, FillEnd, p), 0.3f) * Capacity;
            float tilt = p < FillEnd ? Smooth(TiltStart, TiltEnd, p) : 1f - Smooth(FillEnd, UntiltEnd, p);
            pourerRoot.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(_pourAngle + RestTilt, _pourAngle, tilt));

            if (cycle > 0 && p < RefillEnd)
            {
                // Смена цвета: прошлый цвет в полной бутылке тает, наливающая наполняется новым.
                float k = Smooth(0f, RefillEnd, p);
                target.ShowLiquid(Order[(cycle - 1) % Order.Length], Capacity, Capacity, 1f - k);
                target.SetCompletedGlow(1f - k);
                pourer.ShowLiquid(color, Capacity, Capacity, k);
            }
            else
            {
                target.ShowLiquid(color, Capacity, fill);
                pourer.ShowLiquid(color, Capacity, Capacity - fill);
                target.SetCompletedGlow(Mathf.InverseLerp(FillEnd, FillEnd + GlowIn, p));
            }

            // Струя: голова бежит от горлышка вниз к поверхности, в конце хвост отрывается и догоняет её.
            float surface = SurfaceAt(fill);
            float head = Mathf.Lerp(StreamTop, surface, Easing.Apply(Ease.OutCubic, Mathf.InverseLerp(StreamStart, StreamStart + StreamGrow, p)));
            float tail = Mathf.Lerp(StreamTop, surface, Easing.Apply(Ease.InCubic, Mathf.InverseLerp(FillEnd, FillEnd + StreamDrop, p)));
            bool flowing = p >= StreamStart && tail < surface - 0.5f;
            stream.gameObject.SetActive(flowing);
            if (flowing)
            {
                streamColors.SetColors(UiColors.Liquid(color, 1), UiColors.Liquid(color, 0), true);
                stream.anchoredPosition = new Vector2(stream.anchoredPosition.x, _streamY - (tail - StreamTop) * mockupScale);
                stream.sizeDelta = new Vector2(stream.sizeDelta.x, Mathf.Max(head - tail, 0f) * mockupScale);
            }

            // Круги на поверхности — пока струя касается воды, и вместе с уровнем.
            bool splashing = flowing && head >= surface - 0.5f;
            ripple.gameObject.SetActive(splashing);
            if (splashing)
            {
                ripple.anchoredPosition = new Vector2(ripple.anchoredPosition.x, -(surface - 4f) * mockupScale);
                ripple.GetComponent<Image>().color = UiColors.Liquid(color, 1);
            }

            if (!live)
                return;
            PlayPourSound(cycle, p);
            if (p >= FillEnd && _burstCycle != cycle)
            {
                _burstCycle = cycle;
                Burst();
                PlaySound(UiSound.BottleComplete, 1f, CompleteShare);
            }
        }

        void PlayPourSound(int cycle, float p)
        {
            if (_soundCycle != cycle)
            {
                _soundCycle = cycle;
                _soundCue = 0;
            }
            int due = _soundCue;
            while (due < PourCues.Length && p >= PourCues[due] && p < FillEnd)
                due++;
            // Если кадр перескочил несколько отметок, догонять пачкой не надо — только последняя.
            if (due > _soundCue)
                PlaySound(UiSound.Pour, PourPitch[due - 1], PourShare);
            _soundCue = due;
        }

        static void PlaySound(UiSound sound, float pitch, float share)
        {
            var audio = AudioService.Instance;
            if (audio != null)
                audio.Play(sound, pitch, share);
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
