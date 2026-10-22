using System;
using BottleSort.Services;
using Firebase.Analytics;
using UnityEngine;

namespace BottleSort.Integrations
{
    // Firebase Analytics. Конфиг тот же, что у push: Assets/google-services.json (Android)
    // и Assets/GoogleService-Info.plist (iOS). Без него Start вернёт false, и игра просто не шлёт события.
    public sealed class FirebaseAnalyticsBackend : IAnalyticsBackend
    {
#if !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => Analytics.DeviceBackendFactory = () => new FirebaseAnalyticsBackend();
#endif

        public void Start(Action<bool> ready)
        {
            FirebaseReady.Then((ok, problem) =>
            {
                if (!ok)
                {
                    Debug.LogWarning("[Analytics] Firebase is not available: " + problem);
                    ready?.Invoke(false);
                    return;
                }
                try
                {
                    FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);
                    ready?.Invoke(true);
                }
                catch (Exception e)
                {
                    // Чаще всего так выглядит отсутствующий GoogleService-Info.plist на iOS.
                    Debug.LogWarning("[Analytics] Firebase Analytics failed to start: " + e.Message);
                    ready?.Invoke(false);
                }
            });
        }

        public void Log(string name, AnalyticsParams parameters)
        {
            var items = parameters.Items;
            var firebase = new Parameter[items.Count];
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                switch (item.Value)
                {
                    case long number:
                        firebase[i] = new Parameter(item.Key, number);
                        break;
                    case double real:
                        firebase[i] = new Parameter(item.Key, real);
                        break;
                    default:
                        firebase[i] = new Parameter(item.Key, Limit(item.Value?.ToString(), 100));
                        break;
                }
            }
            FirebaseAnalytics.LogEvent(name, firebase);
        }

        public void SetUserProperty(string name, string value) =>
            FirebaseAnalytics.SetUserProperty(name, Limit(value, 36));

        // Длиннее Firebase молча отбрасывает значение целиком, лучше обрезать.
        static string Limit(string value, int length) =>
            value != null && value.Length > length ? value.Substring(0, length) : value;
    }
}
