using System;
using UnityEngine;

namespace BottleSort.UI
{
    public enum UiSound
    {
        Tap,
        Select,
        Deselect,
        Pour,
        Wrong,
        BottleComplete,
        Win,
        Reward,
        Popup
    }

    // Звук и вибрация от интерфейса. Сам звук играет тот, кто подписан на SoundRequested:
    // сейчас UiSoundPlayer в сцене, потом его заменит аудиосервис.
    public static class UiFeedback
    {
        public static event Action<UiSound> SoundRequested;

        public static void Play(UiSound sound)
        {
            if (PlayerData.Current.soundsOn)
                SoundRequested?.Invoke(sound);
        }

        public static void Tap()
        {
            Play(UiSound.Tap);
            Haptics.Light();
        }
    }

    public static class Haptics
    {
        const long LightMilliseconds = 12;

#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject _vibrator;
        static int _sdk = -1;
#endif

        public static void Light()
        {
            if (!PlayerData.Current.vibrationOn)
                return;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (_sdk < 0)
                {
                    using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                        _sdk = version.GetStatic<int>("SDK_INT");
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                        _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                }

                if (_vibrator == null)
                    return;
                if (_sdk >= 26)
                {
                    using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                    using (var effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", LightMilliseconds, -1))
                        _vibrator.Call("vibrate", effect);
                }
                else
                {
                    _vibrator.Call("vibrate", LightMilliseconds);
                }
            }
            catch (Exception)
            {
                _vibrator = null;
                Handheld.Vibrate();
            }
#endif
            // На iOS короткий тактильный отклик есть только через нативный UIImpactFeedbackGenerator,
            // Handheld.Vibrate там слишком длинный для нажатия. Подключим вместе с плагином сервисов.
        }
    }
}
