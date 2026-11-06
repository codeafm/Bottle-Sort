using System;
using System.Collections.Generic;

namespace BottleSort.Core
{
    // Params(L) из LEVEL_DESIGN.md: номер уровня → описание для генератора.
    // Всё случайное здесь (какая фишка, 4 или 5 слоёв) берётся из своего seed по номеру, а не из seed раскладки,
    // чтобы смена генератора не меняла состав фишек уровня.
    public static class LevelParams
    {
        // Обычные уровни с 5 слоями с L501 — предложение level-designer, ждёт решения заказчика (§3.1, §10 п.10).
        // Пока выключено: 5 слоёв только в особых, колбах и боссах, как в запасном варианте документа.
        public const bool FiveLayersInNormalLevels = false;

        public const int MaxBottlesAtStart = 14;
        private static readonly SolverOptions DeviceSolver = new SolverOptions { MaxStates = 40_000, WeightedMaxStates = 150_000, Weight = 1.5 };
        public const int DatabaseCandidates = 16;

        public const string Hidden = "hidden";
        public const string Lock = "lock";
        public const string Ice = "ice";
        public const string Tint = "tint";
        public const string FiveLayers = "five_layers";
        public const string ColoredLock = "colored_lock";
        public const string MoveLimit = "move_limit";
        public const string Hourglass = "hourglass";
        public const string Pipette = "pipette";
        public const string Combo = "combo";

        // Разновидности «?», у каждой свой первый уровень (§5.1).
        public const string HiddenTwo = "hidden_two";
        public const string HiddenFog = "hidden_fog";
        public const string HiddenStairs = "hidden_stairs";
        public static readonly IReadOnlyDictionary<string, int> HiddenStyleDebut = new Dictionary<string, int>
        {
            [HiddenTwo] = 10, [HiddenFog] = 21, [HiddenStairs] = 44
        };

        // С какого уровня 5 слоёв бывают в колбах и у боссов (в особых — с ввода фишки).
        public const int FiveLayerFlasksFrom = 251;
        public const int FiveLayerBossesFrom = 301;
        // «Ловушка» у боссов x50 (замок или лёд на пустой) — не раньше этого уровня.
        public const int TrapsFrom = 150;
        public const int TwoColorRecipesFrom = 108;
        public const int FlaskFeaturesFrom = 101;

        // Уровень, на котором фишку вводят (§5). До него её нигде нет.
        public static readonly IReadOnlyDictionary<string, int> IntroLevel = new Dictionary<string, int>
        {
            [Hidden] = 5, [Ice] = 13, [Lock] = 18, [Tint] = 33, [Combo] = 41, [ColoredLock] = 61,
            [Pipette] = 81, [Hourglass] = 101, [FiveLayers] = 201, [MoveLimit] = 301
        };

        private sealed class Range
        {
            public int From;
            public int Easy, Medium, Hard;
            public int Easy5, Medium5, Hard5;
            public double FiveShare;
            public int HiddenMin, HiddenMax;
            public double HiddenShare;
        }

        // §3.1. До 501 своей колонки U5 нет: 5 слоёв только в особых, колбах и у боссов, берём на цвет меньше.
        private static readonly Range[] Ranges =
        {
            R(4, 5, 6, 6, hidden: (1, 1, 0.50)), R(11, 5, 6, 6, hidden: (1, 2, 0.50)),
            R(21, 6, 6, 7, hidden: (1, 2, 0.50)), R(31, 6, 7, 7, hidden: (1, 2, 0.45)),
            R(51, 6, 7, 8, hidden: (1, 2, 0.45)), R(76, 7, 7, 8, hidden: (1, 2, 0.45)),
            R(101, 7, 7, 8, hidden: (1, 2, 0.45)), R(151, 7, 8, 8, hidden: (1, 2, 0.45)),
            R(201, 7, 8, 9, hidden: (1, 2, 0.45)), R(251, 8, 8, 9, hidden: (2, 2, 0.45)),
            R(301, 8, 8, 9, hidden: (2, 2, 0.45)), R(351, 8, 9, 9, hidden: (2, 2, 0.45)),
            R(451, 8, 9, 10, hidden: (2, 2, 0.45)),
            R(501, 8, 9, 10, (6, 7, 8), 0.25, (2, 2, 0.45)), R(701, 9, 9, 10, (7, 7, 8), 0.30, (2, 2, 0.45)),
            R(1001, 9, 10, 10, (7, 8, 8), 0.35, (2, 3, 0.50)), R(1101, 9, 10, 11, (7, 8, 9), 0.40, (2, 3, 0.50)),
            R(1501, 10, 10, 11, (8, 8, 9), 0.40, (2, 3, 0.50)), R(1801, 10, 11, 12, (8, 9, 10), 0.45, (3, 3, 0.50))
        };

        private static Range R(int from, int easy, int medium, int hard, (int e, int m, int h)? five = null,
            double fiveShare = 0, (int min, int max, double share)? hidden = null)
        {
            var f = five ?? (easy, medium, hard);
            var h = hidden ?? (0, 0, 0.0);
            return new Range
            {
                From = from, Easy = easy, Medium = medium, Hard = hard,
                Easy5 = f.e, Medium5 = f.m, Hard5 = f.h, FiveShare = fiveShare,
                HiddenMin = h.min, HiddenMax = h.max, HiddenShare = h.share
            };
        }

        private static readonly (string feature, double weight)[] MechanicWeights =
        {
            (Lock, 0.25), (Ice, 0.30), (Tint, 0.25), (ColoredLock, 0.25), (Hourglass, 0.20), (Pipette, 0.20)
        };

        // BOTTLE_MODELS §4: какие формы бывают в особых уровнях.
        private static readonly (int from, string shape, bool fiveLayers)[] SpecialShapes =
        {
            (30, "wine", false), (30, "glass", false), (30, "flask", false), (60, "jug", false), (80, "jar", false),
            (110, "teapot", false), (140, "potion", false), (210, "heart", false), (360, "tube", false),
            (201, "wine", true), (201, "glass", true), (201, "flask", true), (201, "jug", true), (201, "jar", true),
            (360, "tube", true), (410, "amphora", false), (410, "amphora", true), (410, "heart", true),
            (610, "crystal", false), (610, "crystal", true), (610, "teapot", true), (910, "lamp", false), (910, "lamp", true)
        };

        // Позиция на пиле → какое место по DS среди кандидатов брать (§2.3: E 10–35, M 35–65, H 65–90, B 88–98).
        public static double PercentileOf(LevelTier tier)
        {
            switch (tier)
            {
                // Не середины диапазонов, а ближе к M: так пила остаётся, а скачки между соседними уровнями меньше.
                case LevelTier.Easy: return 0.28;
                case LevelTier.Hard: return 0.72;
                case LevelTier.Boss: return 0.93;
                default: return 0.50;
            }
        }

        // forDatabase: отбор по сложности среди кандидатов (долго, для сборки базы).
        // Иначе — первый подходящий кандидат, как на устройстве для уровней после 2000.
        public static LevelSpec SpecFor(int level, bool forDatabase = false)
        {
            if (level < 1)
                throw new ArgumentOutOfRangeException(nameof(level));

            var rng = new Rng(Seeds.Combine(Seeds.FromText("params"), level));
            var range = RangeOf(level);
            var spec = new LevelSpec
            {
                Number = level,
                Type = LevelSchedule.TypeOf(level),
                Tier = LevelSchedule.TierOf(level)
            };
            // Спасение — уровень-награда: средний, где бы он ни выпал на пиле.
            if (spec.Type == LevelType.Rescue)
                spec.Tier = LevelTier.Medium;

            if (!ApplyIntro(spec, level, range))
            {
                switch (spec.Type)
                {
                    case LevelType.Special: BuildSpecial(spec, range, rng); break;
                    case LevelType.BigFlask: BuildFlask(spec, range, rng); break;
                    case LevelType.Rescue: BuildRescue(spec, range, rng); break;
                    case LevelType.Boss: BuildBoss(spec, range, rng); break;
                    case LevelType.ChapterBoss: BuildChapterBoss(spec, range, rng); break;
                    default: BuildNormal(spec, range, rng); break;
                }
            }

            spec.Colors = Math.Min(spec.Colors, Palette.MaxColors);
            FitOnScreen(spec);
            spec.PaletteSize = PaletteFor(spec);
            ApplyEarlyLimits(spec);

            if (forDatabase)
            {
                // Больше кандидатов там, где фильтры строже (часы, лимит, босс главы), и в начале игры,
                // где пила должна быть ровной.
                bool picky = spec.HasMoveLimit || spec.Type == LevelType.ChapterBoss || level <= 100;
                spec.Candidates = spec.HourglassFlips > 0 ? 64 : picky ? 32 : DatabaseCandidates;
                spec.TargetPercentile = level == 2000 ? 0.40 : PercentileOf(spec.Tier);
                spec.Analyzer = new AnalyzerOptions { Runs = 48, DeadSamples = 12, DeadCheckStates = 3000 };
            }
            else
            {
                // На устройстве метрики нужны только фильтрам (лимит ходов): хватит коротких прогонов без проверки тупиков.
                spec.Analyzer = new AnalyzerOptions { Runs = 40, DeadSamples = 0 };
                // Спасения и колбы точный A* иногда считает секундами. После 2000 par нужен только для звёзд,
                // так что раньше переходим на взвешенный поиск: решение то же по смыслу, может быть на пару ходов длиннее.
                spec.Solver = DeviceSolver;
            }
            return spec;
        }

        private static Range RangeOf(int level)
        {
            var found = Ranges[0];
            foreach (var range in Ranges)
                if (level >= range.From)
                    found = range;
            return found;
        }

        private static int ColorsFor(Range range, LevelTier tier, bool fiveLayers)
        {
            switch (tier)
            {
                case LevelTier.Easy: return fiveLayers ? range.Easy5 : range.Easy;
                case LevelTier.Medium: return fiveLayers ? range.Medium5 : range.Medium;
                default: return fiveLayers ? range.Hard5 : range.Hard;
            }
        }

        // §5: первый уровень главы вводит фишку в мягкой форме, два следующих её закрепляют.
        private static bool ApplyIntro(LevelSpec spec, int level, Range range)
        {
            string feature = null;
            int step = -1;
            foreach (var pair in IntroLevel)
                if (level >= pair.Value && level <= pair.Value + 2)
                {
                    feature = pair.Key;
                    step = level - pair.Value;
                }
            if (spec.Type != LevelType.Normal)
                return false;
            if (feature == null)
                return ApplyHiddenDebut(spec, level, range);
            // 5 слоёв в обычных уровнях закреплять нельзя, пока заказчик их не разрешил; лимит ходов закрепляют боссы.
            if (step > 0 && (feature == FiveLayers || feature == MoveLimit || feature == Combo))
                return false;

            // Уровень ввода на цвет проще лёгкого уровня диапазона (кроме самого начала), закрепление — как средний.
            spec.Colors = step == 0 ? Math.Max(4, range.Easy - (level > 30 ? 1 : 0)) : range.Medium;
            if (step == 0)
                spec.Introduces = feature;

            switch (feature)
            {
                case Hidden:
                    spec.HiddenLayers = 1;
                    spec.HiddenBottles = step == 0 ? 3 : spec.Colors / 2 + 1;
                    break;
                case Lock:
                    if (step == 0)
                    {
                        spec.EmptyBottles = 3;
                        spec.Locks.Add(new LockFeature { Placement = FeaturePlacement.Empty });
                    }
                    else
                        spec.Locks.Add(new LockFeature { Placement = FeaturePlacement.Full });
                    break;
                case Ice:
                    spec.Ice.Add(new IceFeature { Placement = FeaturePlacement.Full, Turns = 3 });
                    break;
                case Tint:
                    // Ввод — тонированная третья пустая, закрепление — одна из двух.
                    spec.EmptyBottles = step == 0 ? 3 : 2;
                    spec.Tints.Add(new TintFeature { Placement = FeaturePlacement.Empty });
                    break;
                case Combo:
                    spec.Colors = range.Easy;
                    spec.HiddenLayers = 1;
                    spec.HiddenBottles = -1;
                    spec.Ice.Add(new IceFeature { Placement = FeaturePlacement.Full, Turns = 3 });
                    break;
                case FiveLayers:
                    spec.Unit = 5;
                    spec.Colors = 5;
                    break;
                case ColoredLock:
                    spec.Locks.Add(new LockFeature { Placement = FeaturePlacement.Full, Colored = true });
                    break;
                case MoveLimit:
                    spec.Colors = range.Medium;
                    spec.MoveLimitExtra = 15;
                    break;
                case Hourglass:
                    UseHourglass(spec);
                    break;
                case Pipette:
                    spec.Pipettes = 1;
                    break;
            }
            return true;
        }

        // Первые уровни разновидностей «?»: две скрытых, «туман» (виден только верх), «лесенка».
        private static bool ApplyHiddenDebut(LevelSpec spec, int level, Range range)
        {
            string style = null;
            foreach (var pair in HiddenStyleDebut)
                if (pair.Value == level)
                    style = pair.Key;
            if (style == null)
                return false;

            spec.Introduces = style;
            spec.Colors = range.Easy;
            switch (style)
            {
                case HiddenTwo:
                    spec.HiddenLayers = 2;
                    spec.HiddenBottles = spec.Colors / 2;
                    break;
                case HiddenFog:
                    spec.HiddenLayers = spec.Unit - 1;
                    break;
                default:
                    spec.HiddenLayers = spec.Unit - 1;
                    spec.HiddenVaried = true;
                    break;
            }
            return true;
        }

        private static void BuildNormal(LevelSpec spec, Range range, Rng rng)
        {
            int level = spec.Number;
            bool five = FiveLayersInNormalLevels && range.FiveShare > 0 && rng.Chance(range.FiveShare);
            spec.Unit = five ? 5 : 4;
            spec.Colors = ColorsFor(range, spec.Tier, five);

            double clean = CleanShare(level);
            if (level < IntroLevel[Hidden] || rng.Chance(clean))
                return;

            int count = MaxNormalFeatures(level) >= 2 && rng.Chance(ComboShare(level)) ? 2 : 1;
            // «?» бросаем отдельно, чтобы его доля среди всех уровней диапазона была как в §3.1.
            double hiddenChance = Math.Min(0.9, range.HiddenShare / (1 - clean));
            var features = new List<string>();
            if (rng.Chance(hiddenChance))
                features.Add(Hidden);
            // Часы держатся на единственной пустой бутылке и с «?» почти не решаются — только поодиночке.
            features.AddRange(PickFeatures(level, range, count - features.Count, rng, includeHidden: false,
                allowHourglass: features.Count == 0));
            if (features.Count == 0)
                features.Add(Hidden);
            foreach (var feature in features)
                AddFeature(spec, feature, range, rng);
        }

        private static void BuildSpecial(LevelSpec spec, Range range, Rng rng)
        {
            bool five = spec.Number >= IntroLevel[FiveLayers] && rng.Chance(0.5);
            spec.Unit = five ? 5 : 4;
            spec.Colors = five ? FiveLayerColors(range, LevelTier.Medium) : range.Medium;
            spec.ShapePool = ShapesFor(spec.Number, five, spec.Colors + spec.EmptyBottles);
        }

        // §2.2: колба 2U — склад, куда можно лить что угодно, и ровно одна пустая. С таким складом уровень
        // заметно легче обычного, поэтому цветов больше: до 54-го на один, дальше на два (но не новые цвета раньше срока).
        private static void BuildFlask(LevelSpec spec, Range range, Rng rng)
        {
            int level = spec.Number;
            bool five = level >= FiveLayerFlasksFrom && rng.Chance(0.5);
            spec.Unit = five ? 5 : 4;
            int extra = level < 55 ? 1 : 2;
            spec.Colors = Math.Min((five ? FiveLayerColors(range, LevelTier.Medium) : range.Medium) + extra, MediumPaletteLimit(level));
            spec.BigFlask = true;
            spec.EmptyBottles = 1;

            // С 10 цветами дальше расти некуда, и без фишек колба к 300-му отставала от среднего уровня на 15 пунктов DS.
            // Тонировка и часы забрали бы единственную пустую, их здесь нет.
            if (level < FlaskFeaturesFrom)
                return;
            int count = level >= IntroLevel[MoveLimit] && rng.Chance(0.4) ? 2 : 1;
            var features = new List<string>();
            if (rng.Chance(0.6))
                features.Add(Hidden);
            features.AddRange(PickFeatures(level, range, count - features.Count, rng, includeHidden: false,
                allowHourglass: false, allowTint: false));
            foreach (var feature in features)
                AddFeature(spec, feature, range, rng);
        }

        // W и D в средних уровнях — только с 1801 (§3.1), колба — средний уровень.
        private static int MediumPaletteLimit(int level) => level >= 1801 ? 12 : 10;

        // §2.4: C полных, одна пустая и фигурка; с L108 через раз рецепт из двух цветов, иногда «?».
        private static void BuildRescue(LevelSpec spec, Range range, Rng rng)
        {
            int level = spec.Number;
            spec.Unit = 4;
            // С двумя пустыми фигурка работает как третья пустая, и уровень проходится сам (RW 90–98 %).
            // С одной пустой и цветами лёгкого уровня спасение чуть легче среднего — уровень-награда.
            spec.Colors = range.Easy;
            spec.EmptyBottles = 1;
            spec.RescueRecipeColors = level >= TwoColorRecipesFrom && rng.Chance(0.5) ? 2 : 1;
            spec.RescueShape = RescueCast.FigureOf(level);
            if (level > HiddenStyleDebut[HiddenTwo] && rng.Chance(0.5))
                AddHidden(spec, range, rng);
        }

        // §2.2: босс — пик пилы плюс особенность по L mod 100.
        private static void BuildBoss(LevelSpec spec, Range range, Rng rng)
        {
            int level = spec.Number;
            bool five = level >= FiveLayerBossesFrom && rng.Chance(BossFiveShare(range));
            spec.Unit = five ? 5 : 4;
            spec.Colors = five ? FiveLayerColors(range, LevelTier.Hard) : range.Hard;

            switch (level % 100)
            {
                case 25:
                    // Первому боссу хватит «?» вместо лишнего цвета: +1 цвет на 25-м уровне — это сразу RW 29 %.
                    if (level == 25)
                        AddHidden(spec, range, rng);
                    else
                        spec.Colors++;
                    break;
                case 75:
                    if (level >= IntroLevel[MoveLimit])
                    {
                        // Требование §5.8 «жадный игрок укладывается в лимит в 40 % попыток» выполнимо только до 8 цветов:
                        // при 9 жадный игрок уже теряется (16 %). Здесь трудность даёт сам лимит, а не число цветов.
                        spec.Unit = 4;
                        spec.Colors = Math.Min(range.Easy, 8);
                        spec.MoveLimitSlack = level <= 1000 ? 0.6 : level <= 1500 ? 0.5 : 0.4;
                    }
                    else
                        spec.Colors++;
                    break;
                case 50:
                    if (level >= TrapsFrom)
                        AddTrap(spec, rng);
                    else
                        AddHidden(spec, range, rng);
                    break;
            }
        }

        // Босс главы: смесь форм и фишки главы (2, с L301 — 3); в коротких главах с L150 он же «Ловушка».
        private static void BuildChapterBoss(LevelSpec spec, Range range, Rng rng)
        {
            int level = spec.Number;
            if (level == 2000)
            {
                BuildFinal(spec);
                return;
            }

            bool five = level >= FiveLayerBossesFrom && rng.Chance(BossFiveShare(range));
            spec.Unit = five ? 5 : 4;
            spec.Colors = five ? FiveLayerColors(range, LevelTier.Hard) : range.Hard;
            spec.ShapePool = ShapesFor(level, five, spec.Colors + spec.EmptyBottles);

            bool trap = level <= 500 && level % 100 == 50 && level >= TrapsFrom;
            int count = (level < 301 ? 2 : 3) - (trap ? 1 : 0);
            // «Ловушка» занимает пустую бутылку: тонированной и часам (у них пустая одна) места уже нет.
            foreach (var feature in PickFeatures(level, range, count, rng, allowHourglass: !trap, allowTint: !trap))
                AddFeature(spec, feature, range, rng);
            if (trap)
                AddTrap(spec, rng);
        }

        // §5.13: 12 цветов, U5, «?» H3, лёд 5 и замок на полных бутылках, без лимита ходов, сложность из лёгкой половины.
        private static void BuildFinal(LevelSpec spec)
        {
            spec.Unit = 5;
            spec.Colors = 12;
            spec.HiddenLayers = 3;
            spec.Ice.Add(new IceFeature { Placement = FeaturePlacement.Full, Turns = 5 });
            spec.Locks.Add(new LockFeature { Placement = FeaturePlacement.Full });
        }

        // «Ловушка»: лёд на пустой или замок на пустой, второй — только при C ≤ 8 (при 10 цветах почти нерешаемо).
        private static void AddTrap(LevelSpec spec, Rng rng)
        {
            if (spec.Unit == 4 && spec.Colors <= 8 && rng.Chance(0.5))
                spec.Locks.Add(new LockFeature { Placement = FeaturePlacement.Empty });
            else
                spec.Ice.Add(new IceFeature { Placement = FeaturePlacement.Empty, Turns = 3 + rng.NextInt(3) });
        }

        private static List<string> PickFeatures(int level, Range range, int count, Rng rng, bool includeHidden = true,
            bool allowHourglass = true, bool allowTint = true)
        {
            var options = new List<(string feature, double weight)>();
            if (includeHidden && level >= IntroLevel[Hidden])
                options.Add((Hidden, Math.Max(range.HiddenShare, 0.25)));
            foreach (var (feature, weight) in MechanicWeights)
                // Часы держатся на единственной пустой бутылке, в комбо с другими фишками уровень почти не решается.
                if (level >= IntroLevel[feature] && !(feature == Hourglass && (count > 1 || !allowHourglass))
                    && (feature != Tint || allowTint))
                    options.Add((feature, weight));

            var picked = new List<string>();
            while (picked.Count < count && options.Count > 0)
            {
                double total = 0;
                foreach (var option in options)
                    total += option.weight;
                double roll = rng.NextDouble() * total;
                int index = 0;
                for (; index < options.Count - 1; index++)
                {
                    roll -= options[index].weight;
                    if (roll < 0)
                        break;
                }
                picked.Add(options[index].feature);
                // Замок и цветной замок вместе не ставим: один уровень — одна разновидность замка.
                string twin = options[index].feature == Lock ? ColoredLock : options[index].feature == ColoredLock ? Lock : null;
                options.RemoveAt(index);
                if (twin != null)
                    options.RemoveAll(o => o.feature == twin);
            }
            return picked;
        }

        private static void AddFeature(LevelSpec spec, string feature, Range range, Rng rng)
        {
            switch (feature)
            {
                case Hidden:
                    AddHidden(spec, range, rng);
                    break;
                case Lock:
                    spec.Locks.Add(new LockFeature { Placement = FeaturePlacement.Full });
                    break;
                case ColoredLock:
                    spec.Locks.Add(new LockFeature { Placement = FeaturePlacement.Full, Colored = true });
                    break;
                case Ice:
                    spec.Ice.Add(new IceFeature { Placement = FeaturePlacement.Full, Turns = 3 + rng.NextInt(2) });
                    // С L801 — иногда два льда с разными счётчиками.
                    if (spec.Number >= 801 && rng.Chance(0.5))
                        spec.Ice.Add(new IceFeature { Placement = FeaturePlacement.Full, Turns = 5 });
                    break;
                case Tint:
                    spec.Tints.Add(new TintFeature { Placement = FeaturePlacement.Empty });
                    break;
                case Hourglass:
                    UseHourglass(spec);
                    break;
                case Pipette:
                    spec.Pipettes = 1;
                    break;
            }
        }

        // «?» по-разному (§5.1): в части бутылок, во всех по две, «туман» (виден только верхний слой)
        // и «лесенка» (у каждой бутылки своя глубина). Разновидность открывается на своём первом уровне.
        private static void AddHidden(LevelSpec spec, Range range, Rng rng)
        {
            int level = spec.Number;
            var styles = new List<string> { Hidden, Hidden };
            foreach (var pair in HiddenStyleDebut)
                if (level > pair.Value)
                    styles.Add(pair.Key);
            string style = rng.Pick(styles);

            int layers = range.HiddenMin + rng.NextInt(range.HiddenMax - range.HiddenMin + 1);
            // H3 в обычной разновидности — только в 5-слойных: из пяти видно хотя бы два.
            if (layers >= 3 && spec.Unit < 5)
                layers = 2;
            layers = Math.Max(1, layers);

            switch (style)
            {
                case HiddenTwo:
                    spec.HiddenLayers = Math.Max(2, layers);
                    spec.HiddenBottles = -1;
                    break;
                case HiddenFog:
                    spec.HiddenLayers = spec.Unit - 1;
                    spec.HiddenBottles = -1;
                    break;
                case HiddenStairs:
                    spec.HiddenLayers = spec.Unit - 1;
                    spec.HiddenVaried = true;
                    spec.HiddenBottles = -1;
                    break;
                default:
                    spec.HiddenLayers = layers;
                    spec.HiddenBottles = Math.Max(2, spec.Colors / 2 + rng.NextInt(2));
                    break;
            }
        }

        // При двух пустых переворот ни разу не сократил решение (240 раскладок), так что часы ставим с одной пустой.
        // Одна пустая при 8+ цветах почти не решается даже с переворотом, поэтому не больше 7 цветов и больше кандидатов.
        private static void UseHourglass(LevelSpec spec)
        {
            spec.EmptyBottles = 1;
            spec.Colors = Math.Min(7, Math.Max(5, spec.Colors - 2));
            spec.HourglassFlips = 1;
        }

        private static int FiveLayerColors(Range range, LevelTier tier) =>
            range.From >= 501 ? ColorsFor(range, tier, true) : ColorsFor(range, tier, false) - 1;

        // Доля обычных уровней без фишек и «?»: классика остаётся, но уже не большинство.
        private static double CleanShare(int level) => level <= 1000 ? 0.35 : 0.30;
        private static double ComboShare(int level) => level <= 300 ? 0.20 : level <= 1100 ? 0.25 : 0.30;
        private static int MaxNormalFeatures(int level) => level < IntroLevel[Combo] ? 1 : 2;

        // §3.2: сколько фишек (включая «?») может быть в одном уровне.
        public static int MaxFeatures(LevelType type, int level)
        {
            bool boss = type == LevelType.Boss || type == LevelType.ChapterBoss;
            if (level == 2000) return 4;
            if (level < IntroLevel[Hidden]) return 0;
            if (level < IntroLevel[Combo]) return boss ? 2 : 1;
            return boss ? 3 : 2;
        }

        // До 501 доли 5-слойных боссов в таблице нет — берём как у особых уровней.
        private static double BossFiveShare(Range range) => range.FiveShare > 0 ? range.FiveShare : 0.3;

        private static List<string> ShapesFor(int level, bool fiveLayers, int bottles)
        {
            var shapes = new List<string>();
            foreach (var (from, shape, five) in SpecialShapes)
            {
                if (level < from || five != fiveLayers || shapes.Contains(shape))
                    continue;
                // Чайник при 5 слоях — только в двух рядах (w ≥ 58), зелье — только при 4 слоях.
                if (fiveLayers && shape == "teapot" && bottles > 10)
                    continue;
                shapes.Add(shape);
            }
            return shapes;
        }

        // ≤ 14 бутылок на старте (§3.1): лишние цвета снимаем, а не бутылки-фишки.
        private static void FitOnScreen(LevelSpec spec)
        {
            int extra = spec.EmptyBottles + (spec.BigFlask ? 1 : 0) + (spec.HasRescue ? 1 : 0);
            spec.Colors = Math.Min(spec.Colors, MaxBottlesAtStart - extra);
        }

        // W (11-й) — с 1101 только в H и боссах, D (12-й) — с 1601 там же, с 1801 и в M (§3.1).
        private static int PaletteFor(LevelSpec spec)
        {
            int level = spec.Number;
            int allowed = level >= 1601 ? 12 : level >= 1101 ? 11 : 10;
            bool late = spec.Tier == LevelTier.Hard || spec.Tier == LevelTier.Boss
                || spec.Type == LevelType.Boss || spec.Type == LevelType.ChapterBoss
                || (level >= 1801 && spec.Tier == LevelTier.Medium);
            return Math.Max(spec.Colors, late ? allowed : 10);
        }

        // §3.3: на 4–30 уже настоящие уровни, но без стены — нижняя граница RW, предел OPT, без тупика в первые 5 ходов.
        public static (double minRandomWinRate, int maxPar, bool noEarlyTrap) EarlyLimits(int level, LevelType type)
        {
            if (level > 30)
                return (0, 0, false);
            bool boss = type == LevelType.Boss || type == LevelType.ChapterBoss;
            if (level <= 10)
                return (0.5, 22, true);
            return (boss ? 0.25 : 0.35, 26, true);
        }

        private static void ApplyEarlyLimits(LevelSpec spec)
        {
            var limits = EarlyLimits(spec.Number, spec.Type);
            spec.MinRandomWinRate = limits.minRandomWinRate;
            spec.MaxPar = limits.maxPar;
            spec.NoEarlyTrap = limits.noEarlyTrap;
        }

        // Какие фишки реально стоят в описании уровня — для отчёта и проверки расписания.
        public static List<string> FeaturesOf(LevelSpec spec)
        {
            var features = new List<string>();
            if (spec.HasHidden) features.Add(Hidden);
            foreach (var lockFeature in spec.Locks)
                features.Add(lockFeature.Colored ? ColoredLock : Lock);
            if (spec.Ice.Count > 0) features.Add(Ice);
            if (spec.Tints.Count > 0) features.Add(Tint);
            if (spec.Unit == 5) features.Add(FiveLayers);
            if (spec.HasMoveLimit) features.Add(MoveLimit);
            if (spec.HourglassFlips > 0) features.Add(Hourglass);
            if (spec.Pipettes > 0) features.Add(Pipette);
            return features;
        }
    }
}
