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

    [Fact]
    public void Place_TowerPastTheCap_Rejected()
    {
        // Arrange — four towers at Hearth 1.
        var state = Village(1);
        foreach (var (x, y) in new[] { (6, 2), (6, 3), (6, 4), (6, 6) })
        {
            Place(state, BuildingType.SlurryChute, x, y).Status.Should().Be(CommandStatus.Accepted);
        }

        // Act
        var outcome = Place(state, BuildingType.SlurryChute, 7, 2);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Rejected);
        outcome.Reason.Should().Contain("4 towers");
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
