using BottleSort.Core;
using BottleSort.Gameplay;
using BottleSort.UI;
using UnityEngine;

namespace BottleSort.Services
{
    // Связка игрового поля со звуком и вибрацией. Слушает GameEvents, сам GameScreen про сервисы не знает.
    public sealed class GameFeedback : MonoBehaviour
    {
        [SerializeField] AudioService audioService;

        public void Bind(AudioService audio) => audioService = audio;

        void OnEnable()
        {
            GameEvents.PourStarted += OnPourStarted;
            GameEvents.BottleCompleted += OnBottleCompleted;
            GameEvents.InvalidMove += OnInvalidMove;
            GameEvents.IceChanged += OnIceChanged;
            GameEvents.BottleUnlocked += OnBottleUnlocked;
            GameEvents.HiddenRevealed += OnHiddenRevealed;
            GameEvents.UndoUsed += OnUndoUsed;
            GameEvents.HintShown += OnHintShown;
            GameEvents.LevelWon += OnLevelWon;
        }

        void OnDisable()
        {
            GameEvents.PourStarted -= OnPourStarted;
            GameEvents.BottleCompleted -= OnBottleCompleted;
            GameEvents.InvalidMove -= OnInvalidMove;
            GameEvents.IceChanged -= OnIceChanged;
            GameEvents.BottleUnlocked -= OnBottleUnlocked;
            GameEvents.HiddenRevealed -= OnHiddenRevealed;
            GameEvents.UndoUsed -= OnUndoUsed;
            GameEvents.HintShown -= OnHintShown;
            GameEvents.LevelWon -= OnLevelWon;
        }

        void Play(UiSound sound, float pitch = 1f)
        {
            if (audioService != null)
                audioService.Play(sound, pitch);
        }

        void OnPourStarted(Move move, MoveOutcome outcome)
        {
            // Один слой — короткая струя. Чуть разный тон, чтобы серия переливаний не звучала одной записью.
            Play(outcome.Amount <= 1 ? UiSound.PourShort : UiSound.Pour, Random.Range(0.96f, 1.04f));
            Haptics.Light();
        }

        void OnBottleCompleted(int bottle, int color)
        {
            Play(UiSound.BottleComplete);
            Haptics.Success();
        }

        void OnInvalidMove(MoveError error)
        {
            Play(UiSound.Wrong);
            Haptics.Error();
        }

        // Лёд трескается на каждом ходе, а когда растаял совсем — звучит в полный тон.
        void OnIceChanged(int bottle, int turnsLeft) => Play(UiSound.IceCrack, turnsLeft > 0 ? 1.12f : 1f);

        void OnBottleUnlocked(int bottle)
        {
            Play(UiSound.Unlock);
            Haptics.Impact(HapticStrength.Medium);
        }

        void OnHiddenRevealed(int bottle, int layers) => Play(UiSound.Reveal);

        void OnUndoUsed() => Play(UiSound.Undo);

        void OnHintShown(Move move) => Play(UiSound.Hint);

        void OnLevelWon(int level, int stars)
        {
            Play(UiSound.Win);
            Haptics.Success();
        }
    }
}
