using System.IO;
using UnityEditor;
using UnityEngine;

namespace CultOfComfort.Editor
{
    /// <summary>Creates assets the code-first client depends on. Idempotent; also runs before every build.</summary>
    public static class ProjectSetup
    {
        public const string BaseMaterialPath = "Assets/Resources/BaseLit.mat";

        [MenuItem("Cult of Comfort/Ensure Project Assets")]
        public static void EnsureAssets()
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
    }
}
