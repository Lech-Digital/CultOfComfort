using UnityEngine;

namespace CultOfComfort.Client
{
    /// <summary>
    /// Loads Blender-pipeline models (art/build.sh → Resources/Art/Models) by name. Returns null when a model has not
    /// been made yet, so callers fall back to the primitive placeholders and the game stays playable at every step.
    /// </summary>
    public static class ArtLibrary
    {
        private const string ModelFolder = "Art/Models/";
        private static Material _palette;

        public static Material PaletteMaterial =>
            _palette != null ? _palette : _palette = Resources.Load<Material>("Art/Materials/Palette");

        public static GameObject Instantiate(string name, Transform parent)
        {
            var model = Resources.Load<GameObject>(ModelFolder + name);
            if (model == null)
            {
                return null;
            }

            var instance = Object.Instantiate(model, parent, false);
            instance.name = name;
            var material = PaletteMaterial;
            if (material != null)
            {
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
                {
                    renderer.sharedMaterial = material;
                }
            }

            return instance;
        }

        /// <summary>Depth-first search for a named part (Blender object name), e.g. "Umbrella".</summary>
        public static Transform FindPart(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindPart(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
