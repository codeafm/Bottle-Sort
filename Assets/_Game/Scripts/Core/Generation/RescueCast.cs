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
            for (int i = 0; i <= index; i++)
            {
                current = CharacterAt(i, previous);
                previous = current;
            }
            return current;
        }

        public static int CharactersIntroducedBy(int rescueIndex) =>
            System.Math.Min(Characters.Length, rescueIndex / 2 + 1);

        private static string CharacterAt(int index, string previous)
        {
            if (index % 2 == 0 && index / 2 < Characters.Length)
                return Characters[index / 2];

            int introduced = CharactersIntroducedBy(index);
            if (introduced == 1)
                return Mascot;

            var rng = new Rng(Seeds.Combine(Seeds.FromText("rescue-character"), index));
            if (previous != Mascot && rng.Chance(MascotShare))
                return Mascot;
            var others = new List<string>();
            for (int i = 1; i < introduced; i++)
                if (Characters[i] != previous)
                    others.Add(Characters[i]);
            return others.Count > 0 ? rng.Pick(others) : Mascot;
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
