using System;
using System.Runtime.InteropServices;
using BottleSort.UI;
using UnityEngine;

namespace BottleSort.Services
{
    public enum HapticStrength
    {
        Light,
        Medium,
        Heavy
    }

    // Короткий тактильный отклик. На Android — VibrationEffect, на iOS — UIImpactFeedbackGenerator
    // через Plugins/iOS/BottleSortHaptics.mm. Handheld.Vibrate на iOS длится полсекунды, для нажатий не годится.
    public static class Haptics
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject _vibrator;
        static int _sdk = -1;
#endif

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern void BottleSortImpact(int style);

        [DllImport("__Internal")]
        static extern void BottleSortNotify(int type);
#endif

        public static bool Enabled => PlayerData.Current.vibrationOn;

        public static void Light() => Impact(HapticStrength.Light);

        public static void Impact(HapticStrength strength)
        {
            if (!Enabled)
                return;
#if UNITY_IOS && !UNITY_EDITOR
            BottleSortImpact((int)strength);
#elif UNITY_ANDROID && !UNITY_EDITOR
            VibrateAndroid(strength == HapticStrength.Light ? 12 : strength == HapticStrength.Medium ? 22 : 40,
                strength == HapticStrength.Light ? 80 : strength == HapticStrength.Medium ? 160 : 255);
#endif
        }

        // Победа и собранная бутылка: на iOS системный «успех», на Android двойной импульс.
        public static void Success()
        {
            if (!Enabled)
                return;
#if UNITY_IOS && !UNITY_EDITOR
            BottleSortNotify(0);
#elif UNITY_ANDROID && !UNITY_EDITOR
            VibratePattern(new long[] { 0, 18, 70, 28 }, new[] { 0, 140, 0, 220 });
#endif
        }

        public static void Error()
        {
            if (!Enabled)
                return;
#if UNITY_IOS && !UNITY_EDITOR
            BottleSortNotify(2);
#elif UNITY_ANDROID && !UNITY_EDITOR
            VibrateAndroid(30, 120);
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static bool EnsureVibrator()
        {
            if (_sdk >= 0)
                return _vibrator != null;
            try
            {
                using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                    _sdk = version.GetStatic<int>("SDK_INT");
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                    _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
            }
            catch (Exception)
            {
                _sdk = 0;
                _vibrator = null;
            }
            return _vibrator != null;
        }

        static void VibrateAndroid(long milliseconds, int amplitude)
        {
            if (!EnsureVibrator())
                return;
            try
            {
                if (_sdk >= 26)
                {
                    using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                    using (var effect = effectClass.CallStatic<AndroidJavaObject>("createOneShot", milliseconds, amplitude))
                        _vibrator.Call("vibrate", effect);
                }
                else
                {
                    _vibrator.Call("vibrate", milliseconds);
                }
            }
            catch (Exception)
            {
                // Заодно эта ссылка на Handheld.Vibrate заставляет Unity добавить в манифест разрешение VIBRATE.
                _vibrator = null;
                Handheld.Vibrate();
            }
        }

        static void VibratePattern(long[] timings, int[] amplitudes)
        {
            if (!EnsureVibrator())
                return;
            try
            {
                if (_sdk >= 26)
                {
                    using (var effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                    using (var effect = effectClass.CallStatic<AndroidJavaObject>("createWaveform", timings, amplitudes, -1))
                        _vibrator.Call("vibrate", effect);
                }
                else
                {
                    _vibrator.Call("vibrate", timings, -1);
                }
            }
            catch (Exception)
            {
                _vibrator = null;
            }
        }
#endif
    }
}
