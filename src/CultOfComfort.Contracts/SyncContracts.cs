using System;
using System.Collections.Generic;

namespace CultOfComfort.Contracts;

// The /api/base wire contract (blueprint: "command batch, not state upload"). Same DTO style as the save:
// [Serializable] classes with public fields, so the Unity client can (de)serialise them with JsonUtility.
#pragma warning disable SA1307, SA1401, CS8618 // DTO fields, filled by the serialisers

/// <summary>POST /api/base/sync: the player's intents, applied in order against <see cref="expectedVersion"/>.</summary>
[Serializable]
public sealed class SyncRequestDto
{
    /// <summary>The village version the client last saw; a mismatch is a 409 and the client re-pulls.</summary>
    public long expectedVersion;

    public List<CommandDto> commands;
}

/// <summary>One command. <see cref="type"/> names the command; only the fields that command needs are read.</summary>
[Serializable]
public sealed class CommandDto
{
    /// <summary>Idempotency key (a GUID): a retried command is answered "Duplicate", never applied twice.</summary>
    public string id;

    public string type;
    public string building;
    public int buildingId = -1;
    public int x;
    public int y;
    public bool rotated;
    public List<TileDto> tiles;
    public int night;
    public List<InputDto> inputs;
    public int stars;
    public List<int> wrecked;
    public int amount;
}

/// <summary>One entry of a Night's input log.</summary>
[Serializable]
public sealed class InputDto
{
    public int tick;
    public string action;
    public int x;
    public int y;
}

/// <summary>GET /api/base and POST /api/base/sync: the village as the server holds it now, plus what happened.</summary>
[Serializable]
public sealed class VillageResponseDto
{
    public long version;

    /// <summary>Server clock (UTC ticks): the client evaluates its mirror at server time, not at its own.</summary>
    public long serverTimeTicks;

    public VillageSaveDto village;

    /// <summary>One per command sent, in order (empty for GET).</summary>
    public List<OutcomeDto> outcomes;
}

[Serializable]
public sealed class OutcomeDto
{
    public string id;

    /// <summary>"Accepted", "Duplicate" or "Rejected".</summary>
    public string status;

    public string reason;
    public int timber;
    public int peat;
    public int scrap;
    public int devotion;
    public bool hasPlan;
    public NightDto plan;
    public bool hasReport;
    public ReportDto report;
}

/// <summary>POST /api/auth/guest: a new village and the bearer token that owns it.</summary>
[Serializable]
public sealed class GuestDto
{
    public string playerId;
    public string token;
}

#pragma warning restore SA1307, SA1401, CS8618
