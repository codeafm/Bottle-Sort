using System.Collections;
using System.IO;
using BottleSort.Services;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace BottleSort.UI.Tests
{
    // Карточки с выбором «за рекламу или обычно» (две кнопки) и обычная карточка с одной кнопкой:
    // снимки в Docs/Screens/popups и проверка, что вторая кнопка — объект из префаба и не наезжает на первую.
    // Запуск: -runTests -testPlatform PlayMode -testFilter AdChoiceCapture (без -nographics).
    [Explicit, Category("MockupCompare")]
    public sealed class AdChoiceCapture
    {
        const int Width = 1080;
        const int Height = 2337;
        const string OutputDir = "Docs/Screens/popups";

        string _backup;
        RenderTexture _target;
        Camera _camera;

        [SetUp]
        public void SaveProgress()
        {
            _backup = File.Exists(PlayerData.SavePath) ? File.ReadAllText(PlayerData.SavePath) : null;
            PlayerData.DeleteSave();
            var data = PlayerData.Current;
            data.coins = 1250;
            data.highestUnlocked = 24;
            data.tutorialGiftTaken = true;
            data.Save();
            float top = 44f * (Width / 390f) / Height;
            SafeAreaFitter.Simulated = new Rect(0f, 0f, Screen.width, Screen.height * (1f - top));
            _target = new RenderTexture(Width, Height, 24);
            Directory.CreateDirectory(OutputDir);
        }

        [TearDown]
        public void RestoreProgress()
        {
            SafeAreaFitter.Simulated = null;
            if (_camera != null)
                _camera.targetTexture = null;
            _target.Release();
            PlayerData.DeleteSave();
            if (_backup != null)
                File.WriteAllText(PlayerData.SavePath, _backup);
            PlayerData.Reload();
        }

        [UnityTest]
        public IEnumerator CardsWithTwoButtons()
        {
            yield return Open(SceneLoader.MenuScene);
            var router = FindRouter();

            router.ShowPopup(ScreenId.Calendar);
            yield return Settle(0.8f);
            CheckPair(router.Get<CalendarPopup>());
            Shot("calendar_x2");
            router.ClosePopup(router.Get<CalendarPopup>());
            yield return Settle(0.4f);

            var card = router.Get<RewardPopup>();
            card.SetupChoice("daily_gift_title", "daily_gift_text", EconomyRules.DailyGift, "reward_double_ad", null, "gift_claim", null);
            router.ShowPopup(ScreenId.Reward);
            yield return Settle(0.8f);
            CheckPair(card);
            Shot("daily_gift_x2");
            router.ClosePopup(card);
            yield return Settle(0.4f);

            // Та же карточка с одной кнопкой: вторая прячется, карточка и кнопка — как в префабе.
            card.Setup("chest_chapter_title", "chest_chapter_text", new RewardBundle(150), "chest_ok", null, 1);
            router.ShowPopup(ScreenId.Reward);
            yield return Settle(0.8f);
            Assert.IsFalse(card.transform.Find("Content/Panel/SecondaryButton").gameObject.activeSelf, "single card shows two buttons");
            Shot("reward_single");
            router.ClosePopup(card);

            yield return Open(SceneLoader.GameScene);
            router = FindRouter();
            var game = router.Get<GameScreen>();
            float deadline = Time.realtimeSinceStartup + 20f;
            while ((router.Current != game || game.IsBusy) && Time.realtimeSinceStartup < deadline)
                yield return null;
            // На снимке канвас рисует камера, и мировые полки поля встали бы поверх карточки; в игре канвас overlay.
            var shelves = GameObject.Find("Field Shelves");
            if (shelves != null)
                shelves.SetActive(false);
            card = router.Get<RewardPopup>();
            card.SetupChoice("lose_moves_title", "lose_moves_text", default, "lose_moves_ad", null, "lose_restart", null);
            router.ShowPopup(ScreenId.Reward);
            yield return Settle(0.8f);
            CheckPair(card);
            Shot("lose_moves");
            router.ClosePopup(card);
            yield return Settle(0.4f);

            card.SetupChoice("deadend_title", "deadend_text", new RewardBundle(0, bottles: 1), "deadend_bottle_ad", null,
                "lose_restart", null);
            router.ShowPopup(ScreenId.Reward);
            yield return Settle(0.8f);
            CheckPair(card);
            Shot("deadend");
        }

        static void CheckPair(UiScreen popup)
        {
            var panel = (RectTransform)popup.transform.Find("Content/Panel");
            var primary = (RectTransform)panel.Find("Claim");
            var secondary = (RectTransform)panel.Find("SecondaryButton");
            Assert.IsNotNull(secondary, popup.name + ": no SecondaryButton in the prefab");
            Assert.IsTrue(secondary.gameObject.activeSelf, popup.name + ": second button is hidden");
            Assert.IsTrue(primary.Find("Face/VideoIcon").gameObject.activeSelf, popup.name + ": no video icon");
            Assert.AreEqual(2, panel.GetComponentsInChildren<UnityEngine.UI.Button>().Length - CountOtherButtons(panel),
                popup.name + ": buttons were cloned at runtime");
            Assert.Greater(primary.offsetMin.y, secondary.offsetMax.y, popup.name + ": buttons overlap");
        }

        // Кнопки карточки, кроме пары «за рекламу / обычно» (например, крестик бонуса недели).
        static int CountOtherButtons(RectTransform panel)
        {
            int other = 0;
            foreach (var button in panel.GetComponentsInChildren<UnityEngine.UI.Button>())
                if (button.name != "Claim" && button.name != "SecondaryButton")
                    other++;
            return other;
        }

        IEnumerator Open(string scene)
        {
            SceneManager.LoadScene(scene);
            yield return null;
            float deadline = Time.realtimeSinceStartup + 10f;
            while ((SceneLoader.IsBusy || FindRouter() == null || FindRouter().Current == null) && Time.realtimeSinceStartup < deadline)
                yield return null;
            _camera = Camera.main;
            _camera.targetTexture = _target;
            var canvas = GameObject.Find("Canvas").GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = _camera;
            canvas.planeDistance = 1f;
            yield return Settle(1.6f);
        }

        void Shot(string name)
        {
            _camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = _target;
            var texture = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(Path.Combine(OutputDir, name + ".jpg"), texture.EncodeToJPG(85));
            Object.Destroy(texture);
        }

        static IEnumerator Settle(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until)
                yield return null;
        }

        static ScreenRouter FindRouter()
        {
            var router = Object.FindFirstObjectByType<ScreenRouter>();
            return router != null && router.gameObject.scene == SceneManager.GetActiveScene() ? router : null;
        }
    }
}
