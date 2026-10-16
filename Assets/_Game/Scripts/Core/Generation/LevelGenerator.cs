using System;
using System.Collections.Generic;

namespace BottleSort.Core
{
    public sealed class GeneratedLevel
    {
        public LevelSpec Spec;
        public ulong Seed;
        public int Candidate;
        public LevelState State;
        public int Par;
        public bool ParExact;
        public List<Move> Solution;
        public int MoveLimit;
        // false — ни один кандидат не прошёл мягкие фильтры, взят лучший из решаемых.
        public bool PassedAllFilters;
        public LevelMetrics Metrics;

        public GameSession CreateSession() => new GameSession(State, Par, MoveLimit, Spec.AllowsExtraBottle);
    }

    // Перемешать слои → нарезать по бутылкам → расставить фишки → отфильтровать → проверить солвером.
    // Всё случайное идёт только из Rng с seed уровня, так что один seed — один и тот же уровень везде.
    public static class LevelGenerator
    {
        // Любое изменение, влияющее на раскладки, — новая версия. Иначе у игроков поменяются уже выпущенные уровни.
        public const int CurrentGenVersion = 1;

        public const string ClassicShape = Bottle.DefaultShape;
        public const string FlaskShape = "cylinder";
        public const string HourglassShape = "hourglass";
        public const int BigFlaskIndex = 1;

        public static GeneratedLevel Generate(LevelSpec spec) =>
            Generate(spec, Seeds.ForLevel(spec.GenVersion, spec.Number));

        public static GeneratedLevel Generate(LevelSpec spec, ulong seed)
        {
            Validate(spec);

            GeneratedLevel best = null;
            GeneratedLevel fallback = null;
            double bestDistance = double.MaxValue;

            for (int k = 0; k < spec.Candidates; k++)
            {
                var rng = new Rng(Seeds.ForCandidate(seed, k));
                var state = BuildCandidate(spec, rng);
                bool passesSoft = PassesLayoutFilters(spec, state);
                if (!passesSoft && fallback != null)
                    continue;

                var level = TrySolve(spec, state, seed, k, ref passesSoft);
                if (level == null)
                    continue;
                level.PassedAllFilters = passesSoft;

                if (!passesSoft)
                {
                    fallback = fallback ?? level;
                    continue;
                }

                if (spec.TargetDifficulty == null)
                    return level;

                double distance = Math.Abs(level.Metrics.Difficulty - spec.TargetDifficulty.Value);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = level;
                }
            }

            var chosen = best ?? fallback;
            if (chosen == null)
                throw new InvalidOperationException(
                    $"No solvable layout for level {spec.Number} in {spec.Candidates} candidates");
            return chosen;
        }

        private static GeneratedLevel TrySolve(LevelSpec spec, LevelState state, ulong seed, int k, ref bool passesSoft)
        {
            var solve = Solver.Solve(state, spec.Solver);
            if (!solve.IsSolved || solve.Length == 0)
                return null;

            if (passesSoft)
                passesSoft = FeaturesMatter(spec, state, solve);

            var level = new GeneratedLevel
            {
                Spec = spec,
                Seed = seed,
                Candidate = k,
                State = state,
                Par = solve.Length,
                ParExact = solve.IsOptimal,
                Solution = solve.Moves,
                MoveLimit = MoveLimitFor(spec, solve.Length)
            };

            if (spec.ComputeMetrics || spec.TargetDifficulty != null || spec.MoveLimitSlack > 0)
            {
                level.Metrics = DifficultyAnalyzer.Analyze(state, solve, level.MoveLimit, spec.Analyzer);
                if (passesSoft)
                    passesSoft = PassesMetricFilters(spec, level.Metrics);
            }
            return level;
        }

        public static int MoveLimitFor(LevelSpec spec, int par)
        {
            if (spec.MoveLimitSlack <= 0)
                return 0;
            return par + Math.Max(8, (int)Math.Ceiling(spec.MoveLimitSlack * par));
        }

        private static void Validate(LevelSpec spec)
        {
            if (spec.Colors < 1 || spec.Colors > Math.Min(spec.PaletteSize, Palette.MaxColors))
                throw new ArgumentException($"Colors must be 1..{spec.PaletteSize}");
            if (spec.Unit < 2 || spec.Unit * (spec.BigFlask ? 2 : 1) > Bottle.MaxCapacity)
                throw new ArgumentException("Unit doesn't fit into a bottle");
            if (spec.Candidates < 1)
                throw new ArgumentException("Need at least one candidate");

            int onFull = spec.Pipettes + (spec.HourglassFlips > 0 ? 1 : 0);
            int onEmpty = 0;
            foreach (var ice in spec.Ice)
                if (ice.Placement == FeaturePlacement.Full) onFull++; else onEmpty++;
            foreach (var lockFeature in spec.Locks)
                if (lockFeature.Placement == FeaturePlacement.Full) onFull++; else onEmpty++;
            foreach (var tint in spec.Tints)
                if (tint.Placement == FeaturePlacement.Full) onFull++; else onEmpty++;

            if (onFull > spec.Colors || onEmpty > spec.EmptyBottles)
                throw new ArgumentException("Not enough bottles for the requested features");
        }

        private sealed class Slot
        {
            public readonly List<byte> Layers = new List<byte>();
            public int Capacity;
            public string Shape = ClassicShape;
            public int AcceptColor = Palette.AnyColor;
            public int LockColor = Palette.AnyColor;
            public int PourLimit;
            public int Frozen;
            public bool Locked;
            public int Flips;
            public int Hidden;
            public bool HasFeature;
            public bool IsFlask;
        }

        private static LevelState BuildCandidate(LevelSpec spec, Rng rng)
        {
            var colors = PickColors(spec, rng);

            var units = new List<byte>(spec.Colors * spec.Unit);
            foreach (byte color in colors)
                for (int i = 0; i < spec.Unit; i++)
                    units.Add(color);
            rng.Shuffle(units);

            var slots = new List<Slot>();
            for (int b = 0; b < spec.Colors; b++)
            {
                var slot = new Slot { Capacity = spec.Unit };
                slot.Layers.AddRange(units.GetRange(b * spec.Unit, spec.Unit));
                slots.Add(slot);
            }
            for (int e = 0; e < spec.EmptyBottles; e++)
                slots.Add(new Slot { Capacity = spec.Unit });
            rng.Shuffle(slots);

            if (spec.ShapePool.Count > 0)
                foreach (var slot in slots)
                    slot.Shape = rng.Pick(spec.ShapePool);

            PlaceFeatures(spec, slots, colors, rng);

            if (spec.BigFlask)
                slots.Insert(Math.Min(BigFlaskIndex, slots.Count),
                    new Slot { Capacity = spec.Unit * 2, Shape = FlaskShape, IsFlask = true });

            var bottles = new List<Bottle>(slots.Count);
            foreach (var slot in slots)
            {
                var bottle = new Bottle(slot.Capacity, slot.Shape, slot.AcceptColor, slot.LockColor, slot.PourLimit);
                foreach (byte layer in slot.Layers)
                    bottle.Push(layer);
                bottle.FrozenTurns = slot.Frozen;
                bottle.IsLocked = slot.Locked;
                bottle.FlipsLeft = slot.Flips;
                bottle.HiddenCount = slot.Hidden;
                bottles.Add(bottle);
            }
            return new LevelState(spec.Unit, bottles);
        }

        // При C ≤ 6 цвета берутся из seed, но без похожих пар (K/R, L/G, C/B, D/B).
        private static List<byte> PickColors(LevelSpec spec, Rng rng)
        {
            var pool = new List<byte>();
            for (int c = 0; c < spec.PaletteSize; c++)
                pool.Add((byte)c);

            if (spec.Colors == pool.Count)
                return pool;

            List<byte> picked = null;
            for (int attempt = 0; attempt < 50; attempt++)
            {
                rng.Shuffle(pool);
                picked = pool.GetRange(0, spec.Colors);
                if (spec.Colors > 6 || !HasLookalikes(picked))
                    break;
            }
            picked.Sort();
            return picked;
        }

        private static bool HasLookalikes(List<byte> colors)
        {
            for (int i = 0; i < colors.Count; i++)
                for (int j = i + 1; j < colors.Count; j++)
                    if (Palette.AreLookalikes(colors[i], colors[j]))
                        return true;
            return false;
        }

        private static void PlaceFeatures(LevelSpec spec, List<Slot> slots, List<byte> colors, Rng rng)
        {
            foreach (var ice in spec.Ice)
                TakeSlot(slots, ice.Placement, rng).Frozen = ice.Turns;

            foreach (var lockFeature in spec.Locks)
            {
                var slot = TakeSlot(slots, lockFeature.Placement, rng);
                slot.Locked = true;
                if (lockFeature.Colored)
                    slot.LockColor = rng.Pick(colors);
            }

            foreach (var tint in spec.Tints)
            {
                var slot = TakeSlot(slots, tint.Placement, rng);
                slot.AcceptColor = tint.Placement == FeaturePlacement.Full ? slot.Layers[0] : rng.Pick(colors);
            }

            for (int i = 0; i < spec.Pipettes; i++)
                TakeSlot(slots, FeaturePlacement.Full, rng).PourLimit = 1;

            if (spec.HourglassFlips > 0)
            {
                var slot = TakeSlot(slots, FeaturePlacement.Full, rng);
                slot.Shape = HourglassShape;
                slot.Flips = spec.HourglassFlips;
            }

            if (spec.HiddenLayers > 0 && spec.HiddenBottles != 0)
            {
                var full = slots.FindAll(s => s.Layers.Count > 0);
                rng.Shuffle(full);
                int count = spec.HiddenBottles < 0 ? full.Count : Math.Min(spec.HiddenBottles, full.Count);
                for (int i = 0; i < count; i++)
                    full[i].Hidden = Math.Min(spec.HiddenLayers, full[i].Layers.Count - 1);
            }
        }

        private static Slot TakeSlot(List<Slot> slots, FeaturePlacement placement, Rng rng)
        {
            var free = slots.FindAll(s => !s.HasFeature && (placement == FeaturePlacement.Full) == (s.Layers.Count > 0));
            var slot = rng.Pick(free);
            slot.HasFeature = true;
            return slot;
        }

        // LEVEL_DESIGN.md §9.4.3: без собранных бутылок, не больше одной почти собранной,
        // хотя бы один «хороший» стартовый ход, но не больше 40 % от всех допустимых.
        public static bool PassesLayoutFilters(LevelSpec spec, LevelState state)
        {
            int nearlyDone = 0;
            foreach (var bottle in state.Bottles)
            {
                if (bottle.IsCompleted(state.Unit))
                    return false;
                if (LongestRun(bottle) >= state.Unit - 1)
                    nearlyDone++;
            }
            if (nearlyDone > 1)
                return false;

            var legal = new List<Move>();
            state.GetLegalMoves(legal);
            int good = DifficultyAnalyzer.CountGoodMoves(state, legal);
            return good >= 1 && good * 10 <= legal.Count * 4;
        }

        private static int LongestRun(Bottle bottle)
        {
            int longest = 0, run = 0;
            for (int i = 0; i < bottle.Count; i++)
            {
                run = i > 0 && bottle[i] == bottle[i - 1] ? run + 1 : 1;
                longest = Math.Max(longest, run);
            }
            return longest;
        }

        // Фишка должна работать, а не стоять для красоты: в тонированную льют, замок мешает,
        // переворот сокращает решение хотя бы на 2 хода.
        private static bool FeaturesMatter(LevelSpec spec, LevelState state, SolveResult solve)
        {
            for (int i = 0; i < state.BottleCount; i++)
            {
                var bottle = state[i];
                if (bottle.AcceptColor != Palette.AnyColor && !SolutionTouches(solve.Moves, i, asTarget: true))
                    return false;
                if (bottle.IsLocked && !SolutionTouches(solve.Moves, i, asTarget: false))
                    return false;
            }

            if (spec.HourglassFlips > 0)
            {
                var withoutFlips = state.Clone();
                foreach (var bottle in withoutFlips.Bottles)
                    bottle.FlipsLeft = 0;
                var plain = Solver.Solve(withoutFlips, spec.Solver);
                if (plain.IsSolved && plain.Length - solve.Length < 2)
                    return false;
            }
            return true;
        }

        private static bool SolutionTouches(List<Move> moves, int bottle, bool asTarget)
        {
            foreach (var move in moves)
                if (move.To == bottle || (!asTarget && move.From == bottle))
                    return true;
            return false;
        }

        private static bool PassesMetricFilters(LevelSpec spec, LevelMetrics metrics)
        {
            if (spec.HasHidden && metrics.BlindWinRate < 0.6 * metrics.RandomWinRate)
                return false;
            if (spec.MoveLimitSlack > 0 && metrics.GreedyWinRateWithinLimit < 0.4)
                return false;
            return true;
        }
    }
}
