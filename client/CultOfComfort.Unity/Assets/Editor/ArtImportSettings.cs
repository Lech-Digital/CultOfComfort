using UnityEditor;
using UnityEngine;

namespace CultOfComfort.Editor
{
    /// <summary>Import rules for the Blender pipeline's output (art/build.sh). Keeps palette swatches crisp and models lean.</summary>
    internal sealed class ArtImportSettings : AssetPostprocessor
    {
        private const string ArtRoot = "Assets/Resources/Art/";

        private void OnPreprocessTexture()
        {
            if (assetPath != ArtRoot + "Palette.png")
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.filterMode = FilterMode.Point;          // no blending between neighbouring swatches
            importer.mipmapEnabled = false;                  // mips would average swatches together at distance
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.sRGBTexture = true;
        }

        private void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(ArtRoot + "Models/"))
            {
                return;
            }

            var importer = (ModelImporter)assetImporter;
            importer.materialImportMode = ModelImporterMaterialImportMode.None; // ArtLibrary assigns Palette.mat
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importBlendShapes = false;
            importer.isReadable = false;
        }
    }
}
