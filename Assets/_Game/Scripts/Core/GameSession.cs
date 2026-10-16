using System;

namespace BottleSort.Core
{
    // Одна попытка прохождения уровня: текущая позиция, история, бустеры. Без анимаций и UI.
    public sealed class GameSession
    {
        private readonly LevelState initial;
        private readonly UndoStack history = new UndoStack();

        public LevelState State { get; private set; }
        public int Par { get; }
        public int MoveLimit { get; }
        public bool ExtraBottleAllowed { get; }
        public bool ExtraBottleUsed { get; private set; }

        public GameSession(LevelState initial, int par, int moveLimit = 0, bool extraBottleAllowed = true)
        {
            this.initial = initial?.Clone() ?? throw new ArgumentNullException(nameof(initial));
            Par = par;
            MoveLimit = moveLimit;
            ExtraBottleAllowed = extraBottleAllowed;
            State = initial.Clone();
        }

        // Отмена уменьшает счётчик, поэтому ходы = длина истории.
        public int MovesMade => history.Count;
        public bool CanUndo => history.Count > 0;
        public bool IsWon => State.IsWon;
        public bool HasMoveLimit => MoveLimit > 0;
        public int MovesLeft => HasMoveLimit ? Math.Max(MoveLimit - MovesMade, 0) : int.MaxValue;
        public bool IsOutOfMoves => HasMoveLimit && MovesMade >= MoveLimit && !IsWon;
        public int Stars => StarRating.Calculate(MovesMade, Par, ExtraBottleUsed);
        public bool CanAddBottle => ExtraBottleAllowed && !ExtraBottleUsed;

        public MoveError TryMove(Move move, out MoveOutcome outcome)
        {
            outcome = default;
            if (IsWon || IsOutOfMoves)
                return MoveError.LevelOver;

            var error = State.Check(move);
            if (error != MoveError.None)
                return error;

            history.Push(State);
            outcome = State.Apply(move);
            return MoveError.None;
        }

        public bool Undo()
        {
            if (!history.TryPop(out var previous))
                return false;

            // Бутылка от бустера переживает отмену: в старом снимке её ещё нет, добавляем пустой.
            for (int i = previous.BottleCount; i < State.BottleCount; i++)
                previous.AddBottle(State[i].CloneEmpty());

            State = previous;
            return true;
        }

        public void Restart()
        {
            history.Clear();
            ExtraBottleUsed = false;
            State = initial.Clone();
        }

        public bool AddBottle()
        {
            if (!CanAddBottle)
                return false;
            State.AddBottle(new Bottle(State.Unit));
            ExtraBottleUsed = true;
            return true;
        }

        public Move? Hint(SolverOptions options = null) => Solver.Hint(State, options);
    }
}
