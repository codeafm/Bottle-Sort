using BottleSort.Services;
using UnityEngine;
using UnityEngine.UI;

namespace BottleSort.UI
{
    public sealed class SettingsPopup : UiScreen
    {
        [SerializeField] Button closeButton;
        [SerializeField] Button dimmer;
        [SerializeField] ToggleSwitch music;
        [SerializeField] ToggleSwitch sounds;
        [SerializeField] ToggleSwitch vibration;
        [SerializeField] ToggleSwitch notifications;
        [SerializeField] Button russian;
        [SerializeField] Button english;
        [SerializeField] UiGradient russianBackground;
        [SerializeField] UiGradient englishBackground;

        public override ScreenId Id => ScreenId.Settings;
        public override bool IsPopup => true;

        void Awake()
        {
            closeButton.onClick.AddListener(Close);
            dimmer.onClick.AddListener(Close);
            music.Changed += value => Change(d => d.musicOn = value);
            sounds.Changed += value => Change(d => d.soundsOn = value);
            vibration.Changed += value =>
            {
                Change(d => d.vibrationOn = value);
                if (value)
                    Haptics.Light();
            };
            notifications.Changed += value => Change(d => d.notificationsOn = value);
            russian.onClick.AddListener(() => SetLanguage(Language.Ru));
            english.onClick.AddListener(() => SetLanguage(Language.En));
        }

        protected override void OnOpen()
        {
            var data = PlayerData.Current;
            music.SetValue(data.musicOn, false);
            sounds.SetValue(data.soundsOn, false);
            vibration.SetValue(data.vibrationOn, false);
            notifications.SetValue(data.notificationsOn, false);
            RefreshLanguage();
        }

        void Close() => Router.ClosePopup(this);

        static void Change(System.Action<PlayerData> apply)
        {
            apply(PlayerData.Current);
            PlayerData.Current.Save();
        }

        void SetLanguage(Language language)
        {
            PlayerData.Current.language = language.ToString();
            PlayerData.Current.Save();
            Loc.Set(language);
            RefreshLanguage();
        }

        void RefreshLanguage()
        {
            var on = (UiColors.Hex("#35C7FF"), UiColors.Hex("#0870E8"));
            bool ru = Loc.Current == Language.Ru;
            russianBackground.SetColors(ru ? on.Item1 : Color.clear, ru ? on.Item2 : Color.clear);
            englishBackground.SetColors(ru ? Color.clear : on.Item1, ru ? Color.clear : on.Item2);
        }
    }
}
