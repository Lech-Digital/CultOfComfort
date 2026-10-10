using System.IO;
using UnityEditor;
using UnityEngine;

namespace CultOfComfort.Editor
{
    /// <summary>Creates assets the code-first client depends on. Idempotent; also runs before every build.</summary>
    public static class ProjectSetup
    {
        public const string BaseMaterialPath = "Assets/Resources/BaseLit.mat";
        public const string PaletteTexturePath = "Assets/Resources/Art/Palette.png";
        public const string PaletteMaterialPath = "Assets/Resources/Art/Materials/Palette.mat";

        [MenuItem("Cult of Comfort/Ensure Project Assets")]
        public static void EnsureAssets()
        {
            EnsureBaseMaterial();
            EnsurePaletteMaterial();
        }

        private static void EnsureBaseMaterial()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(BaseMaterialPath) != null)
            {
                return;
            }

            // A material asset referencing URP/Lit makes the build ship that shader; Shader.Find alone gets stripped.
            Directory.CreateDirectory(Path.GetDirectoryName(BaseMaterialPath));
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetFloat("_Smoothness", 0.1f);
            AssetDatabase.CreateAsset(material, BaseMaterialPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CoC] Created {BaseMaterialPath}");
        }

        /// <summary>The one material every Blender-pipeline model shares (spec: one material, GPU instancing).</summary>
        private static void EnsurePaletteMaterial()
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(PaletteTexturePath);
            if (texture == null || AssetDatabase.LoadAssetAtPath<Material>(PaletteMaterialPath) != null)
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(PaletteMaterialPath));
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { enableInstancing = true };
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0.05f);
            AssetDatabase.CreateAsset(material, PaletteMaterialPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CoC] Created {PaletteMaterialPath}");
        }
    }
}
