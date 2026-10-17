using System;
using System.Text;

namespace BottleSort.Core
{
    // Слои хранятся снизу вверх. Свойства из первой группы задаются при создании уровня и не меняются,
    // вторая группа (слои, лёд, замок, скрытые, перевороты) меняется по ходу игры.
    public sealed class Bottle
    {
        public const int MaxCapacity = 10;
        public const string DefaultShape = "classic";

        private readonly byte[] layers;

        public int Capacity { get; }
        public string Shape { get; }
        public int AcceptColor { get; }
        public int LockColor { get; }
        public int PourLimit { get; }
        public bool CanPourOut { get; }
        // Фигурка из уровня-спасения: принимает только следующий цвет рецепта (снизу вверх), null — обычная бутылка.
        public byte[] Recipe { get; }

        public int Count { get; private set; }
        public int HiddenCount { get; set; }
        public int FrozenTurns { get; set; }
        public bool IsLocked { get; set; }
        public int FlipsLeft { get; set; }

        public Bottle(int capacity, string shape = DefaultShape, int acceptColor = Palette.AnyColor,
            int lockColor = Palette.AnyColor, int pourLimit = 0, bool canPourOut = true, byte[] recipe = null)
        {
            if (recipe != null && recipe.Length != capacity)
                throw new ArgumentException("Recipe must fill the whole bottle", nameof(recipe));

            if (capacity < 1 || capacity > MaxCapacity)
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, $"Capacity must be 1..{MaxCapacity}");

            Capacity = capacity;
            Shape = shape ?? DefaultShape;
            AcceptColor = acceptColor;
            LockColor = lockColor;
            PourLimit = pourLimit;
            CanPourOut = canPourOut;
            Recipe = recipe;
            layers = new byte[capacity];
        }

        public bool IsEmpty => Count == 0;
        public bool IsFull => Count == Capacity;
        public int FreeSpace => Capacity - Count;
        public bool IsFrozen => FrozenTurns > 0;
        public bool IsBlocked => IsLocked || FrozenTurns > 0;
        public int TopColor => Count == 0 ? Palette.AnyColor : layers[Count - 1];

        public byte this[int index]
        {
            get
            {
                if ((uint)index >= (uint)Count)
                    throw new ArgumentOutOfRangeException(nameof(index));
                return layers[index];
            }
        }

        public bool IsLayerHidden(int index) => index < HiddenCount;

        public int TopRunLength
        {
            get
            {
                if (Count == 0)
                    return 0;
                byte top = layers[Count - 1];
                int run = 1;
                while (run < Count && layers[Count - 1 - run] == top)
                    run++;
                return run;
            }
        }

        public bool IsUniform => Count > 0 && TopRunLength == Count;

        // Собранная = ровно unit слоёв одного цвета. Для большой колбы (2U) это тоже ровно U.
        public bool IsCompleted(int unit) => Count == unit && IsUniform;

        // Для победы: обычная бутылка пуста или собрана, фигурка — заполнена по рецепту до верха.
        public bool IsDone(int unit) => Recipe != null ? Count == Capacity : IsEmpty || IsCompleted(unit);

        // Сколько слоёв цвета подряд ещё ждёт рецепт. Для обычной бутылки — без ограничения.
        public int RecipeRoomFor(int color)
        {
            if (Recipe == null)
                return int.MaxValue;
            int room = 0;
            while (Count + room < Recipe.Length && Recipe[Count + room] == color)
                room++;
            return room;
        }

        public bool Accepts(int color)
        {
            if (Recipe != null)
                return RecipeRoomFor(color) > 0;
            return AcceptColor == Palette.AnyColor || AcceptColor == color;
        }

        public void Push(byte color)
        {
            if (Count == Capacity)
                throw new InvalidOperationException("Bottle is full");
            layers[Count++] = color;
        }

        public byte Pop()
        {
            if (Count == 0)
                throw new InvalidOperationException("Bottle is empty");
            return layers[--Count];
        }

        public void Clear() => Count = 0;

        public void Reverse() => Array.Reverse(layers, 0, Count);

        // Скрытым может быть всё, кроме верхнего слоя: он открывается, как только оказывается сверху.
        public int RevealTop()
        {
            int visibleLimit = Math.Max(Count - 1, 0);
            if (HiddenCount <= visibleLimit)
                return 0;
            int revealed = HiddenCount - visibleLimit;
            HiddenCount = visibleLimit;
            return revealed;
        }

        public Bottle Clone()
        {
            var copy = CloneEmpty();
            Array.Copy(layers, copy.layers, Count);
            copy.Count = Count;
            copy.HiddenCount = HiddenCount;
            copy.FrozenTurns = FrozenTurns;
            copy.IsLocked = IsLocked;
            copy.FlipsLeft = FlipsLeft;
            return copy;
        }

        public Bottle CloneEmpty() => new Bottle(Capacity, Shape, AcceptColor, LockColor, PourLimit, CanPourOut, Recipe);

        // Форма только для вида: правила от неё не зависят, поэтому солвер её не различает.
        public bool HasSameRules(Bottle other)
        {
            return Capacity == other.Capacity && AcceptColor == other.AcceptColor && LockColor == other.LockColor
                && PourLimit == other.PourLimit && CanPourOut == other.CanPourOut && SameRecipe(Recipe, other.Recipe);
        }

        private static bool SameRecipe(byte[] a, byte[] b)
        {
            if (a == null || b == null)
                return a == b;
            if (a.Length != b.Length)
                return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i])
                    return false;
            return true;
        }

        public bool StateEquals(Bottle other)
        {
            if (!HasSameRules(other) || Shape != other.Shape || Count != other.Count || HiddenCount != other.HiddenCount
                || FrozenTurns != other.FrozenTurns || IsLocked != other.IsLocked || FlipsLeft != other.FlipsLeft)
                return false;
            for (int i = 0; i < Count; i++)
                if (layers[i] != other.layers[i])
                    return false;
            return true;
        }

        public override string ToString()
        {
            if (Count == 0)
                return "-";
            var text = new StringBuilder(Count);
            for (int i = 0; i < Count; i++)
                text.Append(Palette.ToLetter(layers[i]));
            return text.ToString();
        }
    }
}
