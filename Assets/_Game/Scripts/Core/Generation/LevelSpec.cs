using System.Collections.Generic;

namespace BottleSort.Core
{
    // Куда ставить фишку: на бутылку со слоями или на пустую (LEVEL_DESIGN.md §5: мягкая и жёсткая формы).
    public enum FeaturePlacement
    {
        Full,
        Empty
    }

    public sealed class IceFeature
    {
        public FeaturePlacement Placement = FeaturePlacement.Full;
        public int Turns = 3;
    }

    public sealed class LockFeature
    {
        public FeaturePlacement Placement = FeaturePlacement.Full;
        // false — открывается любым собранным цветом, true — только своим (цветной замок, L401).
        public bool Colored;
    }

    public sealed class TintFeature
    {
        // На пустой — случайный цвет; на полной — цвет её нижнего слоя.
        public FeaturePlacement Placement = FeaturePlacement.Empty;
    }

    // Описание уровня для генератора. Какие параметры у какого номера — решает план уровней
    // (LEVEL_DESIGN.md §3), здесь только то, что нужно для раскладки.
    public sealed class LevelSpec
    {
        public int Number = 1;
        public int GenVersion = LevelGenerator.CurrentGenVersion;

        public int Colors = 4;
        public int Unit = 4;
        public int EmptyBottles = 2;
        // Из скольких первых цветов палитры выбирать: 10 до L1100, 11 до L1600, потом 12.
        public int PaletteSize = 10;
        public bool BigFlask;

        public int HiddenLayers;
        // В скольких бутылках со слоями прятать низ; -1 — во всех.
        public int HiddenBottles = -1;

        public List<IceFeature> Ice = new List<IceFeature>();
        public List<LockFeature> Locks = new List<LockFeature>();
        public List<TintFeature> Tints = new List<TintFeature>();
        public int Pipettes;
        public int HourglassFlips;

        // k из формулы лимита ходов: limit = par + max(8, ceil(k·par)). 0 — без лимита.
        public double MoveLimitSlack;

        // Формы для особых уровней. Пусто — все бутылки classic.
        public List<string> ShapePool = new List<string>();

        public int Candidates = 64;
        // null — берём первый подходящий кандидат; иначе — ближайший к этой сложности DS (0..100).
        public double? TargetDifficulty;
        public bool ComputeMetrics;
        public SolverOptions Solver = SolverOptions.Default;
        public AnalyzerOptions Analyzer = AnalyzerOptions.Default;

        public bool AllowsExtraBottle => !BigFlask;
        public bool HasHidden => HiddenLayers > 0 && HiddenBottles != 0;
    }
}
