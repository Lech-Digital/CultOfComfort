using UnityEditor;
using UnityEngine;

namespace CultOfComfort.Editor
{
    /// <summary>Import rules for the Blender pipeline's output (art/build.sh). Keeps palette swatches crisp and models lean.</summary>
    internal sealed class ArtImportSettings : AssetPostprocessor
    {
        private const string ArtRoot = "Assets/Resources/Art/";
        private const string UiRoot = "Assets/Resources/UI/";

        private void OnPreprocessTexture()
        {
            if (assetPath.StartsWith(UiRoot))
            {
                var ui = (TextureImporter)assetImporter;
                ui.textureType = TextureImporterType.Default;
                ui.filterMode = FilterMode.Bilinear;               // parchment and icons are painterly, not swatches
                ui.mipmapEnabled = false;                          // UI is drawn at ~1:1
                ui.textureCompression = TextureImporterCompression.Uncompressed;
                ui.alphaIsTransparency = true;
                ui.npotScale = TextureImporterNPOTScale.None;
                ui.wrapMode = TextureWrapMode.Clamp;
                ui.sRGBTexture = true;
                return;
            }

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
            importer.bakeAxisConversion = true; // Blender Z-up → Unity Y-up baked into every node, children included
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importBlendShapes = false;
            importer.isReadable = false;
        }
    }
}
