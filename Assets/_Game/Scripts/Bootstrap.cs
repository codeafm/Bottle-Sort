using BottleSort.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BottleSort
{
    public sealed class Bootstrap : MonoBehaviour
    {
        public const string MainScene = "Main";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ApplyRuntimeSettings()
        {
            Application.targetFrameRate = GameInfo.TargetFrameRate;
            QualitySettings.vSyncCount = 0;
            Screen.orientation = ScreenOrientation.Portrait;
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
        }

        void Start()
        {
            SceneManager.LoadScene(MainScene);
        }
    }
}
