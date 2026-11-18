using NUnit.Framework;
using UnityEngine;

namespace CultOfComfort.Client.Tests
{
    public class OutlineTests
    {
        [Test]
        public void HullScale_OneUnitPart_GrowsByTwiceTheWidth()
        {
            // Act
            var scale = Shapes.HullScale(Vector3.one, 0.05f);

            // Assert
            Assert.AreEqual(1.1f, scale.x, 1e-4f);
            Assert.AreEqual(1.1f, scale.y, 1e-4f);
            Assert.AreEqual(1.1f, scale.z, 1e-4f);
        }

        [Test]
        public void HullScale_FlatPart_IsCapped()
        {
            // Act
            var scale = Shapes.HullScale(new Vector3(1f, 0f, 1f), 0.05f);

            // Assert
            Assert.LessOrEqual(scale.y, 1.5f, "a flat part doesn't balloon");
            Assert.AreEqual(1.1f, scale.x, 1e-4f);
        }
    }
}
