using NUnit.Framework;
using UnityEngine;

namespace CultOfComfort.Client.Tests
{
    public class CameraRigTests
    {
        private static readonly Vector2 OpenMin = new Vector2(0f, -19f);
        private static readonly Vector2 OpenMax = new Vector2(29f, 0f);
        private static readonly Vector2 RoamMin = new Vector2(-6f, -38f);
        private static readonly Vector2 RoamMax = new Vector2(54f, 6f);
        private static readonly Rect ClearPortrait = Rect.MinMaxRect(0.03f, 0.25f, 0.97f, 0.85f);

        private GameObject _holder;
        private Camera _camera;

        [SetUp]
        public void SetUp()
        {
            _holder = new GameObject("Test Camera");
            _camera = _holder.AddComponent<Camera>();
            _camera.fieldOfView = 44f;
            _camera.aspect = 1080f / 2424f;
            _camera.pixelRect = new Rect(0, 0, 1080, 2424);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_holder);

        private CameraRig FramedRig()
        {
            var rig = new CameraRig(_camera, new Vector3(0f, 16.5f, -10.4f), OpenMin, OpenMax);
            rig.SetRoam(RoamMin, RoamMax);
            rig.Frame(-90f, ClearPortrait);
            rig.Reset();
            return rig;
        }

        private Vector2 CentreOfScreen => new Vector2(_camera.pixelWidth / 2f, _camera.pixelHeight / 2f);

        [Test]
        public void Zoom_AllTheWayOut_ShowsTheWholeValleyWithTheRiver()
        {
            // Arrange
            var rig = FramedRig();

            // Act
            rig.Zoom(0.01f, CentreOfScreen);

            // Assert — the whole map, the forest edge and the river fit on screen at once.
            foreach (var corner in new[] { new Vector3(RoamMin.x, 0f, RoamMin.y), new Vector3(RoamMax.x, 0f, RoamMax.y), new Vector3(RoamMin.x, 0f, RoamMax.y), new Vector3(RoamMax.x, 0f, RoamMin.y) })
            {
                var viewport = _camera.WorldToViewportPoint(corner);
                Assert.That(viewport.x, Is.InRange(-0.05f, 1.05f), $"{corner} is off screen");
                Assert.That(viewport.y, Is.InRange(-0.05f, 1.05f), $"{corner} is off screen");
            }
        }

        [Test]
        public void Pan_PastTheOpenLand_ReachesTheRiverButNoFurther()
        {
            // Arrange
            var rig = FramedRig();
            Assert.IsTrue(rig.TryScreenToGround(CentreOfScreen, out var before));

            // Act — drag a long way, many times, towards the far corner.
            for (var i = 0; i < 60; i++)
            {
                rig.Pan(CentreOfScreen, CentreOfScreen + new Vector2(-400f, 400f));
                rig.Pan(CentreOfScreen, CentreOfScreen + new Vector2(400f, 400f));
            }

            // Assert
            Assert.IsTrue(rig.TryScreenToGround(CentreOfScreen, out var after));
            var leftTheOpenLand = after.x > OpenMax.x + 1f || after.z < OpenMin.y - 1f || after.x < OpenMin.x - 1f || after.z > OpenMax.y + 1f;
            Assert.IsTrue(leftTheOpenLand, $"stuck on the open land at {after} (started at {before})");
            Assert.That(after.x, Is.InRange(RoamMin.x - 0.01f, RoamMax.x + 0.01f));
            Assert.That(after.z, Is.InRange(RoamMin.y - 0.01f, RoamMax.y + 0.01f));
        }

        [Test]
        public void Frame_HomeView_StillFitsTheOpenLandNotTheWholeValley()
        {
            // Arrange & Act
            FramedRig();

            // Assert — the far corner of the valley is off screen in the home view; the open land is on it.
            var open = _camera.WorldToViewportPoint(new Vector3(OpenMax.x, 0f, OpenMin.y));
            var valley = _camera.WorldToViewportPoint(new Vector3(RoamMax.x, 0f, RoamMin.y));
            Assert.That(open.x, Is.InRange(0f, 1f));
            Assert.That(open.y, Is.InRange(0f, 1f));
            Assert.IsFalse(valley.x is >= 0f and <= 1f && valley.y is >= 0f and <= 1f, "home view should stay on the village");
        }
    }
}
