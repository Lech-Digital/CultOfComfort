using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace CultOfComfort.Client.Tests
{
    public class IntroTests
    {
        private FakeProgress _progress;

        [SetUp]
        public void SetUp()
        {
            TestVillage.Use();
            _progress = new FakeProgress();
            VillageComposition.IntroOverride = () => _progress;
        }

        [TearDown]
        public void TearDown()
        {
            VillageComposition.IntroOverride = null;
            TestVillage.Reset();
        }

        [UnityTest]
        public IEnumerator FirstLaunch_IntroPlays_HudWaitsAndCameraIsBorrowed()
        {
            // Arrange & Act
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return null;
            yield return null;

            // Assert
            var game = Object.FindAnyObjectByType<GameController>();
            Assert.IsNotNull(game.Intro, "a device that never saw the intro plays it");
            Assert.AreEqual(IntroScript.Garage, game.Intro.CurrentShot);
            Assert.AreEqual(DisplayStyle.None, HudRoot(game).style.display.value);
            Assert.Greater(Camera.main.transform.position.x, IntroDirector.StageOrigin.x - 20f, "the camera is on the intro stage");
            var overlay = GameObject.Find("Intro Overlay").GetComponent<UIDocument>().rootVisualElement;
            Assert.IsNotNull(overlay.Q(className: "intro-caption"));
            Assert.AreEqual(DisplayStyle.Flex, overlay.resolvedStyle.display, "the captions show while the HUD waits");
            Assert.Greater(overlay.Q(className: "intro-note").worldBound.height, 10f, "the caption note is laid out on screen");
        }

        [UnityTest]
        public IEnumerator Taps_ThroughEveryShot_EndInTheVillageAndRememberIt()
        {
            // Arrange
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return null;
            var game = Object.FindAnyObjectByType<GameController>();
            var finished = false;
            game.IntroFinished += () => finished = true;

            // Act: every shot gets a few frames, then a tap.
            var seen = 0;
            while (game.Intro != null && seen < 20)
            {
                for (var f = 0; f < 5; f++)
                {
                    yield return null;
                }

                game.Intro?.Next();
                seen++;
            }

            yield return null;

            // Assert
            Assert.IsTrue(finished);
            Assert.AreEqual(IntroScript.Shots.Count, seen, "one tap per shot");
            Assert.IsTrue(_progress.Seen);
            Assert.AreEqual(DisplayStyle.Flex, HudRoot(game).style.display.value);
            Assert.Less(Camera.main.transform.position.x, IntroDirector.StageOrigin.x - 100f, "the camera is back over the village");
            Assert.IsNull(GameObject.Find("Intro Stage"), "the stage is struck");
            var until = Time.realtimeSinceStartup + 3f;
            while (GameObject.Find("Intro Overlay") != null && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            Assert.IsNull(GameObject.Find("Intro Overlay"), "the overlay fades out and goes");
        }

        [UnityTest]
        public IEnumerator Skip_MidShot_EndsAtOnce()
        {
            // Arrange
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return null;
            var game = Object.FindAnyObjectByType<GameController>();
            game.Intro.Next();
            game.Intro.Next();
            yield return null;

            // Act
            game.Intro.Skip();
            yield return null;

            // Assert
            Assert.IsNull(game.Intro);
            Assert.IsTrue(_progress.Seen);
        }

        [UnityTest]
        public IEnumerator SecondLaunch_IntroSeen_GoesStraightToTheVillage()
        {
            // Arrange
            _progress.MarkSeen();

            // Act
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return null;

            // Assert
            var game = Object.FindAnyObjectByType<GameController>();
            Assert.IsNull(game.Intro);
        }

        private static VisualElement HudRoot(GameController game) => game.GetComponent<UIDocument>().rootVisualElement;

        private sealed class FakeProgress : IIntroProgress
        {
            public bool Seen { get; private set; }

            public void MarkSeen() => Seen = true;
        }
    }
}
