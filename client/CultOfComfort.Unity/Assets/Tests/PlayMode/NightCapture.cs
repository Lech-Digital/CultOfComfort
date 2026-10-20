using System;
using System.Collections;
using System.IO;
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

        private static IEnumerator Capture(bool portrait)
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

            var width = portrait ? Short : Long;
            var height = portrait ? Long : Short;
            var game = UnityEngine.Object.FindAnyObjectByType<GameController>();
            var document = game.GetComponent<UIDocument>();
            var ui = new RenderTexture(width, height, 24);
            document.panelSettings.targetTexture = ui;
            document.panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
            document.panelSettings.scale = Scale;
            document.panelSettings.clearColor = true;
            document.panelSettings.colorClearValue = new Color(0f, 0f, 0f, 0f);
            var world = new RenderTexture(width, height, 24) { antiAliasing = 4 };
            Camera.main.aspect = width / (float)height;
            game.ScreenOverride = new Vector2Int(width, height);
            yield return Frames(6);

            var suffix = portrait ? "portrait" : "landscape";
            var output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "art", "previews", "night"));
            Directory.CreateDirectory(output);
            Save(world, ui, Path.Combine(output, $"dusk_{suffix}.png"));
            game.StartNight();
            Time.captureDeltaTime = 1f / 30f;
            yield return Frames(90);
            Save(world, ui, Path.Combine(output, $"night_early_{suffix}.png"));
            yield return Frames(300);
            Save(world, ui, Path.Combine(output, $"night_fight_{suffix}.png"));
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
