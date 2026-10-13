using System.Linq;
using NUnit.Framework;

namespace CultOfComfort.Client.Tests
{
    public class IntroScriptTests
    {
        [Test]
        public void Shots_TheWholeStory_RunsAboutFortySeconds()
        {
            // Act
            var total = IntroScript.TotalSeconds;

            // Assert
            Assert.That(total, Is.InRange(30f, 50f), "short enough that nobody reaches for Skip out of boredom");
        }

        [Test]
        public void Shots_EveryShot_HasACaptionAndAUniqueId()
        {
            // Act
            var shots = IntroScript.Shots;

            // Assert
            Assert.IsTrue(shots.All(s => !string.IsNullOrWhiteSpace(s.Caption) && s.Seconds > 0f));
            Assert.AreEqual(shots.Count, shots.Select(s => s.Id).Distinct().Count());
        }

        [Test]
        public void Shots_Order_GarageThenMarchlandsThenTitle()
        {
            // Act
            var sets = IntroScript.Shots.Select(s => s.Set).ToList();

            // Assert
            Assert.AreEqual(IntroSet.Garage, sets.First());
            Assert.AreEqual(IntroSet.Title, sets.Last());
            Assert.AreEqual(sets.OrderBy(s => s).ToList(), sets, "the story never cuts back to 2024");
            Assert.AreEqual(IntroScript.Jump, IntroScript.Shots.Last(s => s.Set == IntroSet.Garage).Id, "2024 ends with the jump");
        }

        [Test]
        public void BootRelics_EveryRelic_HasAModel()
        {
            // Assert
            foreach (var name in IntroScript.BootRelics.Concat(IntroScript.Locals).Concat(new[] { "time_machine", "hearth_l1", "prophet" }))
            {
                Assert.IsTrue(ArtLibrary.Exists(name), $"Run ./art/build.sh {name}.");
            }
        }
    }
}
