using CultOfComfort.Simulation.Balance;
using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Combat;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Tests.Base;

/// <summary>Spec 2026-10-20 defence: tower levels and a cap, the Guardhouse, and more raiders' roads.</summary>
public class DefenceTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);
    private static readonly VillageMap Map = VillageMap.Demo;

    private static BaseState Village(int hearth)
    {
        var state = BaseState.CreateNew(BuildingTable.Default, Map, T0);
        for (var level = 2; level <= hearth; level++)
        {
            state.Apply(new DebugFillStores(Guid.NewGuid()), T0);
            state.Apply(new UpgradeHearth(Guid.NewGuid()), T0).Status.Should().Be(CommandStatus.Accepted);
            state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);
        }

        state.Apply(new DebugFillStores(Guid.NewGuid()), T0);
        return state;
    }

    private static CommandOutcome Place(BaseState state, BuildingType type, int x, int y) =>
        state.Apply(new PlaceBuilding(Guid.NewGuid(), type, new GridPosition(x, y)), T0);

    private static int IdAt(BaseState state, int x, int y) => state.Snapshot(T0).BuildingAt(new GridPosition(x, y))!.Id;

    // ---------------- towers ----------------

    [Fact]
    public void TowerAt_LevelThreeManned_ReachesFurtherHitsHarderFiresFaster()
    {
        // Arrange
        var balance = BalanceTable.Default;
        var plain = balance.Towers[TowerType.SaplingFlinger];

        // Act
        var top = balance.TowerAt(TowerType.SaplingFlinger, 3, manned: true);

        // Assert
        top.Range.Should().Be(plain.Range + CultOfComfort.Simulation.Numerics.Fix.FromInt(2));
        top.Damage.Should().Be(plain.Damage * 160 / 100);
        top.CooldownTicks.Should().Be(plain.CooldownTicks * 65 / 100);
        balance.TowerAt(TowerType.SaplingFlinger, 1, manned: false).Should().BeSameAs(plain, "level 1 unmanned: old Nights replay as they were");
    }

    [Fact]
    public void Upgrade_TowerAtHearthTwo_LevelTwoAtOnceIntoTheNightPlan()
    {
        // Arrange
        var state = Village(2);
        Place(state, BuildingType.SlurryChute, 16, 6).Status.Should().Be(CommandStatus.Accepted);
        var id = IdAt(state, 16, 6);

        // Act
        var outcome = state.Apply(new UpgradeBuilding(Guid.NewGuid(), id), T0);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Accepted);
        state.Snapshot(T0).Building(id).Level.Should().Be(2, "towers upgrade at once, no builder");
        state.Apply(new BeginNight(Guid.NewGuid()), T0).Plan!.Towers.Single(t => t.Position == new GridPosition(16, 6)).Level.Should().Be(2);
    }

    [Fact]
    public void Upgrade_TowerToLevelThreeAtHearthTwo_Rejected()
    {
        // Arrange
        var state = Village(2);
        Place(state, BuildingType.SlurryChute, 16, 6);
        var id = IdAt(state, 16, 6);
        state.Apply(new UpgradeBuilding(Guid.NewGuid(), id), T0);
        state.Apply(new DebugFillStores(Guid.NewGuid()), T0);

        // Act
        var outcome = state.Apply(new UpgradeBuilding(Guid.NewGuid(), id), T0);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Rejected);
        outcome.Reason.Should().Contain("Hearth to level 3");
    }

    private static readonly (int X, int Y)[] EightSpots = { (6, 2), (6, 3), (6, 4), (6, 6), (7, 2), (7, 3), (7, 4), (7, 6) };

    [Fact]
    public void Place_TowerPastTheCap_Rejected()
    {
        // Arrange — eight towers at Hearth 1, its cap.
        var state = Village(1);
        foreach (var (x, y) in EightSpots)
        {
            Place(state, BuildingType.SlurryChute, x, y).Status.Should().Be(CommandStatus.Accepted);
            state.Apply(new DebugFillStores(Guid.NewGuid()), T0);
        }

        // Act
        var outcome = Place(state, BuildingType.SlurryChute, 9, 2);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Rejected);
        outcome.Reason.Should().Contain("8 towers");
    }

    // ---------------- packing up (spec 2026-10-20 river) ----------------

    [Fact]
    public void PackUpTower_ThenSetDown_FreeAndAtItsLevel()
    {
        // Arrange — a level 2 chute, packed up.
        var state = Village(2);
        Place(state, BuildingType.SlurryChute, 16, 6);
        var id = IdAt(state, 16, 6);
        state.Apply(new UpgradeBuilding(Guid.NewGuid(), id), T0);
        state.Apply(new PackUpTower(Guid.NewGuid(), id), T0).Status.Should().Be(CommandStatus.Accepted);
        var stock = state.Snapshot(T0).Stock;

        // Act
        var outcome = Place(state, BuildingType.SlurryChute, 6, 3);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Accepted);
        var snapshot = state.Snapshot(T0);
        snapshot.BuildingAt(new GridPosition(6, 3))!.Level.Should().Be(2);
        snapshot.Stock.Should().Be(stock, "a packed-up tower costs nothing to set down");
        snapshot.Stowed.Should().BeEmpty();
    }

    [Fact]
    public void PackUpTowers_All_NoneStandingAllKeptAndSaved()
    {
        // Arrange
        var state = Village(2);
        Place(state, BuildingType.SlurryChute, 16, 6);
        Place(state, BuildingType.SaplingFlinger, 6, 3);

        // Act
        state.Apply(new PackUpTowers(Guid.NewGuid()), T0).Status.Should().Be(CommandStatus.Accepted);

        // Assert
        var restored = BaseState.Restore(BuildingTable.Default, Map, state.ToData()).Snapshot(T0);
        restored.Buildings.Should().NotContain(b => BuildingTable.Default.Spec(b.Type).IsTower);
        restored.Stowed!.Select(t => t.Type).Should().BeEquivalentTo(new[] { BuildingType.SlurryChute, BuildingType.SaplingFlinger });
        state.Apply(new BeginNight(Guid.NewGuid()), T0).Plan!.Towers.Should().BeEmpty("packed-up towers don't fight");
    }

    [Fact]
    public void Place_PackedUpTowersCountTowardsTheCap()
    {
        // Arrange — eight towers at Hearth 1, all packed up.
        var state = Village(1);
        foreach (var (x, y) in EightSpots)
        {
            Place(state, BuildingType.SlurryChute, x, y);
            state.Apply(new DebugFillStores(Guid.NewGuid()), T0);
        }

        state.Apply(new PackUpTowers(Guid.NewGuid()), T0);

        // Act & Assert — a packed-up one may go back; a ninth may not be bought.
        Place(state, BuildingType.SlurryChute, 9, 2).Status.Should().Be(CommandStatus.Accepted);
        state.Apply(new PackUpTowers(Guid.NewGuid()), T0);
        state.Apply(new DebugFillStores(Guid.NewGuid()), T0);
        state.Snapshot(T0).Stowed.Should().HaveCount(8);
    }

    [Fact]
    public void PackUpTower_NotATower_Rejected()
    {
        // Arrange
        var state = Village(2);
        var hearth = state.Snapshot(T0).Buildings.Single(b => b.Type == BuildingType.Hearth).Id;

        // Act & Assert
        state.Apply(new PackUpTower(Guid.NewGuid(), hearth), T0).Status.Should().Be(CommandStatus.Rejected);
        state.Apply(new PackUpTowers(Guid.NewGuid()), T0).Status.Should().Be(CommandStatus.Rejected, "nothing to pack");
    }

    // ---------------- the Guardhouse ----------------

    private static (BaseState State, int House) Garrisoned()
    {
        var state = Village(2);
        Place(state, BuildingType.Guardhouse, 20, 8).Status.Should().Be(CommandStatus.Accepted);
        state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);
        Place(state, BuildingType.SlurryChute, 16, 6).Status.Should().Be(CommandStatus.Accepted);  // 4 tiles away
        Place(state, BuildingType.SlurryChute, 6, 3).Status.Should().Be(CommandStatus.Accepted);   // far out of reach
        return (state, IdAt(state, 20, 8));
    }

    [Fact]
    public void PlanNight_GuardhouseOnDuty_MansTheTowerInReachAndSendsTheRestOut()
    {
        // Arrange
        var (state, house) = Garrisoned();
        state.Snapshot(T0).Building(house).Crew.Should().Be(2, "two wardens at level 1");

        // Act
        var plan = state.Apply(new BeginNight(Guid.NewGuid()), T0).Plan!;

        // Assert
        plan.Towers.Single(t => t.Position == new GridPosition(16, 6)).Manned.Should().BeTrue();
        plan.Towers.Single(t => t.Position == new GridPosition(6, 3)).Manned.Should().BeFalse("eight tiles is as far as a warden goes");
        plan.Defenders.Should().HaveCount(1, "the warden with no tower in reach comes out to fight");
    }

    [Fact]
    public void PlanNight_GuardhouseOnPatrol_EveryWardenFightsNoTowerManned()
    {
        // Arrange
        var (state, house) = Garrisoned();
        state.Apply(new SetDuty(Guid.NewGuid(), house, Patrol: true), T0).Status.Should().Be(CommandStatus.Accepted);

        // Act
        var plan = state.Apply(new BeginNight(Guid.NewGuid()), T0).Plan!;

        // Assert
        plan.Towers.Should().OnlyContain(t => !t.Manned);
        plan.Defenders.Should().HaveCount(2);
        BaseState.Restore(BuildingTable.Default, Map, state.ToData()).Snapshot(T0).Building(house).Patrol.Should().BeTrue("the duty is saved");
    }

    [Fact]
    public void SetDuty_NotAGuardhouse_Rejected()
    {
        // Arrange
        var state = Village(2);
        Place(state, BuildingType.SlurryChute, 16, 6);

        // Act & Assert
        state.Apply(new SetDuty(Guid.NewGuid(), IdAt(state, 16, 6), true), T0).Status.Should().Be(CommandStatus.Rejected);
    }

    [Fact]
    public void Battle_MannedTower_FiresMoreOften()
    {
        // Arrange — the same Night twice, the tower manned in one.
        var (state, _) = Garrisoned();
        var plan = state.Apply(new BeginNight(Guid.NewGuid()), T0).Plan!;
        var unmanned = plan with { Towers = plan.Towers.Select(t => t with { Manned = false }).ToList(), Defenders = Array.Empty<GridPosition>() };
        var manned = unmanned with { Towers = plan.Towers };

        int Shots(NightPlan p)
        {
            var battle = new Battle(p.ToBattleSetup(Map, Array.Empty<PlayerInput>()));
            var shots = 0;
            while (!battle.IsFinished && battle.Tick < 4000)
            {
                battle.Step();
                shots += battle.LastTickEvents.OfType<TowerFired>().Count(f => f.TowerId == 1 + p.Towers.ToList().FindIndex(t => t.Position == new GridPosition(16, 6)));
            }

            return shots;
        }

        // Act & Assert
        Shots(manned).Should().BeGreaterThan(Shots(unmanned));
    }

    // ---------------- roads ----------------

    [Fact]
    public void OpenRoads_ByHearth_OneThenTwoThenThree()
    {
        // Act & Assert
        Map.OpenRoads(1).Should().Equal(0);
        Map.OpenRoads(2).Should().Equal(0, 1);
        Map.OpenRoads(3).Should().Equal(0, 1, 2);
        Enumerable.Range(0, Map.RoadCount).Should().OnlyContain(i => Map.RoadAt(i).Last() == Map.Hearth, "every road ends at the fire");
    }

    [Fact]
    public void Routes_NeverCrossRockOrBoulder()
    {
        // Assert
        foreach (var route in Map.Routes)
        {
            route.Tiles().Should().NotContain(t => Map.Rocks.Contains(t), route.Name);
        }
    }

    [Fact]
    public void River_OnlyInTheWild_AndEveryNewRoadCrossesItByABridge()
    {
        // Assert — spec 2026-10-20 river: the south and east roads start on the far bank.
        Map.Water.Should().NotBeEmpty();
        Map.Water.Should().OnlyContain(t => Map.OpensAt(t) == VillageMap.Wild, "nobody builds in the river");
        foreach (var road in new[] { 1, 2 })
        {
            var path = Map.RoadAt(road);
            path.Should().Contain(t => Map.IsBridge(t), Map.RoadName(road));
            Map.OpensAt(path[0]).Should().Be(VillageMap.Wild, "it comes out of the raiders' country");
        }

        Map.Road.Should().NotContain(t => Map.Water.Contains(t), "the north road stays dry");
    }

    [Fact]
    public void River_WideEnoughToMatter()
    {
        // Assert — three or more tiles of water across, nearly everywhere along the south reach.
        var widths = Enumerable.Range(0, 50).Select(x => Enumerable.Range(0, Map.Height).Count(y => Map.Water.Contains(new GridPosition(x, y)))).ToList();
        widths.Average().Should().BeGreaterThan(3.0);
        widths.Should().OnlyContain(w => w >= 2);
    }

    [Fact]
    public void TonightsRoad_ManyNightsAtHearthThree_EveryRoadComes()
    {
        // Arrange
        var state = Village(3);

        // Act
        var roads = Enumerable.Range(1, 30).Select(n => state.TonightsRoad(n, (ulong)(946 + n))).ToList();

        // Assert
        roads.Distinct().Should().BeEquivalentTo(new[] { 0, 1, 2 });
    }

    [Fact]
    public void PlanNight_TheScoutsRoad_IsTheRoadTheyTake()
    {
        // Arrange — at Hearth 3, some Night comes by another road than the first.
        var state = Village(3);
        var scout = state.Snapshot(T0).NextRoad;

        // Act
        var plan = state.Apply(new BeginNight(Guid.NewGuid()), T0).Plan!;
        var battle = new Battle(plan.ToBattleSetup(Map, Array.Empty<PlayerInput>()));

        // Assert
        plan.Road.Should().Be(scout);
        battle.Path.Should().Equal(Map.RoadAt(scout));
    }

    [Fact]
    public void Place_OnTheSouthRoad_FineAtHearthOneRefusedAtTwo()
    {
        // Arrange
        var one = Village(1);
        var two = Village(2);

        // Act & Assert — (20,13) is on the south road, which opens at Hearth 2.
        Place(one, BuildingType.Woodcutter, 20, 14).Status.Should().Be(CommandStatus.Accepted);
        Place(two, BuildingType.Woodcutter, 20, 13).Reason.Should().Contain("raiders' road");
    }

    [Fact]
    public void UpgradeHearth_ANewRoadOpens_WhatStoodOnItMovesAside()
    {
        // Arrange — at Hearth 1 a cottage and a track stand where the south road will run.
        var state = Village(1);
        Place(state, BuildingType.Cottage, 21, 12).Status.Should().Be(CommandStatus.Accepted); // covers (21..22, 12..13)
        state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);
        state.Apply(new PaintTiles(Guid.NewGuid(), BuildingType.DirtTrack, new[] { new GridPosition(24, 13) }), T0);

        // Act
        state.Apply(new UpgradeHearth(Guid.NewGuid()), T0).Status.Should().Be(CommandStatus.Accepted);
        state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);

        // Assert
        var snapshot = state.Snapshot(T0);
        var road = Map.RoadTiles(2);
        snapshot.Buildings.Should().NotContain(b => b.Type != BuildingType.Hearth && b.Type != BuildingType.Gate && b.Tiles.Any(road.Contains));
        snapshot.Buildings.Should().ContainSingle(b => b.Type == BuildingType.Cottage, "the cottage moved, it wasn't lost");
    }
}

/// <summary>Spec 2026-10-20 across the river: gates on the bridges, each road's raiders, Hearth 5 and the Comfy Altar.</summary>
public class AcrossTheRiverTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);
    private static readonly VillageMap Map = VillageMap.Demo;

    private static BaseState Village(int hearth)
    {
        var state = BaseState.CreateNew(BuildingTable.Default, Map, T0);
        for (var level = 2; level <= hearth; level++)
        {
            state.Apply(new DebugFillStores(Guid.NewGuid()), T0);
            state.Apply(new UpgradeHearth(Guid.NewGuid()), T0).Status.Should().Be(CommandStatus.Accepted);
            state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);
        }

        state.Apply(new DebugFillStores(Guid.NewGuid()), T0);
        return state;
    }

    private static GridPosition SouthBridge => Map.RoadAt(1).First(Map.IsBridge);

    [Fact]
    public void Place_GateOnABridge_RefusedBeforeHearthFourHeldFromIt()
    {
        // Arrange
        var three = Village(3);
        var four = Village(4);

        // Act
        var early = three.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Gate, SouthBridge), T0);
        var held = four.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Gate, SouthBridge), T0);

        // Assert
        early.Reason.Should().Contain("from Hearth 4");
        held.Status.Should().Be(CommandStatus.Accepted);
        BaseState.Restore(BuildingTable.Default, Map, four.ToData()).Snapshot(T0).BuildingAt(SouthBridge)!.Type.Should().Be(BuildingType.Gate, "a bridge gate survives a reload");
    }

    [Fact]
    public void Place_AnythingButAGateOnABridge_Refused()
    {
        // Act
        var outcome = Village(4).Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.SlurryChute, SouthBridge), T0);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Rejected);
    }

    [Theory]
    [InlineData(3, 0, CultOfComfort.Simulation.Balance.EnemyType.Lord)]
    [InlineData(3, 1, CultOfComfort.Simulation.Balance.EnemyType.Bishop)]
    public void ForRoad_EachRoadItsOwnLeader(int hearth, int road, CultOfComfort.Simulation.Balance.EnemyType boss)
    {
        // Act & Assert
        CultOfComfort.Simulation.Waves.RaidRoster.ForRoad(hearth, road)!.Boss.Should().Be(boss);
    }

    [Fact]
    public void ForRoad_EastRoad_Beasts_AndTheFirstRoadIsAsBefore()
    {
        // Act
        var east = CultOfComfort.Simulation.Waves.RaidRoster.ForRoad(3, 2)!;

        // Assert
        east.Mix.Should().OnlyContain(m => m.Type == CultOfComfort.Simulation.Balance.EnemyType.Boar || m.Type == CultOfComfort.Simulation.Balance.EnemyType.Goat
            || m.Type == CultOfComfort.Simulation.Balance.EnemyType.Goose || m.Type == CultOfComfort.Simulation.Balance.EnemyType.Peasant);
        foreach (var hearth in new[] { 1, 2, 3, 4 })
        {
            CultOfComfort.Simulation.Waves.RaidRoster.ForRoad(hearth, 0).Should().BeSameAs(CultOfComfort.Simulation.Waves.RaidRoster.ForHearth(hearth), "old Nights replay as they were");
        }
    }

    [Fact]
    public void HearthFive_OpensTheFarBankByTheStoneCircle()
    {
        // Assert
        Map.IsOpen(Map.StoneCircle!.Value, 4).Should().BeFalse();
        Map.IsOpen(Map.StoneCircle.Value, 5).Should().BeTrue();
        Map.Water.Should().OnlyContain(t => !Map.IsOpen(t, 5), "the river itself never opens");
    }

    [Fact]
    public void Place_ComfyAltar_OnlyInTheMiddleOfTheCircle()
    {
        // Arrange
        var state = Village(5);
        var circle = Map.StoneCircle!.Value;

        // Act
        var beside = state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.ComfyAltar, new GridPosition(circle.X - 2, circle.Y)), T0);
        var middle = state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.ComfyAltar, circle), T0);
        state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);
        // A track from the east road (row 17, over the bridge) down to the circle, felling the wild trees in its way.
        foreach (var tree in Enumerable.Range(18, circle.Y - 18).Select(y => new GridPosition(circle.X, y)).Where(Map.WildTrees.Contains))
        {
            state.Apply(new ClearTerrain(Guid.NewGuid(), tree), T0);
        }

        state.Apply(new PaintTiles(Guid.NewGuid(), BuildingType.DirtTrack, Enumerable.Range(18, circle.Y - 18).Select(y => new GridPosition(circle.X, y)).ToList()), T0);

        // Assert
        beside.Reason.Should().Contain("middle of the stone circle");
        middle.Status.Should().Be(CommandStatus.Accepted);
        var altar = state.Snapshot(T0).BuildingAt(circle)!;
        altar.Connected.Should().BeTrue("the east road carries the network over the bridge, a track does the rest");
        altar.PerHour.Should().BeGreaterThan(0, "its crew keep the candles lit");
    }
}

/// <summary>Spec 2026-10-20 long builds: days at the top, more builders, Towels as speed-ups.</summary>
public class LongBuildsTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    private static BaseState Village(int hearth)
    {
        var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);
        for (var level = 2; level <= hearth; level++)
        {
            state.Apply(new DebugFillStores(Guid.NewGuid()), T0);
            state.Apply(new UpgradeHearth(Guid.NewGuid()), T0);
            state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);
        }

        state.Apply(new DebugFillStores(Guid.NewGuid()), T0);
        return state;
    }

    [Fact]
    public void Place_HearthThree_TwoJobsAtOnceNotThree()
    {
        // Arrange
        var state = Village(3);

        // Act
        var first = state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Cottage, new GridPosition(22, 15)), T0);
        var second = state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Cottage, new GridPosition(25, 15)), T0);
        var third = state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Cottage, new GridPosition(22, 18)), T0);

        // Assert
        (first.Status, second.Status).Should().Be((CommandStatus.Accepted, CommandStatus.Accepted), "two builders at Hearth 3");
        third.Reason.Should().Contain("builders are busy");
        state.Snapshot(T0).FreeBuilders.Should().Be(0);
    }

    [Fact]
    public void Place_HearthOne_OneJobAtATime()
    {
        // Arrange
        var state = Village(1);
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Cottage, new GridPosition(22, 15)), T0).Status.Should().Be(CommandStatus.Accepted);

        // Act
        var second = state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Cottage, new GridPosition(25, 15)), T0);

        // Assert
        second.Reason.Should().Contain("builder is busy");
    }

    [Fact]
    public void SpendTowels_HurryANamedJob_TakesAnHourOffThatOne()
    {
        // Arrange — two jobs; the later one is hurried by name.
        var state = Village(3);
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Cottage, new GridPosition(22, 15)), T0);
        state.Apply(new UpgradeHearth(Guid.NewGuid()), T0).Status.Should().Be(CommandStatus.Accepted); // a day's work
        var hut = state.Snapshot(T0).Buildings.Single(b => b.Type == BuildingType.Hearth);
        var before = state.Snapshot(T0).Building(hut.Id).UpgradeEndsAtUtc!.Value;
        var data = state.ToData();
        state = BaseState.Restore(BuildingTable.Default, VillageMap.Demo, data with { Daily = data.Daily! with { Towels = 3 } });

        // Act
        state.Apply(new SpendTowels(Guid.NewGuid(), TowelUse.Hurry, hut.Id + 1), T0).Status.Should().Be(CommandStatus.Accepted);

        // Assert
        state.Snapshot(T0).Building(hut.Id).UpgradeEndsAtUtc.Should().Be(before.AddHours(-1));
    }

    [Fact]
    public void ClaimErrand_Done_PaysATowel()
    {
        // Arrange — the first errand (a Woodcutter's Lean-to) done.
        var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, new GridPosition(15, 5)), T0);
        state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);
        var towels = state.Snapshot(T0).Towels;

        // Act
        state.Apply(new ClaimErrand(Guid.NewGuid()), T0).Status.Should().Be(CommandStatus.Accepted);

        // Assert
        state.Snapshot(T0).Towels.Should().Be(towels + DailyBook.ErrandTowels, "Towels show on the snapshot, daily list or not");
    }
}
