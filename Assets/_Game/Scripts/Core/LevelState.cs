using System;
using System.Collections.Generic;

namespace BottleSort.Core
{
    // Позиция на поле: все бутылки и счётчики фишек. Правила хода и победы живут здесь,
    // и ими же пользуются солвер и генератор, чтобы правила не разъехались.
    public sealed class LevelState
    {
        private readonly List<Bottle> bottles;

        // Сколько слоёв каждого цвета на уровне (U). Обычная бутылка вмещает U, большая колба 2U.
        public int Unit { get; }

        public LevelState(int unit, IEnumerable<Bottle> bottles)
        {
            if (unit < 1 || unit > Bottle.MaxCapacity)
                throw new ArgumentOutOfRangeException(nameof(unit));
            Unit = unit;
            this.bottles = new List<Bottle>(bottles);
        }

        public IReadOnlyList<Bottle> Bottles => bottles;
        public int BottleCount => bottles.Count;
        public Bottle this[int index] => bottles[index];

        // Раскладка вида "RRGB BGRG - -": бутылка — слои снизу вверх, '-' — пустая.
        public static LevelState FromLayout(int unit, string layout)
        {
            var list = new List<Bottle>();
            foreach (string token in layout.Split(new[] { ' ', '|' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var bottle = new Bottle(unit);
                if (token != "-")
                    foreach (char letter in token)
                        bottle.Push(Palette.FromLetter(letter));
                list.Add(bottle);
            }
            return new LevelState(unit, list);
        }

        public void AddBottle(Bottle bottle) => bottles.Add(bottle);

        public int CountColors()
        {
            int mask = 0;
            foreach (var bottle in bottles)
                for (int i = 0; i < bottle.Count; i++)
                    mask |= 1 << bottle[i];
            int count = 0;
            for (; mask != 0; mask &= mask - 1)
                count++;
            return count;
        }

        // Каждого цвета ровно Unit слоёв — на этом держится проверка победы.
        public bool IsWellFormed()
        {
            var perColor = new int[Palette.MaxColors];
            foreach (var bottle in bottles)
                for (int i = 0; i < bottle.Count; i++)
                    perColor[bottle[i]]++;
            foreach (int count in perColor)
                if (count != 0 && count != Unit)
                    return false;
            return true;
        }

        public bool IsWon
        {
            get
            {
                foreach (var bottle in bottles)
                    if (!bottle.IsDone(Unit))
                        return false;
                return true;
            }
        }

        public int CompletedCount
        {
            get
            {
                int count = 0;
                foreach (var bottle in bottles)
                    if (bottle.IsCompleted(Unit))
                        count++;
                return count;
            }
        }

        // Можно ли взять бутылку первым тапом.
        public MoveError CheckSelection(int index)
        {
            if ((uint)index >= (uint)bottles.Count)
                return MoveError.BadIndex;
            var bottle = bottles[index];
            if (bottle.IsLocked)
                return MoveError.SourceLocked;
            if (bottle.IsFrozen)
                return MoveError.SourceFrozen;
            if (bottle.IsEmpty)
                return MoveError.SourceEmpty;
            // Собранный цвет ещё может понадобиться фигурке спасения: тогда бутылку можно взять.
            if (bottle.IsCompleted(Unit) && !FigureWaitsFor(bottle.TopColor))
                return MoveError.SourceCompleted;
            if (!bottle.CanPourOut)
                return MoveError.SourceNoPourOut;
            return MoveError.None;
        }

        private bool FigureWaitsFor(int color)
        {
            foreach (var bottle in bottles)
                if (bottle.Recipe != null && !bottle.IsFull && bottle.Accepts(color))
                    return true;
            return false;
        }

        public MoveError Check(Move move)
        {
            if ((uint)move.From >= (uint)bottles.Count || (uint)move.To >= (uint)bottles.Count)
                return MoveError.BadIndex;

            var source = bottles[move.From];
            if (source.IsLocked)
                return MoveError.SourceLocked;
            if (source.IsFrozen)
                return MoveError.SourceFrozen;

            if (move.Kind == MoveKind.Flip)
            {
                if (source.FlipsLeft <= 0)
                    return MoveError.NoFlipsLeft;
                return source.Count < 2 ? MoveError.SourceEmpty : MoveError.None;
            }

            if (move.From == move.To)
                return MoveError.SameBottle;
            if (source.IsEmpty)
                return MoveError.SourceEmpty;
            if (!source.CanPourOut)
                return MoveError.SourceNoPourOut;

            var target = bottles[move.To];
            if (target.IsLocked)
                return MoveError.TargetLocked;
            if (target.IsFrozen)
                return MoveError.TargetFrozen;
            if (target.IsFull)
                return MoveError.TargetFull;
            // Фигурке спасения неважно, что сверху: она ждёт следующий цвет рецепта.
            if (target.Recipe == null && !target.IsEmpty && target.TopColor != source.TopColor)
                return MoveError.ColorMismatch;
            if (!target.Accepts(source.TopColor))
                return MoveError.TargetRejectsColor;
            return MoveError.None;
        }

        public bool CanApply(Move move) => Check(move) == MoveError.None;

        // Сколько слоёв уйдёт: весь верхний отрезок, но не больше свободного места и лимита пипетки.
        public int PourAmount(Move move)
        {
            var source = bottles[move.From];
            var target = bottles[move.To];
            int amount = Math.Min(source.TopRunLength, Math.Min(target.FreeSpace, target.RecipeRoomFor(source.TopColor)));
            if (source.PourLimit > 0)
                amount = Math.Min(amount, source.PourLimit);
            return amount;
        }

        public void GetLegalMoves(List<Move> result)
        {
            result.Clear();
            for (int from = 0; from < bottles.Count; from++)
            {
                var source = bottles[from];
                if (source.IsEmpty || source.IsBlocked)
                    continue;

                if (source.FlipsLeft > 0 && source.Count > 1)
                    result.Add(Move.Flip(from));

                if (!source.CanPourOut)
                    continue;
                for (int to = 0; to < bottles.Count; to++)
                {
                    var move = Move.Pour(from, to);
                    if (to != from && Check(move) == MoveError.None)
                        result.Add(move);
                }
            }
        }

        public MoveOutcome Apply(Move move)
        {
            var error = Check(move);
            if (error != MoveError.None)
                throw new InvalidOperationException($"Move {move} is not allowed: {error}");

            var outcome = new MoveOutcome { Move = move, Color = Palette.AnyColor };
            var source = bottles[move.From];

            if (move.Kind == MoveKind.Flip)
            {
                source.Reverse();
                source.FlipsLeft--;
            }
            else
            {
                var target = bottles[move.To];
                bool sourceWasDone = source.IsCompleted(Unit);
                bool targetWasDone = target.IsCompleted(Unit);

                outcome.Color = source.TopColor;
                outcome.Amount = PourAmount(move);
                for (int i = 0; i < outcome.Amount; i++)
                    target.Push(source.Pop());

                // С большой колбой бутылка может собраться и у источника: под отлитым слоем лежали U одинаковых.
                outcome.CompletedBottle = (!targetWasDone && target.IsCompleted(Unit))
                    || (!sourceWasDone && source.IsCompleted(Unit));
            }

            AfterMove(ref outcome);
            return outcome;
        }

        private void AfterMove(ref MoveOutcome outcome)
        {
            foreach (var bottle in bottles)
            {
                if (bottle.FrozenTurns > 0 && --bottle.FrozenTurns == 0)
                    outcome.ThawedAny = true;
                if (bottle.RevealTop() > 0)
                    outcome.RevealedAny = true;
            }

            outcome.UnlockedAny = UnlockByCompletedColors();
            outcome.Won = IsWon;
        }

        private bool UnlockByCompletedColors()
        {
            bool anyLocked = false;
            foreach (var bottle in bottles)
                anyLocked |= bottle.IsLocked;
            if (!anyLocked)
                return false;

            int completedMask = 0;
            foreach (var bottle in bottles)
                if (bottle.IsCompleted(Unit))
                    completedMask |= 1 << bottle.TopColor;
            if (completedMask == 0)
                return false;

            bool unlocked = false;
            foreach (var bottle in bottles)
            {
                if (!bottle.IsLocked)
                    continue;
                bool keyCollected = bottle.LockColor == Palette.AnyColor || (completedMask & (1 << bottle.LockColor)) != 0;
                if (keyCollected)
                {
                    bottle.IsLocked = false;
                    unlocked = true;
                }
            }
            return unlocked;
        }

        public LevelState Clone()
        {
            var copy = new List<Bottle>(bottles.Count);
            foreach (var bottle in bottles)
                copy.Add(bottle.Clone());
            return new LevelState(Unit, copy);
        }

        public bool StateEquals(LevelState other)
        {
            if (other == null || Unit != other.Unit || bottles.Count != other.bottles.Count)
                return false;
            for (int i = 0; i < bottles.Count; i++)
                if (!bottles[i].StateEquals(other.bottles[i]))
                    return false;
            return true;
        }

        public override string ToString() => string.Join(" ", bottles);
    }
}
