using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BottleSort.Core
{
    public sealed class LevelIssue
    {
        public int Level;
        public string Check;
        public string Details;
        // Предупреждение не мешает выпуску, но его стоит посмотреть (пила, скачки сложности).
        public bool Warning;

        public override string ToString() => $"L{Level} [{Check}] {Details}";
    }

    // Сборка базы 1–N и проверки из LEVEL_DESIGN.md §9.5. Работает без Unity: из Editor-окна,
    // из batchmode и из обычного .NET (так быстрее гонять при настройке параметров).
    public static class LevelAudit
    {
        public const int ShortJumpLimit = 25;

        // Уровень, который не удалось собрать, не роняет сборку: он попадает в failures и в отчёт.
        public static List<GeneratedLevel> Build(int from, int to, List<LevelIssue> failures, Action<int> progress = null,
            int threads = 0)
        {
            var levels = new GeneratedLevel[to - from + 1];
            int done = 0;
            var options = new ParallelOptions { MaxDegreeOfParallelism = threads > 0 ? threads : Environment.ProcessorCount };
            Parallel.For(from, to + 1, options, level =>
            {
                try
                {
                    levels[level - from] = BuildOne(level);
                }
                catch (Exception e)
                {
                    lock (failures)
                        failures.Add(new LevelIssue { Level = level, Check = "сборка", Details = e.Message });
                }
                progress?.Invoke(System.Threading.Interlocked.Increment(ref done));
            });

            var built = levels.Where(l => l != null).ToList();
            ReplaceNearDuplicates(built);
            return built;
        }

        public const int MaxRetries = 8;

        // Похожие раскладки у соседей ловятся только после сборки всех уровней. Такой уровень пересобираем
        // с другим seed по порядку номеров, так что результат тоже детерминирован; номер попытки хранится в базе.
        private static void ReplaceNearDuplicates(List<GeneratedLevel> levels)
        {
            for (int i = 0; i < levels.Count; i++)
            {
                var level = levels[i];
                if (level.IsHandmade)
                    continue;
                while (NearDuplicateOf(levels, i, out _, out _) >= 0 && level.Retry < MaxRetries)
                {
                    level = BuildOne(level.Spec.Number, level.Retry + 1);
                    levels[i] = level;
                }
            }
        }

        public static GeneratedLevel BuildOne(int level, int retry = 0)
        {
            if (HandmadeLevels.TryGet(level, out var handmade))
            {
                var solve = Solver.Solve(handmade.State);
                handmade.Spec.Type = LevelType.Normal;
                handmade.Spec.Tier = LevelSchedule.TierOf(level);
                handmade.Metrics = DifficultyAnalyzer.Analyze(handmade.State, solve, 0,
                    new AnalyzerOptions { Runs = 48, DeadSamples = 12, DeadCheckStates = 3000 });
                return handmade;
            }
            var spec = LevelParams.SpecFor(level, forDatabase: true);
            ulong seed = Seeds.ForLevel(spec.GenVersion, level);
            if (retry > 0)
                seed = Seeds.Combine(seed, 1000 + retry);
            var generated = LevelGenerator.Generate(spec, seed);
            generated.Retry = retry;
            return generated;
        }

        public static List<LevelIssue> Validate(IReadOnlyList<GeneratedLevel> levels, int determinismEvery = 25)
        {
            var issues = new List<LevelIssue>();
            var hashes = new Dictionary<string, int>();
            var issueLock = new object();

            Parallel.ForEach(levels, level =>
            {
                var found = new List<LevelIssue>();
                CheckLevel(level, found);
                if (determinismEvery > 0 && (level.Spec.Number % determinismEvery == 0 || level.Spec.Number <= 3))
                    CheckDeterminism(level, found);
                lock (issueLock)
                    issues.AddRange(found);
            });

            foreach (var level in levels)
            {
                string hash = CanonicalHash(level.State);
                if (hashes.TryGetValue(hash, out int twin))
                    issues.Add(Issue(level, "дубликат", $"та же раскладка, что у L{twin}"));
                else
                    hashes[hash] = level.Spec.Number;
            }
            CheckNearDuplicates(levels, issues);
            CheckCurve(levels, issues);

            issues.Sort((a, b) => a.Level != b.Level ? a.Level.CompareTo(b.Level) : string.CompareOrdinal(a.Check, b.Check));
            return issues;
        }

        private static void CheckLevel(GeneratedLevel level, List<LevelIssue> issues)
        {
            int number = level.Spec.Number;
            var state = level.State;

            var replay = state.Clone();
            try
            {
                foreach (var move in level.Solution)
                    replay.Apply(move);
                if (!replay.IsWon)
                    issues.Add(Issue(level, "решаемость", "сохранённое решение не доводит до победы"));
            }
            catch (InvalidOperationException e)
            {
                issues.Add(Issue(level, "решаемость", "решение содержит недопустимый ход: " + e.Message));
            }
            if (level.Solution.Count != level.Par)
                issues.Add(Issue(level, "par", $"длина решения {level.Solution.Count} ≠ par {level.Par}"));
            if (!level.PassedAllFilters)
                issues.Add(Issue(level, "фильтры", "ни один кандидат не прошёл все фильтры генератора, взят запасной"));

            var expectedType = HandmadeLevels.IsTutorial(number) ? LevelType.Normal : LevelSchedule.TypeOf(number);
            if (level.Spec.Type != expectedType)
                issues.Add(Issue(level, "тип", $"{level.Spec.Type}, по расписанию {expectedType}"));

            CheckStructure(level, issues);
            CheckFeatures(level, issues);

            if (number <= 30 && level.Metrics != null)
            {
                double minRw = expectedType == LevelType.Boss ? 0.6 : 0.8;
                if (level.Metrics.RandomWinRate < minRw)
                    issues.Add(Issue(level, "обучение", $"RW {level.Metrics.RandomWinRate:P0} < {minRw:P0}"));
                if (level.Par > 20)
                    issues.Add(Issue(level, "обучение", $"OPT {level.Par} > 20"));
                if (DifficultyAnalyzer.HasEarlyTrap(state, level.Solution))
                    issues.Add(Issue(level, "обучение", "trap5: тупик в первые 5 ходов"));
            }

            if (level.Metrics != null && HasHidden(state) && level.Metrics.BlindWinRate < 0.6 * level.Metrics.RandomWinRate)
                issues.Add(Issue(level, "скрытые", $"BW {level.Metrics.BlindWinRate:P0} < 0.6·RW"));
            if (level.MoveLimit > 0)
            {
                if (level.MoveLimit > StarRating.TwoStarLimit(level.Par))
                    issues.Add(Issue(level, "лимит", $"лимит {level.MoveLimit} больше порога 2★ {StarRating.TwoStarLimit(level.Par)}"));
                if (level.Metrics != null && level.Metrics.GreedyWinRateWithinLimit < 0.4)
                    issues.Add(Issue(level, "лимит", $"жадный игрок укладывается только в {level.Metrics.GreedyWinRateWithinLimit:P0}"));
            }
        }

        private static void CheckStructure(GeneratedLevel level, List<LevelIssue> issues)
        {
            var state = level.State;
            int unit = state.Unit;
            var type = level.Spec.Type;

            if (unit != 4 && unit != 5)
                issues.Add(Issue(level, "вместимость", $"U = {unit}"));
            if (unit == 5 && !FiveLayersAllowed(level.Spec.Number, type, level.Spec.Introduces))
                issues.Add(Issue(level, "вместимость", "5 слоёв там, где по плану только 4"));
            if (state.BottleCount > LevelParams.MaxBottlesAtStart)
                issues.Add(Issue(level, "экран", $"{state.BottleCount} бутылок на старте > {LevelParams.MaxBottlesAtStart}"));
            if (!state.IsWellFormed())
                issues.Add(Issue(level, "цвета", "у какого-то цвета не U слоёв"));

            int flasks = 0, figures = 0, shaped = 0;
            foreach (var bottle in state.Bottles)
            {
                if (bottle.IsCompleted(unit))
                    issues.Add(Issue(level, "старт", "собранная бутылка на старте"));
                if (bottle.Recipe != null)
                {
                    figures++;
                    if (bottle.Capacity % unit != 0 || bottle.CanPourOut)
                        issues.Add(Issue(level, "вместимость", "фигурка спасения не по правилам"));
                }
                else if (bottle.Capacity == unit * 2)
                    flasks++;
                else if (bottle.Capacity != unit)
                    issues.Add(Issue(level, "вместимость", $"бутылка на {bottle.Capacity} при U = {unit}"));
                if (bottle.Shape != Bottle.DefaultShape && bottle.Shape != LevelGenerator.FlaskShape
                    && bottle.Shape != LevelGenerator.HourglassShape && bottle.Recipe == null)
                    shaped++;
            }

            if ((type == LevelType.BigFlask) != (flasks == 1) || flasks > 1)
                issues.Add(Issue(level, "тип", $"колб: {flasks}"));
            if ((type == LevelType.Rescue) != (figures == 1))
                issues.Add(Issue(level, "тип", $"фигурок спасения: {figures}"));
            bool wantsShapes = type == LevelType.Special || (type == LevelType.ChapterBoss && level.Spec.Number >= 30
                && level.Spec.Number != 2000);
            if (wantsShapes && shaped == 0)
                issues.Add(Issue(level, "тип", "особый уровень без особых форм"));
            if (type == LevelType.Normal && shaped > 0)
                issues.Add(Issue(level, "тип", "особые формы в обычном уровне"));
        }

        private static bool FiveLayersAllowed(int level, LevelType type, string introduces)
        {
            if (introduces == LevelParams.FiveLayers)
                return true;
            switch (type)
            {
                case LevelType.Special: return level >= LevelParams.IntroLevel[LevelParams.FiveLayers];
                case LevelType.BigFlask: return level >= 451;
                case LevelType.Boss:
                case LevelType.ChapterBoss: return level >= 500;
                case LevelType.Normal: return LevelParams.FiveLayersInNormalLevels && level >= 501;
                default: return false;
            }
        }

        private static void CheckFeatures(GeneratedLevel level, List<LevelIssue> issues)
        {
            int number = level.Spec.Number;
            var features = FeaturesIn(level);
            foreach (var feature in features)
                if (LevelParams.IntroLevel.TryGetValue(feature, out int intro) && number < intro)
                    issues.Add(Issue(level, "расписание", $"фишка {feature} раньше ввода на L{intro}"));

            foreach (var pair in LevelParams.IntroLevel)
                if (pair.Value == number && pair.Key != LevelParams.Combo && !features.Contains(pair.Key))
                    issues.Add(Issue(level, "расписание", $"на L{number} должна вводиться {pair.Key}"));

            int mechanics = features.Count(f => f != LevelParams.FiveLayers && f != LevelParams.MoveLimit);
            int max = MaxFeatures(level.Spec.Type, number);
            if (mechanics > max)
                issues.Add(Issue(level, "расписание", $"фишек {mechanics} > {max}"));
        }

        // §3.2: сколько фишек (включая «?») может быть в одном уровне.
        private static int MaxFeatures(LevelType type, int level)
        {
            bool boss = type == LevelType.Boss || type == LevelType.ChapterBoss;
            if (level == 2000) return 4;
            if (level <= 100) return 0;
            if (level <= 300) return boss ? 2 : 1;
            return boss ? 3 : 2;
        }

        public static List<string> FeaturesIn(GeneratedLevel level)
        {
            var features = new List<string>();
            var state = level.State;
            if (HasHidden(state)) features.Add(LevelParams.Hidden);
            foreach (var bottle in state.Bottles)
            {
                if (bottle.IsLocked)
                    features.Add(bottle.LockColor == Palette.AnyColor ? LevelParams.Lock : LevelParams.ColoredLock);
                if (bottle.AcceptColor != Palette.AnyColor) features.Add(LevelParams.Tint);
                if (bottle.PourLimit > 0) features.Add(LevelParams.Pipette);
                if (bottle.FlipsLeft > 0) features.Add(LevelParams.Hourglass);
            }
            if (state.Bottles.Any(b => b.FrozenTurns > 0)) features.Add(LevelParams.Ice);
            if (state.Unit == 5) features.Add(LevelParams.FiveLayers);
            if (level.MoveLimit > 0) features.Add(LevelParams.MoveLimit);
            return features.Distinct().ToList();
        }

        private static bool HasHidden(LevelState state) => state.Bottles.Any(b => b.HiddenCount > 0);

        private static void CheckDeterminism(GeneratedLevel level, List<LevelIssue> issues)
        {
            var again = BuildOne(level.Spec.Number, level.Retry);
            if (LevelDatabase.Encode(again) != LevelDatabase.Encode(level))
                issues.Add(Issue(level, "детерминизм", "повторная генерация дала другой уровень"));
        }

        // Дубликаты с точностью до порядка бутылок и перестановки цветов (§9.4.5).
        public static string CanonicalHash(LevelState state)
        {
            var tokens = LevelDatabase.EncodeBottles(state).Split(' ').ToList();
            tokens.Sort(string.CompareOrdinal);
            var rename = new Dictionary<char, char>();
            var text = new StringBuilder();

            char Rename(char color)
            {
                if (!rename.TryGetValue(color, out char renamed))
                    rename[color] = renamed = (char)('a' + rename.Count);
                return renamed;
            }

            foreach (var token in tokens)
            {
                var parts = token.Split(';');
                foreach (char c in parts[0])
                    text.Append(c == '-' ? c : Rename(c));
                for (int i = 1; i < parts.Length; i++)
                {
                    char key = parts[i][0];
                    text.Append(';').Append(key);
                    bool colors = key == 'k' || key == 'a' || key == 'r';
                    foreach (char c in parts[i].Substring(1))
                        text.Append(colors ? Rename(c) : c);
                }
                text.Append(' ');
            }
            return text.ToString();
        }

        // «Почти дубликат»: больше 30 % одинаковых бутылок с одним из 50 предыдущих уровней.
        private static void CheckNearDuplicates(IReadOnlyList<GeneratedLevel> levels, List<LevelIssue> issues)
        {
            for (int i = 0; i < levels.Count; i++)
            {
                int twin = NearDuplicateOf(levels, i, out int shared, out int total);
                if (twin >= 0)
                    issues.Add(Issue(levels[i], "почти дубликат", $"{shared} из {total} бутылок совпадают с L{levels[twin].Spec.Number}"));
            }
        }

        private static int NearDuplicateOf(IReadOnlyList<GeneratedLevel> levels, int index, out int shared, out int total)
        {
            var mine = FilledBottles(levels[index]);
            total = mine.Count;
            shared = 0;
            if (total == 0)
                return -1;
            for (int j = Math.Max(0, index - 50); j < index; j++)
            {
                var other = FilledBottles(levels[j]);
                shared = 0;
                foreach (var bottle in mine)
                    if (other.Remove(bottle))
                        shared++;
                if (shared * 10 > total * 3)
                    return j;
            }
            return -1;
        }

        private static List<string> FilledBottles(GeneratedLevel level) =>
            level.State.Bottles.Where(b => !b.IsEmpty).Select(b => b.ToString()).ToList();

        // §2.3: средние уровни не проседают по скользящему окну 10, скачок между соседними обычными ≤ 25.
        private static void CheckCurve(IReadOnlyList<GeneratedLevel> levels, List<LevelIssue> issues)
        {
            var medium = levels.Where(l => l.Metrics != null && l.Spec.Tier == LevelTier.Medium && l.Spec.Type == LevelType.Normal).ToList();
            for (int i = 10; i < medium.Count; i++)
            {
                double before = medium.Skip(i - 10).Take(10).Average(l => l.Metrics.Difficulty);
                double after = medium.Skip(Math.Max(0, i - 9)).Take(10).Average(l => l.Metrics.Difficulty);
                if (after < before - 8)
                    issues.Add(new LevelIssue { Level = medium[i].Spec.Number, Check = "пила", Warning = true,
                        Details = $"средняя DS M-уровней просела с {before:F0} до {after:F0}" });
            }

            GeneratedLevel previous = null;
            foreach (var level in levels)
            {
                if (level.Metrics == null || level.Spec.Type != LevelType.Normal)
                    continue;
                if (previous != null && level.Spec.Number - previous.Spec.Number <= 2
                    && Math.Abs(level.Metrics.Difficulty - previous.Metrics.Difficulty) > ShortJumpLimit
                    && LevelSchedule.TypeOf(level.Spec.Number - 1) != LevelType.Boss
                    && LevelSchedule.TypeOf(level.Spec.Number - 1) != LevelType.ChapterBoss)
                    issues.Add(new LevelIssue { Level = level.Spec.Number, Check = "скачок", Warning = true,
                        Details = $"DS {previous.Metrics.Difficulty:F0} → {level.Metrics.Difficulty:F0} относительно L{previous.Spec.Number}" });
                previous = level;
            }
        }

        private static LevelIssue Issue(GeneratedLevel level, string check, string details) =>
            new LevelIssue { Level = level.Spec.Number, Check = check, Details = details };

        public static string Csv(IReadOnlyList<GeneratedLevel> levels)
        {
            var text = new StringBuilder("level,type,tier,colors,unit,bottles,par,par_exact,rw,gw,bw,dead,spread,ds,move_limit,features\n");
            foreach (var level in levels)
            {
                var m = level.Metrics;
                text.Append(string.Join(",", new[]
                {
                    Num(level.Spec.Number), level.Spec.Type.ToString(), level.Spec.Tier.ToString(),
                    Num(level.State.CountColors()), Num(level.State.Unit), Num(level.State.BottleCount),
                    Num(level.Par), level.ParExact ? "1" : "0",
                    Dec(m?.RandomWinRate), Dec(m?.GreedyWinRate), Dec(m?.BlindWinRate), Dec(m?.DeadRatio),
                    Dec(m?.Spread), Dec(m?.Difficulty), Num(level.MoveLimit), string.Join(" ", FeaturesIn(level))
                })).Append('\n');
            }
            return text.ToString();
        }

        private static string Num(int value) => value.ToString(CultureInfo.InvariantCulture);
        private static string Dec(double? value) => value?.ToString("0.###", CultureInfo.InvariantCulture) ?? "";
    }
}
