using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Combat;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Tests.Base;

public class ErrandTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);
    private static readonly GridPosition Field = new(3, 9);

    private static BaseState NewBase(params Errand[] errands) =>
        BaseState.CreateNew(errands.Length == 0 ? BuildingTable.Default : new BuildingTable { Errands = new ErrandBook("test", errands) }, VillageMap.Demo, T0);

    private static Errand Build(BuildingType type, int count = 1, int timber = 50) =>
        new("build", $"Build {type}", ErrandGoal.Build, type, count, new ResourceBag(timber, 0, 0, 0));

    private static void Built(BaseState state, BuildingType type, GridPosition at)
    {
        state.Apply(new PlaceBuilding(Guid.NewGuid(), type, at), T0).Status.Should().Be(CommandStatus.Accepted);
        state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);
    }

    private static CommandOutcome Claim(BaseState state, DateTimeOffset? at = null) => state.Apply(new ClaimErrand(Guid.NewGuid()), at ?? T0);

    [Fact]
    public void Snapshot_NewVillage_ShowsTheFirstErrandNotYetDone()
    {
        // Act
        var errand = NewBase().Snapshot(T0).Errand;

        // Assert
        errand.Should().NotBeNull();
        errand!.Index.Should().Be(0);
        errand.Text.Should().Contain("Woodcutter");
        errand.Progress.Should().Be(0);
        errand.Target.Should().Be(1);
        errand.Done.Should().BeFalse();
    }

    [Fact]
    public void ClaimErrand_BeforeItIsDone_IsRejectedAndPaysNothing()
    {
        // Arrange
        var state = NewBase();

        // Act
        var outcome = Claim(state);

        // Assert
        outcome.Status.Should().Be(CommandStatus.Rejected);
        state.Snapshot(T0).Stock.Should().Be(BuildingTable.Default.StartingStock);
    }

    [Fact]
    public void ClaimErrand_WhenDone_PaysOnceAndMovesToTheNext()
    {
        // Arrange — a Woodcutter that is still being built does not count yet.
        var state = NewBase(Build(BuildingType.Woodcutter), Build(BuildingType.Cottage, timber: 60));
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, Field), T0);
        state.Snapshot(T0).Errand!.Done.Should().BeFalse();
        state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);
        var before = state.Snapshot(T0).Stock;

        // Act
        var first = Claim(state);
        var second = Claim(state);

        // Assert
        first.Status.Should().Be(CommandStatus.Accepted);
        first.Collected.Should().Be(new ResourceBag(50, 0, 0, 0));
        second.Status.Should().Be(CommandStatus.Rejected);
        var after = state.Snapshot(T0);
        after.Stock.Should().Be(before + new ResourceBag(50, 0, 0, 0));
        after.Errand!.Index.Should().Be(1);
        after.Errand.Text.Should().Contain("Cottage");
    }

    [Fact]
    public void Snapshot_ErrandAlreadyTrue_IsDoneAtOnce()
    {
        // Arrange — the village built two Woodcutters before the errand came up.
        var state = NewBase(Build(BuildingType.Cottage), Build(BuildingType.Woodcutter, count: 2));
        Built(state, BuildingType.Woodcutter, Field);
        Built(state, BuildingType.Woodcutter, new GridPosition(20, 14));
        Built(state, BuildingType.Cottage, new GridPosition(20, 11));

        // Act
        Claim(state).Status.Should().Be(CommandStatus.Accepted);
        var errand = state.Snapshot(T0).Errand!;

        // Assert
        errand.Progress.Should().Be(2);
        errand.Done.Should().BeTrue();
    }

    [Fact]
    public void Collect_WhileItIsTheErrand_CompletesIt()
    {
        // Arrange
        var state = NewBase(new Errand("collect", "Collect Timber", ErrandGoal.Collect, BuildingType.Woodcutter, 1, new ResourceBag(50, 0, 0, 0)));
        Built(state, BuildingType.Woodcutter, Field);
        var later = T0.AddHours(1);
        state.Snapshot(later).Errand!.Done.Should().BeFalse();

        // Act
        state.Apply(new Collect(Guid.NewGuid(), null), later).Status.Should().Be(CommandStatus.Accepted);

        // Assert
        state.Snapshot(later).Errand!.Done.Should().BeTrue();
    }

    [Fact]
    public void ResolveNight_WhileSurviveIsTheErrand_CompletesIt()
    {
        // Arrange
        var state = NewBase(new Errand("night", "Survive a Night", ErrandGoal.SurviveNight, null, 1, new ResourceBag(100, 0, 40, 0)));
        var night = state.Apply(new BeginNight(Guid.NewGuid()), T0).Plan!;

        // Act
        state.Apply(new ResolveNight(Guid.NewGuid(), night.Night, Array.Empty<PlayerInput>()), T0).Status.Should().Be(CommandStatus.Accepted);

        // Assert
        state.Snapshot(T0).Errand!.Done.Should().BeTrue();
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public void RecordNight_WinRaidErrand_NeedsTwoStars(int stars, bool done)
    {
        // Arrange
        var state = NewBase(new Errand("win", "Win a raid with 2 stars", ErrandGoal.WinRaid, null, 2, new ResourceBag(0, 0, 100, 0)));

        // Act
        state.Apply(new RecordNightResult(Guid.NewGuid(), 1, stars), T0);

        // Assert
        state.Snapshot(T0).Errand!.Done.Should().Be(done);
    }

    [Fact]
    public void Snapshot_GateErrand_CountsOnlyGatesOnTheRaidersRoad()
    {
        // Arrange — one gate in a field, then one across the road.
        var state = NewBase(new Errand("gate", "Gate the road", ErrandGoal.BuildOnRoad, BuildingType.Gate, 1, new ResourceBag(100, 0, 0, 0)));
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Gate, Field), T0).Status.Should().Be(CommandStatus.Accepted);
        state.Snapshot(T0).Errand!.Done.Should().BeFalse();
        var road = VillageMap.Demo.Road[VillageMap.Demo.Road.Count / 2];

        // Act
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Gate, road), T0).Status.Should().Be(CommandStatus.Accepted);

        // Assert
        state.Snapshot(T0).Errand!.Done.Should().BeTrue();
    }

    [Fact]
    public void ClaimErrand_LastOne_LeavesNoErrand()
    {
        // Arrange
        var state = NewBase(Build(BuildingType.Woodcutter));
        Built(state, BuildingType.Woodcutter, Field);

        // Act
        Claim(state);

        // Assert
        state.Snapshot(T0).Errand.Should().BeNull();
        Claim(state).Status.Should().Be(CommandStatus.Rejected);
    }

    [Fact]
    public void ClaimErrand_RewardOverStorage_IsCappedAtTheStorageLimit()
    {
        // Arrange — 990 of 1000 Timber, and the errand pays 50.
        var state = NewBase(Build(BuildingType.Woodcutter));
        Built(state, BuildingType.Woodcutter, Field);
        var data = state.ToData();
        state = BaseState.Restore(new BuildingTable { Errands = new ErrandBook("test", new[] { Build(BuildingType.Woodcutter) }) }, VillageMap.Demo,
            data with { Stock = data.Stock with { Timber = 990 } });

        // Act
        Claim(state);

        // Assert
        state.Snapshot(T0).Stock.Timber.Should().Be(1000);
    }

    [Fact]
    public void ToData_MidErrand_RestoresTheSameErrandAndItsEvent()
    {
        // Arrange — on the second errand, whose event (a collect) has happened.
        var book = new[] { Build(BuildingType.Woodcutter), new Errand("collect", "Collect", ErrandGoal.Collect, null, 1, default) };
        var state = NewBase(book);
        Built(state, BuildingType.Woodcutter, Field);
        Claim(state);
        state.Apply(new Collect(Guid.NewGuid(), null), T0.AddHours(1));

        // Act
        var back = BaseState.Restore(new BuildingTable { Errands = new ErrandBook("test", book) }, VillageMap.Demo, state.ToData());

        // Assert
        var errand = back.Snapshot(T0.AddHours(1)).Errand!;
        errand.Index.Should().Be(1);
        errand.Done.Should().BeTrue();
    }

    [Fact]
    public void Default_ErrandBook_RunsFromWoodcutterToHearthThreeThenARaid()
    {
        // Act
        var errands = ErrandBook.Default.Errands;
        var hearthOne = errands.TakeWhile(e => !(e.Goal == ErrandGoal.HearthLevel && e.Target == 2)).Append(errands.First(e => e.Goal == ErrandGoal.HearthLevel));

        // Assert
        errands.Should().HaveCount(16); // 2026-10-14: missionary, study and patron errands after Hearth 2
        var hearthTwo = errands.ToList().FindIndex(e => e.Goal == ErrandGoal.HearthLevel && e.Target == 2);
        errands.Skip(hearthTwo + 1).Select(e => e.Goal).Should().Contain(new[] { ErrandGoal.SendMissionary, ErrandGoal.Study, ErrandGoal.Patron }, "the Marchlands and the manual come after Hearth 2");
        errands[0].Type.Should().Be(BuildingType.Woodcutter);
        errands[^2].Goal.Should().Be(ErrandGoal.HearthLevel);
        errands[^2].Target.Should().Be(3);
        errands[^1].Goal.Should().Be(ErrandGoal.WinRaid, "the 12-hour shield after a Night makes a won raid a next-session goal");
        hearthOne.Sum(e => e.Reward.Timber).Should().Be(300, "the Hearth 1 errands bridge the 450 Timber Hearth 2 after a starter village");
        errands.Select(e => e.Id).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Default_CultistHut_MakesEnoughDevotionForHearthThreeOnTheGddClock()
    {
        // Act
        var levels = BuildingTable.Default.Spec(BuildingType.CultistHut).Levels;

        // Assert
        levels.Select(l => l.PerHour).Should().Equal(10, 18, 28);
        BuildingTable.Default.Version.Should().Be("village-2026.10.6");
    }
}
