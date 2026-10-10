using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace CultOfComfort.Client.Tests
{
    public class GestureRecognizerTests
    {
        private const float Slop = 10f;
        private static readonly Vector2[] None = new Vector2[0];

        private static bool NeverOverUi(Vector2 _) => false;

        private static GestureFrame Feed(GestureRecognizer recognizer, params Vector2[] pointers) =>
            recognizer.Update(new List<Vector2>(pointers), NeverOverUi);

        [Test]
        public void Update_PressAndReleaseWithinSlop_ReportsTapAtReleasePosition()
        {
            // Arrange
            var recognizer = new GestureRecognizer(Slop);
            Feed(recognizer, new Vector2(100, 100));
            Feed(recognizer, new Vector2(104, 103));

            // Act
            var release = Feed(recognizer, None);

            // Assert
            Assert.IsTrue(release.Tapped);
            Assert.AreEqual(new Vector2(104, 103), release.TapPosition);
        }

        [Test]
        public void Update_DragBeyondSlop_PansAndDoesNotTap()
        {
            // Arrange
            var recognizer = new GestureRecognizer(Slop);
            Feed(recognizer, new Vector2(100, 100));

            // Act
            var drag = Feed(recognizer, new Vector2(150, 100));
            var release = Feed(recognizer, None);

            // Assert
            Assert.IsTrue(drag.Panned);
            Assert.AreEqual(new Vector2(100, 100), drag.PanFrom);
            Assert.AreEqual(new Vector2(150, 100), drag.PanTo);
            Assert.IsFalse(release.Tapped);
        }

        [Test]
        public void Update_TwoFingersSpreading_ReportsPinchRatioAndNoTap()
        {
            // Arrange
            var recognizer = new GestureRecognizer(Slop);
            Feed(recognizer, new Vector2(100, 100));
            Feed(recognizer, new Vector2(100, 100), new Vector2(200, 100));

            // Act
            var pinch = Feed(recognizer, new Vector2(50, 100), new Vector2(250, 100));
            Feed(recognizer, new Vector2(50, 100));
            var release = Feed(recognizer, None);

            // Assert
            Assert.IsTrue(pinch.Pinched);
            Assert.AreEqual(2f, pinch.PinchRatio, 0.0001f);
            Assert.AreEqual(new Vector2(150, 100), pinch.PinchCenter);
            Assert.IsFalse(release.Tapped);
        }

        [Test]
        public void Update_SecondFingerLifted_DoesNotJumpCamera()
        {
            // Arrange — pinch centre at (150,100); remaining finger is at (50,100).
            var recognizer = new GestureRecognizer(Slop);
            Feed(recognizer, new Vector2(100, 100));
            Feed(recognizer, new Vector2(100, 100), new Vector2(200, 100));

            // Act
            var transition = Feed(recognizer, new Vector2(100, 100));

            // Assert
            Assert.IsFalse(transition.Panned, "Going from two fingers to one must re-anchor, not pan from the old centre.");
        }

        [Test]
        public void Update_GestureStartingOverUi_IsIgnoredEntirely()
        {
            // Arrange
            var recognizer = new GestureRecognizer(Slop);
            var overUi = new List<Vector2> { new Vector2(10, 10) };
            recognizer.Update(overUi, _ => true);

            // Act
            var drag = recognizer.Update(new List<Vector2> { new Vector2(300, 300) }, _ => false);
            var release = recognizer.Update(new List<Vector2>(), _ => false);

            // Assert
            Assert.IsFalse(drag.Panned);
            Assert.IsFalse(release.Tapped);
        }

        [Test]
        public void Update_DragCrossesSlopAfterWobble_PansFromPressPosition()
        {
            // Arrange — finger wobbles within the slop first, then drags.
            var recognizer = new GestureRecognizer(Slop);
            Feed(recognizer, new Vector2(100, 100));
            Feed(recognizer, new Vector2(106, 100));

            // Act
            var drag = Feed(recognizer, new Vector2(130, 100));

            // Assert
            Assert.IsTrue(drag.Panned);
            Assert.AreEqual(new Vector2(100, 100), drag.PanFrom, "The board must catch up with the slop so it stays under the finger.");
            Assert.AreEqual(new Vector2(130, 100), drag.PanTo);
        }

        [Test]
        public void TryGetPendingTap_FingerDownWithinSlop_ReportsFingerPosition()
        {
            // Arrange
            var recognizer = new GestureRecognizer(Slop);
            Feed(recognizer, new Vector2(100, 100));
            Feed(recognizer, new Vector2(103, 101));

            // Act
            var pending = recognizer.TryGetPendingTap(out var position);

            // Assert
            Assert.IsTrue(pending);
            Assert.AreEqual(new Vector2(103, 101), position);
        }

        [Test]
        public void TryGetPendingTap_AfterDragOrSecondFinger_ReportsNothing()
        {
            // Arrange
            var dragged = new GestureRecognizer(Slop);
            Feed(dragged, new Vector2(100, 100));
            Feed(dragged, new Vector2(100, 100));
            Feed(dragged, new Vector2(100, 160));
            var pinched = new GestureRecognizer(Slop);
            Feed(pinched, new Vector2(100, 100));
            Feed(pinched, new Vector2(100, 100), new Vector2(200, 100));

            // Act & Assert
            Assert.IsFalse(dragged.TryGetPendingTap(out _));
            Assert.IsFalse(pinched.TryGetPendingTap(out _));
        }
    }
}
