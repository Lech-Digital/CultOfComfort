using System;
using System.Linq;
using CultOfComfort.Simulation.Base;
using NUnit.Framework;

namespace CultOfComfort.Client.Tests
{
    public class FeatureSheetPresenterTests
    {
        private static readonly DateTimeOffset T0 = new DateTimeOffset(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);

        private static (BaseState State, DateTimeOffset Now) Village(int hearth)
        {
            var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);
            var now = T0;
            for (var level = 2; level <= hearth; level++)
            {
                state.Apply(new DebugFillStores(Guid.NewGuid()), now);
                state.Apply(new UpgradeHearth(Guid.NewGuid()), now);
                now = now.AddHours(2);
            }

            state.Apply(new DebugFillStores(Guid.NewGuid()), now);
            return (state, now);
        }

        [Test]
        public void Research_HearthTwo_HearthTwoStudiesHaveButtonsDeeperOnesWait()
        {
            // Arrange
            var (state, now) = Village(2);

            // Act
            var sheet = FeatureSheetPresenter.Research(state.Snapshot(now), now);

            // Assert
            var axes = sheet.Rows.Single(r => r.Action == "research:sharper-axes");
            Assert.AreEqual("Study", axes.Button);
            Assert.IsTrue(axes.Enabled);
            var plumbing = sheet.Rows.Single(r => r.Action == "research:plumbing");
            Assert.IsNull(plumbing.Button, "plumbing opens at Hearth 3");
        }

        [Test]
        public void Research_FourRooms_EveryStudyOnItsRoomsTab()
        {
            // Arrange
            var (state, now) = Village(2);

            // Act
            var sheet = FeatureSheetPresenter.Research(state.Snapshot(now), now);

            // Assert
            CollectionAssert.AreEqual(new[] { "Workshop", "Night", "Pulpit", "Bath" }, sheet.Tabs);
            var studies = sheet.Rows.Where(r => r.Action != null && r.Action.StartsWith("research:")).ToList();
            Assert.AreEqual(ResearchBook.Default.Techs.Count, studies.Count);
            Assert.IsTrue(studies.All(r => sheet.Tabs.Contains(r.Tab)), "every study sits on a tab");
            Assert.AreEqual("Night", sheet.Rows.Single(r => r.Action == "research:springier-saplings").Tab);
            StringAssert.Contains("Towers hit 20% harder", sheet.Rows.Single(r => r.Action == "research:springier-saplings").Text);
        }

        [Test]
        public void Research_StopTappedOnce_ButtonAsksToBeSure()
        {
            // Arrange
            var (state, now) = Village(2);
            state.Apply(new StartResearch(Guid.NewGuid(), "sermon-notes"), now);

            // Act
            var sheet = FeatureSheetPresenter.Research(state.Snapshot(now), now, confirmStop: true);

            // Assert — on the button itself: a toast would sit behind the sheet.
            Assert.AreEqual("Sure? Tap again", sheet.Rows.Single(r => r.Action == "research-stop").Button);
        }

        [Test]
        public void Research_Studying_SaidOnEveryTab()
        {
            // Arrange
            var (state, now) = Village(2);
            state.Apply(new StartResearch(Guid.NewGuid(), "sermon-notes"), now);

            // Act
            var sheet = FeatureSheetPresenter.Research(state.Snapshot(now), now);

            // Assert — a heading with no tab shows on all of them.
            var reading = sheet.Rows.Single(r => r.Heading);
            Assert.IsNull(reading.Tab);
            StringAssert.Contains("Sermon Notes", reading.Text);
        }

        [Test]
        public void Research_Studying_ShowsTimeLeftAndAStopButton()
        {
            // Arrange
            var (state, now) = Village(2);
            state.Apply(new StartResearch(Guid.NewGuid(), "sermon-notes"), now);

            // Act
            var sheet = FeatureSheetPresenter.Research(state.Snapshot(now.AddMinutes(30)), now.AddMinutes(30));

            // Assert
            var notes = sheet.Rows.Single(r => !r.Heading && r.Text.StartsWith("Sermon Notes"));
            Assert.AreEqual("Stop", notes.Button, "a study can be stopped (owner 2026-10-16)");
            Assert.AreEqual("research-stop", notes.Action);
            Assert.IsTrue(notes.Enabled);
            StringAssert.Contains("left", notes.Detail);
            Assert.IsFalse(sheet.Rows.Single(r => r.Action == "research:sharper-axes").Enabled, "one study at a time");
        }

        [Test]
        public void Market_FreshVillage_TimberSwapsAffordableDevotionOnesNot()
        {
            // Arrange
            var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);

            // Act
            var sheet = FeatureSheetPresenter.Market(state.Snapshot(T0), BuildingTable.Default);

            // Assert
            Assert.IsTrue(sheet.Rows.Single(r => r.Action == "trade:Timber:Peat:20").Enabled);
            Assert.IsFalse(sheet.Rows.Single(r => r.Action == "trade:Devotion:Scrap:40").Enabled, "a new village has no Devotion");
            StringAssert.Contains("for 100 Timber", sheet.Rows.Single(r => r.Action == "trade:Timber:Peat:20").Text);
        }

        [Test]
        public void Cult_HearthTwo_ComfortPartsAndTheRelicsFound()
        {
            // Arrange
            var (state, now) = Village(2);

            // Act
            var sheet = FeatureSheetPresenter.Cult(state.Snapshot(now));

            // Assert
            Assert.IsTrue(sheet.Rows.Any(r => r.Text != null && r.Text.StartsWith("Hearth")), "the Hearth's Comfort is listed");
            Assert.IsTrue(sheet.Rows.Any(r => r.Text == "✓ The laser pointer"));
            Assert.IsTrue(sheet.Rows.Any(r => r.Text == "· The magnifying glass"), "not found until Hearth 3");
        }

        [Test]
        public void Cult_Tabs_EveryRowOnOneOfThem()
        {
            // Arrange
            var (state, now) = Village(2);

            // Act
            var sheet = FeatureSheetPresenter.Cult(state.Snapshot(now));

            // Assert
            CollectionAssert.AreEqual(new[] { "Hearth", "People", "Comfort", "Relics", "Errand" }, sheet.Tabs);
            Assert.IsTrue(sheet.Rows.All(r => sheet.Tabs.Contains(r.Tab)));
            Assert.IsTrue(sheet.Rows.Any(r => r.Tab == FeatureSheetPresenter.RelicsTab && r.Text == "✓ The laser pointer"));
        }

        [Test]
        public void Cult_HearthTabAtLevelOne_OffersTheRaiseAndSaysWhatComesNext()
        {
            // Arrange — a rich level 1 village.
            var (state, now) = Village(1);

            // Act
            var hearth = FeatureSheetPresenter.Cult(state.Snapshot(now)).Rows.Where(r => r.Tab == FeatureSheetPresenter.HearthTab).ToList();

            // Assert
            var raise = hearth.Single(r => r.Action == "hearth:upgrade");
            Assert.AreEqual("Raise", raise.Button);
            Assert.IsTrue(raise.Enabled);
            Assert.IsTrue(hearth.Any(r => r.Text != null && r.Text.Contains("The map opens")), "Hearth 2 opens the map");
            Assert.IsTrue(hearth.Any(r => r.Text != null && r.Text.StartsWith("· New: ") && r.Text.Contains("Cistern")));
        }

        [Test]
        public void Unlocks_HearthTwoToThree_TheBathhouseAndMoreMissionaries()
        {
            // Act
            var lines = FeatureSheetPresenter.Unlocks(2, 3, BuildingTable.Default);

            // Assert
            Assert.IsTrue(lines.Any(l => l.Contains("Bathhouse")));
            Assert.IsTrue(lines.Any(l => l.StartsWith("2 missionaries at once")));
        }

        [Test]
        public void Prophet_AtLevelThree_OffersThePairToChoose()
        {
            // Arrange
            var state = BaseState.Restore(BuildingTable.Default, VillageMap.Demo,
                BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, System.DateTimeOffset.UnixEpoch).ToData() with { ProphetXp = 150 });

            // Act
            var sheet = FeatureSheetPresenter.Prophet(state.Snapshot(System.DateTimeOffset.UnixEpoch));

            // Assert
            Assert.AreEqual("Level 3 of 10", sheet.Rows[0].Text);
            CollectionAssert.AreEquivalent(new[] { "perk:bathrobe", "perk:slippers" }, sheet.Rows.Where(r => r.Button == "Choose").Select(r => r.Action).ToArray());
        }

        [Test]
        public void PilgrimsWay_StageFive_ABossAndAWalkButton()
        {
            // Arrange
            var state = BaseState.Restore(BuildingTable.Default, VillageMap.Demo,
                BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, System.DateTimeOffset.UnixEpoch).ToData() with { TrialsCleared = 4 });

            // Act
            var sheet = FeatureSheetPresenter.PilgrimsWay(state.Snapshot(System.DateTimeOffset.UnixEpoch));

            // Assert
            Assert.AreEqual("Stage 5 · a boss leads it", sheet.Rows[0].Text);
            Assert.AreEqual("trial:begin", sheet.Rows[1].Action);
            Assert.IsTrue(sheet.Rows.Any(r => r.Action == "trial:alms"));
        }

        [Test]
        public void AwaySummary_ThreeHoursLater_WhatTheStoresGainedAndWhatFinished()
        {
            // Arrange — remembered at noon; back at three with more Timber and a lean-to built.
            var t0 = new System.DateTimeOffset(2026, 10, 20, 12, 0, 0, System.TimeSpan.Zero);
            var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, t0);
            state.Apply(new PlaceBuilding(System.Guid.NewGuid(), BuildingType.Woodcutter, new CultOfComfort.Simulation.Grid.GridPosition(15, 5)), t0);
            var remembered = AwaySummary.Remember(state.Snapshot(t0));
            var later = state.Snapshot(t0.AddHours(3));

            // Act
            var sheet = AwaySummary.Welcome(remembered, later, BuildingTable.Default);

            // Assert
            Assert.IsNotNull(sheet);
            StringAssert.Contains("Away 3h 00m", sheet.Footer);
            StringAssert.Contains("Timber", sheet.Rows[1].Text);
            Assert.IsTrue(sheet.Rows.Any(r => r.Text == "The Woodcutter's Lean-to is built."));
        }

        [Test]
        public void AwaySummary_TenMinutes_Nothing()
        {
            // Arrange
            var t0 = new System.DateTimeOffset(2026, 10, 20, 12, 0, 0, System.TimeSpan.Zero);
            var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, t0);

            // Act & Assert
            Assert.IsNull(AwaySummary.Welcome(AwaySummary.Remember(state.Snapshot(t0)), state.Snapshot(t0.AddMinutes(10)), BuildingTable.Default));
            Assert.IsNull(AwaySummary.Welcome("garbage", state.Snapshot(t0.AddHours(3)), BuildingTable.Default));
        }
    }
}
