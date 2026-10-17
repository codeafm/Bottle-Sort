using System;
using System.Collections;
using BottleSort.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BottleSort.UI
{
    // Фирменное интро студии CODEAFM перед заставкой игры, перенесено из MahjongVita один в один:
    // белый силуэт AF из исходного рисунка (шейдер Brand/CodeafmReveal), CODEAFM и GAMING, мягкий белый ореол
    // на тёмном фоне и фирменный звук. 3.4 с; картинка идёт по времени звука, чтобы акценты совпадали.
    public sealed class BrandIntro : MonoBehaviour
    {
        public const float Duration = 3.4f;
        const float RevealTime = 0.5f;
        const float EmblemY = 102f;
        // В MahjongVita надписи стоят на -180 и -267, но TMP сажает строку выше старого Text: сверено по эталонному кадру.
        const float WordmarkY = -195f;
        const float GamingY = -278f;
        const float StageWidth = 1000f;
        const float StageHeight = 920f;

        [SerializeField] CanvasGroup overlay;
        [SerializeField] RectTransform stage;
        [SerializeField] CanvasGroup artwork;
        [SerializeField] RawImage left;
        [SerializeField] RawImage right;
        [SerializeField] TMP_Text wordmark;
        [SerializeField] TMP_Text gaming;
        [SerializeField] Image halo;
        [SerializeField] AudioSource signature;

        Material _leftReveal;
        Material _rightReveal;
        bool _paused;

        public bool IsPlaying { get; private set; }

        // done зовётся, когда интро погасло и начинает растворяться тёмный фон: под ним уже можно показывать заставку.
        public void Play(Action done)
        {
            gameObject.SetActive(true);
            StartCoroutine(Run(done));
        }

        // Сразу убрать интро (снимки в тестах, повторный показ заставки).
        public void Skip()
        {
            StopAllCoroutines();
            AudioService.Silenced = false;
            IsPlaying = false;
            gameObject.SetActive(false);
        }

        IEnumerator Run(Action done)
        {
            IsPlaying = true;
            AudioService.Silenced = true;
            if (AudioService.Instance != null)
                AudioService.Instance.PlayMusic(MusicTrack.None);
            overlay.alpha = 1f;
            overlay.blocksRaycasts = true;
            MakeRevealMaterials();
            Fit();
            Present(0f);
            StartSignature();
            yield return null;

            float elapsed = 0f;
            while (elapsed < Duration)
            {
                Present(elapsed / Duration);
                yield return null;
                if (_paused)
                    continue;
                // Пока звук играет, время берём у него; после его конца (3.25 с) — обычные кадры, без скачков.
                float step = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                elapsed = signature != null && signature.isPlaying ? Mathf.Max(elapsed, signature.time) : elapsed + step;
            }
            Present(1f);
            AudioService.Silenced = false;
            done?.Invoke();

            overlay.blocksRaycasts = false;
            for (float time = 0f; time < RevealTime; time += Mathf.Min(Time.unscaledDeltaTime, 0.05f))
            {
                overlay.alpha = 1f - Easing.Apply(Ease.InOutSine, time / RevealTime);
                yield return null;
            }
            IsPlaying = false;
            gameObject.SetActive(false);
        }

        void StartSignature()
        {
            if (signature == null || signature.clip == null)
                return;
            ApplyVolume();
            signature.Play();
        }

        // Настройка «Звуки» и громкость из настроек, как у остальных эффектов.
        void ApplyVolume()
        {
            var data = PlayerData.Current;
            signature.mute = !data.soundsOn;
            signature.volume = 0.8f * data.soundVolume;
        }

        void Update()
        {
            if (signature != null && IsPlaying)
                ApplyVolume();
        }

        void OnApplicationPause(bool paused)
        {
            _paused = paused;
            if (signature == null || !IsPlaying)
                return;
            if (paused)
                signature.Pause();
            else
                signature.UnPause();
        }

        void OnRectTransformDimensionsChange() => Fit();

        void OnDisable()
        {
            IsPlaying = false;
            if (signature != null)
                signature.Stop();
        }

        void OnDestroy()
        {
            if (_leftReveal != null)
                Destroy(_leftReveal);
            if (_rightReveal != null)
                Destroy(_rightReveal);
        }

        // В сцене у половинок эмблемы материалы-ассеты (видно в редакторе), в игре — свои копии, их раскрываем.
        void MakeRevealMaterials()
        {
            if (_leftReveal != null)
                return;
            _leftReveal = new Material(left.material) { name = "CODEAFM left reveal" };
            _rightReveal = new Material(right.material) { name = "CODEAFM right reveal" };
            left.material = _leftReveal;
            right.material = _rightReveal;
        }

        // Один общий масштаб: эмблема и надписи не искажаются ни на планшетах, ни на узких телефонах.
        void Fit()
        {
            if (stage == null)
                return;
            var area = ((RectTransform)stage.parent).rect.size;
            stage.localScale = Vector3.one * Mathf.Max(0.01f, Mathf.Min(area.x / StageWidth, area.y / StageHeight));
        }

        // Кадр интро по доле времени 0…1. Тайминги из MahjongVita.
        public void Present(float normalized)
        {
            float t = Mathf.Clamp01(normalized);
            float fadeIn = Mathf.SmoothStep(0f, 1f, Phase(t, 0.015f, 0.12f));
            float fadeOut = 1f - Mathf.SmoothStep(0f, 1f, Phase(t, 0.88f, 1f));
            artwork.alpha = fadeIn * fadeOut;
            Emblem(left, _leftReveal, EaseOut(Phase(t, 0.045f, 0.39f)), -1);
            Emblem(right, _rightReveal, EaseOut(Phase(t, 0.085f, 0.43f)), 1);
            Word(wordmark, EaseOut(Phase(t, 0.31f, 0.63f)), WordmarkY, 20f);
            Word(gaming, EaseOut(Phase(t, 0.48f, 0.71f)), GamingY, 12f);
            halo.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.035f, 0.08f, EaseOut(Phase(t, 0.08f, 0.48f))));
        }

        static void Emblem(RawImage image, Material reveal, float amount, int side)
        {
            float motion = 1f - amount;
            var rect = image.rectTransform;
            rect.anchoredPosition = new Vector2(side * 160f + side * 104f * motion, EmblemY - 27f * motion);
            rect.localEulerAngles = new Vector3(0f, 0f, side * -5f * motion);
            rect.localScale = Vector3.one * (1f - 0.07f * motion);
            if (reveal != null)
                reveal.SetFloat("_Reveal", Mathf.Lerp(-0.10f, 1.10f, amount));
        }

        static void Word(TMP_Text label, float amount, float y, float rise)
        {
            label.rectTransform.anchoredPosition = new Vector2(0f, y - rise * (1f - amount));
            label.alpha = amount;
        }

        static float Phase(float t, float start, float end) => Mathf.InverseLerp(start, end, t);
        static float EaseOut(float value) => 1f - Mathf.Pow(1f - Mathf.Clamp01(value), 3f);
    }
}
