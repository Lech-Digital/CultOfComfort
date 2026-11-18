using System.Collections.Generic;
using CultOfComfort.Simulation.Combat;
using CultOfComfort.Simulation.Grid;
using UnityEngine;

namespace CultOfComfort.Client
{
    /// <summary>
    /// The Night's light (spec 2026-11-17 fun first, F1; the reference frame art/concepts/night-final.jpg): a big warm pool
    /// round the Hearth, two fire baskets beside it, and torches along the last stretch of the raiders' road, so the fight
    /// reads warm against the cool moonlight. Built under the battle's root and gone with it.
    /// </summary>
    public static class NightLights
    {
        private static readonly Color Fire = Shapes.Hex("#ffa24a");

        /// <summary>The flame itself: deep orange, lit just past 1 so the bloom catches it without washing it white.</summary>
        private static readonly Color Flame = Shapes.Hex("#ff6a10");
        private static readonly Color Pool = Shapes.Hex("#ffb35c");

        /// <summary>How many torches line the road, and how far apart (tiles), counted back from the Hearth.</summary>
        public const int Torches = 5;
        public const int TorchEvery = 3;

        public static void Build(Transform root, Battle battle)
        {
            var path = battle.Path;
            if (path == null || path.Count < 2)
            {
                return;
            }

            var hearth = Shapes.ToWorld(path[path.Count - 1]);
            PointLight(root, "Hearth pool", hearth + Vector3.up * 1.6f, Pool, range: 7.5f, intensity: 3.2f);

            // Fire baskets on either side of the Hearth, across the road's last step.
            var into = (Shapes.ToWorld(path[path.Count - 1]) - Shapes.ToWorld(path[path.Count - 2])).normalized;
            var side = new Vector3(-into.z, 0f, into.x);
            foreach (var s in new[] { -1.6f, 1.6f })
            {
                Basket(root, hearth - into * 1.4f + side * s);
            }

            foreach (var at in TorchSpots(path))
            {
                Torch(root, at);
            }
        }

        /// <summary>Torch posts beside the road, every <see cref="TorchEvery"/> tiles back from the Hearth, sides alternating. Pure.</summary>
        public static List<Vector3> TorchSpots(IReadOnlyList<GridPosition> path)
        {
            var spots = new List<Vector3>();
            for (int i = path.Count - 1 - TorchEvery, n = 0; i >= 1 && n < Torches; i -= TorchEvery, n++)
            {
                var here = Shapes.ToWorld(path[i]);
                var along = (Shapes.ToWorld(path[i + 1]) - Shapes.ToWorld(path[i - 1])).normalized;
                var side = new Vector3(-along.z, 0f, along.x) * (n % 2 == 0 ? 0.62f : -0.62f);
                spots.Add(here + side);
            }

            return spots;
        }

        private static void Torch(Transform root, Vector3 at)
        {
            var post = Shapes.Create(PrimitiveType.Cylinder, Shapes.Hex("#4a3322"), at + Vector3.up * 0.55f, new Vector3(0.08f, 0.55f, 0.08f), root, "Torch post");
            var flame = Shapes.Create(PrimitiveType.Sphere, Fire, at + Vector3.up * 1.18f, new Vector3(0.2f, 0.3f, 0.2f), root, "Torch flame");
            flame.GetComponent<Renderer>().sharedMaterial = Shapes.Glow(Flame, 1.25f); // just past 1: the night's bloom catches it
            flame.AddComponent<Flicker>();
            PointLight(post.transform, "Torch light", at + Vector3.up * 1.3f, Fire, range: 3.2f, intensity: 1.8f, world: true);
        }

        private static void Basket(Transform root, Vector3 at)
        {
            Shapes.Create(PrimitiveType.Cylinder, Shapes.Hex("#5a3d24"), at + Vector3.up * 0.22f, new Vector3(0.42f, 0.22f, 0.42f), root, "Fire basket");
            var flame = Shapes.Create(PrimitiveType.Sphere, Fire, at + Vector3.up * 0.55f, new Vector3(0.36f, 0.42f, 0.36f), root, "Basket flame");
            flame.GetComponent<Renderer>().sharedMaterial = Shapes.Glow(Flame, 1.3f);
            flame.AddComponent<Flicker>();
            PointLight(root, "Basket light", at + Vector3.up * 0.9f, Fire, range: 3.5f, intensity: 2.2f);
        }

        private static void PointLight(Transform parent, string name, Vector3 at, Color color, float range, float intensity, bool world = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, world);
            go.transform.position = at;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None; // cheap on a phone: the sun's shadows do the work
            go.AddComponent<Flicker>().Light = light;
        }

        /// <summary>A fire's breathing: the flame's size and its light's strength wobble a little, never in step.</summary>
        private sealed class Flicker : MonoBehaviour
        {
            public Light Light;
            private float _seed;
            private float _base;
            private Vector3 _scale;

            private void Start()
            {
                _seed = Random.value * 10f;
                _base = Light != null ? Light.intensity : 0f;
                _scale = transform.localScale;
            }

            private void Update()
            {
                var t = Time.time * 7f + _seed;
                var wobble = 0.85f + 0.1f * Mathf.Sin(t) + 0.05f * Mathf.Sin(t * 2.3f);
                if (Light != null)
                {
                    Light.intensity = _base * wobble;
                }
                else
                {
                    transform.localScale = new Vector3(_scale.x, _scale.y * (0.9f + 0.15f * wobble), _scale.z);
                }
            }
        }
    }
}
