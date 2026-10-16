using System;
using System.Collections.Generic;

namespace BottleSort.Core
{
    public enum SolveStatus
    {
        Solved,
        Unsolvable,
        // Бюджет кончился раньше, чем нашли решение или доказали, что его нет.
        Unknown
    }

    public sealed class SolverOptions
    {
        // Сколько позиций можно запомнить в точном A*. Узел — около 150 байт.
        public int MaxStates = 300_000;

        // Если точный поиск не уложился, пробуем взвешенный A*: решение найдётся быстрее, но не обязательно кратчайшее.
        public bool AllowWeighted = true;
        public int WeightedMaxStates = 300_000;
        public double Weight = 2.5;

        public static readonly SolverOptions Default = new SolverOptions();

        // На устройстве подсказка и проверка тупика должны укладываться в десятки миллисекунд.
        public static SolverOptions Quick(int maxStates = 20_000) =>
            new SolverOptions { MaxStates = maxStates, WeightedMaxStates = maxStates };
    }

    public sealed class SolveResult
    {
        public SolveStatus Status;
        public bool IsOptimal;
        public List<Move> Moves = new List<Move>();
        public int StatesVisited;
        public int DeadEnds;
        public int LowerBound;

        public bool IsSolved => Status == SolveStatus.Solved;
        public int Length => Moves.Count;
    }

    // A* по графу позиций. Эвристика: число одноцветных отрезков минус число цветов.
    // За ход отрезков становится меньше максимум на один, так что эвристика не переоценивает
    // и точный режим находит кратчайшее решение.
    public static class Solver
    {
        public static SolveResult Solve(LevelState state, SolverOptions options = null)
        {
            options = options ?? SolverOptions.Default;
            var result = new Search(state, 1.0, options.MaxStates).Run();
            if (result.Status == SolveStatus.Unknown && options.AllowWeighted)
            {
                var weighted = new Search(state, options.Weight, options.WeightedMaxStates).Run();
                weighted.StatesVisited += result.StatesVisited;
                result = weighted;
            }
            return result;
        }

        public static Move? Hint(LevelState state, SolverOptions options = null)
        {
            var result = Solve(state, options ?? SolverOptions.Quick());
            return result.IsSolved && result.Length > 0 ? result.Moves[0] : (Move?)null;
        }

        // true, только если доказано, что выиграть уже нельзя. Не хватило бюджета — не тупик.
        public static bool IsDeadEnd(LevelState state, int maxStates = 20_000)
        {
            var options = new SolverOptions { MaxStates = maxStates, AllowWeighted = false };
            return Solve(state, options).Status == SolveStatus.Unsolvable;
        }

        public static int LowerBound(LevelState state) => CountSegments(state) - state.CountColors();

        internal static int CountSegments(LevelState state)
        {
            int segments = 0;
            for (int b = 0; b < state.BottleCount; b++)
            {
                var bottle = state[b];
                for (int i = 0; i < bottle.Count; i++)
                    if (i == 0 || bottle[i] != bottle[i - 1])
                        segments++;
            }
            return segments;
        }

        private sealed class Search
        {
            private readonly LevelState root;
            private readonly StateCodec codec;
            private readonly NodeStore nodes;
            private readonly LevelState scratch;
            private readonly int colorCount;
            private readonly int weightTenths;
            private readonly bool exact;
            private readonly int maxStates;
            private readonly List<Move> moves = new List<Move>();

            private readonly List<List<int>> buckets = new List<List<int>>();
            private readonly List<bool> expanded = new List<bool>();
            private int cursor;

            public Search(LevelState root, double weight, int maxStates)
            {
                this.root = root;
                this.maxStates = maxStates;
                exact = weight <= 1.0;
                weightTenths = Math.Max(10, (int)Math.Round(weight * 10));
                codec = new StateCodec(root);
                nodes = new NodeStore(codec.Width);
                scratch = codec.CreateCanonicalScratch();
                colorCount = root.CountColors();
            }

            public SolveResult Run()
            {
                var result = new SolveResult { LowerBound = Heuristic(root) };
                if (root.IsWon)
                {
                    result.Status = SolveStatus.Solved;
                    result.IsOptimal = true;
                    return result;
                }

                codec.EncodeOriginal(root, nodes.Buffer, nodes.DraftOffset);
                int start = nodes.CommitDraft(-1, 0);
                expanded.Add(false);
                Push(start, Priority(0, result.LowerBound));

                int goal = -1;
                int goalDepth = int.MaxValue;
                bool budgetHit = false;

                while (TryPop(out int node, out int priority))
                {
                    // Найденное решение длины G уже не улучшить, если у всех оставшихся f >= G.
                    if (goal >= 0 && (!exact || priority >= goalDepth * 10))
                        break;
                    if (expanded[node])
                        continue;
                    expanded[node] = true;

                    codec.Decode(nodes.Buffer, nodes.OffsetOf(node), scratch);
                    CollectMoves(scratch, moves);
                    if (moves.Count == 0)
                    {
                        result.DeadEnds++;
                        continue;
                    }

                    int depth = nodes.DepthOf(node) + 1;
                    foreach (var move in moves)
                    {
                        codec.Decode(nodes.Buffer, nodes.OffsetOf(node), scratch);
                        scratch.Apply(move);
                        codec.EncodeCanonical(scratch, nodes.Buffer, nodes.DraftOffset);

                        int known = nodes.FindDraft();
                        if (known >= 0)
                        {
                            if (nodes.DepthOf(known) > depth && !expanded[known])
                            {
                                nodes.SetDepth(known, depth, node);
                                if (!scratch.IsWon)
                                    Push(known, Priority(depth, Heuristic(scratch)));
                                else if (depth < goalDepth)
                                {
                                    goal = known;
                                    goalDepth = depth;
                                }
                            }
                            continue;
                        }

                        int child = nodes.CommitDraft(node, depth);
                        expanded.Add(false);

                        if (scratch.IsWon)
                        {
                            if (depth < goalDepth)
                            {
                                goal = child;
                                goalDepth = depth;
                            }
                            continue;
                        }
                        Push(child, Priority(depth, Heuristic(scratch)));
                    }

                    if (nodes.Count >= maxStates)
                    {
                        budgetHit = true;
                        break;
                    }
                }

                result.StatesVisited = nodes.Count;
                if (goal >= 0)
                {
                    result.Status = SolveStatus.Solved;
                    result.IsOptimal = exact && !budgetHit;
                    RebuildPath(goal, result.Moves);
                }
                else
                {
                    result.Status = budgetHit ? SolveStatus.Unknown : SolveStatus.Unsolvable;
                }
                return result;
            }

            private int Heuristic(LevelState state) => CountSegments(state) - colorCount;

            private int Priority(int depth, int heuristic) => depth * 10 + heuristic * weightTenths;

            // Однородную бутылку в пустую такой же бутылки переливать бессмысленно: позиция та же.
            // Но пока на поле есть лёд, такой ход сдвигает счётчики и может понадобиться, чтобы переждать.
            private void CollectMoves(LevelState state, List<Move> result)
            {
                state.GetLegalMoves(result);

                for (int i = 0; i < state.BottleCount; i++)
                    if (state[i].IsFrozen)
                        return;

                int kept = 0;
                for (int i = 0; i < result.Count; i++)
                {
                    var move = result[i];
                    if (!IsSamePositionMove(state, move))
                        result[kept++] = move;
                }
                result.RemoveRange(kept, result.Count - kept);
            }

            private static bool IsSamePositionMove(LevelState state, Move move)
            {
                if (move.Kind != MoveKind.Pour)
                    return false;
                var source = state[move.From];
                var target = state[move.To];
                return target.IsEmpty && source.IsUniform && source.HasSameRules(target)
                    && source.FlipsLeft == target.FlipsLeft;
            }

            // Узлы хранятся в каноническом порядке бутылок, а ходы нужны в исходных индексах.
            // Поэтому проигрываем путь от корня и на каждом шаге ищем ход, дающий следующий узел.
            private void RebuildPath(int goal, List<Move> path)
            {
                var chain = new List<int>();
                for (int node = goal; node >= 0; node = nodes.ParentOf(node))
                    chain.Add(node);
                chain.Reverse();

                var current = root.Clone();
                var candidates = new List<Move>();
                var probe = new ulong[codec.Width];

                for (int step = 1; step < chain.Count; step++)
                {
                    int targetOffset = nodes.OffsetOf(chain[step]);
                    current.GetLegalMoves(candidates);
                    bool found = false;
                    foreach (var move in candidates)
                    {
                        var next = current.Clone();
                        next.Apply(move);
                        codec.EncodeOriginal(next, probe, 0);
                        if (SameSlice(probe, nodes.Buffer, targetOffset))
                        {
                            path.Add(move);
                            current = next;
                            found = true;
                            break;
                        }
                    }
                    if (!found)
                        throw new InvalidOperationException("Solver path could not be replayed");
                }
            }

            private static bool SameSlice(ulong[] probe, ulong[] buffer, int offset)
            {
                for (int i = 0; i < probe.Length; i++)
                    if (probe[i] != buffer[offset + i])
                        return false;
                return true;
            }

            private void Push(int node, int priority)
            {
                while (buckets.Count <= priority)
                    buckets.Add(new List<int>());
                buckets[priority].Add(node);
                if (priority < cursor)
                    cursor = priority;
            }

            // Внутри одной корзины берём последний добавленный узел — это ближе к поиску в глубину
            // и быстрее приводит к решению при равных f.
            private bool TryPop(out int node, out int priority)
            {
                for (; cursor < buckets.Count; cursor++)
                {
                    var bucket = buckets[cursor];
                    if (bucket.Count == 0)
                        continue;
                    node = bucket[bucket.Count - 1];
                    bucket.RemoveAt(bucket.Count - 1);
                    priority = cursor;
                    return true;
                }
                node = -1;
                priority = 0;
                return false;
            }
        }
    }
}
