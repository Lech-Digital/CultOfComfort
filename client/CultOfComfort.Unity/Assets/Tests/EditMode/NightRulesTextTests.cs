using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Combat;
using NUnit.Framework;

namespace CultOfComfort.Client.Tests
{
    public class NightRulesTextTests
    {
        [Test]
        public void ObjectiveLine_Met_SaysSoWithATick()
        {
            // Act & Assert
            Assert.AreEqual("Third star: no building wrecked ✓", NightRulesText.ObjectiveLine(NightObjective.NothingWrecked, true));
            Assert.IsNull(NightRulesText.ObjectiveLine(NightObjective.None, true), "a Night planned before has none");
        }

        [Test]
        public void GapLine_WithTally_SaysWhenTheWaveComes()
        {
            // Act & Assert
            Assert.AreEqual("Wave 2/3 in 8s · 4 driven off", NightRulesText.GapLine(2, 3, 8, 4));
        }

        [Test]
        public void DawnCard_LeftToTheTowers_SaysWhyOneStar()
        {
            // Arrange
            var result = new BattleResult(BattleOutcome.Won, 1, 100, 100, 1000, 6, 0, 0UL);

            // Act
            var model = DawnCard.Build(result, null, null, default, null, "Wulfric", NightObjective.NothingWrecked, played: false);

            // Assert
            CollectionAssert.Contains(model.Earned, "Left to the towers: one star. Play the Night to earn more.");
        }

        [Test]
        public void DawnCard_PlayedObjectiveMissedAndAPerkWaiting_SaysBoth()
        {
            // Arrange
            var result = new BattleResult(BattleOutcome.Won, 2, 90, 100, 1000, 6, 0, 0UL, ObjectiveMet: false, EarlyCalls: 2);
            var report = new NightReport(1, false, false, 2, 90, 100, new int[0], 96, "x");

            // Act
            var model = DawnCard.Build(result, report, null, default, 1, "Wulfric", NightObjective.ThreeInOneStone, played: true, perkLevel: 2);

            // Assert
            CollectionAssert.Contains(model.Earned, "Third star: three raiders in one Hot Stone ✗");
            CollectionAssert.Contains(model.Earned, "+96 Scrap salvaged (2 waves called early)");
            CollectionAssert.Contains(model.Earned, "The Prophet is level 2: a perk to choose. Tap his portrait.");
        }
    }
}
