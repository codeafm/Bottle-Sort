using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BottleSort.UI
{
    // Переходы между сценами Splash, Menu и Game: шторка поверх всего, асинхронная загрузка, проявление
    // уже в новой сцене. Живёт между сценами. Какой экран и какой уровень открыть, новая сцена забирает
    // через TakePendingScreen / TakePendingLevel.
    public sealed class SceneLoader : MonoBehaviour
    {
        public const string SplashScene = "Splash";
        public const string MenuScene = "Menu";
        public const string GameScene = "Game";

        const float FadeTime = 0.28f;
        static readonly Color CurtainColor = new Color(0.03f, 0.04f, 0.1f, 1f);

        static SceneLoader _instance;
        static ScreenId? _pendingScreen;
        static int? _pendingLevel;

        CanvasGroup _curtain;
        AsyncOperation _preload;
        string _preloadScene;

        public static bool IsBusy { get; private set; }

        // Сколько готово из заранее начатой загрузки (заставка показывает это на полосе).
        public static float PreloadProgress =>
            _instance == null || _instance._preload == null ? 1f : Mathf.Clamp01(_instance._preload.progress / 0.9f);

        static SceneLoader Instance
        {
            get
            {
                if (_instance == null)
                    Create();
                return _instance;
            }
        }

        public static string SceneOf(ScreenId screen)
        {
            switch (screen)
            {
                case ScreenId.Splash:
                    return SplashScene;
                case ScreenId.Game:
                case ScreenId.Win:
                case ScreenId.Gift:
                    return GameScene;
                default:
                    return MenuScene;
            }
        }

        // Начать грузить сцену заранее, но не включать её, пока не позовут Open.
        public static void Preload(string scene)
        {
            var loader = Instance;
            if (loader._preload != null || IsBusy)
                return;
            loader._preload = SceneManager.LoadSceneAsync(scene);
            loader._preload.allowSceneActivation = false;
            loader._preloadScene = scene;
        }

        public static void Open(ScreenId screen, int? level = null)
        {
            if (IsBusy)
                return;
            _pendingScreen = screen;
            _pendingLevel = level;
            Instance.StartCoroutine(Instance.Switch(SceneOf(screen)));
        }

        public static ScreenId? TakePendingScreen()
        {
            var screen = _pendingScreen;
            _pendingScreen = null;
            return screen;
        }

        public static int? TakePendingLevel()
        {
            var level = _pendingLevel;
            _pendingLevel = null;
            return level;
        }

        static void Create()
        {
            var root = new GameObject("SceneLoader", typeof(RectTransform));
            DontDestroyOnLoad(root);
            _instance = root.AddComponent<SceneLoader>();

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            root.AddComponent<GraphicRaycaster>();

            var curtain = new GameObject("Curtain", typeof(RectTransform));
            var rect = (RectTransform)curtain.transform;
            rect.SetParent(root.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            curtain.AddComponent<Image>().color = CurtainColor;
            _instance._curtain = curtain.AddComponent<CanvasGroup>();
            _instance._curtain.alpha = 0f;
            _instance._curtain.blocksRaycasts = false;
        }

        IEnumerator Switch(string scene)
        {
            IsBusy = true;
            _curtain.blocksRaycasts = true;
            yield return Fade(1f);

            // Unity не начнёт новую загрузку, пока не включена отложенная: если заставка грузила другую сцену, пропускаем её.
            if (_preload != null && _preloadScene != scene)
            {
                _preload.allowSceneActivation = true;
                yield return _preload;
                _preload = null;
            }
            var loading = _preload ?? SceneManager.LoadSceneAsync(scene);
            _preload = null;
            loading.allowSceneActivation = true;
            yield return loading;
            // Кадр на Start новой сцены, чтобы под шторкой уже стоял нужный экран.
            yield return null;

            yield return Fade(0f);
            _curtain.blocksRaycasts = false;
            IsBusy = false;
        }

        IEnumerator Fade(float to)
        {
            float from = _curtain.alpha;
            for (float time = 0f; time < FadeTime; time += Time.unscaledDeltaTime)
            {
                _curtain.alpha = Mathf.Lerp(from, to, time / FadeTime);
                yield return null;
            }
            _curtain.alpha = to;
        }
    }
}
