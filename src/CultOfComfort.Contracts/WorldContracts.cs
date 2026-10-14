using System;
using System.Collections.Generic;

namespace CultOfComfort.Contracts;

// The /api/world wire contract (spec 2026-10-14 shared world). The map itself never travels: the client generates it
// from the seed. Only what changes does: cults, influence, the purse.
#pragma warning disable SA1307, SA1401, CS8618 // DTO fields, filled by the serialisers

/// <summary>GET /api/world: your place on the shared world and the window of it around (q, r).</summary>
[Serializable]
public sealed class WorldDto
{
    public int worldId;
    public long seed; // the generator's seed (a ulong, stored as its bit pattern)
    public string cultId;
    public int q;
    public int r;

    /// <summary>The window's centre and radius (hexes).</summary>
    public int viewQ;
    public int viewR;
    public int viewRadius;

    public List<WorldCultDto> cults;
    public List<WorldHoldingDto> holdings;

    /// <summary>Your offerings purse (whole resources), what it earns an hour, and the Tension your influence adds (thousandths an hour).</summary>
    public int purseTimber;
    public int pursePeat;
    public int purseScrap;
    public int purseDevotion;
    public int perHourTimber;
    public int perHourPeat;
    public int perHourScrap;
    public int perHourDevotion;
    public int tensionPerHourMilli;

    public long serverTimeTicks;
}

/// <summary>A cult with a village (or a bot camp) inside the window.</summary>
[Serializable]
public sealed class WorldCultDto
{
    public string id;
    public string name;
    public int q;
    public int r;
    public int colour;
    public bool bot;
}

/// <summary>One cult's influence at one place (thousandths of 100); the Authority's loyalty is whatever is left.</summary>
[Serializable]
public sealed class WorldHoldingDto
{
    public int node;
    public string cult;
    public long milli;
    public bool patron;
}

/// <summary>POST /api/world/collect: what came out of the purse into the stores, and the village after.</summary>
[Serializable]
public sealed class WorldCollectDto
{
    public int timber;
    public int peat;
    public int scrap;
    public int devotion;
    public string reason;
    public VillageResponseDto village;
}
