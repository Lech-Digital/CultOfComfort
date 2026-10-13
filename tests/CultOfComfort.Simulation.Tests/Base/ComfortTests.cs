using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Tests.Base;

public class ComfortTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

    private static BuildingTable Rich(IReadOnlyList<int>? hearthComfort = null) => new()
    {
        StartingStock = new ResourceBag(8000, 2000, 2000, 1000),
        HearthStorageCaps = new[] { 9000, 9000, 9000 },
        HearthBeds = 12,
        HearthComfort = hearthComfort ?? new[] { 10, 20, 35 },
    };

    /// <summary>A rich village raised to <paramref name="hearth"/>; returns it and the time it got there.</summary>
    private static (BaseState State, DateTimeOffset Now) Village(int hearth, BuildingTable? table = null)
    {
        var state = BaseState.CreateNew(table ?? Rich(), VillageMap.Demo, T0);
        var now = T0;
        for (var level = 2; level <= hearth; level++)
        {
            state.Apply(new UpgradeHearth(Guid.NewGuid()), now).Status.Should().Be(CommandStatus.Accepted);
            now = now.AddHours(2);
        }

        state.Snapshot(now).HearthLevel.Should().Be(hearth);
        return (state, now);
    }

    /// <summary>Places <paramref name="type"/> on the first legal spot, nearest to (or, with <paramref name="far"/>, at least that far from) <paramref name="from"/>; lets it finish.</summary>
    private static int Place(BaseState state, BuildingType type, DateTimeOffset now, GridPosition from, int far = -1)
    {
        var spots = Enumerable.Range(0, VillageMap.Demo.Width).SelectMany(x => Enumerable.Range(0, VillageMap.Demo.Height).Select(y => new GridPosition(x, y)))
            .Where(p => far < 0 || Math.Max(Math.Abs(p.X - from.X), Math.Abs(p.Y - from.Y)) > far)
            .OrderBy(p => Math.Max(Math.Abs(p.X - from.X), Math.Abs(p.Y - from.Y)));
        foreach (var spot in spots)
        {
            if (state.Apply(new PlaceBuilding(Guid.NewGuid(), type, spot), now).Status == CommandStatus.Accepted)
            {
                state.Apply(new FinishConstructionNow(Guid.NewGuid()), now);
                return state.Snapshot(now).BuildingAt(spot)!.Id;
            }
        }

        throw new InvalidOperationException($"nowhere to put a {type}");
    }

    [Fact]
    public void Comfort_FreshVillage_IsTheHearthsOwn()
    {
        // Act
        var (state, now) = Village(1);

        // Assert
        state.Snapshot(now).Comfort.Should().Be(10);
    }

    [Fact]
    public void Comfort_ACottage_AddsItsComfort()
    {
        // Arrange
        var (state, now) = Village(1);

        // Act
        Place(state, BuildingType.Cottage, now, VillageMap.Demo.Hearth);

        // Assert
        state.Snapshot(now).Comfort.Should().Be(14);
    }

    [Fact]
    public void Comfort_MiddenBesideAHome_SmellsWorseThanOneFarAway()
    {
        // Arrange — Hearth 2 (20) with a cottage (+4).
        var (near, nowNear) = Village(2);
        var cottage = Place(near, BuildingType.Cottage, nowNear, VillageMap.Demo.Hearth);
        var at = near.Snapshot(nowNear).Building(cottage).Position;
        var (far, nowFar) = Village(2);
        Place(far, BuildingType.Cottage, nowFar, VillageMap.Demo.Hearth);

        // Act
        Place(near, BuildingType.MiddenSorter, nowNear, at);
        Place(far, BuildingType.MiddenSorter, nowFar, at, far: 7);

        // Assert
        far.Snapshot(nowFar).Comfort.Should().Be(20 + 4 - 10, "a midden on its own is −10");
        near.Snapshot(nowNear).Comfort.Should().Be(20 + 4 - 10 - 5, "and −5 more for the cottage it stinks out");
    }

    [Fact]
    public void Rate_CultistHut_DevotionScalesWithComfort()
    {
        // Arrange — the same village, once at Comfort 0 and once at 200.
        var (bleak, nowBleak) = Village(1, Rich(new[] { 0, 0, 0 }));
        var (cosy, nowCosy) = Village(1, Rich(new[] { 200, 200, 200 }));

        // Act
        var bleakHut = Place(bleak, BuildingType.CultistHut, nowBleak, VillageMap.Demo.Hearth);
        var cosyHut = Place(cosy, BuildingType.CultistHut, nowCosy, VillageMap.Demo.Hearth);

        // Assert — Comfort 3 (the hut's own) vs 203: about double.
        var plain = bleak.Snapshot(nowBleak).Building(bleakHut).PerHour;
        var doubled = cosy.Snapshot(nowCosy).Building(cosyHut).PerHour;
        plain.Should().BeGreaterThan(0);
        doubled.Should().BeInRange(plain * 2 - 1, plain * 2 + 1);
    }

    [Fact]
    public void Spec_NewBuildings_UnlockWithTheHearth()
    {
        // Act
        var table = BuildingTable.Default;

        // Assert
        table.Spec(BuildingType.MiddenSorter).UnlockHearth.Should().Be(2);
        table.Spec(BuildingType.Bathhouse).UnlockHearth.Should().Be(3);
        (table.Spec(BuildingType.Bathhouse).Width, table.Spec(BuildingType.Bathhouse).Depth).Should().Be((3, 3), "the first big building");
    }

    [Fact]
    public void Place_BathhouseAtHearthTwo_Rejected()
    {
        // Arrange
        var (state, now) = Village(2);

        // Act
        var outcome = state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Bathhouse, new GridPosition(14, 4)), now);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Rejected);
    }

    [Fact]
    public void Upkeep_Bathhouse_BurnsPeatOnTopOfTheVillagers()
    {
        // Arrange
        var (withBath, now) = Village(3);
        var (without, _) = Village(3);
        var bath = Place(withBath, BuildingType.Bathhouse, now, VillageMap.Demo.Hearth);
        var peatWith = withBath.Snapshot(now).Stock.Peat;
        var peatWithout = without.Snapshot(now).Stock.Peat;

        // Act
        var later = now.AddHours(2);
        var burnedWith = peatWith - withBath.Snapshot(later).Stock.Peat;
        var burnedWithout = peatWithout - without.Snapshot(later).Stock.Peat;

        // Assert
        withBath.Snapshot(later).Building(bath).Level.Should().Be(1);
        (burnedWith - burnedWithout).Should().Be(20, "10 Peat an hour for the stove");
        withBath.Snapshot(later).Comfort.Should().BeGreaterThanOrEqualTo(35 + 40);
    }
}
