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
    NightReport? UnseenReport = null);

public sealed record RaidData(int Id, ulong Seed, long WarningEndsAtUtcTicks);

public sealed record BuildingData(int Id, BuildingType Type, int Level, int X, int Y, long? UpgradeEndsAtUtcTicks, long BufferMilli, long AsOfUtcTicks, bool Wrecked = false);

public sealed class InvalidBaseDataException : Exception
{
    public InvalidBaseDataException(string message) : base(message)
    {
    }
}
