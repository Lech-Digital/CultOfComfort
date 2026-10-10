using System.Collections.Generic;
using CultOfComfort.Simulation.Grid;
using CultOfComfort.Simulation.Numerics;
using UnityEngine;

namespace CultOfComfort.Client
{
    /// <summary>
    /// Placeholder art: Unity primitives with flat colours. Swapped for real models later without touching game logic.
    /// Grid tile (x, y) maps to world (x, 0, -y), so the board reads top-to-bottom like the replay viewer.
    /// </summary>
    public static class Shapes
    {
        private static readonly Dictionary<Color, Material> Materials = new Dictionary<Color, Material>();
        private static Material _baseMaterial;

        public static Vector3 ToWorld(GridPosition tile, float height = 0f) => new Vector3(tile.X, height, -tile.Y);

        /// <summary>Simulation → presentation. Floats are fine here; they never flow back into the sim.</summary>
        public static Vector3 ToWorld(FixVector2 position, float height = 0f) =>
            new Vector3((float)position.X.ToDouble(), height, -(float)position.Y.ToDouble());

        public static GameObject Create(PrimitiveType type, Color color, Vector3 position, Vector3 scale, Transform parent, string name = null)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.Destroy(go.GetComponent<Collider>()); // Taps are resolved mathematically against the ground plane.
            go.name = name ?? type.ToString();
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = Material(color);
            return go;
        }

        public static Material Material(Color color)
        {
            if (Materials.TryGetValue(color, out var material))
            {
                return material;
            }

            // Assets/Resources/BaseLit.mat (created by ProjectSetup) makes the build include the URP Lit shader;
            // Shader.Find alone works in the editor but returns null on devices because unreferenced shaders are stripped.
            if (_baseMaterial == null)
            {
                _baseMaterial = Resources.Load<Material>("BaseLit");
            }

            material = new Material(_baseMaterial);
            material.color = color;
            Materials[color] = material;
            return material;
        }

        /// <summary>Per-instance tint without allocating a new material per enemy.</summary>
        public static void Tint(Renderer renderer, Color color, MaterialPropertyBlock block)
        {
            renderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", color);
            block.SetColor("_Color", color);
            renderer.SetPropertyBlock(block);
        }

        public static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
    }

    public static class Palette
    {
        public static readonly Color Ground = Shapes.Hex("#3a3024");
        public static readonly Color GroundAlt = Shapes.Hex("#342b20");
        public static readonly Color Grass = Shapes.Hex("#3f4a2c");
        public static readonly Color Road = Shapes.Hex("#6b5238");
        public static readonly Color Rock = Shapes.Hex("#5b5852");
        public static readonly Color Hearth = Shapes.Hex("#7a6652");
        public static readonly Color Ember = Shapes.Hex("#ffb04d");
        public static readonly Color Wood = Shapes.Hex("#8a6a43");
        public static readonly Color Sapling = Shapes.Hex("#8aa65a");
        public static readonly Color Stone = Shapes.Hex("#a8a29a");
        public static readonly Color Levy = Shapes.Hex("#d9c39a");
        public static readonly Color ManAtArms = Shapes.Hex("#9aa3ab");
        public static readonly Color Zealot = Shapes.Hex("#c7533a");
        public static readonly Color HotStone = Shapes.Hex("#e0663a");
        public static readonly Color Steam = Shapes.Hex("#ece6dc");
        public static readonly Color Mud = Shapes.Hex("#5c3e20");
        public static readonly Color Fright = Shapes.Hex("#e6e0ff");
        public static readonly Color Linen = Shapes.Hex("#f2ece0");
        public static readonly Color Water = Shapes.Hex("#6db3dc");
        public static readonly Color Highlight = Shapes.Hex("#e8a54b");
        public static readonly Color Invalid = Shapes.Hex("#d0583c");
        public static readonly Color HealthGood = Shapes.Hex("#8aa65a");
        public static readonly Color HealthBad = Shapes.Hex("#d0583c");
    }
}
