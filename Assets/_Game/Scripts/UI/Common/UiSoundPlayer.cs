using System;
using UnityEngine;

namespace BottleSort.UI
{
    // Временный проигрыватель звуков интерфейса. Когда появится аудиосервис, он подпишется
    // на UiFeedback.SoundRequested сам, а этот компонент можно будет убрать из сцены.
    [RequireComponent(typeof(AudioSource))]
    public sealed class UiSoundPlayer : MonoBehaviour
    {
        [Serializable]
        public struct Entry
        {
            public UiSound sound;
            public AudioClip clip;
            [Range(0f, 1f)] public float volume;
        }

        [SerializeField] Entry[] clips;

        AudioSource _source;

        void Awake() => _source = GetComponent<AudioSource>();
        void OnEnable() => UiFeedback.SoundRequested += Play;
        void OnDisable() => UiFeedback.SoundRequested -= Play;

        void Play(UiSound sound)
        {
            foreach (var entry in clips)
            {
                if (entry.sound != sound || entry.clip == null)
                    continue;
                _source.PlayOneShot(entry.clip, entry.volume > 0f ? entry.volume : 1f);
                return;
            }
        }
    }
}
