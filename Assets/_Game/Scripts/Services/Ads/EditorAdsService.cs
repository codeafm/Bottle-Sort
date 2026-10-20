using System;
using BottleSort.UI;
using UnityEngine;
using UnityEngine.UI;

namespace BottleSort.Services
{
    // Реклама без SDK: в редакторе и в сборках, где Yandex Mobile Ads ещё не подключён.
    // Rewarded «досматривается» за полсекунды, interstitial только пишет в лог.
    public sealed class EditorAdsService : IAdsService
    {
        const float FakeAdSeconds = 0.5f;

        readonly MainThread _runner;
        GameObject _bannerStub;

        // Для проверки веток «реклама не загрузилась» и «закрыл, не досмотрев».
        public static bool SimulateNoFill;
        public static bool SimulateSkip;

        public EditorAdsService(MainThread runner)
        {
            _runner = runner;
        }

        public bool IsRewardedReady => !SimulateNoFill;
        public bool IsInterstitialReady => !SimulateNoFill;

        public void Initialize() => Debug.Log("[Ads] Stub ads service, no SDK");

        public void LoadInterstitial()
        {
        }

        public void LoadRewarded()
        {
        }

        public void ShowRewarded(Action<bool> done)
        {
            if (SimulateNoFill)
            {
                done?.Invoke(false);
                return;
            }
            Debug.Log("[Ads] Rewarded shown (stub)");
            _runner.After(FakeAdSeconds, () => done?.Invoke(!SimulateSkip));
        }

        public void ShowInterstitial(Action done)
        {
            Debug.Log("[Ads] Interstitial shown (stub)");
            _runner.After(FakeAdSeconds, () => done?.Invoke());
        }

        // SDK нет, поэтому рисуем серую плашку 320×50 там, где на телефоне встанет баннер:
        // с плейсментом и номером загрузки, чтобы в редакторе было видно, когда пришёл новый.
        readonly int[] _loads = new int[3];
        BannerPlacement _shown;

        public void ShowBanner(BannerPlacement placement)
        {
            _shown = placement;
            if (_bannerStub == null)
                _bannerStub = CreateBannerStub();
            _bannerStub.SetActive(_loads[(int)placement] > 0);
            UpdateStubLabel();
        }

        public void HideBanner()
        {
            _shown = BannerPlacement.None;
            if (_bannerStub != null)
                _bannerStub.SetActive(false);
        }

        public void LoadBanner(BannerPlacement placement, Action<bool> done)
        {
            _runner.After(FakeAdSeconds, () =>
            {
                _loads[(int)placement]++;
                Debug.Log($"[Ads] Banner {placement} loaded (stub), #{_loads[(int)placement]}");
                if (_shown == placement)
                    ShowBanner(placement);
                done?.Invoke(true);
            });
        }

        void UpdateStubLabel()
        {
            var label = _bannerStub != null ? _bannerStub.GetComponentInChildren<Text>(true) : null;
            if (label != null)
                label.text = $"Баннер Yandex {_shown} №{_loads[(int)_shown]} (настоящий — только на телефоне)";
        }

        static GameObject CreateBannerStub()
        {
            var root = new GameObject("Banner Stub", typeof(Canvas), typeof(CanvasScaler));
            UnityEngine.Object.DontDestroyOnLoad(root);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            // Тот же масштаб, что у интерфейса игры: 390 pt макета = 975 единиц.
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(975f, 1732.5f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            var safe = new GameObject("SafeArea", typeof(RectTransform), typeof(SafeAreaFitter));
            var safeRect = (RectTransform)safe.transform;
            safeRect.SetParent(root.transform, false);
            safeRect.anchorMin = Vector2.zero;
            safeRect.anchorMax = Vector2.one;
            safeRect.offsetMin = safeRect.offsetMax = Vector2.zero;

            var banner = new GameObject("Banner 320x50", typeof(RectTransform), typeof(Image));
            var rect = (RectTransform)banner.transform;
            rect.SetParent(safeRect, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(320f, 50f) * 2.5f;
            rect.anchoredPosition = Vector2.zero;
            var image = banner.GetComponent<Image>();
            image.color = new Color(0.82f, 0.84f, 0.88f, 0.92f);
            image.raycastTarget = false;

            var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
            var labelRect = (RectTransform)label.transform;
            labelRect.SetParent(rect, false);
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
            var text = label.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 34;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.25f, 0.27f, 0.32f);
            text.text = "Баннер Yandex 320×50 (настоящий — только на телефоне)";
            text.raycastTarget = false;
            return root;
        }
    }
}
