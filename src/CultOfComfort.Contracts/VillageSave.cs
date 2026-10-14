using System;
using System.Collections.Generic;

namespace CultOfComfort.Contracts;

// The village document: the device save and the server's wire format are the same JSON.
// Lower-case public fields on purpose: JsonUtility (Unity) serialises fields by their names, and the server's
// System.Text.Json is configured with IncludeFields to write the very same document.
#pragma warning disable SA1307, SA1401, CS8618 // DTO fields, filled by the serialisers

[Serializable]
public sealed class VillageSaveDto
{
    public int format = VillageSaveMapping.CurrentFormat;
    public long version;
    public int timber;
    public int peat;
    public int scrap;
    public int devotion;
    public int nextId;
    public int lastNight;
    public List<BuildingDto> buildings;
    public List<string> recentCommands;
    public long tensionMilli;
    public long tensionAsOfTicks;
    public long shieldEndsAtTicks = -1;
    public int nextRaidId = 1;
    public bool hasRaid;
    public RaidDto raid;
    public bool hasNight;
    public NightDto night;
    public long nightStartedAtTicks;
    public bool hasReport;
    public ReportDto report;
    public List<TileDto> cleared;
    public List<TreeCutDto> treeCuts;
    public int layoutVersion; // 0 in saves from before building shapes: the village migrates them
    public int population;
    public long populationAsOfTicks;
    public long upkeepAsOfTicks;
    public long upkeepMilli;
    public bool cold;
    public int errandIndex; // older saves lack it: 0, so they start the errands (already-true ones are claimable at once)
    public bool errandEventDone;
    public bool hasDomain; // older saves: no Marchlands yet
    public DomainDto domain;
}

/// <summary>The Marchlands (spec 2026-10-14 world map): influence per node, missionaries, the offerings purse.</summary>
[Serializable]
public sealed class DomainDto
{
    public List<InfluenceDto> influence;
    public List<MissionaryDto> missionaries;
    public List<long> offeringsMilli;
    public long asOfTicks;
    public long nextDecayTicks = -1;
    public long lastTitheTicks = -1;
    public int nextMissionaryId = 1;
}

[Serializable]
public sealed class InfluenceDto
{
    public int node;
    public long milli;
}

[Serializable]
public sealed class MissionaryDto
{
    public int id;
    public int node;
    public int hours;
    public long departedTicks;
    public bool preached;
}

[Serializable]
public sealed class TreeCutDto
{
    public int x;
    public int y;
    public long ticks;
}

[Serializable]
public sealed class RaidDto
{
    public int id;
    public string seed = "0";
    public long warningEndsAtTicks;
}

[Serializable]
public sealed class NightDto
{
    public int night;
    public string seed = "0";
    public int budget;
    public int waves;
    public int extraSluice;
    public bool scheduled;
    public int roster; // older saves lack it: 0, the pre-roster raid
    public List<TowerDto> towers;
    public List<TileDto> obstacles;
    public List<StructureDto> structures;
}

[Serializable]
public sealed class TowerDto
{
    public string type;
    public int x;
    public int y;
}

[Serializable]
public sealed class TileDto
{
    public int x;
    public int y;
}

[Serializable]
public sealed class StructureDto
{
    public int id;
    public int x;
    public int y;
    public int max;
    public int health;
    public bool barrier;
    public bool gate;
    public List<TileDto> tiles;
}

[Serializable]
public sealed class ReportDto
{
    public int night;
    public bool scheduled;
    public bool whileAway;
    public int stars;
    public int hearth;
    public int hearthMax;
    public List<int> wrecked;
    public int salvage;
    public string hash;
}

[Serializable]
public sealed class BuildingDto
{
    public int id;
    public string type;
    public int level;
    public int x;
    public int y;
    public long upgradeEndsAtTicks = -1;
    public long bufferMilli;
    public long asOfTicks;
    public bool wrecked;
    public bool rotated;
}

#pragma warning restore SA1307, SA1401, CS8618
