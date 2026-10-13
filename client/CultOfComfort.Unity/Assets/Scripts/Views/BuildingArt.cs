using System;
using CultOfComfort.Simulation.Base;
using UnityEngine;

namespace CultOfComfort.Client
{
    /// <summary>
    /// Which model a building wears at each level, and how big it stands on its plot. A building never outgrows its
    /// footprint (neighbours stay neighbours): it grows by being rebuilt as something taller and sillier.
    /// </summary>
    public static class BuildingArt
    {
        /// <summary>A finished building stands this big per tile of its narrow side.</summary>
        public const float ScalePerTile = 0.92f;

        /// <summary>A half-built shell (or a sapling) until it is done.</summary>
        public const float ShellScalePerTile = 0.5f;

        public static string BaseModel(BuildingType type) => type switch
        {
            BuildingType.Woodcutter => "woodcutter",
            BuildingType.PeatCut => "peat_cut",
            BuildingType.StorageHut => "storage_hut",
            BuildingType.CultistHut => "cultist_hut",
            BuildingType.Cistern => "cistern",
            BuildingType.SlurryChute => "slurry_chute",
            BuildingType.SaplingFlinger => "sapling_flinger",
            BuildingType.WattleFence => "fence",
            BuildingType.WoodenWall => "palisade",
            BuildingType.Gate => "gate_wood",
            BuildingType.DirtTrack => "road_dirt",
            BuildingType.PlankWalk => "road_planks",
            BuildingType.CobbledRoad => "road_cobbles",
            BuildingType.Cottage => "cottage",
            BuildingType.Stone => "rock_b",
            BuildingType.Tree => "oak",
            _ => null,
        };

        /// <summary>
        /// The model for a level: <c>name_l3</c>, else <c>name_l2</c>, else the level-1 <c>name</c>, so a level whose
        /// redesign is not drawn yet still shows the best one that is.
        /// </summary>
        public static string ModelName(BuildingType type, int level, Func<string, bool> exists)
        {
            var name = BaseModel(type);
            if (name == null)
            {
                return null;
            }

            for (var k = level; k >= 2; k--)
            {
                var upgraded = $"{name}_l{k}";
                if (exists(upgraded))
                {
                    return upgraded;
                }
            }

            return name;
        }

        /// <summary>
        /// Uniform scale for a model of <paramref name="modelSize"/> (its X/Z extent at scale 1) on a plot of
        /// <paramref name="footprint"/> tiles: <see cref="ScalePerTile"/> per tile of the narrow side, but never
        /// spilling over the plot onto the neighbours.
        /// </summary>
        public static float Scale(Vector2 footprint, Vector2 modelSize, bool finished)
        {
            var wanted = (finished ? ScalePerTile : ShellScalePerTile) * Mathf.Min(footprint.x, footprint.y);
            if (modelSize.x <= 0f || modelSize.y <= 0f)
            {
                return wanted;
            }

            return Mathf.Min(wanted, footprint.x / modelSize.x, footprint.y / modelSize.y);
        }

        /// <summary>X/Z size of everything a model draws, as placed under <paramref name="parent"/> at scale 1.</summary>
        public static Vector2 MeasuredSize(GameObject model, Transform parent)
        {
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return Vector2.zero;
            }

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            var unit = parent.lossyScale;
            return new Vector2(bounds.size.x / Mathf.Max(unit.x, 1e-4f), bounds.size.z / Mathf.Max(unit.z, 1e-4f));
        }

        /// <summary>Fits a freshly made model (scale 1, already turned) to its plot.</summary>
        public static void Fit(GameObject model, Transform parent, Vector2 footprint, bool finished)
        {
            model.transform.localScale = Vector3.one;
            model.transform.localScale = Vector3.one * Scale(footprint, MeasuredSize(model, parent), finished);
        }
    }
}
