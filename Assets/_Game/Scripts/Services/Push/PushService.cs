using System;
using BottleSort.UI;

namespace BottleSort.Services
{
    public static class PushService
    {
        // Во Flutter push поднимался через ~2 с после запуска, чтобы не тормозить заставку.
        const float StartDelay = 2f;

        // Firebase регистрирует Integrations/Firebase: его код вне Game.Runtime, рядом с SDK.
        public static Func<IPushBackend> DeviceBackendFactory;

        static PushController _controller;

        public static PushController Controller => _controller;

        public static void Initialize(MainThread thread)
        {
            thread.After(StartDelay, () =>
            {
                IPushBackend backend = null;
#if !UNITY_EDITOR
                backend = DeviceBackendFactory?.Invoke();
#endif
                _controller = new PushController(backend ?? new EditorPushBackend());
                PlayerData.Changed -= Sync;
                PlayerData.Changed += Sync;
                Sync();
            });
        }

        static void Sync()
        {
            var data = PlayerData.Current;
            _controller?.Sync(data.highestUnlocked, data.notificationsOn);
        }
    }
}
