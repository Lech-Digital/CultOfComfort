using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using UnityEngine.TestTools;

namespace CultOfComfort.Client.Tests
{
    public class GameSmokeTests
    {
        [SetUp]
        public void SetUp() => TestVillage.Use();

        [TearDown]
        public void TearDown()
        {
            TestVillage.Reset();
            Time.timeScale = 1f;
            Time.captureDeltaTime = 0f;
        }

        [UnityTest]
        public IEnumerator Bootstrap_SceneLoaded_CreatesGameInDusk()
        {
            // Arrange & Act
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return null;

            // Assert
            var game = Object.FindAnyObjectByType<GameController>();
            Assert.IsNotNull(game, "GameBootstrap should create a GameController after the scene loads.");
            Assert.AreEqual(GamePhase.Dusk, game.Phase);
            Assert.AreEqual(true, DeterminismProbe.Matches, $"Probe hash {DeterminismProbe.LastHash} should equal the .NET hash {DeterminismProbe.ExpectedHash}.");
        }

        [UnityTest]
        public IEnumerator StartNight_UndefendedBase_PlaysToDawnWithVerifiedReplay()
        {
            // Arrange
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return null;
            var game = Object.FindAnyObjectByType<GameController>();
            Time.timeScale = 20f;

            // Act
            game.StartNight();
            var timeout = Time.realtimeSinceStartup + 120f;
            while (game.Phase != GamePhase.Dawn && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            // The replays run on worker threads: the verdict lands a few frames into Dawn.
            while (game.LastNightVerified == null && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            // Assert
            Assert.AreEqual(GamePhase.Dawn, game.Phase, "The Night should finish.");
            Assert.AreEqual(true, game.LastNightVerified, "Server-style replay of the input log should match the live result.");
            yield return null;
            var root = Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            Assert.AreEqual(DisplayStyle.Flex, root.Q("stamp").resolvedStyle.display, "Dawn shows the verdict stamp.");

            // Back to Dusk restores the frame (Review Focus 4).
            game.GetType().GetMethod("BackToDusk", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(game, null);
            yield return null;
            yield return null;
            Assert.AreEqual(GamePhase.Dusk, game.Phase);
            Assert.AreEqual(DisplayStyle.Flex, root.Q("tension-strip").resolvedStyle.display);
            Assert.AreEqual(DisplayStyle.Flex, root.Q("side-left").resolvedStyle.display);
        }

        [UnityTest]
        public IEnumerator CameraShake_NightLostOnAHearthHit_SettlesAtDawn()
        {
            // Arrange: an undefended base loses, and the final Hearth hit shakes the camera on the last tick.
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return null;
            var game = Object.FindAnyObjectByType<GameController>();
            var battleView = Object.FindAnyObjectByType<BattleView>();
            Time.captureDeltaTime = 1f / 60f; // phone-sized frames, as fast as the editor can run them
            game.StartNight();
            var timeout = Time.realtimeSinceStartup + 120f;
            while (game.Phase != GamePhase.Dawn && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            // Act: let a few game seconds pass on the Dawn screen.
            var settle = Time.time + 3f;
            while (Time.time < settle)
            {
                yield return null;
            }

            // Assert
            Assert.AreEqual(GamePhase.Dawn, game.Phase, "The Night should finish.");
            Assert.AreEqual(0f, battleView.CameraShake, "The camera must stop shaking once the Night is over.");
        }

        [UnityTest]
        public IEnumerator Hud_DuskThenNight_SwapsBuildBarForSkillBar()
        {
            // Arrange
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return null;
            var game = Object.FindAnyObjectByType<GameController>();
            var root = Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            yield return null;
            var startNight = root.Q<Button>("start-night");
            var sluice = root.Q<Button>("sluice");
            var duskVisible = startNight.resolvedStyle.display == DisplayStyle.Flex && startNight.worldBound.width > 0f;

            // Act
            game.StartNight();
            yield return null;
            yield return null;

            // Assert
            Assert.IsTrue(duskVisible, "Dusk should show the Light the Lamps button.");
            Assert.AreEqual(DisplayStyle.None, root.Q("dusk-bar").resolvedStyle.display, "The build bar hides at night.");
            Assert.AreEqual(DisplayStyle.Flex, root.Q("night-bar").resolvedStyle.display, "The skill bar shows at night.");
            StringAssert.StartsWith("Sluice", sluice.Q<Label>(className: "skill-text").text);
        }
    }
}
