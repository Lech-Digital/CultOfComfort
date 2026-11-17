using System;
using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Combat;
using NUnit.Framework;

namespace CultOfComfort.Client.Tests
{
    public class DawnCardTests
    {
        private static BattleResult Won(int stars) => new BattleResult(BattleOutcome.Won, stars, 67, 100, 1200, 5, 6, 0x1234);

        [Test]
        public void Build_RealNightWon_SaysWhatItEarnedAndOffersThePilgrimsWay()
        {
            // Arrange
            var report = new NightReport(1, false, false, 2, 67, 100, new[] { 3 }, 40, "abc");

            // Act
            var card = DawnCard.Build(Won(2), report, null, default, 1, "Wulfric");

            // Assert
            Assert.AreEqual("The Hearth stands", card.Title);
            Assert.AreEqual(2, card.Stars);
            CollectionAssert.Contains(card.Earned, "+40 Scrap salvaged");
            CollectionAssert.Contains(card.Earned, "1 building wrecked: repair it from its card");
            CollectionAssert.Contains(card.Earned, $"The Prophet: +{ProphetBook.XpForNight(2)} experience");
            Assert.AreEqual("Next: the Pilgrims' Way · stage 1", card.Next);
        }

        [Test]
        public void Build_NeverMentionsTheServersCheck()
        {
            // Act — playtest 2026-11-17: "VERIFIED" and the hash meant nothing to a player.
            var card = DawnCard.Build(Won(3), new NightReport(1, false, false, 3, 90, 100, Array.Empty<int>(), 0, "deadbeef"), null, default, null, "Wulfric");

            // Assert
            var all = string.Join(" ", card.Title, card.Summary, card.Flavour, string.Join(" ", card.Earned));
            StringAssert.DoesNotContain("deadbeef", all);
            StringAssert.DoesNotContain("erif", all);
            Assert.IsNull(card.Next);
        }

        [Test]
        public void Build_StageCleared_ShowsThePilgrimsGiftAndNoExperience()
        {
            // Act — the Pilgrims' Way gives no experience (BaseState.ResolveTrial).
            var card = DawnCard.Build(Won(3), null, 2, new ResourceBag(150, 0, 64, 0), 3, "Wulfric");

            // Assert
            Assert.AreEqual("Stage 2 cleared", card.Title);
            Assert.AreEqual(1, card.Earned.Count);
            StringAssert.StartsWith("The pilgrims leave", card.Earned[0]);
            Assert.AreEqual("Next: the Pilgrims' Way · stage 3", card.Next);
        }

        [Test]
        public void Build_Summary_CountsTheRaidersDrivenOff()
        {
            // Act — "6 reached the Hearth" read as a failure next to two stars.
            var card = DawnCard.Build(Won(2), null, null, default, null, "Wulfric");

            // Assert
            Assert.AreEqual("Hearth 67/100 · 5 driven off", card.Summary);
        }
    }
}
