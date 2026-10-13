using System;
using NUnit.Framework;

namespace CultOfComfort.Client.Tests
{
    public class BarkBookTests
    {
        [Test]
        public void Line_EveryKind_HasSomethingToSay()
        {
            foreach (BarkKind kind in Enum.GetValues(typeof(BarkKind)))
            {
                // Act
                var line = BarkBook.Line(kind, 3);

                // Assert
                Assert.IsFalse(string.IsNullOrWhiteSpace(line), kind.ToString());
            }
        }

        [Test]
        public void Line_SameKindAndRoll_SameLine()
        {
            // Act & Assert — the same Night replays the same shouts.
            Assert.AreEqual(BarkBook.Line(BarkKind.RaiderArrives, 17), BarkBook.Line(BarkKind.RaiderArrives, 17));
        }

        [Test]
        public void Director_Crowded_KeepsThreeAndRestsEachSpeaker()
        {
            // Arrange
            var director = new BubbleDirector();

            // Act
            var a = director.TrySpeak("raider-1", 0f);
            var b = director.TrySpeak("raider-2", 0f);
            var c = director.TrySpeak("prophet", 0f);
            var d = director.TrySpeak("raider-3", 0f);
            var again = director.TrySpeak("raider-1", 3f);
            var later = director.TrySpeak("raider-1", 9f);

            // Assert
            Assert.IsTrue(a && b && c);
            Assert.IsFalse(d, "at most three bubbles at once");
            Assert.IsFalse(again, "one speaker rests 8 seconds");
            Assert.IsTrue(later);
        }

        [Test]
        public void Arrival_EveryRaiderType_HasItsOwnShout()
        {
            foreach (CultOfComfort.Simulation.Balance.EnemyType type in Enum.GetValues(typeof(CultOfComfort.Simulation.Balance.EnemyType)))
            {
                // Act & Assert
                Assert.IsFalse(string.IsNullOrWhiteSpace(BarkBook.Arrival(type, 2)), type.ToString());
            }

            Assert.AreEqual("HONK.", BarkBook.Arrival(CultOfComfort.Simulation.Balance.EnemyType.Goose, 0));
        }
    }
}
