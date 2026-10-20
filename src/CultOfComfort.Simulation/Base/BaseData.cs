using System;
using System.Collections.Generic;

namespace CultOfComfort.Simulation.Base;

/// <summary>The village as stored (device save today, database row later). Mapped to and from <see cref="BaseState"/>.</summary>
public sealed record BaseData(
    long Version,
    ResourceBag Stock,
    int NextId,
    int LastNightRecorded,
    IReadOnlyList<BuildingData> Buildings,
    IReadOnlyList<Guid> RecentCommands,
    long TensionMilli = 0,
    long TensionAsOfUtcTicks = 0,
    long? ShieldEndsAtUtcTicks = null,
    int NextRaidId = 1,
    RaidData? Raid = null,
    NightPlan? NightInProgress = null,
    long NightStartedAtUtcTicks = 0,
    NightReport? UnseenReport = null,
    IReadOnlyList<CultOfComfort.Simulation.Grid.GridPosition>? ClearedTerrain = null,
    IReadOnlyList<TreeCutData>? TreeCuts = null,
    int LayoutVersion = 0,
    int Population = 0,
    long PopulationAsOfUtcTicks = 0,
    long UpkeepAsOfUtcTicks = 0,
    long UpkeepMilli = 0,
    bool Cold = false,
    int ErrandIndex = 0,
    bool ErrandEventDone = false,
    DomainData? Domain = null,
    ResearchData? Research = null,
    IReadOnlyList<long>? StockFractions = null,
    DailyData? Daily = null,
    string? ErrandId = null,
    int RaidersRoad = 0,
    IReadOnlyList<StowedTower>? Stowed = null);

/// <summary>A tower packed up (spec 2026-10-20 river): set down again for free, at its level, wrecked or not.</summary>
public sealed record StowedTower(BuildingType Type, int Level, bool Wrecked = false);

/// <summary>A wild tree cut at <see cref="CutAtUtcTicks"/>; it grows back after the regrow time.</summary>
public sealed record TreeCutData(int X, int Y, long CutAtUtcTicks);

public sealed record RaidData(int Id, ulong Seed, long WarningEndsAtUtcTicks);

public sealed record BuildingData(int Id, BuildingType Type, int Level, int X, int Y, long? UpgradeEndsAtUtcTicks, long BufferMilli, long AsOfUtcTicks, bool Wrecked = false, bool Rotated = false,
    int? Crew = null, bool Flipped = false, bool Patrol = false);

public sealed class InvalidBaseDataException : Exception
{
    public InvalidBaseDataException(string message) : base(message)
    {
    }
}
