using NUnit.Framework;
using UnityEngine;

namespace CultOfComfort.Client.Tests
{
    public class CameraLeanTests
    {
        private static readonly Rect Clear = Rect.MinMaxRect(0f, 0.15f, 1f, 0.75f);

        [Test]
        public void LeanShift_RoadAboveAndSpareHeight_MovesTheBoardDown()
        {
            // Arrange — a wide board filling the width, with spare height; the road's entrance is above it.
            var board = new Rect(0f, 0.35f, 1f, 0.2f);

            // Act
            var shift = CameraRig.LeanShift(board, Clear, new Vector2(0.6f, 0.9f));

            // Assert
            Assert.AreEqual(0f, shift.x, 1e-4f, "no spare width");
            Assert.AreEqual(-0.2f, shift.y, 1e-4f, "half the spare height (0.4), away from the road");
        }

        [Test]
        public void LeanShift_NoSpareRoom_IsZero()
        {
            // Act
            var shift = CameraRig.LeanShift(Clear, Clear, new Vector2(0.5f, 1f));

            // Assert
            Assert.AreEqual(Vector2.zero, shift);
        }
    }
}
