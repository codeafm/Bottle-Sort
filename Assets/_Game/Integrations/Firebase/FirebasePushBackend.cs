using System;
using BottleSort.Services;
using Firebase;
using Firebase.Extensions;
using Firebase.Messaging;
using UnityEngine;

namespace BottleSort.Integrations
{
    // Firebase Cloud Messaging, как во Flutter: токен и входящие сообщения пишем в лог, подписка на топик —
    // через PushController. Конфиг: Assets/google-services.json (Android) и GoogleService-Info.plist (iOS).
    // Без конфига или без Google Play Services Start вернёт false, и игра просто останется без push.
    public sealed class FirebasePushBackend : IPushBackend
    {
#if !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => PushService.DeviceBackendFactory = () => new FirebasePushBackend();
#endif

        public void Start(Action<bool> ready)
        {
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
            {
                bool ok = task.IsCompleted && !task.IsFaulted && task.Result == DependencyStatus.Available;
                if (!ok)
                {
                    Debug.LogWarning($"[Push] Firebase is not available: {(task.IsFaulted ? task.Exception?.Message : task.Result.ToString())}");
                    ready?.Invoke(false);
                    return;
                }
                try
                {
                    FirebaseMessaging.TokenReceived += (sender, args) => Debug.Log("[Push] FCM token: " + args.Token);
                    FirebaseMessaging.MessageReceived += (sender, args) =>
                        Debug.Log($"[Push] Message: {args.Message.Notification?.Title} - {args.Message.Notification?.Body}");
                    ready?.Invoke(true);
                }
                catch (Exception e)
                {
                    // Чаще всего так выглядит отсутствующий GoogleService-Info.plist на iOS.
                    Debug.LogWarning("[Push] Firebase Messaging failed to start: " + e.Message);
                    ready?.Invoke(false);
                }
            });
        }

        public void RequestPermission()
        {
            FirebaseMessaging.RequestPermissionAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                    Debug.LogWarning("[Push] Permission request failed: " + task.Exception?.Message);
            });
        }

        public void Subscribe(string topic) =>
            FirebaseMessaging.SubscribeAsync(topic).ContinueWithOnMainThread(task => Report("subscribe", topic, task.IsFaulted));

        public void Unsubscribe(string topic) =>
            FirebaseMessaging.UnsubscribeAsync(topic).ContinueWithOnMainThread(task => Report("unsubscribe", topic, task.IsFaulted));

        static void Report(string action, string topic, bool failed)
        {
            if (failed)
                Debug.LogWarning($"[Push] Could not {action} {topic}");
            else
                Debug.Log($"[Push] {action} {topic}");
        }
    }
}
