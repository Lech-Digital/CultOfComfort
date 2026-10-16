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
            Assert.IsNull(step.Line, "the note already says \"Done! Tap to claim\": the arrow alone");
        }

        [Test]
        public void Step_NoErrandsLeft_CoachIsQuiet()
        {
            // Act & Assert
            Assert.AreSame(CoachStep.None, TutorialCoach.Step(null, null, Nothing, Table));
        }

        [Test]
        public void Step_PastTheCottage_StillPointsAtTheNextCard()
        {
            // Act — the Peat Cut errand, after the old three-errand tutorial ended.
            var step = TutorialCoach.Step(Errand(Index("peat-cut")), null, Nothing, Table);

            // Assert
            Assert.AreEqual(CoachTarget.BuildCard, step.Target);
            Assert.AreEqual(BuildingType.PeatCut, step.Type);
        }

        [Test]
        public void Step_FirstNight_PointsAtTheLamps()
        {
            // Act
            var step = TutorialCoach.Step(Errand(Index("first-night")), null, Nothing, Table);

            // Assert
            Assert.AreEqual(CoachTarget.StartNight, step.Target);
        }

        [Test]
        public void Step_RaiseTheHearth_PointsAtTheHearth()
        {
            // Arrange
            var hearth = new BuildingView(1, BuildingType.Hearth, 1, new GridPosition(5, 5), 0, 0, 0, null);

            // Act
            var step = TutorialCoach.Step(Errand(Index("hearth-2")), null, new[] { hearth }, Table);

            // Assert
            Assert.AreEqual(CoachTarget.Building, step.Target);
            Assert.AreEqual(1, step.BuildingId);
            StringAssert.Contains("Upgrade", step.Line);
        }

        [Test]
        public void Step_HearthAlreadyUpgrading_CoachIsQuiet()
        {
            // Arrange — hours of work: the note, not a nagging line.
            var hearth = new BuildingView(1, BuildingType.Hearth, 1, new GridPosition(5, 5), 0, 0, 0, DateTimeOffset.UtcNow.AddHours(2));

            // Act & Assert
            Assert.AreSame(CoachStep.None, TutorialCoach.Step(Errand(Index("hearth-2")), null, new[] { hearth }, Table));
        }

        [Test]
        public void Step_SendAMissionary_PointsAtTheMissionHouse()
        {
            // Act
            var step = TutorialCoach.Step(Errand(Index("missionary")), null, Nothing, Table);

            // Assert
            Assert.AreEqual(CoachTarget.Feature, step.Target);
            Assert.AreEqual("missionaries", step.Feature);
        }

        [Test]
        public void Step_PlacingTheChute_SaysBesideTheRaidersRoad()
        {
            // Act — the chute errand, before the first Night (spec 2026-10-16).
            var step = TutorialCoach.Step(Errand(Index("slurry-chute")), BuildingType.SlurryChute, Nothing, Table);

            // Assert
            Assert.AreEqual(CoachTarget.GhostConfirm, step.Target);
            StringAssert.Contains("raiders' road", step.Line);
            Assert.Less(Index("slurry-chute"), Index("first-night"), "the first Night is defended");
        }

        private static int Index(string id)
        {
            for (var i = 0; i < ErrandBook.Default.Errands.Count; i++)
            {
                if (ErrandBook.Default.Errands[i].Id == id)
                {
                    return i;
                }
            }

            throw new ArgumentException($"no errand {id}: the errand book changed, re-check the coach");
        }
    }
}
