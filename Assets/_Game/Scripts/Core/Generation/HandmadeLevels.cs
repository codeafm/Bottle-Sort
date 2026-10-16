using System;
using System.Collections.Generic;

namespace BottleSort.Core
{
    // Уровни, заданные вручную по номеру. Сейчас это обучающие 1–3 из LEVEL_DESIGN.md §3.4;
    // сюда же потом лягут закреплённые обучающие уровни фишек.
    public static class HandmadeLevels
    {
        public const int TutorialCount = 3;

        private sealed class Entry
        {
            public string Layout;
            public int Par;
            // Ходы из документа, бутылки с единицы. null — берём оптимальное решение солвера.
            public int[] Script;
            public bool Strict;
        }

        private static readonly Dictionary<int, Entry> Entries = new Dictionary<int, Entry>
        {
            [1] = new Entry { Layout = "RRBB BBRR - -", Par = 3, Script = new[] { 1, 3, 2, 1, 2, 3 }, Strict = true },
            [2] = new Entry { Layout = "GBRR RGBB BRGG - -", Par = 7, Script = new[] { 1, 4, 2, 1, 3, 2, 3, 4, 1, 3, 2, 1, 2, 4 } },
            [3] = new Entry { Layout = "YYYR RRBY BGGB GBRG - -", Par = 9 }
        };

        public static bool IsTutorial(int level) => level >= 1 && level <= TutorialCount;
        public static bool Has(int level) => Entries.ContainsKey(level);

        public static LevelState Tutorial(int level)
        {
            if (!IsTutorial(level))
                throw new ArgumentOutOfRangeException(nameof(level));
            return LevelState.FromLayout(4, Entries[level].Layout);
        }

        public static int TutorialPar(int level)
        {
            if (!IsTutorial(level))
                throw new ArgumentOutOfRangeException(nameof(level));
            return Entries[level].Par;
        }

        public static bool TryGet(int level, out GeneratedLevel generated)
        {
            if (!Entries.TryGetValue(level, out var entry))
            {
                generated = null;
                return false;
            }

            var state = LevelState.FromLayout(4, entry.Layout);
            generated = new GeneratedLevel
            {
                Spec = new LevelSpec { Number = level, Colors = state.CountColors(), Unit = 4, EmptyBottles = 2, Candidates = 1 },
                State = state,
                Par = entry.Par,
                ParExact = true,
                Solution = entry.Script != null ? ParseScript(entry.Script) : Solver.Solve(state).Moves,
                PassedAllFilters = true,
                IsHandmade = true,
                IsTutorial = IsTutorial(level)
            };
            return true;
        }

        public static TutorialGuide CreateGuide(GeneratedLevel level, GameSession session)
        {
            bool strict = Entries.TryGetValue(level.Spec.Number, out var entry) && entry.Strict;
            return new TutorialGuide(session, level.Solution, strict);
        }

        private static List<Move> ParseScript(int[] oneBased)
        {
            var moves = new List<Move>(oneBased.Length / 2);
            for (int i = 0; i < oneBased.Length; i += 2)
                moves.Add(Move.Pour(oneBased[i] - 1, oneBased[i + 1] - 1));
            return moves;
        }
    }

    public static class LevelCatalog
    {
        // Ручной уровень, если он задан для этого номера, иначе — из генератора.
        public static GeneratedLevel Get(int level, Func<int, LevelSpec> specFor)
        {
            if (HandmadeLevels.TryGet(level, out var handmade))
                return handmade;
            return LevelGenerator.Generate(specFor(level));
        }
    }
}
