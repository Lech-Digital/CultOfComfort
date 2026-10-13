using System.Collections.Generic;
using System.Linq;
using System.IO;
using CultOfComfort.Client;
using CultOfComfort.Simulation.Balance;
using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Grid;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CultOfComfort.Editor
{
    /// <summary>
    /// Renders the real board (BoardView + models) with a few towers, raiders and the Prophet posed on it, from the
    /// game camera, without a phone. Batch:
    /// Unity -batchmode -quit -projectPath client/CultOfComfort.Unity -executeMethod CultOfComfort.Editor.BoardScreenshot.Render
    /// Output: art/previews/board_landscape.png and board_portrait.png.
    /// </summary>
    public static class BoardScreenshot
    {
        [MenuItem("Cult of Comfort/Render Board Screenshots")]
        public static void Render()
        {
            if (!PrepareEditor(out var previous))
            {
                return;
            }

            var output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "art", "previews"));
            Shoot(Path.Combine(output, "board_landscape.png"), 1600, 900, 0f);
            Shoot(Path.Combine(output, "board_portrait.png"), 720, 1520, -90f);
            Debug.Log($"[CoC] Board screenshots written to {output}");
            Restore(previous);
        }

        [MenuItem("Cult of Comfort/Render Village Screenshots")]
        public static void RenderVillage()
        {
            if (!PrepareEditor(out var previous))
            {
                return;
            }

            var output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "art", "previews"));
            ShootVillage(Path.Combine(output, "village_day.png"), night: false);
            ShootVillage(Path.Combine(output, "village_night.png"), night: true);
            Debug.Log($"[CoC] Village screenshots written to {output}");
            Restore(previous);
        }

        /// <summary>
        /// Every levelled building at each of its levels, on its real plot, sized as the board sizes it (BuildingArt),
        /// with a cultist for scale. Output: art/previews/building_levels.png.
        /// </summary>
        [MenuItem("Cult of Comfort/Render Building Levels")]
        public static void RenderLevels()
        {
            if (!PrepareEditor(out var previous))
            {
                return;
            }

            NewStage();
            var table = BuildingTable.Default;
            BuildingType[] rows = { BuildingType.CultistHut, BuildingType.PeatCut, BuildingType.Woodcutter, BuildingType.StorageHut, BuildingType.Cottage, BuildingType.Cistern };
            var z = 0f;
            foreach (var type in rows)
            {
                var spec = table.Spec(type);
                var footprint = new Vector2(spec.Width, spec.Depth);
                for (var level = 1; level <= spec.MaxLevel; level++)
                {
                    var holder = new GameObject($"{type} {level}").transform;
                    holder.position = new Vector3((level - 2) * 4.2f, 0f, z);
                    Shapes.Create(PrimitiveType.Cube, Palette.Road, new Vector3(0f, 0.01f, 0f), new Vector3(footprint.x - 0.06f, 0.02f, footprint.y - 0.06f), holder, "Plot");
                    var model = ArtLibrary.Instantiate(BuildingArt.ModelName(type, level, ArtLibrary.Exists), holder);
                    if (model != null)
                    {
                        model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                        BuildingArt.Fit(model, holder, footprint, finished: true);
                    }

                    Pose("cultist", holder.position + new Vector3(footprint.x / 2f + 0.35f, 0f, -footprint.y / 2f + 0.2f), 200f);
                }

                z -= spec.Depth + 1.4f;
            }

            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.localScale = new Vector3(16f, 0.2f, -z + 4f);
            ground.transform.position = new Vector3(0f, -0.1f, z / 2f);
            ground.GetComponent<Renderer>().sharedMaterial = Shapes.Material(Palette.Grass);
            var sun = Object.FindAnyObjectByType<Light>(FindObjectsInactive.Exclude);
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(40f, -35f, 0f);
                sun.intensity = 1.3f;
            }

            var camera = Camera.main != null ? Camera.main : new GameObject("Camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.55f, 0.62f, 0.7f);
            camera.fieldOfView = 42f;
            camera.transform.position = new Vector3(0f, 27f, z / 2f - 17f);
            camera.transform.LookAt(new Vector3(0f, 0f, z / 2f + 0.3f));
            const int width = 1200, height = 1500;
            var target = new RenderTexture(width, height, 24) { antiAliasing = 4 };
            camera.targetTexture = target;
            camera.aspect = width / (float)height;
            camera.Render();
            camera.Render(); // warm-up: see Shoot
            RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "art", "previews", "building_levels.png"));
            File.WriteAllBytes(path, image.EncodeToPNG());
            RenderTexture.active = null;
            camera.targetTexture = null;
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(image);
            Debug.Log($"[CoC] Building levels written to {path}");
            Restore(previous);
        }

        /// <summary>
        /// Renders happen in a throwaway scene so the game scene is never modified. Offers to save unsaved work first;
        /// returns false (and does nothing) if the user cancels.
        /// </summary>
        private static bool PrepareEditor(out string previousScene)
        {
            previousScene = EditorSceneManager.GetActiveScene().path;
            return Application.isBatchMode || EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        }

        private static void Restore(string previousScene)
        {
            if (!string.IsNullOrEmpty(previousScene))
            {
                EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);
            }
        }

        /// <summary>A fresh untitled scene with a camera and a sun, like the game's (never saved).</summary>
        private static void NewStage()
        {
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        }

        /// <summary>
        /// A hand-placed day village (spec step 4 preview): Hearth L3 in the middle, buildings around it, people and
        /// animals about their business, nature round the edge. Night swaps in teal moonlight and warm point lights.
        /// </summary>
        private static void ShootVillage(string path, bool night)
        {
            NewStage();
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.localScale = new Vector3(16f, 0.2f, 12f);
            ground.transform.position = new Vector3(0f, -0.1f, 0f);
            ground.GetComponent<Renderer>().sharedMaterial = Shapes.Material(Palette.Grass);
            var path_ = GameObject.CreatePrimitive(PrimitiveType.Cube);
            path_.transform.localScale = new Vector3(1.1f, 0.21f, 9f);
            path_.transform.position = new Vector3(0f, -0.1f, -2.5f);
            path_.GetComponent<Renderer>().sharedMaterial = Shapes.Material(Palette.Road);

            var placements = new (string Model, float X, float Z, float Yaw, float Scale)[]
            {
                ("hearth_l3", 0f, 0.5f, 180f, 1.3f), ("sauna", 3.2f, 1.5f, 200f, 1.1f), ("cultist_hut", -3.3f, 1.4f, 160f, 1.1f),
                ("storage_hut", 4.2f, -1.6f, 230f, 1f), ("woodcutter", -4.4f, -1.5f, 140f, 1f), ("cistern", 2.0f, -2.4f, 180f, 1f),
                ("well", -1.8f, -2.6f, 180f, 1f), ("peat_cut", 5.6f, 2.6f, 200f, 1f), ("props_barrels", 1.4f, -0.9f, 0f, 1f),
                ("fence", -2.4f, 3.2f, 0f, 1f), ("fence", -1.4f, 3.2f, 0f, 1f), ("fence", -0.4f, 3.2f, 0f, 1f),
                ("palisade", 1.6f, 3.6f, 0f, 1f), ("palisade", 2.6f, 3.6f, 0f, 1f), ("palisade", 3.6f, 3.6f, 0f, 1f),
                ("prophet", 0.1f, -1.4f, 200f, 1f), ("cultist_towel", 0.9f, -1.6f, 230f, 1f), ("cultist", -0.8f, -1.8f, 150f, 1f),
                ("villager_woman", -2.4f, -1.9f, 120f, 1f), ("blacksmith", 3.0f, -0.6f, 250f, 1f), ("herbwife", -4.0f, 0.4f, 140f, 1f),
                ("friar_odo", 5.6f, -3.6f, 300f, 1f), ("bishop", 6.3f, -3.3f, 290f, 1f),
                ("pig", -1.2f, 2.4f, 90f, 1f), ("goose", 1.0f, 1.9f, 220f, 1f), ("chicken", 1.4f, 2.3f, 140f, 1f), ("chicken", 0.6f, 2.5f, 40f, 1f),
                ("sheep", -5.6f, 2.8f, 60f, 1f), ("goat", -5.0f, 3.4f, 200f, 1f), ("cow", 6.2f, 0.2f, 250f, 1f), ("dog", -0.4f, -2.6f, 30f, 1f),
                ("cat", 2.3f, 0.7f, 180f, 1f), ("traffic_cone", 0.6f, -3.6f, 0f, 1f), ("torch", -0.9f, -3.2f, 0f, 1f), ("torch", 0.9f, -3.2f, 0f, 1f),
                ("oak", -6.5f, -0.5f, 0f, 1.2f), ("pine", -7f, 4f, 30f, 1.3f), ("pine", 7f, 4.2f, 80f, 1.4f), ("birch", 7.2f, -1.8f, 0f, 1.1f),
                ("pine", -6.8f, -4f, 10f, 1.2f), ("oak", 7.1f, -4.6f, 0f, 1.1f), ("bush", -3.6f, -3.6f, 0f, 1f), ("bush", 4.3f, 3.6f, 0f, 1f),
                ("rock_b", 5.2f, -5f, 0f, 1.2f), ("stump", -5.3f, -2.8f, 0f, 1f), ("dead_tree", 6.4f, 5.2f, 0f, 1.1f),
            };
            foreach (var p in placements)
            {
                var go = ArtLibrary.Instantiate(p.Model, null);
                if (go == null)
                {
                    continue;
                }

                go.transform.position = new Vector3(p.X, 0f, p.Z);
                go.transform.rotation = Quaternion.Euler(0f, p.Yaw, 0f);
                go.transform.localScale = Vector3.one * p.Scale;
                if (night && (p.Model == "torch" || p.Model.StartsWith("hearth") || p.Model == "sauna" || p.Model == "cultist_hut"))
                {
                    var light = new GameObject("Night Light").AddComponent<Light>();
                    light.type = LightType.Point;
                    light.color = new Color(1f, 0.6f, 0.25f);
                    light.range = p.Model == "torch" ? 3f : 4.5f;
                    light.intensity = p.Model == "torch" ? 4f : 7f;
                    light.transform.position = new Vector3(p.X, p.Model == "torch" ? 1.5f : 1.2f, p.Z - 0.6f);
                }
            }

            var sun = Object.FindAnyObjectByType<Light>(FindObjectsInactive.Exclude);
            if (sun != null && sun.type == LightType.Directional)
            {
                sun.transform.rotation = night ? Quaternion.Euler(50f, 140f, 0f) : Quaternion.Euler(35f, -40f, 0f);
                sun.color = night ? new Color(0.45f, 0.75f, 0.85f) : new Color(1f, 0.86f, 0.66f);
                sun.intensity = night ? 0.12f : 1.4f;
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = night ? new Color(0.025f, 0.05f, 0.08f) : new Color(0.55f, 0.58f, 0.62f);

            var camera = Camera.main != null ? Camera.main : new GameObject("Camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = night ? new Color(0.03f, 0.05f, 0.08f) : new Color(0.55f, 0.62f, 0.7f);
            camera.fieldOfView = 40f;
            camera.transform.position = new Vector3(0f, 9.5f, -9.5f);
            camera.transform.LookAt(new Vector3(0f, 0f, -0.3f));
            const int width = 1600, height = 900;
            var target = new RenderTexture(width, height, 24) { antiAliasing = 4 };
            camera.targetTexture = target;
            camera.aspect = width / (float)height;
            camera.Render();
            camera.Render(); // warm-up: see Shoot
            RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            RenderTexture.active = null;
            camera.targetTexture = null;
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(image);
        }

        private static void Shoot(string path, int width, int height, float yaw)
        {
            NewStage();
            var stage = new GameObject("Stage");
            var board = stage.AddComponent<BoardView>();
            var rocks = DemoMap.CreateRocks();
            board.Build(rocks);
            var towers = new List<PlacedTower>
            {
                new PlacedTower(TowerType.SlurryChute, new GridPosition(4, 6)),
                new PlacedTower(TowerType.SaplingFlinger, new GridPosition(6, 8)),
                new PlacedTower(TowerType.SaplingFlinger, new GridPosition(9, 7)),
                new PlacedTower(TowerType.SlurryChute, new GridPosition(10, 2)),
                new PlacedTower(TowerType.SaplingFlinger, new GridPosition(12, 1)),
            };
            var buildings = towers.Select((t, i) => new BuildingView(i + 1,
                t.Type == TowerType.SlurryChute ? BuildingType.SlurryChute : BuildingType.SaplingFlinger, 1, t.Position, 0, 0, 0, null)).ToList();
            board.SetBuildings(buildings, 1, BuildingTable.Default, System.DateTimeOffset.UtcNow);
            var layout = new BattleLayout(DemoMap.Width, DemoMap.Height, DemoMap.Spawn, DemoMap.Hearth, towers, rocks);
            var road = layout.ResolveEnemyPath();
            board.ShowRoad(road);
            board.SetHearthWarmth(1f);
            // Pose the cast along the road: raiders marching in, the Prophet waiting near the Hearth.
            Pose("prophet", Shapes.ToWorld(road[road.Count - 4]), 200f);
            string[] raiders = { "levy", "levy", "manatarms", "zealot", "levy", "manatarms", "levy" };
            for (var i = 0; i < raiders.Length; i++)
            {
                var step = 3 + i * 3;
                if (step + 1 >= road.Count)
                {
                    break;
                }

                var at = Shapes.ToWorld(road[step]);
                var next = Shapes.ToWorld(road[step + 1]);
                Pose(raiders[i], at, Quaternion.LookRotation(next - at).eulerAngles.y);
            }

            var camera = Camera.main != null ? Camera.main : new GameObject("Camera").AddComponent<Camera>();
            camera.orthographic = false;
            camera.fieldOfView = 44f;
            var target = new RenderTexture(width, height, 24) { antiAliasing = 4 };
            camera.targetTexture = target;
            camera.aspect = width / (float)height;
            var rig = new CameraRig(camera, new Vector3(0f, 16.5f, -10.4f), new Vector2(0f, -(DemoMap.Height - 1)), new Vector2(DemoMap.Width - 1, 0f));
            rig.Frame(yaw, Rect.MinMaxRect(0.03f, 0.04f, 0.97f, 0.96f));
            rig.Reset();

            // Warm-up frame: URP's batcher uploads freshly created materials on their first frame; capturing that
            // frame mixes materials up (tiles showed the palette texture). The second frame is correct.
            camera.Render();
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(width, height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
            RenderTexture.active = null;
            camera.targetTexture = null;
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(image);
        }

        private static void Pose(string model, Vector3 at, float yaw)
        {
            var go = ArtLibrary.Instantiate(model, null);
            if (go == null)
            {
                return;
            }

            go.transform.position = at;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }
    }
}
