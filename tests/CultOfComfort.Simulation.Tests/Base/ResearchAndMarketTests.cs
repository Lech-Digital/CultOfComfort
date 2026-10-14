using CultOfComfort.Simulation.Base;

namespace CultOfComfort.Simulation.Tests.Base;

public class ResearchTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);

    private static (BaseState State, DateTimeOffset Now) Village(int hearth)
    {
        var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);
        var now = T0;
        for (var level = 2; level <= hearth; level++)
        {
            state.Apply(new DebugFillStores(Guid.NewGuid()), now);
            state.Apply(new UpgradeHearth(Guid.NewGuid()), now).Status.Should().Be(CommandStatus.Accepted);
            now = now.AddHours(2);
        }

        state.Apply(new DebugFillStores(Guid.NewGuid()), now);
        return (state, now);
    }

    private static TechView View(BaseState state, string id, DateTimeOffset at) => state.Snapshot(at).Research!.Single(t => t.Tech.Id == id);

    [Fact]
    public void Book_EveryPrerequisite_IsInTheBookAndEarlier()
    {
        // Act
        var techs = ResearchBook.Default.Techs;

        // Assert
        for (var i = 0; i < techs.Count; i++)
        {
            if (techs[i].Needs != null)
            {
                techs.Take(i).Select(t => t.Id).Should().Contain(techs[i].Needs!, $"{techs[i].Id} needs something listed before it");
            }
        }
    }

    [Fact]
    public void Start_HearthOne_Locked()
    {
        // Arrange
        var (state, now) = Village(1);

        // Act
        var outcome = state.Apply(new StartResearch(Guid.NewGuid(), "sharper-axes"), now);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Rejected);
        View(state, "sharper-axes", now).State.Should().Be(TechState.Locked);
    }

    [Fact]
    public void Start_Available_ChargesAndFinishesOnTime()
    {
        // Arrange
        var (state, now) = Village(2);
        var before = state.Snapshot(now).Stock;
        var tech = ResearchBook.Default.Get("sharper-axes")!;

        // Act
        var outcome = state.Apply(new StartResearch(Guid.NewGuid(), tech.Id), now);
        var charged = before - state.Snapshot(now).Stock;
        var during = View(state, tech.Id, now.AddMinutes(tech.Minutes - 1));
        var after = View(state, tech.Id, now.AddMinutes(tech.Minutes));

        // Assert
        outcome.Status.Should().Be(CommandStatus.Accepted);
        charged.Should().Be(tech.Cost);
        during.State.Should().Be(TechState.Studying);
        after.State.Should().Be(TechState.Done);
    }

    [Fact]
    public void Start_SecondWhileStudying_Rejected()
    {
        // Arrange
        var (state, now) = Village(2);
        state.Apply(new StartResearch(Guid.NewGuid(), "sharper-axes"), now);

        // Act
        var outcome = state.Apply(new StartResearch(Guid.NewGuid(), "peat-stacking"), now);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Rejected);
    }

    [Fact]
    public void Start_WithoutItsPrerequisite_Rejected()
    {
        // Arrange — Thicker Walls needs Sharper Axes.
        var (state, now) = Village(2);

        // Act
        var outcome = state.Apply(new StartResearch(Guid.NewGuid(), "thicker-walls"), now);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Rejected);
        outcome.Reason.Should().Contain("Sharper Axes");
    }

    [Fact]
    public void SharperAxes_Done_WoodcuttersMakeFifteenPercentMore()
    {
        // Arrange — a lean-to by the road (finished), then the study.
        var (state, now) = Village(2);
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, new CultOfComfort.Simulation.Grid.GridPosition(15, 5)), now).Status.Should().Be(CommandStatus.Accepted);
        state.Apply(new FinishConstructionNow(Guid.NewGuid()), now);
        var plain = state.Snapshot(now).Buildings.Single(b => b.Type == BuildingType.Woodcutter).PerHour;

        // Act
        state.Apply(new StartResearch(Guid.NewGuid(), "sharper-axes"), now);
        var later = now.AddHours(2);
        var sharper = state.Snapshot(later).Buildings.Single(b => b.Type == BuildingType.Woodcutter).PerHour;

        // Assert
        sharper.Should().Be(plain * 115 / 100);
    }

    [Fact]
    public void ToData_StudyUnderWay_RestoresAndFinishes()
    {
        // Arrange
        var (state, now) = Village(2);
        state.Apply(new StartResearch(Guid.NewGuid(), "sermon-notes"), now);

        // Act
        var restored = BaseState.Restore(BuildingTable.Default, VillageMap.Demo, state.ToData());

        // Assert
        View(restored, "sermon-notes", now.AddMinutes(10)).State.Should().Be(TechState.Studying);
        View(restored, "sermon-notes", now.AddHours(2)).State.Should().Be(TechState.Done);
    }
}

public class MarketTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(ResourceType.Timber, ResourceType.Peat, 10, 50)]     // 10 Peat (worth 20) × 2.5 = 50 Timber
    [InlineData(ResourceType.Scrap, ResourceType.Devotion, 4, 20)]  // 4 Devotion (worth 16) × 2.5 = 40 worth = 20 Scrap
    [InlineData(ResourceType.Timber, ResourceType.Scrap, 1, 5)]
    public void TradePrice_BadRateOnPurpose(ResourceType give, ResourceType get, int amount, int price)
    {
        // Act & Assert
        BuildingTable.Default.TradePrice(give, get, amount).Should().Be(price);
    }

    [Fact]
    public void Trade_Affordable_SwapsAtTheMarketRate()
    {
        // Arrange
        var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);
        var before = state.Snapshot(T0).Stock;

        // Act
        var outcome = state.Apply(new Trade(Guid.NewGuid(), ResourceType.Timber, ResourceType.Peat, 20), T0);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Accepted);
        state.Snapshot(T0).Stock.Should().Be(before + new ResourceBag(-100, 20, 0, 0));
    }

    [Fact]
    public void Trade_TooDear_Rejected()
    {
        // Arrange
        var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);

        // Act
        var outcome = state.Apply(new Trade(Guid.NewGuid(), ResourceType.Devotion, ResourceType.Timber, 10), T0);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Rejected, "a new village has no Devotion to spend");
    }

    [Fact]
    public void Trade_SameResource_Rejected()
    {
        // Act
        var outcome = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0).Apply(new Trade(Guid.NewGuid(), ResourceType.Timber, ResourceType.Timber, 5), T0);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Rejected);
    }

    [Fact]
    public void ComfortParts_FreshVillage_TheHearthAlone()
    {
        // Act
        var parts = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0).Snapshot(T0).ComfortParts!;

        // Assert
        parts.Should().ContainSingle().Which.Should().Be(("Hearth", 10));
    }
}
