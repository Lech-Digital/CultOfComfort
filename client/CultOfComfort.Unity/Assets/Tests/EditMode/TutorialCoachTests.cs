using System;
using System.Collections.Generic;
using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Grid;
using NUnit.Framework;

namespace CultOfComfort.Client.Tests
{
    public class TutorialCoachTests
    {
        private static readonly BuildingTable Table = BuildingTable.Default;
        private static readonly IReadOnlyList<BuildingView> Nothing = Array.Empty<BuildingView>();

        private static ErrandView Errand(int index, bool done = false)
        {
            var e = ErrandBook.Default.Errands[index];
            return new ErrandView(index, ErrandBook.Default.Errands.Count, e.Id, e.Text, done ? e.Target : 0, e.Target, done, e.Reward, e.Goal, e.Type);
        }

        private static BuildingView Woodcutter(int level, int ready, DateTimeOffset? upgrading = null) =>
            new BuildingView(7, BuildingType.Woodcutter, level, new GridPosition(3, 4), ready, 800, 100, upgrading);

        [Test]
        public void Step_FirstErrandNothingPlaced_PointsAtTheLeanToCard()
        {
            // Act
            var step = TutorialCoach.Step(Errand(0), null, Nothing, Table);

            // Assert
            Assert.AreEqual(CoachTarget.BuildCard, step.Target);
            Assert.AreEqual(BuildingType.Woodcutter, step.Type);
            StringAssert.Contains("Woodcutter", step.Line);
        }

        [Test]
        public void Step_PlacingTheErrandBuilding_PointsAtConfirm()
        {
            // Act
            var step = TutorialCoach.Step(Errand(0), BuildingType.Woodcutter, Nothing, Table);

            // Assert
            Assert.AreEqual(CoachTarget.GhostConfirm, step.Target);
        }

        [Test]
        public void Step_PlacingSomethingElse_StillPointsAtTheRightCard()
        {
            // Act
            var step = TutorialCoach.Step(Errand(0), BuildingType.Cottage, Nothing, Table);

            // Assert
            Assert.AreEqual(CoachTarget.BuildCard, step.Target);
            Assert.AreEqual(BuildingType.Woodcutter, step.Type);
        }

        [Test]
        public void Step_UnderConstruction_WaitsWithoutAnArrow()
        {
            // Act
            var step = TutorialCoach.Step(Errand(0), null, new[] { Woodcutter(0, 0, DateTimeOffset.UtcNow.AddSeconds(30)) }, Table);

            // Assert
            Assert.IsFalse(step.HasArrow);
            Assert.IsNotNull(step.Line);
        }

        [Test]
        public void Step_ErrandDone_PointsAtTheNote()
        {
            // Act
            var step = TutorialCoach.Step(Errand(0, done: true), null, Nothing, Table);

            // Assert
            Assert.AreEqual(CoachTarget.ErrandNote, step.Target);
        }

        [Test]
        public void Step_PastTheCottage_TutorialIsOver()
        {
            // Act & Assert
            Assert.AreSame(CoachStep.None, TutorialCoach.Step(Errand(TutorialCoach.LastErrand + 1), null, Nothing, Table));
            Assert.AreSame(CoachStep.None, TutorialCoach.Step(null, null, Nothing, Table));
        }

        [Test]
        public void LastErrand_IsTheCottage()
        {
            // Assert
            Assert.AreEqual("cottage", ErrandBook.Default.Errands[TutorialCoach.LastErrand].Id, "the errand book changed: re-check the tutorial");
        }
    }
}
