using NUnit.Framework;

namespace CultOfComfort.Client.Tests
{
    public class RelicNewsTests
    {
        [Test]
        public void Found_HearthTwo_LaserAndTorch()
        {
            // Act
            var news = RelicNews.Found(1, 2);

            // Assert
            StringAssert.Contains("laser pointer", news);
            StringAssert.Contains("dynamo torch", news);
        }

        [Test]
        public void Found_HearthThree_TheMagnifierOnly()
        {
            // Act
            var news = RelicNews.Found(2, 3);

            // Assert
            StringAssert.Contains("magnifying glass", news);
            StringAssert.DoesNotContain("laser", news);
            StringAssert.Contains("it's a weapon", news);
        }

        [Test]
        public void Found_LoadingOrNothingNew_Silent()
        {
            // Act & Assert
            Assert.IsNull(RelicNews.Found(0, 3), "loading a village is not news");
            Assert.IsNull(RelicNews.Found(2, 2));
        }
    }
}
