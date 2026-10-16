using BottleSort.Core;
using NUnit.Framework;

namespace BottleSort.Tests
{
    public class RulesTests
    {
        private static LevelState Layout(string layout, int unit = 4) => LevelState.FromLayout(unit, layout);

        [Test]
        public void Pour_MovesWholeTopRun()
        {
            var state = Layout("RGBB RRGG -");
            var outcome = state.Apply(Move.Pour(0, 2));

            Assert.AreEqual(2, outcome.Amount);
            Assert.AreEqual(Palette.Blue, outcome.Color);
            Assert.AreEqual("RG RRGG BB", state.ToString());
        }

        [Test]
        public void Pour_IsLimitedByFreeSpace()
        {
            var state = Layout("RBBB YYB");
            var outcome = state.Apply(Move.Pour(0, 1));

            Assert.AreEqual(1, outcome.Amount);
            Assert.AreEqual("RBB YYBB", state.ToString());
        }

        [Test]
        public void Pour_OntoDifferentColorIsRejected()
        {
            var state = Layout("RGB RG - -");
            Assert.AreEqual(MoveError.ColorMismatch, state.Check(Move.Pour(0, 1)));
        }

        [Test]
        public void Pour_RejectsObviousMistakes()
        {
            var state = Layout("RGBY RGBY - -");
            Assert.AreEqual(MoveError.SameBottle, state.Check(Move.Pour(0, 0)));
            Assert.AreEqual(MoveError.SourceEmpty, state.Check(Move.Pour(2, 0)));
            Assert.AreEqual(MoveError.TargetFull, state.Check(Move.Pour(0, 1)));
            Assert.AreEqual(MoveError.BadIndex, state.Check(Move.Pour(0, 9)));
            Assert.AreEqual(MoveError.None, state.Check(Move.Pour(0, 2)));
        }

        [Test]
        public void IllegalApply_Throws()
        {
            var state = Layout("RGB RG");
            Assert.Throws<System.InvalidOperationException>(() => state.Apply(Move.Pour(0, 1)));
        }

        [Test]
        public void Win_RequiresEveryColorInOneBottle()
        {
            Assert.IsTrue(Layout("RRRR BBBB - -").IsWon);
            Assert.IsFalse(Layout("RRRB BBBR - -").IsWon);
            Assert.IsTrue(Layout("- -").IsWon);
        }

        [Test]
        public void BigFlask_WinsOnlyWithExactlyOneColor()
        {
            var flask = new Bottle(8, "cylinder");
            foreach (char c in "RRRRBBBB")
                flask.Push(Palette.FromLetter(c));
            var mixed = new LevelState(4, new[] { flask, new Bottle(4) });
            Assert.IsFalse(mixed.IsWon);

            var outcome = mixed.Apply(Move.Pour(0, 1));
            Assert.IsTrue(outcome.CompletedBottle, "Flask left with RRRR counts as collected");
            Assert.IsTrue(outcome.Won);
        }

        [Test]
        public void SelectingCompletedBottle_IsNotAllowed()
        {
            var state = Layout("RRRR BBGB G - -");
            Assert.AreEqual(MoveError.SourceCompleted, state.CheckSelection(0));
            Assert.AreEqual(MoveError.None, state.CheckSelection(1));
            Assert.AreEqual(MoveError.SourceEmpty, state.CheckSelection(3));
        }

        [Test]
        public void Ice_BlocksBottleAndMeltsOnEveryMove()
        {
            var state = Layout("RGBB GGRR - -");
            state[1].FrozenTurns = 2;

            Assert.AreEqual(MoveError.SourceFrozen, state.Check(Move.Pour(1, 2)));
            Assert.AreEqual(MoveError.TargetFrozen, state.Check(Move.Pour(0, 1)));

            var first = state.Apply(Move.Pour(0, 2));
            Assert.AreEqual(1, state[1].FrozenTurns);
            Assert.IsFalse(first.ThawedAny);

            var second = state.Apply(Move.Pour(0, 3));
            Assert.AreEqual(0, state[1].FrozenTurns);
            Assert.IsTrue(second.ThawedAny);
            Assert.AreEqual(MoveError.None, state.Check(Move.Pour(1, 0)));
        }

        [Test]
        public void Lock_OpensWhenAnyColorIsCollected()
        {
            var state = Layout("RRR BGBG R -");
            state[1].IsLocked = true;

            Assert.AreEqual(MoveError.SourceLocked, state.Check(Move.Pour(1, 3)));
            Assert.AreEqual(MoveError.TargetLocked, state.Check(Move.Pour(2, 1)));

            var outcome = state.Apply(Move.Pour(2, 0));
            Assert.IsTrue(outcome.CompletedBottle);
            Assert.IsTrue(outcome.UnlockedAny);
            Assert.IsFalse(state[1].IsLocked);
        }

        [Test]
        public void ColoredLock_WaitsForItsOwnColor()
        {
            var red = new Bottle(4);
            foreach (char c in "RRR") red.Push(Palette.FromLetter(c));
            var blue = new Bottle(4);
            foreach (char c in "BBB") blue.Push(Palette.FromLetter(c));
            var looseRed = new Bottle(4);
            looseRed.Push(Palette.Red);
            var looseBlue = new Bottle(4);
            looseBlue.Push(Palette.Blue);
            var locked = new Bottle(4, lockColor: Palette.Blue) { IsLocked = true };

            var state = new LevelState(4, new[] { red, blue, looseRed, looseBlue, locked });

            state.Apply(Move.Pour(2, 0));
            Assert.IsTrue(state[4].IsLocked, "Red is collected, but the lock wants blue");

            var outcome = state.Apply(Move.Pour(3, 1));
            Assert.IsTrue(outcome.UnlockedAny);
            Assert.IsFalse(state[4].IsLocked);
        }

        [Test]
        public void HiddenLayer_OpensWhenItBecomesTop()
        {
            var state = Layout("RGBB - -");
            state[0].HiddenCount = 2;

            state.Apply(Move.Pour(0, 1));
            Assert.AreEqual(1, state[0].HiddenCount, "G is on top now and must be visible");
            Assert.IsTrue(state[0].IsLayerHidden(0));
            Assert.IsFalse(state[0].IsLayerHidden(1));

            var outcome = state.Apply(Move.Pour(0, 2));
            Assert.IsTrue(outcome.RevealedAny);
            Assert.AreEqual(0, state[0].HiddenCount);
        }

        [Test]
        public void HiddenLayers_NeverCoverTheTop()
        {
            var bottle = new Bottle(4);
            bottle.Push(Palette.Red);
            bottle.Push(Palette.Blue);
            bottle.HiddenCount = 3;
            bottle.RevealTop();
            Assert.AreEqual(1, bottle.HiddenCount);
        }

        [Test]
        public void TintedBottle_AcceptsOnlyItsColor()
        {
            var source = new Bottle(4);
            source.Push(Palette.Green);
            source.Push(Palette.Red);
            var tinted = new Bottle(4, acceptColor: Palette.Green);
            var state = new LevelState(4, new[] { source, tinted });

            Assert.AreEqual(MoveError.TargetRejectsColor, state.Check(Move.Pour(0, 1)));
            source.Pop();
            Assert.AreEqual(MoveError.None, state.Check(Move.Pour(0, 1)));
        }

        [Test]
        public void Pipette_GivesOneLayerPerMove()
        {
            var pipette = new Bottle(4, pourLimit: 1);
            foreach (char c in "RBBB") pipette.Push(Palette.FromLetter(c));
            var state = new LevelState(4, new[] { pipette, new Bottle(4) });

            var outcome = state.Apply(Move.Pour(0, 1));
            Assert.AreEqual(1, outcome.Amount);
            Assert.AreEqual("RBB B", state.ToString());
        }

        [Test]
        public void Hourglass_FlipReversesLayersAndSpendsAFlip()
        {
            var hourglass = new Bottle(4, "hourglass") { FlipsLeft = 1 };
            foreach (char c in "RGBY") hourglass.Push(Palette.FromLetter(c));
            var state = new LevelState(4, new[] { hourglass, new Bottle(4) });

            state.Apply(Move.Flip(0));
            Assert.AreEqual("YBGR", state[0].ToString());
            Assert.AreEqual(0, state[0].FlipsLeft);
            Assert.AreEqual(MoveError.NoFlipsLeft, state.Check(Move.Flip(0)));
        }

        [Test]
        public void LegalMoves_SkipBlockedBottles()
        {
            var state = Layout("RGBY RGBY - -");
            state[0].IsLocked = true;
            state[2].FrozenTurns = 1;

            var moves = new System.Collections.Generic.List<Move>();
            state.GetLegalMoves(moves);

            CollectionAssert.AreEquivalent(new[] { Move.Pour(1, 3) }, moves);
        }

        [Test]
        public void Clone_IsIndependent()
        {
            var state = Layout("RGBB - -");
            var copy = state.Clone();
            copy.Apply(Move.Pour(0, 1));

            Assert.AreEqual("RGBB - -", state.ToString());
            Assert.IsFalse(state.StateEquals(copy));
        }
    }
}
