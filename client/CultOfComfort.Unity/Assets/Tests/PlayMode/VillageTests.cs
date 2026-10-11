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

        private static BaseSnapshot Snapshot(GameController game) =>
            (BaseSnapshot)typeof(GameController).GetField("_snapshot", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(game);

        [UnityTest]
        public IEnumerator Drawer_PickWoodcutterThenTapAField_BuildsItAndChargesTimber()
        {
            // Arrange
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return Frames(3);
            var game = UnityEngine.Object.FindAnyObjectByType<GameController>();
            var root = UnityEngine.Object.FindAnyObjectByType<UIDocument>().rootVisualElement;

            // Act
            Click(root.Q<Button>("card-Woodcutter"));
            yield return null;
            Tap(game, new GridPosition(3, 9));
            yield return Frames(2);

            // Assert
            var snapshot = Snapshot(game);
            Assert.IsNotNull(snapshot.BuildingAt(new GridPosition(3, 9)));
            Assert.AreEqual(450, snapshot.Stock.Timber);
            Assert.AreEqual("450/1k", root.Q("pill-timber").Q<Label>(className: "pill-value").text);
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
            yield return null;
            Tap(game, new GridPosition(3, 9));
            yield return null;
            _clock.UtcNow = _clock.UtcNow.AddHours(1).AddSeconds(30);

            // Act
            Tap(game, new GridPosition(3, 9));
            yield return Frames(2);

            // Assert
            Assert.AreEqual(550, Snapshot(game).Stock.Timber);
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
        public IEnumerator Night_BuildingOnTheRoad_RaidersWalkAroundIt()
        {
            // Arrange
            yield return SceneManager.LoadSceneAsync("SampleScene");
            yield return Frames(3);
            var game = UnityEngine.Object.FindAnyObjectByType<GameController>();
            var root = UnityEngine.Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            var before = VillageMap.Demo.ToBattleLayout(Array.Empty<PlacedTower>(), Array.Empty<GridPosition>()).ResolveEnemyPath();
            var onRoad = before[before.Count / 2];
            Click(root.Q<Button>("card-StorageHut"));
            yield return null;
            Tap(game, onRoad);
            yield return Frames(2);

            // Act
            game.StartNight();
            yield return null;
            var battle = (CultOfComfort.Simulation.Combat.Battle)typeof(GameController).GetField("_battle", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(game);

            // Assert
            CollectionAssert.DoesNotContain(battle.Path.ToList(), onRoad);
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
    }
}
