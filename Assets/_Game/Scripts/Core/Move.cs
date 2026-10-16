using System;

namespace BottleSort.Core
{
    public enum MoveKind : byte
    {
        Pour,
        Flip
    }

    public readonly struct Move : IEquatable<Move>
    {
        public readonly int From;
        public readonly int To;
        public readonly MoveKind Kind;

        private Move(int from, int to, MoveKind kind)
        {
            From = from;
            To = to;
            Kind = kind;
        }

        public static Move Pour(int from, int to) => new Move(from, to, MoveKind.Pour);

        // Переворот песочных часов: бутылка одна, поэтому From == To.
        public static Move Flip(int bottle) => new Move(bottle, bottle, MoveKind.Flip);

        public bool Equals(Move other) => From == other.From && To == other.To && Kind == other.Kind;
        public override bool Equals(object obj) => obj is Move other && Equals(other);
        public override int GetHashCode() => (From * 31 + To) * 2 + (int)Kind;
        public static bool operator ==(Move a, Move b) => a.Equals(b);
        public static bool operator !=(Move a, Move b) => !a.Equals(b);

        public override string ToString() => Kind == MoveKind.Flip ? $"flip {From}" : $"{From}->{To}";
    }

    // Причина, по которой ход нельзя сделать. UI по ней выбирает тост и тряску.
    public enum MoveError : byte
    {
        None,
        BadIndex,
        LevelOver,
        SameBottle,
        SourceEmpty,
        SourceCompleted,
        SourceLocked,
        SourceFrozen,
        SourceNoPourOut,
        TargetLocked,
        TargetFrozen,
        TargetFull,
        ColorMismatch,
        TargetRejectsColor,
        NoFlipsLeft,
        // На строгом обучающем уровне можно делать только показанный ход.
        NotRecommended
    }

    // Что случилось после хода: этого хватает, чтобы проиграть анимацию и показать тосты.
    public struct MoveOutcome
    {
        public Move Move;
        public int Color;
        public int Amount;
        public bool CompletedBottle;
        public bool UnlockedAny;
        public bool ThawedAny;
        public bool RevealedAny;
        public bool Won;
    }
}
