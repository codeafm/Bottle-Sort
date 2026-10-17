using System;
using BottleSort.Services;

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
        Popup,
        Button,
        Whoosh,
        Coin,
        Star1,
        Star2,
        Star3,
        PourShort,
        Hint,
        Undo,
        IceCrack,
        Unlock,
        Reveal,
        WheelTick,
        WheelStop
    }

    // Звук и вибрация от интерфейса. Играет AudioService, он подписан на SoundRequested.
    public static class UiFeedback
    {
        public static event Action<UiSound, float> SoundRequested;

        public static void Play(UiSound sound, float pitch = 1f)
        {
            if (PlayerData.Current.soundsOn)
                SoundRequested?.Invoke(sound, pitch);
        }

        public static void Tap()
        {
            Play(UiSound.Button);
            Haptics.Light();
        }
    }
}
