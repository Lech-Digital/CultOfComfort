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
    }
}
