using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Tests.Base;

public class CrewTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    private static BuildingTable Table(int hearthBeds = 2) =>
        new() { StartingStock = new ResourceBag(5000, 900, 1000, 300), HearthBeds = hearthBeds, StarterStockMinutes = 0 };

    private static (BaseState State, int Peat, int Lean) PeatAndLean(int villagers)
    {
        var state = BaseState.CreateNew(Table(villagers), VillageMap.Demo, T0);
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.PeatCut, new GridPosition(12, 3), true), T0).Status.Should().Be(CommandStatus.Accepted);
        state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, new GridPosition(15, 5)), T0).Status.Should().Be(CommandStatus.Accepted);
        state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);
        var snapshot = state.Snapshot(T0);
        return (state, snapshot.Buildings.Single(b => b.Type == BuildingType.PeatCut).Id, snapshot.Buildings.Single(b => b.Type == BuildingType.Woodcutter).Id);
    }

    [Theory]
    [InlineData(0, 2, 0)]
    [InlineData(1, 2, 666)]   // 100 of 150 shares
    [InlineData(2, 2, 1000)]
    [InlineData(1, 3, 555)]   // 100 of 180
    [InlineData(3, 4, 900)]   // 180 of 200
    [InlineData(9, 2, 1000)]  // never more than full
    public void CrewPerMille_DiminishingReturns(int crew, int slots, int perMille)
    {
        // Act & Assert
        BuildingSpec.CrewPerMille(crew, slots).Should().Be(perMille);
    }

    [Fact]
    public void Slots_Upgrades_AskForMoreHandsButNeverMakeLess()
    {
        // Arrange
        var lean = BuildingTable.Default.Spec(BuildingType.Woodcutter);

        // Act
        var levelOneFull = lean.Levels[0].PerHour;
        var levelTwoOneHand = lean.Levels[1].PerHour * BuildingSpec.CrewPerMille(1, lean.SlotsAt(2)) / 1000;

        // Assert
        (lean.SlotsAt(1), lean.SlotsAt(2), lean.SlotsAt(3)).Should().Be((1, 2, 3));
        levelTwoOneHand.Should().BeGreaterThan(levelOneFull, "upgrading never hurts, even short-handed");
    }

    [Fact]
    public void Crew_OneHandForATwoManPeatCut_StillCutsTwoThirds()
    {
        // Arrange — one villager, a Peat Cut (2 slots) built first.
        var (state, peat, _) = PeatAndLean(villagers: 1);

        // Act
        var view = state.Snapshot(T0.AddHours(3)).Building(peat);

        // Assert
        (view.Crew, view.CrewNeeded).Should().Be((1, 2));
        view.Ready.Should().Be(3 * (25 * 666 / 1000), "16 an hour instead of 25");
    }

    [Fact]
    public void SetCrew_TakeOneFromThePeatCut_TheLeanToGetsIt()
    {
        // Arrange — two villagers: both at the Peat Cut, none at the lean-to.
        var (state, peat, lean) = PeatAndLean(villagers: 2);
        state.Snapshot(T0).Building(lean).Crew.Should().Be(0);

        // Act
        var outcome = state.Apply(new SetCrew(Guid.NewGuid(), peat, 1), T0);
        var snapshot = state.Snapshot(T0);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Accepted);
        (snapshot.Building(peat).Crew, snapshot.Building(peat).CrewWanted).Should().Be((1, 1));
        snapshot.Building(lean).Crew.Should().Be(1);
        snapshot.IdleVillagers.Should().Be(0);
    }

    [Fact]
    public void SetCrew_ZeroEverywhere_EveryoneIdleAndNothingMade()
    {
        // Arrange
        var (state, peat, lean) = PeatAndLean(villagers: 3);

        // Act
        state.Apply(new SetCrew(Guid.NewGuid(), peat, 0), T0);
        state.Apply(new SetCrew(Guid.NewGuid(), lean, 0), T0);
        var snapshot = state.Snapshot(T0.AddHours(2));

        // Assert
        snapshot.IdleVillagers.Should().Be(3);
        snapshot.Building(peat).Ready.Should().Be(0);
        snapshot.Building(lean).Ready.Should().Be(0);
    }

    [Fact]
    public void SetCrew_MoreThanItsSlots_Rejected()
    {
        // Arrange
        var (state, _, lean) = PeatAndLean(villagers: 2);

        // Act
        var outcome = state.Apply(new SetCrew(Guid.NewGuid(), lean, 2), T0);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Rejected);
        outcome.Reason.Should().Contain("room for 1");
    }

    [Fact]
    public void SetCrew_OnTheHearth_Rejected()
    {
        // Arrange
        var state = BaseState.CreateNew(Table(), VillageMap.Demo, T0);

        // Act
        var outcome = state.Apply(new SetCrew(Guid.NewGuid(), 0, 1), T0);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Rejected);
    }

    [Fact]
    public void SetCrew_ProductionBeforeTheChange_KeptAtTheOldRate()
    {
        // Arrange — the lean-to (alone, one villager) works two hours, then is emptied.
        var state = BaseState.CreateNew(Table(1), VillageMap.Demo, T0);
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, new GridPosition(15, 5)), T0);
        state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);
        var lean = state.Snapshot(T0).Buildings.Single(b => b.Type == BuildingType.Woodcutter).Id;

        // Act
        state.Apply(new SetCrew(Guid.NewGuid(), lean, 0), T0.AddHours(2));
        var later = state.Snapshot(T0.AddHours(5)).Building(lean).Ready;

        // Assert
        later.Should().Be(200, "two hours at 100, then nothing");
    }

    [Fact]
    public void ToData_ChosenCrew_RestoresTheSame()
    {
        // Arrange
        var (state, peat, _) = PeatAndLean(villagers: 3);
        state.Apply(new SetCrew(Guid.NewGuid(), peat, 1), T0);

        // Act
        var restored = BaseState.Restore(Table(3), VillageMap.Demo, state.ToData());

        // Assert
        restored.Snapshot(T0).Building(peat).CrewWanted.Should().Be(1);
        restored.Snapshot(T0).IdleVillagers.Should().Be(state.Snapshot(T0).IdleVillagers);
    }
}
