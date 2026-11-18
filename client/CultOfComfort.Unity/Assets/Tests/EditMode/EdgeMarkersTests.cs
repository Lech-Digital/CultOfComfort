using NUnit.Framework;
using UnityEngine;

namespace CultOfComfort.Client.Tests
{
    public class EdgeMarkersTests
    {
        private static readonly Rect Clear = Rect.MinMaxRect(0f, 0.2f, 1f, 0.8f);

        [Test]
        public void Compute_EveryoneInView_NoMarkers()
        {
            // Act & Assert
            CollectionAssert.IsEmpty(EdgeMarkers.Compute(new[] { new Vector3(0.5f, 0.5f, 10f) }, Clear));
        }

        [Test]
        public void Compute_ThreeAboveTheBoard_OneMarkerAtTheTopCountingThree()
        {
            // Act
            var markers = EdgeMarkers.Compute(new[] { new Vector3(0.4f, 1.2f, 10f), new Vector3(0.5f, 1.5f, 10f), new Vector3(0.6f, 0.9f, 10f) }, Clear);

            // Assert
            Assert.AreEqual(1, markers.Count);
            Assert.AreEqual(3, markers[0].Count);
            Assert.AreEqual(0f, markers[0].AngleDegrees);
            Assert.AreEqual(0.5f, markers[0].At.x, 0.001f);
            Assert.Less(markers[0].At.y, Clear.yMax);
        }

        [Test]
        public void Compute_OneLeftOneBelow_TwoMarkers()
        {
            // Act
            var markers = EdgeMarkers.Compute(new[] { new Vector3(-0.3f, 0.5f, 10f), new Vector3(0.5f, 0.05f, 10f) }, Clear);

            // Assert
            Assert.AreEqual(2, markers.Count);
            CollectionAssert.AreEquivalent(new[] { 180f, 270f }, new[] { markers[0].AngleDegrees, markers[1].AngleDegrees });
        }
    }
}
