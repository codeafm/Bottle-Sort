using BottleSort.Core;
using UnityEngine;

namespace BottleSort
{
    // Настройки запуска до первой сцены, с какой бы сцены ни стартовала игра.
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ApplyRuntimeSettings()
        {
            Application.targetFrameRate = GameInfo.TargetFrameRate;
            QualitySettings.vSyncCount = 0;
            Screen.orientation = ScreenOrientation.Portrait;
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
        }
    }
}
