using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Grid;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace CultOfComfort.Client.Tests
{
    public class VillageTests
    {
        private sealed class Clock : IClock { public DateTimeOffset UtcNow { get; set; } = new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero); }

        private sealed class Store : IBaseStore
        {
            public BaseData Saved;
            public System.Threading.Tasks.Task<BaseData> LoadAsync(System.Threading.CancellationToken ct) => System.Threading.Tasks.Task.FromResult(Saved);
            public System.Threading.Tasks.Task SaveAsync(BaseData data, System.Threading.CancellationToken ct) { Saved = data; return System.Threading.Tasks.Task.CompletedTask; }
        }

        private Clock _clock;

        [SetUp]
        public void SetUp()
        {
            _clock = new Clock();
            var store = new Store();
            VillageComposition.Override = () => new LocalBaseGateway(store, _clock, BuildingTable.Default, VillageMap.Demo, _ => { });
        }

        [TearDown]
        public void TearDown() => VillageComposition.Override = null;

        private static IEnumerator Frames(int n) { for (var i = 0; i < n; i++) yield return null; }

        private static void Click(Button b) { using (var e = NavigationSubmitEvent.GetPooled()) { e.target = b; b.SendEvent(e); } }

        private static void Tap(GameController game, GridPosition tile) =>
            typeof(GameController).GetMethod("HandleDayTap", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(game, new object[] { tile });

        private static PlacementTool Tool(GameController game) =>
            (PlacementTool)typeof(GameController).GetField("_tool", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(game);

        /// <summary>Drags the ghost so its anchor lands on <paramref name="tile"/>, as a finger would.</summary>
        private static void GhostTo(GameController game, GridPosition tile)
        {
            var tool = Tool(game);
            tool.TryGrab(tool.Anchor, BuildingTable.Default);
            tool.DragTo(tile);
            tool.Release();
        }

        private static BaseSnapshot Snapshot(GameController game) =>
            (BaseSnapshot)typeof(GameController).GetField("_snapshot", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(game);

        [UnityTest]
        public IEnumerator ErrandNote_BuildTheWoodcutterThenClaim_PaysAndShowsTheNextErrand()
        {
            // Arrange — a fresh village: errand 1 is on the note and the lamps wait.
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return Frames(3);
            var game = UnityEngine.Object.FindAnyObjectByType<GameController>();
            var root = UnityEngine.Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            var note = root.Q<Button>("errand-note");
            Assert.AreEqual(DisplayStyle.Flex, note.resolvedStyle.display);
            StringAssert.Contains("Woodcutter", note.Q<Label>(className: "errand-text").text);
            Assert.AreEqual(DisplayStyle.None, note.Q<Label>(className: "errand-claim").resolvedStyle.display);
            Assert.IsTrue(note.ClassListContains("hushed"), "one voice: the coach points at the card, the note waits");
            Assert.IsTrue(root.Q<Button>("start-night").ClassListContains("waiting"), "the lamps wait for the first-Night errand");
            Click(root.Q<Button>("card-Woodcutter"));
            yield return Frames(2);
            GhostTo(game, new GridPosition(3, 9));
            Click(root.Q<Button>("ghost-confirm"));
            _clock.UtcNow = _clock.UtcNow.AddMinutes(1); // the Lean-to takes 30 s
            yield return new WaitForSecondsRealtime(1.2f); // the village is re-read twice a second
            StringAssert.Contains("claim", note.Q<Label>(className: "errand-text").text, "done: the note (one line, folded) asks to claim");
            var timber = Snapshot(game).Stock.Timber;

            // Act
            Click(note);
            yield return Frames(3);

            // Assert
            Assert.AreEqual(1, Snapshot(game).Errand.Index);
            Assert.AreEqual(timber + 50, Snapshot(game).Stock.Timber);
            StringAssert.Contains("Storage Hut", note.Q<Label>(className: "errand-text").text);
        }

        private static HudView Hud(GameController game) =>
            (HudView)typeof(GameController).GetField("_hud", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(game);

        private static bool Near(Rect arrow, VisualElement target) =>
            Mathf.Abs(arrow.center.x - target.worldBound.center.x) < 30f && Mathf.Abs(arrow.center.y - target.worldBound.center.y) < 120f;

        [UnityTest]
        public IEnumerator Coach_TapTheNote_StepsAsideUntilTheNextStep()
        {
            // Arrange — a fresh village; the coach points at the Lean-to card.
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return Frames(4);
            var game = UnityEngine.Object.FindAnyObjectByType<GameController>();
            var root = UnityEngine.Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            var line = root.Q<Label>("coach-line");

            // Act: tap the coach's note away.
            using (var e = ClickEvent.GetPooled()) { e.target = line; line.SendEvent(e); }
            yield return Frames(2);

            // Assert — quiet now, and the errand note is back…
            Assert.IsFalse(Hud(game).CoachShown.Line);
            Assert.IsFalse(root.Q<Button>("errand-note").ClassListContains("hushed"));

            // …but not gone for good: the next step brings him back.
            Click(root.Q<Button>("card-Woodcutter"));
            yield return Frames(3);
            Assert.IsTrue(Hud(game).CoachShown.Line, "picking the card: the coach says where to put it");
        }

        [UnityTest]
        public IEnumerator Coach_FirstErrand_PointsAtCardThenConfirmThenNote()
        {
            // Arrange — a fresh village.
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return Frames(4);
            var game = UnityEngine.Object.FindAnyObjectByType<GameController>();
            var root = UnityEngine.Object.FindAnyObjectByType<UIDocument>().rootVisualElement;

            // Assert: the arrow is on the Lean-to card.
            var shown = Hud(game).CoachShown;
            Assert.IsTrue(shown.Arrow, "a new player gets an arrow");
            StringAssert.Contains("Woodcutter", shown.Text);
            Assert.IsTrue(Near(shown.ArrowBounds, root.Q<Button>("card-Woodcutter")), $"arrow {shown.ArrowBounds} over the card {root.Q<Button>("card-Woodcutter").worldBound}");
            Click(root.Q<Button>("settings-button"));
            yield return Frames(2);
            Assert.IsFalse(Hud(game).CoachShown.Arrow, "the coach waits behind an open sheet");
            Assert.IsFalse(Hud(game).CoachShown.Line);
            Click(root.Q<Button>("settings-button"));
            yield return Frames(2);

            // Act: pick it — the arrow moves to ✓.
            Click(root.Q<Button>("card-Woodcutter"));
            yield return Frames(3);
            shown = Hud(game).CoachShown;
            Assert.IsTrue(Near(shown.ArrowBounds, root.Q<Button>("ghost-confirm")), "arrow over ✓");

            // Act: build it and let it finish — the arrow goes to the note.
            GhostTo(game, new GridPosition(3, 9));
            Click(root.Q<Button>("ghost-confirm"));
            yield return Frames(2);
            Assert.IsFalse(Hud(game).CoachShown.Arrow, "while it is being built there is nothing to tap");
            _clock.UtcNow = _clock.UtcNow.AddMinutes(1);
            yield return new WaitForSecondsRealtime(1.2f);
            shown = Hud(game).CoachShown;
            Assert.IsTrue(Near(shown.ArrowBounds, root.Q<Button>("errand-note")), "arrow at the note: claim it");
            Assert.IsFalse(shown.Line, "the note says it already: the arrow alone");

            // Act: claim it — the next card is on the Homes tab, which opens for it.
            Click(root.Q<Button>("errand-note"));
            yield return Frames(4);
            shown = Hud(game).CoachShown;
            StringAssert.Contains("Storage Hut", shown.Text);
            Assert.IsTrue(Near(shown.ArrowBounds, root.Q<Button>("card-StorageHut")), $"arrow {shown.ArrowBounds} over the hut {root.Q<Button>("card-StorageHut").worldBound}");
        }

        [UnityTest]
        public IEnumerator Night_HearthTwo_RelicButtonsShowAndSayTheyFireThemselves()
        {
            // Arrange — a village whose Hearth reached level 2 (the upgrade finished while we were away).
            var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, _clock.UtcNow);
            Assert.AreEqual(CommandStatus.Accepted, state.Apply(new UpgradeHearth(Guid.NewGuid()), _clock.UtcNow).Status);
            var store = new Store { Saved = state.ToData() };
            _clock.UtcNow = _clock.UtcNow.AddHours(1);
            VillageComposition.Override = () => new LocalBaseGateway(store, _clock, BuildingTable.Default, VillageMap.Demo, _ => { });
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return Frames(4);
            var game = UnityEngine.Object.FindAnyObjectByType<GameController>();
            Assert.AreEqual(2, Snapshot(game).HearthLevel);
            var root = UnityEngine.Object.FindAnyObjectByType<UIDocument>().rootVisualElement;

            // Act
            game.StartNight();
            var until = Time.realtimeSinceStartup + 10f;
            while (game.Phase != GamePhase.Night && Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            yield return Frames(3);
            var battleNow = (CultOfComfort.Simulation.Combat.Battle)typeof(GameController).GetField("_battle", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(game);
            var clickedAt = battleNow.Tick;
            Click(root.Q<Button>("torch"));
            var ticked = Time.realtimeSinceStartup + 5f;
            while (battleNow.Tick < clickedAt + 2 && Time.realtimeSinceStartup < ticked)
            {
                yield return null; // the input applies on the next simulated tick, not the next frame
            }

            // Assert
            Assert.AreEqual(DisplayStyle.Flex, root.Q("relics").resolvedStyle.display, "Hearth 2 found the laser pointer and the torch");
            Assert.AreEqual(DisplayStyle.Flex, root.Q<Button>("laser").resolvedStyle.display);
            Assert.AreEqual(DisplayStyle.None, root.Q<Button>("magnifier").resolvedStyle.display, "the glass waits for Hearth 3");
            var battle = (CultOfComfort.Simulation.Combat.Battle)typeof(GameController).GetField("_battle", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(game);
            Assert.IsTrue(battle.AutoRelics, "tonight's relics fire themselves (owner 2026-10-15)");
            Assert.IsFalse(battle.InputLog.Any(i => i.Type == CultOfComfort.Simulation.Combat.PlayerActionType.DynamoTorch), "the button explains; it doesn't fire");
            StringAssert.Contains("auto", root.Q<Button>("torch").Q<Label>().text);
        }

        [UnityTest]
        public IEnumerator Map_HearthTwo_OpenTapAPlaceAndSendAMissionary()
        {
            // Arrange — Hearth 2 (the upgrade finished while away), stores full.
            var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, _clock.UtcNow);
            state.Apply(new UpgradeHearth(Guid.NewGuid()), _clock.UtcNow);
            state.Apply(new DebugFillStores(Guid.NewGuid()), _clock.UtcNow.AddHours(1));
            var store = new Store { Saved = state.ToData() };
            _clock.UtcNow = _clock.UtcNow.AddHours(1);
            VillageComposition.Override = () => new LocalBaseGateway(store, _clock, BuildingTable.Default, VillageMap.Demo, _ => { });
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return Frames(4);
            var game = UnityEngine.Object.FindAnyObjectByType<GameController>();
            var root = UnityEngine.Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            Assert.IsFalse(root.Q<Button>("locked-map").ClassListContains("locked"), "the Map is open at Hearth 2");

            // Act — open the map, tap Muddlecombe, preach for two hours.
            Click(root.Q<Button>("locked-map"));
            yield return Frames(3);
            Assert.AreEqual(DisplayStyle.Flex, root.Q("world-map").resolvedStyle.display);
            Click(root.Q<Button>("node-1"));
            yield return Frames(2);
            Click(root.Q<Button>("preach-2"));
            yield return new WaitForSecondsRealtime(1.2f);

            // Assert
            var domain = Snapshot(game).Domain;
            Assert.AreEqual(1, domain.Missionaries.Count, "a missionary is on the road");
            Assert.AreEqual(1, domain.Missionaries[0].NodeId);
            StringAssert.Contains("Muddlecombe", root.Q<Label>("map-missionaries").text);
        }

        [UnityTest]
        public IEnumerator Drawer_PickWoodcutterGhostThenConfirm_BuildsItAndChargesTimber()
        {
            // Arrange
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return Frames(3);
            var game = UnityEngine.Object.FindAnyObjectByType<GameController>();
            var root = UnityEngine.Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            Click(root.Q<Button>("card-Woodcutter"));
            yield return Frames(2);
            GhostTo(game, new GridPosition(3, 9));
            yield return null;
            Assert.IsNull(Snapshot(game).BuildingAt(new GridPosition(3, 9)), "a ghost isn't a building");
            Assert.AreEqual(DisplayStyle.Flex, root.Q("ghost-actions").resolvedStyle.display);

            // Act
            Click(root.Q<Button>("ghost-confirm"));
            yield return Frames(2);

            // Assert
            var snapshot = Snapshot(game);
            Assert.IsNotNull(snapshot.BuildingAt(new GridPosition(3, 9)));
            Assert.AreEqual(450, snapshot.Stock.Timber);
            Assert.AreEqual("450/1k", root.Q("pill-timber").Q<Label>(className: "pill-value").text);
            Assert.AreEqual(DisplayStyle.None, root.Q("ghost-actions").resolvedStyle.display, "the ghost is gone");
        }

        [UnityTest]
        public IEnumerator Drawer_PickThenCancelOrTapTheGround_BuildsNothing()
        {
            // Arrange
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return Frames(3);
            var game = UnityEngine.Object.FindAnyObjectByType<GameController>();
            var root = UnityEngine.Object.FindAnyObjectByType<UIDocument>().rootVisualElement;

            // Act — ✗, then a stray tap on empty ground.
            Click(root.Q<Button>("card-Cottage"));
            yield return Frames(2);
            Click(root.Q<Button>("ghost-cancel"));
            yield return Frames(2);
            var afterCancel = Tool(game);
            Click(root.Q<Button>("card-Cottage"));
            yield return Frames(2);
            Tap(game, new GridPosition(25, 15));
            yield return Frames(2);

            // Assert
            Assert.IsNull(afterCancel);
            Assert.IsNull(Tool(game), "tapping empty ground puts the ghost away");
            Assert.IsFalse(Snapshot(game).Buildings.Any(b => b.Type == BuildingType.Cottage));
        }

        [UnityTest]
        public IEnumerator Board_TapAProducerWithOutput_CollectsIt()
        {
            // Arrange
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return Frames(3);
            var game = UnityEngine.Object.FindAnyObjectByType<GameController>();
            var root = UnityEngine.Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            Click(root.Q<Button>("card-Woodcutter"));
            yield return Frames(2);
            GhostTo(game, new GridPosition(3, 9));
            Click(root.Q<Button>("ghost-confirm"));
            yield return Frames(2);
            _clock.UtcNow = _clock.UtcNow.AddHours(1).AddSeconds(30);

            // Act
            Tap(game, new GridPosition(3, 9));
            yield return Frames(2);

            // Assert
            Assert.AreEqual(575, Snapshot(game).Stock.Timber); // 450 + an hour (100) + the 25 starter pile
        }

        [UnityTest]
        public IEnumerator HearthCard_UpgradeAndFinishNow_ReachesLevelTwo()
        {
            // Arrange — enough Timber: collect isn't needed, the save is seeded rich.
            var store = new Store
            {
                Saved = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, _clock.UtcNow).ToData() with { Stock = new ResourceBag(900, 60, 380, 0) },
            };
            VillageComposition.Override = () => new LocalBaseGateway(store, _clock, BuildingTable.Default, VillageMap.Demo, _ => { });
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return Frames(3);
            var game = UnityEngine.Object.FindAnyObjectByType<GameController>();
            var root = UnityEngine.Object.FindAnyObjectByType<UIDocument>().rootVisualElement;

            // Act
            Tap(game, VillageMap.Demo.Hearth);
            yield return Frames(2);
            Click(root.Q<Button>("upgrade"));
            yield return Frames(2);
            Click(root.Q<Button>("finish-now"));
            yield return Frames(2);

            // Assert
            Assert.AreEqual(2, Snapshot(game).HearthLevel);
            Assert.AreEqual("2", root.Q<Label>("hearth-badge").text);
        }

        [UnityTest]
        public IEnumerator Board_BuildOnTheRoad_RefusedAndTheNightKeepsTheRoad()
        {
            // Arrange — the raiders' road is the valley's old track: nobody builds on it, so it never moves.
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return Frames(3);
            var game = UnityEngine.Object.FindAnyObjectByType<GameController>();
            var root = UnityEngine.Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            var road = VillageMap.Demo.Road;
            var onRoad = road[road.Count / 2];
            Click(root.Q<Button>("card-WattleFence"));
            yield return null;

            // Act
            Tap(game, onRoad);
            yield return Frames(2);
            game.StartNight();
            yield return null;
            var battle = (CultOfComfort.Simulation.Combat.Battle)typeof(GameController).GetField("_battle", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(game);

            // Assert
            Assert.IsNull(Snapshot(game).BuildingAt(onRoad));
            CollectionAssert.AreEqual(road.ToList(), battle.Path.ToList());
        }
    
        [UnityTest]
        public IEnumerator Launch_SaveWithThreeNightsPlayed_NextNightIsFour()
        {
            // Arrange
            var store = new Store { Saved = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, _clock.UtcNow).ToData() with { LastNightRecorded = 3 } };
            VillageComposition.Override = () => new LocalBaseGateway(store, _clock, BuildingTable.Default, VillageMap.Demo, _ => { });

            // Act
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return Frames(3);
            var game = UnityEngine.Object.FindAnyObjectByType<GameController>();

            // Assert
            Assert.AreEqual(4, (int)typeof(GameController).GetField("_night", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(game));
        }

        [UnityTest]
        public IEnumerator Board_TapAProducerWhileStorageIsFull_OpensItsCard()
        {
            // Arrange — a finished Woodcutter with output ready and Timber already at the cap.
            var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, _clock.UtcNow);
            state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, new GridPosition(3, 9)), _clock.UtcNow);
            state.Apply(new FinishConstructionNow(Guid.NewGuid()), _clock.UtcNow);
            var store = new Store { Saved = state.ToData() with { Stock = new ResourceBag(1000, 60, 380, 0) } };
            VillageComposition.Override = () => new LocalBaseGateway(store, _clock, BuildingTable.Default, VillageMap.Demo, _ => { });
            _clock.UtcNow = _clock.UtcNow.AddHours(2);
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return Frames(3);
            var game = UnityEngine.Object.FindAnyObjectByType<GameController>();
            var root = UnityEngine.Object.FindAnyObjectByType<UIDocument>().rootVisualElement;

            // Act
            Tap(game, new GridPosition(3, 9));
            yield return Frames(3);

            // Assert
            Assert.AreEqual(DisplayStyle.Flex, root.Q("building-card").resolvedStyle.display, "you must still be able to upgrade or demolish it");
        }
    
        [UnityTest]
        public IEnumerator Card_RepairAWreckedBuilding_FixesIt()
        {
            // Arrange — a finished Woodcutter, wrecked by last Night.
            var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, _clock.UtcNow);
            state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, new GridPosition(3, 9)), _clock.UtcNow);
            state.Apply(new FinishConstructionNow(Guid.NewGuid()), _clock.UtcNow);
            var id = state.Snapshot(_clock.UtcNow).Buildings.Single(b => b.Type == BuildingType.Woodcutter).Id;
            state.Apply(new RecordNightResult(Guid.NewGuid(), 1, 0, new[] { id }), _clock.UtcNow);
            var store = new Store { Saved = state.ToData() };
            VillageComposition.Override = () => new LocalBaseGateway(store, _clock, BuildingTable.Default, VillageMap.Demo, _ => { });
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return Frames(3);
            var game = UnityEngine.Object.FindAnyObjectByType<GameController>();
            var root = UnityEngine.Object.FindAnyObjectByType<UIDocument>().rootVisualElement;

            // Act
            Tap(game, new GridPosition(3, 9));
            yield return Frames(2);
            Click(root.Q<Button>("repair"));
            yield return Frames(2);

            // Assert
            var snapshot = Snapshot(game);
            Assert.IsFalse(snapshot.Building(id).Wrecked);
            Assert.AreEqual(450 + 25 - 5, snapshot.Stock.Timber, "the starter pile went into the stores; the repair charged 5 Timber (10% of 50)");
        }
    
        [UnityTest]
        public IEnumerator Launch_RaidPlayedItselfWhileAway_ShowsTheReportOnce()
        {
            // Arrange — a raid announced long ago; its deadline passed while the app was closed.
            var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, _clock.UtcNow);
            state.Apply(new DebugRaiseTension(Guid.NewGuid(), 75), _clock.UtcNow);
            var store = new Store { Saved = state.ToData() };
            VillageComposition.Override = () => new LocalBaseGateway(store, _clock, BuildingTable.Default, VillageMap.Demo, _ => { });
            _clock.UtcNow = _clock.UtcNow.AddDays(2);

            // Act
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return Frames(4);
            var game = UnityEngine.Object.FindAnyObjectByType<GameController>();
            var root = UnityEngine.Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            var shown = root.Q("card").resolvedStyle.display == DisplayStyle.Flex;
            var title = root.Q("card").Q<Label>(className: "card-title").text;
            Click(root.Q("card").Q<Button>(className: "primary"));
            yield return Frames(3);

            // Assert
            Assert.IsTrue(shown);
            StringAssert.StartsWith("While you were away", title);
            Assert.IsNull(Snapshot(game).UnseenReport, "acknowledged");
            Assert.AreEqual(GamePhase.Dusk, game.Phase);
        }

        [UnityTest]
        public IEnumerator Night_FoughtThroughTheVillage_IsVerifiedByItsReplay()
        {
            // Arrange
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return Frames(3);
            var game = UnityEngine.Object.FindAnyObjectByType<GameController>();
            Time.timeScale = 20f;

            // Act
            game.StartNight();
            var timeout = Time.realtimeSinceStartup + 120f;
            while (game.Phase != GamePhase.Dawn && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            yield return Frames(3);
            Time.timeScale = 1f;

            // Assert
            Assert.AreEqual(GamePhase.Dawn, game.Phase);
            Assert.AreEqual(true, game.LastNightVerified);
            Assert.AreEqual(1, Snapshot(game).LastNightRecorded, "the village resolved it, not the client");
            Assert.IsNotNull(Snapshot(game).ShieldEndsAtUtc);
        }

        [UnityTest]
        public IEnumerator Settings_DevRaiseTension_ShowsTheRaidCountdown()
        {
            // Arrange
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return Frames(3);
            var root = UnityEngine.Object.FindAnyObjectByType<UIDocument>().rootVisualElement;

            // Act
            Click(root.Q<Button>("dev-raise-tension"));
            yield return Frames(2);
            Click(root.Q<Button>("dev-raise-tension"));
            yield return Frames(3);

            // Assert
            var note = root.Q("tension-strip").Q<Label>(className: "marker-note").text; // +50 twice = 100: a Punitive raid
            Assert.IsTrue(note.Contains("Wulfric's men are coming") || note.Contains("Punitive raid"), note);
            StringAssert.StartsWith("Face the raid", root.Q<Button>("start-night").text);
        }
    
        [UnityTest]
        public IEnumerator Board_TapAWildTreeAndCutIt_GivesTimber()
        {
            // Arrange
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return Frames(3);
            var game = UnityEngine.Object.FindAnyObjectByType<GameController>();
            var root = UnityEngine.Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            var tree = VillageMap.Demo.WildTrees.First(t => VillageMap.Demo.IsOpen(t, 1));

            // Act
            Tap(game, tree);
            yield return Frames(2);
            var title = root.Q<Label>("building-title").text;
            Click(root.Q<Button>("upgrade"));
            yield return Frames(2);

            // Assert
            Assert.AreEqual("Wild tree", title);
            Assert.AreEqual(515, Snapshot(game).Stock.Timber);
            CollectionAssert.Contains(Snapshot(game).ClearedTerrain.ToList(), tree);
        }

        [UnityTest]
        public IEnumerator Drawer_RotateTheGhostThenConfirm_StandsTurned()
        {
            // Arrange
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return Frames(3);
            var game = UnityEngine.Object.FindAnyObjectByType<GameController>();
            var root = UnityEngine.Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            Click(root.Q<Button>("card-Woodcutter"));
            yield return Frames(2);
            Assert.AreEqual(DisplayStyle.Flex, root.Q<Button>("ghost-rotate").resolvedStyle.display, "↻ shows for things that turn");

            // Act
            Click(root.Q<Button>("ghost-rotate"));
            GhostTo(game, new GridPosition(13, 5));
            yield return null;
            Click(root.Q<Button>("ghost-confirm"));
            yield return Frames(2);

            // Assert
            var lean = Snapshot(game).BuildingAt(new GridPosition(13, 6));
            Assert.IsNotNull(lean, "a turned lean-to runs north–south");
            Assert.IsTrue(lean.Rotated);
        }

        [UnityTest]
        public IEnumerator Drawer_SquareBuilding_CanTurnToo()
        {
            // Arrange
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return Frames(3);
            var game = UnityEngine.Object.FindAnyObjectByType<GameController>();
            var root = UnityEngine.Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            Hud(game).ShowBuildTab("Homes");
            yield return null;

            // Act
            Click(root.Q<Button>("card-StorageHut"));
            yield return Frames(2);

            // Assert
            Assert.AreEqual(DisplayStyle.Flex, root.Q<Button>("ghost-rotate").resolvedStyle.display, "a 2×2 hut can face another way");
        }

        [UnityTest]
        public IEnumerator Card_Move_SetsTheBuildingDownElsewhere()
        {
            // Arrange — a lean-to by the road; open its card.
            var store = new Store();
            var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, _clock.UtcNow);
            state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, new GridPosition(15, 5)), _clock.UtcNow);
            store.Saved = state.ToData();
            VillageComposition.Override = () => new LocalBaseGateway(store, _clock, BuildingTable.Default, VillageMap.Demo, _ => { });
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return Frames(3);
            var game = UnityEngine.Object.FindAnyObjectByType<GameController>();
            var root = UnityEngine.Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            Tap(game, new GridPosition(15, 5));
            yield return Frames(2);

            // Act
            Click(root.Q<Button>("move"));
            yield return null;
            GhostTo(game, new GridPosition(13, 6));
            Click(root.Q<Button>("ghost-confirm"));
            yield return Frames(2);

            // Assert
            var snapshot = Snapshot(game);
            Assert.IsNull(snapshot.BuildingAt(new GridPosition(15, 5)));
            Assert.AreEqual(BuildingType.Woodcutter, snapshot.BuildingAt(new GridPosition(13, 6))?.Type);
        }

        [UnityTest]
        public IEnumerator RepairAll_Wrecks_OneTapFixesThem()
        {
            // Arrange — two wrecked fences.
            var store = new Store();
            var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, _clock.UtcNow);
            state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.WattleFence, new GridPosition(13, 6)), _clock.UtcNow);
            state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.WattleFence, new GridPosition(14, 6)), _clock.UtcNow);
            state.Apply(new RecordNightResult(Guid.NewGuid(), 1, 0, state.Snapshot(_clock.UtcNow).Buildings.Where(b => b.Type == BuildingType.WattleFence).Select(b => b.Id).ToList()), _clock.UtcNow);
            store.Saved = state.ToData();
            VillageComposition.Override = () => new LocalBaseGateway(store, _clock, BuildingTable.Default, VillageMap.Demo, _ => { });
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return Frames(3);
            var root = UnityEngine.Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            var game = UnityEngine.Object.FindAnyObjectByType<GameController>();
            var button = root.Q<Button>("repair-all");
            Assert.AreEqual("Repair all (2) · 4 Timber", button.text);

            // Act
            Click(button);
            yield return Frames(2);

            // Assert
            Assert.IsFalse(Snapshot(game).Buildings.Any(b => b.Wrecked));
            Assert.AreEqual(DisplayStyle.None, button.resolvedStyle.display);
        }

        [UnityTest]
        public IEnumerator Drawer_PickATrackAndTapTiles_LaysRoadAndStaysSelected()
        {
            // Arrange
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return Frames(3);
            var game = UnityEngine.Object.FindAnyObjectByType<GameController>();
            var root = UnityEngine.Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            Click(root.Q<Button>("card-DirtTrack"));
            yield return null;

            Assert.AreEqual(DisplayStyle.Flex, root.Q<Button>("paint-done").resolvedStyle.display, "Done ends the paint tool");

            // Act — roads stay selected: tap, tap.
            Tap(game, new GridPosition(13, 6));
            yield return Frames(2);
            Tap(game, new GridPosition(14, 6));
            yield return Frames(2);

            // Assert
            var snapshot = Snapshot(game);
            Assert.AreEqual(BuildingType.DirtTrack, snapshot.BuildingAt(new GridPosition(13, 6))?.Type);
            Assert.AreEqual(BuildingType.DirtTrack, snapshot.BuildingAt(new GridPosition(14, 6))?.Type);
        }

        [UnityTest]
        public IEnumerator Board_DragWithATrackSelected_PaintsAlongTheFinger()
        {
            // Arrange
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return Frames(3);
            var game = UnityEngine.Object.FindAnyObjectByType<GameController>();
            var root = UnityEngine.Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            Click(root.Q<Button>("card-DirtTrack"));
            yield return null;

            // Act — a stroke over (12,6)…(15,6), as the finger would leave it.
            var paint = typeof(GameController).GetMethod("PaintStroke", BindingFlags.NonPublic | BindingFlags.Instance);
            paint.Invoke(game, new object[] { new[] { new GridPosition(12, 6), new GridPosition(15, 6) } });
            yield return Frames(3);

            // Assert — the gap between the two points is filled in.
            var snapshot = Snapshot(game);
            foreach (var x in new[] { 12, 13, 14, 15 })
            {
                Assert.AreEqual(BuildingType.DirtTrack, snapshot.BuildingAt(new GridPosition(x, 6))?.Type, $"tile ({x},6)");
            }
        }
    }
}
