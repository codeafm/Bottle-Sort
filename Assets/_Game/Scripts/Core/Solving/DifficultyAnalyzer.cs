using System;
using System.Collections.Generic;

namespace BottleSort.Core
{
    public sealed class AnalyzerOptions
    {
        public int Runs = 200;
        public ulong Seed = 0xB0771E5047UL;
        // Сколько позиций из прогонов случайного игрока проверить на тупик и с каким бюджетом.
        public int DeadSamples = 40;
        public int DeadCheckStates = 3000;
        public int FallbackStepLimit = 300;

        public static readonly AnalyzerOptions Default = new AnalyzerOptions();
    }

    // Метрики из LEVEL_DESIGN.md §9.2. В базе хранятся все сырые значения, DS — только сводка.
    public sealed class LevelMetrics
    {
        public int Opt;
        public bool OptExact;
        public int LowerBound;
        public int DeadEndsInSearch;
        public double RandomWinRate;
        public double GreedyWinRate;
        public double BlindWinRate;
        // Доля побед жадного игрока, уложившегося в лимит ходов (если лимит есть).
        public double GreedyWinRateWithinLimit;
        public double DeadRatio;
        public double Spread;
        public double Depth;
        public int StartMoves;
        public int GoodStartMoves;
        public double Difficulty;
    }

    public static class DifficultyAnalyzer
    {
        // На старте все бутылки обычно полные, и налить «на свой цвет» некуда. Поэтому хорошим считаем
        // и ход в пустую, который переносит сразу отрезок из двух и больше слоёв (так начинают собирать цвет).
        public static bool IsGoodMove(LevelState state, Move move)
        {
            if (move.Kind != MoveKind.Pour)
                return false;
            var source = state[move.From];
            if (!state[move.To].IsEmpty)
                return true;
            return !source.IsUniform && state.PourAmount(move) >= 2;
        }

        public static int CountGoodMoves(LevelState state, List<Move> legal)
        {
            int good = 0;
            foreach (var move in legal)
                if (IsGoodMove(state, move))
                    good++;
            return good;
        }

        public static LevelMetrics Analyze(LevelState start, SolveResult solve, int moveLimit = 0,
            AnalyzerOptions options = null)
        {
            options = options ?? AnalyzerOptions.Default;
            var metrics = new LevelMetrics
            {
                Opt = solve.Length,
                OptExact = solve.IsOptimal,
                LowerBound = Solver.LowerBound(start),
                DeadEndsInSearch = solve.DeadEnds,
                Spread = Spread(start),
                Depth = Depth(start)
            };

            var legal = new List<Move>();
            start.GetLegalMoves(legal);
            metrics.StartMoves = legal.Count;
            metrics.GoodStartMoves = CountGoodMoves(start, legal);

            int stepLimit = solve.IsSolved ? Math.Max(4 * solve.Length, 20) : options.FallbackStepLimit;
            var rng = new Rng(options.Seed);
            var deadCandidates = new List<LevelState>();

            int randomWins = 0, greedyWins = 0, blindWins = 0, greedyWinsInLimit = 0;
            bool hasHidden = HasHidden(start);
            for (int run = 0; run < options.Runs; run++)
            {
                var collect = deadCandidates.Count < options.DeadSamples ? deadCandidates : null;
                if (Play(start, AgentKind.Random, stepLimit, rng, collect, options.DeadSamples) > 0)
                    randomWins++;

                int greedyMoves = Play(start, AgentKind.Greedy, stepLimit, rng, null, 0);
                if (greedyMoves > 0)
                {
                    greedyWins++;
                    if (moveLimit <= 0 || greedyMoves <= moveLimit)
                        greedyWinsInLimit++;
                }

                if (hasHidden && Play(start, AgentKind.Blind, stepLimit, rng, null, 0) > 0)
                    blindWins++;
            }

            int runs = Math.Max(options.Runs, 1);
            metrics.RandomWinRate = (double)randomWins / runs;
            metrics.GreedyWinRate = (double)greedyWins / runs;
            metrics.GreedyWinRateWithinLimit = (double)greedyWinsInLimit / runs;
            metrics.BlindWinRate = hasHidden ? (double)blindWins / runs : metrics.GreedyWinRate;

            int dead = 0;
            foreach (var state in deadCandidates)
                if (Solver.IsDeadEnd(state, options.DeadCheckStates))
                    dead++;
            metrics.DeadRatio = deadCandidates.Count == 0 ? 0 : (double)dead / deadCandidates.Count;

            metrics.Difficulty = Score(metrics, hasHidden);
            return metrics;
        }

        // trap5 (§9.2): на одной из первых позиций оптимального пути есть ход, после которого уже не выиграть.
        public static bool HasEarlyTrap(LevelState start, IReadOnlyList<Move> solution, int positions = 5,
            int deadCheckStates = 20_000)
        {
            var state = start.Clone();
            var legal = new List<Move>();
            for (int step = 0; step < positions && step < solution.Count; step++)
            {
                state.GetLegalMoves(legal);
                foreach (var move in legal)
                {
                    var next = state.Clone();
                    next.Apply(move);
                    if (!next.IsWon && Solver.IsDeadEnd(next, deadCheckStates))
                        return true;
                }
                state.Apply(solution[step]);
            }
            return false;
        }

        // DS = 100 × (0.35·(1−RW) + 0.20·(1−GW или BW) + 0.15·DEAD + 0.15·SPREAD + 0.15·min(1, OPT/60)).
        public static double Score(LevelMetrics m, bool hasHidden)
        {
            double greedy = hasHidden ? m.BlindWinRate : m.GreedyWinRate;
            return 100 * (0.35 * (1 - m.RandomWinRate) + 0.20 * (1 - greedy) + 0.15 * m.DeadRatio
                + 0.15 * m.Spread + 0.15 * Math.Min(1.0, m.Opt / 60.0));
        }

        // Σ по цветам (бутылок с этим цветом − 1) / (C·(U−1)): 0 — всё уже лежит кучками, 1 — максимально раскидано.
        public static double Spread(LevelState state)
        {
            int colors = state.CountColors();
            if (colors == 0 || state.Unit < 2)
                return 0;
            var bottlesWithColor = new int[Palette.MaxColors];
            foreach (var bottle in state.Bottles)
            {
                int mask = 0;
                for (int i = 0; i < bottle.Count; i++)
                    mask |= 1 << bottle[i];
                for (int c = 0; c < Palette.MaxColors; c++)
                    if ((mask & (1 << c)) != 0)
                        bottlesWithColor[c]++;
            }
            int sum = 0;
            foreach (int count in bottlesWithColor)
                if (count > 0)
                    sum += count - 1;
            return (double)sum / (colors * (state.Unit - 1));
        }

        // Сколько в среднем чужих слоёв лежит над каждым слоем.
        public static double Depth(LevelState state)
        {
            int units = 0, buried = 0;
            foreach (var bottle in state.Bottles)
            {
                for (int i = 0; i < bottle.Count; i++)
                {
                    units++;
                    for (int above = i + 1; above < bottle.Count; above++)
                        if (bottle[above] != bottle[i])
                            buried++;
                }
            }
            return units == 0 ? 0 : (double)buried / units;
        }

        private static bool HasHidden(LevelState state)
        {
            foreach (var bottle in state.Bottles)
                if (bottle.HiddenCount > 0)
                    return true;
            return false;
        }

        private enum AgentKind
        {
            Random,
            Greedy,
            Blind
        }

        // Агент не возвращается в уже виденные позиции. Возвращает число ходов до победы или 0.
        private static int Play(LevelState start, AgentKind agent, int stepLimit, Rng rng,
            List<LevelState> collect, int collectLimit)
        {
            var codec = new StateCodec(start);
            var visited = new HashSet<PositionKey>();
            var state = start.Clone();
            visited.Add(PositionKey.Of(codec, state));

            var legal = new List<Move>();
            var best = new List<(LevelState next, PositionKey key)>();

            for (int step = 1; step <= stepLimit; step++)
            {
                state.GetLegalMoves(legal);
                rng.Shuffle(legal);
                best.Clear();
                int bestScore = int.MinValue;

                foreach (var move in legal)
                {
                    int score = agent == AgentKind.Random ? 0 : ScoreMove(state, move, agent == AgentKind.Blind);
                    if (score < bestScore)
                        continue;

                    var next = state.Clone();
                    next.Apply(move);
                    var key = PositionKey.Of(codec, next);
                    if (visited.Contains(key))
                        continue;

                    if (score > bestScore)
                    {
                        bestScore = score;
                        best.Clear();
                    }
                    best.Add((next, key));
                    // Случайному игроку годится первый же новый ход из перемешанного списка.
                    if (agent == AgentKind.Random)
                        break;
                }

                if (best.Count == 0)
                    return 0;

                var chosen = best[rng.NextInt(best.Count)];
                state = chosen.next;
                visited.Add(chosen.key);
                if (state.IsWon)
                    return step;
                if (collect != null && collect.Count < collectLimit)
                    collect.Add(state.Clone());
            }
            return 0;
        }

        // «Казуальный» игрок: собрать бутылку > налить на свой цвет > налить в пустую > бессмысленный перелив.
        // Слепой видит только открытые слои и не может знать, что под «?».
        private static int ScoreMove(LevelState state, Move move, bool blind)
        {
            if (move.Kind == MoveKind.Flip)
                return 1;

            var source = state[move.From];
            var target = state[move.To];
            int amount = state.PourAmount(move);

            if (!target.IsEmpty)
            {
                bool targetKnown = !blind || target.HiddenCount == 0;
                bool completes = targetKnown && target.Count + amount == state.Unit && target.TopRunLength == target.Count;
                return completes ? 3 : 2;
            }

            bool sourceKnown = !blind || source.HiddenCount == 0;
            if (sourceKnown && source.IsUniform)
                return 0;
            return 1;
        }

        private readonly struct PositionKey : IEquatable<PositionKey>
        {
            private readonly ulong[] packed;
            private readonly int hash;

            private PositionKey(ulong[] packed)
            {
                this.packed = packed;
                ulong h = 14695981039346656037UL;
                foreach (ulong value in packed)
                {
                    h ^= value;
                    h *= 1099511628211UL;
                    h ^= h >> 29;
                }
                hash = (int)(h ^ (h >> 32));
            }

            public static PositionKey Of(StateCodec codec, LevelState state)
            {
                var packed = new ulong[codec.Width];
                codec.EncodeOriginal(state, packed, 0);
                return new PositionKey(packed);
            }

            public bool Equals(PositionKey other)
            {
                if (hash != other.hash)
                    return false;
                for (int i = 0; i < packed.Length; i++)
                    if (packed[i] != other.packed[i])
                        return false;
                return true;
            }

            public override bool Equals(object obj) => obj is PositionKey other && Equals(other);
            public override int GetHashCode() => hash;
        }
    }
}
