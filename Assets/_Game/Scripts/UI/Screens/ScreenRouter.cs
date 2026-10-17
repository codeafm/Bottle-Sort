using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BottleSort.UI
{
    // У каждой сцены (Splash, Menu, Game) свой роутер со своими экранами. Экраны из другой сцены
    // открываются через SceneLoader.
    public sealed class ScreenRouter : MonoBehaviour
    {
        [SerializeField] UiScreen[] screens;
        [SerializeField] ScreenId startScreen = ScreenId.Splash;
        [SerializeField] ThemeBackdrop backdrop;

        readonly Dictionary<ScreenId, UiScreen> _byId = new Dictionary<ScreenId, UiScreen>();
        readonly List<UiScreen> _popups = new List<UiScreen>();
        UiScreen _current;

        // Смену экрана слушают музыка и реклама (баннер только на некоторых экранах).
        public static event System.Action<UiScreen, bool> ScreenShown;

        public UiScreen Current => _current;
        public IReadOnlyList<UiScreen> Screens => screens;

        void Awake()
        {
            var data = PlayerData.Current;
            Loc.Set(System.Enum.TryParse(data.language, out Language saved) ? saved : Loc.SystemDefault);
            foreach (var screen in screens)
            {
                _byId[screen.Id] = screen;
                screen.Attach(this);
            }
        }

        // Сцену можно открыть и напрямую из редактора: тогда Game берёт текущий уровень игрока.
        void Start()
        {
            var first = SceneLoader.TakePendingScreen() ?? startScreen;
            int? level = SceneLoader.TakePendingLevel();
            if (!_byId.ContainsKey(first))
                first = startScreen;
            if (first == ScreenId.Game)
                Get<GameScreen>().Prepare(level ?? PlayerData.Current.highestUnlocked);
            Show(first, false);
        }

        public T Get<T>() where T : UiScreen
        {
            foreach (var screen in screens)
                if (screen is T typed)
                    return typed;
            return null;
        }

        public void Show(ScreenId id, bool animated = true)
        {
            if (!_byId.TryGetValue(id, out var next))
            {
                SceneLoader.Open(id);
                return;
            }
            CloseAllPopups();
            if (_current != null && _current != next)
                _current.Close(animated);
            _current = next;
            next.Open(animated);
            RefreshBackdrop();
            ScreenShown?.Invoke(next, animated);
        }

        public void ShowPopup(ScreenId id)
        {
            if (!_byId.TryGetValue(id, out var popup))
            {
                Debug.LogWarning($"[ScreenRouter] Popup {id} is not in this scene");
                return;
            }
            if (_popups.Contains(popup))
                return;
            _popups.Add(popup);
            popup.Open(true);
            UiFeedback.Play(UiSound.Popup);
        }

        public void ClosePopup(UiScreen popup)
        {
            if (_popups.Remove(popup))
                popup.Close(true);
        }

        // В сцене игры следующий уровень открывается без перезагрузки, из меню — через сцену Game.
        public void PlayLevel(int level)
        {
            var game = Get<GameScreen>();
            if (game == null)
            {
                SceneLoader.Open(ScreenId.Game, level);
                return;
            }
            game.Prepare(level);
            Show(ScreenId.Game);
        }

        public void RefreshBackdrop()
        {
            var theme = _current != null ? _current.BackdropTheme : null;
            if (theme.HasValue)
                backdrop.SetTheme(theme.Value);
        }

        void CloseAllPopups()
        {
            foreach (var popup in _popups)
                popup.Close(true);
            _popups.Clear();
        }

        void Update()
        {
            // На Android системная «назад» приходит как Escape.
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
                return;

            if (_popups.Count > 0)
            {
                var top = _popups[_popups.Count - 1];
                if (!top.OnBack())
                    ClosePopup(top);
                return;
            }

            if (_current != null && !_current.OnBack() && _current.Id == ScreenId.Menu)
                Application.Quit();
        }
    }
}
