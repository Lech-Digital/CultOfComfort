using NUnit.Framework;

namespace CultOfComfort.Client.Tests
{
    public class RaiderStatusTests
    {
        private readonly string[] _icons = new string[2];

        [Test]
        public void Select_NothingGoingOn_ShowsNoIcons()
        {
            // Act
            var count = RaiderStatus.Select(false, false, false, false, _icons);

            // Assert
            Assert.AreEqual(0, count);
        }

        [Test]
        public void Select_EverythingAtOnce_ShowsTheTwoMostImportantInOrder()
        {
            // Act
            var count = RaiderStatus.Select(true, true, true, true, _icons);

            // Assert
            Assert.AreEqual(2, count);
            Assert.AreEqual("mud", _icons[0]);
            Assert.AreEqual("speed", _icons[1]);
        }

        [Test]
        public void Select_FrightenedAndFighting_ShowsVoiceThenSwords()
        {
            // Act
            var count = RaiderStatus.Select(false, false, true, true, _icons);

            // Assert
            Assert.AreEqual(2, count);
            Assert.AreEqual("voice", _icons[0]);
            Assert.AreEqual("swords", _icons[1]);
        }
    }
}
