using System.Text.Json;
using CultOfComfort.Contracts;
using CultOfComfort.Server.Infrastructure;
using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Combat;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Server.Tests;

public class ContractsTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

    public static TheoryData<BaseCommand> EveryCommand() => new()
    {
        new PlaceBuilding(Guid.NewGuid(), BuildingType.Cottage, new GridPosition(4, 5), true),
        new PaintTiles(Guid.NewGuid(), BuildingType.DirtTrack, new[] { new GridPosition(1, 2), new GridPosition(1, 3) }),
        new MoveBuilding(Guid.NewGuid(), 7, new GridPosition(9, 9), false),
        new RepairAll(Guid.NewGuid()),
        new UpgradeBuilding(Guid.NewGuid(), 3),
        new UpgradeHearth(Guid.NewGuid()),
        new DemolishBuilding(Guid.NewGuid(), 4),
        new RepairBuilding(Guid.NewGuid(), 5),
        new Collect(Guid.NewGuid(), 6),
        new Collect(Guid.NewGuid(), null),
        new ClearTerrain(Guid.NewGuid(), new GridPosition(2, 2)),
        new BeginNight(Guid.NewGuid()),
        new ResolveNight(Guid.NewGuid(), 3, new[] { new PlayerInput(120, PlayerActionType.HotStones, new GridPosition(5, 6)), new PlayerInput(300, PlayerActionType.OpenSluiceGate) }),
        new AcknowledgeReport(Guid.NewGuid()),
        new ClaimErrand(Guid.NewGuid()),
        new RecordNightResult(Guid.NewGuid(), 2, 3, new[] { 4, 5 }),
        new FinishConstructionNow(Guid.NewGuid()),
        new DebugRaiseTension(Guid.NewGuid(), 50),
        new DebugSkipWarning(Guid.NewGuid()),
        new SendMissionary(Guid.NewGuid(), 7, 4),
        new SendMissionary(Guid.NewGuid(), 7, 4, PreacherId: 2),
        new SendWorldMissionary(Guid.NewGuid(), 1201, 8, 3),
        new SendWorldMissionary(Guid.NewGuid(), 1201, 8, 3, PreacherId: 1),
        new MeetDemand(Guid.NewGuid(), 4),
        new ClaimDaily(Guid.NewGuid(), 2),
        new ClaimThermos(Guid.NewGuid()),
        new SpendTowels(Guid.NewGuid(), TowelUse.Reroll, 1),
        new SpendTowels(Guid.NewGuid(), TowelUse.Hurry),
        new SetDayClock(Guid.NewGuid(), 120),
        new SetCrew(Guid.NewGuid(), 4, 1),
        new CollectOfferings(Guid.NewGuid()),
        new PayTithe(Guid.NewGuid(), new ResourceBag(100, 20, 30, 40)),
        new PleadWithLord(Guid.NewGuid(), 3, Plea.Flatter),
        new ChooseTale(Guid.NewGuid(), 12, 1),
        new StartResearch(Guid.NewGuid(), "sharper-axes"),
        new CancelResearch(Guid.NewGuid()),
        new CancelConstruction(Guid.NewGuid(), 7),
        new Trade(Guid.NewGuid(), ResourceType.Timber, ResourceType.Peat, 25),
    };

    [Theory]
    [MemberData(nameof(EveryCommand))]
    public void CommandMapping_RoundTripThroughJson_GivesTheSameCommand(BaseCommand command)
    {
        // Act
        var json = JsonSerializer.Serialize(CommandMapping.ToDto(command), VillageJson.Options);
        var back = CommandMapping.ToCommand(JsonSerializer.Deserialize<CommandDto>(json, VillageJson.Options)!);

        // Assert
        back.Should().BeEquivalentTo(command, o => o.RespectingRuntimeTypes());
    }

    [Theory]
    [InlineData("{\"type\":\"UpgradeHearth\"}")]
    [InlineData("{\"id\":\"00000000-0000-0000-0000-000000000000\",\"type\":\"UpgradeHearth\"}")]
    [InlineData("{\"id\":\"8b1c2a3e-1111-4222-8333-944455556666\",\"type\":\"SummonDragon\"}")]
    [InlineData("{\"id\":\"8b1c2a3e-1111-4222-8333-944455556666\",\"type\":\"PlaceBuilding\",\"building\":\"Castle\"}")]
    public void CommandMapping_Malformed_IsRefused(string json)
    {
        // Arrange
        var dto = JsonSerializer.Deserialize<CommandDto>(json, VillageJson.Options)!;

        // Act
        var act = () => CommandMapping.ToCommand(dto);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void VillageJson_MidNightVillage_RoundTripsEverything()
    {
        // Arrange — a village with buildings, a Night in progress and an errand under way.
        var state = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0);
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, new GridPosition(3, 9)), T0);
        state.Apply(new BeginNight(Guid.NewGuid()), T0);
        var data = state.ToData();

        // Act
        var back = VillageJson.Read(VillageJson.Write(data));

        // Assert
        back.Should().BeEquivalentTo(data, o => o.ComparingByMembers<NightPlan>());
    }

    [Fact]
    public void VillageJson_MissionaryOnTheRoad_RoundTripsTheDomain()
    {
        // Arrange — Hearth 2, a missionary out, influence landed somewhere, a tithe paid.
        var table = new BuildingTable { StartingStock = new ResourceBag(5000, 900, 900, 900), HearthStorageCaps = new[] { 9000, 9000, 9000 } };
        var state = BaseState.CreateNew(table, VillageMap.Demo, T0);
        state.Apply(new UpgradeHearth(Guid.NewGuid()), T0);
        var later = T0.AddHours(1);
        state.Apply(new SendMissionary(Guid.NewGuid(), 1, 1), later);
        state.Snapshot(later.AddHours(3));
        state.Apply(new SendMissionary(Guid.NewGuid(), 2, 2), later.AddHours(3));
        state.Apply(new PayTithe(Guid.NewGuid(), new ResourceBag(100, 0, 0, 0)), later.AddHours(3));
        state.Apply(new StartResearch(Guid.NewGuid(), "sermon-notes"), later.AddHours(3));
        var data = state.ToData();

        // Act
        var back = VillageJson.Read(VillageJson.Write(data));

        // Assert
        back.Domain.Should().NotBeNull();
        back.Domain.Should().BeEquivalentTo(data.Domain);
        back.Research.Should().BeEquivalentTo(data.Research);
        data.Research!.Current.Should().Be("sermon-notes");
        BaseState.Restore(table, VillageMap.Demo, back).Snapshot(later.AddDays(2)).Domain.Should()
            .BeEquivalentTo(BaseState.Restore(table, VillageMap.Demo, data).Snapshot(later.AddDays(2)).Domain);
    }

    [Fact]
    public void VillageJson_DemandPreachersAndReports_RoundTrip()
    {
        // Arrange — Hearth 2 done, Brother Damp home from 4 hours of preaching, the lord's first demand open.
        var table = new BuildingTable { StartingStock = new ResourceBag(5000, 900, 900, 900), HearthStorageCaps = new[] { 9000, 9000, 9000 } };
        var state = BaseState.CreateNew(table, VillageMap.Demo, T0);
        state.Apply(new UpgradeHearth(Guid.NewGuid()), T0);
        state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);
        state.Apply(new SendMissionary(Guid.NewGuid(), 1, 4, PreacherId: 1), T0);
        var asked = T0.AddHours(1); // the first letter (half an hour after Hearth 2), stalled so it's still open when he's home
        state.Apply(new PleadWithLord(Guid.NewGuid(), state.Snapshot(asked).Domain!.Demand!.Id, Plea.Stall), asked).Status.Should().Be(CommandStatus.Accepted);
        var later = T0.AddHours(5);
        var domain = state.Snapshot(later).Domain!;
        domain.Demand.Should().NotBeNull();
        state.Apply(new ChooseTale(Guid.NewGuid(), domain.Tales!.Single().Id, 1), later);
        state.Apply(new SendMissionary(Guid.NewGuid(), 1, 1, PreacherId: 1), later);
        later = later.AddHours(2);
        state.Snapshot(later).Domain!.Tales.Should().ContainSingle("a second tale waits");
        var data = state.ToData();

        // Act
        var back = VillageJson.Read(VillageJson.Write(data));

        // Assert
        back.Domain.Should().BeEquivalentTo(data.Domain);
        back.Domain!.Reports.Should().ContainSingle();
        back.Domain.Demand.Should().NotBeNull();
        BaseState.Restore(table, VillageMap.Demo, back).Snapshot(later).Domain.Should()
            .BeEquivalentTo(BaseState.Restore(table, VillageMap.Demo, data).Snapshot(later).Domain);
    }

    [Fact]
    public void VillageJson_DailyList_RoundTrips()
    {
        // Arrange
        var data = BaseState.CreateNew(new BuildingTable(), VillageMap.Demo, T0).ToData() with
        {
            ErrandIndex = ErrandBook.Default.Errands.Count,
            Daily = new DailyData(20376, 120, new[] { "preach", "build", "trade" }, new[] { 3, 2, 0 }, new[] { false, true, false }, false, 4, 20375, 9, new[] { "golden-duck" }),
        };

        // Act
        var back = VillageJson.Read(VillageJson.Write(data));

        // Assert
        back.Daily.Should().BeEquivalentTo(data.Daily);
    }

    [Fact]
    public void NightPlan_DefendersAndAutoRelics_RoundTrip()
    {
        // Arrange
        var plan = new NightPlan(3, 99, 40, 4, 0, false, [], [], [], 2, AutoRelics: true, Defenders: new[] { new GridPosition(5, 6), new GridPosition(5, 6) });

        // Act
        var back = VillageSaveMapping.PlanFromDto(VillageSaveMapping.PlanToDto(plan));

        // Assert
        back.AutoRelics.Should().BeTrue();
        back.Defenders.Should().Equal(plan.Defenders);
    }

    [Fact]
    public void NightPlan_ResearchBonuses_RoundTrip()
    {
        // Arrange — research is frozen into the plan, so the server replays the Night the phone fought.
        var plan = new NightPlan(3, 99, 40, 4, 1, false, [], [], [], 2, AutoRelics: true, TowerDamagePercent: 40, ProphetHealthPercent: 25);

        // Act
        var back = VillageSaveMapping.PlanFromDto(VillageSaveMapping.PlanToDto(plan));

        // Assert
        back.TowerDamagePercent.Should().Be(40);
        back.ProphetHealthPercent.Should().Be(25);
    }

    [Fact]
    public void VillageJson_ErrandId_RoundTrips()
    {
        // Arrange
        var data = BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0).ToData() with { ErrandId = "study-2" };

        // Act
        var back = VillageJson.Read(VillageJson.Write(data));

        // Assert
        back.ErrandId.Should().Be("study-2");
    }

    [Fact]
    public void VillageJson_ChosenCrew_RoundTripsAndFullStaysNull()
    {
        // Arrange — a lean-to emptied by the player; the Hearth untouched.
        var table = new BuildingTable { StartingStock = new ResourceBag(5000, 900, 900, 900) };
        var state = BaseState.CreateNew(table, VillageMap.Demo, T0);
        state.Apply(new PlaceBuilding(Guid.NewGuid(), BuildingType.Woodcutter, new GridPosition(15, 5)), T0);
        state.Apply(new FinishConstructionNow(Guid.NewGuid()), T0);
        var lean = state.Snapshot(T0).Buildings.Single(b => b.Type == BuildingType.Woodcutter).Id;
        state.Apply(new SetCrew(Guid.NewGuid(), lean, 0), T0);
        var data = state.ToData();

        // Act
        var back = VillageJson.Read(VillageJson.Write(data));

        // Assert
        back.Buildings.Single(b => b.Id == lean).Crew.Should().Be(0);
        back.Buildings.Where(b => b.Id != lean).Should().OnlyContain(b => b.Crew == null);
    }

    [Fact]
    public void VillageJson_Document_UsesTheDeviceSaveFieldNames()
    {
        // Act — the Unity client reads this with JsonUtility, which matches field names exactly.
        var json = VillageJson.Write(BaseState.CreateNew(BuildingTable.Default, VillageMap.Demo, T0).ToData());

        // Assert
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("format").GetInt32().Should().Be(1);
        doc.RootElement.GetProperty("timber").GetInt32().Should().Be(500);
        doc.RootElement.GetProperty("buildings")[0].GetProperty("type").GetString().Should().Be("Hearth");
        doc.RootElement.GetProperty("errandIndex").GetInt32().Should().Be(0);
    }
}

public class DevCommandMappingTests
{
    [Fact]
    public void IsDevelopmentOnly_TestingCommands_AreRefusedByServers()
    {
        // Act & Assert
        CommandMapping.IsDevelopmentOnly(new DebugFillStores(Guid.NewGuid())).Should().BeTrue();
        CommandMapping.IsDevelopmentOnly(new DebugEndShield(Guid.NewGuid())).Should().BeTrue();
        CommandMapping.IsDevelopmentOnly(new Collect(Guid.NewGuid(), null)).Should().BeFalse();
    }

    [Fact]
    public void ToCommand_DevCommandsByName_RoundTrip()
    {
        // Act
        var fill = CommandMapping.ToCommand(CommandMapping.ToDto(new DebugFillStores(Guid.NewGuid())));
        var shield = CommandMapping.ToCommand(CommandMapping.ToDto(new DebugEndShield(Guid.NewGuid())));

        // Assert
        fill.Should().BeOfType<DebugFillStores>();
        shield.Should().BeOfType<DebugEndShield>();
    }

    private static readonly DateTimeOffset T0 = new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ToCommand_ServerOnlyWorldCommands_HaveNoWireForm()
    {
        // Arrange
        var commands = new BaseCommand[]
        {
            new JoinWorld(Guid.NewGuid(), 3, 4),
            new WorldUpdate(Guid.NewGuid(), 5000, 8),
            new ReceiveOfferings(Guid.NewGuid(), new ResourceBag(10, 0, 0, 0)),
        };

        foreach (var command in commands)
        {
            // Act
            Action act = () => CommandMapping.ToCommand(new CommandDto { id = Guid.NewGuid().ToString(), type = command.GetType().Name });

            // Assert
            act.Should().Throw<ArgumentException>($"a client must never send {command.GetType().Name}");
            CommandMapping.IsServerOnly(command).Should().BeTrue();
        }
    }

    [Fact]
    public void VillageJson_WorldVillage_RoundTripsTheLinkAndMissionaries()
    {
        // Arrange — on a world at (12, -7), a world rate, a missionary 3 hexes out.
        var table = new BuildingTable { StartingStock = new ResourceBag(5000, 900, 900, 900), HearthStorageCaps = new[] { 9000, 9000, 9000 } };
        var state = BaseState.CreateNew(table, VillageMap.Demo, T0);
        state.Apply(new UpgradeHearth(Guid.NewGuid()), T0);
        var later = T0.AddHours(1);
        state.Apply(new JoinWorld(Guid.NewGuid(), 12, -7), later);
        state.Apply(new WorldUpdate(Guid.NewGuid(), 3500, 0), later);
        state.Apply(new SendWorldMissionary(Guid.NewGuid(), 1201, 2, 3), later).Status.Should().Be(CommandStatus.Accepted);
        var data = state.ToData();

        // Act
        var back = VillageJson.Read(VillageJson.Write(data));

        // Assert
        back.Domain.Should().BeEquivalentTo(data.Domain);
        back.Domain!.World.Should().Be(new WorldLink(12, -7, 3500));
        back.Domain.Missionaries.Should().ContainSingle().Which.Distance.Should().Be(3);
    }
}
