using System.Collections.Generic;
using CultOfComfort.Simulation.Base;
using NUnit.Framework;
using UnityEngine;

namespace CultOfComfort.Client.Tests
{
    public class BuildingArtTests
    {
        [Test]
        public void ModelName_LevelWithItsOwnRedesign_UsesIt()
        {
            // Arrange
            var drawn = new HashSet<string> { "woodcutter_l2", "woodcutter_l3" };

            // Act & Assert
            Assert.AreEqual("woodcutter_l3", BuildingArt.ModelName(BuildingType.Woodcutter, 3, drawn.Contains));
            Assert.AreEqual("woodcutter_l2", BuildingArt.ModelName(BuildingType.Woodcutter, 2, drawn.Contains));
        }

        [Test]
        public void ModelName_LevelNotDrawnYet_FallsBackToTheBestDrawnBelow()
        {
            // Arrange
            var drawn = new HashSet<string> { "cottage_l2" };

            // Act & Assert
            Assert.AreEqual("cottage_l2", BuildingArt.ModelName(BuildingType.Cottage, 3, drawn.Contains));
            Assert.AreEqual("storage_hut", BuildingArt.ModelName(BuildingType.StorageHut, 3, drawn.Contains));
        }

        [Test]
        public void ModelName_ShellOrFirstLevelOrNoArt_UsesTheBaseModelOrNothing()
        {
            // Act & Assert
            Assert.AreEqual("cistern", BuildingArt.ModelName(BuildingType.Cistern, 0, _ => true));
            Assert.AreEqual("cistern", BuildingArt.ModelName(BuildingType.Cistern, 1, _ => true));
            Assert.IsNull(BuildingArt.ModelName(BuildingType.Hearth, 2, _ => true));
        }

        [TestCase(BuildingType.Woodcutter, 3)]
        [TestCase(BuildingType.PeatCut, 3)]
        [TestCase(BuildingType.StorageHut, 3)]
        [TestCase(BuildingType.CultistHut, 3)]
        [TestCase(BuildingType.Cistern, 2)]
        [TestCase(BuildingType.Cottage, 2)]
        public void ModelName_EveryUpgradeLevel_HasItsOwnRedesign(BuildingType type, int maxLevel)
        {
            // Assert
            Assert.AreEqual(maxLevel, BuildingTable.Default.Spec(type).MaxLevel, "a level was added: draw it (art/blender/models)");
            for (var level = 2; level <= maxLevel; level++)
            {
                Assert.AreEqual($"{BuildingArt.BaseModel(type)}_l{level}", BuildingArt.ModelName(type, level, ArtLibrary.Exists),
                    "Run ./art/build.sh for the missing model.");
            }
        }

        [Test]
        public void Scale_SmallModelOnABigPlot_GrowsToTheTileRate()
        {
            // Act
            var scale = BuildingArt.Scale(new Vector2(2f, 2f), new Vector2(1f, 1f), finished: true);

            // Assert
            Assert.AreEqual(BuildingArt.ScalePerTile * 2f, scale, 1e-4f);
        }

        [Test]
        public void Scale_WideModel_NeverSpillsOntoTheNeighbours()
        {
            // Act
            var scale = BuildingArt.Scale(new Vector2(2f, 1f), new Vector2(2.5f, 0.5f), finished: true);

            // Assert
            Assert.AreEqual(2f / 2.5f, scale, 1e-4f, "2.5 model units must fit 2 tiles");
        }

        [Test]
        public void Scale_Unfinished_IsAShell()
        {
            // Act
            var scale = BuildingArt.Scale(new Vector2(2f, 2f), new Vector2(1f, 1f), finished: false);

            // Assert
            Assert.AreEqual(BuildingArt.ShellScalePerTile * 2f, scale, 1e-4f);
        }

        [Test]
        public void Shape_Pop_StartsSquatOvershootsAndSettles()
        {
            // Act
            var start = UpgradePop.Shape(0f);
            var end = UpgradePop.Shape(1f);
            var tallest = 0f;
            for (var t = 0f; t <= 1f; t += 0.02f)
            {
                tallest = Mathf.Max(tallest, UpgradePop.Shape(t).y);
            }

            // Assert
            Assert.Less(start.y, 0.7f, "it springs up out of the ground");
            Assert.Greater(start.x, 1f, "squat and wide");
            Assert.Greater(tallest, 1.05f, "overshoots");
            Assert.AreEqual(1f, end.x, 1e-4f);
            Assert.AreEqual(1f, end.y, 1e-4f);
        }
    }
}
