using System;
using System.Collections.Generic;

namespace BottleSort.Core
{
    // Кто кого спасает: фигура-сосуд и зверёк внутри для каждого уровня-спасения (LEVEL_DESIGN.md §2.4).
    // Зависит только от номера уровня, поэтому в базе уровней не хранится.
    public static class RescueCast
    {
        // Новая фигура на каждом из первых десяти спасений, дальше — перемешанными кругами по 10.
        public static readonly string[] Figures =
        {
            "hippo", "whale", "owl", "turtle", "elephant", "fish", "fox", "bear", "rabbit", "dragon"
        };

        // Новый зверёк на каждом втором спасении, порядок и id — как у героев vfx-art (RescueHeroes).
        // Котёнок — талисман игры, он встречается чаще остальных.
        public static readonly string[] Characters =
        {
            "cat", "puppy", "chick", "bunny", "penguin", "panda", "fox", "duckling", "hedgehog", "owlet"
        };

        public const string Mascot = "cat";
        private const double MascotShare = 0.35;

        public static bool IsRescue(int level) => level >= 1 && LevelSchedule.TypeOf(level) == LevelType.Rescue;

        // 0 — первое спасение игры (L8), 1 — второе (L28) и так далее.
        public static int IndexOf(int level) => (level - LevelSchedule.FirstRescue) / 20;

        public static int LevelOf(int index) => LevelSchedule.FirstRescue + index * 20;

        public static string FigureOf(int level)
        {
            int index = IndexOf(level);
            if (index < Figures.Length)
                return Figures[index];

            int round = index / Figures.Length;
            var order = ShuffledRound(round);
            // Стык кругов: первая фигура нового круга не должна повторять последнюю предыдущего.
            var previous = ShuffledRound(round - 1);
            if (order[0] == previous[Figures.Length - 1])
                (order[0], order[1]) = (order[1], order[0]);
            return order[index % Figures.Length];
        }

        public static string CharacterOf(int level)
        {
            int index = IndexOf(level);
            string previous = null, current = Mascot;
            int introduced = 0;
            for (int i = 0; i <= index; i++)
            {
                string figure = FigureOf(LevelOf(i));
                // Новый герой положен на каждом втором спасении; если он не подходит к фигуре, выйдет на следующем.
                bool due = introduced < Characters.Length && i >= introduced * 2;
                if (due && Fits(Characters[introduced], figure, previous))
                    current = Characters[introduced++];
                else
                    current = PickIntroduced(i, Math.Max(introduced, 1), figure, previous);
                previous = current;
            }
            return current;
        }

        // Один и тот же зверь в звере смотрится странно, а цыплёнок и утёнок подряд почти не отличаются.
        private static readonly (string figure, string hero)[] SameAnimal =
        {
            ("fox", "fox"), ("owl", "owlet"), ("rabbit", "bunny")
        };

        private static bool Fits(string hero, string figure, string previous)
        {
            if (hero == previous)
                return false;
            foreach (var (sameFigure, sameHero) in SameAnimal)
                if (figure == sameFigure && hero == sameHero)
                    return false;
            return !(IsYellowBird(hero) && IsYellowBird(previous));
        }

        private static bool IsYellowBird(string hero) => hero == "chick" || hero == "duckling";

        private static string PickIntroduced(int index, int introduced, string figure, string previous)
        {
            var rng = new Rng(Seeds.Combine(Seeds.FromText("rescue-character"), index));
            if (Fits(Mascot, figure, previous) && rng.Chance(MascotShare))
                return Mascot;
            var others = new List<string>();
            for (int i = 1; i < introduced; i++)
                if (Fits(Characters[i], figure, previous))
                    others.Add(Characters[i]);
            if (others.Count > 0)
                return rng.Pick(others);
            // Пока введён один котёнок, повтор неизбежен.
            return Mascot;
        }

        private static List<string> ShuffledRound(int round)
        {
            var order = new List<string>(Figures);
            if (round <= 0)
                return order;
            var rng = new Rng(Seeds.Combine(Seeds.FromText("rescue-figure"), round));
            rng.Shuffle(order);
            return order;
        }
    }
}
