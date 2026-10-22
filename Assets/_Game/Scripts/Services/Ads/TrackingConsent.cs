using System;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace BottleSort.Services
{
    // iOS спрашивает игрока, можно ли рекламе знать его устройство (App Tracking Transparency).
    // Yandex читает IDFA при каждом запросе, поэтому рекламу начинаем грузить после ответа:
    // согласившиеся игроки получают рекламу дороже. Текст вопроса и фреймворк добавляет IosBuildPostprocess.
    // На Android и в редакторе спрашивать нечего — done сразу.
    public static class TrackingConsent
    {
        const float PollEvery = 0.25f;
        // Окно системы модальное, игрок ответит; но если iOS его не показала (приложение не активно),
        // статус так и останется «не спрашивали» — тогда не держим рекламу дольше минуты.
        const float GiveUpAfter = 60f;

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern int BSTrackingStatus();

        [DllImport("__Internal")]
        static extern void BSRequestTracking();
#endif

        public static void Request(MainThread thread, float delay, Action done)
        {
#if UNITY_IOS && !UNITY_EDITOR
            if (BSTrackingStatus() != 0)
            {
                done();
                return;
            }
            thread.After(delay, () =>
            {
                BSRequestTracking();
                Wait(thread, 0f, done);
            });
#else
            done();
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        static void Wait(MainThread thread, float waited, Action done)
        {
            if (BSTrackingStatus() != 0 || waited >= GiveUpAfter)
            {
                done();
                return;
            }
            thread.After(PollEvery, () => Wait(thread, waited + PollEvery, done));
        }
#endif
    }
}
