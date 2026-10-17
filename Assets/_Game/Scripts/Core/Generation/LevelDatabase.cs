using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace BottleSort.Core
{
    // Готовые уровни 1–2000 одной строкой на уровень. Сборка с отбором по сложности занимает минуты,
    // поэтому на устройстве уровни берутся отсюда, а генератор работает только для 2001+.
    //
    // L12|t=Normal|r=Hard|u=4|p=17|x=1|k=5|m=0|ok=1|b=RGBY BBRG - -;f3|s=0-3,1-3|ds=41.2|rw=0.5|gw=0.6|bw=0.6
    // rt — номер пересборки с другим seed, если уровень повторял соседа (LevelAudit).
    // Бутылка: слои снизу вверх или «-», дальше через «;» её свойства: c — вместимость, если не U; f — лёд;
    // L — замок; k — цвет замка; a — принимает только цвет; p — пипетка; o — нельзя выливать; h — скрытые;
    // x — перевороты; s — форма; r — рецепт фигурки.
    public sealed class LevelDatabase
    {
        public const string Header = "# bottle sort levels";

        private readonly Dictionary<int, string> lines = new Dictionary<int, string>();

        public int GenVersion { get; private set; } = LevelGenerator.CurrentGenVersion;
        // Сколько заняла сборка базы (для отчёта), 0 — неизвестно.
        public double BuildSeconds { get; private set; }
        public int Count => lines.Count;
        public IEnumerable<int> Levels => lines.Keys;

        public bool Contains(int level) => lines.ContainsKey(level);

        public static LevelDatabase Parse(string text)
        {
            var database = new LevelDatabase();
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0)
                    continue;
                if (line[0] == '#')
                {
                    string version = HeaderValue(line, "genVersion=");
                    if (version != null)
                        database.GenVersion = int.Parse(version, CultureInfo.InvariantCulture);
                    string seconds = HeaderValue(line, "buildSeconds=");
                    if (seconds != null)
                        database.BuildSeconds = double.Parse(seconds, CultureInfo.InvariantCulture);
                    continue;
                }
                int bar = line.IndexOf('|');
                database.lines[int.Parse(line.Substring(1, bar - 1), CultureInfo.InvariantCulture)] = line;
            }
            return database;
        }

        private static string HeaderValue(string line, string key)
        {
            int at = line.IndexOf(key, StringComparison.Ordinal);
            return at < 0 ? null : line.Substring(at + key.Length).Split(' ')[0];
        }

        // Строку разбираем только когда уровень нужен: вся база — пара тысяч строк.
        public GeneratedLevel Get(int level) => lines.TryGetValue(level, out var line) ? Decode(line) : null;

        public static string Write(IEnumerable<GeneratedLevel> levels, int genVersion, double buildSeconds = 0)
        {
            var text = new StringBuilder();
            text.Append(Header).Append(" genVersion=").Append(genVersion.ToString(CultureInfo.InvariantCulture));
            if (buildSeconds > 0)
                text.Append(" buildSeconds=").Append(buildSeconds.ToString("0", CultureInfo.InvariantCulture));
            text.Append('\n');
            foreach (var level in levels)
                text.Append(Encode(level)).Append('\n');
            return text.ToString();
        }

        public static string Encode(GeneratedLevel level)
        {
            var s = new StringBuilder();
            s.Append('L').Append(Num(level.Spec.Number));
            s.Append("|t=").Append(level.Spec.Type);
            s.Append("|r=").Append(level.Spec.Tier);
            s.Append("|u=").Append(Num(level.State.Unit));
            s.Append("|p=").Append(Num(level.Par));
            s.Append("|x=").Append(level.ParExact ? '1' : '0');
            s.Append("|k=").Append(Num(level.Candidate));
            s.Append("|m=").Append(Num(level.MoveLimit));
            s.Append("|ok=").Append(level.PassedAllFilters ? '1' : '0');
            if (level.Retry > 0)
                s.Append("|rt=").Append(Num(level.Retry));
            if (!string.IsNullOrEmpty(level.Spec.Introduces))
                s.Append("|i=").Append(level.Spec.Introduces);
            s.Append("|b=").Append(EncodeBottles(level.State));
            s.Append("|s=").Append(EncodeMoves(level.Solution));
            if (level.Metrics != null)
            {
                s.Append("|ds=").Append(Dec(level.Metrics.Difficulty));
                s.Append("|rw=").Append(Dec(level.Metrics.RandomWinRate));
                s.Append("|gw=").Append(Dec(level.Metrics.GreedyWinRate));
                s.Append("|bw=").Append(Dec(level.Metrics.BlindWinRate));
                s.Append("|gl=").Append(Dec(level.Metrics.GreedyWinRateWithinLimit));
                s.Append("|de=").Append(Dec(level.Metrics.DeadRatio));
                s.Append("|sp=").Append(Dec(level.Metrics.Spread));
            }
            return s.ToString();
        }

        public static GeneratedLevel Decode(string line)
        {
            var fields = new Dictionary<string, string>();
            var parts = line.Split('|');
            int number = int.Parse(parts[0].Substring(1), CultureInfo.InvariantCulture);
            for (int i = 1; i < parts.Length; i++)
            {
                int eq = parts[i].IndexOf('=');
                fields[parts[i].Substring(0, eq)] = parts[i].Substring(eq + 1);
            }

            int unit = Int(fields["u"]);
            var state = DecodeBottles(fields["b"], unit);
            var spec = new LevelSpec
            {
                Number = number,
                Type = (LevelType)Enum.Parse(typeof(LevelType), fields["t"]),
                Tier = (LevelTier)Enum.Parse(typeof(LevelTier), fields["r"]),
                Unit = unit,
                Colors = state.CountColors(),
                Introduces = fields.TryGetValue("i", out var intro) ? intro : null
            };
            foreach (var bottle in state.Bottles)
            {
                if (bottle.Recipe != null)
                    spec.RescueRecipeColors = bottle.Capacity / unit;
                else if (bottle.Capacity > unit)
                    spec.BigFlask = true;
            }

            var level = new GeneratedLevel
            {
                Spec = spec,
                State = state,
                Par = Int(fields["p"]),
                ParExact = fields["x"] == "1",
                Candidate = Int(fields["k"]),
                MoveLimit = Int(fields["m"]),
                PassedAllFilters = fields["ok"] == "1",
                Retry = fields.TryGetValue("rt", out var retry) ? Int(retry) : 0,
                Solution = DecodeMoves(fields["s"])
            };
            if (fields.TryGetValue("ds", out var ds))
            {
                level.Metrics = new LevelMetrics
                {
                    Opt = level.Par,
                    OptExact = level.ParExact,
                    Difficulty = Dbl(ds),
                    RandomWinRate = Dbl(fields["rw"]),
                    GreedyWinRate = Dbl(fields["gw"]),
                    BlindWinRate = Dbl(fields["bw"]),
                    GreedyWinRateWithinLimit = Dbl(fields["gl"]),
                    DeadRatio = Dbl(fields["de"]),
                    Spread = Dbl(fields["sp"])
                };
            }
            return level;
        }

        public static string EncodeBottles(LevelState state)
        {
            var s = new StringBuilder();
            for (int b = 0; b < state.BottleCount; b++)
            {
                var bottle = state[b];
                if (b > 0)
                    s.Append(' ');
                s.Append(bottle.ToString());
                if (bottle.Capacity != state.Unit) s.Append(";c").Append(Num(bottle.Capacity));
                if (bottle.FrozenTurns > 0) s.Append(";f").Append(Num(bottle.FrozenTurns));
                if (bottle.IsLocked) s.Append(";L");
                if (bottle.LockColor != Palette.AnyColor) s.Append(";k").Append(Palette.ToLetter(bottle.LockColor));
                if (bottle.AcceptColor != Palette.AnyColor) s.Append(";a").Append(Palette.ToLetter(bottle.AcceptColor));
                if (bottle.PourLimit > 0) s.Append(";p").Append(Num(bottle.PourLimit));
                if (!bottle.CanPourOut) s.Append(";o");
                if (bottle.HiddenCount > 0) s.Append(";h").Append(Num(bottle.HiddenCount));
                if (bottle.FlipsLeft > 0) s.Append(";x").Append(Num(bottle.FlipsLeft));
                if (bottle.Shape != Bottle.DefaultShape) s.Append(";s").Append(bottle.Shape);
                if (bottle.Recipe != null)
                {
                    s.Append(";r");
                    foreach (byte color in bottle.Recipe)
                        s.Append(Palette.ToLetter(color));
                }
            }
            return s.ToString();
        }

        public static LevelState DecodeBottles(string text, int unit)
        {
            var bottles = new List<Bottle>();
            foreach (var token in text.Split(' '))
            {
                var parts = token.Split(';');
                int capacity = unit, frozen = 0, lockColor = Palette.AnyColor, accept = Palette.AnyColor;
                int pourLimit = 0, hidden = 0, flips = 0;
                bool locked = false, canPourOut = true;
                string shape = Bottle.DefaultShape;
                byte[] recipe = null;
                for (int i = 1; i < parts.Length; i++)
                {
                    string value = parts[i].Substring(1);
                    switch (parts[i][0])
                    {
                        case 'c': capacity = Int(value); break;
                        case 'f': frozen = Int(value); break;
                        case 'L': locked = true; break;
                        case 'k': lockColor = Palette.FromLetter(value[0]); break;
                        case 'a': accept = Palette.FromLetter(value[0]); break;
                        case 'p': pourLimit = Int(value); break;
                        case 'o': canPourOut = false; break;
                        case 'h': hidden = Int(value); break;
                        case 'x': flips = Int(value); break;
                        case 's': shape = value; break;
                        case 'r':
                            recipe = new byte[value.Length];
                            for (int c = 0; c < value.Length; c++)
                                recipe[c] = Palette.FromLetter(value[c]);
                            break;
                    }
                }

                var bottle = new Bottle(capacity, shape, accept, lockColor, pourLimit, canPourOut, recipe);
                if (parts[0] != "-")
                    foreach (char letter in parts[0])
                        bottle.Push(Palette.FromLetter(letter));
                bottle.FrozenTurns = frozen;
                bottle.IsLocked = locked;
                bottle.HiddenCount = hidden;
                bottle.FlipsLeft = flips;
                bottles.Add(bottle);
            }
            return new LevelState(unit, bottles);
        }

        private static string EncodeMoves(List<Move> moves)
        {
            var s = new StringBuilder();
            foreach (var move in moves)
            {
                if (s.Length > 0)
                    s.Append(',');
                if (move.Kind == MoveKind.Flip)
                    s.Append('F').Append(Num(move.From));
                else
                    s.Append(Num(move.From)).Append('-').Append(Num(move.To));
            }
            return s.ToString();
        }

        private static List<Move> DecodeMoves(string text)
        {
            var moves = new List<Move>();
            if (text.Length == 0)
                return moves;
            foreach (var token in text.Split(','))
            {
                if (token[0] == 'F')
                {
                    moves.Add(Move.Flip(Int(token.Substring(1))));
                    continue;
                }
                int dash = token.IndexOf('-');
                moves.Add(Move.Pour(Int(token.Substring(0, dash)), Int(token.Substring(dash + 1))));
            }
            return moves;
        }

        private static string Num(int value) => value.ToString(CultureInfo.InvariantCulture);
        private static string Dec(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
        private static int Int(string text) => int.Parse(text, CultureInfo.InvariantCulture);
        private static double Dbl(string text) => double.Parse(text, CultureInfo.InvariantCulture);
    }
}
