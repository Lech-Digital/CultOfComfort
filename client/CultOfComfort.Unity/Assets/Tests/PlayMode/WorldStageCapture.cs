using System.Collections;
using System.IO;
using System.Linq;
using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace CultOfComfort.Client.Tests
{
    /// <summary>
    /// Renders the shared world's 3D map (no server: a generated world, a few rival camps, a missionary on the road) to
    /// art/previews/world/stage_{near,far}.png. Explicit: a look, not a check.
    /// </summary>
    [Explicit]
    public class WorldStageCapture
    {
        private const int Width = 540;
        private const int Height = 1212;

        [UnityTest]
        public IEnumerator WorldStageCapture_SharedWorld_WritesPngs()
        {
            var map = WorldGenerator.Generate(20261014);
            var keep = map.Nodes.First(n => n.Type == NodeType.Keep && Hex.Distance(0, 0, n.Q, n.R) < 20);
            var home = Hex.Disc(map.Shape.Radius).Select(h => (h.Q, h.R))
                .Where(h => map.NodeAt(h.Q, h.R) == null && Hex.Distance(h.Q, h.R, keep.Q, keep.R) is var d && d >= 2 && d <= 3).First();
            var land = new WorldLand(20261014, map.Shape.Radius, map.Nodes.Select(n => (n.Id, n.Type, n.Q, n.R)), home);
            var light = new GameObject("Sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            light.shadows = LightShadows.Soft;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Shapes.Hex("#b8a47a");
            var stage = WorldStage.Create();
            var world = new RenderTexture(Width, Height, 24) { antiAliasing = 4 };
            land.TryPlace(WorldMapPresenter.HomeId, out var village);
            stage.Open(land, village.At, null);
            stage.Camera.targetTexture = world;
            stage.Camera.aspect = Width / (float)Height;
            var free = Hex.Disc(map.Shape.Radius).Select(h => (h.Q, h.R)).Where(h => map.NodeAt(h.Q, h.R) == null && h != home)
                .OrderBy(h => Hex.Distance(h.Q, h.R, home.Q, home.R)).Where((h, i) => i % 5 == 3).Take(4).ToList();
            var camps = free.Select((h, i) => new StageCamp { Id = -(i + 1), Key = $"cult{i}", Q = h.Q, R = h.R, Colour = SharedWorldPresenter.CultColours[i] }).ToList();
            var target = map.Near(home.Q, home.R, 3).First(n => n.Type != NodeType.Keep);
            stage.SetPeople(2, camps, new[] { new StageWalker { NodeId = target.Id, Phase = MissionaryPhase.Travelling, Fraction = 0.4f } });
            yield return new WaitForSecondsRealtime(4f);
            var output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "art", "previews", "world"));
            Directory.CreateDirectory(output);
            Save(world, Path.Combine(output, "stage_near.png"));
            stage.Rig.JumpTo(stage.Rig.Focus, MapCamera.MaxDistance);
            yield return new WaitForSecondsRealtime(5f);
            Save(world, Path.Combine(output, "stage_far.png"));
            stage.Rig.JumpTo(stage.Rig.Focus, MapCamera.MinDistance);
            yield return new WaitForSecondsRealtime(2f);
            Save(world, Path.Combine(output, "stage_close.png"));
            Object.Destroy(stage.gameObject);
            Object.Destroy(light.gameObject);
        }

        private static void Save(RenderTexture source, string path)
        {
            RenderTexture.active = source;
            var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            texture.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }
    }
}
