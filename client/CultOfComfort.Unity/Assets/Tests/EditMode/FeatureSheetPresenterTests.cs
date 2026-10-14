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
        public void Research_HearthTwo_MudEraStudiesHaveButtonsComfortEraWaits()
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
            Assert.IsNull(plumbing.Button, "the Comfort Era opens at Hearth 3");
            Assert.IsTrue(sheet.Rows.Any(r => r.Heading));
        }

        [Test]
        public void Research_Studying_ShowsTimeLeftAndNoButton()
        {
            // Arrange
            var (state, now) = Village(2);
            state.Apply(new StartResearch(Guid.NewGuid(), "sermon-notes"), now);

            // Act
            var sheet = FeatureSheetPresenter.Research(state.Snapshot(now.AddMinutes(30)), now.AddMinutes(30));

            // Assert
            var notes = sheet.Rows.Single(r => r.Action == "research:sermon-notes");
            Assert.IsNull(notes.Button);
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
    }
}
