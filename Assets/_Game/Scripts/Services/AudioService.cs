using System.Collections.Generic;
using BottleSort.UI;
using UnityEngine;

namespace BottleSort.Services
{
    public enum MusicTrack
    {
        None,
        Menu,
        Game
    }

    // Эффекты и музыка. Клипы лежат в Audio/Resources/Sfx и Music, имя файла = имя звука.
    // Настройки читаются из PlayerData при каждом изменении, так что переключатели в настройках действуют сразу.
    public sealed class AudioService : MonoBehaviour
    {
        const float CrossfadeTime = 1f;
        const float SameClipInterval = 0.05f;
        const int SfxVoices = 6;

        static readonly Dictionary<UiSound, string> UiClips = new Dictionary<UiSound, string>
        {
            { UiSound.Tap, "ui_tap" },
            { UiSound.Button, "ui_button" },
            { UiSound.Popup, "ui_popup" },
            { UiSound.Whoosh, "ui_whoosh" },
            { UiSound.Select, "bottle_select" },
            { UiSound.Deselect, "bottle_deselect" },
            { UiSound.Pour, "pour" },
            { UiSound.PourShort, "pour_short" },
            { UiSound.Wrong, "wrong_move" },
            { UiSound.BottleComplete, "bottle_complete" },
            { UiSound.Win, "level_win" },
            { UiSound.Reward, "reward" },
            { UiSound.Coin, "coin" },
            { UiSound.Star1, "star_1" },
            { UiSound.Star2, "star_2" },
            { UiSound.Star3, "star_3" },
            { UiSound.Hint, "booster_hint" },
            { UiSound.Undo, "booster_undo" },
            { UiSound.IceCrack, "ice_crack" },
            { UiSound.Unlock, "unlock" },
            { UiSound.Reveal, "reveal" },
            { UiSound.WheelTick, "wheel_tick" },
            { UiSound.WheelStop, "wheel_stop" }
        };

        // Свой баланс громкости для каждого звука: переливание и выбор звучат часто, их тише.
        static readonly Dictionary<string, float> ClipVolume = new Dictionary<string, float>
        {
            { "ui_tap", 0.6f },
            { "ui_button", 0.7f },
            { "bottle_select", 0.75f },
            { "bottle_deselect", 0.7f },
            { "pour", 0.8f },
            { "pour_short", 0.8f },
            { "wrong_move", 0.8f },
            { "ui_whoosh", 0.55f },
            { "wheel_tick", 0.7f }
        };

        // Щелчки колеса идут чаще общего ограничения на повтор.
        static readonly Dictionary<string, float> RepeatInterval = new Dictionary<string, float>
        {
            { "wheel_tick", 0.04f }
        };

        public static AudioService Instance { get; private set; }

        readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, float> _lastPlayed = new Dictionary<string, float>();
        AudioSource[] _voices;
        int _nextVoice;
        AudioSource _musicA;
        AudioSource _musicB;
        AudioSource _activeMusic;
        MusicTrack _track = MusicTrack.None;
        float _fade = 1f;

        public MusicTrack Track => _track;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // на камерах в сценах слушателя нет, а без него Unity не выводит звук вообще
            if (FindAnyObjectByType<AudioListener>() == null)
                gameObject.AddComponent<AudioListener>();

            _voices = new AudioSource[SfxVoices];
            for (int i = 0; i < SfxVoices; i++)
                _voices[i] = CreateSource(false);
            _musicA = CreateSource(true);
            _musicB = CreateSource(true);
            _activeMusic = _musicA;
        }

        // Эффекты распаковываются при загрузке, поэтому грузим все заранее: первый тап не должен подвисать.
        void Start()
        {
            foreach (string name in UiClips.Values)
                Clip("Sfx/", name);
        }

        void OnEnable()
        {
            UiFeedback.SoundRequested += OnUiSound;
            PlayerData.Changed += ApplySettings;
            ScreenRouter.ScreenShown += OnScreenShown;
        }

        void OnDisable()
        {
            UiFeedback.SoundRequested -= OnUiSound;
            PlayerData.Changed -= ApplySettings;
            ScreenRouter.ScreenShown -= OnScreenShown;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        AudioSource CreateSource(bool music)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = music;
            source.spatialBlend = 0f;
            source.priority = music ? 0 : 128;
            return source;
        }

        static float SfxVolume
        {
            get
            {
                var data = PlayerData.Current;
                return data.soundsOn ? data.soundVolume : 0f;
            }
        }

        static float MusicVolume
        {
            get
            {
                var data = PlayerData.Current;
                return data.musicOn ? data.musicVolume : 0f;
            }
        }

        void OnUiSound(UiSound sound, float pitch) => Play(sound, pitch);

        public void Play(UiSound sound, float pitch = 1f)
        {
            if (UiClips.TryGetValue(sound, out string name))
                Play(name, pitch);
        }

        // Пока идёт интро студии, слышен только его фирменный звук.
        public static bool Silenced { get; set; }

        public void Play(string clipName, float pitch = 1f)
        {
            float volume = SfxVolume;
            if (volume <= 0f || Silenced)
                return;
            float now = Time.unscaledTime;
            float interval = RepeatInterval.TryGetValue(clipName, out float custom) ? custom : SameClipInterval;
            if (_lastPlayed.TryGetValue(clipName, out float last) && now - last < interval)
                return;
            var clip = Clip("Sfx/", clipName);
            if (clip == null)
                return;
            _lastPlayed[clipName] = now;

            var voice = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Length;
            voice.pitch = pitch;
            voice.PlayOneShot(clip, volume * (ClipVolume.TryGetValue(clipName, out float scale) ? scale : 1f));
        }

        public void PlayMusic(MusicTrack track)
        {
            if (track == _track)
                return;
            _track = track;
            if (MusicVolume <= 0f)
            {
                StopMusicNow();
                return;
            }
            StartTrack(track);
        }

        void StartTrack(MusicTrack track)
        {
            var clip = track == MusicTrack.None ? null : Clip("Music/", track == MusicTrack.Game ? "music_game_loop" : "music_menu_loop");
            var next = _activeMusic == _musicA ? _musicB : _musicA;
            next.Stop();
            next.clip = clip;
            next.volume = 0f;
            if (clip != null)
                next.Play();
            _activeMusic = next;
            _fade = 0f;
        }

        void StopMusicNow()
        {
            _musicA.Stop();
            _musicB.Stop();
            _musicA.clip = null;
            _musicB.clip = null;
        }

        // Музыка — фон: в меню тише эффектов, в игре ещё тише, чтобы не надоедала.
        static float TrackGain(MusicTrack track) => track == MusicTrack.Game ? 0.35f : 0.45f;

        void Update()
        {
            float target = MusicVolume * TrackGain(_track);
            var other = _activeMusic == _musicA ? _musicB : _musicA;
            if (_fade < 1f)
            {
                _fade = Mathf.Min(1f, _fade + Time.unscaledDeltaTime / CrossfadeTime);
                // Равная мощность: при линейном переходе посередине заметно проседает громкость.
                _activeMusic.volume = target * Mathf.Sin(_fade * Mathf.PI * 0.5f);
                other.volume = target * Mathf.Cos(_fade * Mathf.PI * 0.5f);
                if (_fade >= 1f)
                {
                    other.Stop();
                    other.clip = null;
                }
            }
            else
            {
                _activeMusic.volume = target;
            }
        }

        void ApplySettings()
        {
            bool musicAllowed = MusicVolume > 0f;
            bool playing = _activeMusic.isPlaying;
            if (!musicAllowed && playing)
                StopMusicNow();
            else if (musicAllowed && !playing && _track != MusicTrack.None)
                StartTrack(_track);
        }

        void OnScreenShown(UiScreen screen, bool animated)
        {
            // На заставке музыку включает она сама, когда закончится интро студии.
            if (screen.Id == ScreenId.Splash)
                return;
            if (animated)
                Play(UiSound.Whoosh);
            PlayMusic(screen.Id == ScreenId.Game ? MusicTrack.Game : MusicTrack.Menu);
        }

        AudioClip Clip(string folder, string name)
        {
            string key = folder + name;
            if (_clips.TryGetValue(key, out var clip))
                return clip;
            clip = Resources.Load<AudioClip>(key);
            if (clip == null)
                Debug.LogWarning($"[AudioService] Missing clip {key}");
            _clips[key] = clip;
            return clip;
        }
    }
}
