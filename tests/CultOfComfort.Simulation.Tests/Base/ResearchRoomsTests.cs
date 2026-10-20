using CultOfComfort.Simulation.Balance;
using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Combat;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Tests.Base;

/// <summary>Spec 2026-10-16 first day: research in four rooms, and what the new studies do.</summary>
public class ResearchRoomsTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);

    /// <summary>A Hearth 2 village (not full: salvage and rewards have room) with <paramref name="studied"/> already read.</summary>
    private static (BaseState State, DateTimeOffset Now) Village(params string[] studied)
    {
        var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);
        state.Apply(new UpgradeHearth(Guid.NewGuid()), T0).Status.Should().Be(CommandStatus.Accepted);
        state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);
        var data = state.ToData() with { Research = new ResearchData(studied, null, null) };
        return (BaseState.Restore(BuildingTable.Default, VillageMap.Demo, data), T0);
    }

    [Fact]
    public void Book_EveryRoom_HasStudiesAtHearthTwoAndDeeperOnesAtThree()
    {
        // Act
        var rooms = ResearchBook.Default.Techs.GroupBy(t => t.Category).ToDictionary(g => g.Key, g => g.ToList());

        // Assert
        rooms.Keys.Should().BeEquivalentTo(Enum.GetValues<TechCategory>());
        foreach (var (room, techs) in rooms)
        {
            techs.Should().Contain(t => t.UnlockHearth == 2, $"{room} has something to read at Hearth 2");
            techs.Should().Contain(t => t.UnlockHearth == 3, $"{room} has something deeper at Hearth 3");
        }

        ResearchBook.Default.Techs.Should().HaveCountGreaterThanOrEqualTo(24);
        ResearchBook.Default.Techs.Select(t => t.Id).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Book_OldStudyIds_StayInTheBook()
    {
        // Assert — saves hold these ids: renaming one would break every village that read it.
        var ids = ResearchBook.Default.Techs.Select(t => t.Id);
        ids.Should().Contain(new[] { "sharper-axes", "peat-stacking", "sermon-notes", "thicker-walls", "plumbing", "tithe-accounting", "long-sermons", "bigger-stores", "hot-water" });
    }

    [Fact]
    public void MeasureTwice_Done_BuildingIsQuicker()
    {
        // Arrange
        var (state, now) = Village("sharper-axes", "measure-twice");

        // Act
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, new GridPosition(15, 5)), now).Status.Should().Be(CommandStatus.Accepted);

        // Assert — 30 s less a fifth.
        state.Snapshot(now).Buildings.Single(b => b.Type == BuildingType.Woodcutter).UpgradeEndsAtUtc.Should().Be(now.AddSeconds(24));
    }

    [Fact]
    public void FindersKeepers_Done_ANightLeavesHalfAsMuchScrapAgain()
    {
        // Arrange
        var (plain, now) = Village();
        var (lucky, _) = Village("finders-keepers");
        var before = plain.Snapshot(now).Stock.Scrap;

        // Act
        plain.Apply(new RecordNightResult(Guid.NewGuid(), 1, 3), now);
        lucky.Apply(new RecordNightResult(Guid.NewGuid(), 1, 3), now);

        // Assert
        var salvage = plain.Snapshot(now).Stock.Scrap - before;
        salvage.Should().BeGreaterThan(0);
        (lucky.Snapshot(now).Stock.Scrap - before).Should().Be(salvage * 150 / 100);
    }

    [Fact]
    public void NightStudies_Done_WrittenIntoTheNightPlan()
    {
        // Arrange
        var (state, now) = Village("bigger-bucket", "springier-saplings", "padded-cardigan");
        var (plain, _) = Village();

        // Act
        var plan = state.Apply(new BeginNight(Guid.NewGuid()), now).Plan!;
        var plainPlan = plain.Apply(new BeginNight(Guid.NewGuid()), now).Plan!;

        // Assert
        plan.ExtraSluiceCharges.Should().Be(plainPlan.ExtraSluiceCharges + 1);
        plan.TowerDamagePercent.Should().Be(20);
        plan.ProphetHealthPercent.Should().Be(25);
        plainPlan.Balance().Should().BeNull("nothing read: the default balance, as every Night before research");
    }

    [Fact]
    public void Balance_TowerAndProphetResearch_ScalesDamageAndHealth()
    {
        // Arrange
        var plan = new NightPlan(1, 1, 10, 1, 0, false, Array.Empty<PlacedTower>(), Array.Empty<GridPosition>(), Array.Empty<PlacedStructure>(),
            TowerDamagePercent: 50, ProphetHealthPercent: 25);
        var d = BalanceTable.Default;

        // Act
        var balance = plan.Balance()!;

        // Assert
        balance.Towers[TowerType.SaplingFlinger].Damage.Should().Be(d.Towers[TowerType.SaplingFlinger].Damage * 150 / 100);
        balance.Towers[TowerType.SlurryChute].Range.Should().Be(d.Towers[TowerType.SlurryChute].Range, "only the damage changes");
        balance.Leader.MaxHealth.Should().Be(150);
        balance.Enemies.Should().BeEquivalentTo(d.Enemies, "raiders are untouched");
    }

    [Fact]
    public void Grovelling_Done_TheLordAsksForLess()
    {
        // Arrange
        var (plain, now) = Village();
        var (humble, _) = Village("grovelling");
        var asked = now.AddMinutes(Demands.FirstGapMinutes + 1);

        // Act
        var full = plain.Snapshot(asked).Domain!.Demand!.Amount;
        var less = humble.Snapshot(asked).Domain!.Demand!.Amount;

        // Assert
        DomainTable.TitheValue(less).Should().BeLessThan(DomainTable.TitheValue(full));
        less.Should().Be(Demands.Scaled(full, 80));
    }

    [Fact]
    public void WhisperedHymns_Done_TemperRisesMoreSlowly()
    {
        // Arrange
        var (plain, now) = Village();
        var (quiet, _) = Village("whispered-hymns");

        // Act — five hours: before the first letter could be missed.
        var later = now.AddHours(5);

        // Assert — 6 an hour, less a fifth.
        plain.Snapshot(later).Tension.Should().Be(30);
        quiet.Snapshot(later).Tension.Should().Be(24);
    }

    [Fact]
    public void MiddenSieve_Done_SortersMakeAQuarterMore()
    {
        // Arrange
        var (plain, now) = Village();
        var (sieved, _) = Village("midden-sieve");
        foreach (var state in new[] { plain, sieved })
        {
            state.Apply(new DebugFillStores(Guid.NewGuid()), now);
            state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.MiddenSorter, new GridPosition(15, 5)), now).Status.Should().Be(CommandStatus.Accepted);
            state.Apply(new FinishConstructionNow(Guid.NewGuid()), now);
        }

        // Act
        var rate = plain.Snapshot(now).Buildings.Single(b => b.Type == BuildingType.MiddenSorter).PerHour;
        var sievedRate = sieved.Snapshot(now).Buildings.Single(b => b.Type == BuildingType.MiddenSorter).PerHour;

        // Assert
        rate.Should().BeGreaterThan(0, "a connected, crewed sorter makes Scrap");
        sievedRate.Should().Be(rate * 125 / 100);
    }
}

/// <summary>Errands saved by id; old saves (a position only) read through the old order.</summary>
public class ErrandSaveTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(6, "hearth-2")]       // old position 6: the Hearth errand, now later in the book
    [InlineData(9, "slurry-chute")]   // old position 9: the chute, now before the first Night
    [InlineData(15, "win-raid")]
    public void Restore_OldSaveWithAPositionOnly_KeepsTheSameErrand(int oldIndex, string id)
    {
        // Arrange
        var data = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0).ToData() with { ErrandIndex = oldIndex, ErrandId = null };

        // Act
        var restored = BaseState.Restore(BuildingTable.Default, VillageMap.Demo, data);

        // Assert
        restored.Snapshot(T0).Errand!.Id.Should().Be(id);
    }

    [Fact]
    public void Restore_OldSavePastTheLastErrand_AllDone()
    {
        // Arrange
        var data = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0).ToData() with { ErrandIndex = ErrandBook.LegacyOrder.Count, ErrandId = null };

        // Act & Assert
        BaseState.Restore(BuildingTable.Default, VillageMap.Demo, data).Snapshot(T0).Errand.Should().BeNull();
    }

    [Fact]
    public void ToData_CurrentErrand_SavedByIdAndRestored()
    {
        // Arrange
        var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);
        var data = state.ToData() with { ErrandId = "midden-sorter" };

        // Act
        var restored = BaseState.Restore(BuildingTable.Default, VillageMap.Demo, data);

        // Assert
        restored.ToData().ErrandId.Should().Be("midden-sorter");
        restored.Snapshot(T0).Errand!.Id.Should().Be("midden-sorter");
    }
}
