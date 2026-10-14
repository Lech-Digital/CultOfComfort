using System;
using System.Collections;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Grid;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace CultOfComfort.Client.Tests
{
    /// <summary>
    /// Review tool, not a test: renders the live board with a few shaped buildings round the Hearth at each level into
    /// art/previews/world/editor_shapes_hearth{level}.png.
    /// Run explicitly: -testPlatform PlayMode -testFilter BoardCapture.
    /// </summary>
    [Explicit, Category("Capture")]
    public class BoardCapture
    {
        [TearDown]
        public void TearDown() => VillageComposition.Override = null;

        [UnityTest]
        public IEnumerator BoardCapture_Shapes_WritesPng([Values(1, 2, 3)] int hearthLevel)
        {
            // A village with shapes: a turned lean-to, a storage hut, a cultist hut, a peat trench, fences both ways, a tower.
            var now = DateTimeOffset.UtcNow;
            var state = BaseState.CreateNew(new BuildingTable { StartingStock = new ResourceBag(9000, 900, 2000, 500) }, VillageMap.Demo, now);
            for (var level = 1; level < hearthLevel; level++)
            {
                state.Apply(new UpgradeHearth(Guid.NewGuid()), now);
                state.Apply(new FinishConstructionNow(Guid.NewGuid()), now);
            }

            void Build(BuildingType type, int x, int y, bool rotated = false)
            {
                state.Apply(new PlaceBuilding(Guid.NewGuid(), type, new GridPosition(x, y), rotated), now);
                state.Apply(new FinishConstructionNow(Guid.NewGuid()), now);
            }

            Build(BuildingType.Woodcutter, 13, 5, true);
            Build(BuildingType.StorageHut, 14, 10);
            Build(BuildingType.CultistHut, 23, 11);
            Build(BuildingType.PeatCut, 20, 12);
            Build(BuildingType.SlurryChute, 16, 7);
            foreach (var y in new[] { 10, 11, 12 })
            {
                Build(BuildingType.WattleFence, 6, y, true);
            }

            Build(BuildingType.WattleFence, 7, 13);
            Build(BuildingType.WattleFence, 8, 13);
            state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Cottage, new GridPosition(20, 10)), now); // left under construction: the builder at work
            var store = new Store { Saved = state.ToData() };
            VillageComposition.Override = () => new LocalBaseGateway(store, new SystemClock(), BuildingTable.Default, VillageMap.Demo, _ => { });

            yield return SceneManager.LoadSceneAsync("SampleScene");
            for (var i = 0; i < 8; i++)
            {
                yield return null;
            }

            var camera = Camera.main;
            var target = new RenderTexture(2424, 1080, 24);
            camera.targetTexture = target;
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            camera.targetTexture = null;
            var folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "art", "previews", "world"));
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, $"editor_shapes_hearth{hearthLevel}.png"), texture.EncodeToPNG());
            UnityEngine.Object.Destroy(texture);
            Assert.Pass("written");
        }

        private sealed class Store : IBaseStore
        {
            public BaseData Saved;
            public Task<BaseData> LoadAsync(CancellationToken ct) => Task.FromResult(Saved);
            public Task SaveAsync(BaseData data, CancellationToken ct) { Saved = data; return Task.CompletedTask; }
        }
    }
}
