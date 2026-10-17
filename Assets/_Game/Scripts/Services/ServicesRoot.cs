using BottleSort.UI;
using UnityEngine;

namespace BottleSort.Services
{
    // Сервисы создаются сами до загрузки первой сцены и живут всю игру,
    // поэтому их не нужно добавлять в сцены и пересобранная UiSceneBuilder сцена их не потеряет.
    public sealed class ServicesRoot : MonoBehaviour
    {
        public static ServicesRoot Instance { get; private set; }

        public AudioService Audio { get; private set; }
        public MainThread Thread { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Create()
        {
            if (Instance != null)
                return;
            var go = new GameObject("Services");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<ServicesRoot>();
        }

        void Awake()
        {
            Thread = gameObject.AddComponent<MainThread>();
            Audio = gameObject.AddComponent<AudioService>();
            gameObject.AddComponent<GameFeedback>().Bind(Audio);
            Ads.Initialize(Thread);
            NotificationService.Initialize();
        }

        void Start() => NotificationService.Reschedule();

        void OnApplicationPause(bool paused)
        {
            if (!paused)
                return;
            PlayerData.Current.Save();
            NotificationService.Reschedule();
        }

        void OnApplicationQuit() => PlayerData.Current.Save();

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
