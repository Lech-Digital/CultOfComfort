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
    }
}
