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
    public List<long> fractionsMilli; // thousandths beyond the whole stock (Timber, Peat, Scrap, Devotion); auto-collect
    public bool cold;
    public int errandIndex; // older saves lack it: 0, so they start the errands (already-true ones are claimable at once)
    public bool errandEventDone;
    public string errandId;
    public int raidersRoad; // pavings laid on the raiders' road (0: trodden mud)
    public List<StowedDto> stowed; // towers packed up // the errand by id (errandIndex alone moved players when the book was reordered); empty: read errandIndex the old way
    public bool hasDomain; // older saves: no Marchlands yet
    public DomainDto domain;
    public List<string> researched;   // older saves: none
    public string researching;        // empty: nothing under way
    public long researchEndsTicks;
    public bool hasDaily;             // older saves: no daily list yet
    public DailyDto daily;
}

/// <summary>The Prophet's daily list (spec 2026-10-15): today's errands, the Thermos, the streak, the Towels.</summary>
[Serializable]
public sealed class DailyDto
{
    public int day;
    public int offsetMinutes;
    public List<string> ids;
    public List<int> progress;
    public List<bool> claimed;
    public bool thermos;
    public int streak;
    public int lastThermosDay;
    public int towels;
    public List<string> cosmetics;
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

    /// <summary>On a shared world (spec 2026-10-14 shared world): where the village stands, and the world's Tension rate.</summary>
    public bool inWorld;
    public int worldQ;
    public int worldR;
    public int worldTensionMilli;
    public int worldPatronages;

    /// <summary>The Mission House (spec 2026-10-15): preachers' experience and the latest road reports.</summary>
    public List<PreacherXpDto> preachers;
    public List<RoadReportDto> reports;

    /// <summary>The lord's demand, if one is open, and when the next comes (-1: the clock hasn't started).</summary>
    public bool hasDemand;
    public int demandId;
    public int demandTimber;
    public int demandPeat;
    public int demandScrap;
    public int demandDevotion;
    public long demandDueTicks;
    public long nextDemandTicks = -1;
    public int nextDemandId = 1;

    /// <summary>What you've tried on him over the open demand (spec 2026-10-16 the lord's hall).</summary>
    public bool demandStalled;
    public string demandHaggle;
    public bool demandFlattered;

    /// <summary>Road tales waiting for you to choose (spec 2026-10-16 mission board).</summary>
    public List<TaleDto> tales;
}

[Serializable]
public sealed class TaleDto
{
    public int id;
    public int preacher;
    public int node;
    public bool inWorld;
    public int hours;
    public string tale;
    public long homeTicks;
}

[Serializable]
public sealed class PreacherXpDto
{
    public int id;
    public int xp;
}

[Serializable]
public sealed class RoadReportDto
{
    public int preacher;
    public int node;
    public bool inWorld;
    public int hours;
    public string find;
    public string tale; // empty for a report from before the tales
    public int choice;
    public int timber;
    public int peat;
    public int scrap;
    public int devotion;
    public long homeTicks;
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
    public int distance; // hexes to a shared-world place; 0 in the Marchlands
    public int preacher; // the Mission House preacher who went; 0 for one sent before it
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
    public bool autoRelics;          // older plans lack it: the relics wait for the player
    public List<TileDto> defenders;  // older plans lack it: nobody comes out to defend
    public int towerDamage;          // research, % (older plans lack it: 0, the default balance)
    public int prophetHealth;        // research, %
    public int road;                 // which raiders' road (0: the first)
}

[Serializable]
public sealed class TowerDto
{
    public string type;
    public int x;
    public int y;
    public int level; // 0 (older plans): level 1
    public bool manned;
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
    public bool flipped;
    public bool patrol; // a Guardhouse's wardens fight rather than man towers
    public int crew = -1; // the crew the player chose; -1: a full crew
}

[Serializable]
public sealed class StowedDto
{
    public string type;
    public int level;
    public bool wrecked;
}

#pragma warning restore SA1307, SA1401, CS8618
