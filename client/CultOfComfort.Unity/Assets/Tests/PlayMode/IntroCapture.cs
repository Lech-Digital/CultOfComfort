using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace CultOfComfort.Client.Tests
{
    /// <summary>
    /// Review tool, not a test: plays the intro at 30 fps and writes one still per moment (3D and captions composited)
    /// into art/previews/intro/. Run explicitly: -testPlatform PlayMode -testFilter IntroCapture.
    /// </summary>
    [Explicit, Category("Capture")]
    public class IntroCapture
    {
        private const int Width = 540;
        private const int Height = 1212;
        private const float Scale = 1.3125f; // half the Pixel 10's ~420 dpi / 160

        /// <summary>(shot, seconds into it) worth a still.</summary>
        private static readonly (string Shot, float At)[] Moments =
        {
            (IntroScript.Garage, 2.5f), (IntroScript.Machine, 3.8f), (IntroScript.Jump, 0.5f), (IntroScript.Jump, 2.5f),
            (IntroScript.Marchlands, 3f), (IntroScript.Landing, 0.7f), (IntroScript.Landing, 1.3f), (IntroScript.NoSockets, 1.8f),
            (IntroScript.NoSockets, 3.5f), (IntroScript.Boot, 2.5f), (IntroScript.Boot, 6f), (IntroScript.Cult, 1.5f), (IntroScript.Cult, 4.5f),
            (IntroScript.Title, 3f),
        };

        private FakeProgress _progress;

        [SetUp]
        public void SetUp()
        {
            TestVillage.Use();
            _progress = new FakeProgress();
            VillageComposition.IntroOverride = () => _progress;
        }

        [TearDown]
        public void TearDown()
        {
            Time.captureDeltaTime = 0f;
            VillageComposition.IntroOverride = null;
            TestVillage.Reset();
        }

        [UnityTest]
        public IEnumerator IntroCapture_EveryShot_WritesPngs()
        {
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return null;
            var game = Object.FindAnyObjectByType<GameController>();
            var intro = game.Intro;
            Assert.IsNotNull(intro);
            var output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "art", "previews", "intro"));
            Directory.CreateDirectory(output);

            var ui = new RenderTexture(Width, Height, 24);
            var panel = game.GetComponent<UIDocument>().panelSettings;
            panel.targetTexture = ui;
            panel.scaleMode = PanelScaleMode.ConstantPixelSize;
            panel.scale = Scale;
            panel.clearColor = true;
            panel.colorClearValue = new Color(0f, 0f, 0f, 0f);
            var world = new RenderTexture(Width, Height, 24) { antiAliasing = 4 };
            var camera = Camera.main;
            camera.aspect = Width / (float)Height;

            Time.captureDeltaTime = 1f / 30f;
            var pending = new Queue<(string Shot, float At)>(Moments);
            var index = 0;
            var guard = 0;
            while (pending.Count > 0 && intro != null && intro.IsPlaying && guard++ < 3000)
            {
                yield return null;
                var next = pending.Peek();
                if (intro.CurrentShot == next.Shot && intro.ShotTime >= next.At)
                {
                    pending.Dequeue();
                    Save(camera, world, ui, Path.Combine(output, $"{index++:00}_{next.Shot}_{next.At:0.0}.png"));
                }
            }

            Assert.AreEqual(0, pending.Count, "every moment was captured");
        }

        [UnityTest]
        public IEnumerator IntroCapture_TutorialCoach_WritesPngs()
        {
            _progress.MarkSeen();
            yield return SceneManager.LoadSceneAsync("SampleScene");
            for (var f = 0; f < 6; f++)
            {
                yield return null;
            }

            var game = Object.FindAnyObjectByType<GameController>();
            var document = game.GetComponent<UIDocument>();
            var output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "art", "previews", "intro"));
            Directory.CreateDirectory(output);
            var ui = new RenderTexture(Width, Height, 24);
            document.panelSettings.targetTexture = ui;
            document.panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
            document.panelSettings.scale = Scale;
            document.panelSettings.clearColor = true;
            document.panelSettings.colorClearValue = new Color(0f, 0f, 0f, 0f);
            var hud = (HudView)typeof(GameController).GetField("_hud", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(game);
            hud.SetPortrait(true);
            var world = new RenderTexture(Width, Height, 24) { antiAliasing = 4 };
            Camera.main.aspect = Width / (float)Height;
            for (var f = 0; f < 8; f++)
            {
                yield return null;
            }

            Save(Camera.main, world, ui, Path.Combine(output, "coach_1_card.png"));
            var card = document.rootVisualElement.Q<Button>("card-Woodcutter");
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = card;
                card.SendEvent(submit);
            }

            for (var f = 0; f < 8; f++)
            {
                yield return null;
            }

            Save(Camera.main, world, ui, Path.Combine(output, "coach_2_confirm.png"));
        }

        private sealed class Clock : IClock { public System.DateTimeOffset UtcNow { get; set; } = new System.DateTimeOffset(2026, 10, 11, 12, 0, 0, System.TimeSpan.Zero); }

        private sealed class Store : IBaseStore
        {
            public CultOfComfort.Simulation.Base.BaseData Saved;
            public System.Threading.Tasks.Task<CultOfComfort.Simulation.Base.BaseData> LoadAsync(System.Threading.CancellationToken ct) => System.Threading.Tasks.Task.FromResult(Saved);
            public System.Threading.Tasks.Task SaveAsync(CultOfComfort.Simulation.Base.BaseData data, System.Threading.CancellationToken ct) { Saved = data; return System.Threading.Tasks.Task.CompletedTask; }
        }

        [UnityTest]
        public IEnumerator IntroCapture_RelicNight_WritesPngs()
        {
            _progress.MarkSeen();
            var clock = new Clock();
            var state = CultOfComfort.Simulation.Base.BaseState.CreateNew(CultOfComfort.Simulation.Base.BuildingTable.Default, CultOfComfort.Simulation.Base.VillageMap.Demo, clock.UtcNow);
            state.Apply(new CultOfComfort.Simulation.Base.UpgradeHearth(System.Guid.NewGuid()), clock.UtcNow);
            var store = new Store { Saved = state.ToData() };
            clock.UtcNow = clock.UtcNow.AddHours(1);
            VillageComposition.Override = () => new LocalBaseGateway(store, clock, CultOfComfort.Simulation.Base.BuildingTable.Default, CultOfComfort.Simulation.Base.VillageMap.Demo, _ => { });
            yield return SceneManager.LoadSceneAsync("SampleScene");
            for (var f = 0; f < 6; f++)
            {
                yield return null;
            }

            var game = Object.FindAnyObjectByType<GameController>();
            var document = game.GetComponent<UIDocument>();
            var output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "art", "previews", "intro"));
            var ui = new RenderTexture(Width, Height, 24);
            document.panelSettings.targetTexture = ui;
            document.panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
            document.panelSettings.scale = Scale;
            document.panelSettings.clearColor = true;
            document.panelSettings.colorClearValue = new Color(0f, 0f, 0f, 0f);
            var hud = (HudView)typeof(GameController).GetField("_hud", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(game);
            hud.SetPortrait(true);
            var world = new RenderTexture(Width, Height, 24) { antiAliasing = 4 };
            Camera.main.aspect = Width / (float)Height;
            game.StartNight();
            Time.captureDeltaTime = 1f / 30f;
            for (var f = 0; f < 150; f++)
            {
                yield return null;
            }

            var battle = (CultOfComfort.Simulation.Combat.Battle)typeof(GameController).GetField("_battle", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(game);
            battle.Intervene(CultOfComfort.Simulation.Combat.PlayerActionType.DynamoTorch);
            var raider = battle.Enemies.FirstOrDefault(e => e.IsActive);
            if (raider != null)
            {
                var p = battle.PositionOf(raider);
                battle.Intervene(CultOfComfort.Simulation.Combat.PlayerActionType.LaserPointer, new CultOfComfort.Simulation.Grid.GridPosition(p.X.FloorToInt(), p.Y.FloorToInt()));
            }

            for (var f = 0; f < 4; f++)
            {
                yield return null;
            }

            Save(Camera.main, world, ui, Path.Combine(output, "relic_night.png"));
        }

        [UnityTest]
        public IEnumerator IntroCapture_WorldMap_WritesPng()
        {
            _progress.MarkSeen();
            var clock = new Clock();
            var table = CultOfComfort.Simulation.Base.BuildingTable.Default;
            var state = CultOfComfort.Simulation.Base.BaseState.CreateNew(table, CultOfComfort.Simulation.Base.VillageMap.Demo, clock.UtcNow);
            state.Apply(new CultOfComfort.Simulation.Base.UpgradeHearth(System.Guid.NewGuid()), clock.UtcNow);
            clock.UtcNow = clock.UtcNow.AddHours(2);
            state.Apply(new CultOfComfort.Simulation.Base.DebugFillStores(System.Guid.NewGuid()), clock.UtcNow);
            state.Apply(new CultOfComfort.Simulation.Base.UpgradeHearth(System.Guid.NewGuid()), clock.UtcNow);
            clock.UtcNow = clock.UtcNow.AddHours(2);
            state.Apply(new CultOfComfort.Simulation.Base.DebugFillStores(System.Guid.NewGuid()), clock.UtcNow);
            state.Apply(new CultOfComfort.Simulation.Base.SendMissionary(System.Guid.NewGuid(), 1, 8), clock.UtcNow);
            state.Apply(new CultOfComfort.Simulation.Base.SendMissionary(System.Guid.NewGuid(), 7, 4), clock.UtcNow);
            clock.UtcNow = clock.UtcNow.AddHours(9);
            state.Snapshot(clock.UtcNow);
            state.Apply(new CultOfComfort.Simulation.Base.SendMissionary(System.Guid.NewGuid(), 3, 2), clock.UtcNow);
            clock.UtcNow = clock.UtcNow.AddMinutes(30);
            var store = new Store { Saved = state.ToData() };
            VillageComposition.Override = () => new LocalBaseGateway(store, clock, CultOfComfort.Simulation.Base.BuildingTable.Default, CultOfComfort.Simulation.Base.VillageMap.Demo, _ => { });
            yield return SceneManager.LoadSceneAsync("SampleScene");
            for (var f = 0; f < 6; f++)
            {
                yield return null;
            }

            var game = Object.FindAnyObjectByType<GameController>();
            var document = game.GetComponent<UIDocument>();
            var ui = new RenderTexture(Width, Height, 24);
            document.panelSettings.targetTexture = ui;
            document.panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
            document.panelSettings.scale = Scale;
            document.panelSettings.clearColor = true;
            document.panelSettings.colorClearValue = new Color(0f, 0f, 0f, 0f);
            var hud = (HudView)typeof(GameController).GetField("_hud", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(game);
            hud.SetPortrait(true);
            var world = new RenderTexture(Width, Height, 24) { antiAliasing = 4 };
            Camera.main.aspect = Width / (float)Height;
            for (var f = 0; f < 4; f++)
            {
                yield return null;
            }

            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                var map = document.rootVisualElement.Q<Button>("locked-map");
                submit.target = map;
                map.SendEvent(submit);
            }

            for (var f = 0; f < 6; f++)
            {
                yield return null;
            }

            // The 3D map has its own camera: draw it into the capture and let the land stream in round the village.
            var stage = (WorldStage)typeof(GameController).GetField("_stage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(game);
            stage.Camera.targetTexture = world;
            stage.Camera.aspect = Width / (float)Height;
            yield return new WaitForSecondsRealtime(4f);
            var output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "art", "previews", "intro"));
            yield return null;
            SaveRendered(world, ui, Path.Combine(output, "world_map.png"));
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                var node = document.rootVisualElement.Q<Button>("node-2");
                submit.target = node;
                node.SendEvent(submit);
            }

            for (var f = 0; f < 4; f++)
            {
                yield return null;
            }

            yield return new WaitForSecondsRealtime(1f);
            yield return null;
            SaveRendered(world, ui, Path.Combine(output, "world_map_card.png"));
            stage.Rig.JumpTo(stage.Rig.Focus, MapCamera.MaxDistance);
            yield return new WaitForSecondsRealtime(5f);
            yield return null;
            SaveRendered(world, ui, Path.Combine(output, "world_map_far.png"));

            // The missionaries' roster, then the lord's seat with the tithe.
            var view = (WorldMapView)typeof(GameController).GetField("_map", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(game);
            view.ShowRoster();
            yield return new WaitForSecondsRealtime(0.5f);
            SaveRendered(world, ui, Path.Combine(output, "world_map_roster.png"));
            typeof(GameController).GetMethod("OpenSeat", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(game, null);
            yield return new WaitForSecondsRealtime(3f);
            SaveRendered(world, ui, Path.Combine(output, "world_map_tithe.png"));
        }

        [UnityTest]
        public IEnumerator IntroCapture_Sheets_WritePngs()
        {
            _progress.MarkSeen();
            var clock = new Clock();
            var table = CultOfComfort.Simulation.Base.BuildingTable.Default;
            var state = CultOfComfort.Simulation.Base.BaseState.CreateNew(table, CultOfComfort.Simulation.Base.VillageMap.Demo, clock.UtcNow);
            state.Apply(new CultOfComfort.Simulation.Base.UpgradeHearth(System.Guid.NewGuid()), clock.UtcNow);
            clock.UtcNow = clock.UtcNow.AddHours(2);
            state.Apply(new CultOfComfort.Simulation.Base.DebugFillStores(System.Guid.NewGuid()), clock.UtcNow);
            state.Apply(new CultOfComfort.Simulation.Base.StartResearch(System.Guid.NewGuid(), "sharper-axes"), clock.UtcNow);
            clock.UtcNow = clock.UtcNow.AddMinutes(20);
            var store = new Store { Saved = state.ToData() };
            VillageComposition.Override = () => new LocalBaseGateway(store, clock, table, CultOfComfort.Simulation.Base.VillageMap.Demo, _ => { });
            yield return SceneManager.LoadSceneAsync("SampleScene");
            for (var f = 0; f < 6; f++)
            {
                yield return null;
            }

            var game = Object.FindAnyObjectByType<GameController>();
            var document = game.GetComponent<UIDocument>();
            var ui = new RenderTexture(Width, Height, 24);
            document.panelSettings.targetTexture = ui;
            document.panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
            document.panelSettings.scale = Scale;
            document.panelSettings.clearColor = true;
            document.panelSettings.colorClearValue = new Color(0f, 0f, 0f, 0f);
            var hud = (HudView)typeof(GameController).GetField("_hud", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(game);
            hud.SetPortrait(true);
            var world = new RenderTexture(Width, Height, 24) { antiAliasing = 4 };
            Camera.main.aspect = Width / (float)Height;
            var output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "art", "previews", "intro"));
            foreach (var id in new[] { "research", "shop", "cult" })
            {
                for (var f = 0; f < 4; f++)
                {
                    yield return null;
                }

                using (var submit = NavigationSubmitEvent.GetPooled())
                {
                    var button = document.rootVisualElement.Q<Button>("locked-" + id);
                    submit.target = button;
                    button.SendEvent(submit);
                }

                for (var f = 0; f < 6; f++)
                {
                    yield return null;
                }

                Save(Camera.main, world, ui, Path.Combine(output, $"sheet_{id}.png"));
            }
        }

        private static void Save(Camera camera, RenderTexture world, RenderTexture ui, string path)
        {
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
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(overlay);
        }

        /// <summary>
        /// Like <see cref="Save"/> for a camera that already draws into <paramref name="world"/> every frame (the 3D
        /// map: its instanced forests only reach the camera's own render, not a manual one).
        /// </summary>
        private static void SaveRendered(RenderTexture world, RenderTexture ui, string path)
        {
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
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(overlay);
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

        private sealed class FakeProgress : IIntroProgress
        {
            public bool Seen { get; private set; }

            public void MarkSeen() => Seen = true;
        }
    }
}
