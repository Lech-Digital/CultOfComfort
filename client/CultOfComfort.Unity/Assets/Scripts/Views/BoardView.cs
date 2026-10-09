using System.Collections.Generic;
using CultOfComfort.Simulation.Balance;
using CultOfComfort.Simulation.Grid;
using UnityEngine;

namespace CultOfComfort.Client
{
    /// <summary>Static scenery: ground, rocks, the Hearth, towers, the road preview and the tile cursor.</summary>
    public sealed class BoardView : MonoBehaviour
    {
        private readonly Dictionary<GridPosition, GameObject> _towers = new Dictionary<GridPosition, GameObject>();
        private readonly List<GameObject> _roadTiles = new List<GameObject>();
        private Transform _root;
        private Transform _roadRoot;
        private GameObject _cursor;
        private GameObject _leaderMarker;
        private Light _hearthLight;

        public Vector3 HearthPosition { get; private set; }

        public void Build(IReadOnlyList<GridPosition> rocks)
        {
            _root = new GameObject("Board").transform;
            _root.SetParent(transform, false);
            _roadRoot = new GameObject("Road").transform;
            _roadRoot.SetParent(_root, false);

            for (var y = 0; y < DemoMap.Height; y++)
            {
                for (var x = 0; x < DemoMap.Width; x++)
                {
                    var noise = Mathf.PerlinNoise(x * 0.37f, y * 0.37f);
                    var color = noise > 0.62f ? Palette.Grass : (x + y) % 2 == 0 ? Palette.Ground : Palette.GroundAlt;
                    Shapes.Create(PrimitiveType.Cube, color, new Vector3(x, -0.1f, -y), new Vector3(1f, 0.2f, 1f), _root, "Tile");
                }
            }

            foreach (var rock in rocks)
            {
                var seed = rock.X * 31 + rock.Y * 17;
                var go = Shapes.Create(PrimitiveType.Sphere, Palette.Rock, Shapes.ToWorld(rock, 0.3f),
                    new Vector3(0.85f, 0.6f + seed % 3 * 0.12f, 0.8f), _root, "Rock");
                go.transform.localRotation = Quaternion.Euler(0f, seed * 37 % 360, 8f);
            }

            BuildSpawn();
            BuildHearth();

            _cursor = Shapes.Create(PrimitiveType.Cube, Palette.Highlight, Vector3.zero, new Vector3(0.95f, 0.05f, 0.95f), _root, "Cursor");
            _cursor.SetActive(false);
            _leaderMarker = Shapes.Create(PrimitiveType.Cylinder, Palette.Linen, Vector3.zero, new Vector3(0.5f, 0.01f, 0.5f), _root, "Leader Destination");
            _leaderMarker.SetActive(false);
        }

        public void SetTowers(IEnumerable<PlacedTower> towers)
        {
            foreach (var go in _towers.Values)
            {
                Destroy(go);
            }

            _towers.Clear();
            foreach (var tower in towers)
            {
                _towers[tower.Position] = CreateTower(tower);
            }
        }

        public void ShowRoad(IReadOnlyList<GridPosition> path)
        {
            foreach (var go in _roadTiles)
            {
                Destroy(go);
            }

            _roadTiles.Clear();
            foreach (var tile in path)
            {
                _roadTiles.Add(Shapes.Create(PrimitiveType.Cube, Palette.Road, Shapes.ToWorld(tile, 0.005f), new Vector3(0.8f, 0.02f, 0.8f), _roadRoot, "Road"));
            }
        }

        public void ShowCursor(GridPosition? tile, bool valid)
        {
            _cursor.SetActive(tile.HasValue);
            if (tile.HasValue)
            {
                _cursor.transform.localPosition = Shapes.ToWorld(tile.Value, 0.03f);
                _cursor.GetComponent<Renderer>().sharedMaterial = Shapes.Material(valid ? Palette.Highlight : Palette.Invalid);
            }
        }

        public void ShowLeaderDestination(GridPosition? tile)
        {
            _leaderMarker.SetActive(tile.HasValue);
            if (tile.HasValue)
            {
                _leaderMarker.transform.localPosition = Shapes.ToWorld(tile.Value, 0.03f);
            }
        }

        public void SetHearthWarmth(float fraction)
        {
            _hearthLight.intensity = Mathf.Lerp(0.2f, 3.5f, fraction) * (1f + Mathf.Sin(Time.time * 6f) * 0.06f);
        }

        private void BuildSpawn()
        {
            var at = Shapes.ToWorld(DemoMap.Spawn);
            Shapes.Create(PrimitiveType.Cylinder, Palette.Wood, at + new Vector3(0f, 0.5f, 0.35f), new Vector3(0.12f, 0.5f, 0.12f), _root, "Gate Post");
            Shapes.Create(PrimitiveType.Cylinder, Palette.Wood, at + new Vector3(0f, 0.5f, -0.35f), new Vector3(0.12f, 0.5f, 0.12f), _root, "Gate Post");
            Shapes.Create(PrimitiveType.Cube, Palette.Wood, at + new Vector3(0f, 0.95f, 0f), new Vector3(0.12f, 0.1f, 0.9f), _root, "Gate Beam");
        }

        private void BuildHearth()
        {
            HearthPosition = Shapes.ToWorld(DemoMap.Hearth);
            var hearth = new GameObject("Hearth").transform;
            hearth.SetParent(_root, false);
            hearth.localPosition = HearthPosition;
            Shapes.Create(PrimitiveType.Cube, Palette.Hearth, new Vector3(0f, 0.35f, 0f), new Vector3(0.85f, 0.7f, 0.85f), hearth, "Bathhouse");
            var roof = Shapes.Create(PrimitiveType.Cube, Shapes.Hex("#4a3c30"), new Vector3(0f, 0.85f, 0f), new Vector3(0.7f, 0.7f, 0.7f), hearth, "Roof");
            roof.transform.localRotation = Quaternion.Euler(45f, 0f, 0f);
            roof.transform.localScale = new Vector3(0.95f, 0.5f, 0.5f);
            Shapes.Create(PrimitiveType.Cube, Palette.Ember, new Vector3(0f, 0.25f, -0.43f), new Vector3(0.3f, 0.35f, 0.02f), hearth, "Glowing Door");
            Shapes.Create(PrimitiveType.Cylinder, Palette.Rock, new Vector3(0.25f, 1.1f, 0.1f), new Vector3(0.14f, 0.25f, 0.14f), hearth, "Chimney");

            var lightGo = new GameObject("Hearth Glow");
            lightGo.transform.SetParent(hearth, false);
            lightGo.transform.localPosition = new Vector3(0f, 1.2f, -0.6f);
            _hearthLight = lightGo.AddComponent<Light>();
            _hearthLight.type = LightType.Point;
            _hearthLight.color = Palette.Ember;
            _hearthLight.range = 4.5f;
            _hearthLight.intensity = 3.5f;
        }

        private GameObject CreateTower(PlacedTower tower)
        {
            var root = new GameObject(tower.Type.ToString()).transform;
            root.SetParent(_root, false);
            root.localPosition = Shapes.ToWorld(tower.Position);

            if (tower.Type == TowerType.SlurryChute)
            {
                for (var i = 0; i < 4; i++)
                {
                    var x = i % 2 == 0 ? -0.25f : 0.25f;
                    var z = i < 2 ? -0.25f : 0.25f;
                    Shapes.Create(PrimitiveType.Cylinder, Palette.Wood, new Vector3(x, 0.35f, z), new Vector3(0.07f, 0.35f, 0.07f), root, "Stilt");
                }

                Shapes.Create(PrimitiveType.Cube, Palette.Wood, new Vector3(0f, 0.85f, 0f), new Vector3(0.75f, 0.35f, 0.75f), root, "Hopper");
                Shapes.Create(PrimitiveType.Cube, Palette.Mud, new Vector3(0f, 1.03f, 0f), new Vector3(0.65f, 0.02f, 0.65f), root, "Slurry");
            }
            else
            {
                Shapes.Create(PrimitiveType.Cube, Palette.Wood, new Vector3(0f, 0.08f, 0f), new Vector3(0.7f, 0.16f, 0.4f), root, "Base");
                var sapling = Shapes.Create(PrimitiveType.Capsule, Palette.Sapling, new Vector3(-0.1f, 0.6f, 0f), new Vector3(0.12f, 0.5f, 0.12f), root, "Sapling");
                sapling.transform.localRotation = Quaternion.Euler(0f, 0f, 25f);
                Shapes.Create(PrimitiveType.Sphere, Palette.Stone, new Vector3(-0.32f, 1.05f, 0f), Vector3.one * 0.2f, root, "Rock");
            }

            return root.gameObject;
        }
    }
}
