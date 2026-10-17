namespace BottleSort.Services
{
    // Push как во Flutter (firebase_notification_service.dart): общий топик all_users, подписка по тумблеру
    // «Уведомления». Отличие: Firebase и запрос разрешения — только после обучения, с 4-го уровня,
    // как у локальных напоминаний; во Flutter системное окно выскакивало при первом запуске.
    public sealed class PushController
    {
        public const string Topic = "all_users";
        public const int StartFromLevel = 4;

        enum State
        {
            Waiting,
            Starting,
            Ready,
            Failed
        }

        readonly IPushBackend _backend;
        State _state = State.Waiting;
        bool? _subscribed;
        bool _wanted;

        public PushController(IPushBackend backend)
        {
            _backend = backend;
        }

        public bool IsReady => _state == State.Ready;
        public bool? Subscribed => _subscribed;

        // Вызывать при запуске и при каждом изменении PlayerData: лишних запросов к Firebase не будет.
        public void Sync(int highestUnlocked, bool notificationsOn)
        {
            _wanted = notificationsOn;
            if (_state == State.Waiting && highestUnlocked >= StartFromLevel)
            {
                _state = State.Starting;
                _backend.Start(OnStarted);
                return;
            }
            if (_state == State.Ready)
                ApplySubscription();
        }

        void OnStarted(bool ok)
        {
            if (!ok)
            {
                _state = State.Failed;
                return;
            }
            _state = State.Ready;
            _backend.RequestPermission();
            ApplySubscription();
        }

        void ApplySubscription()
        {
            if (_subscribed == _wanted)
                return;
            _subscribed = _wanted;
            if (_wanted)
                _backend.Subscribe(Topic);
            else
                _backend.Unsubscribe(Topic);
        }
    }
}
