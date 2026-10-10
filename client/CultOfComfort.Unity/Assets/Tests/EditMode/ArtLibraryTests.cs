using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CultOfComfort.Client.Tests
{
    public class ArtLibraryTests
    {
        private GameObject _instance;

        [TearDown]
        public void TearDown()
        {
            if (_instance != null)
            {
                Object.DestroyImmediate(_instance);
            }
        }

        [Test]
        public void Instantiate_UnknownName_ReturnsNull()
        {
            // Act
            _instance = ArtLibrary.Instantiate("beige_of_regret", null);

            // Assert
            Assert.IsNull(_instance);
        }

        [Test]
        public void Instantiate_PipelineCube_UsesPaletteMaterialEverywhere()
        {
            // Act
            _instance = ArtLibrary.Instantiate("pipeline_cube", null);

            // Assert
            Assert.IsNotNull(_instance, "Run ./art/build.sh pipeline_cube first.");
            Assert.IsNotNull(ArtLibrary.PaletteMaterial, "Run ProjectSetup.EnsureAssets to create Palette.mat.");
            foreach (var renderer in _instance.GetComponentsInChildren<Renderer>())
            {
                Assert.AreEqual(ArtLibrary.PaletteMaterial, renderer.sharedMaterial, renderer.name);
            }

            Assert.IsNotNull(ArtLibrary.FindPart(_instance.transform, "Cube"));
            Assert.IsNull(ArtLibrary.FindPart(_instance.transform, "Umbrella"));
        }

        [Test]
        public void PaletteTexture_ImportSettings_KeepSwatchesCrisp()
        {
            // Act
            var importer = (TextureImporter)AssetImporter.GetAtPath("Assets/Resources/Art/Palette.png");

            // Assert
            Assert.AreEqual(FilterMode.Point, importer.filterMode);
            Assert.IsFalse(importer.mipmapEnabled);
            Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression);
            Assert.AreEqual(TextureImporterNPOTScale.None, importer.npotScale);
        }

        [Test]
        public void Instantiate_Prophet_IsAboutOneMetreTallAndUpright()
        {
            // Arrange
            _instance = ArtLibrary.Instantiate("prophet", null);
            Assert.IsNotNull(_instance, "Run ./art/build.sh prophet first.");
            var bounds = new Bounds(_instance.transform.position, Vector3.zero);

            // Act
            foreach (var renderer in _instance.GetComponentsInChildren<Renderer>())
            {
                bounds.Encapsulate(renderer.bounds);
            }

            // Assert
            // Renderer bounds box the tilted head's rotated local bounds, so they read a few cm tall; the Blender-side
            // test measures real vertices. This one guards against wrong scale or a model lying down.
            Assert.AreEqual(1.0f, bounds.size.y, 0.1f, "height");
            Assert.AreEqual(0f, bounds.min.y, 0.02f, "feet on the ground");
            Assert.Greater(bounds.size.y, bounds.size.x * 1.5f, "upright, not lying down");
        }

        [Test]
        public void Instantiate_Prophet_FacesPlusZ()
        {
            // Act
            _instance = ArtLibrary.Instantiate("prophet", null);

            // Assert
            Assert.Greater(ArtLibrary.FindPart(_instance.transform, "Front").position.z, 0.2f,
                "Unity moves units with LookRotation(heading), so models must face +Z.");
        }

        [Test]
        public void Instantiate_Prophet_PivotsAtFeetAndShoulders()
        {
            // Act
            _instance = ArtLibrary.Instantiate("prophet", null);
            var root = _instance.transform;

            // Assert
            Assert.AreEqual(0f, ArtLibrary.FindPart(root, "Body").position.y, 0.01f);
            Assert.AreEqual(0.56f, ArtLibrary.FindPart(root, "ArmR").position.y, 0.02f);
            Assert.AreEqual(0.35f, ArtLibrary.FindPart(root, "Umbrella").position.y, 0.02f);
            Assert.AreEqual("ArmR", ArtLibrary.FindPart(root, "Umbrella").parent.name);
        }

        [Test]
        public void PartAnimator_Swooning_TipsTheBodyBackwards()
        {
            // Arrange
            _instance = ArtLibrary.Instantiate("prophet", null);
            var animator = _instance.AddComponent<PartAnimator>();
            animator.Bind();
            var head = ArtLibrary.FindPart(_instance.transform, "Head");
            var standingHeadZ = head.position.z;

            // Act
            animator.IsSwooning = true;
            for (var i = 0; i < 60; i++)
            {
                animator.Tick(1f / 30f, i / 30f);
            }

            // Assert — he faces +Z, so falling on his back moves his head towards -Z and down near the ground.
            Assert.Less(head.position.z, standingHeadZ - 0.4f);
            Assert.Less(head.position.y, 0.35f);
        }
    }
}
