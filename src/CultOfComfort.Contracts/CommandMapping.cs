using System;
using System.Collections.Generic;
using System.Linq;
using CultOfComfort.Simulation.Base;
using CultOfComfort.Simulation.Combat;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Contracts;

/// <summary>Anti-corruption mapping between village commands and their wire form, both ways.</summary>
public static class CommandMapping
{
    public static CommandDto ToDto(BaseCommand command)
    {
        var dto = new CommandDto { id = command.CommandId.ToString(), type = command.GetType().Name };
        switch (command)
        {
            case PlaceBuilding c:
                (dto.building, dto.x, dto.y, dto.rotated, dto.flipped) = (c.Type.ToString(), c.Position.X, c.Position.Y, c.Rotated, c.Flipped);
                break;
            case PaintTiles c:
                dto.building = c.Type.ToString();
                dto.tiles = c.Tiles.Select(t => new TileDto { x = t.X, y = t.Y }).ToList();
                break;
            case MoveBuilding c:
                (dto.buildingId, dto.x, dto.y, dto.rotated, dto.flipped) = (c.BuildingId, c.Position.X, c.Position.Y, c.Rotated, c.Flipped);
                break;
            case UpgradeBuilding c:
                dto.buildingId = c.BuildingId;
                break;
            case DemolishBuilding c:
                dto.buildingId = c.BuildingId;
                break;
            case PackUpTower c:
                dto.buildingId = c.BuildingId;
                break;
            case AssignNotable c:
                (dto.building, dto.buildingId) = (c.NotableId, c.BuildingId);
                break;
            case ChooseProphetPerk c:
                dto.building = c.PerkId;
                break;
            case AnswerVisitor c:
                dto.amount = c.Accept ? 1 : 0;
                break;
            case RepairBuilding c:
                dto.buildingId = c.BuildingId;
                break;
            case Collect c:
                dto.buildingId = c.BuildingId ?? -1;
                break;
            case ClearTerrain c:
                (dto.x, dto.y) = (c.Tile.X, c.Tile.Y);
                break;
            case ResolveNight c:
                dto.night = c.Night;
                dto.inputs = c.Inputs.Select(i => new InputDto { tick = i.Tick, action = i.Type.ToString(), x = i.Target.X, y = i.Target.Y }).ToList();
                break;
            case ResolveTrial c:
                dto.night = c.Stage;
                dto.inputs = c.Inputs.Select(i => new InputDto { tick = i.Tick, action = i.Type.ToString(), x = i.Target.X, y = i.Target.Y }).ToList();
                break;
            case RecordNightResult c:
                (dto.night, dto.stars) = (c.Night, c.Stars);
                dto.wrecked = c.WreckedBuildingIds?.ToList() ?? new List<int>();
                break;
            case DebugRaiseTension c:
                dto.amount = c.Amount;
                break;
            case SendMissionary c:
                (dto.buildingId, dto.amount, dto.y) = (c.NodeId, c.Hours, c.PreacherId);
                break;
            case MeetDemand c:
                dto.amount = c.DemandId;
                break;
            case PleadWithLord c:
                (dto.amount, dto.give) = (c.DemandId, c.Plea.ToString());
                break;
            case ChooseTale c:
                (dto.buildingId, dto.amount) = (c.TaleId, c.Choice);
                break;
            case ClaimDaily c:
                dto.amount = c.Slot;
                break;
            case SpendTowels c:
                (dto.give, dto.amount) = (c.Use.ToString(), c.Slot);
                break;
            case SetDayClock c:
                dto.amount = c.OffsetMinutes;
                break;
            case SetCrew c:
                (dto.buildingId, dto.amount) = (c.BuildingId, c.Crew);
                break;
            case SetDuty c:
                (dto.buildingId, dto.amount) = (c.BuildingId, c.Patrol ? 1 : 0);
                break;
            case SendWorldMissionary c:
                (dto.buildingId, dto.amount, dto.x, dto.y) = (c.NodeId, c.Hours, c.Distance, c.PreacherId);
                break;
            case StartResearch c:
                dto.tech = c.TechId;
                break;
            case CancelConstruction c:
                dto.buildingId = c.BuildingId;
                break;
            case Trade c:
                (dto.give, dto.get, dto.amount) = (c.Give.ToString(), c.Get.ToString(), c.Amount);
                break;
            case PayTithe c:
                (dto.timber, dto.peat, dto.scrap, dto.devotion) = (c.Amount.Timber, c.Amount.Peat, c.Amount.Scrap, c.Amount.Devotion);
                break;
        }

        return dto;
    }

    /// <exception cref="ArgumentException">The command is unknown or malformed.</exception>
    public static BaseCommand ToCommand(CommandDto dto)
    {
        if (dto == null || !Guid.TryParse(dto.id, out var id) || id == Guid.Empty)
        {
            throw new ArgumentException("Every command needs an id (a GUID).");
        }

        return dto.type switch
        {
            nameof(PlaceBuilding) => new PlaceBuilding(id, Building(dto), new GridPosition(dto.x, dto.y), dto.rotated, dto.flipped),
            nameof(PaintTiles) => new PaintTiles(id, Building(dto), (dto.tiles ?? new List<TileDto>()).Select(t => new GridPosition(t.x, t.y)).ToList()),
            nameof(MoveBuilding) => new MoveBuilding(id, dto.buildingId, new GridPosition(dto.x, dto.y), dto.rotated, dto.flipped),
            nameof(RepairAll) => new RepairAll(id),
            nameof(PaveRaidersRoad) => new PaveRaidersRoad(id),
            nameof(UpgradeBuilding) => new UpgradeBuilding(id, dto.buildingId),
            nameof(UpgradeHearth) => new UpgradeHearth(id),
            nameof(DemolishBuilding) => new DemolishBuilding(id, dto.buildingId),
            nameof(PackUpTower) => new PackUpTower(id, dto.buildingId),
            nameof(PackUpTowers) => new PackUpTowers(id),
            nameof(AnswerVisitor) => new AnswerVisitor(id, dto.amount != 0),
            nameof(ChooseProphetPerk) => new ChooseProphetPerk(id, dto.building ?? string.Empty),
            nameof(AssignNotable) => new AssignNotable(id, dto.building ?? string.Empty, dto.buildingId),
            nameof(RepairBuilding) => new RepairBuilding(id, dto.buildingId),
            nameof(Collect) => new Collect(id, dto.buildingId < 0 ? null : dto.buildingId),
            nameof(ClearTerrain) => new ClearTerrain(id, new GridPosition(dto.x, dto.y)),
            nameof(BeginNight) => new BeginNight(id),
            nameof(ResolveNight) => new ResolveNight(id, dto.night, (dto.inputs ?? new List<InputDto>()).Select(Input).ToList()),
            nameof(ResolveTrial) => new ResolveTrial(id, dto.night, (dto.inputs ?? new List<InputDto>()).Select(Input).ToList()),
            nameof(BeginTrial) => new BeginTrial(id),
            nameof(ClaimAlms) => new ClaimAlms(id),
            nameof(AcknowledgeReport) => new AcknowledgeReport(id),
            nameof(ClaimErrand) => new ClaimErrand(id),
            nameof(RecordNightResult) => new RecordNightResult(id, dto.night, dto.stars, dto.wrecked),
            nameof(FinishConstructionNow) => new FinishConstructionNow(id),
            nameof(DebugRaiseTension) => new DebugRaiseTension(id, dto.amount),
            nameof(DebugSkipWarning) => new DebugSkipWarning(id),
            nameof(DebugFillStores) => new DebugFillStores(id),
            nameof(DebugEndShield) => new DebugEndShield(id),
            nameof(SendMissionary) => new SendMissionary(id, dto.buildingId, dto.amount, dto.y),
            nameof(MeetDemand) => new MeetDemand(id, dto.amount),
            nameof(PleadWithLord) => new PleadWithLord(id, dto.amount, Enum.TryParse<Plea>(dto.give, out var plea) ? plea : throw new ArgumentException("He doesn't follow.")),
            nameof(ChooseTale) => new ChooseTale(id, dto.buildingId, dto.amount),
            nameof(ClaimDaily) => new ClaimDaily(id, dto.amount),
            nameof(ClaimThermos) => new ClaimThermos(id),
            nameof(SpendTowels) => new SpendTowels(id, Enum.TryParse<TowelUse>(dto.give, out var use) ? use : throw new ArgumentException("Towels don't do that."), dto.amount),
            nameof(SetDayClock) => new SetDayClock(id, dto.amount),
            nameof(SetCrew) => new SetCrew(id, dto.buildingId, dto.amount),
            nameof(SetDuty) => new SetDuty(id, dto.buildingId, dto.amount != 0),
            nameof(SendWorldMissionary) => new SendWorldMissionary(id, dto.buildingId, dto.amount, dto.x, dto.y),
            nameof(CollectOfferings) => new CollectOfferings(id),
            nameof(StartResearch) => new StartResearch(id, dto.tech ?? string.Empty),
            nameof(CancelResearch) => new CancelResearch(id),
            nameof(CancelConstruction) => new CancelConstruction(id, dto.buildingId),
            nameof(Trade) => new Trade(id, Resource(dto.give), Resource(dto.get), dto.amount),
            nameof(PayTithe) => new PayTithe(id, new ResourceBag(dto.timber, dto.peat, dto.scrap, dto.devotion)),
            _ => throw new ArgumentException($"Unknown command '{dto.type}'."),
        };
    }

    /// <summary>
    /// Commands only the server's world service gives a village (joining, Tension from the world, offerings). They have
    /// no wire form: <see cref="ToCommand"/> never builds one, so no client can send them.
    /// </summary>
    public static bool IsServerOnly(BaseCommand command) => command is JoinWorld or WorldUpdate or ReceiveOfferings;

    /// <summary>Development helpers a real server must refuse: they hand out time, Tension or Night results.</summary>
    public static bool IsDevelopmentOnly(BaseCommand command) =>
        command is RecordNightResult or FinishConstructionNow or DebugRaiseTension or DebugSkipWarning or DebugFillStores or DebugEndShield;

    public static OutcomeDto ToDto(Guid id, CommandOutcome outcome) => new()
    {
        id = id.ToString(),
        status = outcome.Status.ToString(),
        reason = outcome.Reason ?? string.Empty,
        timber = outcome.Collected.Timber,
        peat = outcome.Collected.Peat,
        scrap = outcome.Collected.Scrap,
        devotion = outcome.Collected.Devotion,
        hasPlan = outcome.Plan != null,
        plan = outcome.Plan == null ? new NightDto() : VillageSaveMapping.ToDto(outcome.Plan),
        hasReport = outcome.Report != null,
        report = outcome.Report == null ? new ReportDto() : VillageSaveMapping.ToDto(outcome.Report),
    };

    private static ResourceType Resource(string? name) =>
        Enum.TryParse<ResourceType>(name, out var type) ? type : throw new ArgumentException($"Unknown resource '{name}'.");

    private static BuildingType Building(CommandDto dto) =>
        Enum.TryParse<BuildingType>(dto.building, out var type) ? type : throw new ArgumentException($"Unknown building '{dto.building}'.");

    private static PlayerInput Input(InputDto input) =>
        Enum.TryParse<PlayerActionType>(input.action, out var action)
            ? new PlayerInput(input.tick, action, new GridPosition(input.x, input.y))
            : throw new ArgumentException($"Unknown action '{input.action}'.");
}
