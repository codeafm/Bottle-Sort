using System;
using BottleSort.UI;
using UnityEngine;
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
using Unity.Notifications;
#endif

namespace BottleSort.Services
{
    // Локальное напоминание раз в день в 19:00. При каждом запуске и уходе в фон всё пересоздаём:
    // кто играет каждый день, уведомления почти не видит, а пропавшему игроку напомним 7 дней и замолчим.
    public static class NotificationService
    {
        const int ReminderHour = 19;
        const int ReminderDays = 7;
        const int TextVariants = 3;
        // Разрешение спрашиваем после обучения, а не на первом экране.
        const int AskPermissionFromLevel = 4;

        static bool _lastEnabled = true;
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        static bool _initialized;
        static bool _permissionAsked;
#endif

        public static void Initialize()
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            if (_initialized)
                return;
            var args = NotificationCenterArgs.Default;
            args.AndroidChannelId = "reminders";
            args.AndroidChannelName = Loc.Get("notify_channel");
            args.AndroidChannelDescription = Loc.Get("notify_channel_text");
            NotificationCenter.Initialize(args);
            _initialized = true;
#endif
            PlayerData.Changed -= OnSettingsChanged;
            PlayerData.Changed += OnSettingsChanged;
        }

        static void OnSettingsChanged()
        {
            bool enabled = PlayerData.Current.notificationsOn;
            if (enabled == _lastEnabled)
                return;
            _lastEnabled = enabled;
            Reschedule();
        }

        public static void Reschedule()
        {
            var data = PlayerData.Current;
            _lastEnabled = data.notificationsOn;
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            if (!_initialized)
                return;
            NotificationCenter.CancelAllScheduledNotifications();
            NotificationCenter.CancelAllDeliveredNotifications();
            if (!data.notificationsOn || data.highestUnlocked < AskPermissionFromLevel)
                return;

            if (!_permissionAsked)
            {
                _permissionAsked = true;
                NotificationCenter.RequestPermission();
            }

            foreach (var fireTime in ReminderTimes(DateTime.Now))
            {
                int variant = fireTime.DayOfYear % TextVariants + 1;
                var notification = new Notification
                {
                    Title = Loc.Get("notify_title_" + variant),
                    Text = Loc.Get("notify_text_" + variant)
                };
                NotificationCenter.ScheduleNotification(notification, new NotificationDateTimeSchedule(fireTime));
            }
#else
            if (data.notificationsOn)
                Debug.Log($"[Notifications] {ReminderDays} reminders from {ReminderTimes(DateTime.Now)[0]:g} (editor, not scheduled)");
#endif
        }

        // Первое — завтра в 19:00, дальше каждый день.
        public static DateTime[] ReminderTimes(DateTime now)
        {
            var times = new DateTime[ReminderDays];
            var first = now.Date.AddDays(1).AddHours(ReminderHour);
            for (int i = 0; i < ReminderDays; i++)
                times[i] = first.AddDays(i);
            return times;
        }
    }
}
