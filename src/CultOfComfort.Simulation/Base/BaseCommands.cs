using System;
using System.Collections.Generic;
using CultOfComfort.Simulation.Grid;

namespace CultOfComfort.Simulation.Base;

/// <summary>An intent sent to whoever holds authority over the village (the device today, the server later).</summary>
public abstract record BaseCommand(Guid CommandId);

public sealed record PlaceBuilding(Guid CommandId, BuildingType Type, GridPosition Position) : BaseCommand(CommandId);

public sealed record UpgradeBuilding(Guid CommandId, int BuildingId) : BaseCommand(CommandId);

public sealed record UpgradeHearth(Guid CommandId) : BaseCommand(CommandId);

public sealed record DemolishBuilding(Guid CommandId, int BuildingId) : BaseCommand(CommandId);

/// <param name="BuildingId">Null collects from every producer.</param>
public sealed record Collect(Guid CommandId, int? BuildingId) : BaseCommand(CommandId);

/// <param name="WreckedBuildingIds">Buildings smashed to nothing in the Night (structure ids are building ids).</param>
public sealed record RecordNightResult(Guid CommandId, int Night, int Stars, IReadOnlyList<int>? WreckedBuildingIds = null) : BaseCommand(CommandId);

public sealed record RepairBuilding(Guid CommandId, int BuildingId) : BaseCommand(CommandId);

/// <summary>Development only: adds Tension at once. A server refuses this.</summary>
public sealed record DebugRaiseTension(Guid CommandId, int Amount) : BaseCommand(CommandId);

/// <summary>Development only: ends a raid's warning now. A server refuses this.</summary>
public sealed record DebugSkipWarning(Guid CommandId) : BaseCommand(CommandId);

/// <summary>Development only: completes the running construction. A server refuses (or charges for) this.</summary>
public sealed record FinishConstructionNow(Guid CommandId) : BaseCommand(CommandId);

public enum CommandStatus
{
    Accepted,
    Duplicate,
    Rejected,
}

/// <param name="Collected">Resources gained by a Collect or night salvage.</param>
public sealed record CommandOutcome(CommandStatus Status, long Version, string? Reason = null, ResourceBag Collected = default)
{
    internal static CommandOutcome Accept(ResourceBag collected = default) => new(CommandStatus.Accepted, 0, null, collected);

    internal static CommandOutcome Reject(string reason) => new(CommandStatus.Rejected, 0, reason);
}
