using System;

namespace BottleSort.Services
{
    // Сеть push-уведомлений. На устройстве — Firebase Messaging (Integrations/Firebase), в редакторе — заглушка.
    public interface IPushBackend
    {
        // ready(true) — можно подписываться; false — Firebase не поднялся (нет конфига, нет Google Play Services).
        void Start(Action<bool> ready);
        void RequestPermission();
        void Subscribe(string topic);
        void Unsubscribe(string topic);
    }

    public sealed class EditorPushBackend : IPushBackend
    {
        public void Start(Action<bool> ready)
        {
            UnityEngine.Debug.Log("[Push] Stub push backend, no Firebase in the editor");
            ready?.Invoke(true);
        }

        public void RequestPermission() => UnityEngine.Debug.Log("[Push] Permission requested (stub)");
        public void Subscribe(string topic) => UnityEngine.Debug.Log($"[Push] Subscribed to {topic} (stub)");
        public void Unsubscribe(string topic) => UnityEngine.Debug.Log($"[Push] Unsubscribed from {topic} (stub)");
    }
}
