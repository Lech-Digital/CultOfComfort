using System.Collections.Generic;
using System.IO;
using CultOfComfort.Client;
using CultOfComfort.Simulation.Balance;
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
            var output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "art", "previews"));
            Shoot(Path.Combine(output, "board_landscape.png"), 1600, 900, 0f);
            Shoot(Path.Combine(output, "board_portrait.png"), 720, 1520, -90f);
            Debug.Log($"[CoC] Board screenshots written to {output}");
        }

        private static void Shoot(string path, int width, int height, float yaw)
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity", OpenSceneMode.Single);
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
            board.SetTowers(towers);
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
