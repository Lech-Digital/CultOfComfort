using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Tests.Base;

/// <summary>Spec 2026-10-20 new buildings: the Watchtower, the Tavern and the Bell Tower.</summary>
public class NewBuildingsTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 9, 9, 0, 0, TimeSpan.Zero); // spring

    private static BaseState Village()
    {
        var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);
        for (var level = 2; level <= 3; level++)
        {
            state.Apply(new DebugFillStores(Guid.NewGuid()), T0);
            state.Apply(new UpgradeHearth(Guid.NewGuid()), T0);
            state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);
        }

        state.Apply(new DebugFillStores(Guid.NewGuid()), T0);
        return state;
    }

    /// <summary>Builds it beside the Hearth (on the road network) and finishes it.</summary>
    private static void Build(BaseState state, BuildingType type, int x, int y)
    {
        state.Apply(new PlaceBuilding(Guid.NewGuid(), type, new GridPosition(x, y)), T0).Status.Should().Be(CommandStatus.Accepted);
        state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);
        state.Snapshot(T0).BuildingAt(new GridPosition(x, y))!.Connected.Should().BeTrue();
    }

    [Fact]
    public void Watchtower_Standing_ForecastsTheNextTwoNights()
    {
        // Arrange
        var state = Village();
        state.Snapshot(T0).Forecast.Should().BeEmpty("no lookout, no forecast");

        // Act
        Build(state, BuildingType.Watchtower, 20, 8);

        // Assert
        var forecast = state.Snapshot(T0).Forecast!;
        forecast.Should().HaveCount(2);
        forecast[0].Road.Should().Be(state.Snapshot(T0).NextRoad, "the first is the scout's road");
    }

    [Fact]
    public void BellTower_Standing_CultistsHitHarderAtNight()
    {
        // Arrange
        var state = Village();
        Build(state, BuildingType.BellTower, 20, 8);

        // Act
        var plan = state.Apply(new BeginNight(Guid.NewGuid()), T0).Plan!;

        // Assert
        plan.DefenderDamagePercent.Should().Be(BuildingTable.Default.BellDefenderPercent);
        plan.Balance()!.Defender.AttackDamage.Should().BeGreaterThan(CultOfComfort.Simulation.Balance.BalanceTable.Default.Defender.AttackDamage);
    }

    [Fact]
    public void Tavern_Standing_VillagersArriveQuicker()
    {
        // Arrange — two villages with room for more, one with a Tavern.
        var plain = Village();
        var tavern = Village();
        Build(tavern, BuildingType.Tavern, 20, 8);
        foreach (var state in new[] { plain, tavern })
        {
            Build(state, BuildingType.Cottage, 20, 11);
        }

        // Act — an hour later.
        var a = plain.Snapshot(T0.AddHours(1)).Population;
        var b = tavern.Snapshot(T0.AddHours(1)).Population;

        // Assert
        b.Should().BeGreaterThanOrEqualTo(a);
        tavern.Snapshot(T0).Buildings.Single(x => x.Type == BuildingType.Tavern).PerHour.Should().BeGreaterThan(0, "its crew pour the ale");
    }
}
