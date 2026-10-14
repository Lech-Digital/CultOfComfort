using System;
using System.Collections.Generic;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Base;

/// <summary>An intent sent to whoever holds authority over the village (the device today, the server later).</summary>
public abstract record BaseCommand(Guid CommandId);

/// <param name="Position">The anchor: the footprint's lowest x and y.</param>
/// <param name="Rotated">Turned a quarter: width and depth swap.</param>
public sealed record PlaceBuilding(Guid CommandId, BuildingType Type, GridPosition Position, bool Rotated = false) : BaseCommand(CommandId);

/// <summary>Paints roads, fences or walls over many tiles at once: tiles that can't take it are skipped.</summary>
public sealed record PaintTiles(Guid CommandId, BuildingType Type, IReadOnlyList<GridPosition> Tiles) : BaseCommand(CommandId);

/// <summary>Picks a building up and sets it down elsewhere: free, keeping its level, output, construction and damage.</summary>
public sealed record MoveBuilding(Guid CommandId, int BuildingId, GridPosition Position, bool Rotated) : BaseCommand(CommandId);

/// <summary>Repairs every wrecked building at once, or none if the village can't pay for all of them.</summary>
public sealed record RepairAll(Guid CommandId) : BaseCommand(CommandId);

public sealed record UpgradeBuilding(Guid CommandId, int BuildingId) : BaseCommand(CommandId);

public sealed record UpgradeHearth(Guid CommandId) : BaseCommand(CommandId);

public sealed record DemolishBuilding(Guid CommandId, int BuildingId) : BaseCommand(CommandId);

/// <param name="BuildingId">Null collects from every producer.</param>
public sealed record Collect(Guid CommandId, int? BuildingId) : BaseCommand(CommandId);

/// <summary>Development and tests only (a server refuses it): records a Night's outcome directly. Real Nights use <see cref="ResolveNight"/>.</summary>
/// <param name="WreckedBuildingIds">Buildings smashed to nothing in the Night (structure ids are building ids).</param>
public sealed record RecordNightResult(Guid CommandId, int Night, int Stars, IReadOnlyList<int>? WreckedBuildingIds = null) : BaseCommand(CommandId);

public sealed record RepairBuilding(Guid CommandId, int BuildingId) : BaseCommand(CommandId);

/// <summary>Starts a Night now: the raid that is coming, or an early one sized by the current Tension.</summary>
public sealed record BeginNight(Guid CommandId) : BaseCommand(CommandId);

/// <summary>Ends the Night begun with <see cref="BeginNight"/>: only the input log is sent; the village replays it.</summary>
public sealed record ResolveNight(Guid CommandId, int Night, IReadOnlyList<CultOfComfort.Simulation.Combat.PlayerInput> Inputs) : BaseCommand(CommandId);

/// <summary>The player has seen the latest Night report.</summary>
public sealed record AcknowledgeReport(Guid CommandId) : BaseCommand(CommandId);

/// <summary>Takes the reward for the current errand (only once it is done) and moves on to the next one.</summary>
public sealed record ClaimErrand(Guid CommandId) : BaseCommand(CommandId);

/// <summary>Cuts a wild tree (Timber) or clears a loose boulder (costs Timber, gives Scrap). Walls are bedrock.</summary>
public sealed record ClearTerrain(Guid CommandId, GridPosition Tile) : BaseCommand(CommandId);

/// <summary>Development only: adds Tension at once. A server refuses this.</summary>
public sealed record DebugRaiseTension(Guid CommandId, int Amount) : BaseCommand(CommandId);

/// <summary>Development only: ends a raid's warning now. A server refuses this.</summary>
public sealed record DebugSkipWarning(Guid CommandId) : BaseCommand(CommandId);

/// <summary>Development only: fills every store to its cap (for testing the later Hearth levels). A server refuses this.</summary>
public sealed record DebugFillStores(Guid CommandId) : BaseCommand(CommandId);

/// <summary>Development only: completes the running construction. A server refuses (or charges for) this.</summary>
public sealed record FinishConstructionNow(Guid CommandId) : BaseCommand(CommandId);

public enum CommandStatus
{
    Accepted,
    Duplicate,
    Rejected,
}

/// <param name="Collected">Resources gained by a Collect or night salvage.</param>
public sealed record CommandOutcome(CommandStatus Status, long Version, string? Reason = null, ResourceBag Collected = default, NightPlan? Plan = null, NightReport? Report = null)
{
    internal static CommandOutcome Accept(ResourceBag collected = default) => new(CommandStatus.Accepted, 0, null, collected);

    internal static CommandOutcome Reject(string reason) => new(CommandStatus.Rejected, 0, reason);
}
