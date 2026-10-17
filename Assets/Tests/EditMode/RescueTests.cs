using BottleSort.Core;
using NUnit.Framework;

namespace BottleSort.Tests
{
    public class RescueTests
    {
        private static Bottle Full(string layers)
        {
            var bottle = new Bottle(4);
            foreach (char c in layers)
                bottle.Push(Palette.FromLetter(c));
            return bottle;
        }

        private static Bottle Figure(string recipe)
        {
            var colors = new byte[recipe.Length];
            for (int i = 0; i < recipe.Length; i++)
                colors[i] = Palette.FromLetter(recipe[i]);
            return new Bottle(recipe.Length, "hippo", canPourOut: false, recipe: colors);
        }

        [Test]
        public void Figure_AcceptsOnlyNextRecipeColor()
        {
            var state = new LevelState(4, new[] { Full("RRBB"), Full("BBRR"), Figure("RRRRBBBB"), new Bottle(4) });

            Assert.AreEqual(MoveError.TargetRejectsColor, state.Check(Move.Pour(0, 2)));
            Assert.AreEqual(MoveError.None, state.Check(Move.Pour(1, 2)));
        }

        [Test]
        public void Figure_TakesNoMoreThanTheRecipeAsks()
        {
            var figure = Figure("RRRRBBBB");
            foreach (char c in "RRR")
                figure.Push(Palette.FromLetter(c));
            var state = new LevelState(4, new[] { Full("BBRR"), figure });

            var outcome = state.Apply(Move.Pour(0, 1));

            Assert.AreEqual(1, outcome.Amount, "only one red is missing before blue starts");
            Assert.AreEqual("BBR", state[0].ToString());
        }

        [Test]
        public void Figure_AcceptsNextColorOnTopOfDifferentOne()
        {
            var figure = Figure("RRRRBBBB");
            foreach (char c in "RRRR")
                figure.Push(Palette.FromLetter(c));
            var state = new LevelState(4, new[] { Full("RRBB"), figure });

            Assert.AreEqual(MoveError.None, state.Check(Move.Pour(0, 1)));
        }

        [Test]
        public void Figure_CannotBePouredFrom()
        {
            var figure = Figure("RRRR");
            figure.Push(Palette.Red);
            var state = new LevelState(4, new[] { figure, new Bottle(4) });

            Assert.AreEqual(MoveError.SourceNoPourOut, state.Check(Move.Pour(0, 1)));
            Assert.AreEqual(MoveError.SourceNoPourOut, state.CheckSelection(0));
        }

        [Test]
        public void Win_NeedsTheRecipeFilled()
        {
            var figure = Figure("RRRRBBBB");
            foreach (char c in "RRRR")
                figure.Push(Palette.FromLetter(c));
            var state = new LevelState(4, new[] { Full("BBBB"), figure, new Bottle(4) });
            Assert.IsFalse(state.IsWon, "blue is collected, but not in the figure");

            state.Apply(Move.Pour(0, 1));
            Assert.IsTrue(state.IsWon);
        }

        [Test]
        public void GeneratedRescueLevels_AreSolvable()
        {
            foreach (int level in new[] { 78, 128, 328, 378 })
            {
                var generated = LevelGenerator.Generate(LevelParams.SpecFor(level));
                Assert.AreEqual(LevelType.Rescue, generated.Spec.Type);

                var state = generated.State.Clone();
                foreach (var move in generated.Solution)
                    state.Apply(move);
                Assert.IsTrue(state.IsWon, $"L{level}");
            }
        }
    }
}
