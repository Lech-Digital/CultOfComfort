using CultOfComfort.Simulation.Base;
using NUnit.Framework;

namespace CultOfComfort.Client.Tests
{
    public class ErrandNoteTests
    {
        private static ErrandView View(ErrandGoal goal, BuildingType? type, int progress, int target, bool done, ResourceBag reward = default) =>
            new ErrandView(2, 13, "id", "Build a Cottage. Seekers need somewhere to sleep.", progress, target, done, reward, goal, type);

        [Test]
        public void From_NoErrand_ShowsNoNote()
        {
            // Act & Assert
            Assert.IsNull(ErrandNote.From(null));
        }

        [Test]
        public void From_BuildErrandNotDone_PointsAtItsBuildCard()
        {
            // Act
            var note = ErrandNote.From(View(ErrandGoal.Build, BuildingType.Cottage, 0, 1, false, new ResourceBag(60, 0, 0, 0)));

            // Assert
            Assert.AreEqual("Errand 3 of 13", note.Heading);
            Assert.AreEqual("Build a Cottage. Seekers need somewhere to sleep.", note.Text);
            Assert.AreEqual("+60 Timber", note.Reward);
            Assert.IsNull(note.Progress, "a single thing to do needs no counter");
            Assert.IsFalse(note.Done);
            Assert.AreEqual(ErrandHint.BuildCard, note.Hint);
            Assert.AreEqual(BuildingType.Cottage, note.Type);
        }

        [Test]
        public void From_CountedErrand_ShowsProgressAndMixedReward()
        {
            // Act
            var note = ErrandNote.From(View(ErrandGoal.Build, BuildingType.Cottage, 1, 2, false, new ResourceBag(40, 40, 0, 20)));

            // Assert
            Assert.AreEqual("1/2", note.Progress);
            Assert.AreEqual("+40 Timber · +40 Peat · +20 Devotion", note.Reward);
        }

        [Test]
        public void From_DoneErrand_AsksToClaim()
        {
            // Act
            var note = ErrandNote.From(View(ErrandGoal.SurviveNight, null, 1, 1, true, new ResourceBag(100, 0, 40, 0)));

            // Assert
            Assert.IsTrue(note.Done);
            Assert.AreEqual(ErrandHint.Claim, note.Hint);
        }

        [TestCase(ErrandGoal.SurviveNight, ErrandHint.StartNight)]
        [TestCase(ErrandGoal.WinRaid, ErrandHint.StartNight)]
        [TestCase(ErrandGoal.HearthLevel, ErrandHint.Hearth)]
        [TestCase(ErrandGoal.UpgradeTo, ErrandHint.Building)]
        [TestCase(ErrandGoal.Collect, ErrandHint.Building)]
        [TestCase(ErrandGoal.BuildOnRoad, ErrandHint.BuildCard)]
        [TestCase(ErrandGoal.SendMissionary, ErrandHint.Map)]
        [TestCase(ErrandGoal.Patron, ErrandHint.Map)]
        [TestCase(ErrandGoal.Study, ErrandHint.Research)]
        public void From_OpenErrand_HintMatchesTheGoal(ErrandGoal goal, ErrandHint hint)
        {
            // Act
            var note = ErrandNote.From(View(goal, BuildingType.Woodcutter, 0, 2, false));

            // Assert
            Assert.AreEqual(hint, note.Hint);
            Assert.IsNull(goal is ErrandGoal.HearthLevel or ErrandGoal.WinRaid or ErrandGoal.UpgradeTo ? note.Progress : null,
                "levels and stars are not counts");
        }

        [Test]
        public void LampsWait_BeforeTheFirstNightErrand_True()
        {
            // Arrange
            var book = ErrandBook.Default.Errands;
            var firstNight = 0;
            while (book[firstNight].Goal != ErrandGoal.SurviveNight)
            {
                firstNight++;
            }

            // Act & Assert
            Assert.IsTrue(ErrandNote.LampsWait(new ErrandView(firstNight - 1, book.Count, "x", "x", 0, 1, false, default), book));
            Assert.IsFalse(ErrandNote.LampsWait(new ErrandView(firstNight, book.Count, "x", "x", 0, 1, false, default, ErrandGoal.SurviveNight), book));
            Assert.IsFalse(ErrandNote.LampsWait(null, book), "after the last errand the lamps are always free");
        }
    }
}
