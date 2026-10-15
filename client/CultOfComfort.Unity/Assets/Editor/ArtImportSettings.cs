using UnityEditor;
using UnityEngine;

namespace CultOfComfort.Editor
{
    /// <summary>Import rules for the Blender pipeline's output (art/build.sh). Keeps palette swatches crisp and models lean.</summary>
    internal sealed class ArtImportSettings : AssetPostprocessor
    {
        private const string ArtRoot = "Assets/Resources/Art/";
        private const string UiRoot = "Assets/Resources/UI/";

        /// <summary>The world map merges each chunk's trees and rocks into one mesh, so their vertices must be readable.</summary>
        private static readonly string[] LandProps =
        {
            "pine", "oak", "birch", "crag", "mountain", "cottage", "cottage_l2", "haycart", "well", "world_market", "world_abbey", "world_keep", "world_cathedral",
        };

        public override uint GetVersion() => 3;

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
            importer.isReadable = System.Array.IndexOf(LandProps, System.IO.Path.GetFileNameWithoutExtension(assetPath)) >= 0;
        }
    }
}
