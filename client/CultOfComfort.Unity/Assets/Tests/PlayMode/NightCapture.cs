using System;
using System.Collections;
using System.IO;
using System.Linq;
using CultOfComfort.Contracts;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace CultOfComfort.Client.Tests
{
    /// <summary>
    /// Review tool, not a test: a Night at phone size, board and HUD together, in landscape and portrait, written to
    /// art/previews/night/. Uses the village in the file named by CAPTURE_VILLAGE (a save's JSON) when set, else a
    /// fresh Hearth 2 village. Run explicitly: -testPlatform PlayMode -testFilter NightCapture.
    /// </summary>
    [Explicit, Category("Capture")]
    public class NightCapture
    {
        private const int Long = 1212;
        private const int Short = 540;
        private const float Scale = 1.3125f; // the Pixel's 420 dpi at half size

        private sealed class Store : IBaseStore
        {
            public CultOfComfort.Simulation.Base.BaseData Saved;
            public System.Threading.Tasks.Task<CultOfComfort.Simulation.Base.BaseData> LoadAsync(System.Threading.CancellationToken ct) => System.Threading.Tasks.Task.FromResult(Saved);
            public System.Threading.Tasks.Task SaveAsync(CultOfComfort.Simulation.Base.BaseData data, System.Threading.CancellationToken ct) { Saved = data; return System.Threading.Tasks.Task.CompletedTask; }
        }

        private sealed class Clock : IClock
        {
            public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
        }

        private sealed class SeenIntro : IIntroProgress
        {
            public bool Seen => true;

            public void MarkSeen()
            {
            }
        }

        /// <summary>The ground paints off the main thread: wait for it (a few seconds at most).</summary>
        private static IEnumerator GroundPainted()
        {
            var board = UnityEngine.Object.FindAnyObjectByType<BoardView>();
            for (var waited = 0f; board != null && !board.GroundReady && waited < 20f; waited += Time.unscaledDeltaTime)
            {
                yield return null;
            }
        }

        [SetUp]
        public void SetUp() => VillageComposition.IntroOverride = () => new SeenIntro();

        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
            VillageComposition.Override = null;
            VillageComposition.IntroOverride = null;
        }

        [UnityTest]
        public IEnumerator NightCapture_Landscape_WritesPngs() => Capture(false);

        [UnityTest]
        public IEnumerator NightCapture_Portrait_WritesPngs() => Capture(true);

        /// <summary>The daily list on the note: the same village with its starter errands done.</summary>
        [UnityTest]
        public IEnumerator NightCapture_DailyList_WritesPng()
        {
            var clock = new Clock();
            var table = CultOfComfort.Simulation.Base.BuildingTable.Default;
            var map = CultOfComfort.Simulation.Base.VillageMap.Demo;
            var path = Environment.GetEnvironmentVariable("CAPTURE_VILLAGE");
            var state = CultOfComfort.Simulation.Base.BaseState.CreateNew(table, map, clock.UtcNow);
            state.Apply(new CultOfComfort.Simulation.Base.UpgradeHearth(Guid.NewGuid()), clock.UtcNow);
            state.Apply(new CultOfComfort.Simulation.Base.FinishConstructionNow(Guid.NewGuid()), clock.UtcNow);
            var data = !string.IsNullOrEmpty(path) && File.Exists(path)
                ? VillageSaveMapping.FromDto(JsonUtility.FromJson<VillageSaveDto>(File.ReadAllText(path)))
                : state.ToData();
            data = data with { ErrandIndex = table.Errands.Errands.Count, ErrandId = null }; // every errand claimed
            var store = new Store { Saved = data };
            VillageComposition.Override = () => new LocalBaseGateway(store, clock, table, map, _ => { });
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return Frames(6);
            yield return GroundPainted();
            var game = UnityEngine.Object.FindAnyObjectByType<GameController>();
            var document = game.GetComponent<UIDocument>();
            var ui = new RenderTexture(Short, Long, 24);
            document.panelSettings.targetTexture = ui;
            document.panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
            document.panelSettings.scale = Scale;
            document.panelSettings.clearColor = true;
            document.panelSettings.colorClearValue = new Color(0f, 0f, 0f, 0f);
            var world = new RenderTexture(Short, Long, 24) { antiAliasing = 4 };
            Camera.main.aspect = Short / (float)Long;
            game.ScreenOverride = new Vector2Int(Short, Long);
            yield return Frames(8);
            var output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "art", "previews", "night"));
            Directory.CreateDirectory(output);
            Save(world, ui, Path.Combine(output, "daily_portrait.png"));
        }

        /// <summary>The roster card at Dusk (spec 2026-11-17 fun first, B5): who may come tonight, all new.</summary>
        [UnityTest]
        public IEnumerator NightCapture_Roster_WritesPng()
        {
            PlayerPrefs.DeleteKey(GameController.RosterSeenKey);
            var clock = new Clock();
            var table = CultOfComfort.Simulation.Base.BuildingTable.Default;
            var map = CultOfComfort.Simulation.Base.VillageMap.Demo;
            var state = CultOfComfort.Simulation.Base.BaseState.CreateNew(table, map, clock.UtcNow);
            state.Apply(new CultOfComfort.Simulation.Base.UpgradeHearth(Guid.NewGuid()), clock.UtcNow);
            state.Apply(new CultOfComfort.Simulation.Base.FinishConstructionNow(Guid.NewGuid()), clock.UtcNow);
            var store = new Store { Saved = state.ToData() };
            VillageComposition.Override = () => new LocalBaseGateway(store, clock, table, map, _ => { });
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return Frames(6);
            yield return GroundPainted();
            var game = UnityEngine.Object.FindAnyObjectByType<GameController>();
            var document = game.GetComponent<UIDocument>();
            var ui = new RenderTexture(Short, Long, 24);
            document.panelSettings.targetTexture = ui;
            document.panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
            document.panelSettings.scale = Scale;
            document.panelSettings.clearColor = true;
            document.panelSettings.colorClearValue = new Color(0f, 0f, 0f, 0f);
            var world = new RenderTexture(Short, Long, 24) { antiAliasing = 4 };
            Camera.main.aspect = Short / (float)Long;
            game.ScreenOverride = new Vector2Int(Short, Long);
            yield return Frames(8);
            var output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "art", "previews", "night"));
            Directory.CreateDirectory(output);
            Save(world, ui, Path.Combine(output, "dusk_roster_link_portrait.png"));
            typeof(GameController).GetMethod("OpenRoster", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(game, null);
            yield return Frames(4);
            Save(world, ui, Path.Combine(output, "dusk_roster_portrait.png"));
            PlayerPrefs.DeleteKey(GameController.RosterSeenKey);
        }

        /// <summary>
        /// The store's captures (spec 2026-11-17 fun first, F5): the Pixel's full 1080 × 2424, a lived-in village at Dusk,
        /// a close Night with the Prophet mid-skill, the breather's boons. Into store-assets/_src/shots/ for build.mjs.
        /// </summary>
        [UnityTest]
        public IEnumerator NightCapture_Store_WritesShots() => Capture(true, forStore: true);

        /// <summary>A village worth a screenshot: Hearth 2, the first buildings, two towers by the road. Spots found by trying.</summary>
        private static CultOfComfort.Simulation.Base.BaseData LivedIn(DateTimeOffset now)
        {
            var rich = new CultOfComfort.Simulation.Base.BuildingTable { StartingStock = new CultOfComfort.Simulation.Base.ResourceBag(20000, 5000, 5000, 500) };
            var map = CultOfComfort.Simulation.Base.VillageMap.Demo;
            var state = CultOfComfort.Simulation.Base.BaseState.CreateNew(rich, map, now);
            state.Apply(new CultOfComfort.Simulation.Base.UpgradeHearth(Guid.NewGuid()), now);
            state.Apply(new CultOfComfort.Simulation.Base.FinishConstructionNow(Guid.NewGuid()), now);
            var hearth = map.HearthTiles[0];
            var spots = Enumerable.Range(0, map.Width).SelectMany(x => Enumerable.Range(0, map.Height).Select(y => new CultOfComfort.Simulation.Grid.GridPosition(x, y)))
                .OrderBy(p => Math.Abs(p.X - hearth.X) + Math.Abs(p.Y - hearth.Y)).ToList();
            foreach (var type in new[]
                     {
                         CultOfComfort.Simulation.Base.BuildingType.Woodcutter, CultOfComfort.Simulation.Base.BuildingType.Cottage,
                         CultOfComfort.Simulation.Base.BuildingType.StorageHut, CultOfComfort.Simulation.Base.BuildingType.CultistHut,
                         CultOfComfort.Simulation.Base.BuildingType.PeatCut, CultOfComfort.Simulation.Base.BuildingType.Cottage,
                         CultOfComfort.Simulation.Base.BuildingType.SlurryChute, CultOfComfort.Simulation.Base.BuildingType.SaplingFlinger,
                     })
            {
                foreach (var spot in spots.Skip(6))
                {
                    if (state.Apply(new CultOfComfort.Simulation.Base.PlaceBuilding(Guid.NewGuid(), type, spot), now).Status == CultOfComfort.Simulation.Base.CommandStatus.Accepted)
                    {
                        state.Apply(new CultOfComfort.Simulation.Base.FinishConstructionNow(Guid.NewGuid()), now);
                        break;
                    }
                }
            }

            return state.ToData();
        }

        private static IEnumerator Capture(bool portrait, bool forStore = false)
        {
            var clock = new Clock();
            var table = CultOfComfort.Simulation.Base.BuildingTable.Default;
            var map = CultOfComfort.Simulation.Base.VillageMap.Demo;
            var path = Environment.GetEnvironmentVariable("CAPTURE_VILLAGE");
            CultOfComfort.Simulation.Base.BaseData data;
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                data = VillageSaveMapping.FromDto(JsonUtility.FromJson<VillageSaveDto>(File.ReadAllText(path)));
            }
            else if (forStore)
            {
                data = LivedIn(clock.UtcNow);
            }
            else
            {
                var state = CultOfComfort.Simulation.Base.BaseState.CreateNew(table, map, clock.UtcNow);
                state.Apply(new CultOfComfort.Simulation.Base.UpgradeHearth(Guid.NewGuid()), clock.UtcNow);
                state.Apply(new CultOfComfort.Simulation.Base.FinishConstructionNow(Guid.NewGuid()), clock.UtcNow);
                data = state.ToData();
            }

            var store = new Store { Saved = data };
            VillageComposition.Override = () => new LocalBaseGateway(store, clock, table, map, _ => { });
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return Frames(6);
            yield return GroundPainted();

            var width = forStore ? 1080 : portrait ? Short : Long;
            var height = forStore ? 2424 : portrait ? Long : Short;
            var game = UnityEngine.Object.FindAnyObjectByType<GameController>();
            var document = game.GetComponent<UIDocument>();
            var ui = new RenderTexture(width, height, 24);
            document.panelSettings.targetTexture = ui;
            document.panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
            document.panelSettings.scale = forStore ? Scale * 2f : Scale;
            document.panelSettings.clearColor = true;
            document.panelSettings.colorClearValue = new Color(0f, 0f, 0f, 0f);
            var world = new RenderTexture(width, height, 24) { antiAliasing = 4 };
            Camera.main.aspect = width / (float)height;
            game.ScreenOverride = new Vector2Int(width, height);
            yield return Frames(6);

            var suffix = portrait ? "portrait" : "landscape";
            var output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "art", "previews", "night"));
            Directory.CreateDirectory(output);
            if (forStore)
            {
                yield return StoreShots(game, world, ui, Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "store-assets", "_src", "shots")));
                yield break;
            }

            Save(world, ui, Path.Combine(output, $"dusk_{suffix}.png"));
            game.StartNight();
            Time.captureDeltaTime = 1f / 30f;
            yield return Frames(90);
            Save(world, ui, Path.Combine(output, $"night_early_{suffix}.png"));
            yield return Frames(300);
            Save(world, ui, Path.Combine(output, $"night_fight_{suffix}.png"));

            // The first gap between waves (spec 2026-11-17 fun first, C2–C3): the boons and the call.
            var field = typeof(GameController).GetField("_battle", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            for (var frame = 0; frame < 6000; frame++)
            {
                if (field.GetValue(game) is CultOfComfort.Simulation.Combat.Battle { IsGapOpen: true })
                {
                    yield return Frames(15);
                    Save(world, ui, Path.Combine(output, $"night_gap_{suffix}.png"));
                    break;
                }

                yield return null;
            }
        }

        private static IEnumerator StoreShots(GameController game, RenderTexture world, RenderTexture ui, string output)
        {
            Directory.CreateDirectory(output);
            Save(world, ui, Path.Combine(output, "village.png"));
            game.StartNight();
            Time.captureDeltaTime = 1f / 30f;
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var field = typeof(GameController).GetField("_battle", flags);
            var stones = typeof(GameController).GetMethod("ToggleAimingStones", flags);

            // The Prophet mid-skill: Hot Stones on the first knot of raiders in reach, caught as they land.
            for (var frame = 0; frame < 3000; frame++)
            {
                if (field.GetValue(game) is CultOfComfort.Simulation.Combat.Battle battle && battle.IsHotStonesReady && battle.BestHotStonesTarget() != null)
                {
                    stones.Invoke(game, null);
                    yield return Frames(24); // as they land, in a puff
                    Save(world, ui, Path.Combine(output, "night.png"));
                    break;
                }

                yield return null;
            }

            for (var frame = 0; frame < 6000; frame++)
            {
                if (field.GetValue(game) is CultOfComfort.Simulation.Combat.Battle { IsGapOpen: true })
                {
                    yield return Frames(15);
                    Save(world, ui, Path.Combine(output, "boons.png"));
                    break;
                }

                yield return null;
            }
        }

        private static IEnumerator Frames(int count)
        {
            for (var i = 0; i < count; i++)
            {
                yield return null;
            }
        }

        private static void Save(RenderTexture world, RenderTexture ui, string path)
        {
            var camera = Camera.main;
            camera.targetTexture = world;
            camera.Render();
            camera.targetTexture = null;
            var image = Read(world);
            var overlay = Read(ui);
            var pixels = image.GetPixels();
            var top = overlay.GetPixels();
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.Lerp(pixels[i], new Color(top[i].r, top[i].g, top[i].b, 1f), top[i].a);
            }

            image.SetPixels(pixels);
            File.WriteAllBytes(path, image.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(image);
            UnityEngine.Object.DestroyImmediate(overlay);
        }

        private static Texture2D Read(RenderTexture source)
        {
            RenderTexture.active = source;
            var texture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            texture.Apply();
            RenderTexture.active = null;
            return texture;
        }
    }
}
