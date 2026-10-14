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
        new CollectOfferings(Guid.NewGuid()),
        new PayTithe(Guid.NewGuid(), new ResourceBag(100, 20, 30, 40)),
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
        var data = state.ToData();

        // Act
        var back = VillageJson.Read(VillageJson.Write(data));

        // Assert
        back.Domain.Should().NotBeNull();
        back.Domain.Should().BeEquivalentTo(data.Domain);
        BaseState.Restore(table, VillageMap.Demo, back).Snapshot(later.AddDays(2)).Domain.Should()
            .BeEquivalentTo(BaseState.Restore(table, VillageMap.Demo, data).Snapshot(later.AddDays(2)).Domain);
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
}
