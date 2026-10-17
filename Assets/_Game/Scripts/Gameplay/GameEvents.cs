using System;
using BottleSort.Core;

namespace BottleSort.Gameplay
{
    // События игрового поля для звука, вибрации, аналитики. Время совпадает с картинкой:
    // PourStarted — когда пошла струя, BottleCompleted — когда налив закончился, LevelWon — после последней анимации.
    // Игровые звуки (налив, ошибка, собранная бутылка, победа) идут только отсюда, через UiFeedback их больше нет.
    public static class GameEvents
    {
        // MoveOutcome.Amount — сколько слоёв переливается
        public static event Action<Move, MoveOutcome> PourStarted;
        public static event Action<Move, MoveOutcome> PourFinished;
        // Индекс бутылки и собранный цвет
        public static event Action<int, int> BottleCompleted;
        public static event Action<MoveError> InvalidMove;

        // Индекс бутылки и сколько ходов льда осталось; 0 — растаял
        public static event Action<int, int> IceChanged;
        public static event Action<int> BottleUnlocked;
        // Индекс бутылки и сколько слоёв «?» открылось
        public static event Action<int, int> HiddenRevealed;

        public static event Action UndoUsed;
        public static event Action<Move> HintShown;
        public static event Action RestartUsed;
        public static event Action ExtraBottleAdded;

        // Номер уровня и звёзды
        public static event Action<int, int> LevelWon;

        public static void RaisePourStarted(Move move, MoveOutcome outcome) => PourStarted?.Invoke(move, outcome);
        public static void RaisePourFinished(Move move, MoveOutcome outcome) => PourFinished?.Invoke(move, outcome);
        public static void RaiseBottleCompleted(int bottle, int color) => BottleCompleted?.Invoke(bottle, color);
        public static void RaiseInvalidMove(MoveError error) => InvalidMove?.Invoke(error);
        public static void RaiseIceChanged(int bottle, int turnsLeft) => IceChanged?.Invoke(bottle, turnsLeft);
        public static void RaiseBottleUnlocked(int bottle) => BottleUnlocked?.Invoke(bottle);
        public static void RaiseHiddenRevealed(int bottle, int layers) => HiddenRevealed?.Invoke(bottle, layers);
        public static void RaiseUndoUsed() => UndoUsed?.Invoke();
        public static void RaiseHintShown(Move move) => HintShown?.Invoke(move);
        public static void RaiseRestartUsed() => RestartUsed?.Invoke();
        public static void RaiseExtraBottleAdded() => ExtraBottleAdded?.Invoke();
        public static void RaiseLevelWon(int level, int stars) => LevelWon?.Invoke(level, stars);
    }
}
