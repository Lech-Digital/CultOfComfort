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
                (dto.building, dto.x, dto.y, dto.rotated) = (c.Type.ToString(), c.Position.X, c.Position.Y, c.Rotated);
                break;
            case PaintTiles c:
                dto.building = c.Type.ToString();
                dto.tiles = c.Tiles.Select(t => new TileDto { x = t.X, y = t.Y }).ToList();
                break;
            case MoveBuilding c:
                (dto.buildingId, dto.x, dto.y, dto.rotated) = (c.BuildingId, c.Position.X, c.Position.Y, c.Rotated);
                break;
            case UpgradeBuilding c:
                dto.buildingId = c.BuildingId;
                break;
            case DemolishBuilding c:
                dto.buildingId = c.BuildingId;
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
            case RecordNightResult c:
                (dto.night, dto.stars) = (c.Night, c.Stars);
                dto.wrecked = c.WreckedBuildingIds?.ToList() ?? new List<int>();
                break;
            case DebugRaiseTension c:
                dto.amount = c.Amount;
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
            nameof(PlaceBuilding) => new PlaceBuilding(id, Building(dto), new GridPosition(dto.x, dto.y), dto.rotated),
            nameof(PaintTiles) => new PaintTiles(id, Building(dto), (dto.tiles ?? new List<TileDto>()).Select(t => new GridPosition(t.x, t.y)).ToList()),
            nameof(MoveBuilding) => new MoveBuilding(id, dto.buildingId, new GridPosition(dto.x, dto.y), dto.rotated),
            nameof(RepairAll) => new RepairAll(id),
            nameof(UpgradeBuilding) => new UpgradeBuilding(id, dto.buildingId),
            nameof(UpgradeHearth) => new UpgradeHearth(id),
            nameof(DemolishBuilding) => new DemolishBuilding(id, dto.buildingId),
            nameof(RepairBuilding) => new RepairBuilding(id, dto.buildingId),
            nameof(Collect) => new Collect(id, dto.buildingId < 0 ? null : dto.buildingId),
            nameof(ClearTerrain) => new ClearTerrain(id, new GridPosition(dto.x, dto.y)),
            nameof(BeginNight) => new BeginNight(id),
            nameof(ResolveNight) => new ResolveNight(id, dto.night, (dto.inputs ?? new List<InputDto>()).Select(Input).ToList()),
            nameof(AcknowledgeReport) => new AcknowledgeReport(id),
            nameof(ClaimErrand) => new ClaimErrand(id),
            nameof(RecordNightResult) => new RecordNightResult(id, dto.night, dto.stars, dto.wrecked),
            nameof(FinishConstructionNow) => new FinishConstructionNow(id),
            nameof(DebugRaiseTension) => new DebugRaiseTension(id, dto.amount),
            nameof(DebugSkipWarning) => new DebugSkipWarning(id),
            nameof(DebugFillStores) => new DebugFillStores(id),
            nameof(DebugEndShield) => new DebugEndShield(id),
            _ => throw new ArgumentException($"Unknown command '{dto.type}'."),
        };
    }

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

    private static BuildingType Building(CommandDto dto) =>
        Enum.TryParse<BuildingType>(dto.building, out var type) ? type : throw new ArgumentException($"Unknown building '{dto.building}'.");

    private static PlayerInput Input(InputDto input) =>
        Enum.TryParse<PlayerActionType>(input.action, out var action)
            ? new PlayerInput(input.tick, action, new GridPosition(input.x, input.y))
            : throw new ArgumentException($"Unknown action '{input.action}'.");
}
