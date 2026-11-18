using System;
using System.Collections.Generic;
using CultOfComfort.Simulation.Base;
using NUnit.Framework;

namespace CultOfComfort.Client.Tests
{
    public class FeatureUnlocksTests
    {
        private static readonly DateTimeOffset T0 = new DateTimeOffset(2026, 11, 17, 12, 0, 0, TimeSpan.Zero);
        private static readonly string[] Nothing = Array.Empty<string>();

        private static BaseSnapshot Fresh() => BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0).Snapshot(T0);

        private static DomainView Domain(DemandView demand = null) =>
            new DomainView(true, Array.Empty<DomainNodeView>(), Array.Empty<MissionaryView>(), 1, default, default, default, 0, null, Demand: demand);

        /// <summary>Hearth 2, one Night behind it, the lands open, the lord calm.</summary>
        private static BaseSnapshot HearthTwo() => Fresh() with { HearthLevel = 2, LastNightRecorded = 1, Domain = Domain(), Tension = 0 };

        [Test]
        public void Open_FreshVillage_NoSideShortcuts()
        {
            // Act & Assert
            CollectionAssert.IsEmpty(FeatureUnlocks.Open(Fresh(), Nothing, Nothing));
        }

        [Test]
        public void Open_AfterTheFirstNight_ThePilgrimsWayArrives()
        {
            // Act
            var open = FeatureUnlocks.Open(Fresh() with { LastNightRecorded = 1 }, Nothing, Nothing);

            // Assert
            CollectionAssert.AreEquivalent(new[] { "pilgrims" }, open);
        }

        [Test]
        public void Open_HearthTwoWithNothingTried_OneNewShortcutNotSix()
        {
            // Act — playtest 2026-11-17: six shortcuts and six toasts arrived at once at Hearth 2.
            var open = FeatureUnlocks.Open(HearthTwo(), Nothing, Nothing);

            // Assert
            CollectionAssert.AreEquivalent(new[] { "pilgrims" }, open);
        }

        [Test]
        public void Open_PilgrimsTried_TheLandsArrive()
        {
            // Act
            var open = FeatureUnlocks.Open(HearthTwo(), new[] { "pilgrims" }, new[] { "pilgrims" });

            // Assert — the preachers live on the Map since one world screen (spec 2026-11-17 fun first, D).
            CollectionAssert.AreEquivalent(new[] { "pilgrims", "map" }, open);
        }

        [Test]
        public void Open_LandsTried_StudyComesNext()
        {
            // Act
            var open = FeatureUnlocks.Open(HearthTwo(), new[] { "pilgrims", "map" }, new[] { "pilgrims", "map" });

            // Assert
            Assert.IsTrue(open.Contains("research"));
            Assert.IsFalse(open.Contains("festival"));
        }

        [Test]
        public void Open_Anything_NeverTheOldMissionariesOrTithes()
        {
            // Arrange — the lord cross, everything tried: his hall opens from the strip now, not a shortcut.
            var all = new[] { "pilgrims", "map", "research", "festival", "missionaries", "tithes" };

            // Act
            var open = FeatureUnlocks.Open(HearthTwo() with { Tension = FeatureUnlocks.TithesAtTension }, all, all);

            // Assert
            Assert.IsFalse(open.Contains("tithes"));
            Assert.IsFalse(open.Contains("missionaries"));
        }

        [Test]
        public void Open_ErrandNeedsTheMissionaries_TheyDoNotWaitInTheQueue()
        {
            // Arrange — the missionary errand, with the Pilgrims' Way shown but never tried.
            var errand = new ErrandView(8, 20, "missionary", "Send a preacher.", 0, 1, false, default, ErrandGoal.SendMissionary, null);

            // Act
            var open = FeatureUnlocks.Open(HearthTwo() with { Errand = errand }, new[] { "pilgrims" }, Nothing);

            // Assert
            Assert.IsTrue(open.Contains("map"));
        }

        [Test]
        public void Open_ShownBefore_StaysShown()
        {
            // Act — a shortcut never disappears once it has been on screen.
            var open = FeatureUnlocks.Open(Fresh(), new[] { "pilgrims", "research" }, Nothing);

            // Assert
            Assert.IsTrue(open.Contains("research"));
        }

        [Test]
        public void Open_HearthThree_EverythingEarnedAtOnce()
        {
            // Act — a village this far along doesn't wait in the queue.
            var open = FeatureUnlocks.Open(HearthTwo() with { HearthLevel = 3, Tension = 40, LastNightRecorded = 3 }, Nothing, Nothing);

            // Assert
            CollectionAssert.AreEquivalent(new[] { "pilgrims", "map", "research", "festival" }, open);
        }
    }
}
