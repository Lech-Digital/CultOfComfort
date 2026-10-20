using System;
using System.Collections.Generic;
using System.Linq;

namespace CultOfComfort.Simulation.Base;

public enum NodeType
{
    Hamlet,
    Village,
    MarketTown,
    Abbey,
    Keep,

    /// <summary>A Bishop's seat in the shared world (a Warlord's is a <see cref="Keep"/>).</summary>
    Cathedral,
}

/// <summary>A place on the Authority's map (hex axial <see cref="Q"/>, <see cref="R"/>; the village is at 0, 0).</summary>
public sealed record DomainNode(int Id, string Name, NodeType Type, int Q, int R, int Squalor)
{
    /// <summary>Hexes from the village.</summary>
    public int Distance => (Math.Abs(Q) + Math.Abs(R) + Math.Abs(Q + R)) / 2;
}

/// <summary>What each kind of place gives and how it takes to preaching (GDD 04; spec 2026-10-14 world map).</summary>
/// <param name="ReceptivityPercent">Base receptivity × 100.</param>
/// <param name="WeightPercent">How much the Authority minds you there (Tension), × 100.</param>
/// <param name="OfferingsPerHour">Paid per hour at 100 influence.</param>
public sealed record NodeKind(NodeType Type, int ReceptivityPercent, int WeightPercent, ResourceBag OfferingsPerHour);

/// <summary>A missionary on the road: walking out, preaching, walking home. <paramref name="Distance"/> is set for a place on the shared world (hexes from home); null in the Marchlands.
/// <paramref name="PreacherId"/> is the Mission House preacher who went (0: from before the Mission House).</summary>
public sealed record MissionaryData(int Id, int NodeId, int Hours, long DepartedAtUtcTicks, bool Preached, int? Distance = null, int PreacherId = 0);

/// <summary>The village's place on a shared world (spec 2026-10-14 shared world): where it stands, the Tension the world's Authorities add per hour,
/// and how many of the world's places it is Patron of (the server's ledger counts; the Patron errand reads it).</summary>
public sealed record WorldLink(int Q, int R, int TensionPerHourMilli, int Patronages = 0);

/// <summary>The Domain as stored: your influence per node (thousandths), the missionaries out, the offerings purse (thousandths of Timber, Peat, Scrap, Devotion).</summary>
public sealed record DomainData(
    IReadOnlyList<(int NodeId, long InfluenceMilli)> Influence,
    IReadOnlyList<MissionaryData> Missionaries,
    IReadOnlyList<long> OfferingsMilli,
    long AsOfUtcTicks,
    long? NextDecayUtcTicks,
    long? LastTitheUtcTicks,
    int NextMissionaryId,
    WorldLink? World = null,
    IReadOnlyList<(int PreacherId, int Xp)>? PreacherXp = null,
    IReadOnlyList<RoadReport>? Reports = null,
    DemandData? Demand = null,
    long? NextDemandUtcTicks = null,
    int NextDemandId = 1,
    IReadOnlyList<TaleData>? Tales = null);

/// <summary>
/// Lord Wulfric's Marchlands: one fixed domain of 16 places around the village, the Keep five hexes out. The same
/// for everyone (single-player first; the shared shard comes later on the same rules).
/// </summary>
public sealed class DomainMap
{
    public static DomainMap Marchlands { get; } = new(new[]
    {
        new DomainNode(1, "Muddlecombe", NodeType.Hamlet, 0, -1, 80),
        new DomainNode(2, "Puddle End", NodeType.Hamlet, 1, -1, 70),
        new DomainNode(3, "Lower Bogwick", NodeType.Village, -1, -1, 75),
        new DomainNode(4, "Soggy Bottom", NodeType.Hamlet, 2, -2, 90),
        new DomainNode(5, "Little Grumbling", NodeType.Hamlet, 0, -2, 60),
        new DomainNode(6, "Upper Bogwick", NodeType.Village, -1, -2, 55),
        new DomainNode(7, "Puddleford Market", NodeType.MarketTown, 1, -3, 50),
        new DomainNode(8, "Nettlebed", NodeType.Hamlet, 3, -3, 85),
        new DomainNode(9, "Brackwater", NodeType.Village, -2, -1, 70),
        new DomainNode(10, "St. Grumble's Abbey", NodeType.Abbey, 0, -4, 30),
        new DomainNode(11, "Wetherby-in-the-Mire", NodeType.Village, 2, -4, 80),
        new DomainNode(12, "Dampton", NodeType.Hamlet, -2, -2, 65),
        new DomainNode(13, "Drizzleford Market", NodeType.MarketTown, -3, -1, 45),
        new DomainNode(14, "Our Lady of Perpetual Drizzle", NodeType.Abbey, -1, -4, 25),
        new DomainNode(15, "Mudwick", NodeType.Village, 3, -5, 95),
        new DomainNode(16, "Wulfric's Keep", NodeType.Keep, 1, -5, 10),
    });

    private readonly Dictionary<int, DomainNode> _byId;

    public DomainMap(IReadOnlyList<DomainNode> nodes)
    {
        Nodes = nodes;
        _byId = nodes.ToDictionary(n => n.Id);
    }

    public IReadOnlyList<DomainNode> Nodes { get; }

    public DomainNode? Node(int id) => _byId.TryGetValue(id, out var node) ? node : null;
}

/// <summary>The Domain's balance (spec 2026-10-14 world map). Versioned with the village table.</summary>
public sealed record DomainTable(
    IReadOnlyDictionary<NodeType, NodeKind> Kinds,
    int UnlockHearth,
    IReadOnlyList<int> ReachByHearth,
    int MissionaryPower,
    IReadOnlyList<(int Hours, int EfficiencyPercent)> Sessions,
    int MinutesPerHex,
    ResourceBag ProvisionsPerHour,
    int PatronInfluence,
    int PatronBonusPercent,
    int PatronTensionSpike,
    int KeepTensionSpike,
    int DecayPercentPerDay,
    int OfferingsBufferHours,
    int TensionPer100WeightedInfluenceMilli,
    int TitheValuePerTension,
    int TitheRepeatHours)
{
    public static DomainTable Default { get; } = new(
        new Dictionary<NodeType, NodeKind>
        {
            [NodeType.Hamlet] = new(NodeType.Hamlet, 80, 100, new ResourceBag(30, 0, 0, 0)),
            [NodeType.Village] = new(NodeType.Village, 65, 100, new ResourceBag(20, 10, 0, 0)),
            [NodeType.MarketTown] = new(NodeType.MarketTown, 50, 150, new ResourceBag(0, 0, 8, 0)),
            [NodeType.Abbey] = new(NodeType.Abbey, 35, 200, new ResourceBag(0, 0, 0, 6)),
            [NodeType.Keep] = new(NodeType.Keep, 15, 300, default),
            [NodeType.Cathedral] = new(NodeType.Cathedral, 15, 300, default),
        },
        UnlockHearth: 2,
        ReachByHearth: new[] { 0, 3, 5, 6 },
        MissionaryPower: 5,
        Sessions: new[] { (1, 100), (2, 110), (4, 125), (8, 150) },
        MinutesPerHex: 20,
        ProvisionsPerHour: new ResourceBag(25, 0, 0, 10),
        PatronInfluence: 40,
        PatronBonusPercent: 25,
        PatronTensionSpike: 8,
        KeepTensionSpike: 15,
        DecayPercentPerDay: 2,
        OfferingsBufferHours: 8,
        TensionPer100WeightedInfluenceMilli: 2000,
        TitheValuePerTension: 20,
        TitheRepeatHours: 24);

    public int Reach(int hearthLevel) => ReachByHearth[Math.Min(Math.Max(hearthLevel, 1), ReachByHearth.Count) - 1];

    /// <summary>Missionaries out at once: one per Hearth level above the first.</summary>
    public int Missionaries(int hearthLevel) => hearthLevel < UnlockHearth ? 0 : hearthLevel - 1;

    public int? EfficiencyPercent(int hours)
    {
        foreach (var (h, e) in Sessions)
        {
            if (h == hours)
            {
                return e;
            }
        }

        return null;
    }

    /// <summary>Influence (thousandths) a session of <paramref name="hours"/> at <paramref name="node"/> brings.</summary>
    public long GainMilli(DomainNode node, int hours)
    {
        var kind = Kinds[node.Type];
        var efficiency = EfficiencyPercent(hours) ?? 0;
        // power × hours × receptivity × (0.5 + squalor/200) × efficiency, in thousandths.
        return (long)MissionaryPower * hours * 1000 * kind.ReceptivityPercent * (50 + node.Squalor / 2) * efficiency / (100L * 100 * 100);
    }

    /// <summary>A tithe's worth: Timber 1, Peat 2, Scrap 2, Devotion 4.</summary>
    public static int TitheValue(ResourceBag bag) => bag.Timber + 2 * bag.Peat + 2 * bag.Scrap + 4 * bag.Devotion;
}
