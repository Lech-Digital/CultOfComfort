using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CultOfComfort.Client.Tests
{
    public class GameSmokeTests
    {
        [TearDown]
        public void TearDown() => Time.timeScale = 1f;

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

            // Assert
            Assert.AreEqual(GamePhase.Dawn, game.Phase, "The Night should finish.");
            Assert.AreEqual(true, game.LastNightVerified, "Server-style replay of the input log should match the live result.");
        }
    }
}
