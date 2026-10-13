using NUnit.Framework;
using UnityEngine;

namespace CultOfComfort.Client.Tests
{
    public class NightMoodTests
    {
        private GameObject _sunObject;
        private Camera _camera;
        private Color _fogBefore;

        [SetUp]
        public void SetUp()
        {
            _fogBefore = RenderSettings.fogColor;
            _sunObject = new GameObject("Sun");
            var sun = _sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 2f;
            sun.color = Color.white;
            _camera = new GameObject("Camera").AddComponent<Camera>();
            _camera.backgroundColor = Shapes.Hex("#b8a47a");
            RenderSettings.fogColor = _camera.backgroundColor;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_sunObject);
            Object.DestroyImmediate(_camera.gameObject);
            RenderSettings.fogColor = _fogBefore;
        }

        [Test]
        public void Apply_FullNight_DimsTheSunAndTheHaze()
        {
            // Arrange
            var sun = _sunObject.GetComponent<Light>();
            var mood = NightMood.Capture(_camera, sun);

            // Act
            mood.Apply(1f);

            // Assert
            Assert.Less(sun.intensity, 1f);
            Assert.Less(_camera.backgroundColor.grayscale, 0.3f);
            Assert.AreEqual(_camera.backgroundColor, RenderSettings.fogColor);
            Assert.Greater(sun.color.b, sun.color.r, "moonlight is bluish");
        }

        [Test]
        public void Apply_BackToDay_RestoresTheDayExactly()
        {
            // Arrange
            var sun = _sunObject.GetComponent<Light>();
            var mood = NightMood.Capture(_camera, sun);
            mood.Apply(1f);

            // Act
            mood.Apply(0f);

            // Assert
            Assert.AreEqual(2f, sun.intensity);
            Assert.AreEqual(Color.white, sun.color);
            Assert.AreEqual(Shapes.Hex("#b8a47a"), _camera.backgroundColor);
        }

        [Test]
        public void Capture_NoSun_StillDarkensTheSky()
        {
            // Arrange
            var mood = NightMood.Capture(_camera, null);

            // Act
            mood.Apply(1f);

            // Assert
            Assert.Less(_camera.backgroundColor.grayscale, 0.3f);
        }
    }
}
