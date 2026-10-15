using System;
using CultOfComfort.Simulation.Base;
using NUnit.Framework;

namespace CultOfComfort.Client.Tests
{
    public class DailyNoteTests
    {
        private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 10, 15, 10, 0, 0, TimeSpan.Zero);

        private static DailyView View(int towels, bool thermosReady = false, bool thermosClaimed = false, int streak = 0, params DailyErrandView[] errands) =>
            new DailyView(1, errands, thermosReady, thermosClaimed, streak, towels, Array.Empty<string>(), Now.AddHours(18));

        [Test]
        public void From_NoList_IsNull()
        {
            Assert.IsNull(DailyNote.From(null, Now));
        }

        [Test]
        public void From_ADoneErrand_CanBeClaimedAndTheCountedOneShowsProgress()
        {
            // Act
            var note = DailyNote.From(View(0, false, false, 0,
                new DailyErrandView(0, "preach", DailyGoal.PreachHours, "Send preachers out for 4 hours in all.", 2, 4, false, new ResourceBag(0, 0, 0, 25)),
                new DailyErrandView(1, "upgrade", DailyGoal.Upgrade, "Upgrade a building.", 1, 1, false, new ResourceBag(150, 0, 0, 0))), Now);

            // Assert
            Assert.AreEqual("2/4", note.Lines[0].Progress);
            Assert.IsFalse(note.Lines[0].CanClaim);
            Assert.IsTrue(note.Lines[1].CanClaim);
            StringAssert.Contains("18h", note.Footer);
        }

        [Test]
        public void From_TwoTowels_UnfinishedErrandsCanBeSwapped()
        {
            // Act
            var note = DailyNote.From(View(2, false, false, 0,
                new DailyErrandView(0, "trade", DailyGoal.Trade, "Strike a deal at the Market.", 0, 1, false, default)), Now);

            // Assert
            Assert.IsTrue(note.Lines[0].CanSwap);
            StringAssert.Contains("2 Towels", note.Heading);
        }

        [Test]
        public void From_AllClaimed_TheThermosIsReady()
        {
            // Act
            var note = DailyNote.From(View(0, thermosReady: true, streak: 3,
                errands: new DailyErrandView(0, "trade", DailyGoal.Trade, "Strike a deal at the Market.", 1, 1, true, default)), Now);

            // Assert
            Assert.IsTrue(note.ThermosReady);
            StringAssert.Contains("3-day streak", note.Heading);
        }
    }
}
