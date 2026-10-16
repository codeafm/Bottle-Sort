using System;
using System.Collections.Generic;

namespace BottleSort.Core
{
    // Упаковывает бутылку в ulong, чтобы солвер хранил сотни тысяч позиций без лишних объектов.
    //   биты 0..39  — слои по 4 бита (снизу вверх)
    //   40..43      — число слоёв
    //   44..47      — оставшиеся ходы льда
    //   48          — замок
    //   49..50      — перевороты
    //   56..63      — номер «настройки» бутылки (вместимость, тонировка, цвет замка и т.п.)
    // Настройка лежит в старших битах, поэтому после сортировки бутылки с одинаковой настройкой
    // стоят подряд, а порядок настроек в отсортированной позиции всегда один и тот же.
    // Скрытые слои не кодируются: на допустимость ходов они не влияют.
    internal sealed class StateCodec
    {
        private const int CountShift = 40;
        private const int FrozenShift = 44;
        private const int LockedShift = 48;
        private const int FlipsShift = 49;
        private const int SetupShift = 56;

        private const int MaxFrozen = 15;
        private const int MaxFlips = 3;
        private const int MaxColorCode = 15;

        private readonly List<Bottle> setups = new List<Bottle>();
        private readonly int[] originalSetupIds;
        private readonly int[] canonicalSetupIds;

        public int Width { get; }
        public int Unit { get; }

        public StateCodec(LevelState root)
        {
            Unit = root.Unit;
            Width = root.BottleCount;
            originalSetupIds = new int[Width];

            for (int i = 0; i < Width; i++)
            {
                var bottle = root[i];
                if (bottle.FrozenTurns > MaxFrozen || bottle.FlipsLeft > MaxFlips)
                    throw new ArgumentException($"Bottle {i} has counters the solver can't pack");
                originalSetupIds[i] = SetupIdOf(bottle);
            }

            canonicalSetupIds = (int[])originalSetupIds.Clone();
            Array.Sort(canonicalSetupIds);
        }

        private int SetupIdOf(Bottle bottle)
        {
            for (int id = 0; id < setups.Count; id++)
                if (setups[id].HasSameRules(bottle))
                    return id;
            if (setups.Count == 256)
                throw new ArgumentException("Too many distinct bottle setups");
            setups.Add(bottle.CloneEmpty());
            return setups.Count - 1;
        }

        // Пустая позиция в каноническом порядке настроек; в неё раскладываются узлы при раскрытии.
        public LevelState CreateCanonicalScratch()
        {
            var bottles = new List<Bottle>(Width);
            foreach (int id in canonicalSetupIds)
                bottles.Add(setups[id].CloneEmpty());
            return new LevelState(Unit, bottles);
        }

        public void EncodeOriginal(LevelState state, ulong[] buffer, int offset) =>
            Encode(state, originalSetupIds, buffer, offset);

        public void EncodeCanonical(LevelState scratch, ulong[] buffer, int offset) =>
            Encode(scratch, canonicalSetupIds, buffer, offset);

        private void Encode(LevelState state, int[] setupIds, ulong[] buffer, int offset)
        {
            for (int i = 0; i < Width; i++)
                buffer[offset + i] = Pack(state[i], setupIds[i]);
            Array.Sort(buffer, offset, Width);
        }

        public void Decode(ulong[] buffer, int offset, LevelState scratch)
        {
            for (int i = 0; i < Width; i++)
            {
                ulong packed = buffer[offset + i];
                var bottle = scratch[i];
                bottle.Clear();
                int count = (int)((packed >> CountShift) & 0xF);
                for (int layer = 0; layer < count; layer++)
                    bottle.Push((byte)((packed >> (layer * 4)) & 0xF));
                bottle.HiddenCount = 0;
                bottle.FrozenTurns = (int)((packed >> FrozenShift) & 0xF);
                bottle.IsLocked = ((packed >> LockedShift) & 1) != 0;
                bottle.FlipsLeft = (int)((packed >> FlipsShift) & 0x3);
            }
        }

        private static ulong Pack(Bottle bottle, int setupId)
        {
            ulong packed = 0;
            for (int layer = 0; layer < bottle.Count; layer++)
            {
                int color = bottle[layer];
                if (color > MaxColorCode)
                    throw new ArgumentException($"Color {color} doesn't fit into 4 bits");
                packed |= (ulong)color << (layer * 4);
            }
            packed |= (ulong)bottle.Count << CountShift;
            packed |= (ulong)Math.Min(bottle.FrozenTurns, MaxFrozen) << FrozenShift;
            if (bottle.IsLocked)
                packed |= 1UL << LockedShift;
            packed |= (ulong)Math.Min(bottle.FlipsLeft, MaxFlips) << FlipsShift;
            packed |= (ulong)setupId << SetupShift;
            return packed;
        }
    }
}
