using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

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

        [SerializeField] CanvasGroup curtain;
        [SerializeField] float fadeTime = 0.28f;

        static SceneLoader _instance;
        static ScreenId? _pendingScreen;
        static int? _pendingLevel;

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

        // Шторка — префаб Prefabs/UI/Resources/SceneLoader: цвет и время перехода правятся там.
        static void Create()
        {
            _instance = Instantiate(Resources.Load<SceneLoader>("SceneLoader"));
            _instance.name = "SceneLoader";
            DontDestroyOnLoad(_instance.gameObject);
            _instance.curtain.alpha = 0f;
            _instance.curtain.blocksRaycasts = false;
        }

        IEnumerator Switch(string scene)
        {
            IsBusy = true;
            curtain.blocksRaycasts = true;
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
            curtain.blocksRaycasts = false;
            IsBusy = false;
        }

        IEnumerator Fade(float to)
        {
            float from = curtain.alpha;
            for (float time = 0f; time < fadeTime; time += Time.unscaledDeltaTime)
            {
                curtain.alpha = Mathf.Lerp(from, to, time / fadeTime);
                yield return null;
            }
            curtain.alpha = to;
        }
    }
}
