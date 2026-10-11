using System;
using System.Collections.Generic;

namespace CultOfComfort.Simulation.Base;

/// <summary>The village as stored (device save today, database row later). Mapped to and from <see cref="BaseState"/>.</summary>
public sealed record BaseData(long Version, ResourceBag Stock, int NextId, int LastNightRecorded, IReadOnlyList<BuildingData> Buildings, IReadOnlyList<Guid> RecentCommands);

public sealed record BuildingData(int Id, BuildingType Type, int Level, int X, int Y, long? UpgradeEndsAtUtcTicks, long BufferMilli, long AsOfUtcTicks);

public sealed class InvalidBaseDataException : Exception
{
    public InvalidBaseDataException(string message) : base(message)
    {
    }
}
