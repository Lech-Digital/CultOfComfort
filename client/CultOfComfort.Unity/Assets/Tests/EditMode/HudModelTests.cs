using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace CultOfComfort.Client.Tests
{
    public class HudModelTests
    {
        [Test]
        public void LockedFeatures_All_TheSixShortcutsWithNotes()
        {
            // Act
            var ids = LockedFeatures.All.Select(f => f.Id).ToArray();

            // Assert
            CollectionAssert.AreEquivalent(new[] { "map", "research", "pilgrims", "festival", "shop", "cult" }, ids); // eight entry points with the portrait and the strip (spec 2026-11-17 fun first, D)
            Assert.IsTrue(LockedFeatures.All.All(f => f.Note.Length > 20 && f.Label.Length > 0 && f.Icon.Length > 0));
        }

        [Test]
        public void LockedFeatures_Get_ResearchNamesItsHearthLevel()
        {
            // Act
            var research = LockedFeatures.Get("research");

            // Assert
            StringAssert.Contains("Hearth 2", research.Note);
        }

        [Test]
        public void LockedFeatures_GetUnknownId_Throws()
        {
            Assert.Throws<KeyNotFoundException>(() => LockedFeatures.Get("time_machine"));
        }

        [Test]
        public void DemoEconomy_Frame_ScrapIsRealAndTheRestIsDemo()
        {
            // Act
            var frame = DemoEconomy.Frame(scrap: 380, hearthLevel: 1);

            // Assert
            Assert.AreEqual(380, frame.Scrap.Value);
            Assert.IsFalse(frame.Scrap.IsDemo);
            Assert.IsTrue(frame.Timber.IsDemo && frame.Peat.IsDemo && frame.Devotion.IsDemo && frame.Pennies.IsDemo && frame.Comfort.IsDemo);
            Assert.IsTrue(frame.TensionIsDemo);
            Assert.AreEqual(1, frame.HearthLevel);
        }

        [Test]
        public void DemoEconomy_FrameWithNegativeScrap_ClampsToZero()
        {
            // Act
            var frame = DemoEconomy.Frame(scrap: -5, hearthLevel: 1);

            // Assert
            Assert.AreEqual(0, frame.Scrap.Value);
        }

        [Test]
        public void WaveText_NoneDrivenOff_LeavesTheTallyOut()
        {
            // Act
            var text = HudView.WaveText(1, 3, 5, 0);

            // Assert
            Assert.AreEqual("Wave 1/3 · 5 on the road", text);
        }

        [Test]
        public void WaveText_SomeDrivenOff_CountsThem()
        {
            // Act
            var text = HudView.WaveText(2, 3, 4, 7);

            // Assert
            Assert.AreEqual("Wave 2/3 · 4 on the road · 7 driven off", text);
        }
    }
}
